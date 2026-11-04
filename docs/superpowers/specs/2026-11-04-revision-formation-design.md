# Révision, Formation et groupe déjà en cours

Conception établie le 04/11/2026 pour Lassad et Taha. Elle couvre la troisième étape de l'épopée [Professeurs et groupes](../../user-stories/03-professeurs-groupes.md), après [les groupes d'Étude](2026-11-04-groupes-etude-design.md).

## But

La secrétaire crée aussi les deux autres types de groupe : la Révision (quelques séances, un prix total divisé par séance) et la Formation (un prix total, avec des tranches facultatives). Un centre qui commence SchoolHub en cours d'année crée ses groupes là où ils en sont, sans recopier le passé. Une Révision ou une Formation finie devient un groupe terminé, rangé dans « Anciens groupes ».

## Périmètre

**Dans cette étape :**

- [GRP-05](../../user-stories/03-professeurs-groupes.md#grp-05--créer-un-groupe-de-révision) : créer une Révision, avec des créneaux ou des dates une à une ; le prix de chaque séance ;
- [GRP-06](../../user-stories/03-professeurs-groupes.md#grp-06--créer-un-groupe-de-formation-avec-ses-tranches) : créer une Formation, avec ses tranches ;
- [GRP-07](../../user-stories/03-professeurs-groupes.md#grp-07--créer-un-groupe-déjà-en-cours) : « Ce groupe a déjà commencé », pour les trois types ;
- [GRP-02](../../user-stories/03-professeurs-groupes.md#grp-02--voir-les-groupes-dun-professeur-filtrés-par-jour), la suite : « Anciens groupes » pour les groupes terminés.

**Plus tard :**

- ce qu'un élève a payé avant SchoolHub (ELV-07), et les parts du professeur (PRT) ;
- fermer un groupe (GRP-08) : « Anciens groupes » montrera aussi les groupes fermés ;
- payer à la séance, en totalité ou par tranche (CAI) : cette étape fixe seulement les prix et les échéances sur le groupe.

## Décisions

Lassad et Taha ont demandé de travailler sans questions et de prendre chaque fois la recommandation. Les voici.

| Question | Décision |
|---|---|
| Comment numéroter les séances d'un groupe déjà en cours ? | Le groupe garde `PremiereSeance`, le numéro de sa première séance dans SchoolHub (1 d'office). Les séances sont numérotées à partir de lui, dans l'ordre des dates : « séance 4 sur 10 » donne les séances 4 à 10. Pour une Étude, « Mois 5 » donne `PremiereSeance` = 17 (la séance 1 du Mois 5), et les Mois se comptent à partir du Mois 5. Une seule règle pour les trois types. |
| Le prix d'une séance de Révision ? | Calculé, jamais enregistré : le prix total divisé par le nombre de séances, arrondi au millime, et la dernière séance prend la différence ([exigences communes](../../user-stories/00-exigences-communes.md)) : 50 TND en 3 séances donnent 16,667 + 16,667 + 16,666. Le calcul porte toujours sur toutes les séances, même pour un groupe déjà en cours. |
| Comment une Révision ou une Formation devient-elle « terminée » ? | Elle se calcule : le lendemain de sa dernière séance (date de Tunis). Rien n'est enregistré. Une Étude n'est jamais terminée avant le passage à l'année suivante (CEN-04). |
| Quels groupes comptent comme « ouverts » ? | Ceux qui ne sont pas terminés : un professeur dont les seules Formations sont finies peut partir. Supprimer reste refusé dès qu'il a eu un groupe. |
| Révision et Formation sont-elles liées à l'année scolaire ? | Non : une Formation peut se faire l'été. Elles gardent l'année qui contient leur première séance, s'il y en a une, et rien n'est refusé sans année. L'Étude l'exige toujours. |
| Les dates une à une : combien ? | Autant que de séances à venir : 3 pour une Révision de 3 séances, 7 pour « séance 4 sur 10 ». Le formulaire ajoute une ligne par date. |
| Les tranches d'une Formation ? | Facultatives. Chacune a un montant et la séance où elle est due (de 1 au nombre de séances), deux tranches ne tombent pas sur la même séance, et leur somme fait le prix total ; sinon, le refus montre l'écart. |
| Que peut-on modifier ensuite ? | Pour les trois types : le nom et la capacité. Le prix d'une Révision ou d'une Formation ne change pas : la story les veut « au prix et au nombre de séances fixés ». Le prix du Mois d'une Étude se change comme avant. |
| Le nom proposé d'une Révision ou d'une Formation ? | La même règle que l'Étude : depuis le premier créneau, ou depuis la première date (« Groupe Samedi 10h → 12h »). La secrétaire le change si elle veut. |
| Le niveau ? | Obligatoire pour une Étude et une Révision ; facultatif pour une Formation. |
| Une création sans type ? | Elle reste une Étude, comme à l'étape 2 : l'API ne change pas pour l'Étude. |
| « Anciens groupes » sur la fiche du professeur ? | Une partie repliée sous ses cadres, « Anciens groupes (2) », avec les mêmes cadres et la pastille « Terminé ». Le filtre par jour ne vaut que pour les groupes ouverts. |

## 1. Les données

Le groupe (`Groupe`) gagne :

| Champ | Contenu |
|---|---|
| `Type` | `Etude`, `Revision` ou `Formation` |
| `PrixTotal` | Révision et Formation : dinars au millime (`numeric(12,3)`) ; vide pour une Étude |
| `NombreDeSeances` | Révision et Formation : de 1 à 200 ; vide pour une Étude |
| `PremiereSeance` | le numéro de la première séance dans SchoolHub ; 1 d'office |
| `Tranches` | Formation : lignes `(Montant, Seance)` (table `tranches_des_groupes`, partie du groupe) |
| `AnneeScolaireId` | devient facultatif (Révision, Formation) |

La séance ne change pas : son numéro se calcule (`PremiereSeance` + son rang dans l'ordre des dates).

## 2. Créer une Révision ou une Formation

`POST /api/groupes` reçoit, en plus des champs de l'Étude :

```json
{
  "type": "Revision",
  "prixTotal": 50.000,
  "nombreDeSeances": 3,
  "dates": [{ "date": "2028-05-20", "debut": "10:00", "fin": "12:00" }],
  "tranches": [{ "montant": 100.000, "seance": 1 }],
  "moisEnCours": 5,
  "seanceEnCours": 4
}
```

- **Les dates viennent soit des créneaux** (`creneaux` et `dateDeDebut`, comme l'Étude) : une séance à chaque créneau à partir de la date de début, jusqu'au nombre de séances à venir ; **soit de `dates`**, si la liste n'est pas vide.
- `tranches` ne compte que pour une Formation ; `moisEnCours` que pour une Étude ; `seanceEnCours` que pour une Révision ou une Formation.

### Les règles de saisie

Dans l'ordre du formulaire ; le premier refusé donne la réponse. Les règles de l'étape 2 (professeur, matière, créneaux, capacité, nom) restent.

| Champ | Règle | Refus (400 sauf mention) |
|---|---|---|
| Type | `Etude`, `Revision` ou `Formation` ; vide : Étude | « Le type du groupe est inconnu » |
| Niveau | obligatoire pour Étude et Révision ; s'il est donné, du centre | « Choisissez le niveau » ; 404 « Niveau introuvable » |
| Prix total (R, F) | plus que 0, au millime, moins d'un million | « Le prix total doit être positif, au millime près » |
| Nombre de séances (R, F) | de 1 à 200 | « Le nombre de séances va de 1 à 200 » |
| Séance en cours (R, F, facultative) | de 1 au nombre de séances | « La séance en cours va de 1 à 10 » |
| Mois en cours (Étude, facultatif) | de 1 à 20 | « Le Mois en cours va de 1 à 20 » |
| Dates une à une | chaque date avec ses deux heures ; la fin après le début ; pas deux séances qui se chevauchent le même jour ; autant de dates que de séances à venir | « Une séance doit avoir une date, une heure de début et une heure de fin » ; « Une séance doit finir après son début » ; « Deux séances se chevauchent » ; « Il faut une date pour chacune des 3 séances » |
| Créneaux (R, F) | les règles de l'Étude, et la date de début obligatoire | celles de l'Étude ; « La date de début est obligatoire » |
| Tranches (F) | montant positif au millime ; séance de 1 au nombre de séances ; une tranche par séance au plus ; somme égale au prix total | « Une tranche doit avoir un montant positif, au millime près » ; « Une tranche est due à une séance de 1 à 10 » ; « Deux tranches tombent sur la même séance » ; « La somme des tranches fait 250,000 TND au lieu de 300,000 TND : il manque 50,000 TND » (ou « il y a 50,000 TND de trop ») |

« Séances à venir » : le nombre de séances, moins celles d'avant la séance en cours (`nombreDeSeances - seanceEnCours + 1`).

## 3. Un groupe déjà en cours (GRP-07)

- **Étude** : « Mois 5, à partir du samedi 15/01/2028 » s'envoie `moisEnCours: 5` et `dateDeDebut: "2028-01-15"`. Les séances vont du 15/01/2028 à la fin de l'année scolaire ; la première est la séance 1 du Mois 5 ; il n'y a ni Mois 1 à 4, ni séances avant. Le prix du Mois vaut « à partir du Mois 5 ».
- **Révision ou Formation** : « séance 4 sur 10, le 15/01/2028 » s'envoie `nombreDeSeances: 10`, `seanceEnCours: 4` et la date (date de début, ou première des dates une à une). Les séances 4 à 10 sont créées. Les prix des séances et les tranches gardent la numérotation complète : une tranche due à la séance 1 a été payée avant SchoolHub (ELV-07 la notera).

## 4. Ce qui se lit

**La fiche d'un groupe** gagne :

```json
{
  "type": "Revision", "termine": false, "premiereSeance": 1,
  "prixTotal": 50.000, "nombreDeSeances": 3, "prixDuMois": null,
  "mois": [],
  "seances": [{ "id": "…", "numero": 1, "date": "2028-05-20", "debut": "10:00", "fin": "12:00", "prix": 16.667 }],
  "tranches": [{ "montant": 100.000, "seance": 1 }]
}
```

- Étude : `mois` comme avant (numérotés à partir du Mois en cours), `seances` vide, `prixTotal`, `nombreDeSeances` vides.
- Révision : `seances` numérotées avec leur prix ; `mois` vide.
- Formation : `seances` numérotées, `prix` vide ; `tranches` rangées par séance.
- `prixDuMois` est vide pour une Révision et une Formation.

**Le résumé** (cadres) gagne `termine`.

**Les professeurs** : `groupesOuverts` ne compte plus les groupes terminés ; « Marquer parti » n'est refusé que par un groupe ouvert.

## 5. Les pages

### La création (`/professeurs/groupes/nouveau`)

```
Nouveau groupe
[ Étude ] [ Révision ] [ Formation ]
Professeur · Matière · Niveau (facultatif pour une Formation)
— Étude —                                     — Révision, Formation —
Créneaux, Prix du Mois, Date de début          Prix total [50,000] TND   Nombre de séances [3]
                                               Les dates : (•) Créneaux par semaine  ( ) Dates une à une
                                               … créneaux et date de début, ou :
                                               [18/05/2028] de [10:00] à [12:00]  [Retirer]
                                               [ + Ajouter une date ]
                                               — Formation : Tranches (facultatif) —
                                               [100,000] TND due à la séance [1]  [Retirer]
                                               [ + Ajouter une tranche ]
[ ] Ce groupe a déjà commencé
     Étude : Mois en cours [5]  (la date de début devient « Première séance dans SchoolHub »)
     R, F  : Séance en cours [4] sur 10 (la date de début devient « Date de cette séance »)
Capacité · Nom                                                     [Annuler] [Créer le groupe]
```

- Le type se choisit en haut (Étude d'office) ; changer de type garde ce qui est commun.
- Les montants s'écrivent avec une virgule ; un montant illisible ne part pas (« Écrivez le prix total, par exemple 50,000 », « Écrivez le montant de chaque tranche, par exemple 100,000 ») ; un nombre de séances ou une séance illisible non plus (« Écrivez le nombre de séances, par exemple 10 »).
- Un champ obligatoire vide ne part pas. Étude : le message de l'étape 2. Révision : « Remplissez le professeur, la matière, le niveau, le prix total, le nombre de séances et les dates ». Formation : « Remplissez le professeur, la matière, le prix total, le nombre de séances et les dates ». « Déjà commencé » coché sans Mois ou sans séance en cours : « Indiquez où en est le groupe ».
- Le nom suit le premier créneau, ou la première date une à une, tant que la secrétaire ne l'a pas écrit.

### La fiche du groupe

- Le sous-titre selon le type : « Étude, 60,000 TND par Mois » ; « Révision, 50,000 TND les 3 séances, ou 16,667 TND la séance » ; « Formation, 300,000 TND, en une fois ou en 3 tranches » (« en une fois » sans tranche).
- Une pastille « Terminé » à côté du titre pour un groupe terminé.
- Étude : la carte « Les Mois », à partir du Mois en cours. Révision et Formation : la carte « Les séances » (« Séance 4 · sam. 15/01 · 10h → 12h », et « 16,667 TND » pour une Révision) ; Formation : la carte « Les tranches » (« Tranche 1 · 100,000 TND, due à la séance 1 »).
- « Modifier » : le nom et la capacité ; le prix du Mois pour une Étude seulement.

### La fiche du professeur

- Les cadres montrent le type (« Révision », « Formation »).
- Les groupes terminés vont dans « Anciens groupes (2) », replié sous les cadres, avec la pastille « Terminé ».

## 6. L'organisation du code

```
backend/src/SchoolHub/Modules/Groupes/
├── Groupe.cs                + Type (3 valeurs), PrixTotal, NombreDeSeances, PremiereSeance, Tranches
├── CalendrierDuGroupe.cs    + séances en nombre, prix des séances, Mois à partir du Mois en cours, terminé
├── GroupesDuCentre.cs       + règles de Révision, de Formation et de groupe déjà en cours
└── GroupesEndpoints.cs      + formes JSON (dates, tranches, séances numérotées)
backend/src/SchoolHub/Modules/Professeurs/ProfesseursDuCentre.cs   groupes ouverts = non terminés

frontend/src/app/pages/groupes/       création à trois types, fiche selon le type
frontend/src/app/pages/professeurs/   types des cadres, « Anciens groupes »
```

## 7. Les tests

### Côté .NET

| Sujet | Ce que les tests vérifient |
|---|---|
| Calendrier | Prix des séances (50 en 3 → 16,667 · 16,667 · 16,666 ; 100 en 4 → 25 × 4) ; séances en nombre depuis des créneaux ; Mois à partir du Mois 5 ; terminé le lendemain de la dernière séance. |
| Révision | Créneaux ou dates une à une ; prix des séances dans la fiche ; chaque refus ; niveau obligatoire. |
| Formation | L'exemple 300 TND, 10 séances, tranches 100 aux séances 1, 5, 9 ; sans tranche ; somme fausse (manque, trop) ; séance hors limite ; deux tranches sur la même séance ; niveau facultatif ; sans année scolaire. |
| Déjà en cours | Étude « Mois 5, à partir du 15/01/2028 » (premier Mois 5, séances depuis le 15/01) ; Révision « séance 4 sur 10 » (séances 4 à 10, prix sur 10) ; bornes. |
| Terminé | Résumé et fiche ; un professeur avec seulement des groupes terminés part, et son compte vaut 0. |
| Modifier | Révision : nom et capacité, prix ignoré. |

### Côté Angular

| Sujet | Ce que les tests vérifient |
|---|---|
| Création | Le choix du type et ses champs ; dates une à une (ajouter, retirer, nom proposé) ; tranches ; « déjà commencé » pour chaque type ; l'envoi exact de chaque type ; les montants illisibles. |
| Fiche | Sous-titres des trois types ; séances numérotées et leur prix ; tranches ; « Terminé » ; pas de prix dans « Modifier » pour une Révision. |
| Professeurs | Type des cadres ; « Anciens groupes » ; le filtre par jour sur les seuls groupes ouverts. |

## Points d'attention

- **La numérotation des séances se calcule** (`PremiereSeance` + rang) ; SEA retirera du compte les séances annulées, comme pour les Mois.
- **Le prix d'une séance de Révision et les tranches** sont ce que la caisse (CAI) demandera ; la caisse recopiera les montants au moment où ils deviennent dus.
