# Fermer un groupe : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** la secrétaire ferme un groupe à une date, avec un motif ; ses séances après cette date disparaissent, il rejoint « Anciens groupes », et la fermeture va au journal.

**Architecture:** `Groupe` gagne `FermeLe` et `MotifDeFermeture` et une méthode `Fermer(date, motif)` ; `GroupesDuCentre.FermerAsync` vérifie la saisie, supprime les séances après la date, note le journal (`ActionDuJournal.GroupeFerme`) et enregistre le tout d'un coup ; l'adresse `POST /api/groupes/{id}/fermeture` passe le compte connecté comme auteur. La fiche Angular gagne un formulaire de fermeture.

**Tech Stack:** .NET 10, EF Core, PostgreSQL 17, xUnit ; Angular 22, Vitest.

**Spec:** [docs/superpowers/specs/2026-11-04-fermer-un-groupe-design.md](../specs/2026-11-04-fermer-un-groupe-design.md)

## Global Constraints

- Messages **à la lettre** de la spec ; refus par `ConnexionEndpoints.Refus` ; date du jour par `DateDeTunis.Aujourdhui`.
- La ligne du journal part dans le même enregistrement que la fermeture (`JournalDuCentre.Noter` avant `SaveChangesAsync`).
- Tests .NET : `dotnet test --artifacts-path ../.superpowers/artefacts` ; Angular : `npm test -- --watch=false`. Arrêter le `serveur` de l'aperçu avant `dotnet ef migrations add`.
- Commits sur `code/fermer-un-groupe`, signés Taha Jdidi, terminés par `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **La séance du jour de fermeture** : gardée ; celle du lendemain, supprimée. → tâche 1, `Fermer_supprime_les_seances_apres_la_date`.
2. **Un groupe pas encore commencé, fermé aujourd'hui** : toutes ses séances disparaissent, la fiche se lit encore (sans Mois). → tâche 1, `Un_groupe_pas_encore_commence_se_ferme`.
3. **Deux fermetures à la suite** : la seconde répond 409 et le journal n'a qu'une ligne. → tâche 1, `Un_groupe_ne_se_ferme_pas_deux_fois`.
4. **Une date choisie dans la page sans séance après elle** : « Aucune séance prévue après le … ». → tâche 2.
5. **Un professeur dont le seul groupe est fermé** : il peut partir. → tâche 1, `Un_professeur_dont_les_groupes_sont_fermes_peut_partir`.

---

### Task 1: Fermer un groupe (API)

**Files:** `Modules/Groupes/Groupe.cs` (`FermeLe`, `MotifDeFermeture`, `Fermer`), `GroupesDuCentre.cs` (+ `JournalDuCentre`), `GroupesEndpoints.cs` (`FermetureDUnGroupe(DateOnly? Date, string? Motif)`, adresse, `FicheDuGroupe.FermeLe/MotifDeFermeture`, `ResumeDuGroupe.Ferme`), `Socle/Journal/LigneDuJournal.cs` (`ActionDuJournal.GroupeFerme`), `Modules/Professeurs/ProfesseursDuCentre.cs` (groupes ouverts sans les fermés) ; migration `AjouterLaFermetureDesGroupes` ; Test `tests/SchoolHub.Tests/Groupes/FermetureTests.cs`.

- [ ] **Step 1: Write the failing tests** : `Fermer_supprime_les_seances_apres_la_date` (Étude du samedi depuis le 18/09/2027 ; horloge au 20/01/2028 ; fermer au 15/01/2028 → séances jusqu'au 15/01 comprises, `fermeLe`, motif, résumé `ferme`) ; `La_fermeture_va_au_journal` (auteur Amira, action, détail « Groupe Samedi 10h → 12h · Karim Ben Salah · Trop peu d'élèves », avant « Ouvert », après « Fermé le 15/01/2028 ») ; `Un_groupe_pas_encore_commence_se_ferme` ; `Un_groupe_ne_se_ferme_pas_deux_fois` ; `Une_fermeture_incomplete_est_refusee` (Theory : sans date, date de demain, motif vide, motif de 201) ; `Un_groupe_d_un_autre_centre_ne_se_ferme_pas` (404) ; `Un_groupe_ferme_ne_se_modifie_plus` ; `Un_professeur_dont_les_groupes_sont_fermes_peut_partir`.
- [ ] **Step 2–4:** RED, implémenter, migration, GREEN, toute la suite.
- [ ] **Step 5: Commit** « GRP-08 : fermer un groupe ».

### Task 2: Fermer depuis la fiche, et les anciens groupes fermés

**Files:** `frontend/src/app/pages/groupes/groupe.ts`, `page-du-groupe.ts|html|spec.ts`, `groupes.css` ; `pages/professeurs/professeurs.ts|html|spec.ts`

- [ ] **Step 1: Write the failing tests** : fiche — « Fermer le groupe » ouvre la date (aujourd'hui) et le motif ; le compte des séances supprimées suit la date (dont « Aucune séance prévue après le … ») ; l'envoi exact `POST /api/groupes/g1/fermeture { date, motif }` ; « Groupe fermé », pastille « Fermé », « Fermé le … : … », plus de « Modifier » ni « Fermer le groupe » ; sans motif : « Choisissez la date et écrivez le motif » ; refus 409 sous la carte. Professeurs — un groupe fermé va dans « Anciens groupes » avec « Fermé ».
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « GRP-08 : fermer un groupe depuis sa fiche ».

### Task 3: Relecture, corrections, essai, fusion

- [ ] Relecteur neuf (Agent, opus) ; Important → test qui échoue d'abord ; Mineurs → mémoire `project-fermeture-reprises.md`.
- [ ] `dotnet ef database update`, essai dans l'aperçu ; fusion locale dans `main`.
