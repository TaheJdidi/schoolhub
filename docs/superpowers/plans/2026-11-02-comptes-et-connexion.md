# Comptes et connexion : plan de travail

> **Pour les agents :** sous-compétence OBLIGATOIRE : utiliser superpowers:subagent-driven-development (recommandé) ou superpowers:executing-plans pour exécuter ce plan tâche par tâche. Les étapes utilisent des cases à cocher (`- [ ]`) pour le suivi.

**But :** une personne d'un centre se connecte par email et mot de passe, ne travaille que sur son centre et selon son rôle, et l'équipe SchoolHub ouvre un centre par une commande.

**Architecture :** ASP.NET Core Identity (`AddIdentityCore`) sur le contexte EF Core existant, qui devient un `IdentityUserContext<Compte, Guid>`. Une connexion par cookie, avec une vérification maison du compte à chaque requête, et des adresses d'API minimales en français. La commande `creer-centre` est un mode de lancement de la même application.

**Technologies :** .NET 10, ASP.NET Core Identity, EF Core 10 + Npgsql, xUnit, Testcontainers (PostgreSQL 17), `WebApplicationFactory`, `FakeTimeProvider`.

**Conception :** [docs/superpowers/specs/2026-11-02-comptes-et-connexion-design.md](../specs/2026-11-02-comptes-et-connexion-design.md). Le plan s'appuie dessus : lire les deux.

## Contraintes globales

- Paquets : `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14 (application) ; `Microsoft.AspNetCore.Mvc.Testing` 10.0.14 et `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 (tests).
- Tout est en français : messages, emails, noms dans le code (sans accents dans les identifiants, comme le code existant).
- Les erreurs et les avis de l'API répondent `{ "message": "…" }` (record `Avis`).
- Les durées : session 12 h sans activité ; blocage après 5 essais faux de suite, pendant 15 min ; lien d'invitation valable 7 jours ; lien de mot de passe oublié valable 1 h ; mot de passe d'au moins 8 caractères, sans autre règle.
- Les textes exacts (à copier tels quels) :
  - « Email ou mot de passe incorrect »
  - « Trop d'essais : réessayez dans 15 minutes »
  - « Ce compte est désactivé : adressez-vous au propriétaire du centre »
  - « Accès réservé au propriétaire »
  - « Ce lien a expiré : demandez au propriétaire une nouvelle invitation »
  - « Ce lien a expiré : refaites une demande de mot de passe oublié »
  - « Le mot de passe doit faire au moins 8 caractères »
  - « Si un compte existe pour cet email, un lien vient de partir »
  - « Aucun envoi d'email configuré »
- Les noms de tables : `comptes`, `comptes_revendications`, `comptes_connexions_externes`, `comptes_jetons` ; les colonnes restent en snake_case (convention existante).
- Les tests qui touchent à la base demandent Docker démarré. Commandes à lancer depuis `backend/`.
- Les commits : git n'a pas d'identité sur ce poste. Demander l'auteur avant le premier commit, puis `git -c user.name="…" -c user.email="…" commit`, sans toucher à la configuration. Les messages de commit sont en français et finissent par `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Points de vigilance (relecture)

Ce que la conception ne dit pas, mais qui fâcherait une vraie utilisatrice :

1. **Un email tapé avec des majuscules ou des espaces autour** (« Amira@Exemple.tn ») doit marcher comme en minuscules, à la connexion et au mot de passe oublié. Tests en tâches 2 et 5.
2. **Un formulaire envoyé vide** (`{}` ou des champs `null`) donne le message habituel, jamais une erreur 500. Tests en tâches 2 et 5.
3. **Un lien abîmé** par la messagerie (identifiant de compte illisible, jeton coupé) donne le message « Ce lien a expiré… », pas une erreur technique 400. Tests en tâches 4 et 5.
4. **Un jeton d'invitation présenté comme jeton de mot de passe oublié**, ou l'inverse, est refusé. Test en tâche 5.
5. **Un email déjà utilisé, écrit avec une autre casse**, est refusé par la commande de création. Test en tâche 6.

---

## Fichiers

```
backend/src/SchoolHub/
├── Program.cs                                   modifié : Identity, session, adresses, mode commande
├── SchoolHub.csproj                             modifié : paquet Identity
├── appsettings.Development.json                 modifié : Application:Adresse
└── Socle/
    ├── Centres/
    │   ├── CreationDeCentre.cs                  créé : crée centre + propriétaire + invitation (tout ou rien)
    │   └── CommandeCreerCentre.cs               créé : lit les arguments, écrit le résultat, code de sortie
    ├── Comptes/
    │   ├── Compte.cs                            créé : Compte, RoleDuCompte, EtatDuCompte
    │   ├── CompteConfiguration.cs               créé : configuration EF du compte
    │   ├── ConfigurationDesComptes.cs           créé : Identity, cookie, politiques, UtiliserLaSession
    │   ├── RevendicationsDuCompte.cs            créé : le centre et le rôle dans le cookie
    │   ├── ValidationDeLaSession.cs             créé : vérification du compte à chaque requête
    │   ├── ConnexionEndpoints.cs                créé : connexion, déconnexion, moi ; records Avis, Moi
    │   ├── Jetons.cs                            créé : jetons d'invitation (7 j) et de mot de passe oublié (1 h)
    │   ├── Invitations.cs                       créé : fabrique et envoie l'email d'invitation
    │   ├── InvitationEndpoints.cs               créé : GET/POST /api/invitation
    │   └── MotDePasseOublieEndpoints.cs         créé : mot de passe oublié, nouveau mot de passe
    ├── Emails/
    │   ├── IEnvoiEmail.cs                       créé
    │   ├── EmailDansLaConsole.cs                créé
    │   └── OptionsDeLApplication.cs             créé : Application:Adresse
    └── Donnees/
        ├── SchoolHubDbContext.cs                modifié : hérite de IdentityUserContext<Compte, Guid>
        └── Migrations/…_AjouterLesComptes.cs    créé par dotnet ef
backend/tests/SchoolHub.Tests/
├── SchoolHub.Tests.csproj                       modifié : Mvc.Testing, TimeProvider.Testing
├── Outils/
│   ├── ApplicationDeTest.cs                     créé : l'application sur le PostgreSQL de test
│   ├── AdressesDeTest.cs                        créé : adresses réservées aux tests (/api/test/…)
│   └── EmailsCaptures.cs                        créé : faux expéditeur
├── Socle/ModeleTests.cs                         modifié : DonneesCommunes
├── Comptes/ComptesEnBaseTests.cs                créé
├── Comptes/ConnexionTests.cs                    créé
├── Comptes/RolesEtCentreCourantTests.cs         créé
├── Comptes/InvitationTests.cs                   créé
├── Comptes/MotDePasseOublieTests.cs             créé
├── Comptes/DemarrageTests.cs                    créé
└── Centres/CreationDeCentreTests.cs             créé
backend/README.md                                modifié : créer un centre
```

