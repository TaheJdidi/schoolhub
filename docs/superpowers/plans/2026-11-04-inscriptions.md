# Les inscriptions : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** la secrétaire inscrit un élève dans un groupe, sait ce qu'il paiera à sa première séance, et envoie la confirmation WhatsApp.

**Architecture:** un module `Inscriptions` : `Inscription` et `AssuranceDeLAnnee` (données du centre), `Echeancier` (calcul pur des sommes dues), `InscriptionsDuCentre` derrière `InscriptionsEndpoints`. Les destinataires d'un message vivent avec l'élève, le lien `wa.me` dans le socle. Côté Angular : une page d'inscription (depuis l'élève ou depuis le groupe), et des cartes sur la fiche de l'élève et sur le groupe.

**Tech Stack:** .NET 10, EF Core, PostgreSQL 17, xUnit ; Angular 22, Vitest.

**Spec:** [docs/superpowers/specs/2026-11-04-inscriptions-design.md](../specs/2026-11-04-inscriptions-design.md)

## Global Constraints

- Messages **à la lettre** de la spec ; refus par `ConnexionEndpoints.Refus` ; montants au millime (`HasPrecision(12, 3)`), la dernière part prenant la différence (`CalendrierDuGroupe.PrixDesSeances`).
- Les modes circulent en JSON comme `ParMois`, `ALaSeance`, `EnTotalite`, `EnUneFois`, `EnTranches` ; les types comme `Etude`, `Revision`, `Formation`.
- Tests .NET : `dotnet test --artifacts-path ../.superpowers/artefacts` ; Angular : `npm test -- --watch=false`. Arrêter le `serveur` de l'aperçu avant `dotnet ef migrations add`.
- Front : messages sous la carte concernée, boutons bloqués pendant l'envoi, `vi.waitFor` après un `flush`, pas de défilement de côté à 375 px.
- Commits sur `code/inscriptions`, signés Taha Jdidi, terminés par `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Deux clics sur « Inscrire »** (ou deux onglets) : une seule inscription et une seule assurance, le second reçoit « Cet élève est déjà inscrit dans ce groupe ». → tâche 3, `Deux_inscriptions_en_meme_temps`.
2. **Une date sans séance** (un dimanche, un jour d'avant le groupe) : refusée ; la page propose la séance du jour ou la prochaine. → tâche 3, `Une_date_sans_seance_est_refusee` ; tâche 5.
3. **Un élève majeur sans son propre téléphone** : le message part au parent s'il en a un ; sans aucun téléphone, aucun lien, et la page dit « Aucun téléphone pour envoyer la confirmation. ». → tâche 3, `Les_destinataires_du_message` ; tâche 5.
4. **Une deuxième Étude dans l'année, qui commence plus tôt que la première** : pas de seconde assurance ; elle est due à la plus ancienne première séance. → tâche 4, `L_assurance_est_due_a_la_premiere_seance_d_etude`.
5. **La capacité tout juste atteinte par cette inscription** : pas d'avertissement ; déjà atteinte : avertissement ; sans capacité : jamais. → tâche 3, `La_capacite_previent_sans_bloquer`.

---

### Task 1: Les reprises de la fiche élève

Les 6 points de la mémoire `project-fiche-eleve-reprises.md`.

**Files:** Modify `frontend/src/app/pages/eleves/eleves.ts|spec.ts` (erreur remise à vide), `fiche-de-l-eleve.ts|spec.ts` (avertissement effacé de l'état après lecture : `history.replaceState` sans `avertissement`), `eleve.ts` (pas d'élision devant `y`), `backend/src/SchoolHub/Modules/Eleves/Eleve.cs` (`[MemberNotNull]` sur `Modifier`), `ElevesDuCentre.cs` (« Le prénom fait au plus 100 caractères » aussi quand `Eleve.Normaliser($"{prenom} {nom}")` dépasse la colonne de `NomDeRecherche`), Test `ElevesTests.cs`.

- [ ] **Step 1: Write the failing tests** : `La_regle_de_la_7e_annee` ajoute « passé en 7ᵉ sans téléphone : 400 « Le téléphone de l'élève est obligatoire à partir de la 7ᵉ année de base » » ; `Un_nom_qui_s_allonge_en_normalisant_est_refuse` (100 × « 한 » : 400, pas 500) ; Angular : `de('Yassine')` vaut « de Yassine » ; un filtre en échec puis réussi sans résultat montre « Aucun élève trouvé. » sans l'erreur ; la fiche ouverte avec un avertissement le lit une fois et l'efface de `history.state`.
- [ ] **Step 2–4:** RED, implémenter, GREEN, aucune alerte CS8618 à la compilation.
- [ ] **Step 5: Commit** « Fiche élève : les reprises de la relecture ».

### Task 2: L'échéancier

**Files:** Create `backend/src/SchoolHub/Modules/Inscriptions/Echeancier.cs` ; Test `backend/tests/SchoolHub.Tests/Inscriptions/EcheancierTests.cs`

**Interfaces — Produces:** `enum ModeDeTarification { ParMois, ALaSeance, EnTotalite, EnUneFois, EnTranches }` ; `record CalendrierAPayer(TypeDeGroupe Type, int PremiereSeance, IReadOnlyList<DateOnly> Seances, IReadOnlyList<PrixDuGroupe> PrixDuMois, decimal? PrixTotal, int? NombreDeSeances, IReadOnlyList<Tranche> Tranches)` (séances rangées par date puis heure) ; `record SommeDue(string Libelle, decimal Montant, int Seance, DateOnly Date)` ; `Echeancier.Calculer(CalendrierAPayer groupe, ModeDeTarification mode, DateOnly debutLe) -> IReadOnlyList<SommeDue>` (par séance due) ; `Echeancier.PremiereSeance(CalendrierAPayer groupe, DateOnly debutLe) -> int?` (le numéro, `null` sans séance à cette date ou après) ; `Echeancier.ModesPossibles(TypeDeGroupe type, bool aDesTranches) -> IReadOnlyList<ModeDeTarification>`.

- [ ] **Step 1: Write the failing tests** (groupe du samedi, Mois de 4 séances) :
  - `Arrivee_en_cours_de_mois` : 40 TND, arrivée à la 2ᵉ séance du Mois 3 → première somme `("Mois 3 (3 séances)", 30.000m)`, puis `("Mois 4", 40m)` ;
  - `Un_prix_qui_change_garde_l_ancien_pour_un_mois_commence` : 40 TND puis 45 TND à partir du Mois 4 ;
  - `Le_dernier_mois_incomplet` : 6 séances à 40 TND, arrivée à la 1ʳᵉ → `Mois 2 (2 séances)` à 20 TND ;
  - `Un_prix_qui_se_divise_mal` : 40,001 TND, 3 séances → 30,001 TND (10 + 10 + 10,001) ;
  - `Revision_en_totalite_et_a_la_seance` : 100 TND en 10 séances, arrivée à la séance 4 → `("Totalité, séances 4 à 10", 70m)` ; à la séance : sept `("Séance 4", 10m)`… ; arrivée à la 1ʳᵉ → `"Totalité"` ;
  - `Un_groupe_deja_en_cours` : Révision créée à la séance 4 sur 10 (`PremiereSeance = 4`), arrivée à sa première date → « Totalité, séances 4 à 10 », 70 TND ;
  - `Formation_en_une_fois_et_en_tranches` : 300 TND, tranches 100 aux séances 1, 5, 9 ; arrivée à la séance 6 → en une fois `("Formation en une fois", 300m)` ; en tranches `Tranche 1` et `Tranche 2` dues à la séance 6, `Tranche 3` à la 9 ;
  - `Sans_seance_apres_le_debut` : `PremiereSeance` vaut `null`, `Calculer` est vide ;
  - `Les_modes_possibles` : Étude → `[ParMois]` ; Révision → `[ALaSeance, EnTotalite]` ; Formation → `[EnUneFois]`, ou `[EnUneFois, EnTranches]` avec des tranches.
- [ ] **Step 2–4:** RED, implémenter (une séance de Mois vaut sa part de `CalendrierDuGroupe.PrixDesSeances(prix, 4)` selon sa place dans le Mois ; une séance de Révision, sa part de `PrixDesSeances(total, nombre)` selon son numéro), GREEN.
- [ ] **Step 5: Commit** « ELV-03 : l'échéancier d'une inscription ».

### Task 3: Inscrire (API)

**Files:** Create `Modules/Inscriptions/Inscription.cs` (`Inscription`, `AssuranceDeLAnnee` et leurs configurations), `InscriptionsDuCentre.cs`, `InscriptionsEndpoints.cs`, `Modules/Eleves/DestinatairesDeLEleve.cs`, `Socle/Messages/WhatsApp.cs` ; Modify `SchoolHubDbContext.cs` (`Inscriptions`, `Assurances`), `Program.cs`, `ElevesDuCentre.cs` (suppression refusée), `GroupesDuCentre.cs` (`Eleves` compté dans `ListerAsync` et `LireAsync`), `backend/README.md` ; migration `AjouterLesInscriptions` ; Test `tests/SchoolHub.Tests/Inscriptions/InscriptionsTests.cs`

**Interfaces — Consumes:** tâche 2. **Produces:** `DemandeDInscription(Guid? EleveId, Guid? GroupeId, string? Mode, DateOnly? DebutLe)` ; `InscriptionDeLEleve(Guid Id, EleveInscrit Eleve, GroupeDeLInscription Groupe, string Professeur, string Mode, decimal PrixNormal, int NombreDeTranches, SeanceDeDebut? PremiereSeance)` avec `EleveInscrit(Guid Id, string Prenom, string Nom)`, `GroupeDeLInscription(Guid Id, string Nom, string Type, string Matiere)`, `SeanceDeDebut(DateOnly Date, TimeOnly Debut)` ; `EnregistrementDeLInscription(InscriptionDeLEleve Inscription, List<SommeALArrivee> APayerALArrivee, string? Avertissement, string Texte, List<MessageWhatsApp> Messages)` avec `SommeALArrivee(string Libelle, decimal Montant)`, `MessageWhatsApp(string Destinataire, string Telephone, string Lien)` ; `WhatsApp.Lien(string chiffres, string texte)` → `https://wa.me/216{chiffres}?text={Uri.EscapeDataString(texte)}` ; `DestinatairesDeLEleve.De(Eleve)` → `[(Destinataire, Chiffres)]` ; `POST /api/inscriptions`.

