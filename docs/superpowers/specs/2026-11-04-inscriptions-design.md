# Les inscriptions

Conception établie le 04/11/2026 pour Lassad et Taha. C'est l'étape 2 de l'épopée [Élèves et inscriptions](../../user-stories/04-eleves-inscriptions.md), après [la fiche élève](2026-11-04-fiche-eleve-design.md).

## But de cette étape

La secrétaire inscrit un élève dans un groupe, depuis la fiche de l'élève ou depuis le groupe. Elle choisit le mode de tarification et la première séance de l'élève. La plateforme lui dit ce que l'élève paiera à cette première séance (le Mois au prorata, les séances qui restent d'une Révision, l'assurance), la prévient si le groupe est complet, et prépare le message WhatsApp de confirmation. La fiche de l'élève montre ses inscriptions et son assurance ; le groupe montre ses élèves, et les cadres des groupes leur nombre.

Les paiements, la couleur et « À payer » viennent à l'étape 3 : elle s'appuiera sur l'échéancier calculé ici.

## Décisions

Lassad et Taha ont demandé de travailler sans questions et de prendre chaque fois la recommandation. Les voici.

| Question | Décision |
|---|---|
| Où vit le code ? | Un module `Inscriptions` (`backend/src/SchoolHub/Modules/Inscriptions/`). L'inscription relie un élève et un groupe, et recevra les paiements de l'étape 3. |
| Comment noter le début ? | Par la date de la première séance de l'élève (`DebutLe`), choisie parmi les séances du groupe : la séance du jour ou la prochaine par défaut. Une séance passée reste possible, pour un élève qui suit le groupe depuis plus longtemps que sa saisie. Une date plutôt qu'une séance : une séance annulée plus tard (SEA) ne casse rien. |
| Le mode de tarification ? | Étude : par Mois, sans choix. Révision : à la séance ou en totalité, à choisir. Formation : en une fois, ou en tranches si le groupe en a, à choisir ; sans tranches, en une fois sans choix. |
| L'arrivée en cours de Mois ? | Chaque séance d'un Mois vaut le quart du prix, la dernière prenant la différence (exigences communes). L'élève paie les parts des séances qu'il suit : Adem arrive à la 2ᵉ séance du Mois 3 à 40 TND, il paie 30,000 TND (3 séances). Un dernier Mois incomplet se compte de la même façon. |
| L'arrivée en cours de Révision ? | En totalité : le prix des séances qui restent, à sa première séance. À la séance : chaque séance qui reste, le jour même. |
| L'arrivée en cours de Formation ? | Le prix total, comme à la première séance : les stories ne prévoient pas de prorata (le propriétaire peut accorder un prix spécial, ELV-05). En tranches : une tranche dont la séance est passée est due à sa première séance. |
| L'assurance ? | À la première inscription en Étude d'une année scolaire, l'élève reçoit son assurance de l'année, au montant des paramètres du centre ce jour-là. Elle est due à sa première séance d'Étude de l'année. Un centre sans montant d'assurance n'en réclame pas. Payer et dispenser viennent à l'étape 3. |
| La capacité ? | Si le groupe compte déjà autant d'élèves que sa capacité, l'inscription passe avec l'avertissement « Ce groupe a atteint sa capacité. L'inscription reste possible. » La page le dit aussi dès qu'on choisit ce groupe. |
| Le message WhatsApp ? | Le serveur prépare le texte et les liens `wa.me`, pour les destinataires des exigences communes : majeur, l'élève seul ; mineur avec un téléphone, l'élève et le parent ; mineur sans téléphone, le parent seul. |
| Quels groupes proposer ? | Les groupes ouverts et pas terminés : ceux du niveau de l'élève d'abord, puis les autres (les Formations sans niveau comprises), par matière puis par nom. Un groupe où l'élève est déjà inscrit est montré, sans pouvoir le choisir. |
| Supprimer une fiche ? | Une fiche qui a une inscription ne se supprime plus : « Cette fiche a un historique : elle ne se supprime pas ». Le bouton disparaît de la fiche. |
| Une inscription faite par erreur ? | Rien pour l'instant : l'arrêt (ELV-08, étape 4) à sa première séance la rendra sans effet. |
| Le journal ? | Une inscription n'y va pas : la liste des exigences communes ne la cite pas. |
| Deux onglets qui inscrivent le même élève ? | La ligne de l'élève est verrouillée pendant l'inscription : le second voit l'élève déjà inscrit, et l'assurance n'est créée qu'une fois. |
| Les reprises de la fiche élève ? | Les 6 remarques de la relecture de l'étape 1 se font au début de cette étape (tâche 1 du plan). |

## 1. Les données

**L'inscription** (`Inscription`, table `inscriptions`, `IAppartientAuCentre`) :

| Champ | Contenu |
|---|---|
| `EleveId`, `GroupeId` | clés étrangères `Restrict` ; un seul couple (élève, groupe) : index unique |
| `Mode` | `ParMois`, `ALaSeance`, `EnTotalite`, `EnUneFois` ou `EnTranches` |
| `DebutLe` | la date de sa première séance |
| `InscritLe` | l'instant de l'enregistrement |