---

### Tâche 1 : le compte en base

**Fichiers :**
- Créer : `backend/src/SchoolHub/Socle/Comptes/Compte.cs`, `backend/src/SchoolHub/Socle/Comptes/CompteConfiguration.cs`
- Modifier : `backend/src/SchoolHub/Socle/Donnees/SchoolHubDbContext.cs`, `backend/src/SchoolHub/SchoolHub.csproj`, `backend/tests/SchoolHub.Tests/Socle/ModeleTests.cs`
- Créer (outil) : la migration `AjouterLesComptes`
- Test : `backend/tests/SchoolHub.Tests/Comptes/ComptesEnBaseTests.cs`

**Interfaces :**
- Produit :
  - `public sealed class Compte : IdentityUser<Guid>`, avec le constructeur `Compte(Guid centreId, string prenom, string nom, string email, RoleDuCompte role)`. Il remplit `Id = Guid.CreateVersion7()`, `UserName = Email = email`, `Etat = EtatDuCompte.Invite`.
  - Ses propriétés à `private set` : `Guid CentreId`, `string Prenom`, `string Nom`, `RoleDuCompte Role`, `EtatDuCompte Etat`.
  - Ses méthodes : `void Activer()` (→ `Actif`) et `void Desactiver()` (→ `Desactive`).
  - `public enum RoleDuCompte { Proprietaire, Secretaire }`, `public enum EtatDuCompte { Invite, Actif, Desactive }`.
  - `SchoolHubDbContext : IdentityUserContext<Compte, Guid>`, avec le même constructeur `(DbContextOptions options, CentreCourant centreCourant)`.

- [ ] **Étape 1 : écrire le test qui échoue**

Dans `ComptesEnBaseTests` (`[Collection(CollectionPostgreSql.Nom)]`, base `comptes_en_base`, créée par `EnsureCreatedAsync` comme dans `SeparationDesCentresTests`) :

```csharp
[Fact]
public async Task Deux_comptes_ne_peuvent_pas_avoir_le_meme_email_meme_dans_deux_centres()
{
    // deux centres (codes uniques), puis deux comptes avec le même email normalisé
    var amira = new Compte(jasmins.Id, "Amira", "Trabelsi", "amira@exemple.tn", RoleDuCompte.Secretaire) { NormalizedEmail = "AMIRA@EXEMPLE.TN" };
    var autre = new Compte(oliviers.Id, "Amira", "Trabelsi", "amira@exemple.tn", RoleDuCompte.Proprietaire) { NormalizedEmail = "AMIRA@EXEMPLE.TN" };
    // le premier s'enregistre, le second lève DbUpdateException
    await Assert.ThrowsAsync<DbUpdateException>(() => /* SaveChangesAsync du second */);
}

[Fact]
public async Task Le_role_et_l_etat_sont_enregistres_en_texte()
{
    // un compte enregistré, puis lu en SQL brut :
    // SELECT role, etat FROM comptes WHERE id = … → "Secretaire", "Invite"
}
```

- [ ] **Étape 2 : lancer le test et vérifier qu'il échoue**

