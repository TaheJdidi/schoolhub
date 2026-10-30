# 12. Architecture technique

Type: grilling
Mode: HITL
Status: open
Blocked by: 03, 09

## Question

Quelle architecture pour un SaaS multi-centres en Angular 22 et .NET 10 ?

- **Séparation des centres** : base partagée avec un identifiant de centre, un schéma par centre, ou une base par centre ?
- **Hébergement** : en Tunisie ou à l'étranger ? Fournisseur et coût mensuel.
- **Base de données** : PostgreSQL ou SQL Server.
- **Authentification et comptes** : ASP.NET Core Identity ou fournisseur externe.
- **Rôles** : un compte appartient à un seul centre, avec deux rôles fixes (Propriétaire, Secrétaire), et un journal des actions réservé au propriétaire. Voir [Rôles, comptes et accès](03-roles-comptes-acces.md).
- **Documents** : reçus PDF, import/export Excel, impressions.
- **Arabe** : écriture de droite à gauche dans Angular, documents bilingues.
- **Notifications** : abstraction d'envoi (liens `wa.me` et email dans le MVP, puis API WhatsApp et SMS), reçus PDF servis par URL à jeton. Voir [Canaux de notification en Tunisie](09-canaux-notification.md).
- **Sauvegardes, journal d'audit, reprise après incident.**

Chaque décision difficile à défaire donne un ADR dans `docs/adr/`.
