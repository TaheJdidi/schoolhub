# L'appel

Conception établie le 06/11/2026 pour Lassad et Taha. Elle ouvre l'épopée [Séances et appel](../../user-stories/06-seances-appel.md).

## Le découpage de l'épopée

1. **L'appel** (cette spec) : SEA-02 l'état des séances d'un groupe, SEA-07 faire l'appel, SEA-09 les appels à recopier, SEA-10 corriger une présence, SEA-11 prévenir les absents.
2. **Changer le calendrier** : SEA-03 annuler, SEA-04 déplacer, SEA-05 séance supplémentaire, SEA-06 changer l'horaire. C'est l'étape qui décale les Mois, donc l'argent.
3. **Feuille d'appel et rattrapage** : SEA-08 imprimer une feuille d'appel, SEA-12 inscrire un élève en rattrapage.
4. **L'accueil complet** : SEA-01, quand la caisse (CAI) et la bibliothèque (BIB) existeront. Cette étape-ci met déjà sur l'accueil les demandes à traiter et les appels à recopier.

## But de cette étape

Depuis la fiche d'un groupe, la secrétaire voit l'état de chaque séance et ouvre son appel. Tous les élèves sont présents ; elle touche les absents, puis enregistre. Elle peut corriger l'appel à tout moment, chaque correction allant au journal. Après l'appel, un message WhatsApp est prêt pour les destinataires de chaque absent. L'accueil liste les appels à recopier.

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Où vit le code ? | Un module `Seances` : l'absence, le service `AppelsDuCentre` et ses adresses. La séance reste dans `Groupes`. |
| Que garde-t-on ? | Seulement les absences (ticket 02, Q5 : présent sauf absent), une ligne par élève absent et par séance, rattachée à son inscription. La séance garde le moment de son premier appel (`AppelFaitLe`) : « appel fait » ou « appel à faire ». |
| Qui est attendu ? | Les élèves inscrits dans le groupe ce jour-là : arrivés à cette séance ou avant, pas encore arrêtés. Les rattrapages viendront à l'étape 3. |
| Quand l'appel s'ouvre-t-il ? | À partir du jour de la séance (date de Tunis). Avant : 409 « L'appel s'ouvre le jour de la séance ». |
| Une correction ? | Le même enregistrement, sur une séance « appel fait ». Chaque élève qui change écrit une ligne `PresenceCorrigee` au journal : l'élève, le groupe et la date de la séance, avec « Présent » ou « Absent » avant et après. Un premier appel n'y va pas : ce n'est pas une correction. |
| Ce qui est dû ? | Une Révision payée à la séance ne doit pas une séance manquée (ticket 02, Q15) : l'échéancier reçoit les numéros des séances manquées. Une somme déjà réglée le reste. L'Étude, la Révision en totalité et la Formation ne changent pas. Une correction met ce qui est dû à jour d'elle-même : rien ne s'enregistre, tout se calcule. |
| Le message d'absence ? | « Bonjour, Adem Miled était absent à la séance de mathématiques du samedi 15/01/2028, de 10h à 12h. Centre Les Jasmins. », pour les [destinataires](../../user-stories/00-exigences-communes.md#messages) de l'élève. La matière perd sa majuscule, sauf un sigle (« SVT »). |
| « Message ouvert » ? | Ouvrir WhatsApp marque l'absence (`PrevenuLe`) : la page le montre, pour ne pas l'envoyer deux fois. |
| Les appels à recopier ? | Les séances d'avant aujourd'hui encore « appel à faire », les plus anciennes en premier, sur l'accueil. Une séance compte à sa date même sans appel : la liste sert seulement à recopier les feuilles. |
| Deux appels en même temps ? | La ligne de la séance est verrouillée pendant l'enregistrement : le second voit le premier, et ses changements sont des corrections. |
| Un groupe fermé ? | Ses séances retirées emportent leurs absences (suppression en cascade). |

## 1. Les données

- **La séance** (`seances`) gagne `appel_fait_le`.
- **L'absence** (`Absence`, table `absences`, `IAppartientAuCentre`) : la séance (suppression en cascade), l'inscription (`Restrict`), `prevenu_le` ; une seule par séance et par inscription.
- **Le journal** gagne l'action `PresenceCorrigee`.

Migration `AjouterLAppel`.

## 2. Les adresses

Toutes pour une personne connectée du centre.

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/seances/{id}/appel` | l'appel d'une séance | 200, `{ "seanceId", "date", "debut", "fin", "groupe", "professeur", "ouvert", "appelFait", "eleves": [{ "inscriptionId", "eleveId", "prenom", "nom", "absent", "prevenuLe", "messages" }] }` ; 404 « Séance introuvable » |
| `PUT /api/seances/{id}/appel` | `{ "absents": [inscriptionId] }` : enregistre ou corrige | 200, l'appel ; 404 ; 409 « L'appel s'ouvre le jour de la séance » ; 400 « Cet élève n'est pas attendu à cette séance » |
| `POST /api/seances/{id}/absences/{inscription}/prevenu` | le message est ouvert | 204 ; 404 « Absence introuvable » |
| `GET /api/seances/a-recopier` | les appels à recopier | 200, `[{ "seanceId", "date", "debut", "fin", "groupe", "professeur" }]` |

La fiche d'un groupe (`GET /api/groupes/{id}`) donne aussi `appelFait` pour chaque séance.

## 3. Les pages

**La fiche d'un groupe** : chaque séance montre son état (« à venir », « appel fait », « appel à faire ») et ouvre son appel. Une Étude montre « séances 9 à 12 » pour chaque Mois et « En cours » sur le dernier Mois commencé ; une Révision ou une Formation, « Séance 2 sur 3 ».

**L'appel** (`/professeurs/groupes/{id}/appel/{seance}`) : « Appel du samedi 27/11/2027 », la matière, le professeur et l'heure ; « 12 présents · 2 absents » ; un bouton par élève, « Présent » ou « Absent » d'un toucher ; « Enregistrer l'appel », puis « Corriger l'appel ». Après l'appel : « Prévenir les parents des absents », avec « Ouvrir WhatsApp » pour chaque destinataire et « Message ouvert » ensuite. Avant le jour de la séance : « L'appel s'ouvre le jour de la séance. » Sur un téléphone, les élèves passent en une colonne.

**L'accueil** : « Appels à recopier · 3 », chaque séance avec « Recopier ».

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | L'appel s'ouvre avec les élèves attendus, tous présents ; l'enregistrer (absents, messages, texte exact, fiche du groupe) ; pas avant le jour ; un élève pas attendu ; corriger (journal, avant et après, auteur) ; une correction sans changement ; une Révision à la séance ne doit pas la séance manquée, et la doit de nouveau une fois corrigée ; une Étude doit son Mois ; un message ouvert ; les appels à recopier ; un autre centre ; une personne non connectée. |
| Angular | L'appel (présents, absent d'un toucher et retour, enregistrement exact, messages, message ouvert, correction, pas encore ouvert, refus du serveur, séance inconnue) ; l'état des séances et l'avancement sur la fiche du groupe ; les appels à recopier sur l'accueil ; les adresses. |