- [ ] **Step 1: Write the failing tests** : `Inscrire_dans_une_etude` (201, mode `ParMois` d'office, `aPayerALArrivee` avec le Mois et « Assurance 2027-2028 », 20 TND) ; chaque refus de la spec (Theory, dans l'ordre) ; `Deja_inscrit` ; `Un_groupe_ferme_ou_termine_est_refuse` ; `Une_date_sans_seance_est_refusee` ; `Deux_inscriptions_en_meme_temps` (deux POST en parallèle : un 201, un 409, une assurance) ; `La_capacite_previent_sans_bloquer` (capacité 1 : le 1ᵉʳ sans avertissement, le 2ᵉ avec « Ce groupe a atteint sa capacité. L'inscription reste possible. » ; sans capacité : rien) ; `Pas_d_assurance_sans_montant_ni_pour_une_revision` ; `Les_destinataires_du_message` (majeur : l'élève seul ; mineur avec téléphone : « Adem » et « Parent » ; mineur sans téléphone : « Parent » ; majeur sans téléphone, avec parent : « Parent ») ; `Le_texte_du_message` (le texte exact de la spec, avec « d'Adem » et le téléphone du centre ; sans téléphone, « À bientôt. Centre Les Jasmins ») ; `Une_fiche_inscrite_ne_se_supprime_pas` (409 « Cette fiche a un historique : elle ne se supprime pas ») ; `Le_groupe_compte_ses_eleves` (`eleves` de la fiche du groupe et de `GET /api/groupes`) ; `Un_eleve_ou_un_groupe_d_un_autre_centre_est_introuvable`.
- [ ] **Step 2–4:** RED, implémenter (transaction et `SELECT 1 FROM eleves WHERE id = … FOR NO KEY UPDATE`, comme `GroupesDuCentre.VerrouillerAsync`), migration, GREEN, toute la suite.
- [ ] **Step 5: Commit** « ELV-03 : inscrire un élève dans un groupe ».

### Task 4: Les inscriptions d'un élève et d'un groupe (API)

**Files:** Modify `InscriptionsDuCentre.cs`, `InscriptionsEndpoints.cs` ; Test `InscriptionsTests.cs`

**Interfaces — Produces:** `GET /api/inscriptions?eleve=|groupe=` → `List<InscriptionDeLEleve>` ; `GroupePossible(Guid Id, string Nom, string Type, string Matiere, string Professeur, string? Niveau, bool DuNiveauDeLEleve, int Eleves, int? Capacite, bool DejaInscrit)` ; `GET /api/inscriptions/groupes-possibles?eleve=` ; `AssuranceDeLEleve(string Annee, decimal Montant, DateOnly? DueLe)` ; `GET /api/eleves/{id}/assurance` (200, ou 204 sans objet).

- [ ] **Step 1: Write the failing tests** : `Les_inscriptions_d_un_eleve` (par nom du groupe ; prix normal, nombre de tranches, première séance) ; `Les_eleves_d_un_groupe` (par nom puis prénom) ; `Sans_eleve_ni_groupe` (400 « Choisissez l'élève ou le groupe ») ; `Les_groupes_possibles` (niveau de l'élève d'abord, puis par matière et nom ; fermés et terminés absents ; `dejaInscrit` ; une Formation sans niveau dans les autres) ; `L_assurance_est_due_a_la_premiere_seance_d_etude` (deux Études, la seconde commence plus tôt : un seul montant, `dueLe` la plus ancienne) ; `Sans_etude_l_assurance_est_sans_objet` (204) ; isolation (un élève ou un groupe d'un autre centre : 404 « Élève introuvable », « Groupe introuvable »).
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-03 : les inscriptions d'un élève et d'un groupe ».

### Task 5: La page d'inscription

**Files:** Create `frontend/src/app/pages/inscriptions/inscription.ts` (types de la tâche 3 et 4, `libelleDuMode(inscription)` : « Étude, 40,000 TND par Mois » … de la spec §4), `page-d-inscription.ts|html|spec.ts`, `inscriptions.css` ; Modify `app.routes.ts` (`eleves/:id/inscription`, `groupes/:id/inscription`), `app.routes.spec.ts`

- [ ] **Step 1: Write the failing tests** : depuis l'élève (titre « Inscrire Adem Miled », « Groupes de son niveau » puis « Autres groupes », « 12 élèves sur 15 », « Complet » et l'avertissement orange, « Déjà inscrit » désactivé, « Aucun groupe ouvert pour l'instant. ») ; depuis le groupe (« Ajouter un élève », recherche, « Déjà inscrit ») ; le mode selon le type (aucun choix pour une Étude ; « À la séance » / « En totalité » ; « En une fois » / « En 3 tranches ») ; la première séance proposée (aujourd'hui ou la prochaine ; la dernière s'il n'y en a plus) ; « Choisissez l'élève et le groupe » ; l'envoi exact ; le refus sous la carte ; le bouton bloqué ; la confirmation (« Adem Miled est inscrit dans … », les sommes ou « Rien à payer à sa première séance. », un lien par destinataire avec son `href`, « Aucun téléphone pour envoyer la confirmation. », « Voir la fiche de l'élève », « Voir le groupe ») ; les adresses.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-03 : la page d'inscription ».

### Task 6: Les inscriptions sur la fiche et sur le groupe

**Files:** Modify `frontend/src/app/pages/eleves/fiche-de-l-eleve.ts|html|spec.ts`, `eleves.css` ; `frontend/src/app/pages/groupes/page-du-groupe.ts|html|spec.ts`, `groupes.css` ; `frontend/README.md`

- [ ] **Step 1: Write the failing tests** : la fiche (la carte « Inscriptions » et ses lignes, le lien vers le groupe, « Inscrire dans un groupe » → `/eleves/e1/inscription`, la phrase sans inscription ; la carte « Assurance » ou « Sans objet : aucune inscription en Étude cette année. » ; « Supprimer la fiche » absent avec une inscription) ; le groupe (la carte « Les élèves », le lien vers la fiche, « Ajouter un élève » → `/groupes/g1/inscription`, absent pour un groupe fermé ou terminé, « Aucun élève pour l'instant. »).
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-03 et ELV-04 : les inscriptions sur la fiche et sur le groupe ».

### Task 7: Relecture, corrections, essai, fusion

- [ ] Relecteur neuf (opus) ; Critique et Important → test d'abord ; Mineurs → mémoire `project-inscriptions-reprises.md` ; essai dans l'aperçu ; fusion locale, puis envoi de `main`.