Lancer : `dotnet test --filter "FullyQualifiedName~ComptesEnBaseTests"`
Résultat attendu : la compilation échoue (`Compte` n'existe pas).

- [ ] **Étape 3 : écrire le code**

- Ajouter `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14 à `SchoolHub.csproj`.
- Créer `Compte.cs` (le compte et ses deux enums) avec les interfaces ci-dessus.
- Dans `CompteConfiguration` : `ToTable("comptes")` ; `Prenom` et `Nom` limités à 100 caractères ; `Role` et `Etat` en `HasConversion<string>()`, limités à 20 ; clé étrangère `CentreId` vers `Centre` en `DeleteBehavior.Restrict` ; l'index existant sur `NormalizedEmail` rendu unique (`HasIndex(c => c.NormalizedEmail).IsUnique()`).
- Dans `SchoolHubDbContext` :
  - hériter de `IdentityUserContext<Compte, Guid>` ;
  - dans `OnModelCreating`, appeler `base.OnModelCreating(modelBuilder)` **en premier** ;
  - nommer les trois tables d'Identity : `IdentityUserClaim<Guid>` → `comptes_revendications`, `IdentityUserLogin<Guid>` → `comptes_connexions_externes`, `IdentityUserToken<Guid>` → `comptes_jetons` ;
  - le reste ne change pas.
- Ne pas régler `Stores.SchemaVersion` d'Identity. Si `Les_migrations_suivent_le_modele` signale une table de clés d'accès (passkeys), c'est que le modèle diffère entre l'application et le test : le signaler plutôt que de contourner le test.
- Dans `ModeleTests.DonneesCommunes`, ajouter `typeof(Compte)`, `typeof(IdentityUserClaim<Guid>)`, `typeof(IdentityUserLogin<Guid>)` et `typeof(IdentityUserToken<Guid>)`, avec ce commentaire : « Le compte se lit avant de connaître le centre (connexion) : toute autre lecture des comptes filtre sur le centre courant et a son test d'isolation. »
- Créer la migration :

```bash
dotnet tool restore
dotnet ef migrations add AjouterLesComptes --project src/SchoolHub --output-dir Socle/Donnees/Migrations
```

- [ ] **Étape 4 : lancer tous les tests**

Lancer : `dotnet test`
Résultat attendu : tous passent. Les 12 anciens tests, dont `Les_migrations_suivent_le_modele` et `Les_migrations_s_appliquent_sur_une_base_vide`, plus les 2 nouveaux.

- [ ] **Étape 5 : committer**

Message : « Comptes : la table des comptes, rattachés à un centre ».

---

### Tâche 2 : connexion et session (CPT-01)

**Fichiers :**
- Créer : `Socle/Comptes/ConfigurationDesComptes.cs`, `Socle/Comptes/RevendicationsDuCompte.cs`, `Socle/Comptes/ValidationDeLaSession.cs`, `Socle/Comptes/ConnexionEndpoints.cs`
- Modifier : `Program.cs`, `tests/SchoolHub.Tests/SchoolHub.Tests.csproj`
- Créer (tests) : `Outils/ApplicationDeTest.cs`, `Comptes/ConnexionTests.cs`

**Interfaces :**
- Consomme : `Compte`, `RoleDuCompte`, `EtatDuCompte` (tâche 1).
- Produit :
  - `public static class ConfigurationDesComptes` :
    - `IServiceCollection AjouterLesComptes(this IServiceCollection services)` ;
    - `IApplicationBuilder UtiliserLaSession(this IApplicationBuilder app)` ;
    - `public const string MotDePasseTropCourt = "Le mot de passe doit faire au moins 8 caractères";`
    - `public const int LongueurMinimaleDuMotDePasse = 8;`
  - `public static class Politiques { public const string Proprietaire = "Proprietaire"; }`, déclarée dans `ConfigurationDesComptes.cs` et utilisée en tâche 3.
  - `public sealed class RevendicationsDuCompte : UserClaimsPrincipalFactory<Compte>`, avec `public const string Centre = "schoolhub:centre";`
  - `public static class ValidationDeLaSession { public static Task ValiderAsync(CookieValidatePrincipalContext contexte); }`
  - `public static class ConnexionEndpoints` :
    - `IEndpointRouteBuilder MapConnexion(this IEndpointRouteBuilder api)` ;
    - `internal static Task<Moi> MoiAsync(Compte compte, SchoolHubDbContext contexte)` ;
    - `internal static Task OuvrirLaSessionAsync(SignInManager<Compte> connexion, Compte compte)`, qui appelle `SignInAsync(compte, isPersistent: true)`.
  - Les records : `public sealed record Avis(string Message);`, `public sealed record Moi(string Prenom, string Nom, string Role, string Centre);`, `public sealed record DemandeDeConnexion(string? Email, string? MotDePasse);`
  - Tests : `ApplicationDeTest` (décrit à l'étape 1).

- [ ] **Étape 1 : écrire l'outil de test**

- Ajouter `Microsoft.AspNetCore.Mvc.Testing` 10.0.14 et `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 au projet de tests.
- Ajouter `public partial class Program;` à la fin de `Program.cs`.
- `ApplicationDeTest(PostgreSqlDeTest postgreSql, string nomDeLaBase) : WebApplicationFactory<Program>` :
  - `FakeTimeProvider Horloge`, qui démarre à `DateTimeOffset.UtcNow` (le vrai « maintenant », pour que le cookie ne paraisse jamais expiré au client HTTP) ;
  - dans `ConfigureWebHost` : `UseEnvironment("Development")` ; `UseSetting("ConnectionStrings:SchoolHub", postgreSql.ChaineDeConnexion(nomDeLaBase))` ; puis, dans `ConfigureTestServices`, `AddSingleton<TimeProvider>(Horloge)`, et le contexte remplacé par `ContexteDeTest` (`RemoveAll<SchoolHubDbContext>()` puis `AddScoped<SchoolHubDbContext, ContexteDeTest>()`) pour que les tests d'isolation aient leurs notes ;
  - `Task InitialiserLaBaseAsync()` : `EnsureCreatedAsync()` dans un scope ;
  - `HttpClient Client()` : `CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false })`. Le client garde les cookies, et le HTTPS est obligatoire car le cookie est `Secure` ;
  - `Task<Centre> CreerCentreAsync(string nom = "Centre Les Jasmins")`, avec un code unique `"les-jasmins-" + 8 caractères` ;
  - `Task<Compte> CreerCompteAsync(Centre centre, string prenom = "Amira", string nom = "Trabelsi", RoleDuCompte role = RoleDuCompte.Secretaire, EtatDuCompte etat = EtatDuCompte.Actif, string? motDePasse = "jasmins-2027", string? email = null)`. L'email par défaut est `"{prenom}.{8 caractères}@exemple.tn"` en minuscules. La méthode applique l'état (`Activer()` ou `Desactiver()`), puis passe par `UserManager<Compte>.CreateAsync(compte, motDePasse)`, ou par `CreateAsync(compte)` si le mot de passe est `null` ;
  - `Task<T> DansUnScopeAsync<T>(Func<IServiceProvider, Task<T>> action)`, pour lire ou modifier la base depuis un test.
- Chaque classe de tests crée son `ApplicationDeTest` dans `InitializeAsync`, avec sa propre base (`comptes_connexion`, …), et le libère dans `DisposeAsync`.

- [ ] **Étape 2 : écrire les tests qui échouent**

Dans `ConnexionTests` (`[Collection(CollectionPostgreSql.Nom)]`), chaque test crée son centre et son compte. `Connexion(client, email, mdp)` fait `PostAsJsonAsync("/api/connexion", new DemandeDeConnexion(email, mdp))`.

```csharp
[Fact] Une_secretaire_se_connecte_et_retrouve_son_centre
// POST /api/connexion → 200, Moi("Amira", "Trabelsi", "Secretaire", "Centre Les Jasmins")
// puis GET /api/moi → 200, même Moi

[Fact] Un_mauvais_mot_de_passe_et_un_email_inconnu_donnent_le_meme_message
// les deux → 401, Avis("Email ou mot de passe incorrect")

[Fact] Un_compte_invite_ne_peut_pas_se_connecter
// compte créé avec motDePasse: null, etat: Invite → 401, « Email ou mot de passe incorrect »

[Fact] Au_cinquieme_essai_faux_le_compte_est_bloque_15_minutes
// essais 1 à 4 → « Email ou mot de passe incorrect » ; essai 5 → 401 « Trop d'essais : réessayez dans 15 minutes »
// puis le BON mot de passe → 401 « Trop d'essais… »
// LockoutEnd - DateTimeOffset.UtcNow entre 14 et 15 minutes

[Fact] Apres_le_blocage_le_compte_se_debloque
// bloqué (5 essais faux), puis SetLockoutEndDateAsync(compte, DateTimeOffset.UtcNow.AddSeconds(-1))
// → le bon mot de passe donne 200
// (le blocage d'Identity lit l'heure réelle, pas l'horloge de test)

[Fact] Un_essai_reussi_remet_le_compteur_a_zero
// 4 faux, 1 bon, puis 4 faux → le 4e donne encore « Email ou mot de passe incorrect »

[Fact] Un_compte_desactive_ne_peut_pas_se_connecter
// bon mot de passe → 403 « Ce compte est désactivé : adressez-vous au propriétaire du centre »
// mauvais mot de passe → 401 « Email ou mot de passe incorrect »

[Fact] La_session_dure_12_heures_sans_activite
// connexion ; Horloge.Advance(11 h) → /api/moi 200 ; Advance(11 h) → 200 (le délai repart)
// Advance(12 h + 1 min) → 401

[Fact] Le_cookie_est_protege
// l'en-tête Set-Cookie de la connexion contient (sans tenir compte de la casse) :
// "schoolhub=", "httponly", "secure", "samesite=strict", "expires="

[Fact] Apres_la_deconnexion_on_ne_voit_plus_rien
// connexion ; POST /api/deconnexion → 204 ; GET /api/moi → 401

[Fact] Les_reponses_de_l_api_ne_sont_pas_gardees_en_cache
// GET /api/moi connecté → Cache-Control contient "no-store"

[Fact] Sans_connexion_l_api_repond_401_sans_redirection
// GET /api/moi sans cookie → 401, pas d'en-tête Location

[Fact] Un_compte_desactive_pendant_sa_session_est_deconnecte_a_la_requete_suivante
// connexion ; compte.Desactiver() enregistré par UserManager.UpdateAsync ; GET /api/moi → 401

[Fact] Un_compte_dont_le_tampon_de_securite_change_est_deconnecte
// connexion ; UpdateSecurityStampAsync(compte) ; GET /api/moi → 401

[Theory] Un_email_avec_majuscules_ou_espaces_marche_comme_en_minuscules   // point de vigilance 1
// InlineData("  {email}  "), InlineData("{EMAIL EN MAJUSCULES}") → 200

[Fact] Un_formulaire_vide_donne_le_message_habituel   // point de vigilance 2
// POST {} et POST { email: null, motDePasse: null } → 401 « Email ou mot de passe incorrect »
```

- [ ] **Étape 3 : lancer les tests et vérifier qu'ils échouent**

Lancer : `dotnet test --filter "FullyQualifiedName~ConnexionTests"`
Résultat attendu : ÉCHEC, avec 404 sur `/api/connexion` (ou une erreur de compilation sur `DemandeDeConnexion`).

- [ ] **Étape 4 : écrire le code**

`AjouterLesComptes` :
- `AddIdentityCore<Compte>`, avec :
  - `Password` : `RequiredLength = 8`, aucune autre exigence (`RequireDigit`, `RequireLowercase`, `RequireUppercase`, `RequireNonAlphanumeric` à `false`, `RequiredUniqueChars = 1`) ;
  - `Lockout` : `MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15 min` ;
  - `User` : `RequireUniqueEmail = true`, `AllowedUserNameCharacters = ""` (l'email sert de nom d'utilisateur).
- Puis `.AddEntityFrameworkStores<SchoolHubDbContext>().AddSignInManager().AddClaimsPrincipalFactory<RevendicationsDuCompte>()`.
- `AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme, …)`, avec :
  - `Cookie.Name = "schoolhub"`, `HttpOnly`, `SecurePolicy = Always`, `SameSite = Strict` ;
  - `ExpireTimeSpan = 12 h`, `SlidingExpiration = false` (on renouvelle nous-mêmes à chaque requête) ;
  - `Events.OnValidatePrincipal = ValidationDeLaSession.ValiderAsync` ;
  - `OnRedirectToLogin` → 401 sans corps ;
  - `OnRedirectToAccessDenied` → 403 avec `Avis("Accès réservé au propriétaire")`.
- `AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme).Configure<TimeProvider>((o, temps) => o.TimeProvider = temps)` : le cookie lit l'horloge de l'application, que les tests remplacent.
- `AddAuthorizationBuilder()`, avec une politique de repli qui exige d'être connecté, et la politique `Politiques.Proprietaire` (`RequireRole(nameof(RoleDuCompte.Proprietaire))`).

`UtiliserLaSession` :
- un middleware qui pose `Cache-Control: no-store` sur les chemins `/api` ;
- puis `UseAuthentication()` et `UseAuthorization()`. La tâche 3 ajoute le centre courant entre les deux.

`RevendicationsDuCompte.GenerateClaimsAsync` : la base, plus `Centre` (= `CentreId`) et `ClaimTypes.Role` (= `Role.ToString()`).

`ValidationDeLaSession.ValiderAsync` :
- relit le compte par `UserManager.GetUserId(principal)` ;
- le rejette (`RejectPrincipal()` puis `SignOutAsync(IdentityConstants.ApplicationScheme)`) s'il n'existe plus, s'il n'est pas `Actif`, ou si son `SecurityStamp` diffère de la revendication `Options.ClaimsIdentity.SecurityStampClaimType` ;
- sinon : `ReplacePrincipal(await signInManager.CreateUserPrincipalAsync(compte))` et `ShouldRenew = true`. C'est ce qui renouvelle le cookie à chaque requête.

`MapConnexion` :
- `POST /connexion` (`AllowAnonymous`) :
  1. nettoyer l'email (`Trim()`) ; s'il est vide, ou le mot de passe `null` → 401 général ;
  2. `FindByEmailAsync` : compte absent → 401 général ;
  3. `CheckPasswordSignInAsync(compte, mdp, lockoutOnFailure: true)` : `IsLockedOut` → 401 « Trop d'essais… » ; échec → 401 général ;
  4. réussite : compte `Desactive` → 403 désactivé ; autre état que `Actif` → 401 général ;
  5. sinon, `OuvrirLaSessionAsync`, puis 200 avec `MoiAsync`.
- `POST /deconnexion` : `HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme)`, puis 204. Ne pas utiliser `SignInManager.SignOutAsync` : il déconnecte aussi des schémas externes, non enregistrés ici, et lève une exception.
- `GET /moi` : `UserManager.GetUserAsync(User)`, puis `MoiAsync`. `MoiAsync` lit le nom du centre dans `Centres`.

`Program.cs` :
- `AddSingleton(TimeProvider.System)` et `AjouterLesComptes()` ;
- après `Build()` : `app.UtiliserLaSession()` ;
- `var api = app.MapGroup("/api")` ; `api.MapGet("/sante", …).AllowAnonymous()` ; `api.MapConnexion()`.

- [ ] **Étape 5 : lancer tous les tests**

Lancer : `dotnet test`
Résultat attendu : tous passent.

- [ ] **Étape 6 : committer**

Message : « CPT-01 : connexion, déconnexion et session de 12 heures ».

---

### Tâche 3 : réservé au propriétaire et centre courant (CPT-06)

**Fichiers :**
- Modifier : `Socle/Comptes/ConfigurationDesComptes.cs` (le centre courant dans `UtiliserLaSession`)
- Créer (tests) : `Outils/AdressesDeTest.cs`, `Comptes/RolesEtCentreCourantTests.cs`
- Modifier (tests) : `Outils/ApplicationDeTest.cs`, pour enregistrer `AdressesDeTest` et ajouter `Task<NoteDeTest> AjouterNoteAsync(Centre centre, string texte)`

**Interfaces :**
- Consomme : `Politiques.Proprietaire`, `UtiliserLaSession`, `RevendicationsDuCompte.Centre` (tâche 2) ; `CentreCourant.Definir(Guid)` (existant).
- Produit (tests) : `AdressesDeTest : IStartupFilter`, qui ajoute deux adresses en tête du pipeline, dans une branche `MapWhen(chemin commence par /api/test)`. La branche contient `UseRouting()`, la vraie `UtiliserLaSession()`, puis `UseEndpoints` :
  - `GET /api/test/proprietaire` → `"données du propriétaire"`, avec `RequireAuthorization(Politiques.Proprietaire)` ;
  - `GET /api/test/notes/{id:guid}` → le texte de la `NoteDeTest`, ou 404, lu par `contexte.Set<NoteDeTest>().FindAsync(id)`.

- [ ] **Étape 1 : écrire les tests qui échouent**

```csharp
[Fact] Une_secretaire_n_a_pas_acces_a_ce_qui_est_reserve_au_proprietaire
// Amira (Secretaire) connectée → GET /api/test/proprietaire → 403, Avis("Accès réservé au propriétaire"),
// et le corps ne contient pas "données du propriétaire"

[Fact] Le_proprietaire_a_acces_a_ce_qui_lui_est_reserve
// Hedi (Proprietaire) → 200 "données du propriétaire"

[Fact] Sans_connexion_une_adresse_reservee_repond_401
// GET /api/test/proprietaire sans cookie → 401

[Fact] La_page_de_sante_reste_ouverte_sans_connexion
// GET /api/sante sans cookie → 200 "SchoolHub fonctionne."

[Fact] Connecte_aux_Jasmins_on_ne_voit_pas_une_donnee_des_Oliviers_meme_avec_son_identifiant
// note des Oliviers + note des Jasmins ; Amira (Jasmins) connectée :
// GET /api/test/notes/{idOliviers} → 404 ; GET /api/test/notes/{idJasmins} → 200 "note des Jasmins"
```

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `dotnet test --filter "FullyQualifiedName~RolesEtCentreCourantTests"`
Résultat attendu : le test d'isolation ÉCHOUE (404 sur la note des Jasmins, car il n'y a pas de centre courant). Les tests de rôle peuvent déjà passer grâce à la tâche 2 : c'est normal, ils en verrouillent le comportement.

