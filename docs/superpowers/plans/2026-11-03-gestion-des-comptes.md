# Gestion des comptes et journal : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** le propriétaire invite, désactive et réactive les comptes de son centre depuis la page « Comptes », et chaque création, désactivation et réactivation laisse une ligne au journal, que personne ne peut modifier ni effacer.

**Architecture:** le journal est une donnée du centre (`LigneDuJournal`, table `journal`), écrite explicitement par `JournalDuCentre.Noter` dans la transaction de l'action, et protégée par un trigger PostgreSQL. Les règles des comptes vivent dans `GestionDesComptes`, derrière 5 adresses réservées au propriétaire (`ComptesEndpoints`). La page Angular `Comptes` remplace « bientôt » à l'adresse `/comptes`.

**Tech Stack:** .NET 10, ASP.NET Core Identity, EF Core avec Npgsql, PostgreSQL 17, xUnit 2.9 et Testcontainers ; Angular 22, Vitest.

**Spec:** [docs/superpowers/specs/2026-11-03-gestion-des-comptes-design.md](../specs/2026-11-03-gestion-des-comptes-design.md)

## Global Constraints

- Tout est en français, les identifiants aussi, sans accents (`Desactive`, `Reactiver`). Les messages de l'API et de la page sont recopiés **à la lettre** de la spec.
- Les refus de l'API répondent `{ "message": "…" }` avec `ConnexionEndpoints.Refus(statut, message)`.
- Les comptes ne passent pas par le filtre automatique : toute lecture filtre explicitement `CentreId == centreCourant.Id`.
- Les tests .NET se lancent depuis `backend/` avec `dotnet test` (Docker démarré). Les tests Angular depuis `frontend/` avec `npm test -- --watch=false`.
- **Le serveur de développement verrouille `SchoolHub.dll`** : arrêter le serveur lancé par l'aperçu (configuration `serveur` de `.claude/launch.json`) avant `dotnet build`, `dotnet test` ou `dotnet ef`, et le relancer à la tâche 7.
- Front : aucun appel au serveur en tâche de fond, pas de bibliothèque de composants, le style vient de `styles.css` et de la maquette.
- Commits : sur la branche `code/gestion-des-comptes`, signés sans toucher à la configuration de git, et terminés par la ligne Co-Authored-By :
  ```bash
  git -c user.name="Taha Jdidi" -c user.email="taha.jdidi11@gmail.com" commit -F <message>
  ```
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  ```

## Review Focus

1. **Un email tapé avec des espaces et des majuscules** (« ␣Sonia.Mabrouk@Exemple.tn␣ ») : il est enregistré sans les espaces, et il bloque ensuite « sonia.mabrouk@exemple.tn », puisque c'est le même email. → tâche 2, `Inviter_retire_les_espaces_et_l_email_en_majuscules_est_deja_pris`.
2. **Un prénom ou un nom de plus de 100 caractères** : sans vérification, la base répondrait 500. La réponse doit être 400 avec « Le prénom et le nom font au plus 100 caractères ». → tâche 2, `Un_prenom_trop_long_est_refuse`.
3. **Un rôle inconnu** (`"Admin"`, `"proprietaire"`) : jamais un compte créé avec un rôle par défaut. → tâche 2, `Un_role_inconnu_est_refuse`.
4. **Un compte invité, désactivé puis réactivé** : l'ancien lien d'invitation reste mort, et seul « Renvoyer l'invitation » en donne un nouveau. → tâche 4, `Un_invite_desactive_puis_reactive_garde_son_ancien_lien_mort`.
5. **Un double clic sur « Envoyer l'invitation »** : une seule requête doit partir. → tâche 6, test « le bouton se bloque pendant l'envoi ».

---

### Task 1: Le journal

**Files:**
- Create: `backend/src/SchoolHub/Socle/Journal/LigneDuJournal.cs` (la ligne, `ActionDuJournal`, la configuration EF Core)
- Create: `backend/src/SchoolHub/Socle/Journal/JournalDuCentre.cs`
- Create: la migration `AjouterLeJournal`, dans `backend/src/SchoolHub/Socle/Donnees/Migrations/`
- Modify: `backend/src/SchoolHub/Socle/Donnees/SchoolHubDbContext.cs` (le `DbSet`)
- Modify: `backend/src/SchoolHub/Program.cs` (l'enregistrement du service)
- Modify: `backend/tests/SchoolHub.Tests/Outils/ApplicationDeTest.cs` (le trigger dans la base des tests, et deux aides)
- Test: `backend/tests/SchoolHub.Tests/Socle/JournalTests.cs`
- Test: `backend/tests/SchoolHub.Tests/Socle/ModeleTests.cs` (classe `MigrationsTests`)

**Interfaces:**
- Produces:
  - `public enum ActionDuJournal { CompteCree, CompteDesactive, CompteReactive }`, dans `SchoolHub.Socle.Journal`.
  - `public sealed class LigneDuJournal : IAppartientAuCentre`, avec `Guid Id`, `Guid CentreId`, `DateTimeOffset Quand`, `Guid? AuteurId`, `string Auteur`, `ActionDuJournal Action`, `string Detail`, `string? Avant`, `string? Apres`, toutes en `private set`, et un constructeur `internal`.
  - `public sealed class JournalDuCentre(SchoolHubDbContext contexte, TimeProvider temps)`, avec :
    - `public const string AuteurSansCompte = "SchoolHub";`
    - `public const string SqlDeLaProtection` : le SQL du trigger ci-dessous ;
    - `public void Noter(Compte? auteur, ActionDuJournal action, string detail, string? avant = null, string? apres = null)` : ajoute la ligne au contexte, **sans** `SaveChangesAsync`.
  - `SchoolHubDbContext.LignesDuJournal` (`DbSet<LigneDuJournal>`).
  - Dans `ApplicationDeTest` : `Task<List<LigneDuJournal>> JournalAsync(Centre centre)` (lit avec le centre courant défini, triée par `Quand` puis `Id`) et `Task<HttpClient> ClientConnecteAsync(Compte compte, string motDePasse = "jasmins-2027")` (connecte un nouveau client et vérifie le 200).

- [ ] **Step 1: Écrire les tests du journal qui échouent**

`JournalTests` suit le modèle d'`InvitationTests` (base `socle_journal`, `ApplicationDeTest`, deux centres « Jasmins » et « Oliviers »). Chaque test note une ligne dans un scope : `CentreCourant.Definir(centre.Id)`, `JournalDuCentre.Noter(...)`, puis `SaveChangesAsync`.

```csharp
[Fact] Une_ligne_garde_l_heure_l_auteur_et_le_centre()
// Noter(hedi, CompteDesactive, "Sonia Mabrouk · Secrétaire", "Actif", "Désactivé")
var ligne = Assert.Single(await _application.JournalAsync(_jasmins));
Assert.Equal(_jasmins.Id, ligne.CentreId);
Assert.Equal(hedi.Id, ligne.AuteurId);
Assert.Equal("Hedi Ben Salah", ligne.Auteur);
Assert.Equal(ActionDuJournal.CompteDesactive, ligne.Action);
Assert.Equal("Sonia Mabrouk · Secrétaire", ligne.Detail);
Assert.Equal(("Actif", "Désactivé"), (ligne.Avant, ligne.Apres));
// PostgreSQL garde la microseconde, l'horloge de test le dixième de microseconde.
Assert.True((ligne.Quand - _application.Horloge.GetUtcNow()).Duration() < TimeSpan.FromMilliseconds(1));