**L'assurance de l'année** (`AssuranceDeLAnnee`, table `assurances`, `IAppartientAuCentre`) : `EleveId`, `AnneeScolaireId` (un seul couple : index unique), `Montant` (au millime). Elle est due à la plus ancienne première séance de ses inscriptions en Étude de cette année.

## 2. L'échéancier

Une classe sans base de données (`Echeancier`) calcule, pour une inscription, toutes les sommes dues et la séance où chacune est due, à partir des séances du groupe (numérotées depuis `PremiereSeance`), de ses prix et de `DebutLe`. La première séance de l'élève est la première séance du groupe à cette date ou après.

| Type et mode | Sommes dues |
|---|---|
| Étude, par Mois | chaque Mois depuis le sien : « Mois 3 », au prix de ce Mois ; « Mois 3 (3 séances) » quand il n'en suit qu'une partie. Due à sa première séance du Mois. |
| Révision, à la séance | chaque séance depuis la sienne : « Séance 4 », au prix d'une séance (le total divisé par le nombre de séances, la dernière prenant la différence). |
| Révision, en totalité | « Totalité, séances 4 à 10 » : la somme de ces séances, due à sa première séance ; « Totalité » s'il les suit toutes. |
| Formation, en une fois | « Formation en une fois » : le prix total, dû à sa première séance. |
| Formation, en tranches | « Tranche 1 », « Tranche 2 »… : chacune à sa séance, ou à sa première séance si elle est passée. |

## 3. Les adresses

