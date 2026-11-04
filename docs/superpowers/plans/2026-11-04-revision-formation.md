# Révision, Formation et groupe déjà en cours : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** la secrétaire crée des Révisions et des Formations (avec tranches), crée un groupe déjà en cours, et retrouve les groupes terminés dans « Anciens groupes ».

**Architecture:** le `Groupe` gagne le type à trois valeurs, `PrixTotal`, `NombreDeSeances`, `PremiereSeance` et ses `Tranches` (table possédée). Les numéros de séance, les prix des séances de Révision, les Mois à partir du Mois en cours et l'état « terminé » se calculent dans `CalendrierDuGroupe`. Les règles de saisie, devenues trop nombreuses pour `GroupesDuCentre`, passent dans `SaisieDuGroupe` (fonctions pures) ; `GroupesDuCentre` garde ce qui lit la base.

**Tech Stack:** .NET 10, EF Core avec Npgsql, PostgreSQL 17, xUnit 2.9 ; Angular 22 (zoneless), Vitest.

**Spec:** [docs/superpowers/specs/2026-11-04-revision-formation-design.md](../specs/2026-11-04-revision-formation-design.md)

## Global Constraints

- Tout en français, identifiants sans accents ; messages **à la lettre** de la spec ; refus par `ConnexionEndpoints.Refus`.
- Montants `decimal` / `numeric(12,3)` ; heures « HH:mm » en culture invariante ; date du jour par `DateDeTunis.Aujourdhui`.
- Une création sans `type` reste une Étude : les tests de l'étape 2 passent sans changement.
- Tests .NET depuis `backend/` : `dotnet test --artifacts-path ../.superpowers/artefacts` ; Angular depuis `frontend/` : `npm test -- --watch=false`.
- Arrêter la configuration `serveur` de l'aperçu avant `dotnet ef migrations add`.
- Front : messages sous la carte concernée, boutons bloqués pendant l'envoi, pas d'envoi si un chargement a échoué, `vi.waitFor` après un `flush`.
- Commits sur `code/revision-formation`, signés `git -c user.name="Taha Jdidi" -c user.email="taha.jdidi11@gmail.com" commit`, terminés par `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Une Révision dont le prix se divise juste** (100 TND en 4) : 25 TND par séance, sans millime perdu ni ajouté. → tâche 1, `Le_prix_des_seances_de_revision` (Theory).
2. **Le jour de la dernière séance** : le groupe n'est pas encore terminé ; le lendemain, il l'est. → tâche 1, `Termine_le_lendemain_de_la_derniere_seance`.
3. **Une Formation « séance 4 sur 10 » avec une tranche due à la séance 1** : acceptée (payée avant SchoolHub), et la somme compte toutes les tranches. → tâche 3, `Une_formation_deja_en_cours_garde_toutes_ses_tranches`.
4. **Des dates une à une saisies dans le désordre** : les séances sont rangées par date, et le nom proposé vient de la première date saisie. → tâche 2, `Les_dates_une_a_une_sont_rangees`.
5. **Modifier une Révision en renvoyant un prix du Mois** : ignoré, rien ne change dans les prix. → tâche 4, `Modifier_une_revision_ne_touche_pas_a_son_prix`.

---

### Task 1: Le calendrier des trois types (fonctions pures)

**Files:** Modify `Modules/Groupes/CalendrierDuGroupe.cs`, `Groupe.cs` (`TypeDeGroupe` : `Etude`, `Revision`, `Formation` ; `Jours.De(DayOfWeek)`) ; Test `tests/SchoolHub.Tests/Groupes/CalendrierDuGroupeTests.cs`

**Interfaces — Produces:**
- `IReadOnlyList<SeancePrevue> SeancesEnNombre(IEnumerable<Creneau> creneaux, DateOnly du, int nombre)`
- `IReadOnlyList<decimal> PrixDesSeances(decimal total, int nombre)` : arrondi au millime (`MidpointRounding.AwayFromZero`), la dernière prend la différence
- `int PremiereSeanceDuMois(int mois)` = `(mois - 1) * 4 + 1` ; `int MoisDeLaSeance(int numero)` = `(numero - 1) / 4 + 1`
- `bool EstTermine(TypeDeGroupe type, DateOnly? derniereSeance, DateOnly aujourdhui)` : Révision ou Formation, et dernière séance avant aujourd'hui
- `string NomPropose(SeancePrevue premiere)` (jour de la date, heures)

- [ ] **Step 1: Write the failing tests** : `Le_prix_des_seances_de_revision` (50/3 → [16.667, 16.667, 16.666] ; 100/4 → 4 × 25 ; 10/1 → [10]) ; `Les_seances_en_nombre` (samedi, depuis le 15/01/2028, 7 séances → du 15/01 au 26/02) ; `Le_mois_d_une_seance_et_la_premiere_seance_d_un_mois` (17 → Mois 5 ; Mois 5 → 17) ; `Termine_le_lendemain_de_la_derniere_seance` (Révision, dernière le 20/05 : pas terminée le 20/05, terminée le 21/05 ; Étude jamais ; sans séance : pas terminée) ; `Le_nom_propose_depuis_une_date` (samedi 20/05/2028 10:00–12:00 → « Groupe Samedi 10h → 12h »).
- [ ] **Step 2–4:** RED (ne compile pas), implémenter, GREEN (`--filter CalendrierDuGroupeTests`).
- [ ] **Step 5: Commit** « GRP-05 à GRP-07 : le calendrier des Révisions, des Formations et des groupes déjà en cours ».

### Task 2: Créer une Révision ou une Formation

**Files:** Modify `Groupe.cs` (champs, `Revision(...)`, `Formation(...)`, `Tranche` possédée, `AnneeScolaireId?`), `GroupesDuCentre.cs`, `GroupesEndpoints.cs` ; Create `Modules/Groupes/SaisieDuGroupe.cs` (lecture des créneaux, des dates, des tranches, des montants ; déplacée depuis `GroupesDuCentre`) ; migration `AjouterRevisionsEtFormations` ; Test `tests/SchoolHub.Tests/Groupes/RevisionsEtFormationsTests.cs`, avec les outils communs déplacés de `GroupesTests` dans `tests/SchoolHub.Tests/Groupes/CentreDeGroupes.cs`.

**Interfaces — Produces:**
- `CreationDUnGroupe` gagne, à la fin et facultatifs : `string? Type`, `decimal? PrixTotal`, `int? NombreDeSeances`, `List<SeanceSaisie>? Dates`, `List<TrancheSaisie>? Tranches`, `int? MoisEnCours`, `int? SeanceEnCours` ; `SeanceSaisie(DateOnly? Date, string? Debut, string? Fin)` ; `TrancheSaisie(decimal? Montant, int? Seance)`.
- `FicheDuGroupe` gagne `bool Termine`, `int PremiereSeance`, `decimal? PrixTotal`, `int? NombreDeSeances`, `List<SeanceNumerotee> Seances`, `List<TrancheDuGroupe> Tranches` ; `PrixDuMois` devient `decimal?` ; `SeanceNumerotee(Guid Id, int Numero, DateOnly Date, string Debut, string Fin, decimal? Prix)` ; `TrancheDuGroupe(decimal Montant, int Seance)`. `ResumeDuGroupe` gagne `bool Termine`.

- [ ] **Step 1: Write the failing tests** : `Creer_une_revision_avec_des_creneaux` (50 TND, 3 séances, samedi, depuis le 20/05/2028 → séances 1 à 3 aux prix 16,667 · 16,667 · 16,666, `mois` vide, `prixDuMois` null, `prixTotal` 50) ; `Creer_une_revision_avec_des_dates_une_a_une` ; `Les_dates_une_a_une_sont_rangees` ; `Creer_une_formation_avec_ses_tranches` (300 TND, 10 séances, tranches 100 aux séances 1, 5, 9) ; `Une_formation_sans_tranche_ni_niveau` ; `Une_formation_sans_annee_scolaire` (centre sans année) ; `Les_tranches_qui_ne_font_pas_le_prix_total_sont_refusees` (manque 50 ; 50 de trop) ; `Une_saisie_de_revision_ou_formation_incomplete_est_refusee` (Theory : type inconnu, niveau de Révision, prix total, nombre de séances 0 et 201, date incomplète, date à l'envers, dates qui se chevauchent, mauvais nombre de dates, date de début absente, tranche à 0, tranche à la séance 11, deux tranches sur la même séance) ; le résumé porte `termine: false`.
- [ ] **Step 2–4:** RED, implémenter, migration, GREEN, toute la suite (les tests de l'étape 2 passent sans changement).
- [ ] **Step 5: Commit** « GRP-05 et GRP-06 : créer une Révision ou une Formation, avec ses tranches ».

### Task 3: Un groupe déjà en cours (GRP-07)

**Files:** `GroupesDuCentre.cs`, `SaisieDuGroupe.cs` ; Test `RevisionsEtFormationsTests.cs`

- [ ] **Step 1: Write the failing tests** : `Une_etude_deja_en_cours` (Mois 5, à partir du samedi 15/01/2028 → premier Mois 5, ses séances 15/01, 22/01, 29/01, 05/02 ; `premiereSeance` 17 ; prix du Mois 5) ; `Une_revision_deja_en_cours` (séance 4 sur 10, le 15/01/2028 → séances 4 à 10, prix calculés sur 10) ; `Une_formation_deja_en_cours_garde_toutes_ses_tranches` ; `Des_dates_une_a_une_pour_les_seances_a_venir` (séance 4 sur 10 → 7 dates attendues : « Il faut une date pour chacune des 7 séances ») ; refus : Mois en cours 0 et 21, séance en cours 0 et 11.
- [ ] **Step 2–4:** RED, implémenter, GREEN.
- [ ] **Step 5: Commit** « GRP-07 : créer un groupe déjà en cours ».

### Task 4: Terminé, professeurs et modification

**Files:** `GroupesDuCentre.cs`, `Modules/Professeurs/ProfesseursDuCentre.cs` (+ `TimeProvider`) ; Tests `RevisionsEtFormationsTests.cs`

- [ ] **Step 1: Write the failing tests** : `Une_revision_finie_est_terminee` (horloge au lendemain de la dernière séance → fiche et résumé `termine: true`) ; `Un_professeur_dont_les_groupes_sont_termines_peut_partir` (compte 0, départ 200) ; `Modifier_une_revision_ne_touche_pas_a_son_prix` (PUT nom, capacité, prixDuMois 99 → nom changé, prix des séances inchangés).
- [ ] **Step 2–4:** RED, implémenter, GREEN, toute la suite.
- [ ] **Step 5: Commit** « GRP-05 et GRP-06 : les groupes terminés, et leur modification ».

### Task 5: Les cadres et « Anciens groupes » (page Professeurs)

**Files:** `frontend/src/app/pages/groupes/groupe.ts` (types, `termine`, formes de la fiche) ; `pages/professeurs/professeurs.ts|html|css|spec.ts`

- [ ] **Step 1: Write the failing tests** : cadre d'une Révision (« Révision ») ; un groupe terminé sort des cadres et va dans « Anciens groupes (1) », replié, avec « Terminé » ; le filtre par jour ignore les anciens groupes.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « GRP-02 : les anciens groupes du professeur ».

### Task 6: La création à trois types

**Files:** `pages/groupes/nouveau-groupe.ts|html|spec.ts`, `groupes.css`

- [ ] **Step 1: Write the failing tests** : choisir « Révision » montre prix total, nombre de séances, choix des dates ; envoi exact d'une Révision par créneaux ; dates une à une (ajouter, retirer, nom depuis la première date, envoi exact) ; Formation avec deux tranches (envoi exact, niveau facultatif) ; « déjà commencé » : Étude (`moisEnCours`), Révision (`seanceEnCours`) ; refus de la page : montant illisible, champs manquants, « Indiquez où en est le groupe ».
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « GRP-05 à GRP-07 : la création d'une Révision, d'une Formation ou d'un groupe déjà en cours ».

### Task 7: La fiche selon le type

**Files:** `pages/groupes/page-du-groupe.ts|html|spec.ts`, `groupes.css`, `frontend/README.md`

- [ ] **Step 1: Write the failing tests** : sous-titre Révision et Formation (avec et sans tranche) ; carte « Les séances » (numéros, dates, prix pour une Révision) ; carte « Les tranches » ; pastille « Terminé » ; « Modifier » d'une Révision sans prix (PUT `{ nom, capacite, prixDuMois: null }`) ; une Étude déjà en cours affiche « Mois 5 » en premier.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « GRP-05 à GRP-07 : la fiche d'une Révision ou d'une Formation ».

### Task 8: Relecture, corrections, essai, fusion

- [ ] Relecteur neuf (Agent, opus) ; Important → test qui échoue d'abord ; Mineurs → mémoire `project-revision-formation-reprises.md`.
- [ ] `dotnet ef database update` ; essai dans l'aperçu (une Révision, une Formation à tranches, une Étude déjà en cours ; 375 px).
- [ ] Suites vertes, fusion locale dans `main`, branche supprimée.