[Fact] Sans_auteur_la_ligne_est_ecrite_par_SchoolHub()
// Noter(null, CompteCree, "…") → AuteurId null, Auteur "SchoolHub", Avant et Apres null

[Fact] Un_centre_ne_lit_jamais_le_journal_d_un_autre()
// une ligne aux Jasmins, une aux Oliviers → JournalAsync(_jasmins) ne contient que celle des Jasmins

[Theory]
[InlineData("UPDATE journal SET detail = 'effacé'")]
[InlineData("DELETE FROM journal")]
[InlineData("TRUNCATE journal")]
Le_journal_refuse_d_etre_modifie_ou_efface(string sql)
// après une ligne notée :
var erreur = await Assert.ThrowsAsync<PostgresException>(() => contexte.Database.ExecuteSqlRawAsync(sql));
Assert.Equal("Le journal ne se modifie pas et ne s'efface pas", erreur.MessageText);
// et la ligne est toujours là, intacte
```

Dans `MigrationsTests` (fichier `ModeleTests.cs`), ajouter :

```csharp
[Fact] Les_migrations_protegent_le_journal()
// base « migrations_journal », MigrateAsync, puis :
var erreur = await Assert.ThrowsAsync<PostgresException>(() => contexte.Database.ExecuteSqlRawAsync("TRUNCATE journal"));
Assert.Equal("Le journal ne se modifie pas et ne s'efface pas", erreur.MessageText);
```

(`TRUNCATE` déclenche le trigger même sur une table vide ; `UPDATE` et `DELETE` ne le feraient pas.)

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `dotnet test --filter "FullyQualifiedName~JournalTests|FullyQualifiedName~MigrationsTests"`
Expected: la compilation échoue (`JournalDuCentre`, `LigneDuJournal` n'existent pas).

- [ ] **Step 3: Écrire `LigneDuJournal`, sa configuration et `JournalDuCentre`**

- Configuration (`internal sealed class LigneDuJournalConfiguration : IEntityTypeConfiguration<LigneDuJournal>`) : table `journal` ; `Auteur` 201 caractères ; `Action` en texte (`HasConversion<string>()`, 30 caractères) ; `Detail` 500 ; `Avant` et `Apres` 100 ; `AuteurId` vers `Compte` avec `DeleteBehavior.Restrict`. La clé étrangère vers le centre et le filtre viennent déjà de `SeparerLesCentres`.
- `Id = Guid.CreateVersion7()`, `Quand = temps.GetUtcNow()`, `Auteur = auteur is null ? AuteurSansCompte : $"{auteur.Prenom} {auteur.Nom}"`. `CentreId` est rempli par `SaveChanges` (centre courant).
- `SqlDeLaProtection`, exactement :

```sql
CREATE FUNCTION journal_refuser_les_changements() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'Le journal ne se modifie pas et ne s''efface pas';
END
$$;
CREATE TRIGGER journal_ni_modification_ni_suppression BEFORE UPDATE OR DELETE ON journal
    FOR EACH ROW EXECUTE FUNCTION journal_refuser_les_changements();
