# Paramètres du centre et niveaux : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** le propriétaire règle les coordonnées, l'année scolaire et les prix de son centre, et complète la liste des niveaux tunisiens, depuis la page « Paramètres du centre ».

**Architecture:** les réglages sont des colonnes du `Centre` (donnée commune, lue et écrite pour le centre courant explicitement) ; l'année scolaire et les niveaux sont des données du centre (`IAppartientAuCentre`). `ParametresDuCentre` et `NiveauxDuCentre` portent les règles, derrière `ParametresEndpoints` et `NiveauxEndpoints`. Les 29 niveaux d'office sont recopiés dans chaque centre (commande CEN-01, et migration pour les centres existants). La page Angular `Parametres` assemble deux cartes, `CarteDuCentre` et `CarteDesNiveaux`.

**Tech Stack:** .NET 10, EF Core avec Npgsql, PostgreSQL 17, xUnit 2.9 et Testcontainers ; Angular 22 (zoneless), Vitest.

**Spec:** [docs/superpowers/specs/2026-11-03-parametres-du-centre-design.md](../specs/2026-11-03-parametres-du-centre-design.md)

## Global Constraints

- Tout est en français, les identifiants aussi, sans accents. Les messages sont recopiés **à la lettre** de la spec.
- Les refus de l'API répondent `{ "message": "…" }` avec `ConnexionEndpoints.Refus(statut, message)`.
- Le centre ne passe pas par le filtre automatique : toute lecture ou écriture du centre filtre `Id == centreCourant.Id`.
- Montants : `decimal` en C#, `numeric(12,3)` en base (part du centre : `numeric(5,2)`), nombres en JSON.
- Tests .NET depuis `backend/` : `dotnet test --artifacts-path ../.superpowers/artefacts` (Docker démarré). Tests Angular depuis `frontend/` : `npm test -- --watch=false`.
- **Le serveur de l'aperçu verrouille `bin/`** : avant `dotnet ef migrations add` ou `dotnet ef database update`, arrêter la configuration `serveur` de l'aperçu (lancée par Claude) ; la relancer à la tâche 7.
- Front : aucun appel en tâche de fond, pas de bibliothèque de composants, style de `styles.css`. Les messages d'une carte s'affichent sous cette carte.
- Commits sur `code/parametres-du-centre`, signés sans toucher à la configuration de git, terminés par la ligne Co-Authored-By :
  ```bash
  git -c user.name="Taha Jdidi" -c user.email="taha.jdidi11@gmail.com" commit -F <message>
  ```
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  ```

## Review Focus

1. **Un téléphone écrit avec des tirets ou des points** (« 71-234-567 », « 71.234.567 ») : accepté comme avec des espaces, enregistré « 71234567 ». → tâche 1, `Le_telephone_accepte_espaces_tirets_points_et_indicatif`.
2. **Une année qui dure exactement un an** (du 15/09/2027 au 15/09/2028) : refusée ; jusqu'au 14/09/2028, acceptée. → tâche 2, `L_annee_dure_un_an_au_plus`.
3. **Un niveau ajouté qui porte le nom d'un niveau d'office**, en majuscules ou avec des espaces autour (« ␣9ᵉ Année de base ») : 409 « Ce niveau existe déjà ». → tâche 4, `Un_nom_deja_dans_la_liste_est_refuse`.
4. **Renommer un niveau en changeant seulement ses majuscules** (« université, 1ʳᵉ année » → « Université, 1ʳᵉ Année ») : accepté, ce n'est pas un doublon de lui-même. → tâche 4, `Renommer_en_changeant_les_majuscules_est_permis`.
5. **Un montant tapé « 1 000,500 », « 2.5 » ou « 2,5 »** dans la page : lu 1000,5 ; 2,5 ; 2,5. Une saisie comme « 2,5 TND » ou « deux » ne part pas. → tâche 5, test « lit les montants écrits à la tunisienne ».

---

### Task 1: Les réglages du centre (CEN-02)

**Files:**
- Modify: `backend/src/SchoolHub/Socle/Centres/Centre.cs`, `CentreConfiguration.cs`
- Create: `backend/src/SchoolHub/Socle/Centres/TelephoneTunisien.cs`, `ParametresDuCentre.cs`, `ParametresEndpoints.cs`
- Create: la migration `AjouterLesReglagesDuCentre`
- Modify: `backend/src/SchoolHub/Program.cs`
- Test: `backend/tests/SchoolHub.Tests/Centres/ParametresDuCentreTests.cs`

**Interfaces:**
- Produces:
  - `Centre` : `string? Telephone`, `string? Email`, `decimal? MontantDeLAssurance`, `decimal? PrixDeLEntreeALaBibliotheque`, `int? CapaciteDeLaBibliotheque`, `decimal? PartDuCentreParDefaut` (tous `private set`), et `void Regler(string nom, string? telephone, string? email, decimal? montantDeLAssurance, decimal? prixDeLEntreeALaBibliotheque, int? capaciteDeLaBibliotheque, decimal? partDuCentreParDefaut)`.
  - `public static class TelephoneTunisien { public static string? Normaliser(string saisie); public static string Grouper(string chiffres); }` : `Normaliser` retire espaces, tirets, points et un « +216 » ou « 00216 » devant, et rend les 8 chiffres ou `null` ; `Grouper("71234567") == "71 234 567"`.
  - `public sealed record ReglagesDuCentre(string? Nom, string? Telephone, string? Email, AnneeScolaireSaisie? AnneeScolaire, decimal? MontantDeLAssurance, decimal? PrixDeLEntreeALaBibliotheque, int? CapaciteDeLaBibliotheque, decimal? PartDuCentreParDefaut)`, pour la requête et la réponse.
  - `public sealed record AnneeScolaireSaisie(DateOnly? Debut, DateOnly? Fin)` (la tâche 2 la remplit ; ici, elle est lue `null`).
  - `public sealed record IssueDesReglages(ReglagesDuCentre? Reglages, string? Refus)`.
  - `public sealed class ParametresDuCentre(SchoolHubDbContext contexte, CentreCourant centreCourant)` : `Task<ReglagesDuCentre> LireAsync()` et `Task<IssueDesReglages> EnregistrerAsync(ReglagesDuCentre saisie)`.
  - `ParametresEndpoints.MapParametres(this IEndpointRouteBuilder api)` : groupe `/centre/parametres`, `RequireAuthorization(Politiques.Proprietaire)`, `GET` et `PUT` (400 avec le refus).

- [ ] **Step 1: Écrire les tests qui échouent**

`ParametresDuCentreTests` (base `centres_parametres`) : un centre « Jasmins » et Hedi propriétaire connecté, comme `GestionDesComptesTests`. Les messages sont ceux de la spec, section 1.

```csharp
[Fact] Un_nouveau_centre_n_a_que_son_nom()
// GET → ReglagesDuCentre("Centre Les Jasmins", null, null, null, null, null, null, null)