- [ ] **Étape 3 : écrire le code**

Dans `UtiliserLaSession`, entre `UseAuthentication()` et `UseAuthorization()`, un middleware lit `RevendicationsDuCompte.Centre` dans `HttpContext.User`. S'il est présent, il appelle `RequestServices.GetRequiredService<CentreCourant>().Definir(Guid.Parse(valeur))`.

- [ ] **Étape 4 : lancer tous les tests**

Lancer : `dotnet test`
Résultat attendu : tous passent.

- [ ] **Étape 5 : committer**

Message : « CPT-06 : réservé au propriétaire, et centre courant pris du compte ».

---

### Tâche 4 : emails et invitation (CPT-02)

**Fichiers :**
- Créer : `Socle/Emails/IEnvoiEmail.cs`, `Socle/Emails/EmailDansLaConsole.cs`, `Socle/Emails/OptionsDeLApplication.cs`, `Socle/Comptes/Jetons.cs`, `Socle/Comptes/Invitations.cs`, `Socle/Comptes/InvitationEndpoints.cs`
- Modifier : `Program.cs`, `Socle/Comptes/ConfigurationDesComptes.cs`, `appsettings.Development.json`
- Créer (tests) : `Outils/EmailsCaptures.cs`, `Comptes/InvitationTests.cs`, `Comptes/DemarrageTests.cs`
- Modifier (tests) : `Outils/ApplicationDeTest.cs`, pour remplacer `IEnvoiEmail` par `EmailsCaptures Emails` et ajouter `Action<IServiceCollection>? reglages = null` au constructeur, appliqué en dernier dans `ConfigureTestServices`

