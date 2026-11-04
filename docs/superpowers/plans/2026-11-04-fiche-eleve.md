# La fiche élève : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** la secrétaire crée, modifie, cherche et supprime les fiches des élèves.

**Architecture:** un module `Eleves` : `Eleve` (donnée du centre, avec un `NomDeRecherche` sans accents), `ElevesDuCentre` (règles, doublon, recherche) derrière `ElevesEndpoints`. Côté Angular : un composant de recherche dans le cadre, la page « Élèves », le formulaire partagé, la page de création et la fiche.

**Tech Stack:** .NET 10, EF Core, PostgreSQL 17, xUnit ; Angular 22, Vitest.

**Spec:** [docs/superpowers/specs/2026-11-04-fiche-eleve-design.md](../specs/2026-11-04-fiche-eleve-design.md)

## Global Constraints

- Messages **à la lettre** de la spec ; refus par `ConnexionEndpoints.Refus` ; téléphones par `TelephoneTunisien`, emails par `AdresseEmail`.
- Tests .NET : `dotnet test --artifacts-path ../.superpowers/artefacts` ; Angular : `npm test -- --watch=false`. Arrêter le `serveur` de l'aperçu avant `dotnet ef migrations add`.
- Front : messages sous la carte concernée, boutons bloqués pendant l'envoi, `vi.waitFor` après un `flush`, pas de défilement de côté à 375 px.
- Commits sur `code/fiche-eleve`, signés Taha Jdidi, terminés par `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **« Hédi » cherché « HEDI » ou « hedi »** : trouvé. → tâche 2, `La_recherche_ignore_accents_et_majuscules`.
2. **Le téléphone d'un parent tapé comme le téléphone d'un autre élève** : « Doublon possible », dans les deux sens. → tâche 1, `Un_telephone_croise_previent_d_un_doublon`.
3. **Un élève de 6ᵉ année sans téléphone ni email, puis passé en 7ᵉ** : accepté, puis refusé à la modification sans téléphone. → tâche 1, `La_regle_de_la_7e_annee`.
4. **Modifier un élève sans rien changer** : pas de « Doublon possible » contre lui-même. → tâche 1.
5. **La recherche de l'en-tête tapée vite** (« a », « ad », « ade ») : une seule requête après la pause, pour « ade ». → tâche 3.

---

### Task 1: La fiche élève (API)

**Files:** Create `backend/src/SchoolHub/Modules/Eleves/Eleve.cs`, `ElevesDuCentre.cs`, `ElevesEndpoints.cs` ; Modify `SchoolHubDbContext.cs` (`Eleves`), `Program.cs`, `Socle/Centres/NiveauxDuCentre.cs` (niveau utilisé par un élève), `backend/README.md` ; migration `AjouterLesEleves` ; Test `tests/SchoolHub.Tests/Eleves/ElevesTests.cs`.

**Interfaces — Produces:** `FicheSaisieDeLEleve(string? Prenom, string? Nom, Guid? NiveauId, bool Majeur, string? TelephoneParent, string? EmailParent, string? TelephoneEleve, string? EmailEleve, string? Etablissement)` ; `FicheDeLEleve(Guid Id, string Prenom, string Nom, NiveauDeLEleve Niveau, bool Majeur, string? TelephoneParent, string? EmailParent, string? TelephoneEleve, string? EmailEleve, string? Etablissement)` ; `EnregistrementDeLEleve(FicheDeLEleve Eleve, string? Avertissement)` ; `EleveTrouve(Guid Id, string Prenom, string Nom, string Niveau)` ; `Recherche.Normaliser(string)` (sans accents, minuscules) ; adresses de la spec §2.

- [ ] **Step 1: Write the failing tests** : créer ; chaque refus (Theory) ; `La_regle_de_la_7e_annee` (6ᵉ sans téléphone ni email : 201 ; 7ᵉ : refusé ; niveau ajouté : refusé ; majeur sans téléphone du parent : 201) ; doublon par le nom (« hédi ben salah » contre « Hedi Ben Salah ») et `Un_telephone_croise_previent_d_un_doublon` ; modifier (pas de doublon contre soi) ; supprimer ; 404 inconnu et autre centre ; liste par nom ; niveau ajouté utilisé par un élève non retiré.
- [ ] **Step 2–4:** RED, implémenter, migration, GREEN, toute la suite.
- [ ] **Step 5: Commit** « ELV-02 et ELV-11 : la fiche d'un élève ».

### Task 2: La recherche (API)

**Files:** `ElevesDuCentre.cs`, `ElevesEndpoints.cs` ; Test `ElevesTests.cs`

- [ ] **Step 1: Write the failing tests** : `La_recherche_ignore_accents_et_majuscules` ; « miled adem » ; « 22 345 » trouve le téléphone de l'élève et celui du parent ; un autre centre invisible ; 100 résultats au plus.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-01 : chercher un élève ».

### Task 3: La recherche en haut de chaque écran

**Files:** Create `frontend/src/app/cadre/recherche-d-eleves.ts|spec.ts` ; Modify `cadre.html|css|ts`, `cadre.spec.ts` (les tests du cadre ne cherchent rien)

- [ ] **Step 1: Write the failing tests** : rien avant 2 caractères ; une seule requête après la pause (« ade ») ; résultats (nom, niveau) ; « Aucun élève trouvé » ; clic → `/eleves/e1`, champ vidé ; Échap ferme.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-01 : la recherche en haut de chaque écran ».

### Task 4: La page « Élèves », la création et la fiche

**Files:** Create `frontend/src/app/pages/eleves/eleve.ts`, `formulaire-de-l-eleve.ts|html`, `eleves.ts|html`, `nouvel-eleve.ts`, `fiche-de-l-eleve.ts|html`, `eleves.css` et leurs tests ; Modify `app.routes.ts` (`eleves` dans `PAGES`, `eleves/nouveau`, `eleves/:id`), `app.routes.spec.ts`, `cadre/cadre.spec.ts` si besoin, `frontend/README.md`

- [ ] **Step 1: Write the failing tests** : page Élèves (liste, sous-titre, filtre par la recherche, vide, aucun résultat) ; formulaire (facultatif selon le niveau et « majeur », envoi exact, « Remplissez les champs obligatoires », refus, bouton bloqué) ; création → fiche ; fiche (contacts, « Aucune inscription… », modifier avec avertissement orange, supprimer avec confirmation → `/eleves`, introuvable) ; adresses.
- [ ] **Step 2–5:** RED, implémenter, GREEN, commit « ELV-02 et ELV-04 : la page Élèves, la création et la fiche ».

### Task 5: Relecture, corrections, essai, fusion

- [ ] Relecteur neuf (opus) ; Important → test d'abord ; Mineurs → mémoire `project-fiche-eleve-reprises.md` ; essai dans l'aperçu ; fusion locale.