[Fact] Enregistrer_puis_relire_rend_les_memes_valeurs()
// PUT ("Centre Les Jasmins", "71 234 567", "contact@jasmins.tn", null, 20.000m, 2.500m, 40, 5m) → 200
// GET → téléphone "71 234 567", montants 20.000m et 2.500m, capacité 40, part 5m ; en base Telephone == "71234567"

[Fact] Seul_le_nom_suffit() // PUT avec tout à null sauf le nom → 200

[Theory]
[InlineData("+216 71 234 567")] [InlineData("00216 71234567")] [InlineData("71-234-567")] [InlineData("71.234.567")]
Le_telephone_accepte_espaces_tirets_points_et_indicatif(string saisie) // en base "71234567"

[Theory] // chaque refus de la spec, 400 et rien d'enregistré (le GET suivant rend l'ancien réglage)
[InlineData("nom", "", "Le nom du centre est obligatoire")]
[InlineData("telephone", "1234", "Le téléphone doit avoir 8 chiffres")]
[InlineData("email", "centre@jasmins", "Cet email n'est pas valable")]
[InlineData("assurance", "-5", "Un montant doit être positif, au millime près")]
[InlineData("assurance", "20.1234", "Un montant doit être positif, au millime près")]
[InlineData("assurance", "1000000", "Un montant doit être positif, au millime près")]
[InlineData("capacite", "0", "La capacité doit être d'au moins 1 place")]
[InlineData("part", "120", "La part du centre va de 0 à 100 %")]
[InlineData("part", "5.555", "La part du centre va de 0 à 100 %")]
Une_saisie_refusee_n_enregistre_rien(string champ, string valeur, string message)
// + un Fact pour un nom de 201 caractères : « Le nom du centre fait au plus 200 caractères »