CREATE TRIGGER journal_pas_de_vidage BEFORE TRUNCATE ON journal
    FOR EACH STATEMENT EXECUTE FUNCTION journal_refuser_les_changements();
```

- `SchoolHubDbContext` : `public DbSet<LigneDuJournal> LignesDuJournal => Set<LigneDuJournal>();`
- `Program.cs` : `builder.Services.AddScoped<JournalDuCentre>();`
- `ApplicationDeTest.InitialiserLaBaseAsync` : si `EnsureCreatedAsync()` rend `true`, exécuter `JournalDuCentre.SqlDeLaProtection` (un commentaire dit pourquoi : `EnsureCreated` ignore les migrations).

- [ ] **Step 4: Créer la migration et y ajouter le trigger**

Run (serveur arrêté) : `dotnet ef migrations add AjouterLeJournal --project src/SchoolHub --output-dir Socle/Donnees/Migrations`

Puis dans la migration : `migrationBuilder.Sql(JournalDuCentre.SqlDeLaProtection);` à la fin de `Up`, et `migrationBuilder.Sql("DROP FUNCTION journal_refuser_les_changements();");` après le `DropTable` de `Down`. Un commentaire dans la migration : si le trigger change un jour, la nouvelle migration recopie l'ancienne valeur ici, pour figer celle-ci.

- [ ] **Step 5: Lancer les tests et vérifier qu'ils passent**

Run: `dotnet test`
Expected: tout passe, y compris `ModeleTests` (la ligne appartient à un centre, elle est filtrée, et la migration suit le modèle).

- [ ] **Step 6: Commit** — « Journal : la ligne du journal, écrite dans la transaction de l'action et protégée par un trigger »

---

### Task 2: Lister et inviter (CPT-04)

**Files:**
- Create: `backend/src/SchoolHub/Socle/Comptes/AdresseEmail.cs` (la règle « email valable », sortie de `CreationDeCentre`)
- Create: `backend/src/SchoolHub/Socle/Comptes/GestionDesComptes.cs`
- Create: `backend/src/SchoolHub/Socle/Comptes/ComptesEndpoints.cs`
- Modify: `backend/src/SchoolHub/Socle/Centres/CreationDeCentre.cs` (utilise `AdresseEmail.EstValable`)
- Modify: `backend/src/SchoolHub/Program.cs` (`AddScoped<GestionDesComptes>()`, `api.MapComptes()`)
- Test: `backend/tests/SchoolHub.Tests/Comptes/GestionDesComptesTests.cs`

**Interfaces:**
- Consumes: `JournalDuCentre.Noter`, `ApplicationDeTest.JournalAsync`, `ApplicationDeTest.ClientConnecteAsync` (tâche 1) ; `Invitations.EnvoyerAsync(Compte, Centre, CancellationToken)` ; `ConnexionEndpoints.Refus`.
- Produces:
  - `public static class AdresseEmail { public static bool EstValable(string email) }` : la règle actuelle de `CreationDeCentre.EmailValable`, déplacée telle quelle.
  - `public sealed record CompteDuCentre(Guid Id, string Prenom, string Nom, string Email, string Role, string Etat, bool Moi)` : `Role` et `Etat` en texte de l'énumération (`"Secretaire"`, `"Invite"`).
  - `public sealed record NouvelleInvitation(string? Prenom, string? Nom, string? Email, string? Role)`.
  - `public sealed record IssueDeLAction(Compte? Compte, int Statut, string? Message)` avec `static Reussie(Compte)` et `static Refusee(int statut, string message)`.
  - `public static class LibellesDesComptes { static string De(RoleDuCompte); static string De(EtatDuCompte) }` : « Propriétaire », « Secrétaire », « Actif », « Invité », « Désactivé ».
  - `public sealed class GestionDesComptes(SchoolHubDbContext contexte, UserManager<Compte> comptes, Invitations invitations, JournalDuCentre journal, CentreCourant centreCourant)` avec `Task<List<Compte>> ListerAsync()` et `Task<IssueDeLAction> InviterAsync(Compte auteur, NouvelleInvitation invitation, CancellationToken annulation = default)`.
  - `ComptesEndpoints.MapComptes(this IEndpointRouteBuilder api)` : un groupe `/comptes` avec `RequireAuthorization(Politiques.Proprietaire)`. L'auteur est `await comptes.GetUserAsync(utilisateur)`.

- [ ] **Step 1: Écrire les tests qui échouent**

`GestionDesComptesTests` (base `comptes_gestion`) : à chaque test, un centre « Jasmins » avec Hedi Ben Salah (propriétaire actif, via `CreerCompteAsync`) et un client connecté en Hedi. Les messages attendus sont ceux de la spec, à la lettre.

```csharp
[Fact] Inviter_cree_un_compte_invite_envoie_l_email_et_note_le_journal()
// POST /api/comptes { "Sonia", "Mabrouk", "sonia.<unique>@exemple.tn", "Secretaire" }
Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
// la réponse : CompteDuCentre(id, "Sonia", "Mabrouk", email, "Secretaire", "Invite", Moi: false)
// en base : Etat Invite, CentreId des Jasmins, Role Secretaire
// un email à cette adresse, dont le lien ouvre GET /api/invitation (200)
// le journal : une ligne CompteCree, Auteur "Hedi Ben Salah",
//   Detail $"Sonia Mabrouk · Secrétaire · {email}", Avant et Apres null

