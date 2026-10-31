# Hébergement sur Azure, région France

SchoolHub est hébergé sur Azure, en région France. L'application .NET tourne sur Azure App Service, la base sur Azure Database for PostgreSQL, et les photos de factures sur Azure Blob Storage.

Le centre ne doit pas perdre plus de quelques minutes de saisies, car la caisse et les reçus sont en jeu ([ticket 12](../wayfinder/cadrage-mvp/issues/12-architecture-technique.md), Q7). La base PostgreSQL gérée par Azure assure cette restauration à la minute près sans rien installer. C'est ce qui a emporté la décision pour une petite équipe sans personne dédiée à l'exploitation.

Coût estimé pour le pilote : environ 30 à 40 $ par mois, à vérifier avant de souscrire.

## Options écartées

- **Serveur virtuel en Europe (Hetzner, OVH)** : deux à trois fois moins cher, mais la sauvegarde continue et la surveillance sont à installer et à entretenir soi-même.
- **Hébergeur tunisien** : une base PostgreSQL gérée, avec restauration à la minute, y est rarement proposée.

## Conséquences

Le code reste du .NET et du PostgreSQL standard. Changer d'hébergeur plus tard demande de refaire le déploiement et les sauvegardes, pas de réécrire l'application.

*Décidé le 31/10/2026, [ticket 12](../wayfinder/cadrage-mvp/issues/12-architecture-technique.md), Q9.*