[Fact] Une_secretaire_ne_regle_pas_le_centre() // GET et PUT → 403 « Accès réservé au propriétaire » ; sans session → 401

[Fact] Un_centre_ne_touche_jamais_aux_reglages_d_un_autre()
// Hedi enregistre ; le centre des Oliviers garde son nom et des réglages vides ; Karim (Oliviers) lit les siens
```

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `dotnet test --artifacts-path ../.superpowers/artefacts --filter "FullyQualifiedName~ParametresDuCentreTests"`
Expected: la compilation échoue (`ReglagesDuCentre` n'existe pas).

- [ ] **Step 3: Écrire les réglages, `TelephoneTunisien`, `ParametresDuCentre` et les adresses**

- Configuration : `Telephone` 8, `Email` 256 (`AdresseEmail.LongueurMaximale`), montants `HasPrecision(12, 3)`, part `HasPrecision(5, 2)`.
- Un montant est valable si `0 <= m < 1 000 000` et `decimal.Round(m, 3) == m` ; une part si `0 <= p <= 100` et `decimal.Round(p, 2) == p`.
- L'ordre de vérification est celui du tableau de la spec ; le premier refus gagne.
- Migration (serveur de l'aperçu arrêté) : `dotnet ef migrations add AjouterLesReglagesDuCentre --project src/SchoolHub --output-dir Socle/Donnees/Migrations`.

- [ ] **Step 4: Lancer tous les tests et vérifier qu'ils passent**

Run: `dotnet test --artifacts-path ../.superpowers/artefacts`
Expected: PASS, y compris `ModeleTests` (la migration suit le modèle).

- [ ] **Step 5: Commit** — « CEN-02 : régler les coordonnées et les prix du centre »

---

### Task 2: L'année scolaire (CEN-02)

**Files:**
- Create: `backend/src/SchoolHub/Socle/Centres/AnneeScolaire.cs` (l'année et sa configuration)
- Modify: `backend/src/SchoolHub/Socle/Centres/ParametresDuCentre.cs`, `SchoolHubDbContext.cs` (`DbSet`)
- Create: la migration `AjouterLesAnneesScolaires`
- Test: `backend/tests/SchoolHub.Tests/Centres/ParametresDuCentreTests.cs`, `backend/tests/SchoolHub.Tests/Socle/SeparationDesCentresTests.cs` (ou un test d'isolation dans `ParametresDuCentreTests`)

**Interfaces:**
- Consumes: `ReglagesDuCentre`, `AnneeScolaireSaisie`, `ParametresDuCentre` (tâche 1).
- Produces: `public sealed class AnneeScolaire : IAppartientAuCentre` avec `Guid Id`, `Guid CentreId`, `DateOnly Debut`, `DateOnly Fin`, un constructeur `internal AnneeScolaire(DateOnly debut, DateOnly fin)` et `void Changer(DateOnly debut, DateOnly fin)` ; `SchoolHubDbContext.AnneesScolaires`.

- [ ] **Step 1: Écrire les tests qui échouent**

```csharp
[Fact] La_premiere_annee_est_creee_puis_modifiee()
// PUT avec (2027-09-15, 2028-06-30) → GET rend la même année ; PUT avec (2027-09-16, 2028-06-30) → toujours une seule ligne en base, nouvelles dates