[Theory]
[InlineData(null, "Mabrouk", "Secretaire")] [InlineData("Sonia", "  ", "Secretaire")] [InlineData("Sonia", "Mabrouk", null)]
Un_champ_manquant_est_refuse(...) // 400 « Le prénom, le nom et le rôle sont obligatoires », aucun compte, aucun email

[Theory] [InlineData("Admin")] [InlineData("proprietaire")]
Un_role_inconnu_est_refuse(string role) // 400 « Le prénom, le nom et le rôle sont obligatoires »

[Fact] Un_prenom_trop_long_est_refuse() // prénom de 101 caractères → 400 « Le prénom et le nom font au plus 100 caractères »

[Theory] [InlineData("")] [InlineData("sonia")] [InlineData("Sonia <sonia@exemple.tn>")]
Un_email_pas_valable_est_refuse(string email) // 400 « Cet email n'est pas valable »

[Fact] Un_email_deja_utilise_dans_un_autre_centre_est_refuse()
// un compte des Oliviers a déjà l'email → 409 « Cet email est déjà utilisé sur SchoolHub », pas de ligne au journal

[Fact] Inviter_retire_les_espaces_et_l_email_en_majuscules_est_deja_pris()
// inviter "  Sonia.Mabrouk.<u>@Exemple.tn  " → 201, Email enregistré "Sonia.Mabrouk.<u>@Exemple.tn"
// puis inviter "sonia.mabrouk.<u>@exemple.tn" → 409

[Fact] Si_l_email_ne_part_pas_rien_n_est_cree()
// Emails.Echouer = true → 503 « L'invitation n'est pas partie : réessayez dans un moment »
// aucun compte avec cet email, aucune ligne au journal

[Fact] La_liste_montre_les_comptes_du_centre_dans_l_ordre()
// aux Jasmins : Hedi (actif), Sonia (invitée), Amira (désactivée), Bilel (actif) ; aux Oliviers : Karim
// GET /api/comptes → prénoms ["Bilel", "Hedi", "Sonia", "Amira"], Moi vrai seulement pour Hedi, jamais Karim

[Fact] Une_secretaire_ne_gere_pas_les_comptes()
// connectée en secrétaire : GET et POST /api/comptes → 403 « Accès réservé au propriétaire »

