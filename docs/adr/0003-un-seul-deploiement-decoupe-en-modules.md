# Un seul déploiement, découpé en modules

L'application .NET 10 sert à la fois l'API et les pages Angular 22, à la même adresse. Il n'y a ainsi qu'un serveur à faire tourner, et la connexion par cookie fonctionne sans configuration entre domaines (CORS).

Le code .NET est un seul projet découpé en modules plutôt qu'en services séparés. Les modules sont : élèves et inscriptions, groupes et séances, caisse et reçus, bibliothèque, professeurs, RH et achats, journal. Ils pourront être séparés plus tard si la charge l'exige.

*Décidé le 31/10/2026, [ticket 12](../wayfinder/cadrage-mvp/issues/12-architecture-technique.md), Q3.*