[Fact] Une_annee_vide_n_efface_pas_l_annee_existante() // PUT avec AnneeScolaire null → la réponse et le GET rendent l'année enregistrée

[Theory]
[InlineData("2027-09-15", null, "L'année scolaire doit avoir une date de début et une date de fin")]
[InlineData("2028-06-30", "2027-09-15", "La fin de l'année scolaire doit venir après son début")]
[InlineData("2027-09-15", "2027-09-15", "La fin de l'année scolaire doit venir après son début")]
Une_annee_mal_saisie_est_refusee(string? debut, string? fin, string message)

[Theory]
[InlineData("2028-09-15", false)] [InlineData("2028-09-14", true)]
L_annee_dure_un_an_au_plus(string fin, bool acceptee) // début 2027-09-15 ; refus « L'année scolaire dure un an au plus »

[Fact] Un_centre_ne_voit_pas_l_annee_d_un_autre() // l'année des Jasmins n'apparaît jamais dans les réglages des Oliviers
```

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `dotnet test --artifacts-path ../.superpowers/artefacts --filter "FullyQualifiedName~ParametresDuCentreTests"`
Expected: la compilation échoue (`AnneeScolaire` n'existe pas), puis, une fois le type créé, les tests de l'année échouent (année lue `null`).

- [ ] **Step 3: Écrire `AnneeScolaire` et son enregistrement**

- Table `annees_scolaires`, colonnes `date`.
- « Un an au plus » : `fin < debut.AddYears(1)`.
- L'année lue est la plus récente : `OrderByDescending(a => a.Debut).FirstOrDefault()`.
- Migration (serveur arrêté) : `AjouterLesAnneesScolaires`.

- [ ] **Step 4: Lancer tous les tests et vérifier qu'ils passent**

Run: `dotnet test --artifacts-path ../.superpowers/artefacts`
Expected: PASS

- [ ] **Step 5: Commit** — « CEN-02 : l'année scolaire du centre »

---

### Task 3: Les niveaux d'office et leur liste (CEN-03)

**Files:**
- Create: `backend/src/SchoolHub/Socle/Centres/Niveau.cs` (le niveau, sa configuration, `NiveauxDOffice`)
- Create: `backend/src/SchoolHub/Socle/Centres/NiveauxDuCentre.cs`, `NiveauxEndpoints.cs`
- Modify: `backend/src/SchoolHub/Socle/Centres/CreationDeCentre.cs`, `SchoolHubDbContext.cs`, `Program.cs`
- Create: la migration `AjouterLesNiveaux`
- Modify: `backend/tests/SchoolHub.Tests/Outils/ApplicationDeTest.cs`
- Test: `backend/tests/SchoolHub.Tests/Centres/NiveauxTests.cs`, `CreationDeCentreTests.cs`, `Socle/ModeleTests.cs` (`MigrationsTests`)

**Interfaces:**
- Produces:
  - `public sealed class Niveau : IAppartientAuCentre` : `Guid Id`, `Guid CentreId`, `string Nom`, `string NomNormalise` (`Nom.ToLowerInvariant()`), `bool DOffice`, `int? Rang` ; constructeur `internal Niveau(string nom, bool dOffice, int? rang)` ; `void Renommer(string nom)` (tâche 4). Index unique `(CentreId, NomNormalise)`.
  - `public static class NiveauxDOffice { public static IReadOnlyList<string> Liste; public static string SqlDeRecopie(); }` : la liste des 29 noms, dans l'ordre de la spec (section 2), écrits « 4ᵉ année secondaire, section mathématiques » ; `SqlDeRecopie()` rend l'`INSERT … SELECT … FROM centres CROSS JOIN (VALUES …)` qui donne les 29 niveaux à chaque centre (`gen_random_uuid()`, nom, nom normalisé calculé en C#, `d_office` vrai, rang 1 à 29 ; apostrophes doublées).
  - `public sealed record NiveauDuCentre(Guid Id, string Nom, bool DOffice)`.
  - `public sealed class NiveauxDuCentre(SchoolHubDbContext contexte)` : `Task<List<Niveau>> ListerAsync()` (d'office par rang, puis ajoutés par nom) et `void RecopierLesNiveauxDOffice()` (ajoute les 29 au contexte, sans enregistrer).
  - `NiveauxEndpoints.MapNiveaux(this IEndpointRouteBuilder api)` : `GET /niveaux` pour toute personne connectée.
  - `ApplicationDeTest.RecopierLesNiveauxDOfficeAsync(Centre centre)`.

- [ ] **Step 1: Écrire les tests qui échouent**

```csharp
// NiveauxTests (base centres_niveaux) : Jasmins avec ses niveaux d'office recopiés, Hedi propriétaire connecté
[Fact] La_liste_contient_les_29_niveaux_d_office_dans_l_ordre_scolaire()
// GET /api/niveaux → 29 éléments, tous DOffice ; [0] "1ʳᵉ année de base", [9] "1ʳᵉ année secondaire",
// [10] "2ᵉ année secondaire, section sciences", [28] "4ᵉ année secondaire, section sport"

