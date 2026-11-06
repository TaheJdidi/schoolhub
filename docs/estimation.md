# Estimation du MVP

> **Mise de côté le 01/11/2026** ([ticket 14](wayfinder/cadrage-mvp/issues/14-estimation-plan-livraison.md), tour 2) : Taha et Lassad n'ont pas d'heures de travail fixes, et ils codent avec Claude. Ces chiffres restent un ordre de grandeur de l'effort, sans date ni engagement.

Proposée le 01/11/2026 pour le ticket 14. Elle couvre les 78 [user stories](user-stories/README.md) et les [travaux techniques](user-stories/travaux-techniques.md).

## Méthode

- **Une estimation par story, en jours de travail, avec une fourchette** : « bas » si tout se passe bien, « haut » si la story réserve des surprises.
- **Un jour de travail = 7 heures de développement effectif**, par une personne qui connaît déjà .NET et Angular.
- **Une story est terminée** quand ses critères passent, que ses calculs d'argent ont des tests automatiques, qu'elle marche sur téléphone et qu'elle est en production.
- **20 % de marge** s'ajoutent au total, pour les corrections, les imprévus et les essais d'ensemble.
- **Le gain d'un assistant de code** (Claude Code) n'est pas compté : on le mesurera sur les premières épopées.

## Résumé

| # | Épopée | Stories | Bas (jours) | Haut (jours) |
|---|---|---|---|---|
| — | Travaux techniques | — | 19 | 30,5 |
| 01 | Comptes et accès | 6 | 5,5 | 9,5 |
| 02 | Centre | 4 | 4 | 7,5 |
| 03 | Professeurs et groupes | 8 | 11 | 19,5 |
| 04 | Élèves et inscriptions | 11 | 14,5 | 24 |
| 05 | Demandes d'inscription | 5 | 5,5 | 8,5 |
| 06 | Séances et appel | 12 | 14 | 22,5 |
| 07 | Caisse et reçus | 10 | 15,5 | 25 |
| 08 | Bibliothèque | 3 | 3 | 4,5 |
| 09 | Parts des professeurs | 4 | 4,5 | 7 |
| 10 | RH et achats | 6 | 5 | 8 |
| 11 | Tableau de bord et journal | 4 | 5 | 8,5 |
| 12 | Listes | 5 | 6,5 | 12 |
| | **Sous-total** | **78** | **113** | **187** |
| | **Avec 20 % de marge** | | **≈ 136** | **≈ 225** |

Soit **environ 950 à 1 570 heures de travail** pour l'équipe.

## Rythme et date de fin

La date de fin dépend des heures que Taha et Lassad y mettent **ensemble** chaque semaine, à partir du 02/11/2026. Les semaines de vacances, de Ramadan ou d'examens, sans travail, repoussent ces dates d'autant.

| Heures par semaine, à deux | Fin, estimation basse | Fin, estimation haute |
|---|---|---|
| 20 h (10 h chacun) | fin septembre 2027 | mai 2028 |
| 30 h (15 h chacun) | mi-juin 2027 | début novembre 2027 |
| 40 h (20 h chacun) | mi-avril 2027 | début août 2027 |
| 60 h (30 h chacun) | fin février 2027 | début mai 2027 |

**Pour être prêts le 01/06/2027**, il faut en moyenne **entre 31 et 52 heures par semaine à deux**, soit environ 16 à 26 heures chacun, pendant 30 semaines.

## Détail par story

### Travaux techniques