**Interfaces :**
- Consomme : `Compte.Activer()`, `ConnexionEndpoints.MoiAsync` et `OuvrirLaSessionAsync`, `Avis`, `ConfigurationDesComptes.MotDePasseTropCourt` et `LongueurMinimaleDuMotDePasse`.
- Produit :
  - `public interface IEnvoiEmail { Task EnvoyerAsync(string destinataire, string sujet, string texte, CancellationToken annulation = default); }`
  - `public sealed class EmailDansLaConsole(ILogger<EmailDansLaConsole> journal) : IEnvoiEmail`, qui écrit destinataire, sujet et texte en `Information`.
  - `public sealed class OptionsDeLApplication { public const string Section = "Application"; [Required] public string Adresse { get; set; } = ""; }`, lue avec `ValidateDataAnnotations().ValidateOnStart()`.
  - `public sealed class JetonDInvitation(IDataProtectionProvider protection, IOptions<OptionsDuJetonDInvitation> options, ILogger<DataProtectorTokenProvider<Compte>> journal) : DataProtectorTokenProvider<Compte>(…)`, avec `public const string Nom = "Invitation";` et `public const string But = "invitation";`
  - `public sealed class OptionsDuJetonDInvitation : DataProtectionTokenProviderOptions`, avec `Name = JetonDInvitation.Nom` et `TokenLifespan = TimeSpan.FromDays(7)` dans son constructeur.
  - `public sealed class Invitations(UserManager<Compte> comptes, IEnvoiEmail emails, IOptions<OptionsDeLApplication> application)`, avec `Task EnvoyerAsync(Compte compte, Centre centre, CancellationToken annulation = default)`, enregistrée en scoped.
  - `public static class InvitationEndpoints { IEndpointRouteBuilder MapInvitation(this IEndpointRouteBuilder api); }`
  - Les records `public sealed record Accueil(string Prenom, string Centre);` et `public sealed record ChoixDuMotDePasse(string? Compte, string? Jeton, string? MotDePasse);`
  - Tests : `EmailsCaptures : IEnvoiEmail`, avec `List<EmailEnvoye> Envoyes`, `bool Echouer` (si vrai, `EnvoyerAsync` lève `InvalidOperationException("Brevo ne répond pas")`) et `(string Compte, string Jeton) LireLeLien(EmailEnvoye email)`. Cette dernière prend la première adresse `http…` du texte et lit `compte` et `jeton` avec `QueryHelpers.ParseQuery`, qui décode les `%2B` et autres. `public sealed record EmailEnvoye(string Destinataire, string Sujet, string Texte);`

- [ ] **Étape 1 : écrire les tests qui échouent**