[Fact] Une_secretaire_lit_la_liste() // 200 et 29 éléments ; sans session → 401

[Fact] Un_centre_ne_voit_pas_les_niveaux_d_un_autre() // un niveau ajouté aux Oliviers (en base) n'apparaît pas aux Jasmins

// CreationDeCentreTests
[Fact] La_commande_donne_les_29_niveaux_d_office_au_centre()
// les noms des niveaux du centre créé (lus avec IgnoreQueryFilters et CentreId == centre.Id, par rang) == NiveauxDOffice.Liste
// et dans Si_l_invitation_ne_part_pas_rien_n_est_cree : aucun niveau pour ce code (IgnoreQueryFilters)

// MigrationsTests
[Fact] La_migration_donne_les_niveaux_d_office_aux_centres_existants()
// base « migrations_niveaux » : IMigrator.MigrateAsync("AjouterLesAnneesScolaires"), INSERT d'un centre en SQL,
// puis MigrateAsync() → 29 niveaux d'office pour ce centre
```

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `dotnet test --artifacts-path ../.superpowers/artefacts --filter "FullyQualifiedName~NiveauxTests|FullyQualifiedName~CreationDeCentreTests|FullyQualifiedName~MigrationsTests"`
Expected: la compilation échoue (`Niveau`, `NiveauxDOffice` n'existent pas).

- [ ] **Step 3: Écrire le niveau, la liste d'office, la recopie et l'adresse de lecture**

- `CreationDeCentre` : juste après `centreCourant.Definir(centre.Id)`, `niveaux.RecopierLesNiveauxDOffice()` ; ils partent avec le `SaveChangesAsync` du journal.
- Migration (serveur arrêté) : `AjouterLesNiveaux`, puis `migrationBuilder.Sql(NiveauxDOffice.SqlDeRecopie())` à la fin de `Up`, avec le commentaire « si la liste change un jour, la nouvelle migration recopiera ici l'ancienne liste ».
- `ApplicationDeTest.CreerCentreAsync` ne change pas : les tests qui ont besoin des niveaux appellent `RecopierLesNiveauxDOfficeAsync`.

- [ ] **Step 4: Lancer tous les tests et vérifier qu'ils passent**

Run: `dotnet test --artifacts-path ../.superpowers/artefacts`
Expected: PASS

- [ ] **Step 5: Commit** — « CEN-03 : les 29 niveaux d'office, recopiés dans chaque centre »

---

### Task 4: Ajouter, renommer et retirer un niveau (CEN-03)

**Files:**
- Modify: `backend/src/SchoolHub/Socle/Centres/Niveau.cs`, `NiveauxDuCentre.cs`, `NiveauxEndpoints.cs`
- Test: `backend/tests/SchoolHub.Tests/Centres/NiveauxTests.cs`

**Interfaces:**
- Consumes: `Niveau`, `NiveauDuCentre`, `NiveauxDuCentre`, `NiveauxEndpoints` (tâche 3). `IssueSurUnNiveau` a la même forme que `IssueDeLAction` des comptes.
- Produces:
  - `public sealed record IssueSurUnNiveau(Niveau? Niveau, int Statut, string? Message)` avec `Reussie(Niveau, int statut = 200)` et `Refusee(int, string)`.
  - `NiveauxDuCentre` : `Task<IssueSurUnNiveau> AjouterAsync(string? nom)` (201), `Task<IssueSurUnNiveau> RenommerAsync(Guid id, string? nom)` (200), `Task<IssueSurUnNiveau> RetirerAsync(Guid id)` (204, `Niveau` rendu pour le test, réponse sans corps).
  - `POST /niveaux`, `PUT /niveaux/{id:guid}`, `DELETE /niveaux/{id:guid}`, avec `RequireAuthorization(Politiques.Proprietaire)` ; corps `{ "nom": "…" }` (`public sealed record NomDuNiveau(string? Nom)`).

- [ ] **Step 1: Écrire les tests qui échouent**

```csharp
[Fact] Ajouter_un_niveau() // POST "Université, 1ʳᵉ année" → 201 NiveauDuCentre(id, "Université, 1ʳᵉ année", false) ; GET : en 30ᵉ position

