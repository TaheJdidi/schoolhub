# Arrêter une inscription et changer de groupe : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** la secrétaire arrête une inscription à une date, et passe un élève dans un autre groupe du même type en gardant la valeur de ses séances payées.

**Architecture:** l'échéancier (pur) apprend l'arrêt, le report et la valeur des séances payées et pas suivies. `InscriptionsDuCentre` porte l'arrêt et le changement de groupe (qui reprend le cœur de l'inscription) ; deux actions vont au journal. Côté Angular, la fiche gagne « Arrêter l'inscription » et « Changer de groupe », et la page d'inscription sait faire un changement.

**Tech Stack:** .NET 10, EF Core, PostgreSQL 17, xUnit ; Angular 22, Vitest.

**Spec:** [docs/superpowers/specs/2026-11-05-arreter-et-changer-de-groupe-design.md](../specs/2026-11-05-arreter-et-changer-de-groupe-design.md)

## Global Constraints

- Messages **à la lettre** de la spec ; refus par `ConnexionEndpoints.Refus` ; montants au millime ; la ligne de l'élève verrouillée pendant chaque action qui écrit.
- Tests .NET : `dotnet test --artifacts-path ../.superpowers/artefacts` ; Angular : `npm test -- --watch=false`. Arrêter le `serveur` de l'aperçu avant `dotnet ef migrations add`.
- Front : messages sous la carte concernée, boutons bloqués pendant l'envoi, `vi.waitFor` après un `flush`, pas de défilement de côté à 375 px.
- Commits sur `code/arreter-et-changer-de-groupe`, signés Taha Jdidi, terminés par `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Un arrêt le jour même d'une séance due** : la somme du jour reste due ; celle de la séance suivante ne l'est plus. → tâche 1, `L_exemple_de_l_arret`.
2. **Un report plus grand que la première somme** : le surplus va sur la suivante, jamais une somme négative. → tâche 1, `Le_surplus_du_report`.
3. **Un report sur une somme ensuite payée** : recalculer ne fait pas servir le report deux fois. → tâche 1, `Une_somme_reglee_garde_sa_part_du_report`.
4. **Réinscrire un élève arrêté dans le même groupe** : accepté ; un second élève non arrêté reste refusé. → tâche 2, `Reinscrire_apres_un_arret`.
5. **Changer deux fois vite** (deux onglets) : un seul changement, le second voit l'inscription arrêtée. → tâche 3, `Deux_changements_en_meme_temps`.

---

### Task 1: L'échéancier apprend l'arrêt, le report et la valeur non suivie

**Files:** Modify `backend/src/SchoolHub/Modules/Inscriptions/Echeancier.cs` ; Test `EcheancierTests.cs`

**Interfaces — Produces:** `ConditionsDeLInscription` gagne `DateOnly? ArreteLe = null, decimal Report = 0` (à la fin) ; `Echeancier.ValeurNonSuivie(CalendrierAPayer groupe, ConditionsDeLInscription conditions, DateOnly depuisLe) -> decimal`.

- [ ] **Step 1: Write the failing tests** : `L_exemple_de_l_arret` (Mois 5 commencé le 15/01, arrêt le 25/01 : Mois 5 dû en entier, Mois 6 plus dû) ; `Une_somme_payee_apres_l_arret_reste_reglee` ; `Un_arret_avant_la_premiere_seance` (rien) ; `L_exemple_du_report` (Mois à 48, arrivée à la 2ᵉ séance, report 30 → 6) ; `Le_surplus_du_report` (report 50 → Mois 1 0, Mois 2 à 34) ; `Une_somme_reglee_garde_sa_part_du_report` ; `La_valeur_non_suivie` (Mois 5 payé 40, changement après la séance 1 → 30 ; séance de Révision payée d'avance → son prix ; totalité 70 depuis la 4, changement à la 7 → 40 ; Formation 300 payée en une fois, 10 séances, changement à la 6 → 150 ; rien de payé → 0).
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-08 et ELV-09 : l'échéancier de l'arrêt et du report ».

### Task 2: Arrêter une inscription (API)

**Files:** Modify `Inscription.cs` (`ArreteLe`, `Report`, `Arreter`, l'index unique filtré), `InscriptionsDuCentre.cs` (les conditions, les comptes d'élèves inscrits, « Déjà inscrit »), `InscriptionsEndpoints.cs`, `GroupesDuCentre.cs` (le nombre d'élèves), `LigneDuJournal.cs` (`InscriptionArretee`, `ChangementDeGroupe`) ; migration `AjouterLArretDesInscriptions` ; Test `tests/SchoolHub.Tests/Inscriptions/ArretEtChangementTests.cs`

**Interfaces — Produces:** `DateSaisie(DateOnly? Date)` ; `InscriptionDeLEleve` gagne `DateOnly? ArreteLe, decimal Report` ; `POST /api/inscriptions/{id}/arret`.

- [ ] **Step 1: Write the failing tests** : `Arreter_une_inscription` (À payer suit, journal de la spec) ; refus (sans date, déjà arrêtée, inconnue) ; `Reinscrire_apres_un_arret` ; `Le_groupe_ne_compte_plus_un_eleve_arrete` (arrêt passé : non compté ; arrêt à venir : compté) ; `Un_eleve_arrete_peut_etre_propose_a_nouveau` (groupes possibles) ; isolation.
- [ ] **Step 2–5:** RED, implémenter, migration, GREEN, toute la suite, commit « ELV-08 : arrêter une inscription ».

### Task 3: Changer de groupe (API)

**Files:** Modify `InscriptionsDuCentre.cs`, `InscriptionsEndpoints.cs` ; Test `ArretEtChangementTests.cs`

**Interfaces — Produces:** `DemandeDeChangement(Guid? GroupeId, string? Mode, DateOnly? DebutLe)` ; `ChangementDeGroupe(… comme EnregistrementDeLInscription …, decimal Report)` ; `POST /api/inscriptions/{id}/changement` ; `GET /api/inscriptions/groupes-possibles?eleve=&depuis=`.

- [ ] **Step 1: Write the failing tests** : `L_exemple_du_changement` (Révision déjà en cours payée en totalité 70, passage dans une autre Révision à la séance 7 : report 40, l'ancienne arrêtée la veille, journal) ; `Le_report_d_une_etude` (via un règlement noté en base : Mois payé 40, passage après la séance 1 dans un Mois à 48 → 6 à payer) ; chaque refus ; `Deux_changements_en_meme_temps` ; `Les_groupes_possibles_d_un_changement` (même type, sans le groupe de départ) ; isolation.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-09 : changer un élève de groupe ».

### Task 4: Les pages

**Files:** Modify `frontend/src/app/pages/inscriptions/inscription.ts|page-d-inscription.*`, `frontend/src/app/pages/eleves/fiche-de-l-eleve.*`, `frontend/src/app/pages/groupes/page-du-groupe.*`, `app.routes.ts|spec.ts`, `frontend/README.md`, `backend/README.md`

- [ ] **Step 1: Write the failing tests** : la fiche (« Arrêter l'inscription » : date d'office, phrase, envoi exact, relecture ; arrêtée : la mention, sans action ; le report ; « Changer de groupe » mène à `/eleves/e1/changement/i1`) ; la page de changement (titre, sous-titre, groupes du même type via `depuis`, envoi exact vers `/api/inscriptions/i1/changement`, « Changement enregistré » et le report) ; le groupe (« arrêtée le 25/01/2028 ») ; l'adresse.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-08 et ELV-09 : arrêter et changer de groupe sur les pages ».

### Task 5: Relecture, corrections, fusion

- [ ] Relecteur neuf (opus) ; Critique et Important → test d'abord ; Mineurs → mémoire `project-arret-changement-reprises.md` ; fusion locale, puis envoi de `main`.
