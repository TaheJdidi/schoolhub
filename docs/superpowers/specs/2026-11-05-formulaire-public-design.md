# Le formulaire public et son lien

Conception établie le 05/11/2026 pour Lassad et Taha. Elle ouvre l'épopée [Demandes d'inscription](../../user-stories/05-demandes.md).

## Le découpage de l'épopée

1. **Le formulaire public et son lien** (cette spec) : DEM-01 remplir le formulaire sans compte, DEM-02 le lien et le QR code, avec l'affiche à imprimer.
2. **Traiter les demandes** : DEM-03 la liste et ses filtres, DEM-04 enregistrer une demande comme élève inscrit, DEM-05 refuser.

## But de cette étape

Un parent ou un élève ouvre le lien du centre (ou scanne son QR code), remplit la demande depuis son téléphone sans créer de compte, et l'envoie. La demande est enregistrée pour le centre. La secrétaire trouve le lien, le copie, voit le QR code et imprime l'affiche.

## Décisions

Lassad et Taha ont demandé de travailler sans questions et de prendre chaque fois la recommandation. La protection contre les envois abusifs était déjà tranchée (DEM-01, [ticket 12](../../wayfinder/cadrage-mvp/issues/12-architecture-technique.md)).

| Question | Décision |
|---|---|
| Où vit le code ? | Un module `Demandes` (`backend/src/SchoolHub/Modules/Demandes/`), de l'[ADR 0003](../../adr/0003-un-seul-deploiement-decoupe-en-modules.md). |
| Les envois abusifs ? | 5 envois par heure et par adresse ; le 6ᵉ reçoit 429 « Trop de demandes envoyées depuis cette connexion. Réessayez dans une heure. » (`AddRateLimiter`). Un champ piège invisible (`siteWeb`) : rempli, la demande fait comme si elle était envoyée, mais rien n'est enregistré. Un captcha seulement si des abus apparaissent. |
| L'adresse de l'envoi, derrière Azure ? | L'App Service ajoute l'adresse du visiteur dans `X-Forwarded-For`. `UseForwardedHeaders` lit la dernière entrée seulement (`ForwardLimit = 1`) : un visiteur ne peut pas se faire passer pour un autre en écrivant cet en-tête lui-même. |
| Le centre d'un formulaire ? | Il se lit dans l'adresse (`…/inscription/les-jasmins`, le code du centre). Le formulaire travaille dans un espace de services à lui, avec ce centre courant : une personne connectée à un autre centre peut aussi l'envoyer, et ne voit rien de son propre centre. |
| Les règles de la fiche ? | Les mêmes que la fiche élève (ELV-02), avec les mêmes messages, vérifiées par le même code. |
| Les groupes proposés ? | Les groupes ouverts et pas terminés du centre : ceux du niveau choisi, et les Formations sans niveau, par matière. Pour chacun : la matière, le professeur, le type et l'horaire. Jamais d'élève, ni de nombre d'inscrits. |
| Choisir ses matières ? | L'élève coche une matière, puis choisit son groupe préféré dans cette matière. Il en faut au moins une ; un seul groupe par matière. |
| Les amis ? | Un texte libre, facultatif, 500 caractères au plus. |
| La demande enregistrée ? | Avec toute la fiche, les groupes préférés, les amis, le jour d'arrivée, et l'état « À traiter ». La liste, l'enregistrement et le refus viennent à l'étape 2. |
| Le QR code ? | Fait par le serveur, avec la bibliothèque QRCoder (licence MIT), en image PNG : rien de plus à charger dans les pages. |
| L'affiche ? | Une page A4 à part, sans le menu : le nom du centre, « Demande d'inscription », le QR code, le lien, et « Imprimer » (caché à l'impression). |
| Le journal ? | Une demande n'y va pas. |

## 1. Les données

**La demande** (`DemandeDInscription`, table `demandes_d_inscription`, `IAppartientAuCentre`) : les champs de la fiche élève (prénom, nom, niveau, majeur, téléphones, emails, établissement) et son `NomDeRecherche`, `Amis` (500 caractères au plus), `RecueLe`, `Etat` (`ATraiter` pour l'instant) ; ses **groupes préférés** (table `groupes_demandes`, un par matière, clé étrangère `Restrict` vers le groupe).

## 2. Les adresses

| Adresse | Qui | Ce qu'elle fait | Réponse |
|---|---|---|---|
| `GET /api/public/inscription/{code}` | tout le monde | le formulaire du centre | 200, `{ "centre", "annee", "niveaux": [{ "id", "nom", "dOffice" }], "groupes": [{ "id", "matiere", "niveauId", "type", "professeur", "creneaux" }] }` ; 404 « Ce lien d'inscription n'existe pas » |
| `POST /api/public/inscription/{code}` | tout le monde | envoyer, la fiche + `groupes` (identifiants) + `amis` + `siteWeb` | 201, `{ "prenom" }` |
| `GET /api/demandes/lien` | connecté | le lien public du centre | 200, `{ "lien" }` |
| `GET /api/demandes/qr-code` | connecté | son QR code | 200, `image/png` |

Le lien vaut `Application:Adresse` suivi de `/inscription/{code}`. `annee` vaut « 2027-2028 », ou `null` si le centre n'a pas d'année scolaire.

### Les refus de l'envoi

Les refus de la fiche élève (ELV-02), dans leur ordre, puis :

| Cas | Refus |
|---|---|
| Aucun groupe | 400 « Choisissez au moins une matière » |
| Un groupe inconnu, fermé, terminé, d'un autre centre, ou d'un autre niveau | 400 « Ce groupe n'est plus proposé » |
| Deux groupes de la même matière | 400 « Choisissez un seul groupe par matière » |
| Amis trop longs | 400 « Les amis font au plus 500 caractères » |
| Plus de 5 envois dans l'heure | 429 « Trop de demandes envoyées depuis cette connexion. Réessayez dans une heure. » |

## 3. Les pages

**Le formulaire public** (`/inscription/{code}`, sans connexion, sans le menu) : en tête le nom du centre, « Demande d'inscription 2027-2028 » (sans l'année si le centre n'en a pas) et « Remplissez ce formulaire, sans créer de compte. Le centre vous confirmera le groupe et la date de la première séance sur WhatsApp. » ; la carte « L'élève » (prénom, nom, niveau scolaire, établissement, téléphone et email de l'élève avec « (facultatif avant la 7ᵉ année) » selon le niveau) ; la carte « Le parent » (« L'élève est majeur », téléphone WhatsApp du parent avec « (facultatif pour un élève majeur) », email du parent) ; la carte « Matières souhaitées » (« Pour chaque matière, choisissez le groupe que vous préférez. » ; « Choisissez d'abord le niveau scolaire. » ; une case par matière, puis la liste de ses groupes : « Karim Ben Salah · Samedi 10h → 12h », et « · Révision » ou « · Formation ») ; la carte « Amis » (« Amis qui suivront le même groupe (facultatif) ») ; « Envoyer ma demande », bloqué pendant l'envoi. Le refus s'affiche sous le formulaire. Envoyée : « Demande envoyée. Merci Adem ! Le centre vous confirmera le groupe et la date de la première séance sur WhatsApp. » Un lien inconnu : « Ce lien d'inscription n'existe pas. ».

**La page « Demandes d'inscription »** (`/demandes`, menu) : pour l'instant la carte « Le lien public du centre » : le lien, « Copier le lien » (« Lien copié » ensuite), le QR code, et « Imprimer l'affiche » vers `/demandes/affiche`.

**L'affiche** (`/demandes/affiche`, connecté, sans le menu) : la page A4 de la décision.

Sur un téléphone, tout passe en une colonne, sans défilement de côté.

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Le formulaire : les groupes ouverts et pas terminés, les Formations sans niveau, ni élève ni nombre d'inscrits ; un lien inconnu ; envoyer (la demande enregistrée pour le bon centre) ; chaque refus ; le champ piège ; la limite de 5 envois ; une personne connectée à un autre centre ; le lien et le QR code (PNG), réservés à une personne connectée. |
| Angular | Le formulaire (la règle de la 7ᵉ année, « majeur », les matières et leurs groupes selon le niveau, l'envoi exact avec le champ piège vide, la confirmation, les refus, la limite, un lien inconnu) ; la page « Demandes » (le lien, la copie, le QR code, l'affiche) ; l'affiche ; les adresses. |