[Fact] Les_niveaux_ajoutes_suivent_les_niveaux_d_office_par_nom() // "Université, 2ᵉ année" puis "Adultes, remise à niveau" → [29] Adultes, [30] Université

[Theory]
[InlineData("  ", "Le nom du niveau est obligatoire")]
Un_nom_vide_est_refuse(...) // + Fact : 101 caractères → « Le nom du niveau fait au plus 100 caractères »

[Theory]
[InlineData(" 9ᵉ Année de base ")] [InlineData("UNIVERSITÉ, 1ʳᵉ ANNÉE")]
Un_nom_deja_dans_la_liste_est_refuse(string nom) // après l'ajout de « Université, 1ʳᵉ année » : 409 « Ce niveau existe déjà »

[Fact] Renommer_un_niveau_ajoute() // PUT → 200, nouveau nom ; renommer vers un autre nom existant → 409 « Ce niveau existe déjà »

[Fact] Renommer_en_changeant_les_majuscules_est_permis() // « université, 1ʳᵉ année » → « Université, 1ʳᵉ Année » : 200

[Fact] Retirer_un_niveau_ajoute() // DELETE → 204 ; GET : 29 niveaux

[Theory] [InlineData("PUT")] [InlineData("DELETE")]
Un_niveau_d_office_ne_se_modifie_pas(string methode) // 409 « Les niveaux d'office ne se modifient pas » ; la liste n'a pas changé

[Theory] [InlineData("PUT")] [InlineData("DELETE")]
Un_niveau_d_un_autre_centre_est_introuvable(string methode) // 404 « Niveau introuvable »

