# Le formulaire public et son lien : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** un parent envoie une demande d'inscription depuis le lien public du centre ; la secrétaire copie ce lien, voit son QR code et imprime l'affiche.

**Architecture:** un module `Demandes` : `DemandeDInscription` (donnée du centre) et `FormulairePublic` (qui travaille dans son propre espace de services, avec le centre de l'adresse) derrière `DemandesEndpoints`. Les règles de la fiche viennent d'`ElevesDuCentre`. Une limite d'envois (`AddRateLimiter`) et un champ piège. Côté Angular : la page publique, la page « Demandes » et l'affiche.

**Tech Stack:** .NET 10, EF Core, PostgreSQL 17, QRCoder, xUnit ; Angular 22, Vitest.

**Spec:** [docs/superpowers/specs/2026-11-05-formulaire-public-design.md](../specs/2026-11-05-formulaire-public-design.md)

## Global Constraints

- Messages **à la lettre** de la spec ; refus par `ConnexionEndpoints.Refus` ; les règles et messages de la fiche élève reviennent d'`ElevesDuCentre`, sans copie.
- Le formulaire public ne renvoie jamais ni élève, ni nombre d'inscrits.
- Tests .NET : `dotnet test --artifacts-path ../.superpowers/artefacts` ; Angular : `npm test -- --watch=false`. Arrêter le `serveur` de l'aperçu avant `dotnet ef migrations add`.
- Front : messages sous la carte concernée, boutons bloqués pendant l'envoi, `vi.waitFor` après un `flush`, pas de défilement de côté à 375 px ; les pages publiques se chargent à la demande (budget de 500 kB).
- Commits sur `code/formulaire-public`, signés Taha Jdidi, terminés par `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Une personne connectée à un autre centre ouvre le lien** : elle voit le formulaire du centre du lien, sa demande va à ce centre, et rien de son propre centre n'apparaît. → tâche 1, `Une_personne_connectee_ailleurs_envoie_au_bon_centre`.
2. **Un robot remplit tous les champs** (champ piège compris) : il croit avoir réussi, rien n'est enregistré. → tâche 1, `Le_champ_piege_n_enregistre_rien`.
3. **Le 6ᵉ envoi de l'heure** : refusé avec le message ; un autre visiteur passe toujours. → tâche 1, `Six_envois_dans_l_heure`.
4. **Un groupe fermé entre l'ouverture du formulaire et l'envoi** : « Ce groupe n'est plus proposé ». → tâche 1, `Un_groupe_qui_n_est_plus_propose`.
5. **Un code de centre tapé à la main qui n'existe pas** : « Ce lien d'inscription n'existe pas », en lecture comme à l'envoi. → tâche 1.

---

### Task 1: La demande et le formulaire public (API)

**Files:** Create `Modules/Demandes/DemandeDInscription.cs`, `FormulairePublic.cs`, `DemandesEndpoints.cs` ; Modify `ElevesDuCentre.cs` (la vérification de la fiche, réutilisable), `SchoolHubDbContext.cs`, `Program.cs` (services, limite d'envois, en-têtes du proxy), `backend/README.md` ; migration `AjouterLesDemandes` ; Test `tests/SchoolHub.Tests/Demandes/FormulairePublicTests.cs`

**Interfaces — Produces:** `FormulaireDuCentre(string Centre, string? Annee, List<NiveauDuFormulaire> Niveaux, List<GroupeDuFormulaire> Groupes)` ; `GroupeDuFormulaire(Guid Id, string Matiere, Guid? NiveauId, string Type, string Professeur, List<CreneauDuGroupe> Creneaux)` ; `DemandeEnvoyee(… FicheSaisieDeLEleve …, List<Guid>? Groupes, string? Amis, string? SiteWeb)` ; `DemandeRecue(string Prenom)` ; les adresses publiques de la spec §2.

- [ ] **Step 1: Write the failing tests** : `Le_formulaire_du_centre` (groupes ouverts et pas terminés, Formation sans niveau, JSON sans élève ni nombre) ; `Un_lien_inconnu` (404 en lecture et en envoi) ; `Envoyer_une_demande` (201, la demande en base pour le bon centre, « À traiter ») ; les refus (Theory : fiche, aucun groupe, groupe d'un autre niveau, deux de la même matière, amis trop longs) ; `Un_groupe_qui_n_est_plus_propose` ; `Le_champ_piege_n_enregistre_rien` ; `Six_envois_dans_l_heure` ; `Une_personne_connectee_ailleurs_envoie_au_bon_centre`.
- [ ] **Step 2–5:** RED, implémenter, migration, GREEN, toute la suite, commit « DEM-01 : le formulaire public d'inscription ».

### Task 2: Le lien et le QR code (API)

**Files:** Modify `DemandesEndpoints.cs`, `SchoolHub.csproj` (QRCoder) ; Test `FormulairePublicTests.cs`

- [ ] **Step 1: Write the failing tests** : `Le_lien_public` (`Application:Adresse` + `/inscription/{code}`) ; `Le_qr_code` (`image/png`, signature PNG) ; une personne pas connectée : 401.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « DEM-02 : le lien public et son QR code ».

### Task 3: La page publique

**Files:** Create `frontend/src/app/pages/inscription-publique/formulaire-public.ts|html|spec.ts|css` ; Modify `app.routes.ts|spec.ts`

- [ ] **Step 1: Write the failing tests** : l'en-tête ; « Choisissez d'abord le niveau scolaire. » ; les matières et groupes selon le niveau (Formation sans niveau comprise) ; la règle de la 7ᵉ année et « majeur » ; l'envoi exact (`siteWeb` vide) ; la confirmation ; un refus ; le 429 ; un lien inconnu ; le bouton bloqué ; l'adresse sans connexion.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « DEM-01 : la page du formulaire public ».

### Task 4: La page « Demandes » et l'affiche

**Files:** Create `frontend/src/app/pages/demandes/demandes.ts|spec.ts`, `affiche.ts|spec.ts` ; Modify `app.routes.ts|spec.ts`, `frontend/README.md`

- [ ] **Step 1: Write the failing tests** : le lien, « Copier le lien » puis « Lien copié », l'image du QR code, « Imprimer l'affiche » vers `/demandes/affiche` ; l'affiche (nom du centre, QR code, lien, « Imprimer ») ; les adresses.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « DEM-02 : la page Demandes et l'affiche ».

### Task 5: Relecture, corrections, fusion

- [ ] Relecteur neuf (opus) ; Critique et Important → test d'abord ; Mineurs → mémoire `project-formulaire-public-reprises.md` ; fusion locale.