[Fact] Sans_session_les_comptes_repondent_401()
```

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `dotnet test --filter "FullyQualifiedName~GestionDesComptesTests"`
Expected: la compilation échoue (`CompteDuCentre`, `NouvelleInvitation` n'existent pas).

- [ ] **Step 3: Écrire `AdresseEmail`, `GestionDesComptes.ListerAsync`, `InviterAsync` et les deux adresses**

- `ListerAsync` : `contexte.Users.Where(c => c.CentreId == centreCourant.Id)`, triée par `c.Etat == EtatDuCompte.Desactive`, puis `Prenom`, puis `Nom`.
- `InviterAsync`, dans cet ordre de vérification : champs (après `Trim`) et rôle (exactement `"Proprietaire"` ou `"Secretaire"`, majuscules comprises : `Enum.TryParse` avec `ignoreCase: false` laisserait passer `"0"` ou `"1"`, donc comparer aux noms de l'énumération) ; longueurs (100) ; `AdresseEmail.EstValable` ; `comptes.FindByEmailAsync` → 409. Puis la transaction, comme `CreationDeCentre` : `CreateAsync(compte)` (un `DuplicateEmail` d'Identity ou une `DbUpdateException` de l'index unique donne aussi le 409), `journal.Noter(auteur, CompteCree, "{Prénom} {Nom} · {rôle} · {email}")`, `SaveChangesAsync`, `invitations.EnvoyerAsync(compte, centre)` (une exception → `RollbackAsync` et 503), `CommitAsync`.
- `POST /api/comptes` répond `TypedResults.Created($"/api/comptes/{id}", compteDuCentre)`.

- [ ] **Step 4: Lancer tous les tests et vérifier qu'ils passent**

Run: `dotnet test`
Expected: tout passe, y compris `CreationDeCentreTests`, qui vérifie toujours l'email pas valable avec la règle déplacée.

- [ ] **Step 5: Commit** — « CPT-04 : lister les comptes du centre et inviter une personne »

---

### Task 3: Renvoyer l'invitation (CPT-04)

**Files:**
- Modify: `backend/src/SchoolHub/Socle/Comptes/GestionDesComptes.cs`
- Modify: `backend/src/SchoolHub/Socle/Comptes/ComptesEndpoints.cs`
- Test: `backend/tests/SchoolHub.Tests/Comptes/GestionDesComptesTests.cs`

**Interfaces:**
- Consumes: `IssueDeLAction`, `CompteDuCentre` (tâche 2).
- Produces: `Task<IssueDeLAction> RenvoyerLInvitationAsync(Guid id, CancellationToken annulation = default)` ; `POST /api/comptes/{id:guid}/invitation` → 200 `CompteDuCentre`. Une méthode privée `Task<Compte?> TrouverAsync(Guid id)` (filtre explicite sur le centre courant), que les tâches 4 et suivantes réutilisent.

- [ ] **Step 1: Écrire les tests qui échouent**

```csharp
[Fact] Renvoyer_l_invitation_donne_un_nouveau_lien_et_tue_l_ancien()
// inviter Sonia, garder le 1er lien ; POST /api/comptes/{id}/invitation → 200, un 2e email
// GET /api/invitation avec le 1er lien → 410 ; avec le 2e → 200
// aucune ligne au journal en plus de « Compte créé »

[Fact] Renvoyer_l_invitation_d_un_compte_actif_est_refuse()
// → 409 « Ce compte n'attend plus d'invitation »

[Fact] Si_le_nouvel_email_ne_part_pas_l_ancien_lien_marche_encore()
// Emails.Echouer = true → 503 « L'invitation n'est pas partie : réessayez dans un moment » ; 1er lien → 200

[Fact] Renvoyer_l_invitation_d_un_compte_d_un_autre_centre_repond_introuvable()
// un compte invité des Oliviers → 404 « Compte introuvable » ; aucun email
```

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `dotnet test --filter "FullyQualifiedName~GestionDesComptesTests"`
Expected: FAIL, l'adresse répond 404 sans message (elle n'existe pas).

- [ ] **Step 3: Écrire `RenvoyerLInvitationAsync` et son adresse**

Dans une transaction : `comptes.UpdateSecurityStampAsync(compte)` (le changement de tampon rend l'ancien jeton caduc), puis `invitations.EnvoyerAsync` ; une exception → `RollbackAsync` et 503.

- [ ] **Step 4: Lancer tous les tests et vérifier qu'ils passent**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 5: Commit** — « CPT-04 : renvoyer l'invitation, l'ancien lien ne marche plus »

---

### Task 4: Désactiver et réactiver (CPT-05)

**Files:**
- Modify: `backend/src/SchoolHub/Socle/Comptes/Compte.cs` (`Reactiver()`)
- Modify: `backend/src/SchoolHub/Socle/Comptes/GestionDesComptes.cs`
- Modify: `backend/src/SchoolHub/Socle/Comptes/ComptesEndpoints.cs`
- Test: `backend/tests/SchoolHub.Tests/Comptes/GestionDesComptesTests.cs`

**Interfaces:**
- Consumes: `TrouverAsync`, `IssueDeLAction`, `LibellesDesComptes` (tâches 2 et 3) ; `JournalDuCentre.Noter` (tâche 1).
- Produces:
  - `Compte.Reactiver()` : `Etat = PasswordHash is null ? EtatDuCompte.Invite : EtatDuCompte.Actif`.
  - `Task<IssueDeLAction> DesactiverAsync(Compte auteur, Guid id)` et `Task<IssueDeLAction> ReactiverAsync(Compte auteur, Guid id)`.
  - `POST /api/comptes/{id:guid}/desactivation` et `POST /api/comptes/{id:guid}/reactivation` → 200 `CompteDuCentre`.

- [ ] **Step 1: Écrire les tests qui échouent**

```csharp
[Fact] Desactiver_coupe_la_session_et_la_connexion_et_note_le_journal()
// Sonia (secrétaire active) a un client connecté
// Hedi : POST /api/comptes/{sonia}/desactivation → 200, Etat "Desactive"
// client de Sonia : GET /api/moi → 401 ; nouvelle connexion → 403 « Ce compte est désactivé : adressez-vous au propriétaire du centre »
// journal : CompteDesactive, Detail "Sonia Mabrouk · Secrétaire", Avant "Actif", Apres "Désactivé"

