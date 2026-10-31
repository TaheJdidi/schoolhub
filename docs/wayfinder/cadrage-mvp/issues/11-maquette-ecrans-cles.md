# 11. Maquette des écrans clés

Type: prototype
Mode: HITL
Status: claimed (Taha)
Blocked by: 01, 02, 03, 10

## Question

À quoi ressemblent les écrans clés ? Le but est de valider le format du front-end avant d'écrire les user stories.

Écrans proposés :

- liste des professeurs, puis les groupes d'un professeur (un cadre par groupe, filtre par jour) ;
- fiche groupe : présence de la séance du jour et code couleur des élèves ;
- feuille d'appel imprimable ([Séances, présence et rattrapage](02-seances-presence-rattrapage.md), Q11) ;
- formulaire public d'inscription et liste des demandes ([Parcours d'inscription d'un élève](04-parcours-inscription-eleve.md)) ;
- parts des professeurs à payer, avec la case « payée » ([Paiement des professeurs](05-paiement-professeurs.md)) ;
- tableau de bord du propriétaire ([RH, achats et tableau de bord](07-rh-achats-tableau-de-bord.md)) ;
- encaissement groupé et reçu ;
- remise du reçu au choix (impression, WhatsApp ou email) et clôture de caisse du soir ([Caisse et reçus](10-caisse-recus.md)) ;
- fiche élève : ses inscriptions, paiements et présences ;
- bibliothèque : liste du jour ;
- version mobile pour la secrétaire.

La maquette est liée depuis ce ticket, pas collée dedans.

## Avancement

### Décisions prises (tour 1, 31/10/2026)

- **Q1. Trois styles comparés** sur le chemin quotidien de la secrétaire : A, par professeur (ses groupes en cadres, filtrés par jour) ; B, la journée (les séances du jour, heure par heure) ; C, le guichet (recherche d'un élève, sur téléphone).
- **Q2. Mélange retenu** : la journée (B) est la page d'accueil ; les professeurs et leurs groupes (A) sont dans le menu ; la recherche d'un élève (C) reste toujours en haut de l'écran, pour encaisser vite au guichet, sur ordinateur comme sur téléphone.
- **Q3. Format** : une maquette cliquable, en une seule page HTML avec des données fictives, qui s'ouvre sans serveur : [docs/maquette/index.html](../../../maquette/index.html).
- **Maquette v1 livrée le 31/10/2026** : les 11 écrans du ticket, plus l'accueil « Aujourd'hui », en rôle secrétaire ou propriétaire et en vue ordinateur ou téléphone. Données fictives (« Centre Les Jasmins »). Reste à la valider avec le centre pilote avant de résoudre le ticket.