| Travail | Bas | Haut |
|---|---|---|
| Socle : projet .NET en modules, Angular servi par .NET, PWA, menu et mise en page de la maquette | 5 | 8 |
| PostgreSQL, EF Core, séparation des centres et tests d'isolation | 2 | 4 |
| Journal : écriture explicite et stockage | 1 | 2 |
| Tâche quotidienne (groupes terminés, caisses oubliées) | 1 | 2 |
| Gabarits PDF (QuestPDF) | 1 | 2 |
| Excel (ClosedXML) | 1 | 1 |
| Envoi : liens `wa.me`, emails Brevo, SPF et DKIM | 2 | 3 |
| Stockage des photos de factures | 1 | 1 |
| Azure : App Service, PostgreSQL, stockage, nom de domaine | 2 | 3 |
| Pipeline GitHub Actions et retour à la version précédente | 1 | 2 |
| Sauvegardes et essai de restauration | 0,5 | 1 |
| Surveillance et alertes | 1 | 1 |
| Données de départ : niveaux scolaires tunisiens | 0,5 | 0,5 |
| **Total** | **19** | **30,5** |

### 01. Comptes et accès

| Story | Bas | Haut |
|---|---|---|
| CPT-01 · Se connecter et se déconnecter | 1,5 | 2,5 |
| CPT-02 · Mot de passe depuis l'invitation | 1 | 1,5 |
| CPT-03 · Mot de passe oublié | 0,5 | 1 |
| CPT-04 · Inviter un compte | 1 | 2 |
| CPT-05 · Désactiver un compte | 0,5 | 1 |
| CPT-06 · Cacher ce qui est réservé au propriétaire | 1 | 1,5 |
| **Total** | **5,5** | **9,5** |

### 02. Centre

| Story | Bas | Haut |
|---|---|---|
| CEN-01 · Créer un centre par une commande | 0,5 | 1 |
| CEN-02 · Paramètres du centre | 1 | 2 |
| CEN-03 · Liste des niveaux | 1 | 1,5 |
| CEN-04 · Année scolaire suivante | 1,5 | 3 |
| **Total** | **4** | **7,5** |

### 03. Professeurs et groupes

| Story | Bas | Haut |
|---|---|---|
| GRP-01 · Ajouter ou modifier un professeur | 1 | 1,5 |
| GRP-02 · Groupes d'un professeur, filtrés par jour | 1 | 2 |
| GRP-03 · Professeur parti | 0,5 | 1 |
| GRP-04 · Groupe d'Étude, séances et Mois | 3 | 5 |
| GRP-05 · Groupe de Révision | 1,5 | 2,5 |
| GRP-06 · Groupe de Formation et tranches | 1,5 | 2,5 |
| GRP-07 · Groupe déjà en cours | 1,5 | 3 |
| GRP-08 · Fermer un groupe | 1 | 2 |
| **Total** | **11** | **19,5** |

### 04. Élèves et inscriptions

| Story | Bas | Haut |
|---|---|---|
| ELV-01 · Chercher un élève | 1 | 2 |
| ELV-02 · Fiche d'un élève | 1,5 | 2,5 |
| ELV-03 · Inscrire, avec le mode de tarification | 3 | 5 |
| ELV-04 · Voir la fiche d'un élève | 2 | 3 |
| ELV-05 · Prix spécial | 1,5 | 3 |
| ELV-06 · Dispense d'assurance | 0,5 | 1 |
| ELV-07 · Paiement fait avant SchoolHub | 1 | 1,5 |
| ELV-08 · Arrêter une inscription | 1 | 1,5 |
| ELV-09 · Changement de groupe | 2 | 3 |
| ELV-10 · Formation payée en totalité | 0,5 | 1 |
| ELV-11 · Supprimer une fiche sans historique | 0,5 | 0,5 |
| **Total** | **14,5** | **24** |

### 05. Demandes d'inscription

| Story | Bas | Haut |
|---|---|---|
| DEM-01 · Formulaire public | 2 | 3 |
| DEM-02 · Lien public et QR code | 0,5 | 1 |
| DEM-03 · Liste des demandes | 1 | 1,5 |
| DEM-04 · Enregistrer une demande | 1,5 | 2,5 |
| DEM-05 · Refuser une demande | 0,5 | 0,5 |
| **Total** | **5,5** | **8,5** |

### 06. Séances et appel

