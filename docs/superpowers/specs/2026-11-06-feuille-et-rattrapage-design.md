# La feuille d'appel et le rattrapage

Conception établie le 06/11/2026 pour Lassad et Taha. C'est l'étape 3 de l'épopée [Séances et appel](../../user-stories/06-seances-appel.md), selon le [découpage](2026-11-06-l-appel-design.md#le-découpage-de-lépopée).

## But de cette étape

La secrétaire imprime une feuille d'appel pour la séance du jour, un Mois ou des dates (SEA-08), que le professeur remplit sur papier. Depuis une séance, elle ajoute un élève d'un autre groupe du même professeur, de la même matière et du même niveau, qui rattrape une séance précise de son groupe (SEA-12). Il apparaît à part à l'appel et continue de payer dans son groupe.

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| La période de la feuille ? | Deux dates, du et au (3 mois au plus). La page les calcule : la séance du jour s'il y en a une, chaque Mois d'une Étude (le Mois en cours d'abord), les 4 prochaines séances d'une Révision ou d'une Formation, ou des dates au choix. |
| Ce que montre la feuille ? | En-tête : le centre, l'année scolaire, le groupe, la matière, le niveau, le professeur. Une colonne par séance qui a lieu (supplémentaires comprises, annulées non), avec sa date et son heure ; une ligne par élève inscrit pendant la période, par nom puis prénom, avec une case par séance et une case de signature ; 3 lignes vides « Élèves en rattrapage » ; la consigne « Écrivez A dans la case d'un élève absent. Les autres sont présents. » et la date d'impression. Ni téléphone, ni paiement. A4 en paysage, sans le menu. |
| Qui peut venir en rattrapage ? | Un élève inscrit ce jour-là dans un autre groupe ouvert du même professeur, de la même matière et du même niveau (une Formation sans niveau va avec une autre sans niveau). |
| Quelle séance rattrape-t-il ? | Une séance précise de son groupe, qui compte, déjà manquée ou à venir : la page propose celles des 8 semaines avant ou après la séance d'accueil, depuis son arrivée. Chaque séance de son groupe ne se rattrape qu'une fois. |
| Ce que l'on garde ? | Le rattrapage (`Rattrapage`, table `rattrapages`) : la séance d'accueil, l'inscription de l'élève dans son groupe, la séance rattrapée. Une séance retirée par la fermeture d'un groupe emporte ses rattrapages. |
| À l'appel ? | Les élèves en rattrapage sont « à part », avec « Paie dans son groupe d'origine », leur groupe et la séance rattrapée. Leur absence s'enregistre et se corrige comme les autres, et ils ont leur message d'absence. Dans leur groupe, la séance rattrapée montre « Rattrapée le 27/11 ». |
| L'argent ? | Rien ne change : le rattrapage n'est pas une inscription. L'élève ne compte pas dans les élèves du groupe d'accueil ; il n'entrera ni dans le badge « groupe payé » ni dans les couleurs (CAI-01, CAI-02). Une absence à la séance d'accueil ne touche pas ce qu'il doit dans son groupe. |
| Retirer un rattrapage ? | Possible, pour une erreur de saisie : son absence à la séance d'accueil part avec lui. |
| Le journal ? | Rien de plus : une correction de l'appel d'un élève en rattrapage y va comme les autres (SEA-10). |

## 1. Les données

**Le rattrapage** (`Rattrapage`, `IAppartientAuCentre`) : `seance_id` (suppression en cascade), `inscription_id` (`Restrict`), `seance_rattrapee_id` (suppression en cascade) ; un seul par séance et par inscription, et une séance de son groupe ne se rattrape qu'une fois. Migration `AjouterLesRattrapages`.

## 2. Les adresses

Toutes pour une personne connectée du centre.

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/groupes/{id}/feuille-d-appel?du=&au=` | la feuille d'appel | 200, `{ "centre", "annee", "groupe", "matiere", "niveau", "professeur", "seances": [{ "date", "debut" }], "eleves": [{ "nom", "prenom" }] }` ; 404 ; 400 « Choisissez la période », « La période finit avant de commencer », « La période fait au plus 3 mois » |
| `GET /api/seances/{id}/rattrapages/possibles` | les élèves qui peuvent venir, et leurs séances | 200, `[{ "inscriptionId", "eleveId", "prenom", "nom", "groupe", "seances" }]` ; 404 |
| `POST /api/seances/{id}/rattrapages` | `{ "inscriptionId", "seanceRattrapeeId" }` | 200, l'appel ; 404 « Inscription introuvable » ; 400 « Cet élève est déjà dans ce groupe », « Le rattrapage se fait dans un groupe du même professeur, de la même matière et du même niveau », « Cet élève n'est pas inscrit dans son groupe à la date de cette séance », « Choisissez la séance de son groupe qu'il rattrape » ; 409 « Cette séance est annulée : elle n'a pas d'appel », « Cet élève est déjà en rattrapage à cette séance », « Cette séance est déjà rattrapée » |
| `DELETE /api/seances/{id}/rattrapages/{rattrapage}` | retire le rattrapage | 200, l'appel ; 404 « Rattrapage introuvable » |

L'appel d'une séance donne aussi `rattrapages` et, pour chaque élève du groupe, `rattrapeeLe`. `PUT /api/seances/{id}/appel` accepte les inscriptions des élèves en rattrapage parmi les absents.

## 3. Les pages

**La fiche d'un groupe**, carte « Le calendrier » : « Imprimer une feuille d'appel », la période, puis « Ouvrir la feuille » (aussi pour un groupe fermé ou terminé).

**La feuille d'appel** (`/feuille-d-appel/{groupe}?du=&au=`, sans le menu) : la page A4 de la décision, avec « Imprimer » et « Retour au groupe », qui ne s'impriment pas.

**L'appel d'une séance** : « En rattrapage », à part, avec « Paie dans son groupe d'origine · Groupe Mardi 17h → 19h, rattrape le 30/11 » et « Retirer » ; « Rattrapée le 27/11 » sur un élève du groupe ; la carte « Rattrapage » : « Ajouter un élève en rattrapage », l'élève, la séance qu'il rattrape, « Ajouter en rattrapage ».

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Les élèves qui peuvent venir (même professeur, matière et niveau ; leurs séances dans la fenêtre) ; ajouter un rattrapage (à part, « rattrapée le », pas d'élève de plus) ; l'appel d'un élève en rattrapage ; chaque refus ; retirer ; la feuille d'un Mois (en-tête, séances annulées et supplémentaires, élèves, sans téléphone) ; les refus de la période ; un autre centre. |
| Angular | La feuille (en-tête, colonnes, lignes de rattrapage, consigne, refus) ; la période choisie (Mois, des dates, groupe fermé) ; l'appel avec des élèves en rattrapage ; ajouter un élève en rattrapage ; aucun élève possible ; l'adresse de la feuille. |
