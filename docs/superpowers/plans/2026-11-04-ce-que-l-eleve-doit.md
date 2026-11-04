# Ce que l'élève doit : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** la fiche dit ce que l'élève doit à ce jour ; le propriétaire accorde un prix spécial et dispense de l'assurance ; la secrétaire note ce qui a été payé avant SchoolHub et regroupe le reste d'une Formation.

**Architecture:** l'échéancier (pur) apprend le prix spécial, les règlements et le reste regroupé. Un `Reglement` (module `Inscriptions`) dit qu'une somme est payée. `InscriptionsDuCentre` calcule « À payer » et porte les actions ; les actions du propriétaire passent par `Politiques.Proprietaire`, et quatre actions vont au journal. Côté Angular, la fiche de l'élève gagne « À payer » et les actions sur ses inscriptions et son assurance.

**Tech Stack:** .NET 10, EF Core, PostgreSQL 17, xUnit ; Angular 22, Vitest.

**Spec:** [docs/superpowers/specs/2026-11-04-ce-que-l-eleve-doit-design.md](../specs/2026-11-04-ce-que-l-eleve-doit-design.md)

## Global Constraints

- Messages **à la lettre** de la spec ; refus par `ConnexionEndpoints.Refus` ; montants au millime (`HasPrecision(12, 3)`), la dernière part prenant la différence (`CalendrierDuGroupe.PrixDesSeances`).
- La ligne de l'élève est verrouillée (`FOR NO KEY UPDATE`, avec `centre_id`) pendant chaque action qui écrit.
- Tests .NET : `dotnet test --artifacts-path ../.superpowers/artefacts` ; Angular : `npm test -- --watch=false`. Arrêter le `serveur` de l'aperçu avant `dotnet ef migrations add`.
- Front : messages sous la carte concernée, boutons bloqués pendant l'envoi, `vi.waitFor` après un `flush`, pas de défilement de côté à 375 px ; la session donne le rôle (`Session`).
- Commits sur `code/ce-que-l-eleve-doit`, signés Taha Jdidi, terminés par `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Un prix spécial accordé après des règlements** : ce qui est réglé garde son montant ; le reste prend le prix spécial, Mois en cours compris. → tâche 1, `Le_prix_special_ne_change_pas_ce_qui_est_regle`.
2. **Un prix spécial plus bas que ce qui est déjà réglé d'une Formation en tranches** : les tranches qui restent valent 0, jamais moins ; elles ne sont plus « à payer ». → tâche 1, `Les_tranches_ne_descendent_pas_sous_zero`.
3. **« Payé avant SchoolHub » tapé deux fois vite** : un seul règlement, le second reçoit « Cette somme est déjà payée ». → tâche 3, `Deux_fois_paye_avant_SchoolHub`.
4. **Une secrétaire qui appelle l'adresse du prix spécial à la main** : 403, rien ne change, rien au journal. → tâche 2, `La_secretaire_n_accorde_pas_de_prix_special`.
5. **« Payer tout le reste » puis un prix spécial** : la somme « Reste de la Formation » prend le prix spécial. → tâche 1, `Le_reste_prend_le_prix_special`.

---

### Task 1: L'échéancier apprend le prix spécial, les règlements et le reste

**Files:** Modify `backend/src/SchoolHub/Modules/Inscriptions/Echeancier.cs` ; Test `backend/tests/SchoolHub.Tests/Inscriptions/EcheancierTests.cs`

**Interfaces — Produces:** `record ConditionsDeLInscription(ModeDeTarification Mode, DateOnly DebutLe, decimal? PrixSpecial = null, IReadOnlyDictionary<string, decimal>? Reglements = null, DateOnly? ResteRegroupeLe = null)` ; `Echeancier.Calculer(CalendrierAPayer groupe, ConditionsDeLInscription conditions)` (l'ancienne surcharge `(groupe, mode, debutLe)` reste) ; `SommeDue` gagne `string Cle` et `bool Reglee` (en fin de record, valeurs par défaut pour garder les tests) ; les clés : « Mois 3 », « Séance 4 », « Totalité », « Une fois », « Tranche 2 », « Reste ».

- [ ] **Step 1: Write the failing tests** : `Les_cles_des_sommes` ; `L_exemple_du_prix_special` (Mois 1 à 3 réglés à 40, prix spécial 20 → Mois 4 et 5 à 20, Mois 1 à 3 réglés à 40) ; `Le_prix_special_ne_change_pas_ce_qui_est_regle` (Mois 3 en cours pas réglé : 20) ; `Un_prix_special_nul` (tout vaut 0) ; `Le_prix_special_d_une_revision` (100 → 50 en 10 séances : 5 la séance ; en totalité depuis la 4 : 35) ; `Le_prix_special_d_une_formation_en_tranches` (3 × 100, 150 → 3 × 50 ; tranche 1 réglée 100, 150 → 25 et 25) ; `Les_tranches_ne_descendent_pas_sous_zero` (tranches 1 et 2 réglées 200, 150 → tranche 3 : 0) ; `Le_reste_regroupe` (3 × 100 aux séances 1, 5, 9 ; la 1 réglée ; regroupé le jour de la séance 3 → « Reste de la Formation », 200, due ce jour-là, clé « Reste ») ; `Le_reste_prend_le_prix_special`.
- [ ] **Step 2–4:** RED, implémenter, GREEN.
- [ ] **Step 5: Commit** « ELV-05 et ELV-10 : l'échéancier du prix spécial et du reste ».

### Task 2: Les règlements, « À payer », le prix spécial et la dispense (API)

**Files:** Create `Modules/Inscriptions/Reglement.cs` ; Modify `Inscription.cs` (champs de la spec §1, `AccorderUnPrixSpecial`, `RetirerLePrixSpecial`, `RegrouperLeReste`, `Dispenser` sur l'assurance), `InscriptionsDuCentre.cs`, `InscriptionsEndpoints.cs`, `SchoolHubDbContext.cs` (`Reglements`), `Socle/Journal/LigneDuJournal.cs` (`PrixSpecialAccorde`, `PrixSpecialRetire`, `AssuranceDispensee`, `PayeAvantSchoolHub`) ; migration `AjouterLesReglements` ; Test `tests/SchoolHub.Tests/Inscriptions/CeQueLEleveDoitTests.cs`

**Interfaces — Produces:** `LigneAPayer(Guid? InscriptionId, string Groupe, string Libelle, string Cle, decimal Montant, DateOnly DueLe)` ; `APayer(List<LigneAPayer> Lignes, decimal Total)` ; `InscriptionDeLEleve` gagne `decimal? PrixSpecial, string? RaisonDuPrixSpecial, List<SommeReglee> Reglees, List<SommePayable> PayablesAvantSchoolHub, bool PeutRegrouperLeReste` avec `SommeReglee(string Libelle, decimal Montant, string Origine)`, `SommePayable(string Cle, string Libelle, decimal Montant)` ; `AssuranceDeLEleve` gagne `string Etat, string? RaisonDeLaDispense` ; `PrixSpecialSaisi(decimal? Montant, string? Raison)` ; `RaisonSaisie(string? Raison)` ; les adresses de la spec §3 sauf le reste et « payé avant SchoolHub ».

- [ ] **Step 1: Write the failing tests** : `A_payer_ce_qui_est_du_a_ce_jour` (Étude depuis le 20/11, on est le 27/11 : Mois 3 (3 séances) ; l'assurance ; total ; rien d'à venir) ; `Rien_a_payer` ; `Accorder_changer_et_retirer_un_prix_special` (À payer suit ; journal avant/après de la spec) ; chaque refus du prix spécial ; `La_secretaire_n_accorde_pas_de_prix_special` (403, journal vide) ; `Un_prix_special_nul_ne_laisse_rien_a_payer` ; `Dispenser_l_assurance` (état, journal, refus, 403 pour la secrétaire) ; isolation (inscription et élève d'un autre centre : 404).
- [ ] **Step 2–4:** RED, implémenter, migration, GREEN, toute la suite.
- [ ] **Step 5: Commit** « ELV-05 et ELV-06 : ce que l'élève doit, le prix spécial et la dispense ».

### Task 3: Payé avant SchoolHub et « Payer tout le reste » (API)

**Files:** Modify `InscriptionsDuCentre.cs`, `InscriptionsEndpoints.cs` ; Test `CeQueLEleveDoitTests.cs`

- [ ] **Step 1: Write the failing tests** : `L_assurance_payee_avant_SchoolHub` (état, plus dans « À payer », journal ; refus déjà payée, dispensée) ; `Une_somme_d_un_groupe_deja_en_cours` (Révision créée à la séance 4 sur 10 : « Totalité, séances 4 à 10 » payable, puis réglée, journal) ; `Une_somme_qui_ne_date_pas_d_avant_SchoolHub` (Étude, ou Révision commencée à la 1 : 409) ; `Une_cle_inconnue` (404) ; `Deux_fois_paye_avant_SchoolHub` (en parallèle : un 200, un 409, un règlement) ; `Payer_tout_le_reste` (3 × 100, la 1 réglée avant SchoolHub : « Reste de la Formation » 200 aujourd'hui) et ses refus.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-07 et ELV-10 : payé avant SchoolHub et payer tout le reste ».

### Task 4: « À payer » et l'assurance sur la fiche

**Files:** Modify `frontend/src/app/pages/inscriptions/inscription.ts` (types), `frontend/src/app/pages/eleves/fiche-de-l-eleve.ts|html|spec.ts`, `eleves.css`

- [ ] **Step 1: Write the failing tests** : la carte « À payer » (lignes, « dû le 20/11/2027 », total ; « Rien à payer aujourd'hui. ») ; l'assurance (« à payer », « payée avant SchoolHub », « dispensée (Orphelin) ») ; « Déjà payée avant SchoolHub » avec confirmation, envoi, relecture ; le propriétaire dispense (formulaire, envoi exact) ; la secrétaire lit « Seul le propriétaire dispense de l'assurance. » ; refus sous la carte ; bouton bloqué.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-04, ELV-06 et ELV-07 : « À payer » et l'assurance sur la fiche ».

### Task 5: Le prix spécial, « payé avant SchoolHub » et le reste sur les inscriptions

**Files:** Modify `fiche-de-l-eleve.ts|html|spec.ts`, `eleves.css`, `frontend/README.md`

- [ ] **Step 1: Write the failing tests** : « Prix spécial 20,000 TND au lieu de 40,000 TND (Famille en difficulté) » ; le propriétaire accorde (envoi exact), change, retire ; la secrétaire lit la phrase ; une somme permise « Déjà payé avant SchoolHub » (confirmation, envoi, elle passe dans les réglées) ; « Payer tout le reste » (confirmation, envoi, « À payer » relu) ; refus sous la carte.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-05, ELV-07 et ELV-10 : les actions sur les inscriptions ».

### Task 6: Relecture, corrections, fusion

- [ ] Relecteur neuf (opus) ; Critique et Important → test d'abord ; Mineurs → mémoire `project-ce-que-l-eleve-doit-reprises.md` ; fusion locale, puis envoi de `main`.