Dans `InvitationTests`, chaque test crée un compte *invité* sans mot de passe (Amira, Secrétaire, « Centre Les Jasmins »), puis appelle `Invitations.EnvoyerAsync` dans un scope.

```csharp
[Fact] L_invitation_part_avec_le_bon_sujet_et_un_lien
// 1 email à Amira ; Sujet == "Rejoindre le Centre Les Jasmins sur SchoolHub"
// Texte contient "Bonjour Amira," et "valable 7 jours"
// le lien commence par "http://localhost:5087/invitation?compte="

[Fact] Le_lien_accueille_la_personne
// GET /api/invitation?compte=…&jeton=… (valeurs ré-encodées avec Uri.EscapeDataString) → 200, Accueil("Amira", "Centre Les Jasmins")

[Fact] Choisir_son_mot_de_passe_active_le_compte_et_connecte
// POST /api/invitation { compte, jeton, motDePasse: "motdepas" } → 200, Moi(…"Secretaire", "Centre Les Jasmins")
// puis GET /api/moi → 200 ; le compte est Actif ; un nouveau client se connecte avec "motdepas"
// ("motdepas" : 8 minuscules, preuve qu'il n'y a pas d'autre règle)

[Fact] Un_mot_de_passe_de_moins_de_8_caracteres_est_refuse
// "motdepa" → 400, Avis("Le mot de passe doit faire au moins 8 caractères") ; le compte reste Invite

[Fact] Le_lien_ne_sert_qu_une_fois
// après un POST réussi : GET → 410 et POST → 410, Avis("Ce lien a expiré : demandez au propriétaire une nouvelle invitation")

[Fact] Le_lien_est_valable_7_jours
// IOptions<OptionsDuJetonDInvitation>.Value.TokenLifespan == TimeSpan.FromDays(7)

[Fact] Un_lien_dont_la_duree_est_ecoulee_est_refuse
// ApplicationDeTest avec reglages : Configure<OptionsDuJetonDInvitation>(o => o.TokenLifespan = TimeSpan.Zero)
// → GET → 410 « Ce lien a expiré : demandez au propriétaire une nouvelle invitation »
// (Identity calcule l'expiration des jetons sur l'heure réelle : on ne peut pas avancer l'horloge)

[Theory] Un_lien_abime_donne_le_message_du_lien_expire   // point de vigilance 3
// compte = "pas-un-guid" ; jeton coupé de moitié ; jeton vide → GET et POST → 410, même message
```

Dans `DemarrageTests` (sans base) :

