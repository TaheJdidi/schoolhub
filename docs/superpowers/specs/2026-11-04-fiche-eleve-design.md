# La fiche élève

Conception établie le 04/11/2026 pour Lassad et Taha. Elle ouvre l'épopée [Élèves et inscriptions](../../user-stories/04-eleves-inscriptions.md).

## Le découpage de l'épopée

L'épopée est grosse, et plusieurs stories dépendent de « ce que l'élève doit », que la caisse utilisera aussi. Elle se fait en quatre étapes, chacune avec sa spec :

1. **La fiche élève** (cette spec) : ELV-02 créer ou modifier, ELV-01 la recherche en haut de chaque écran, ELV-11 supprimer une fiche sans historique, la page « Élèves », et la fiche avec ses contacts (une partie d'ELV-04).
2. **Les inscriptions** : ELV-03, l'arrivée en cours de Mois ou de Révision, l'assurance due, la capacité, le message WhatsApp, et les inscriptions sur la fiche ; le nombre d'élèves sur les cadres des groupes.
3. **Ce que l'élève doit** : les sommes dues (Mois, séance, totalité, tranche, assurance), ELV-05 prix spécial, ELV-06 dispense d'assurance, ELV-07 payé avant SchoolHub, ELV-10 Formation payée en une fois ; « À payer » sur la fiche (sans couleur : CAI-01).
4. **Arrêter et changer de groupe** : ELV-08, ELV-09.

## But de cette étape

La secrétaire enregistre les élèves avec les contacts utiles, les retrouve en tapant quelques lettres depuis n'importe quel écran, ouvre leur fiche, et supprime une fiche créée par erreur.

## Décisions

Lassad et Taha ont demandé de travailler sans questions et de prendre chaque fois la recommandation. Les voici.

| Question | Décision |
|---|---|
| Où vit le code ? | Un module `Eleves` (`backend/src/SchoolHub/Modules/Eleves/`), de l'[ADR 0003](../../adr/0003-un-seul-deploiement-decoupe-en-modules.md). L'élève appartient au centre (`IAppartientAuCentre`). |
| « À partir de la 7ᵉ année de base » : quels niveaux ? | Les niveaux d'office de rang 7 ou plus (7ᵉ année de base et tout le secondaire), et les niveaux ajoutés par le centre (université, adultes), comme l'« Adulte » de la maquette. Pour eux, le téléphone et l'email de l'élève sont obligatoires. |
| La recherche sans accents ni majuscules ? | L'élève garde un `NomDeRecherche` : son prénom et son nom sans accents, en minuscules (« hedi ben salah »). Chaque mot tapé doit s'y trouver : « miled adem » trouve Adem Miled. Une saisie faite de chiffres (espaces permis) cherche dans les deux téléphones : « 22 345 » trouve 22 345 678. |
| Une seule recherche, ou deux ? | Une seule, sur le serveur : l'en-tête et la page « Élèves » appellent la même adresse. L'en-tête montre les 8 premiers résultats. |
| « Doublon possible » ? | Comme pour les professeurs : l'élève s'enregistre, et la réponse porte un avertissement : « Doublon possible : Adem Miled (9ᵉ année de base) ». Un autre élève du centre compte s'il a le même prénom et le même nom (sans accents ni majuscules), ou un téléphone en commun (celui de l'élève ou du parent, dans un sens ou dans l'autre). |
| Supprimer ? | Toujours possible pour l'instant : il n'y a encore ni inscription, ni paiement, ni présence. Chaque étape suivante ajoutera son refus (« Cette fiche a un historique : elle ne se supprime pas »). |
| Le niveau d'un élève, et les niveaux ? | Un niveau ajouté qu'un élève utilise ne se retire plus (le refus de GRP-04 compte aussi les élèves). |
| Le journal ? | Ni la création ni la modification d'une fiche n'y vont : la liste des exigences communes ne les cite pas. |
| Qui y a accès ? | Toute personne connectée du centre. |

## 1. Les données

**L'élève** (`Eleve`, table `eleves`, `IAppartientAuCentre`) :

| Champ | Contenu |
|---|---|
| `Prenom`, `Nom` | 100 caractères au plus |
| `NiveauId` | clé étrangère `Restrict` vers le niveau |
| `Majeur` | vrai : le téléphone du parent devient facultatif |
| `TelephoneParent`, `TelephoneEleve` | 8 chiffres (`TelephoneTunisien`), facultatifs selon les règles |
| `EmailParent`, `EmailEleve` | 256 caractères au plus (`AdresseEmail`) |
| `Etablissement` | facultatif, 200 caractères au plus |
| `NomDeRecherche` | « prénom nom » sans accents, en minuscules ; tenu à jour à chaque enregistrement |

## 2. Les adresses

Toute personne connectée du centre. Un élève d'un autre centre est « Élève introuvable » (404).

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/eleves?recherche=adem` | les élèves, par nom puis prénom ; avec `recherche`, ceux qui correspondent (100 au plus) | 200, `[{ "id", "prenom", "nom", "niveau" }]` |
| `POST /api/eleves` | créer une fiche | 201, `{ "eleve": {…}, "avertissement": … }` |
| `GET /api/eleves/{id}` | la fiche | 200 |
| `PUT /api/eleves/{id}` | modifier la fiche | 200, `{ "eleve", "avertissement" }` |
| `DELETE /api/eleves/{id}` | supprimer la fiche | 204 |

**La fiche, en JSON** : `{ "id", "prenom", "nom", "niveau": { "id", "nom" }, "majeur", "telephoneParent", "emailParent", "telephoneEleve", "emailEleve", "etablissement" }`, les téléphones groupés (« 98 401 220 »), les champs vides à `null`.

### Les règles de saisie

Dans l'ordre du formulaire, sans les espaces autour ; le premier refusé donne la réponse.

| Champ | Règle | Refus |
|---|---|---|
| Prénom, Nom | obligatoires, 100 caractères au plus | 400 « Le prénom est obligatoire » ; « Le prénom fait au plus 100 caractères » ; « Le nom est obligatoire » ; « Le nom fait au plus 100 caractères » |
| Niveau | obligatoire, du centre | 400 « Choisissez le niveau » ; 404 « Niveau introuvable » |
| Téléphone de l'élève | obligatoire à partir de la 7ᵉ année de base ; 8 chiffres | 400 « Le téléphone de l'élève est obligatoire à partir de la 7ᵉ année de base » ; « Le téléphone de l'élève doit avoir 8 chiffres » |
| Email de l'élève | obligatoire à partir de la 7ᵉ année de base ; valable | 400 « L'email de l'élève est obligatoire à partir de la 7ᵉ année de base » ; « L'email de l'élève n'est pas valable » |
| Téléphone du parent | obligatoire, sauf pour un élève majeur ; 8 chiffres | 400 « Le téléphone du parent est obligatoire » ; « Le téléphone du parent doit avoir 8 chiffres » |
| Email du parent | facultatif ; valable | 400 « L'email du parent n'est pas valable » |
| Établissement | facultatif, 200 caractères au plus | 400 « L'établissement fait au plus 200 caractères » |

## 3. Les pages

**La recherche, en haut de chaque écran** (le cadre) : un champ « Chercher un élève ou le téléphone d'un parent ». Dès 2 caractères, les résultats s'affichent pendant la frappe (après une courte pause) : nom et niveau ; « Aucun élève trouvé » sinon. Un clic ouvre la fiche, et le champ se vide. Échap ferme la liste. Sur un téléphone, le champ passe sous le nom du centre, sur toute la largeur. (Les groupes et la couleur viendront avec les étapes 2 et 3.)

**La page « Élèves »** (`/eleves`) : « 3 élèves » en sous-titre, « Ajouter un élève », un champ « Filtrer la liste » (la même recherche), et la liste : nom, niveau, téléphone du parent (ou de l'élève). Un clic ouvre la fiche. Liste vide : « Aucun élève pour l'instant. Ajoutez le premier. » ; aucun résultat : « Aucun élève trouvé. ».

**La création** (`/eleves/nouveau`) et **la modification** (sur la fiche) partagent le même formulaire : « L'élève » (prénom, nom, niveau, établissement, téléphone et email de l'élève, avec « (facultatif avant la 7ᵉ année) » selon le niveau choisi) et « Le parent » (« L'élève est majeur », téléphone WhatsApp du parent, « (facultatif pour un élève majeur) », email du parent). Les champs obligatoires vides ne partent pas : « Remplissez les champs obligatoires ». Le bouton se bloque pendant l'envoi ; le refus du serveur s'affiche sous le formulaire ; l'avertissement « Doublon possible » s'affiche en orange sur la fiche après l'enregistrement.

**La fiche** (`/eleves/{id}`) : le nom en titre, le niveau en sous-titre, et « Modifier » ; la carte « Contacts » (élève, parent, établissement, « Élève majeur ») ; la carte « Inscriptions » : « Aucune inscription : cet élève vient seulement à la bibliothèque. » ; « Supprimer la fiche » avec confirmation (« Supprimer la fiche d'Adem Miled ? Seulement en cas d'erreur de saisie. Oui · Non ») ; supprimée, la page revient à « Élèves ». Élève introuvable : « Cet élève n'existe pas. ».

Sur un téléphone, tout passe en une colonne, sans défilement de côté.

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Créer (téléphones groupés, emails vides à `null`) ; chaque refus, avec la règle de la 7ᵉ année (6ᵉ : facultatif ; 7ᵉ et niveau ajouté : obligatoire) et l'élève majeur ; doublon par le nom (accents, majuscules) et par un téléphone croisé ; modifier, et pas de doublon contre soi-même ; supprimer ; recherche (« hedi » trouve « Hédi », « miled adem », « 22 345 » dans les deux téléphones, ordre par nom) ; isolation entre centres ; niveau utilisé par un élève non retiré. |
| Angular | La recherche de l'en-tête (pause, 2 caractères, résultats, aucun, clic, Échap) ; la page Élèves (liste, filtre, vide) ; le formulaire (facultatif selon le niveau et « majeur », envoi exact, refus, bouton bloqué) ; la fiche (contacts, modifier, avertissement orange, supprimer avec confirmation, introuvable) ; les adresses. |