[Fact] Une_secretaire_ne_modifie_pas_les_niveaux() // POST, PUT, DELETE → 403 « Accès réservé au propriétaire »
```

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `dotnet test --artifacts-path ../.superpowers/artefacts --filter "FullyQualifiedName~NiveauxTests"`
Expected: FAIL (les adresses de modification n'existent pas).

- [ ] **Step 3: Écrire l'ajout, le renommage et le retrait**

Le doublon se cherche sur `NomNormalise` dans le centre (en excluant le niveau renommé lui-même). Une `DbUpdateException` dont l'erreur PostgreSQL est `23505` (deux ajouts simultanés) donne aussi le 409 ; toute autre erreur remonte.

- [ ] **Step 4: Lancer tous les tests et vérifier qu'ils passent**

Run: `dotnet test --artifacts-path ../.superpowers/artefacts`
Expected: PASS

- [ ] **Step 5: Commit** — « CEN-03 : ajouter, renommer et retirer un niveau »

---

### Task 5: La page « Paramètres du centre » : la carte du centre

**Files:**
- Create: `frontend/src/app/socle/montants.ts` (+ `montants.spec.ts`)
- Create: `frontend/src/app/pages/parametres/parametres.ts` (la page), `carte-du-centre.ts`, `carte-du-centre.html`, `parametres.css`, `carte-du-centre.spec.ts`
- Modify: `frontend/src/app/app.routes.ts` (`PAGES`), `frontend/src/app/app.routes.spec.ts`

**Interfaces:**
- Consumes: `GET` et `PUT /api/centre/parametres` (tâches 1 et 2) ; `messageDErreur`.
- Produces:
  - `lireUnNombre(saisie: string): number | null | undefined` : `null` pour une saisie vide, `undefined` pour une saisie illisible ; retire les espaces, accepte la virgule ou le point.
  - `ecrireUnMontant(montant: number | null): string` : `20` → « 20,000 », `2.5` → « 2,500 », `null` → « ».
  - `interface Reglages` (le JSON de la spec), et les composants `Parametres` (`sh-parametres`) et `CarteDuCentre` (`sh-carte-du-centre`).

- [ ] **Step 1: Écrire les tests qui échouent**

```ts
// montants.spec.ts
it('lit les montants écrits à la tunisienne') // "1 000,500" → 1000.5 ; "2.5" → 2.5 ; "2,5" → 2.5 ; "" → null ; "2,5 TND" et "deux" → undefined
it('écrit les montants au millime') // 20 → "20,000" ; 2.5 → "2,500" ; null → ""

// carte-du-centre.spec.ts (GET /api/centre/parametres répondu au chargement, vi.waitFor comme comptes.spec.ts)
it('remplit le formulaire avec les réglages') // nom, téléphone "71 234 567", assurance "20,000", part "5", dates
it('envoie les montants en nombre et les champs vides en null')
// PUT body : { nom, telephone: null, email: null, anneeScolaire: { debut: '2027-09-15', fin: '2028-06-30' },
//   montantDeLAssurance: 20, prixDeLEntreeALaBibliotheque: 2.5, capaciteDeLaBibliotheque: null, partDuCentreParDefaut: 5 }
it('une seule date part avec l\'autre à null, et deux dates vides partent en null')
it('un montant illisible ne part pas') // « Écrivez un montant, par exemple 2,500 », aucune requête
it('rappelle qu\'il faut une année scolaire') // anneeScolaire null → « Sans année scolaire, on ne peut pas créer de groupe d'Étude. »
it('confirme sous la carte, et montre le refus sous la carte') // « Paramètres enregistrés » ; 400 « Le téléphone doit avoir 8 chiffres »
it('le bouton se bloque pendant l\'envoi') // « Enregistrement… », disabled, une seule requête