| Story | Bas | Haut |
|---|---|---|
| SEA-01 · Accueil « Aujourd'hui » | 1,5 | 2,5 |
| SEA-02 · Séances d'un groupe | 1 | 1,5 |
| SEA-03 · Annuler une séance | 1,5 | 2,5 |
| SEA-04 · Déplacer une séance | 1 | 1,5 |
| SEA-05 · Séance supplémentaire | 0,5 | 1 |
| SEA-06 · Changer l'horaire d'un groupe | 1,5 | 2,5 |
| SEA-07 · Faire l'appel | 1,5 | 2,5 |
| SEA-08 · Feuille d'appel | 1,5 | 2 |
| SEA-09 · Recopier une feuille d'appel | 0,5 | 1 |
| SEA-10 · Corriger une présence | 1 | 1,5 |
| SEA-11 · Prévenir les absents et leurs parents | 0,5 | 1 |
| SEA-12 · Rattrapage | 2 | 3 |
| **Total** | **14** | **22,5** |

### 07. Caisse et reçus

| Story | Bas | Haut |
|---|---|---|
| CAI-01 · Ce que l'élève doit, et sa couleur | 3 | 5 |
| CAI-02 · Badge « groupe payé » | 0,5 | 1 |
| CAI-03 · Encaisser un élève | 2,5 | 4 |
| CAI-04 · Encaissement groupé | 2 | 3 |
| CAI-05 · Remettre le reçu | 2 | 3 |
| CAI-06 · Retrouver un reçu | 1 | 1,5 |
| CAI-07 · Annuler un reçu | 1 | 2 |
| CAI-08 · Envoyer un rappel de paiement | 0,5 | 1 |
| CAI-09 · Clôturer sa caisse | 2 | 3 |
| CAI-10 · Contrôler les clôtures | 1 | 1,5 |
| **Total** | **15,5** | **25** |

### 08. Bibliothèque

| Story | Bas | Haut |
|---|---|---|
| BIB-01 · Entrée à la journée | 1,5 | 2 |
| BIB-02 · Liste du jour et départs | 1 | 1,5 |
| BIB-03 · Liste d'un jour passé | 0,5 | 1 |
| **Total** | **3** | **4,5** |

### 09. Parts des professeurs

| Story | Bas | Haut |
|---|---|---|
| PRT-01 · Parts à payer | 2,5 | 4 |
| PRT-02 · Part payée | 0,5 | 0,5 |
| PRT-03 · Avance | 1 | 1,5 |
| PRT-04 · Part du centre d'un professeur | 0,5 | 1 |
| **Total** | **4,5** | **7** |

### 10. RH et achats

| Story | Bas | Haut |
|---|---|---|
| RHA-01 · Fiche d'un employé | 1 | 1,5 |
| RHA-02 · Jours de congé | 0,5 | 1 |
| RHA-03 · Salaire payé | 1 | 1,5 |
| RHA-04 · Saisir un achat | 1,5 | 2,5 |
| RHA-05 · Refaire un achat | 0,5 | 0,5 |
| RHA-06 · Totaux des achats | 0,5 | 1 |
| **Total** | **5** | **8** |

### 11. Tableau de bord et journal

| Story | Bas | Haut |
|---|---|---|
| TDB-01 · Tableau de bord | 2,5 | 4 |
| TDB-02 · Détail par groupe et par professeur | 1 | 2 |
| TDB-03 · Prix spéciaux et dispenses | 0,5 | 1 |
| TDB-04 · Journal | 1 | 1,5 |
| **Total** | **5** | **8,5** |

### 12. Listes

| Story | Bas | Haut |
|---|---|---|
| LST-01 · Imprimer une liste | 1,5 | 3 |
| LST-02 · Exporter en Excel | 1 | 2 |
| LST-03 · Importer des élèves | 2 | 3,5 |
| LST-04 · Importer des professeurs | 0,5 | 1 |
| LST-05 · Sélection multiple | 1,5 | 2,5 |
| **Total** | **6,5** | **12** |

## Ce que l'estimation ne compte pas

- La préparation des fichiers d'import et la formation au centre pilote.
- Les semaines sans travail : vacances, Ramadan, examens.
- Les changements demandés après coup par le centre pilote.