Toute personne connectée du centre. Un élève, un groupe ou une inscription d'un autre centre est introuvable (404).

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/inscriptions/groupes-possibles?eleve={id}` | les groupes proposés à l'élève | 200, `[{ "id", "nom", "type", "matiere", "professeur", "niveau", "duNiveauDeLEleve", "eleves", "capacite", "dejaInscrit" }]` |
| `POST /api/inscriptions` | inscrire, `{ "eleveId", "groupeId", "mode", "debutLe" }` | 201, `{ "inscription", "aPayerALArrivee", "avertissement", "messages" }` |
| `GET /api/inscriptions?eleve={id}` ou `?groupe={id}` | les inscriptions de l'élève (par nom du groupe) ou du groupe (par nom de l'élève) ; sans l'un ni l'autre : 400 « Choisissez l'élève ou le groupe » | 200, `[inscription]` |
| `GET /api/eleves/{id}/assurance` | l'assurance de l'année scolaire du centre (la plus récente) | 200, `{ "annee": "2026-2027", "montant", "dueLe" }`, ou 204 : sans objet |

**Une inscription, en JSON** : `{ "id", "eleve": { "id", "prenom", "nom" }, "groupe": { "id", "nom", "type", "matiere" }, "professeur", "mode", "prixNormal", "nombreDeTranches", "premiereSeance": { "date", "debut" } }`. `prixNormal` est le prix du Mois à venir pour une Étude, le prix total sinon ; `premiereSeance` vaut `null` si le groupe n'a plus de séance à cette date ou après.

**`aPayerALArrivee`** : les sommes dues à sa première séance, assurance comprise si elle vient d'être créée : `[{ "libelle": "Mois 3 (3 séances)", "montant": 30.000 }, { "libelle": "Assurance 2026-2027", "montant": 20.000 }]`.

**`messages`** : `[{ "destinataire": "Parent", "telephone": "98 401 220", "lien": "https://wa.me/21698401220?text=…" }]`, et le `texte` commun :

> Bonjour, l'inscription d'Adem Miled au Centre Les Jasmins est enregistrée.
> Mathématiques avec Karim Ben Ali, Groupe Samedi 10h → 12h : première séance le samedi 15/11/2026 à 10h.
> À bientôt. Centre Les Jasmins, 73 221 400

Sans téléphone du centre, la dernière ligne s'arrête au nom. Le destinataire vaut « Parent » ou le prénom de l'élève.

### Les refus

Dans cet ordre ; le premier refusé donne la réponse.

| Cas | Refus |
|---|---|
| Pas d'élève ; élève inconnu | 400 « Choisissez l'élève » ; 404 « Élève introuvable » |
| Pas de groupe ; groupe inconnu | 400 « Choisissez le groupe » ; 404 « Groupe introuvable » |
| Groupe fermé ; terminé | 409 « Ce groupe est fermé : on n'y inscrit plus » ; 409 « Ce groupe est terminé : on n'y inscrit plus » |
| Déjà inscrit | 409 « Cet élève est déjà inscrit dans ce groupe » |
| Mode absent alors qu'il y a un choix ; mode qui ne convient pas | 400 « Choisissez le mode de tarification » ; 400 « Ce mode de tarification ne convient pas à ce groupe » |
| Pas de date ; aucune séance du groupe ce jour-là | 400 « Choisissez la première séance » ; 400 « Ce groupe n'a pas de séance ce jour-là » |

## 4. Les pages

**L'inscription** (`/eleves/{id}/inscription` depuis la fiche, `/professeurs/groupes/{id}/inscription` depuis le groupe, sous la page Professeurs comme la fiche du groupe), titre « Inscrire Adem Miled » ou « Ajouter un élève » :

- depuis le groupe, la carte « L'élève » : le champ « Chercher un élève » (la recherche de l'étape 1) et les résultats à choisir ; un élève déjà inscrit est marqué « Déjà inscrit » et ne se choisit pas ;
- depuis l'élève, la carte « Le groupe » : « Groupes de son niveau », puis « Autres groupes » ; chaque groupe montre matière et nom, professeur, type, « 12 élèves sur 15 », et « Complet » s'il l'est ; « Déjà inscrit » ne se choisit pas ; « Aucun groupe ouvert pour l'instant. » sinon ;
- « Ce groupe a atteint sa capacité. L'inscription reste possible. » en orange sous un groupe complet choisi ;
- la carte « L'inscription » : le mode (Révision : « À la séance » ou « En totalité » ; Formation à tranches : « En une fois » ou « En 3 tranches »), et « Première séance » : la liste des séances du groupe (« samedi 15/11/2026 · 10h »), la séance du jour ou la prochaine choisie d'office (la dernière s'il n'y en a plus à venir) ;
- « Inscrire », bloqué pendant l'envoi ; « Choisissez l'élève et le groupe » si l'un manque ; le refus du serveur sous la carte.

Après l'enregistrement, la page devient la confirmation : « Inscription enregistrée », « Adem Miled · Groupe Samedi 10h → 12h, Mathématiques avec Karim Ben Ali » (sans accord : la fiche ne dit pas si l'élève est un garçon ou une fille) ; « À payer à sa première séance, le samedi 15/11/2026 » et ses lignes, ou « Rien à payer à sa première séance. » ; l'avertissement en orange ; puis, pour chaque destinataire, le texte du message et « Ouvrir WhatsApp » (« Au parent · 98 401 220 », « À Adem · 22 345 678 »), ou « Aucun téléphone pour envoyer la confirmation. » ; enfin « Voir la fiche de l'élève » et « Voir le groupe ».

**La fiche de l'élève** : la carte « Inscriptions » liste ses inscriptions (le groupe, en lien ; « Mathématiques · Karim Ben Ali » ; « Étude, 40,000 TND par Mois », « Révision en totalité, 100,000 TND », « Révision à la séance, 100,000 TND », « Formation en une fois, 300,000 TND », « Formation en 3 tranches, 300,000 TND » ; « Depuis le samedi 15/11/2026 ») et « Inscrire dans un groupe ». Sans inscription : « Aucune inscription : cet élève vient seulement à la bibliothèque. » La carte « Assurance » : « Année 2026-2027 : 20,000 TND, due le 15/11/2026 », ou « Sans objet : aucune inscription en Étude cette année. ». « Supprimer la fiche » n'apparaît plus si l'élève a une inscription.

**Le groupe** : la carte « Les élèves » liste ses élèves (le nom, en lien vers la fiche ; le mode ; « depuis le 15/11/2026 »), avec « Ajouter un élève » (`/professeurs/groupes/{id}/inscription`) tant que le groupe est ouvert et pas terminé ; « Aucun élève pour l'instant. » sinon. Le résumé et les cadres de la page Professeurs comptent les élèves inscrits (« 12 élèves sur 15 »).

Sur un téléphone, tout passe en une colonne, sans défilement de côté.

## 5. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET, l'échéancier | Les exemples d'ELV-03 : Adem au Mois 3 à 40 TND, 2ᵉ séance, doit 30,000 TND (3 séances) ; un prix qui change garde l'ancien pour un Mois commencé ; le dernier Mois incomplet ; un prix qui se divise mal (40,001 TND) ; Révision en totalité et à la séance, arrivée à la séance 4 ; un groupe déjà en cours (GRP-07) ; Formation en une fois et en tranches, une tranche passée due à la première séance. |
| .NET, l'API | Inscrire (201, mode d'office pour une Étude) ; chaque refus ; déjà inscrit ; groupe fermé ou terminé ; capacité (avertissement) ; assurance créée une fois par année, absente sans montant, absente pour une Révision ; les destinataires (majeur, mineur avec et sans téléphone) et le texte ; les listes par élève et par groupe ; les groupes possibles (niveau d'abord, fermés et terminés absents, déjà inscrit marqué) ; le nombre d'élèves des groupes ; la fiche avec inscription non supprimée ; isolation entre centres. |
| Angular | La page d'inscription depuis l'élève et depuis le groupe (choix, mode selon le type, première séance proposée, envoi exact, capacité, refus, bouton bloqué, confirmation avec les liens WhatsApp) ; la carte « Inscriptions » et « Assurance » de la fiche ; « Supprimer » caché ; la carte « Les élèves » du groupe ; les adresses. |