```csharp
[Fact] En_production_sans_envoi_d_email_l_application_refuse_de_demarrer
// une WebApplicationFactory<Program> en UseEnvironment("Production"), sans faux expéditeur :
// var erreur = Record.Exception(() => usine.CreateClient());
// Assert.Contains("Aucun envoi d'email configuré", erreur?.ToString());
```

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `dotnet test --filter "FullyQualifiedName~InvitationTests|FullyQualifiedName~DemarrageTests"`
Résultat attendu : la compilation échoue (`IEnvoiEmail`, `Invitations` n'existent pas).

- [ ] **Étape 3 : écrire le code**

- `appsettings.Development.json` : `"Application": { "Adresse": "http://localhost:5087" }`.
- `Program.cs` :
  - en développement seulement, `AddSingleton<IEnvoiEmail, EmailDansLaConsole>()` ;
  - lire `OptionsDeLApplication` depuis la section `Application` ;
  - juste après `Build()` : si `app.Services.GetService<IEnvoiEmail>()` est `null`, lever `InvalidOperationException("Aucun envoi d'email configuré")` ;
  - mapper `api.MapInvitation()`.
- `AjouterLesComptes` : `.AddTokenProvider<JetonDInvitation>(JetonDInvitation.Nom)` et `AddScoped<Invitations>()`.
- `Invitations.EnvoyerAsync` :
  - le jeton vient de `GenerateUserTokenAsync(compte, JetonDInvitation.Nom, JetonDInvitation.But)` ;
  - le lien est `{Adresse}/invitation?compte={compte.Id}&jeton={Uri.EscapeDataString(jeton)}` ;
  - le texte de l'email, exactement :

```
Bonjour {Prenom},

Vous êtes invité(e) à rejoindre le {centre.Nom} sur SchoolHub.
Choisissez votre mot de passe en ouvrant ce lien, valable 7 jours :
{lien}
```

- `MapInvitation`, avec deux adresses `AllowAnonymous` :
  - `GET /invitation?compte=&jeton=` et `POST /invitation` (`ChoixDuMotDePasse`) partagent une vérification : `Guid.TryParse(compte)`, compte trouvé, `Etat == Invite`, et `VerifyUserTokenAsync(compte, JetonDInvitation.Nom, JetonDInvitation.But, jeton ?? "")`. Sinon → 410 avec le message d'invitation expirée ;
  - `GET` → 200 `Accueil(Prenom, nom du centre)` ;
  - `POST`, une fois la vérification passée :
    1. si le mot de passe est `null` ou fait moins de 8 caractères → 400 `MotDePasseTropCourt` ;
    2. sinon `compte.Activer()`, puis `AddPasswordAsync` (qui change le tampon de sécurité, donc le lien ne sert plus) ;
    3. puis `OuvrirLaSessionAsync`, et 200 `MoiAsync`.

- [ ] **Étape 4 : lancer tous les tests**

Lancer : `dotnet test`
Résultat attendu : tous passent.

- [ ] **Étape 5 : committer**

Message : « CPT-02 : invitation et choix du mot de passe, emails en console en développement ».

---

### Tâche 5 : mot de passe oublié (CPT-03)

**Fichiers :**
- Modifier : `Socle/Comptes/Jetons.cs`, `Socle/Comptes/ConfigurationDesComptes.cs`, `Program.cs`
- Créer : `Socle/Comptes/MotDePasseOublieEndpoints.cs`
- Test : `Comptes/MotDePasseOublieTests.cs`

**Interfaces :**
- Consomme : `IEnvoiEmail`, `OptionsDeLApplication`, `EmailsCaptures`, `ChoixDuMotDePasse`, `Avis`, `MoiAsync`, `OuvrirLaSessionAsync` (tâches 2 et 4).
- Produit :
  - `public sealed class JetonDeMotDePasseOublie`, construit comme `JetonDInvitation`, avec `Nom = "MotDePasseOublie"`.
  - `public sealed class OptionsDuJetonDeMotDePasseOublie : DataProtectionTokenProviderOptions`, avec `TokenLifespan = TimeSpan.FromHours(1)`.
  - `public static class MotDePasseOublieEndpoints { IEndpointRouteBuilder MapMotDePasseOublie(this IEndpointRouteBuilder api); }`
  - Le record `public sealed record DemandeDeLien(string? Email);`

- [ ] **Étape 1 : écrire les tests qui échouent**

```csharp
[Fact] La_reponse_est_la_meme_que_le_compte_existe_ou_non
// POST /api/mot-de-passe-oublie pour un compte actif, puis pour un email inconnu
// → les deux 200, Avis("Si un compte existe pour cet email, un lien vient de partir")

[Fact] Un_compte_actif_recoit_un_lien
// 1 email ; Sujet == "Changer votre mot de passe SchoolHub" ; Texte contient "valable 1 heure"
// et "Si vous n'avez rien demandé, ignorez cet email." ; le lien commence par "http://localhost:5087/nouveau-mot-de-passe?compte="

[Theory] Aucun_email_pour_un_compte_inconnu_desactive_ou_invite
// → 200, même message ; Emails.Envoyes vide

[Fact] Le_lien_change_le_mot_de_passe_et_connecte
// POST /api/nouveau-mot-de-passe { compte, jeton, motDePasse: "oliviers-2027" } → 200 Moi ; GET /api/moi → 200
// l'ancien mot de passe → 401 ; le nouveau → 200

[Fact] Le_lien_ne_sert_qu_une_fois
// deuxième POST → 410, Avis("Ce lien a expiré : refaites une demande de mot de passe oublié")

[Fact] Un_mot_de_passe_trop_court_est_refuse
// "1234567" → 400 « Le mot de passe doit faire au moins 8 caractères » ; l'ancien mot de passe marche encore

[Fact] Le_lien_est_valable_1_heure
// IOptions<OptionsDuJetonDeMotDePasseOublie>.Value.TokenLifespan == TimeSpan.FromHours(1)

[Fact] Un_lien_dont_la_duree_est_ecoulee_est_refuse
// reglages : TokenLifespan = TimeSpan.Zero → 410 « Ce lien a expiré : refaites une demande de mot de passe oublié »

[Fact] Le_lien_leve_le_blocage
// 5 essais faux (bloqué) ; demande de lien ; nouveau mot de passe → la connexion avec le nouveau mot de passe donne 200 tout de suite

[Theory] Un_email_avec_majuscules_ou_espaces_recoit_son_lien   // point de vigilance 1
// "  AMIRA…@EXEMPLE.TN  " → 1 email envoyé

[Fact] Un_formulaire_vide_donne_la_reponse_habituelle   // point de vigilance 2
// POST {} → 200, même message ; aucun email

[Fact] Un_lien_abime_donne_le_message_du_lien_expire   // point de vigilance 3
// compte = "pas-un-guid", jeton coupé → 410

[Fact] Un_jeton_d_invitation_ne_sert_pas_a_changer_de_mot_de_passe   // point de vigilance 4
// compte actif ; jeton d'invitation fabriqué pour lui (GenerateUserTokenAsync(compte, JetonDInvitation.Nom, JetonDInvitation.But))
// → POST /api/nouveau-mot-de-passe → 410
// et un jeton de mot de passe oublié sur POST /api/invitation d'un compte invité → 410
```

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `dotnet test --filter "FullyQualifiedName~MotDePasseOublieTests"`
Résultat attendu : ÉCHEC (404 sur `/api/mot-de-passe-oublie`) ou erreur de compilation sur `JetonDeMotDePasseOublie`.

- [ ] **Étape 3 : écrire le code**

- `AjouterLesComptes` : `.AddTokenProvider<JetonDeMotDePasseOublie>(JetonDeMotDePasseOublie.Nom)`, et `Tokens.PasswordResetTokenProvider = JetonDeMotDePasseOublie.Nom`.
- `POST /mot-de-passe-oublie` (`AllowAnonymous`) :
  - l'email est nettoyé (`Trim`) ; s'il n'est pas vide, `FindByEmailAsync` ;
  - si le compte est `Actif`, on fabrique le jeton par `GeneratePasswordResetTokenAsync` ;
  - le lien est `{Adresse}/nouveau-mot-de-passe?compte={Id}&jeton={Uri.EscapeDataString(jeton)}` ;
  - le texte de l'email, exactement :

```
Bonjour {Prenom},

Pour changer votre mot de passe SchoolHub, ouvrez ce lien, valable 1 heure :
{lien}

Si vous n'avez rien demandé, ignorez cet email.
```

  - toujours 200, avec le même `Avis`.
- `POST /nouveau-mot-de-passe` (`AllowAnonymous`, `ChoixDuMotDePasse`) :
  1. **la vérification** : `Guid.TryParse`, compte trouvé et `Actif`, puis `VerifyUserTokenAsync(compte, Options.Tokens.PasswordResetTokenProvider, UserManager<Compte>.ResetPasswordTokenPurpose, jeton ?? "")`. Sinon → 410 avec le message de mot de passe oublié expiré ;
  2. **le mot de passe** : moins de 8 caractères → 400 `MotDePasseTropCourt` ;
  3. **le changement** : `ResetPasswordAsync`, puis `ResetAccessFailedCountAsync` et `SetLockoutEndDateAsync(compte, null)` ;
  4. **la fin** : `OuvrirLaSessionAsync`, puis 200 `MoiAsync`.
- `Program.cs` : `api.MapMotDePasseOublie()`.

- [ ] **Étape 4 : lancer tous les tests**

Lancer : `dotnet test`
Résultat attendu : tous passent.

- [ ] **Étape 5 : committer**

Message : « CPT-03 : mot de passe oublié, lien valable 1 heure ».

---

### Tâche 6 : la commande de création d'un centre (CEN-01)

**Fichiers :**
- Créer : `Socle/Centres/CreationDeCentre.cs`, `Socle/Centres/CommandeCreerCentre.cs`
- Modifier : `Program.cs` (enregistrer `CreationDeCentre` en scoped, mode commande), `backend/README.md`
- Test : `Centres/CreationDeCentreTests.cs`

**Interfaces :**
- Consomme : `Compte`, `RoleDuCompte`, `Invitations.EnvoyerAsync`, `IEnvoiEmail`, `EmailsCaptures.Echouer`.
- Produit :
  - `public sealed record NouveauCentre(string Nom, string Code, string PrenomDuProprietaire, string NomDuProprietaire, string EmailDuProprietaire);`
  - `public sealed record ResultatDeCreation(bool Reussi, string Message);`
  - `public sealed class CreationDeCentre(SchoolHubDbContext contexte, UserManager<Compte> comptes, Invitations invitations)`, avec `Task<ResultatDeCreation> CreerAsync(NouveauCentre demande, CancellationToken annulation = default)`, enregistrée en scoped.
  - `public static class CommandeCreerCentre`, avec `public const string Nom = "creer-centre";` et `static Task<int> ExecuterAsync(IServiceProvider services, string[] arguments, TextWriter sortie)`. Elle ouvre un scope, renvoie 0 en cas de réussite et 1 sinon, et écrit le message sur `sortie`.

- [ ] **Étape 1 : écrire les tests qui échouent**

Ils passent par `CommandeCreerCentre.ExecuterAsync(application.Services, arguments, sortie)`, avec `sortie = new StringWriter()` et un code de centre unique par test.

```csharp
[Fact] La_commande_cree_le_centre_et_son_proprietaire_invite
// ["--nom","Centre Les Jasmins","--code",code,"--prenom","Hedi","--nom-proprietaire","Ben Salah","--email",email] → 0
// sortie == "Centre « Centre Les Jasmins » créé. Invitation envoyée à {email}.\n" (Trim pour la fin de ligne)
// en base : le centre (Nom, Code) ; le compte Hedi Ben Salah, Proprietaire, Invite, CentreId = le centre
// 1 email à {email}, sujet "Rejoindre le Centre Les Jasmins sur SchoolHub"

[Fact] Un_code_deja_pris_est_refuse_sans_rien_creer
// 1 ; « Le code « {code} » est déjà pris par un autre centre. » ; aucun compte avec ce nouvel email

[Fact] Un_email_deja_utilise_est_refuse_sans_rien_creer
// 1 ; « L'email {email} est déjà utilisé sur SchoolHub. » ; aucun centre avec ce nouveau code

[Fact] Un_email_deja_utilise_avec_une_autre_casse_est_refuse   // point de vigilance 5
// email existant "hedi.x@exemple.tn" ; commande avec "HEDI.X@Exemple.tn" → 1, même message

[Theory] Un_code_mal_forme_est_refuse
// InlineData("Les Jasmins"), ("les_jasmins"), ("-jasmins"), ("jasmins-"), ("LES-JASMINS"), ("les--jasmins")
// → 1 ; « Le code ne peut contenir que des lettres minuscules, des chiffres et des tirets. »

[Theory] Un_champ_manquant_ou_vide_est_refuse
// sans --nom (ou "--nom", "   ") → « Il manque le nom du centre. »
// --code → « Il manque le code du centre. » ; --prenom → « Il manque le prénom du propriétaire. »
// --nom-proprietaire → « Il manque le nom du propriétaire. » ; --email → « Il manque l'email du propriétaire. »

[Fact] Un_email_mal_forme_est_refuse
// "hedi@exemple" → 1 ; « L'email hedi@exemple n'est pas valable. »

[Fact] Une_option_inconnue_est_refusee
// "--ville","Tunis" → 1 ; « Option inconnue : --ville. »

[Fact] Si_l_invitation_ne_part_pas_rien_n_est_cree
// Emails.Echouer = true → 1 ; la sortie commence par « Rien n'a été créé : l'invitation n'est pas partie »
// ni centre, ni compte en base
```

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `dotnet test --filter "FullyQualifiedName~CreationDeCentreTests"`
Résultat attendu : la compilation échoue (`CommandeCreerCentre` n'existe pas).

- [ ] **Étape 3 : écrire le code**

`CommandeCreerCentre.ExecuterAsync` :
- lit les paires `--option valeur` (`--nom`, `--code`, `--prenom`, `--nom-proprietaire`, `--email`), en enlevant les espaces autour des valeurs ;
- une option inconnue → « Option inconnue : {option}. » ; une valeur absente ou vide → le message « Il manque… » de ce champ ;
- puis, dans un scope, `CreationDeCentre.CreerAsync`, et écrit `Message` sur la sortie.

`CreationDeCentre.CreerAsync`, dans cet ordre :
1. **Le code** : il doit correspondre à `^[a-z0-9]+(-[a-z0-9]+)*$`.
2. **L'email** : il est valable si `MailAddress.TryCreate` réussit, si l'adresse lue est identique au texte, et si le domaine contient un point.
3. **Le code déjà pris** : `Centres.AnyAsync(c => c.Code == code)`.
4. **L'email déjà utilisé** : `FindByEmailAsync`, qui normalise la casse.
5. **La création, en tout ou rien** :
   - ouvrir une transaction avec `BeginTransactionAsync` ;
   - ajouter le centre, puis `SaveChangesAsync` ;
   - créer le compte avec `CreateAsync(new Compte(centre.Id, prénom, nom, email, RoleDuCompte.Proprietaire))`. En cas d'échec, annuler et renvoyer la description de la première erreur ;
   - envoyer l'invitation avec `Invitations.EnvoyerAsync`. Si une exception est levée, annuler et renvoyer « Rien n'a été créé : l'invitation n'est pas partie ({message de l'exception}). » ;
   - valider avec `CommitAsync`, puis renvoyer « Centre « {Nom} » créé. Invitation envoyée à {email}. ».

`Program.cs` :
- `var enCommande = args is [CommandeCreerCentre.Nom, ..];` et `WebApplication.CreateBuilder(enCommande ? [] : args)`. Ainsi, les options de la commande ne se mélangent pas à la configuration ;
- après `Build()` et la vérification de l'envoi d'email : `if (enCommande) return await CommandeCreerCentre.ExecuterAsync(app.Services, args[1..], Console.Out);` ;
- à la fin, `app.Run(); return 0;`.

`README.md` : une section « Créer un centre », avec la commande de développement et celle de production (console SSH de l'App Service) :

```bash
dotnet run --project src/SchoolHub -- creer-centre --nom "Centre Les Jasmins" --code les-jasmins --prenom Hedi --nom-proprietaire "Ben Salah" --email hedi@exemple.tn
dotnet SchoolHub.dll creer-centre --nom "…" --code … --prenom … --nom-proprietaire "…" --email …
```

- [ ] **Étape 4 : lancer tous les tests**

Lancer : `dotnet test`
Résultat attendu : tous passent.

- [ ] **Étape 5 : vérifier à la main en développement**

Avec `docker compose up -d`, puis `dotnet ef database update --project src/SchoolHub`, lancer la commande de développement du README.
Résultat attendu : le message « Centre « Centre Les Jasmins » créé… », et l'email d'invitation complet, lien compris, écrit dans la console. Ensuite, `dotnet run --project src/SchoolHub` démarre, et un `POST /api/invitation` avec ce lien connecte Hedi (essai avec `curl` ou un fichier `.http`).

- [ ] **Étape 6 : committer**

Message : « CEN-01 : commande de création d'un centre et de son propriétaire ».
