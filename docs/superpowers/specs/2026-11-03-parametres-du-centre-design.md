# Paramètres du centre et niveaux

Conception établie le 03/11/2026 avec Lassad et Taha. Elle couvre les deux premières stories de l'épopée [Centre](../../user-stories/02-centre.md) qui restent après CEN-01.

## But

Le propriétaire règle son centre : ses coordonnées, son année scolaire et ses prix, que les stories suivantes utiliseront (groupes, assurance, bibliothèque, parts des professeurs, reçus). Il complète aussi la liste des niveaux scolaires tunisiens avec les publics qui n'y sont pas.

## Périmètre

**Dans cette étape :**

- [CEN-02](../../user-stories/02-centre.md#cen-02--régler-les-paramètres-du-centre) : la page « Paramètres du centre », et les réglages qu'elle enregistre ;
- [CEN-03](../../user-stories/02-centre.md#cen-03--compléter-la-liste-des-niveaux) : la liste des niveaux, avec ses 29 niveaux d'office et les niveaux ajoutés par le centre.

**Plus tard :**

- [CEN-04](../../user-stories/02-centre.md#cen-04--passer-à-lannée-scolaire-suivante) (année suivante) : après les groupes (GRP), quand il y aura des groupes d'Étude à terminer et des Mois à remettre à 1.
- Ce que d'autres stories feront des réglages, à leur tour :
  - **recopier la valeur au moment où quelque chose devient dû** (« ce qui est déjà dû garde l'ancienne valeur ») : l'assurance (inscriptions), la part du centre (parts des professeurs) ;
  - **dire qu'un réglage manque** quand elles en ont besoin : GRP-04 refuse un groupe d'Étude sans année scolaire, BIB-01 une entrée sans prix ;
  - **le nom et le téléphone du centre sur les reçus, et la copie des reçus à l'email du centre** : stories de la caisse ;
  - **refuser de retirer un niveau utilisé** : GRP-04 et ELV-02, quand leurs groupes et leurs élèves pointeront vers un niveau.

## Décisions

| Question | Décision |
|---|---|
| Quelles stories maintenant ? | CEN-02 et CEN-03. CEN-04 agit sur des groupes, des Mois et une assurance qui n'existent pas encore. |
| Quels réglages sont obligatoires ? | Seul le nom. Le propriétaire remplit ce qu'il sait ; ce qui a besoin d'un réglage manquant le dit au bon moment. |
| Comment tenir « ce qui est déjà dû garde l'ancienne valeur » ? | On garde seulement la valeur actuelle. Chaque story qui crée quelque chose de dû y recopie la valeur du moment. Pas d'historique des réglages. |
| Où vivent les niveaux ? | Une seule table, par centre. Les 29 niveaux d'office sont recopiés dans chaque centre, avec une marque « d'office ». Les groupes et les élèves pointeront vers une seule table, et le filtre par centre marche seul. |
| Où vit l'année scolaire ? | Dans sa propre table, une ligne par année et par centre. CEN-04 y ajoutera l'année suivante pendant que l'année en cours tourne encore. |
| La liste des niveaux d'office est-elle juste ? | Oui, validée par Lassad et Taha (section 2). |
| Le journal ? | Ni les réglages ni les niveaux n'y vont : la liste des [exigences communes](../../user-stories/00-exigences-communes.md#journal) ne les cite pas. |

## 1. Les réglages et l'année scolaire (CEN-02)

### Les données

**Le centre** (`Centre`, donnée commune) gagne ses réglages, tous vides au départ sauf le nom, qui vient de la commande CEN-01 :

| Champ | Type | Exemple |
|---|---|---|
| `Telephone` | texte, 8 chiffres | 71234567 |
| `Email` | texte, 256 caractères au plus | contact@jasmins.tn |
| `MontantDeLAssurance` | dinars au millime (`numeric(12,3)`) | 20,000 |
| `PrixDeLEntreeALaBibliotheque` | dinars au millime | 2,500 |
| `CapaciteDeLaBibliotheque` | entier | 40 |
| `PartDuCentreParDefaut` | pourcentage (`numeric(5,2)`) | 5,00 |

Le centre est une donnée commune (CEN-01) : il ne passe pas par le filtre automatique. Les réglages se lisent et s'écrivent donc pour le centre courant, explicitement (`Id == centreCourant.Id`), avec un test d'isolation.

**L'année scolaire** (`AnneeScolaire`, table `annees_scolaires`) appartient au centre (`IAppartientAuCentre`), avec deux dates (`Debut`, `Fin`, en `date`). Pour l'instant, le centre n'en a qu'une : la page règle la plus récente (la plus grande date de début). CEN-04 en ajoutera d'autres.

### Les adresses

Réservées au propriétaire (politique `Proprietaire`).

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/centre/parametres` | lit les réglages | 200, les réglages |
| `PUT /api/centre/parametres` | enregistre tout le formulaire d'un coup | 200, les réglages enregistrés |

**Les réglages, en JSON** :

```json
{
  "nom": "Centre Les Jasmins",
  "telephone": "71 234 567",
  "email": "contact@jasmins.tn",
  "anneeScolaire": { "debut": "2027-09-15", "fin": "2028-06-30" },
  "montantDeLAssurance": 20.000,
  "prixDeLEntreeALaBibliotheque": 2.500,
  "capaciteDeLaBibliotheque": 40,
  "partDuCentreParDefaut": 5
}
```

Un réglage vide vaut `null`, et `anneeScolaire` aussi quand l'année n'est pas réglée. Le téléphone est renvoyé groupé (« 71 234 567 »).

### Les règles de saisie

Les espaces autour des textes sont retirés. Le premier champ refusé donne la réponse, et rien n'est enregistré.

| Champ | Règle | Refus (400) |
|---|---|---|
| Nom | obligatoire, 200 caractères au plus | « Le nom du centre est obligatoire » ; « Le nom du centre fait au plus 200 caractères » |
| Téléphone | facultatif : 8 chiffres, espaces permis, « +216 » ou « 00216 » permis devant ; enregistré en 8 chiffres | « Le téléphone doit avoir 8 chiffres » |
| Email | facultatif : la règle `AdresseEmail.EstValable` des comptes | « Cet email n'est pas valable » |
| Année scolaire | facultative ; les deux dates ensemble ; la fin après le début ; un an au plus (la fin avant le même jour de l'année suivante) | « L'année scolaire doit avoir une date de début et une date de fin » ; « La fin de l'année scolaire doit venir après son début » ; « L'année scolaire dure un an au plus » |
| Assurance, prix de l'entrée | facultatifs : 0 ou plus, 3 décimales au plus, moins d'un million | « Un montant doit être positif, au millime près » |
| Capacité | facultative : entier, 1 ou plus | « La capacité doit être d'au moins 1 place » |
| Part du centre | facultative : de 0 à 100, 2 décimales au plus | « La part du centre va de 0 à 100 % » |

**L'année scolaire s'enregistre ainsi** : si le centre n'en a pas, elle est créée ; sinon, la plus récente prend les nouvelles dates. Envoyer `anneeScolaire: null` alors qu'une année existe ne l'efface pas : elle reste (rien ne disparaît), et la réponse la renvoie.

## 2. Les niveaux (CEN-03)

### Les données

**Le niveau** (`Niveau`, table `niveaux`) appartient au centre (`IAppartientAuCentre`) :

| Champ | Contenu |
|---|---|
| `Nom` | 100 caractères au plus, par exemple « 4ᵉ année secondaire, section mathématiques » |
| `DOffice` | vrai pour les 29 niveaux de la liste tunisienne : ils ne se renomment ni ne se retirent |
| `Rang` | l'ordre scolaire des niveaux d'office (1 à 29) ; vide pour un niveau ajouté |

Un index unique sur le centre et le nom normalisé (en minuscules) empêche deux niveaux du même nom dans un centre.

### Les 29 niveaux d'office

Écrits une seule fois dans le code (`NiveauxDOffice.Liste`), dans cet ordre :

1. 1ʳᵉ année de base … 9. 9ᵉ année de base (les 9 années de l'enseignement de base) ;
10. 1ʳᵉ année secondaire ;
11. à 15. 2ᵉ année secondaire, section sciences · lettres · économie et services · technologies de l'informatique · sport ;
16. à 22. 3ᵉ année secondaire, section mathématiques · sciences expérimentales · sciences techniques · sciences de l'informatique · économie et gestion · lettres · sport ;
23. à 29. 4ᵉ année secondaire, avec les mêmes 7 sections.

Le nom s'écrit « 2ᵉ année secondaire, section sciences », comme l'exemple de la story.

Ils sont recopiés :

- **dans chaque nouveau centre**, par la commande CEN-01, dans la même transaction que le centre ;
- **dans les centres qui existent déjà**, par la migration qui crée la table. Comme pour le trigger du journal, la migration utilise la liste du code, avec un commentaire : si la liste change un jour, la nouvelle migration recopiera ici l'ancienne liste pour figer celle-ci.

### Les adresses

| Adresse | Qui | Ce qu'elle fait | Réponse |
|---|---|---|---|
| `GET /api/niveaux` | toute personne connectée | la liste : d'office par rang, puis les ajoutés par nom | 200 |
| `POST /api/niveaux` | propriétaire | ajouter `{ "nom": "Université, 1ʳᵉ année" }` | 201, le niveau |
| `PUT /api/niveaux/{id}` | propriétaire | renommer un niveau ajouté | 200, le niveau |
| `DELETE /api/niveaux/{id}` | propriétaire | retirer un niveau ajouté | 204 |

La secrétaire lit la liste : elle en aura besoin pour les groupes, les élèves et le formulaire public. **Un niveau, en JSON** : `{ "id", "nom", "dOffice" }`.

| Cas | Statut | Message |
|---|---|---|
| Nom vide | 400 | « Le nom du niveau est obligatoire » |
| Nom de plus de 100 caractères | 400 | « Le nom du niveau fait au plus 100 caractères » |
| Nom déjà dans la liste du centre, d'office compris, sans tenir compte des majuscules | 409 | « Ce niveau existe déjà » |
| Renommer ou retirer un niveau d'office | 409 | « Les niveaux d'office ne se modifient pas » |
| Niveau inconnu ou d'un autre centre | 404 | « Niveau introuvable » |

**Une limite assumée** : la story ne permet de retirer un niveau que si aucun groupe ni aucun élève ne l'utilise. Les groupes et les élèves n'existent pas encore : un niveau ajouté se retire donc toujours. GRP-04 et ELV-02 ajouteront ce refus (« Ce niveau est utilisé par un groupe ou un élève »), avec son test, quand ils lieront leurs données aux niveaux.

## 3. La page « Paramètres du centre »

À l'adresse `/parametres`, dans le menu du propriétaire, elle remplace la page « bientôt ». La secrétaire voit toujours « Accès réservé au propriétaire ».

```
Paramètres du centre
Les informations et les prix de votre centre.

┌─ Le centre ──────────────────────────────────────────────────────────────┐
│ Nom [Centre Les Jasmins        ]  Téléphone [71 234 567]  Email [contact@…]│
│ Année scolaire   du [15/09/2027]  au [30/06/2028]                          │
│ Assurance [20,000] TND    Part du centre par défaut [5] %                  │
│ Bibliothèque : entrée [2,500] TND   capacité [40] places (facultatif)      │
│                                                       [ Enregistrer ]      │
│ ✓ Paramètres enregistrés                                                   │
└───────────────────────────────────────────────────────────────────────────┘

┌─ Les niveaux ────────────────────────────────────────────────────────────┐
│ Les niveaux ajoutés par le centre :                                       │
│   Université, 1ʳᵉ année                         [ Renommer ] [ Retirer ]  │
│   [ Nouveau niveau…              ] [ Ajouter ]                            │
│ ▸ Les 29 niveaux d'office (ils ne se modifient pas)                       │
└───────────────────────────────────────────────────────────────────────────┘
```

**La carte « Le centre »**

- Les montants s'écrivent avec une virgule (« 2,500 ») ; la page les envoie en nombre. Un point est accepté aussi. Une saisie qui n'est pas un nombre est refusée par la page : « Écrivez un montant, par exemple 2,500 ».
- Les dates utilisent le champ de date du navigateur.
- Sans année scolaire, une note le rappelle : « Sans année scolaire, on ne peut pas créer de groupe d'Étude. »
- « Enregistrer » envoie tout le formulaire ; le bouton se bloque pendant l'envoi ; « Paramètres enregistrés » s'affiche sous la carte ; un refus s'affiche en rouge sous la carte.

**La carte « Les niveaux »**

- Les niveaux ajoutés, chacun avec « Renommer » et « Retirer ». S'il n'y en a pas : « Aucun niveau ajouté ».
- « Renommer » change la ligne en champ de saisie, avec « Enregistrer » et « Annuler ».
- « Retirer » demande confirmation dans la ligne : « Retirer « Université, 1ʳᵉ année » ? Oui · Non ».
- Le champ « Nouveau niveau » et « Ajouter » ; un nom vide ne part pas.
- Les 29 niveaux d'office, repliés dans un `<details>` « Les 29 niveaux d'office (ils ne se modifient pas) ».
- Les messages de la carte s'affichent sous elle (« Niveau ajouté », « Niveau renommé », « Niveau retiré », ou le refus en rouge). La relecture de la page « Comptes » a montré qu'un message unique en haut de page peut sortir de l'écran.

Sur un téléphone, les champs passent les uns sous les autres, sans défilement de côté.

## 4. L'organisation du code

```
backend/src/SchoolHub/Socle/Centres/
├── Centre.cs                       + les réglages, et Regler(…)
├── CentreConfiguration.cs          + les colonnes des réglages
├── AnneeScolaire.cs                l'année et sa configuration
├── ParametresDuCentre.cs           lire et enregistrer les réglages : les règles de saisie
├── ParametresEndpoints.cs          GET et PUT /api/centre/parametres
├── Niveau.cs                       le niveau, sa configuration, et NiveauxDOffice.Liste
├── NiveauxDuCentre.cs              lister, ajouter, renommer, retirer, recopier les niveaux d'office
├── NiveauxEndpoints.cs             les 4 adresses des niveaux
└── CreationDeCentre.cs             + recopie les niveaux d'office

frontend/src/app/pages/parametres/  la page, ses deux cartes, et leurs tests
```

## 5. Les tests

### Côté .NET (`dotnet test`)

| Sujet | Ce que les tests vérifient |
|---|---|
| Réglages | Un nouveau centre n'a que son nom. Enregistrer puis relire rend les mêmes valeurs (montants au millime, téléphone groupé). Chaque refus du tableau de la section 1, sans rien enregistrer. Seul le nom suffit. |
| Année scolaire | Créée au premier enregistrement, modifiée au suivant (toujours une seule année) ; `null` ne l'efface pas. |
| Droits | Une secrétaire reçoit 403 sur les réglages, et une personne non connectée 401. Un centre ne lit ni ne modifie jamais les réglages, l'année ou les niveaux d'un autre. |
| Niveaux | La liste dans l'ordre (d'office par rang, puis ajoutés par nom). Ajouter, renommer, retirer. Nom vide, trop long, en double (d'office compris, majuscules comprises). Niveau d'office protégé. Niveau d'un autre centre introuvable. La secrétaire lit la liste mais ne la modifie pas. |
| Recopie | La commande CEN-01 donne 29 niveaux d'office au nouveau centre. La migration les donne à un centre qui existait avant elle (base migrée jusqu'à la migration précédente, centre ajouté, puis dernière migration). |

### Côté Angular (`npm test`, Vitest)

| Sujet | Ce que les tests vérifient |
|---|---|
| Le centre | Le formulaire se remplit avec les réglages. Les montants avec une virgule partent en nombre. Des champs vides partent en `null`. Un montant illisible ne part pas. La note sans année scolaire. Le message enregistré et le refus, sous la carte. Le bouton bloqué pendant l'envoi. |
| Les niveaux | Les niveaux ajoutés et « Aucun niveau ajouté ». Ajouter (nom vide refusé). Renommer, puis annuler ou enregistrer. Retirer avec « Oui » et « Non ». Les niveaux d'office repliés. Un refus du serveur, sous la carte. |
| Adresses | `/parametres` montre la page au propriétaire, et « Accès réservé au propriétaire » à la secrétaire. |

### À la main, dans le navigateur

Avec le vrai serveur et PostgreSQL dans Docker :

1. appliquer la migration : les centres existants reçoivent leurs 29 niveaux ;
2. en propriétaire, remplir puis modifier les réglages, avec l'année scolaire ;
3. ajouter, renommer et retirer un niveau ;
4. tout cela sur une largeur de téléphone (375 pixels) et d'ordinateur, en mode clair et en mode sombre.

## Points d'attention

- **Les montants** : `decimal` en C#, `numeric(12,3)` en PostgreSQL, nombres en JSON. Le front ne calcule rien ; il convertit seulement la virgule.
- **Le téléphone tunisien** a 8 chiffres ; il est enregistré sans espaces ni indicatif, et renvoyé groupé « 71 234 567 ».
- **La migration des niveaux** insère 29 lignes par centre existant, dans la même migration que la création de la table.
- **L'index unique du nom des niveaux** porte sur le centre et le nom en minuscules : deux centres peuvent avoir le même niveau ajouté.
