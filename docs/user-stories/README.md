# User stories du MVP

Les 78 user stories du MVP de SchoolHub, issues du [ticket 13](../wayfinder/cadrage-mvp/issues/13-user-stories-mvp.md). Le MVP couvre tout le [cahier des charges](../cahier-des-charges.md), plus les ajouts décidés dans la [carte de décisions](../wayfinder/cadrage-mvp/map.md). Il est livré **en une seule fois, complet, le plus tôt possible et avant juin 2027**, quand commence la saison des Révisions, des Formations d'été et de la bibliothèque ([ticket 14](../wayfinder/cadrage-mvp/issues/14-estimation-plan-livraison.md), Q2).

| | |
|---|---|
| Vocabulaire | [CONTEXT.md](../../CONTEXT.md) : chaque mot en gras du domaine y est défini |
| Maquette validée | [docs/maquette/index.html](../maquette/index.html) |
| Règles valables partout | [00-exigences-communes.md](00-exigences-communes.md) |
| Travaux techniques (hors stories) | [travaux-techniques.md](travaux-techniques.md) |

## Format d'une story

```
CAI-03 · Encaisser un élève depuis sa fiche

En tant que secrétaire, je veux …, afin de …

Critères d'acceptation
- un exemple chiffré, vérifiable

Maquette : l'écran concerné
```

- **Le numéro** reprend le code de l'épopée (CAI pour la caisse). Il ne change jamais : une story ajoutée prend le numéro suivant.
- **« Secrétaire »** veut dire la secrétaire *et* le propriétaire, qui a accès à tout. Une story réservée au propriétaire le dit.
- **Les exemples** utilisent les données fictives de la maquette : le Centre Les Jasmins, la secrétaire Amira Jlassi, le professeur Karim Ben Salah, l'élève Adem Miled.

## Les épopées, dans l'ordre de construction

Chaque épopée s'appuie sur les précédentes : on ne peut pas encaisser sans élèves, ni inscrire des élèves sans groupes.

| # | Épopée | Code | Stories | S'appuie sur |
|---|---|---|---|---|
| 00 | [Exigences communes](00-exigences-communes.md) | — | — | — |
| 01 | [Comptes et accès](01-comptes-acces.md) | CPT | 6 | — |
| 02 | [Centre](02-centre.md) | CEN | 4 | 01 |
| 03 | [Professeurs et groupes](03-professeurs-groupes.md) | GRP | 8 | 02 |
| 04 | [Élèves et inscriptions](04-eleves-inscriptions.md) | ELV | 11 | 03 |
| 05 | [Demandes d'inscription](05-demandes.md) | DEM | 5 | 04 |
| 06 | [Séances et appel](06-seances-appel.md) | SEA | 12 | 03, 04 |
| 07 | [Caisse et reçus](07-caisse-recus.md) | CAI | 10 | 04, 06 |
| 08 | [Bibliothèque](08-bibliotheque.md) | BIB | 3 | 04, 07 |
| 09 | [Parts des professeurs](09-parts-professeurs.md) | PRT | 4 | 06, 07 |
| 10 | [RH et achats](10-rh-achats.md) | RHA | 6 | 07 |
| 11 | [Tableau de bord et journal](11-tableau-journal.md) | TDB | 4 | 07 à 10 |
| 12 | [Listes](12-listes.md) | LST | 5 | 03, 04 |

Le journal s'écrit dès la première story qui touche à l'argent ou à la présence ([exigences communes](00-exigences-communes.md#journal)) ; seul l'écran qui le consulte (TDB-04) vient à la fin.

## Toutes les stories

| Épopée | Stories |
|---|---|
| CPT | 01 se connecter · 02 mot de passe depuis l'invitation · 03 mot de passe oublié · 04 inviter un compte · 05 désactiver un compte · 06 cacher à la secrétaire ce qui est réservé |
| CEN | 01 créer un centre par une commande · 02 paramètres du centre · 03 liste des niveaux · 04 année scolaire suivante |
| GRP | 01 professeur · 02 groupes d'un professeur · 03 professeur parti · 04 groupe d'Étude · 05 groupe de Révision · 06 groupe de Formation · 07 groupe déjà en cours · 08 fermer un groupe |
| ELV | 01 chercher un élève · 02 fiche élève · 03 inscrire · 04 voir la fiche · 05 prix spécial · 06 dispense d'assurance · 07 déjà payé avant SchoolHub · 08 arrêter une inscription · 09 changement de groupe · 10 Formation payée en totalité · 11 supprimer une fiche |
| DEM | 01 formulaire public · 02 lien et QR code · 03 liste des demandes · 04 enregistrer une demande · 05 refuser une demande |
| SEA | 01 accueil « Aujourd'hui » · 02 séances d'un groupe · 03 annuler · 04 déplacer · 05 séance supplémentaire · 06 changer l'horaire · 07 faire l'appel · 08 feuille d'appel · 09 recopier une feuille · 10 corriger une présence · 11 prévenir les absents et leurs parents · 12 rattrapage |
| CAI | 01 ce que l'élève doit et sa couleur · 02 badge « groupe payé » · 03 encaisser un élève · 04 encaissement groupé · 05 remettre le reçu · 06 retrouver un reçu · 07 annuler un reçu · 08 rappel de paiement · 09 clôturer sa caisse · 10 contrôler les clôtures |
| BIB | 01 entrée à la journée · 02 liste du jour · 03 jour passé |
| PRT | 01 parts à payer · 02 part payée · 03 avance · 04 part du centre d'un professeur |
| RHA | 01 fiche employé · 02 congés · 03 salaire payé · 04 saisir un achat · 05 refaire un achat · 06 totaux des achats |
| TDB | 01 tableau de bord · 02 détail par groupe · 03 prix spéciaux et dispenses · 04 journal |
| LST | 01 imprimer · 02 exporter · 03 importer des élèves · 04 importer des professeurs · 05 sélection multiple |