// app.routes.spec.ts
it('le propriétaire voit « Paramètres du centre » sur /parametres') // GET /api/centre/parametres et /api/niveaux répondus
it('une secrétaire lit « Accès réservé au propriétaire » sur /parametres')
```

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `npm test -- --watch=false`
Expected: FAIL, `./montants` et `./carte-du-centre` introuvables.

- [ ] **Step 3: Écrire `montants.ts`, la page et la carte du centre**

- La capacité se lit avec `lireUnNombre` et doit être entière ; sinon « Écrivez un nombre de places, par exemple 40 ». La part illisible : « Écrivez un pourcentage, par exemple 5 ». La page n'applique pas les autres règles : le serveur répond.
- La page `Parametres` : en-tête « Paramètres du centre » et « Les informations et les prix de votre centre. », puis `<sh-carte-du-centre />` et `<sh-carte-des-niveaux />` (la tâche 6 crée la seconde ; d'ici là, la page n'a que la première).
- Après l'enregistrement, le formulaire prend les réglages renvoyés (téléphone groupé, montants au millime).

- [ ] **Step 4: Lancer les tests et vérifier qu'ils passent**

Run: `npm test -- --watch=false`, puis `npm run build`
Expected: PASS, et la construction réussit.

- [ ] **Step 5: Commit** — « CEN-02 : la page Paramètres du centre »

---

### Task 6: La carte des niveaux

**Files:**
- Create: `frontend/src/app/pages/parametres/carte-des-niveaux.ts`, `carte-des-niveaux.html`, `carte-des-niveaux.spec.ts`
- Modify: `frontend/src/app/pages/parametres/parametres.ts`

**Interfaces:**
- Consumes: les 4 adresses des niveaux (tâches 3 et 4).
- Produces: `interface Niveau { id: string; nom: string; dOffice: boolean }` et le composant `CarteDesNiveaux` (`sh-carte-des-niveaux`).

- [ ] **Step 1: Écrire les tests qui échouent**

```ts
// GET /api/niveaux répondu avec 29 d'office et « Université, 1ʳᵉ année »
it('montre les niveaux ajoutés, et les niveaux d\'office repliés') // details fermé ; résumé « Les 29 niveaux d'office (ils ne se modifient pas) »
it('dit quand aucun niveau n\'est ajouté') // « Aucun niveau ajouté »
it('ajoute un niveau, et un nom vide ne part pas') // POST { nom: 'Adultes' } → 201 → GET rechargé → « Niveau ajouté »
it('renomme, ou annule') // Renommer → champ ; Annuler → nom inchangé, aucune requête ; Enregistrer → PUT → « Niveau renommé »
it('retire après « Oui », et « Non » n\'envoie rien') // « Retirer « Université, 1ʳᵉ année » ? » ; DELETE → ligne disparue, « Niveau retiré »
it('montre un refus sous la carte') // 409 « Ce niveau existe déjà »
```

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `npm test -- --watch=false`
Expected: FAIL, `./carte-des-niveaux` introuvable.

- [ ] **Step 3: Écrire la carte des niveaux et l'ajouter à la page**

Après un ajout, la liste est rechargée ; après un renommage, la ligne prend le niveau renvoyé ; après un retrait, la ligne disparaît. Une seule action à la fois : pendant l'envoi, tous les boutons de la carte sont bloqués (leçon de la relecture de « Comptes »).

- [ ] **Step 4: Lancer les tests et vérifier qu'ils passent**

Run: `npm test -- --watch=false`, puis `npm run build`
Expected: PASS

- [ ] **Step 5: Commit** — « CEN-03 : la carte des niveaux »

---

### Task 7: Documentation et essai dans l'application

**Files:**
- Modify: `backend/README.md` (organisation : réglages, années scolaires, niveaux), `frontend/README.md` (la page `parametres`)

- [ ] **Step 1: Mettre la documentation à jour**
- [ ] **Step 2: Lancer tous les tests** — `dotnet test --artifacts-path ../.superpowers/artefacts` et `npm test -- --watch=false` : tout passe.
- [ ] **Step 3: Appliquer les migrations et relancer le serveur de l'aperçu** — `dotnet ef database update --project src/SchoolHub` : chaque centre existant reçoit 29 niveaux (`select centre_id, count(*) from niveaux group by centre_id`).
- [ ] **Step 4: Jouer la spec à la main** (section « À la main » de la spec), connecté avec le propriétaire du centre de test « essai-comptes-0311 », sur ordinateur et en 375 pixels, en clair et en sombre.
- [ ] **Step 5: Commit** — « Paramètres du centre et niveaux : documentation »