[Fact] Desactiver_un_invite_tue_son_lien() // le lien d'invitation → 410 ; journal Avant "Invité"

[Fact] Personne_ne_desactive_son_propre_compte() // 400 « Vous ne pouvez pas désactiver votre propre compte »

[Fact] Desactiver_un_compte_deja_desactive_est_refuse() // 409 « Ce compte est déjà désactivé »

[Fact] Deux_proprietaires_qui_se_desactivent_en_meme_temps_en_laissent_un()
// Hedi et Mehdi, propriétaires actifs, chacun son client connecté
// Task.WhenAll(Hedi désactive Mehdi, Mehdi désactive Hedi)
// exactement une réponse 200 ; l'autre est 401 ou 403
// en base : exactement un propriétaire actif aux Jasmins

[Fact] Reactiver_rend_l_acces_avec_l_ancien_mot_de_passe()
// Sonia désactivée (avec mot de passe) → réactivation 200, Etat "Actif"
// connexion avec "jasmins-2027" → 200
// journal : CompteReactive, Avant "Désactivé", Apres "Actif"

[Fact] Un_invite_desactive_puis_reactive_garde_son_ancien_lien_mort()
// inviter Sonia (lien 1), désactiver, réactiver → Etat "Invite", journal Apres "Invité"
// lien 1 → 410 ; renvoyer l'invitation → lien 2 → 200

[Fact] Reactiver_un_compte_qui_n_est_pas_desactive_est_refuse() // 409 « Ce compte n'est pas désactivé »

[Theory] [InlineData("desactivation")] [InlineData("reactivation")]
Agir_sur_un_compte_d_un_autre_centre_repond_introuvable(string action) // 404 « Compte introuvable », rien ne change

[Theory] [InlineData("desactivation")] [InlineData("reactivation")] [InlineData("invitation")]
Une_secretaire_n_agit_pas_sur_un_compte(string action) // 403 « Accès réservé au propriétaire » ; sans session : 401
```

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `dotnet test --filter "FullyQualifiedName~GestionDesComptesTests"`
Expected: FAIL, les adresses n'existent pas.

- [ ] **Step 3: Écrire `Reactiver`, `DesactiverAsync`, `ReactiverAsync` et leurs adresses**

`DesactiverAsync`, dans cet ordre :
1. `id == auteur.Id` → 400.
2. Transaction, puis le verrou du centre : `contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM centres WHERE id = {centreCourant.Id} FOR UPDATE")`.
3. Relire l'auteur **sans suivi** (`contexte.Users.AsNoTracking()`) : il est déjà suivi depuis la validation de la session, donc une lecture suivie rendrait l'ancien état. S'il n'est plus actif → 403 « Ce compte est désactivé : adressez-vous au propriétaire du centre ».
4. `TrouverAsync(id)` → 404 ; déjà désactivé → 409.
5. Si c'est un propriétaire et qu'aucun **autre** propriétaire actif ne reste → 409 « Il doit rester au moins un propriétaire actif ».
6. `journal.Noter(auteur, CompteDesactive, "{Prénom} {Nom} · {rôle}", avant: libellé de l'état, apres: "Désactivé")`, `compte.Desactiver()`, `comptes.UpdateSecurityStampAsync(compte)` (qui enregistre tout), `CommitAsync`.

`ReactiverAsync` : `TrouverAsync` → 404 ; pas désactivé → 409 ; `Noter(..., CompteReactive, ..., avant: "Désactivé", apres: libellé du nouvel état)` après `compte.Reactiver()` ; `comptes.UpdateAsync(compte)`.

- [ ] **Step 4: Lancer tous les tests et vérifier qu'ils passent**

Run: `dotnet test`
Expected: PASS. Lancer trois fois de suite le test des deux propriétaires : il doit passer à chaque fois.

- [ ] **Step 5: Commit** — « CPT-05 : désactiver et réactiver un compte, avec un propriétaire actif garanti »

