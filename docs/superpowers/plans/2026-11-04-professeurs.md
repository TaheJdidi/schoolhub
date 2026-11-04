# Les professeurs : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** la secrétaire ajoute, modifie, supprime les professeurs du centre, les marque « partis » ou les fait revenir, depuis la page « Professeurs ».

**Architecture:** un module `Professeurs` dans le projet .NET : `Professeur` est une donnée du centre (`IAppartientAuCentre`, filtre automatique), `ProfesseursDuCentre` porte les règles derrière `ProfesseursEndpoints` (6 adresses, toute personne connectée). La page Angular `Professeurs` montre la liste (filtre « Au centre » / « Professeurs partis »), la fiche du professeur choisi et le formulaire d'ajout ou de modification.

**Tech Stack:** .NET 10, EF Core avec Npgsql, PostgreSQL 17, xUnit 2.9 et Testcontainers ; Angular 22 (zoneless), Vitest.

**Spec:** [docs/superpowers/specs/2026-11-04-professeurs-design.md](../specs/2026-11-04-professeurs-design.md)

## Global Constraints

- Tout est en français, les identifiants aussi, sans accents. Les messages sont recopiés **à la lettre** de la spec.
- Les refus de l'API répondent `{ "message": "…" }` avec `ConnexionEndpoints.Refus(statut, message)`.
- Chaque lecture d'un professeur passe par `contexte.Professeurs` (filtre du centre) : un professeur d'un autre centre est « Professeur introuvable » (404).
- Le téléphone est enregistré en 8 chiffres (`TelephoneTunisien.Normaliser`) et renvoyé groupé (`TelephoneTunisien.Grouper`).
- Tests .NET depuis `backend/` : `dotnet test --artifacts-path ../.superpowers/artefacts` (Docker démarré). Tests Angular depuis `frontend/` : `npm test -- --watch=false`.
- **Le serveur de l'aperçu verrouille `bin/`** : avant `dotnet ef migrations add`, arrêter la configuration `serveur` de l'aperçu.
- Front : style de `styles.css` (`.card`, `.btn`, `.note ok/bad`, `.pill muted`) ; les messages s'affichent sous la carte concernée ; boutons bloqués pendant un envoi ; tests avec `vi.waitFor` après un `flush`.
- Commits sur `code/professeurs`, signés sans toucher à la configuration de git, terminés par la ligne Co-Authored-By :
  ```bash
  git -c user.name="Taha Jdidi" -c user.email="taha.jdidi11@gmail.com" commit -F <message>
  ```
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  ```

## Review Focus

1. **Le même téléphone écrit autrement** (« 98 401 220 » puis « +216 98401220 ») : c'est un doublon, l'avertissement nomme l'autre professeur. → tâche 2, `Un_telephone_deja_pris_previent_sans_bloquer`.
2. **Modifier un professeur sans changer son téléphone** : pas d'avertissement contre lui-même. → tâche 3, `Modifier_sans_changer_le_telephone_ne_previent_pas`.
3. **Un téléphone collé depuis WhatsApp avec des espaces insécables** (« 98 401 220 » avec U+00A0) : accepté. → tâche 1.
4. **Deux clics rapides sur « Marquer parti »** : le second répond 200 sans erreur. → tâche 3, `Un_second_depart_ne_change_rien`.
5. **Un professeur parti, ajouté de nouveau par erreur avec son téléphone** : l'avertissement le nomme, ce qui fait penser à « Revenir au centre ». → tâche 2, `Un_professeur_parti_compte_pour_le_doublon`.

---

### Task 1: Le téléphone ignore tout espace

**Files:**
- Modify: `backend/src/SchoolHub/Socle/Centres/TelephoneTunisien.cs`
- Test: `backend/tests/SchoolHub.Tests/Centres/ParametresDuCentreTests.cs` (le test du téléphone y existe déjà)

- [ ] **Step 1: Write the failing test** `Le_telephone_accepte_les_espaces_insecables` : PUT des paramètres avec `"telephone": "71 234 567"` → 200, `telephone == "71 234 567"`.
- [ ] **Step 2: Run it** : FAIL, 400 « Le téléphone doit avoir 8 chiffres ».
- [ ] **Step 3: Implement** : dans `Normaliser`, retirer tout `char.IsWhiteSpace(c)`, plus `-` et `.`.
- [ ] **Step 4: Run** `dotnet test --artifacts-path ../.superpowers/artefacts --filter ParametresDuCentreTests` : PASS.
- [ ] **Step 5: Commit** « Téléphone : les espaces insécables sont ignorés ».

### Task 2: Le professeur, la liste et l'ajout (GRP-01, GRP-02)

**Files:**
- Create: `backend/src/SchoolHub/Professeurs/Professeur.cs`, `ProfesseursDuCentre.cs`, `ProfesseursEndpoints.cs`
- Modify: `backend/src/SchoolHub/Socle/Donnees/SchoolHubDbContext.cs` (+ `DbSet<Professeur> Professeurs`), `backend/src/SchoolHub/Program.cs` (`AddScoped<ProfesseursDuCentre>()`, `api.MapProfesseurs()`)
- Create: la migration `AjouterLesProfesseurs` (depuis `backend/src/SchoolHub` : `dotnet ef migrations add AjouterLesProfesseurs -o Socle/Donnees/Migrations`)
- Modify: `backend/README.md` (le dossier `Professeurs/`)
- Test: `backend/tests/SchoolHub.Tests/Professeurs/ProfesseursTests.cs`

**Interfaces:**
- Produces :
  - `public sealed class Professeur : IAppartientAuCentre` : `Id`, `CentreId`, `Prenom`, `Nom`, `Telephone` (8 chiffres), `Email?`, `Parti` ; `const int LongueurMaximaleDuNom = 100` ; constructeur `internal Professeur(string prenom, string nom, string telephone, string? email)` ; `void Modifier(string prenom, string nom, string telephone, string? email)`, `void Partir()`, `void Revenir()`. Configuration : table `professeurs`, `Prenom`/`Nom` 100, `Telephone` 8, `Email` `AdresseEmail.LongueurMaximale`.
  - `public sealed record FicheDuProfesseur(string? Prenom, string? Nom, string? Telephone, string? Email)` : ce que la page envoie.
  - `public sealed record ProfesseurDuCentre(Guid Id, string Prenom, string Nom, string Telephone, string? Email, bool Parti, int GroupesOuverts)` avec `static De(Professeur)` (téléphone groupé, `GroupesOuverts = 0` avec le commentaire « GRP-04 les comptera »).
  - `public sealed record EnregistrementDuProfesseur(ProfesseurDuCentre Professeur, string? Avertissement)`.
  - `public sealed record IssueSurUnProfesseur(Professeur? Professeur, int Statut, string? Message, string? Avertissement = null)` avec `Reussie(...)`, `Refusee(statut, message)`, comme `IssueSurUnNiveau`.
  - `ProfesseursDuCentre` : `Task<List<Professeur>> ListerAsync()` (par `Prenom` puis `Nom`), `Task<IssueSurUnProfesseur> AjouterAsync(FicheDuProfesseur saisie)`.
  - `ProfesseursEndpoints.MapProfesseurs(this IEndpointRouteBuilder api)` : groupe `/professeurs`, `RequireAuthorization()` (toute personne connectée).

Les tests suivent `NiveauxTests` : une base `professeurs`, un centre Jasmins, Amira secrétaire connectée (`_client`).

- [ ] **Step 1: Write the failing tests**
  - `Ajouter_un_professeur` : POST `{ "Karim", "Ben Salah", "+216 98401220", "karim@exemple.tn" }` → 201, `Location` `/api/professeurs/{id}`, `Professeur == new(id, "Karim", "Ben Salah", "98 401 220", "karim@exemple.tn", false, 0)`, `Avertissement == null` ; la liste le contient.
  - `L_email_est_facultatif` : email `"  "` → 201, `Email == null`.
  - `Une_fiche_incomplete_est_refusee` (Theory) : chacun des refus 400 de la spec — prénom vide, prénom de 101 caractères, nom vide, nom de 101, téléphone vide, téléphone « 9840122 », email « karim@ » — avec son message exact ; la liste reste vide.
  - `Un_telephone_deja_pris_previent_sans_bloquer` : Karim avec « 98 401 220 », puis Leila avec « +216 98401220 » → 201, `Avertissement == "Ce téléphone est aussi celui de Karim Ben Salah"` ; la liste a 2 professeurs.
  - `Deux_doublons_sont_nommes_ensemble` : Karim et Leila ont le téléphone, Sami l'ajoute → « Ce téléphone est aussi celui de Karim Ben Salah et Leila Khelifi » (ordre prénom puis nom ; trois : « A, B et C »).
  - `Un_professeur_parti_compte_pour_le_doublon` : Karim marqué parti en base (`DansUnScopeAsync`, `Partir()`), puis ajout du même téléphone → l'avertissement nomme Karim.
  - `La_liste_est_dans_l_ordre_des_prenoms` : Sami, Karim, Leila → Karim, Leila, Sami ; chacun `GroupesOuverts == 0`.
  - `Une_personne_non_connectee_est_refusee` : GET et POST sans session → 401.
  - `Le_proprietaire_ajoute_aussi` : Hedi propriétaire → 201.
  - `Un_centre_ne_voit_pas_les_professeurs_d_un_autre` : un professeur des Oliviers (ajouté par un client des Oliviers) n'est pas dans la liste des Jasmins, et son téléphone ne donne pas d'avertissement aux Jasmins.
- [ ] **Step 2: Run them** : ne compilent pas (`Professeur`, `FicheDuProfesseur`… absents).
- [ ] **Step 3: Implement** `Professeur`, `ProfesseursDuCentre.ListerAsync/AjouterAsync`, les formes JSON, `MapProfesseurs` (GET, POST), le `DbSet`, `Program.cs`. La vérification de la fiche est une méthode privée `VerifierLaFiche(FicheDuProfesseur)` qui rend `(prenom, nom, telephone, email)` ou le refus, dans l'ordre de la spec ; l'avertissement vient d'une méthode privée `AvertissementAsync(string telephone, Guid? sauf)` qui liste les autres professeurs du centre avec ce téléphone (par prénom puis nom).
- [ ] **Step 4: Run** `--filter ProfesseursTests` : PASS. Puis toute la suite : PASS (`ModeleTests` vérifie le modèle, `SeparationDesCentresTests` le filtre).
- [ ] **Step 5: Migration** : arrêter le `serveur` de l'aperçu, `dotnet ef migrations add AjouterLesProfesseurs`, vérifier la table `professeurs` (colonnes, clé étrangère vers `centres` en `Restrict`, index sur `centre_id`) ; `ModeleTests` passe (le modèle et les migrations concordent).
- [ ] **Step 6: README** : `backend/README.md`, une ligne `Professeurs/   les professeurs du centre (GRP-01, GRP-03)` dans l'arbre.
- [ ] **Step 7: Commit** « GRP-01 : ajouter un professeur, et la liste des professeurs ».

### Task 3: Modifier, supprimer, partir, revenir (GRP-01, GRP-03)

**Files:**
- Modify: `backend/src/SchoolHub/Professeurs/ProfesseursDuCentre.cs`, `ProfesseursEndpoints.cs`
- Test: `backend/tests/SchoolHub.Tests/Professeurs/ProfesseursTests.cs`

**Interfaces:**
- Consumes: tâche 2.
- Produces : `ModifierAsync(Guid id, FicheDuProfesseur saisie)`, `SupprimerAsync(Guid id)`, `PartirAsync(Guid id)`, `RevenirAsync(Guid id)`, toutes `Task<IssueSurUnProfesseur>` ; adresses `PUT /{id:guid}` (200, `EnregistrementDuProfesseur`), `DELETE /{id:guid}` (204), `POST /{id:guid}/depart` et `/{id:guid}/retour` (200, `ProfesseurDuCentre`).

- [ ] **Step 1: Write the failing tests**
  - `Modifier_un_professeur` : PUT `{ "Karim", "Ben Salah Jr", "22 918 004", null }` → 200, la fiche modifiée, relue dans la liste.
  - `Modifier_sans_changer_le_telephone_ne_previent_pas` : PUT avec le même téléphone → `Avertissement == null`.
  - `Modifier_vers_un_telephone_pris_previent` : Leila prend le téléphone de Karim → avertissement « … celui de Karim Ben Salah ».
  - `Une_modification_incomplete_est_refusee` : prénom vide → 400 « Le prénom est obligatoire », la fiche n'a pas changé.
  - `Supprimer_un_professeur` : DELETE → 204, la liste est vide.
  - `Partir_puis_revenir` : POST `/depart` → 200, `Parti == true`, toujours dans la liste avec `parti: true` ; POST `/retour` → 200, `Parti == false`.
  - `Un_second_depart_ne_change_rien` : deux `/depart` → 200 et 200, `Parti == true` ; deux `/retour` de même.
  - `Un_professeur_inconnu_est_introuvable` (Theory sur PUT, DELETE, `/depart`, `/retour`) : `Guid.NewGuid()` → 404 « Professeur introuvable ».
  - `Un_centre_ne_touche_pas_aux_professeurs_d_un_autre` (Theory sur les 4 actions) : le professeur des Oliviers → 404, et il n'a pas changé (relu par le client des Oliviers).
- [ ] **Step 2: Run them** : FAIL (405 ou 404 sur les adresses absentes).
- [ ] **Step 3: Implement** les 4 méthodes et les 4 adresses. Une méthode privée `TrouverAsync(Guid id)` rend le professeur ou le refus 404. Commentaires, recopiés de la spec, section 4 : dans `SupprimerAsync` (« GRP-04 refusera de supprimer un professeur qui a eu un groupe »), dans `PartirAsync` (« GRP-04 refusera le départ d'un professeur qui a un groupe ouvert : « Fermez d'abord ses groupes » »).
- [ ] **Step 4: Run** toute la suite .NET : PASS.
- [ ] **Step 5: Commit** « GRP-01 et GRP-03 : modifier, supprimer, marquer parti, revenir au centre ».

### Task 4: La page « Professeurs » : liste, filtre et fiche (GRP-02, GRP-03)

**Files:**
- Create: `frontend/src/app/pages/professeurs/professeur.ts` (les formes de l'API et les textes), `professeurs.ts`, `professeurs.html`, `professeurs.css`, `professeurs.spec.ts`
- Modify: `frontend/src/app/app.routes.ts` (`professeurs: Professeurs` dans `PAGES`), `frontend/src/app/app.routes.spec.ts`, `frontend/README.md`

**Interfaces:**
- Produces, dans `professeur.ts` :
  - `interface Professeur { id; prenom; nom; telephone; email: string | null; parti: boolean; groupesOuverts: number }`
  - `interface EnregistrementDuProfesseur { professeur: Professeur; avertissement: string | null }`
  - `function groupesOuverts(n: number): string` → « Aucun groupe ouvert », « 1 groupe ouvert », « 4 groupes ouverts »
  - `function nombreDeProfesseurs(n: number): string` → « Aucun professeur », « 1 professeur », « 3 professeurs »
  - `function trier(liste: Professeur[]): Professeur[]` : par prénom puis nom, `localeCompare(…, 'fr')`
- Produces, dans `Professeurs` : les signaux `professeurs`, `filtre: 'au-centre' | 'partis'`, `choisiId`, `mode: 'fiche' | 'ajout' | 'modification'`, `envoi`, `charge`, `succes`, `avertissement`, `erreur`, `aConfirmer` ; la tâche 5 y ajoute le formulaire.

- [ ] **Step 1: Write the failing tests** (`describe('la page « Professeurs »')`, ouverte comme `carte-des-niveaux.spec.ts`, GET `/api/professeurs` flushé)
  - « liste les professeurs au centre, avec leurs groupes ouverts » : Karim et Leila au centre, Sami parti → deux boutons de liste, « Aucun groupe ouvert » ; le sous-titre « 2 professeurs » ; Karim choisi d'office (`aria-pressed="true"`) et sa fiche montre « 98 401 220 » et son email.
  - « dit quand il n'y a aucun professeur » : liste vide → « Aucun professeur pour l'instant. Ajoutez le premier. », sous-titre « Aucun professeur ».
  - « montre les professeurs partis dans leur filtre » : clic « Professeurs partis » → Sami seul, choisi, pastille « Parti », boutons `['Modifier', 'Revenir au centre', 'Supprimer']` ; sans parti : « Aucun professeur parti. ».
  - « choisit un autre professeur » : clic sur Leila → sa fiche.
  - « marque un professeur parti » : « Marquer parti » → POST `/api/professeurs/k/depart` (bouton bloqué pendant l'envoi), flush Karim `parti: true` → le filtre passe à « Professeurs partis », la fiche de Karim, « Karim Ben Salah est parti » sous la fiche ; « Au centre » ne liste plus que Leila.
  - « fait revenir un professeur » : depuis Sami parti, « Revenir au centre » → POST `/retour` → « Sami Chaabane est revenu au centre », filtre « Au centre ».
  - « supprime un professeur après confirmation » : « Supprimer » → « Supprimer Karim Ben Salah ? Seulement en cas d'erreur de saisie. » ; « Non » revient à la fiche sans requête ; « Oui » → DELETE `/api/professeurs/k` → Karim sort de la liste, Leila est choisie, « Karim Ben Salah supprimé ».
  - « montre un refus sous la fiche » : POST `/depart` répond 409 `{ message: 'Fermez d'abord ses groupes' }` → note rouge dans la carte de la fiche.
  - « bloque l'ajout si la liste n'a pas pu se charger » : GET en erreur 500 → message rouge, bouton « Ajouter un professeur » désactivé.
- [ ] **Step 2: Run them** : FAIL (le composant n'existe pas).
- [ ] **Step 3: Implement** `professeur.ts`, puis `Professeurs` : une action à la fois (`envoi`), chaque action remplace le professeur renvoyé dans la liste (`professeurs.update`), comme `Comptes.agir`. La mise en page suit la maquette : `.colonnes` (liste 260 px à gauche, fiche à droite ; une seule colonne sous 760 px), boutons de liste en `.choix` (style `.pbtn` de la maquette), filtres en `.filtres` (boutons `aria-pressed`).
- [ ] **Step 4: Adresse** : `professeurs: Professeurs` dans `PAGES` ; dans `app.routes.spec.ts`, un test « /professeurs montre la page à la secrétaire » (titre « Professeurs », plus « Cette page arrive bientôt »).
- [ ] **Step 5: Run** `npm test -- --watch=false` : PASS.
- [ ] **Step 6: README** : `frontend/README.md`, ajouter « professeurs » à la liste des pages.
- [ ] **Step 7: Commit** « GRP-02 et GRP-03 : la page Professeurs, sa liste et la fiche ».

### Task 5: Le formulaire : ajouter et modifier (GRP-01)

**Files:**
- Modify: `frontend/src/app/pages/professeurs/professeurs.ts`, `professeurs.html`, `professeurs.css`, `professeurs.spec.ts`

**Interfaces:**
- Consumes: tâche 4 (`Professeur`, `EnregistrementDuProfesseur`, `trier`, les signaux de la page).

- [ ] **Step 1: Write the failing tests**
  - « ajoute un professeur » : « Ajouter un professeur » → formulaire vide (prénom, nom, téléphone, « Email (facultatif) ») ; saisie Nadia Bouzid « 97 112 846 » → POST `/api/professeurs` avec `{ prenom: 'Nadia', nom: 'Bouzid', telephone: '97 112 846', email: '' }` → flush 201 → la fiche de Nadia, « Professeur ajouté », Nadia en dernier dans la liste (Karim, Leila, Nadia : ordre des prénoms), aucune autre requête GET.
  - « refuse une fiche incomplète » : téléphone vide → « Remplissez le prénom, le nom et le téléphone », aucune requête.
  - « prévient d'un téléphone déjà pris » : la réponse a `avertissement: 'Ce téléphone est aussi celui de Karim Ben Salah'` → une note orange (`.note.warn`) avec ce texte, sous la fiche, en plus de « Professeur ajouté ».
  - « modifie un professeur » : « Modifier » sur Karim → champs remplis ; nom changé → PUT `/api/professeurs/k` → « Fiche enregistrée », la liste montre le nouveau nom.
  - « annule le formulaire » : « Annuler » → la fiche, aucune requête.
  - « montre un refus sous le formulaire, et bloque le bouton pendant l'envoi » : pendant le POST, « Enregistrer » est désactivé ; réponse 400 `{ message: 'Le téléphone doit avoir 8 chiffres' }` → note rouge dans la carte du formulaire, formulaire toujours ouvert.
- [ ] **Step 2: Run them** : FAIL.
- [ ] **Step 3: Implement** : un `FormGroup` (`prenom`, `nom`, `telephone`, `email`), `ouvrirLAjout()`, `ouvrirLaModification()`, `annuler()`, `enregistrer()` (POST ou PUT selon `mode`), la note `.note.warn` (à ajouter dans `styles.css` : `background: var(--warn-soft); color: var(--warn)`).
- [ ] **Step 4: Run** `npm test -- --watch=false` : PASS.
- [ ] **Step 5: Commit** « GRP-01 : le formulaire d'un professeur, et l'avertissement du téléphone ».

### Task 6: Relecture, corrections, essai

- [ ] **Step 1** : relecture de toute la branche par un relecteur neuf (Agent, modèle opus) contre la spec.
- [ ] **Step 2** : chaque problème important, avec un test qui échoue d'abord ; les remarques mineures dans la mémoire `project-professeurs-reprises.md`.
- [ ] **Step 3** : `dotnet ef database update`, puis essai dans l'aperçu (secrétaire ou propriétaire du centre `essai-comptes-0311`) : ajouter deux professeurs au même téléphone, modifier, marquer parti, revenir, supprimer ; 375 px et ordinateur.
- [ ] **Step 4** : les deux suites vertes, commit, fusion locale dans `main` si tout est fini.
