# Une seule base de données partagée entre les centres

SchoolHub sert plusieurs centres. Toutes leurs données sont dans une seule base PostgreSQL, où chaque ligne porte l'identifiant de son centre. Un filtre automatique (EF Core) s'applique à chaque requête, et des tests vérifient qu'un centre ne voit jamais les données d'un autre. Un compte appartient à un seul centre ([ticket 03](../wayfinder/cadrage-mvp/issues/03-roles-comptes-acces.md)) : le centre se déduit du compte connecté.

## Options écartées

Un schéma ou une base par centre isolerait mieux les données. Pour un pilote puis quelques dizaines de petits centres, cela coûterait plus cher en hébergement, en migrations et en exploitation.

*Décidé le 31/10/2026, [ticket 12](../wayfinder/cadrage-mvp/issues/12-architecture-technique.md), Q1.*
