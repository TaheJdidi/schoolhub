# Travaux techniques

Ce que personne au centre ne « demande », mais qu'il faut construire pour que les stories marchent. Ces travaux viennent de l'[architecture technique](../wayfinder/cadrage-mvp/issues/12-architecture-technique.md) et des [ADR](../adr/). La liste sert à l'[estimation](../wayfinder/cadrage-mvp/issues/14-estimation-plan-livraison.md) (ticket 14) : ce ne sont pas des user stories.

## Socle de l'application

- **Projet .NET 10 découpé en modules** : élèves et inscriptions, groupes et séances, caisse et reçus, bibliothèque, professeurs, RH et achats, journal ([ADR 0003](../adr/0003-un-seul-deploiement-decoupe-en-modules.md)).
- **Application Angular 22** servie par .NET, à une seule adresse, installable (PWA), responsive.
- **PostgreSQL avec EF Core** ([ADR 0002](../adr/0002-postgresql.md)), et PostgreSQL dans Docker sur le poste du développeur.
- **Séparation des centres** : identifiant du centre sur chaque ligne, filtre automatique, et un test d'isolation pour chaque nouvelle donnée ([ADR 0001](../adr/0001-une-base-partagee-entre-les-centres.md)).
- **ASP.NET Core Identity** : connexion par cookie, deux rôles, invitation, mot de passe oublié, blocage après plusieurs essais.
- **Journal** : une écriture explicite pour chaque action listée dans les [exigences communes](00-exigences-communes.md#journal).
- **Données de départ** : la liste des niveaux scolaires tunisiens (CEN-03).
- **Tâche quotidienne** : termine les groupes arrivés au bout (CEN-04, GRP-05, GRP-06) et signale les caisses oubliées (CAI-09).

## Documents et messages

- **PDF avec QuestPDF** : reçu A5, reçus groupés, feuille d'appel, affiche du QR code, impression des listes.
- **Excel avec ClosedXML** : export des listes, fichiers modèles et import (LST-02 à LST-04).
- **Numéros de reçu** : un compteur par centre, pris dans la même transaction que le paiement, sans trou.
- **Abstraction d'envoi** : liens `wa.me` et emails par Brevo, avec un nom de domaine configuré en SPF et DKIM. Elle permettra plus tard l'API WhatsApp et le SMS ([ticket 09](../wayfinder/cadrage-mvp/issues/09-canaux-notification.md)).
- **Formulaire public** : adresse `…/inscription/<code-du-centre>`, QR code, limite d'envois et champ piège (DEM-01).
- **Photos de factures** dans le stockage Azure, derrière une interface de stockage (RHA-04).
- **Commande de création d'un centre** (CEN-01).

## Hébergement et exploitation

- **Azure, région France** : App Service, Azure Database for PostgreSQL et stockage Azure ([ADR 0005](../adr/0005-hebergement-azure.md)).
- **Pipeline GitHub Actions lancé à la main** : compile, passe les tests, déploie ; retour à la version précédente en cas de problème.
- **Sauvegardes** sur 7 jours, et un essai de restauration chaque mois.
- **Surveillance** : test de disponibilité toutes les 5 minutes, Application Insights, alertes à Taha et Lassad par email et sur téléphone.

## À faire avant le pilote

- Choisir et acheter le nom de domaine, puis configurer SPF et DKIM pour Brevo.
- Ouvrir l'abonnement Azure, payé par carte internationale.
- Préparer avec le centre pilote ses fichiers d'import (élèves, professeurs) et ses groupes de la rentrée 2027.
