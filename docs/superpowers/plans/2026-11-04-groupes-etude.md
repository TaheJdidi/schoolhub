# Les groupes d'Étude : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** la secrétaire crée un groupe d'Étude (ses séances de l'année se créent, rangées en Mois de 4), le retrouve sur la fiche du professeur, et change son nom, sa capacité ou son prix.

**Architecture:** un module `Groupes` : `Matiere`, `Groupe` (avec ses créneaux et sa liste de prix, tables possédées), `Seance` ; `CalendrierDuGroupe` (fonctions pures : séances, Mois, nom proposé, prix) ; `GroupesDuCentre` derrière `GroupesEndpoints`. Les Mois ne s'enregistrent pas, ils se calculent. Côté Angular : les cadres sur la fiche du professeur, et deux pages sous `/professeurs/groupes/`.

**Tech Stack:** .NET 10, EF Core avec Npgsql, PostgreSQL 17, xUnit 2.9 et Testcontainers ; Angular 22 (zoneless), Vitest.

**Spec:** [docs/superpowers/specs/2026-11-04-groupes-etude-design.md](../specs/2026-11-04-groupes-etude-design.md)

## Global Constraints

- Tout est en français, les identifiants aussi, sans accents. Les messages sont recopiés **à la lettre** de la spec.
- Refus de l'API : `ConnexionEndpoints.Refus(statut, message)`. Chaque lecture passe par les `DbSet` filtrés par centre.
- Montants : `decimal`, `numeric(12,3)`. Heures en texte « HH:mm » dans le JSON, `time` en base. Jours : `Lundi` … `Dimanche`, en texte.
- La date du jour vient de `DateDeTunis.Aujourdhui(TimeProvider)` (fuseau `Africa/Tunis`).
- Tests .NET depuis `backend/` : `dotnet test --artifacts-path ../.superpowers/artefacts`. Tests Angular depuis `frontend/` : `npm test -- --watch=false`.
- Arrêter la configuration `serveur` de l'aperçu avant `dotnet ef migrations add`.
- Front : `styles.css` ; messages sous la carte concernée ; boutons bloqués pendant un envoi ; pas d'envoi si un chargement a échoué ; `vi.waitFor` après un `flush`.
- Commits sur `code/groupes-etude`, signés `git -c user.name="Taha Jdidi" -c user.email="taha.jdidi11@gmail.com" commit`, terminés par `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Une date de début qui n'est pas le jour du créneau** (créneau du samedi, début le mercredi 15/09/2027) : la première séance est le samedi suivant (18/09). → tâche 1, `Les_seances_commencent_au_premier_jour_du_creneau`.
2. **La date de début ou la fin de l'année qui tombent le jour du créneau** : ces jours-là ont leur séance (bornes comprises). → tâche 1, `Les_bornes_sont_comprises`.
3. **Un changement de prix le jour de la séance 1 d'un Mois** : ce Mois a commencé, il garde l'ancien prix. → tâche 3, `Un_mois_dont_la_seance_1_est_aujourd_hui_a_commence`.
4. **Une matière tapée avec d'autres majuscules ou des espaces** (« ␣mathématiques ») : la matière existante est reprise, pas de doublon. → tâche 2, `Une_matiere_existante_est_reprise`.
5. **Un prix « inchangé » renvoyé par le formulaire de modification** : aucune ligne de prix ajoutée. → tâche 3, `Un_prix_inchange_n_ajoute_rien`.

---

### Task 1: Le calendrier d'un groupe (fonctions pures)

**Files:**
- Create: `backend/src/SchoolHub/Modules/Groupes/Groupe.cs` (pour l'instant : `Jour`, `Creneau`, `PrixDuGroupe`), `backend/src/SchoolHub/Modules/Groupes/CalendrierDuGroupe.cs`, `backend/src/SchoolHub/Socle/Dates/DateDeTunis.cs`
- Test: `backend/tests/SchoolHub.Tests/Groupes/CalendrierDuGroupeTests.cs`

**Interfaces — Produces:**
- `public enum Jour { Lundi = 1, Mardi, Mercredi, Jeudi, Vendredi, Samedi, Dimanche }` ; `public static class Jours { DayOfWeek VersDayOfWeek(Jour) ; bool EssayerDeLire(string?, out Jour) }` (noms exacts, majuscules ignorées).
- `public sealed class Creneau(Jour jour, TimeOnly debut, TimeOnly fin)` : propriétés `Jour`, `Debut`, `Fin`.
- `public sealed class PrixDuGroupe(int aPartirDuMois, decimal montant)` : `APartirDuMois`, `Montant`.
- `public sealed record SeancePrevue(DateOnly Date, TimeOnly Debut, TimeOnly Fin)`.
- `CalendrierDuGroupe` :
  - `IReadOnlyList<SeancePrevue> Seances(IEnumerable<Creneau> creneaux, DateOnly du, DateOnly au)` : bornes comprises, rangées par date puis début ;
  - `IReadOnlyList<IReadOnlyList<T>> EnMois<T>(IEnumerable<T> seancesRangees)` : paquets de 4 ;
  - `decimal PrixDuMois(IEnumerable<PrixDuGroupe> prix, int numero)` ;
  - `int PremierMoisNonCommence(IReadOnlyList<DateOnly> premieresDatesDesMois, DateOnly aujourdhui)` : premier Mois dont la première date est après aujourd'hui, sinon `Count + 1` ;
  - `string NomPropose(Creneau premier)` et `string Heure(TimeOnly)` (« 10h », « 10h30 », « 8h »).
- `DateDeTunis.Aujourdhui(TimeProvider temps) : DateOnly`.

- [ ] **Step 1: Write the failing tests**
  - `L_exemple_de_la_story` : samedi 10:00–12:00, du 18/09/2027 au 30/06/2028 → première séance 18/09/2027, dernière 24/06/2028, toutes un samedi, 41 séances.
  - `Les_seances_commencent_au_premier_jour_du_creneau` : du mercredi 15/09/2027 → première le 18/09.
  - `Les_bornes_sont_comprises` : du samedi 18/09/2027 au samedi 25/09/2027 → 2 séances.
  - `Deux_creneaux_par_semaine_sont_ranges_par_date_puis_heure` : mardi 17:00 et samedi 10:00 et samedi 08:00 → ordre mardi, samedi 08:00, samedi 10:00.
  - `Les_mois_sont_des_paquets_de_4` : 10 séances → Mois de 4, 4, 2 ; avec deux créneaux par semaine, le Mois 1 dure deux semaines.
  - `Le_prix_d_un_mois_est_celui_de_la_derniere_ligne_qui_ne_le_depasse_pas` : (1 → 40), (6 → 45) : Mois 5 = 40, Mois 6 = 45, Mois 9 = 45.
  - `Le_premier_mois_non_commence` : premières dates [01/01, 15/01, 12/02], aujourd'hui 01/02 → 3 ; aujourd'hui 15/01 → 3 (commencé ce jour-là) ; aujourd'hui 31/12 → 1 ; aujourd'hui 13/02 → 4.
  - `Le_nom_propose` (Theory) : Samedi 10:00–12:00 → « Groupe Samedi 10h → 12h » ; Mardi 17:30–19:00 → « Groupe Mardi 17h30 → 19h » ; Dimanche 08:00–10:00 → « Groupe Dimanche 8h → 10h ».
  - `La_date_de_Tunis` : 31/12/2027 à 23:30 UTC → 01/01/2028.
- [ ] **Step 2: Run** `--filter CalendrierDuGroupeTests` : ne compile pas.
- [ ] **Step 3: Implement.** `DateDeTunis` : `TimeZoneInfo.FindSystemTimeZoneById("Africa/Tunis")`.
- [ ] **Step 4: Run** : PASS.
- [ ] **Step 5: Commit** « GRP-04 : le calendrier d'un groupe, ses séances et ses Mois ».

### Task 2: Créer et lire un groupe d'Étude

**Files:**
- Create: `Modules/Groupes/Matiere.cs`, `Seance.cs`, `GroupesDuCentre.cs`, `GroupesEndpoints.cs` ; compléter `Groupe.cs` (l'entité et sa configuration)
- Modify: `SchoolHubDbContext.cs` (`Matieres`, `Groupes`, `Seances`), `Program.cs` (`AddScoped<GroupesDuCentre>()`, `api.MapGroupes()`), `backend/README.md`
- Create: la migration `AjouterLesGroupes`
- Test: `backend/tests/SchoolHub.Tests/Groupes/GroupesTests.cs`

**Interfaces — Produces:**
- `Groupe : IAppartientAuCentre` : `Id`, `CentreId`, `Type` (`TypeDeGroupe.Etude`), `Nom`, `ProfesseurId`, `MatiereId`, `NiveauId?`, `AnneeScolaireId`, `Capacite?`, `DateDeDebut`, `IReadOnlyList<Creneau> Creneaux`, `IReadOnlyList<PrixDuGroupe> Prix` ; `Modifier(string nom, int? capacite)`, `ChangerLePrix(decimal montant, int aPartirDuMois)`. Créneaux et prix : `OwnsMany`, tables `creneaux_des_groupes` et `prix_des_groupes`, `Jour` et `Type` en texte.
- `Matiere : IAppartientAuCentre` : `Nom`, `NomNormalise`, `static Normaliser(string)` ; index unique (centre, nom normalisé).
- `Seance : IAppartientAuCentre` : `GroupeId`, `Date`, `Debut`, `Fin` ; index (groupe, date).
- Formes JSON (dans `GroupesEndpoints.cs`) : `CreationDUnGroupe`, `CreneauSaisi`, `CreneauDuGroupe`, `FicheDuGroupe`, `MoisDuGroupe`, `SeanceDuGroupe`, `ProfesseurDuGroupe`, `NiveauDuGroupe`, `MatiereDuCentre`, aux champs de la spec section 4.
- `GroupesDuCentre(SchoolHubDbContext, TimeProvider)` : `MatieresAsync()`, `CreerAsync(CreationDUnGroupe)`, `LireAsync(Guid id) : Task<FicheDuGroupe?>` ; `IssueSurUnGroupe(FicheDuGroupe? Fiche, int Statut, string? Message)`.
- Adresses : `GET /api/matieres`, `POST /api/groupes` (201, `Location`), `GET /api/groupes/{id}`.

Les tests partent d'un centre Jasmins, avec son année scolaire 15/09/2027–30/06/2028, ses niveaux d'office, Karim (professeur) et Amira (secrétaire, connectée) ; l'horloge des tests est au 01/09/2027.

- [ ] **Step 1: Write the failing tests**
  - `Creer_un_groupe_d_etude` : l'exemple de la story (Karim, « Mathématiques », 9ᵉ année de base, samedi 10:00–12:00, 60, 18/09/2027, capacité 16, nom vide) → 201 ; la fiche : nom « Groupe Samedi 10h → 12h », type « Etude », 11 Mois, Mois 1 = 18/09, 25/09, 02/10, 09/10 à 60, Mois 11 = 1 séance, le 24/06/2028, `commence: false`, `prixDuMois` 60 ; relue par `GET`.
  - `Une_matiere_nouvelle_s_ajoute_au_centre` puis `Une_matiere_existante_est_reprise` (« ␣mathématiques » → une seule matière « Mathématiques »).
  - `Une_creation_incomplete_est_refusee` (Theory) : chaque refus 400 de la spec, avec son message.
  - `Sans_annee_scolaire_la_creation_est_refusee` (409) ; `La_date_de_debut_hors_de_l_annee_est_refusee` (400, « … du 15/09/2027 au 30/06/2028 ») ; `Un_groupe_sans_seance_est_refuse` (début le 30/06/2028, un mercredi, créneau du samedi).
  - `Un_professeur_parti_est_refuse` (409) ; `Un_professeur_d_un_autre_centre_est_introuvable` (404) ; `Un_niveau_d_un_autre_centre_est_introuvable` (404).
  - `Deux_creneaux_qui_se_chevauchent_sont_refuses` ; `Deux_creneaux_par_semaine_font_des_mois_de_deux_semaines`.
  - `Un_groupe_d_un_autre_centre_est_introuvable` (GET → 404 « Groupe introuvable ») ; `Les_matieres_d_un_autre_centre_sont_invisibles` ; `Une_personne_non_connectee_est_refusee`.
- [ ] **Step 2: Run** : ne compile pas.
- [ ] **Step 3: Implement** les entités, `GroupesDuCentre.CreerAsync/LireAsync/MatieresAsync`, les adresses ; vérification dans l'ordre de la spec ; créneaux lus avec `Jours.EssayerDeLire` et `TimeOnly.TryParseExact(…, "HH:mm")`.
- [ ] **Step 4: Run** `--filter GroupesTests` : PASS ; migration `AjouterLesGroupes` ; toute la suite : PASS.
- [ ] **Step 5: README** (`Groupes/` dans l'arbre) ; **Commit** « GRP-04 : créer un groupe d'Étude, avec ses séances et ses Mois ».

### Task 3: La liste des groupes et la modification

**Files:** `GroupesDuCentre.cs`, `GroupesEndpoints.cs`, `GroupesTests.cs`

**Interfaces — Produces:** `ListerAsync() : Task<List<ResumeDuGroupe>>`, `ModifierAsync(Guid id, ModificationDUnGroupe)`, `ResumeDuGroupe`, `ModificationDUnGroupe(string? Nom, int? Capacite, decimal? PrixDuMois)` ; adresses `GET /api/groupes`, `PUT /api/groupes/{id}`.

- [ ] **Step 1: Write the failing tests**
  - `La_liste_des_groupes` : deux groupes → résumés (matière, nom du niveau, créneaux, capacité, `eleves` 0, `professeurId`).
  - `Le_nouveau_prix_vaut_pour_les_mois_pas_encore_commences` : groupe à 40 ; horloge au 01/02/2028 (dans ce calendrier, le Mois 5 commence le 08/01 et le Mois 6 le 05/02) → PUT 45 ; Mois 1–5 à 40, Mois 6 et suivants à 45, `prixDuMois` 45.
  - `Avant_tout_mois_commence_le_prix_remplace_celui_du_mois_1` : horloge au 01/09/2027 → tous les Mois au nouveau prix.
  - `Un_mois_dont_la_seance_1_est_aujourd_hui_a_commence` : horloge le jour de la séance 1 du Mois 2 → Mois 2 garde l'ancien prix, et `commence: true`.
  - `Un_prix_inchange_n_ajoute_rien` : PUT avec le même prix → une seule ligne de prix (lue en base).
  - `Modifier_le_nom_et_la_capacite` ; nom vide → nom proposé ; capacité 0 → 400 ; prix 0 → 400 ; groupe d'un autre centre → 404.
- [ ] **Step 2–4:** RED, implémenter (`CalendrierDuGroupe.PremierMoisNonCommence` sur les premières dates des Mois ; `Groupe.ChangerLePrix` retire les lignes à partir de N puis ajoute la nouvelle), GREEN, toute la suite.
- [ ] **Step 5: Commit** « GRP-04 : la liste des groupes, et le nouveau prix pour les Mois à venir ».

### Task 4: Les professeurs et les niveaux connaissent les groupes

**Files:** `Modules/Professeurs/ProfesseursDuCentre.cs`, `ProfesseursEndpoints.cs`, `Socle/Centres/NiveauxDuCentre.cs` ; tests dans `GroupesTests.cs` (ou `ProfesseursTests.cs` / `NiveauxTests.cs` avec un groupe créé en base)

- [ ] **Step 1: Write the failing tests** : `groupesOuverts` vaut 2 pour Karim avec deux groupes ; départ → 409 « Fermez d'abord ses groupes » ; suppression → 409 « Ce professeur a eu des groupes : marquez-le parti » ; retrait d'un niveau ajouté utilisé → 409 « Ce niveau est utilisé par un groupe ou un élève ».
- [ ] **Step 2–4:** RED, implémenter, GREEN, toute la suite.
- [ ] **Step 5: Commit** « GRP-04 : les professeurs et les niveaux tiennent compte des groupes ».

### Task 5: Les cadres des groupes sur la fiche du professeur (GRP-02)

**Files:** Create `frontend/src/app/pages/groupes/groupe.ts` (formes de l'API, `JOURS`, `heure`, `nomPropose`, `eleves`) ; Modify `pages/professeurs/professeurs.ts|html|css|spec.ts`

- [ ] **Step 1: Write the failing tests** (spec de la page Professeurs ; le GET `/api/groupes` part avec celui des professeurs) : cadres du professeur choisi (nom, « Étude », « Mathématiques · 9ᵉ année de base », « 0 élève sur 16 » / « 0 élève »), lien `/professeurs/groupes/g1` ; filtre « Sam » ; jour sans groupe grisé et « Pas de groupe le samedi pour … » ; « Aucun groupe ouvert » ; « Nouveau groupe » → `/professeurs/groupes/nouveau?professeur=k`, absent pour un parti ; échec du GET des groupes → message sous la liste et « Nouveau groupe » bloqué.
- [ ] **Step 2–4:** RED, implémenter, GREEN (toute la suite Angular ; adapter les tests existants qui ouvrent la page : ils flushent aussi `/api/groupes`).
- [ ] **Step 5: Commit** « GRP-02 : les groupes du professeur dans des cadres, filtrés par jour ».

### Task 6: La création d'un groupe

**Files:** Create `frontend/src/app/pages/groupes/nouveau-groupe.ts|html|spec.ts`, `groupes.css` ; Modify `app.routes.ts` (+ `professeurs/groupes/nouveau`), `app.routes.spec.ts`

- [ ] **Step 1: Write the failing tests** : chargement des trois listes ; professeur d'office ; nom qui suit le premier créneau, puis plus après une saisie du nom ; ajouter/retirer un créneau ; envoi exact (prix « 60,5 » → 60.5, capacité vide → null) ; refus de la page (champ vide, prix illisible) ; réussite → navigation vers `/professeurs/groupes/g1` ; refus du serveur sous le formulaire ; bouton bloqué ; chargement raté → bouton bloqué ; adresse ouverte à la secrétaire avec « Professeurs » allumé.
- [ ] **Step 2–4:** RED, implémenter, GREEN.
- [ ] **Step 5: Commit** « GRP-04 : la page de création d'un groupe d'Étude ».

### Task 7: La fiche d'un groupe

**Files:** Create `frontend/src/app/pages/groupes/fiche-du-groupe.ts|html|spec.ts` ; Modify `app.routes.ts` (+ `professeurs/groupes/:id`), `app.routes.spec.ts`, `frontend/README.md`

- [ ] **Step 1: Write the failing tests** : en-tête et fil ; Mois et séances (« sam. 18/09 ») avec leur prix ; pastille « Commencé » ; « Modifier » rempli → PUT exact → « Groupe enregistré » et fiche à jour ; refus sous la carte ; 404 → « Ce groupe n'existe pas. ».
- [ ] **Step 2–4:** RED, implémenter, GREEN.
- [ ] **Step 5: Commit** « GRP-04 : la fiche d'un groupe, ses Mois et sa modification ».

### Task 8: Relecture, corrections, essai, fusion

- [ ] Relecteur neuf (Agent, opus) sur toute la branche ; Important → test qui échoue d'abord ; Mineurs → mémoire `project-groupes-reprises.md`.
- [ ] `dotnet ef database update`, essai dans l'aperçu (ordinateur et 375 px).
- [ ] Suites vertes, fusion locale dans `main`, branche supprimée.