---

### Task 5: La commande CEN-01 écrit au journal

**Files:**
- Modify: `backend/src/SchoolHub/Socle/Centres/CreationDeCentre.cs`
- Test: `backend/tests/SchoolHub.Tests/Centres/CreationDeCentreTests.cs`

**Interfaces:**
- Consumes: `JournalDuCentre.Noter`, `JournalDuCentre.AuteurSansCompte` (tâche 1) ; `CentreCourant.Definir(Guid)`.
- Produces: `CreationDeCentre(SchoolHubDbContext contexte, UserManager<Compte> comptes, Invitations invitations, CentreCourant centreCourant, JournalDuCentre journal)`.

- [ ] **Step 1: Écrire les tests qui échouent**

```csharp
[Fact] La_commande_note_la_creation_du_proprietaire_au_journal()
// après la commande : JournalAsync(centre) → une ligne CompteCree, AuteurId null, Auteur "SchoolHub",
//   Detail $"Hedi Ben Salah · Propriétaire · {email}"

// et dans le test existant de l'email qui ne part pas : aucune ligne au journal
// (lire avec IgnoreQueryFilters, puisque le centre n'existe pas)
```

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `dotnet test --filter "FullyQualifiedName~CreationDeCentreTests"`
Expected: FAIL, le journal est vide.

- [ ] **Step 3: Définir le centre courant et noter la ligne**

Juste après le premier `SaveChangesAsync` du centre : `centreCourant.Definir(centre.Id)`. Après la création du propriétaire : `journal.Noter(null, ActionDuJournal.CompteCree, …)`, puis `SaveChangesAsync`, avant l'envoi de l'email.

- [ ] **Step 4: Lancer tous les tests et vérifier qu'ils passent**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 5: Commit** — « CEN-01 : la création du premier propriétaire va au journal »

---

### Task 6: La page « Comptes »

**Files:**
- Create: `frontend/src/app/pages/comptes/compte-du-centre.ts`
- Create: `frontend/src/app/pages/comptes/comptes.ts`, `comptes.html`, `comptes.css`
- Test: `frontend/src/app/pages/comptes/comptes.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.routes.spec.ts`
- Modify: `frontend/src/styles.css` (les pastilles)

**Interfaces:**
- Consumes: les 5 adresses des tâches 2 à 4 ; `messageDErreur` ; `Role` (`socle/session/moi.ts`) ; `saisir`, `envoyer`, `PageDeTest`.
- Produces:
  - `export type EtatDuCompte = 'Invite' | 'Actif' | 'Desactive';`
  - `export interface CompteDuCentre { id: string; prenom: string; nom: string; email: string; role: Role; etat: EtatDuCompte; moi: boolean }`
  - le composant `Comptes` (sélecteur `sh-comptes`, `OnPush`).

- [ ] **Step 1: Écrire les tests de la page qui échouent**

Le modèle est `mot-de-passe-oublie.spec.ts` : `RouterTestingHarness` sur `/comptes`, et `GET /api/comptes` répondu au chargement avec Hedi (moi, propriétaire, actif), Mehdi (propriétaire, actif), Sonia (secrétaire, invitée), Amira (secrétaire, désactivée).

```ts
it('montre chaque compte avec son rôle et son état')
// textes « Hedi Ben Salah (vous) », « Propriétaire », « Secrétaire », « Actif », « Invité », « Désactivé »
// la pastille d'Amira a la classe pill muted, celle de Sonia pill info, celle de Mehdi pill ok

it('les boutons suivent l\'état, et la ligne « (vous) » n\'a pas « Désactiver »')
// Hedi : aucun bouton ; Mehdi : « Désactiver » ; Sonia : « Renvoyer l'invitation » et « Désactiver » ; Amira : « Réactiver »

it('« Désactiver » demande confirmation, et « Non » n\'envoie rien')
// clic Désactiver sur Mehdi → « Désactiver le compte de Mehdi Trabelsi ? » ; clic Non → aucune requête

it('« Oui » désactive, met la ligne à jour et le confirme')
// POST /api/comptes/<id de Mehdi>/desactivation ; flush Mehdi Desactive
// → « Compte de Mehdi Trabelsi désactivé », la ligne de Mehdi montre « Désactivé » et « Réactiver »

it('« Renvoyer l\'invitation » et « Réactiver » envoient et confirment')
// « Nouvelle invitation envoyée à sonia.mabrouk@exemple.tn » ; « Compte de Amira Jlassi réactivé »

it('inviter envoie les champs sans espaces, ferme le formulaire et recharge la liste')
// clic « Inviter une personne » ; saisir prénom, nom, email (avec espaces) ; le rôle vaut Secretaire par défaut
// POST /api/comptes { prenom: 'Sonia', nom: 'Mabrouk', email: 'sonia.mabrouk@exemple.tn', role: 'Secretaire' }
// réponse 201 → GET /api/comptes → « Invitation envoyée à sonia.mabrouk@exemple.tn », plus de formulaire

it('des champs vides ne partent pas') // « Remplissez le prénom, le nom et l'email », aucune requête

it('un refus du serveur s\'affiche') // 409 { message: 'Cet email est déjà utilisé sur SchoolHub' } → texte affiché

it('le bouton se bloque pendant l\'envoi')
// après envoyer() : bouton disabled et libellé « Envoi… » ; un second envoyer() → toujours une seule requête POST
```

