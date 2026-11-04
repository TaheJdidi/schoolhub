# Les groupes d'Étude

Conception établie le 04/11/2026 pour Lassad et Taha. Elle couvre la deuxième étape de l'épopée [Professeurs et groupes](../../user-stories/03-professeurs-groupes.md) : les groupes d'Étude, après [les professeurs](2026-11-04-professeurs-design.md).

## But

La secrétaire crée un groupe d'Étude avec son horaire et son prix : ses séances de l'année se créent toutes seules, rangées en Mois de 4. Elle retrouve les groupes de chaque professeur sur sa fiche, et ouvre la fiche d'un groupe pour voir ses Mois et changer son nom, sa capacité ou son prix.

## Périmètre

**Dans cette étape :**

- [GRP-04](../../user-stories/03-professeurs-groupes.md#grp-04--créer-un-groupe-détude) : créer un groupe d'Étude, le modifier (nom, capacité, prix) ;
- [GRP-02](../../user-stories/03-professeurs-groupes.md#grp-02--voir-les-groupes-dun-professeur-filtrés-par-jour), la suite : les cadres des groupes ouverts sur la fiche du professeur, et le filtre par jour ;
- ce que les professeurs attendaient de GRP-04 ([section 4](2026-11-04-professeurs-design.md#4-ce-que-grp-04-devra-ajouter-ici)) : compter les groupes ouverts, refuser le départ ou la suppression d'un professeur qui a des groupes, refuser un professeur parti ;
- ce que les niveaux attendaient ([CEN-03](2026-11-03-parametres-du-centre-design.md#2-les-niveaux-cen-03)) : refuser de retirer un niveau utilisé par un groupe.

**Plus tard :**

- Révision, Formation, groupe déjà en cours (GRP-05 à GRP-07) : l'étape suivante ;
- fermer un groupe et « Anciens groupes » (GRP-08) : aujourd'hui, tout groupe est ouvert ;
- annuler, déplacer, ajouter une séance, changer l'horaire (SEA) : les Mois se calculent déjà de façon à les accueillir (section 2) ;
- le nombre d'élèves, le badge de paiement, la prochaine séance sur les cadres : avec les inscriptions (ELV), la caisse (CAI) et les séances (SEA) ;
- changer les dates de l'année scolaire après avoir créé des groupes ne déplace pas leurs séances : CEN-04 (année suivante) traitera l'année.

## Décisions

Lassad et Taha ont demandé de travailler sans questions et de prendre chaque fois la recommandation. Les voici.

| Question | Décision |
|---|---|
| Où vit le code ? | Dans un module `Groupes` (`backend/src/SchoolHub/Modules/Groupes/`), à côté de `Professeurs` : l'[ADR 0003](../../adr/0003-un-seul-deploiement-decoupe-en-modules.md) parle du module « groupes et séances ». |
| Les Mois s'enregistrent-ils ? | **Non : ils se calculent.** Une séance annulée ne compte pas et le Mois s'allonge ([ticket 01, Q17c](../../wayfinder/cadrage-mvp/issues/01-programmes-inscriptions-tarification.md)) : les Mois dépendent donc de la liste des séances qui comptent, rangées par date. La 1ʳᵉ à la 4ᵉ forment le Mois 1, la 5ᵉ à la 8ᵉ le Mois 2, etc. SEA n'aura qu'à retirer une séance annulée du compte. |
| Comment tenir « un nouveau prix vaut pour les Mois qui n'ont pas encore commencé » ? | Le groupe garde une **liste de prix**, chacun « à partir du Mois N ». À la création : 60 TND à partir du Mois 1. Le 01/02, passer à 45 TND enregistre « 45 TND à partir du Mois 6 » si le Mois 5 a déjà commencé. Le prix d'un Mois est celui de la dernière ligne dont le numéro ne le dépasse pas. |
| Quand un Mois a-t-il commencé ? | Quand sa première séance a lieu **aujourd'hui ou avant**, à l'heure de Tunis. Un prix changé le matin d'une séance 1 ne vaut donc pas pour ce Mois. |
| Les séances s'enregistrent-elles ? | Oui, une ligne par séance (date, heure de début, heure de fin) : SEA les annulera, les déplacera et y fera l'appel. Un groupe du samedi en a environ 40 par an. |
| Le nom du groupe ? | Proposé par la page depuis le premier créneau (« Groupe Samedi 10h → 12h ») et modifiable. Envoyé vide, le serveur met le même nom proposé. Deux groupes peuvent avoir le même nom. |
| La matière ? | Une liste par centre (table `matieres`), sans écran à elle : la page propose les matières déjà utilisées, et une matière tapée qui n'y est pas s'ajoute à la création du groupe. « mathématiques » et « Mathématiques » sont la même matière. |
| Quelle année scolaire ? | Celle qui contient la date de début. Sans année scolaire, la création est refusée et renvoie aux paramètres. Le groupe garde son année (`AnneeScolaireId`) pour CEN-04. |
| Le prix du Mois peut-il valoir 0 ? | Non : un groupe d'Étude se paie. Un élève gratuit aura un prix spécial (ELV). |
| Deux créneaux du même groupe qui se chevauchent ? | Refusés. Un professeur dans deux groupes à la même heure n'est pas vérifié : aucune story ne le demande. |
| Le filtre par jour des cadres ? | Il suit les créneaux du groupe (son horaire de la semaine). Les séances déplacées viendront avec SEA. |
| Où s'ouvrent la création et la fiche d'un groupe ? | Sous la page Professeurs : `/professeurs/groupes/nouveau?professeur=…` et `/professeurs/groupes/{id}`. Le menu garde « Professeurs » allumé, comme dans la maquette. |
| Qui crée les groupes ? | Toute personne connectée du centre : la secrétaire et le propriétaire. |
| Le journal ? | Ni la création ni la modification d'un groupe n'y vont : la liste des [exigences communes](../../user-stories/00-exigences-communes.md#journal) cite seulement la fermeture (GRP-08). |
| Et si le serveur ne connaît pas le fuseau « Africa/Tunis » ? | Il essaie le nom Windows (« W. Central Africa Standard Time »), puis prend UTC+1 : la Tunisie n'a plus d'heure d'été depuis 2009. Une image Linux réduite ou un ancien Windows ne mettent donc pas les fiches de groupe en erreur. |
| Les heures dans le JSON ? | Toujours « 10:00 », quelle que soit la langue du serveur (culture invariante). |
| Le nombre de groupes de chaque professeur ? | Chaque réponse qui renvoie un professeur (liste, ajout, modification, départ, retour) porte son vrai nombre : la page remplace la ligne par le professeur renvoyé. |
| Où vont les cadres sur la page Professeurs ? | Dans une carte à part, « Ses groupes », sous la fiche du professeur et ses messages ; la fiche garde la ligne « 2 groupes ouverts ». |
| Une capacité illisible (« 16 places ») ? | La page la refuse : « Écrivez la capacité en nombre de places, par exemple 16 ». |
| Le jour du premier créneau ? | Samedi d'office (le jour le plus chargé de la maquette) ; un créneau ajouté reprend le jour du précédent. |

## 1. Les données

**La matière** (`Matiere`, table `matieres`, `IAppartientAuCentre`) : `Nom` (100 caractères au plus) et `NomNormalise` (forme NFKC en minuscules, comme les niveaux), avec un index unique sur le centre et le nom normalisé.

**Le groupe** (`Groupe`, table `groupes`, `IAppartientAuCentre`) :

| Champ | Contenu |
|---|---|
| `Type` | `Etude` (Révision et Formation viendront), enregistré en texte |
| `Nom` | 100 caractères au plus |
| `ProfesseurId`, `MatiereId`, `NiveauId`, `AnneeScolaireId` | clés étrangères `Restrict` ; `NiveauId` peut être vide (une Formation n'en aura pas toujours), mais l'Étude l'exige |
| `Capacite` | facultative, 1 ou plus |
| `DateDeDebut` | `date` |
| `Creneaux` | une ou plusieurs lignes `(Jour, Debut, Fin)` (table `creneaux_des_groupes`, partie du groupe) |
| `Prix` | une ou plusieurs lignes `(APartirDuMois, Montant)` (table `prix_des_groupes`, partie du groupe), montant en `numeric(12,3)` |

`Jour` est `Lundi` … `Dimanche`, enregistré en texte. Les heures sont des `time`.

**La séance** (`Seance`, table `seances`, `IAppartientAuCentre`) : `GroupeId`, `Date`, `Debut`, `Fin`. Un index sur le groupe et la date.

## 2. Créer un groupe d'Étude

### La création

1. Les champs sont vérifiés dans l'ordre du tableau ci-dessous ; le premier refusé donne la réponse.
2. La matière est cherchée par son nom normalisé ; si elle n'existe pas, elle est ajoutée au centre, dans le même enregistrement que le groupe.
3. Les séances sont créées de la date de début à la fin de l'année scolaire, comprises : pour chaque jour de cette période et chaque créneau de ce jour de la semaine, une séance. Exemple de la story : le samedi de 10h à 12h, à partir du samedi 18/09/2027, avec une année qui finit le 30/06/2028, donne une séance chaque samedi jusqu'au 24/06/2028.
4. Le prix du Mois devient « à partir du Mois 1 ».

### Les règles de saisie

| Champ | Règle | Refus |
|---|---|---|
| Professeur | obligatoire ; du centre ; au centre (pas parti) | 400 « Choisissez le professeur » ; 404 « Professeur introuvable » ; 409 « Ce professeur a quitté le centre » |
| Matière | obligatoire, 100 caractères au plus | 400 « La matière est obligatoire » ; « La matière fait au plus 100 caractères » |
| Niveau | obligatoire ; du centre | 400 « Choisissez le niveau » ; 404 « Niveau introuvable » |
| Créneaux | au moins un ; chaque jour connu ; heures « HH:mm » ; la fin après le début ; pas deux créneaux du même jour qui se chevauchent | 400 « Ajoutez au moins un créneau » ; « Un créneau doit avoir un jour, une heure de début et une heure de fin » ; « Un créneau doit finir après son début » ; « Deux créneaux se chevauchent » |
| Prix du Mois | obligatoire ; plus que 0, 3 décimales au plus, moins d'un million | 400 « Le prix du Mois doit être positif, au millime près » |
| Date de début | obligatoire ; une année scolaire réglée ; dans une année scolaire | 400 « La date de début est obligatoire » ; 409 « Réglez d'abord l'année scolaire dans les paramètres du centre » ; 400 « La date de début doit être dans l'année scolaire, du 15/09/2027 au 30/06/2028 » (les dates de l'année la plus récente) |
| Capacité | facultative ; 1 ou plus | 400 « La capacité doit être d'au moins 1 place » |
| Nom | facultatif (vide : le nom proposé) ; 100 caractères au plus | 400 « Le nom du groupe fait au plus 100 caractères » |
| (après) | au moins une séance entre la date de début et la fin de l'année | 400 « Aucune séance entre la date de début et la fin de l'année scolaire » |

**Le nom proposé** : « Groupe » + le jour du premier créneau + ses heures, écrites « 10h », « 10h30 », « 8h » : « Groupe Samedi 10h → 12h ». Le premier créneau est celui que la secrétaire a mis en premier.

### Les Mois et leur prix

- Les séances du groupe, rangées par date puis par heure de début, sont numérotées 1, 2, 3… Le Mois de la séance n est `(n - 1) / 4 + 1` (division entière). Le dernier Mois de l'année peut en avoir moins de 4 (il se paiera au prorata, ELV et CAI).
- Le prix d'un Mois est le montant de la ligne de prix au plus grand `APartirDuMois` qui ne dépasse pas son numéro.
- Un Mois **a commencé** si sa première séance a lieu aujourd'hui ou avant (date de Tunis, `Africa/Tunis`).

## 3. Modifier un groupe

`PUT` du nom, de la capacité et du prix du Mois, ensemble, avec les mêmes règles qu'à la création (un nom vide reprend le nom proposé).

**Le nouveau prix** : soit N le numéro du premier Mois qui n'a pas commencé (ou le dernier Mois + 1 si tous ont commencé). Les lignes de prix à partir de N ou au-delà sont remplacées par une seule : « Montant à partir du Mois N ». Si le prix ne change pas, rien n'est ajouté. Exemple de la story : le prix passe de 40 à 45 TND le 01/02 ; le Mois 5 a commencé le 15/01 et reste à 40 TND ; le Mois 6 et les suivants passent à 45 TND.

## 4. Les adresses

Toute personne connectée du centre. Un groupe d'un autre centre est « Groupe introuvable » (404).

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/matieres` | les matières du centre, par nom | 200, `[{ "id", "nom" }]` |
| `GET /api/groupes` | tous les groupes du centre, pour les cadres | 200, les résumés |
| `POST /api/groupes` | créer un groupe d'Étude | 201, la fiche du groupe |
| `GET /api/groupes/{id}` | la fiche du groupe, avec ses Mois et ses séances | 200 |
| `PUT /api/groupes/{id}` | changer le nom, la capacité, le prix | 200, la fiche du groupe |

**Ce que la page envoie pour créer** :

```json
{
  "professeurId": "…", "matiere": "Mathématiques", "niveauId": "…",
  "creneaux": [{ "jour": "Samedi", "debut": "10:00", "fin": "12:00" }],
  "prixDuMois": 60.000, "dateDeDebut": "2027-09-18", "capacite": 16, "nom": "Groupe Samedi 10h → 12h"
}
```

**Pour modifier** : `{ "nom", "capacite", "prixDuMois" }`.

**Le résumé d'un groupe** (cadres) : `{ "id", "nom", "type": "Etude", "professeurId", "matiere", "niveau", "creneaux": [...], "capacite", "eleves": 0 }`. `niveau` est le nom du niveau ; `eleves` vaut 0 jusqu'aux inscriptions (ELV).

**La fiche d'un groupe** :

```json
{
  "id": "…", "nom": "Groupe Samedi 10h → 12h", "type": "Etude",
  "professeur": { "id": "…", "prenom": "Karim", "nom": "Ben Salah" },
  "matiere": "Mathématiques", "niveau": { "id": "…", "nom": "9ᵉ année de base" },
  "creneaux": [{ "jour": "Samedi", "debut": "10:00", "fin": "12:00" }],
  "capacite": 16, "eleves": 0, "dateDeDebut": "2027-09-18",
  "prixDuMois": 60.000,
  "mois": [
    { "numero": 1, "prix": 60.000, "commence": false,
      "seances": [{ "id": "…", "date": "2027-09-18", "debut": "10:00", "fin": "12:00" }, …] }
  ]
}
```

`prixDuMois` est le prix du premier Mois qui n'a pas commencé : c'est celui que le formulaire de modification propose.

## 5. Ce qui change chez les professeurs et les niveaux

- `groupesOuverts` compte les groupes du professeur (tous sont ouverts jusqu'à GRP-08).
- « Marquer parti » un professeur qui a un groupe ouvert : 409 « Fermez d'abord ses groupes ».
- Supprimer un professeur qui a eu un groupe : 409 « Ce professeur a eu des groupes : marquez-le parti ».
- Créer un groupe avec un professeur parti : 409 « Ce professeur a quitté le centre » (section 2).
- Retirer un niveau ajouté qu'un groupe utilise : 409 « Ce niveau est utilisé par un groupe ou un élève » (ELV-02 ajoutera les élèves).

## 6. Les pages

### La fiche du professeur (page Professeurs)

« Ses groupes » montre ses groupes ouverts dans des cadres, comme la maquette :

```
Ses groupes                                        [ + Nouveau groupe ]
[ Tous ] [ Lun ] [ Mar ] … [ Sam ] [ Dim ]
┌───────────────────────────┐ ┌───────────────────────────┐
│ Étude                     │ │ Étude                     │
│ Groupe Samedi 10h → 12h   │ │ Groupe Dimanche 8h → 10h  │
│ Mathématiques · 9ᵉ année… │ │ Mathématiques · 4ᵉ année… │
│ 0 élève sur 16            │ │ 0 élève                   │
└───────────────────────────┘ └───────────────────────────┘
```

- Chaque cadre est un lien vers la fiche du groupe. Sans capacité : « 0 élève » ; avec : « 0 élève sur 16 ».
- Les jours sans groupe sont grisés ; « Sam » ne montre que les groupes qui ont un créneau le samedi : « Pas de groupe le samedi pour Karim Ben Salah. » sinon.
- Sans groupe : « Aucun groupe ouvert ».
- « Nouveau groupe » ouvre la création avec ce professeur ; il n'existe pas pour un professeur parti.
- Les groupes se chargent en même temps que les professeurs ; un échec bloque « Nouveau groupe » et s'affiche sous la liste.

### La création d'un groupe (`/professeurs/groupes/nouveau`)

```
Nouveau groupe d'Étude
┌────────────────────────────────────────────────────────────┐
│ Professeur [Karim Ben Salah ▾]  Matière [Mathématiques   ] │
│ Niveau [9ᵉ année de base ▾]                                 │
│ Créneaux                                                    │
│   [Samedi ▾] de [10:00] à [12:00]               [Retirer]  │
│   [ + Ajouter un créneau ]                                  │
│ Prix du Mois [60,000] TND   Date de début [18/09/2027]      │
│ Capacité [16] (facultatif)                                  │
│ Nom [Groupe Samedi 10h → 12h]                               │
│                                   [Annuler] [Créer le groupe]│
└────────────────────────────────────────────────────────────┘
```

- La page charge les professeurs (au centre seulement), les niveaux et les matières. Si l'un échoue, le refus s'affiche et « Créer le groupe » reste bloqué.
- Le professeur de l'adresse est choisi d'office.
- La matière se tape, avec les matières du centre proposées (`datalist`).
- Un créneau au départ ; « Ajouter un créneau » en ajoute un ; « Retirer » n'existe que s'il y en a plusieurs.
- Le nom suit le premier créneau tant que la secrétaire ne l'a pas changé elle-même.
- Le prix s'écrit avec une virgule ou un point (`lireUnNombre`) ; une saisie illisible ne part pas : « Écrivez le prix du Mois, par exemple 60,000 ».
- Un champ obligatoire vide ne part pas : « Remplissez le professeur, la matière, le niveau, les créneaux, le prix et la date de début ».
- Le bouton se bloque pendant l'envoi ; le refus s'affiche sous le formulaire. Réussi, la page ouvre la fiche du nouveau groupe.
- « Annuler » revient à la page Professeurs.

### La fiche du groupe (`/professeurs/groupes/{id}`)

```
Professeurs › Karim Ben Salah
Groupe Samedi 10h → 12h
Mathématiques · 9ᵉ année de base · Karim Ben Salah · Étude, 60,000 TND par Mois

┌─ Le groupe ──────────────────────────────────────────────┐
│ Samedi de 10h à 12h · depuis le 18/09/2027 · 0 élève sur 16 │
│                                              [ Modifier ] │
└──────────────────────────────────────────────────────────┘
┌─ Les Mois ────────────────────────────────────────────────┐
│ Mois 1 · 60,000 TND                                        │
│   sam. 18/09  sam. 25/09  sam. 02/10  sam. 09/10           │
│ Mois 2 · 60,000 TND …                                      │
└──────────────────────────────────────────────────────────┘
```

- Le fil « Professeurs › Karim Ben Salah » ramène à la page Professeurs.
- « Modifier » ouvre dans la carte le nom, la capacité et le prix du Mois, avec la note : « Le nouveau prix vaut pour les Mois qui n'ont pas encore commencé. » Le message « Groupe enregistré » ou le refus s'affiche sous la carte.
- Un Mois commencé porte la pastille « Commencé ».
- Groupe introuvable : « Ce groupe n'existe pas. » et un lien vers la page Professeurs.

Sur un téléphone, tout passe en une colonne, sans défilement de côté.

## 7. L'organisation du code

```
backend/src/SchoolHub/Socle/Dates/DateDeTunis.cs     la date du jour à Tunis
backend/src/SchoolHub/Modules/Groupes/
├── Matiere.cs                     la matière et sa configuration
├── Groupe.cs                      le groupe, ses créneaux, ses prix, sa configuration
├── Seance.cs                      la séance et sa configuration
├── CalendrierDuGroupe.cs          les séances depuis les créneaux, les Mois, le nom proposé, le prix d'un Mois
├── GroupesDuCentre.cs             créer, lire, modifier ; les règles de saisie
└── GroupesEndpoints.cs            les adresses des groupes et des matières, et les formes JSON

frontend/src/app/pages/professeurs/   + les cadres des groupes et le filtre par jour
frontend/src/app/pages/groupes/       groupe.ts (formes de l'API), la création (nouveau-groupe) et la fiche (page-du-groupe)
```

## 8. Les tests

### Côté .NET

| Sujet | Ce que les tests vérifient |
|---|---|
| Calendrier | L'exemple de la story (samedis du 18/09/2027 au 24/06/2028) ; deux créneaux par semaine rangés par date et heure ; un Mois de deux semaines avec deux créneaux ; le dernier Mois incomplet ; le nom proposé (« 10h », « 10h30 »). |
| Création | Le groupe complet ; la matière nouvelle ajoutée, la matière existante réutilisée malgré les majuscules ; chaque refus du tableau ; sans année scolaire ; le professeur parti ; aucune séance. |
| Prix | L'exemple de la story (40 → 45 le 01/02 : Mois 5 à 40, Mois 6 à 45), avec l'horloge des tests ; un changement avant tout Mois commencé remplace le prix du Mois 1 ; un Mois dont la séance 1 est aujourd'hui a commencé. |
| Professeurs et niveaux | Le compte des groupes ; départ et suppression refusés ; niveau utilisé non retiré. |
| Droits et centres | Secrétaire autorisée, personne non connectée refusée ; groupe, professeur, niveau d'un autre centre introuvables ; les matières et groupes d'un autre centre invisibles. |

### Côté Angular

| Sujet | Ce que les tests vérifient |
|---|---|
| Cadres | Les groupes du professeur choisi, « 0 élève sur 16 », le filtre par jour, « Aucun groupe ouvert », « Nouveau groupe » absent pour un parti, le lien vers la fiche. |
| Création | Le professeur d'office, le nom qui suit le premier créneau puis plus après une saisie, ajouter et retirer un créneau, le prix à virgule, les refus de la page, l'envoi exact, l'ouverture de la fiche, le refus du serveur, le chargement raté. |
| Fiche | L'en-tête, les Mois et leurs séances, la pastille « Commencé », modifier (nom, capacité, prix), le refus sous la carte, groupe introuvable. |
| Adresses | `/professeurs/groupes/nouveau` et `/professeurs/groupes/{id}` s'ouvrent à la secrétaire, avec « Professeurs » allumé dans le menu. |

## Points d'attention

- **Les Mois ne s'enregistrent nulle part** : toute story qui en parle (inscriptions, caisse, parts) les calcule avec `CalendrierDuGroupe`.
- **La date de Tunis** vient de `TimeProvider`, que les tests règlent.
- **Les heures** circulent en texte « HH:mm » dans le JSON, et en `time` dans PostgreSQL.