Dans `app.routes.spec.ts` :

```ts
it('le propriétaire voit la page « Comptes » sur /comptes') // ouvrir('/comptes', HEDI), répondre GET /api/comptes [] → « Comptes », pas « bientôt »
it('une secrétaire lit « Accès réservé au propriétaire » sur /comptes')
```

- [ ] **Step 2: Lancer les tests et vérifier qu'ils échouent**

Run: `npm test -- --watch=false`
Expected: FAIL, `./comptes` est introuvable.

- [ ] **Step 3: Écrire la page, son adresse et les pastilles**

- `app.routes.ts` : `const PAGES: Partial<Record<string, Type<unknown>>> = { comptes: Comptes };`, et `adressesDe` prend `PAGES[entree.chemin] ?? Bientot`. Le commentaire de `MENU` dit déjà que la story remplace la page à son adresse.
- `Comptes` : la liste est chargée une fois à l'ouverture (`GET /api/comptes`). Des signaux pour la liste, le formulaire ouvert, le compte à confirmer, l'action en cours (le bouton concerné se bloque), le message vert et le message rouge. Un formulaire réactif `prenom`, `nom`, `email`, `role` (`'Secretaire'` par défaut). Après une action, on remplace la ligne par le compte renvoyé. Après une invitation, on recharge la liste.
- En-tête : `.pagehead`, titre « Comptes », sous-titre « Les personnes qui se connectent à SchoolHub pour le Centre Les Jasmins. » (le nom du centre vient de la `Session`), bouton `.btn.primary` « Inviter une personne ».
- Les libellés : « Propriétaire », « Secrétaire » ; « Actif » (`pill ok`), « Invité » (`pill info`), « Désactivé » (`pill muted`) ; « (vous) » après le nom.
- `comptes.css` : une `.card` de lignes en `flex-wrap`, pour qu'un téléphone de 375 pixels n'ait pas de défilement de côté.
- `styles.css` : recopier de la maquette `.pill`, `.pill svg.lucide` et les variantes `.ok`, `.warn`, `.bad`, `.info`, `.muted`.

- [ ] **Step 4: Lancer les tests et vérifier qu'ils passent**

Run: `npm test -- --watch=false`
Expected: PASS, avec les tests existants. Puis `npm run build` : la construction réussit.

- [ ] **Step 5: Commit** — « CPT-04 et CPT-05 : la page Comptes »

---

### Task 7: Documentation et vérification dans l'application

**Files:**
- Modify: `backend/README.md` (une section « Le journal », et `Journal/` dans l'organisation du code)
- Modify: `frontend/README.md` (la page `comptes` dans l'organisation du code)

- [ ] **Step 1: Écrire la documentation**

`backend/README.md`, section « Le journal » : ce qu'est une ligne ; comment une story y écrit (`journal.Noter(auteur, action, détail, avant, après)` avant le `SaveChangesAsync` de l'action, dans la même transaction) ; ajouter un type à `ActionDuJournal` pour une nouvelle action ; le trigger, et le fait que la base des tests l'ajoute elle-même.

- [ ] **Step 2: Lancer tous les tests**

Run: `dotnet test` (depuis `backend/`) et `npm test -- --watch=false` (depuis `frontend/`)
Expected: tout passe.

- [ ] **Step 3: Appliquer la migration et relancer les serveurs**

Run: `dotnet ef database update --project src/SchoolHub`, puis relancer les configurations `serveur` et `angular` de l'aperçu.

- [ ] **Step 4: Jouer la spec à la main** (section « À la main, dans le navigateur » de la spec)

Connecté en Hedi (`hedi@exemple.tn`, centre `les-jasmins`) : inviter une secrétaire, suivre le lien de la console, renvoyer une invitation (l'ancien lien expire), désactiver la secrétaire connectée dans un autre onglet, la réactiver, et lire les lignes avec `docker compose exec -T postgres psql -U schoolhub -d schoolhub -c "select quand, auteur, action, detail, avant, apres from journal order by quand"`. Prendre des captures sur ordinateur et en 375 pixels, en clair et en sombre.

- [ ] **Step 5: Commit** — « Comptes et journal : documentation »
