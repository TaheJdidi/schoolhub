# 12. Architecture technique

Type: grilling
Mode: HITL
Status: resolved
Blocked by: 03, 09

## Question

Quelle architecture pour un SaaS multi-centres en Angular 22 et .NET 10 ?

- **Séparation des centres** : base partagée avec un identifiant de centre, un schéma par centre, ou une base par centre ?
- **Hébergement** : en Tunisie ou à l'étranger ? Fournisseur et coût mensuel.
- **Base de données** : PostgreSQL ou SQL Server.
- **Authentification et comptes** : ASP.NET Core Identity ou fournisseur externe.
- **Rôles** : un compte appartient à un seul centre, avec deux rôles fixes (Propriétaire, Secrétaire), et un journal des actions réservé au propriétaire. Voir [Rôles, comptes et accès](03-roles-comptes-acces.md).
- **Formulaire public d'inscription** : un lien et un QR code par centre, sans compte, avec une protection contre les envois abusifs. Voir [Parcours d'inscription d'un élève](04-parcours-inscription-eleve.md).
- **Documents** : reçus PDF, import/export Excel, impressions.
- **Photos des factures d'achat**, facultatives, jointes à chaque achat. Voir [RH, achats et tableau de bord](07-rh-achats-tableau-de-bord.md).
- **Arabe** : écriture de droite à gauche dans Angular, documents bilingues.
- **Notifications** : abstraction d'envoi (liens `wa.me` et email dans le MVP, puis API WhatsApp et SMS), reçus PDF servis par URL à jeton. Voir [Canaux de notification en Tunisie](09-canaux-notification.md).
- **Reçus** : numérotation sans trou par centre, PDF au format A5 pour l'impression et l'email, texte complet pour le message WhatsApp (sans lien vers le PDF). Voir [Caisse et reçus](10-caisse-recus.md).
- **Sauvegardes, journal d'audit, reprise après incident.**

Chaque décision difficile à défaire donne un ADR dans `docs/adr/`.

## Avancement

> Grilling terminé le 31/10/2026 : toutes les questions sont tranchées, et la synthèse est dans « Réponse », en bas du ticket.

### Décisions prises (tour 1, 31/10/2026)

- **Q1. Une seule base partagée** : chaque ligne porte l'identifiant de son centre, filtré automatiquement. [ADR 0001](../../../adr/0001-une-base-partagee-entre-les-centres.md).
- **Q2. PostgreSQL.** [ADR 0002](../../../adr/0002-postgresql.md).
- **Q3. Un seul déploiement** (l'application .NET sert aussi Angular), et un seul projet découpé en modules. [ADR 0003](../../../adr/0003-un-seul-deploiement-decoupe-en-modules.md).
- **Q4. ASP.NET Core Identity** pour la connexion et les comptes.
- **Q5. Web responsive installable (PWA)**, sans store et sans fonctionnement hors connexion dans le MVP.
- **Q6. Français seulement** : ni fichiers de traduction ni mise en page pour l'écriture de droite à gauche. Cela corrige la décision de cadrage « arabe prévu dès la conception ». [ADR 0004](../../../adr/0004-francais-seulement.md).
- **Q7. Perte maximale de quelques minutes** : sauvegarde continue de la base vers un stockage séparé, et un essai de restauration chaque mois. Les mises à jour se font le soir, après la clôture de caisse.
- **Q8. Choix de détail validés** : QuestPDF, ClosedXML, formulaire public protégé par une limite d'envois et un champ piège, photos derrière une interface de stockage, journal explicite, compteur de reçus par centre.

### Questions du tour 1 (répondues le 31/10/2026)

La recommandation est donnée après ➡️.

- **Q1. Séparation des données entre centres.** (a) Une seule base où chaque ligne porte l'identifiant de son centre, filtré automatiquement à chaque requête ; (b) un schéma par centre dans la même base ; (c) une base par centre. ➡️ (a). C'est le plus simple et le moins cher, pour un pilote puis quelques dizaines de centres. Un compte appartient à un seul centre ([ticket 03](03-roles-comptes-acces.md)) : le centre se déduit du compte connecté. Un filtre automatique dans EF Core et des tests dédiés empêchent un centre de voir les données d'un autre. Difficile à changer plus tard : ADR.
- **Q2. Base de données : PostgreSQL ou SQL Server ?** ➡️ PostgreSQL. Il est gratuit, proposé par tous les hébergeurs, et très bien pris en charge par .NET (EF Core avec Npgsql). SQL Server demande une licence payante au-delà de l'édition Express, limitée à 10 Go par base. ADR.
- **Q3. Forme de l'application.** (a) Un seul déploiement : l'application .NET sert aussi les pages Angular, à la même adresse ; (b) deux déploiements séparés, l'API .NET d'un côté et Angular de l'autre. Côté .NET : un seul projet découpé en modules (élèves et inscriptions, groupes et séances, caisse et reçus, bibliothèque, professeurs, RH et achats, journal), ou plusieurs services séparés ? ➡️ (a), avec un seul projet en modules (« monolithe modulaire »). Il n'y a qu'un serveur à faire tourner, la connexion par cookie reste simple, et les modules pourront être séparés plus tard si besoin. ADR.
- **Q4. Connexion et comptes.** ASP.NET Core Identity, intégré à l'application (email et mot de passe, lien d'invitation pour choisir son mot de passe, mot de passe oublié), ou un service externe (Keycloak, Auth0, Microsoft Entra) ? ➡️ ASP.NET Core Identity. Il est gratuit et suffit pour deux rôles fixes et un compte par centre. La double authentification du propriétaire viendra plus tard.
- **Q5. Sur téléphone.** Web responsive seulement, installable comme une application (PWA : une icône sur l'écran d'accueil) ? Ou une application mobile publiée sur les stores ? Faut-il un fonctionnement hors connexion dans le MVP ? ➡️ Web responsive installable (PWA), sans store et sans hors connexion dans le MVP. Pendant une coupure d'Internet, l'appel se fait sur la feuille d'appel imprimée ([ticket 02](02-seances-presence-rattrapage.md)). Cette réponse tranche deux points de « Pas encore spécifié » de la carte.
- **Q6. Préparer l'arabe dès maintenant.** Le MVP est en français. Pour ajouter l'arabe ensuite sans tout reprendre : (a) tous les textes de l'interface vont dans des fichiers de traduction dès le premier jour, avec changement de langue sans rechargement (bibliothèque Transloco) et une mise en page compatible avec l'écriture de droite à gauche ; (b) les textes sont écrits directement en français, et la traduction viendra plus tard. ➡️ (a). Le surcoût est faible au début, alors que traduire après coup oblige à repasser sur tous les écrans.
- **Q7. Perte de données et coupures acceptables.** Si le serveur tombe en panne, combien de saisies le centre peut-il perdre au pire : (a) la journée ; (b) la dernière heure ; (c) quelques minutes ? L'application peut-elle être indisponible quelques minutes pour une mise à jour, le soir ? ➡️ (c), car la caisse et les reçus sont en jeu : sauvegarde continue de la base vers un stockage séparé, avec un essai de restauration chaque mois. Les mises à jour se font le soir, après la clôture de caisse. *Le choix de l'hébergement (tour 2) en dépend.*
- **Q8. Choix techniques de détail, à valider en bloc.**
  - Reçus PDF au format A5 : bibliothèque QuestPDF, gratuite pour les entreprises de moins d'un million de dollars de chiffre d'affaires.
  - Import et export Excel : ClosedXML, gratuite.
  - Formulaire public d'inscription : nombre d'envois limité par adresse, et champ piège invisible contre les robots. Un captcha seulement si des abus apparaissent.
  - Photos de factures : sur le disque du serveur, sauvegardées avec la base, derrière une interface qui permettra de passer plus tard à un stockage externe.
  - Journal : une écriture explicite pour chaque action listée au [ticket 03](03-roles-comptes-acces.md) (qui, quoi, quand, avant et après), plutôt qu'un enregistrement automatique de toutes les modifications.
  - Numéros de reçu : un compteur par centre, pris dans la même transaction que le paiement, pour qu'il n'y ait jamais de trou.

  ➡️ Tout valider.

### Décisions prises (tour 2, 31/10/2026)

- **Q9. Azure, région France.** L'application et PostgreSQL sont gérés par Azure, avec une restauration de la base à la minute près. Les photos de factures vont dans le stockage d'Azure. [ADR 0005](../../../adr/0005-hebergement-azure.md).
- **Q10. Production seulement**, sans environnement de test. Chaque version est vérifiée sur le poste du développeur, avec PostgreSQL dans Docker. Par conséquent, chaque mise en production se fait le soir, après la clôture de caisse. En cas de problème, on remet la version précédente et, si besoin, on restaure la base à l'heure d'avant la mise à jour.
- **Q11. Mise en production manuelle**, par un pipeline GitHub Actions lancé à la main. On ne pousse jamais directement sur `main` : tout passe par une pull request validée par Taha et Lassad. La règle exacte est en Q15. *Modifié en Q15 : pas d'approbation obligatoire, push direct sur `main` autorisé.*
- **Q12. Une seule adresse pour tous les centres.** Le nom de domaine reste à choisir et à acheter avant la mise en ligne du pilote.
- **Q13. Brevo pendant le pilote**, avec des emails envoyés depuis le domaine du produit et authentifiés (SPF, DKIM).

### Questions du tour 2 (répondues le 31/10/2026)

La recommandation est donnée après ➡️. Les prix sont des ordres de grandeur, à vérifier avant de souscrire.

- **Q9. Hébergeur.** Contraintes : PostgreSQL, sauvegarde continue (Q7), une seule application .NET (Q3), et un paiement de l'hébergeur depuis la Tunisie, en général par carte internationale.
  - (a) **Azure, région France** : application et PostgreSQL gérés par Azure, avec restauration à la minute près incluse. Environ 30 à 40 $ par mois pour le pilote (≈ 90 à 120 TND).
  - (b) **Serveur virtuel en Europe** (Hetzner, OVH) : tout tourne dans des conteneurs Docker, et la sauvegarde continue est à mettre en place soi-même. Environ 10 à 20 € par mois (≈ 35 à 70 TND), mais plus de travail d'exploitation.
  - (c) **Hébergeur tunisien** : prix et services à étudier. Une base PostgreSQL gérée avec restauration à la minute y est rarement proposée.

  ➡️ (a). C'est le moins de travail d'exploitation pour une petite équipe, la meilleure intégration avec .NET, et la sauvegarde continue sans rien installer. Les photos de factures iraient dans un stockage Azure (quelques centimes par mois), grâce à l'interface prévue en Q8. Difficile à changer plus tard : ADR.
- **Q10. Environnements.** Production seule, ou production plus un environnement de test (données fictives) pour vérifier chaque version avant de la mettre en ligne ? ➡️ Production et test, la base de test à part, plus PostgreSQL dans Docker sur le poste du développeur. Avec (a), l'application de test peut tourner sur le même forfait Azure, sans surcoût notable.
- **Q11. Mise en ligne automatique.** À chaque fusion sur `main`, GitHub Actions compile, lance les tests et déploie sur l'environnement de test. La mise en production se fait d'un clic, le soir. ➡️ Oui.
- **Q12. Adresse de l'application.** Une seule adresse pour tous les centres (par exemple `app.<domaine>`), où chacun se connecte à son compte. Le formulaire public d'un centre a une adresse du type `…/inscription/<code-du-centre>`, avec son QR code. Quel nom de domaine, en `.tn` ou en `.com` ? ➡️ Une seule adresse pour tous les centres. Le nom de domaine est à choisir par toi, et à acheter avant la mise en ligne du pilote.
- **Q13. Envoi des emails** (invitations, mot de passe oublié, reçus) : Brevo (gratuit jusqu'à 300 emails par jour) ou Amazon SES (environ 0,10 $ les 1 000), comparés au [ticket 09](09-canaux-notification.md) ? ➡️ Brevo pendant le pilote : gratuit et simple à ouvrir. Les emails partent du nom de domaine du produit, authentifiés (SPF, DKIM). L'abstraction d'envoi permet de changer plus tard.
- **Q14. Surveillance.** Être prévenu si l'application ne répond plus ou si des erreurs apparaissent. ➡️ Une vérification de disponibilité toutes les 5 minutes, par un service gratuit avec alerte par email, et un suivi des erreurs (Application Insights avec Azure). Les alertes vont au développeur. *Question précisée au tour 3.*

### Décisions prises (tour 3, 31/10/2026)

- **Q14. Taha et Lassad reçoivent les alertes**, par email et par notification de l'application mobile du robot de surveillance. Rien n'est envoyé tant que tout fonctionne.
- **Q15. Pas d'approbation obligatoire** : on peut pousser directement sur `main`, et la branche n'est pas protégée. Le garde-fou est le pipeline de mise en production, lancé à la main, qui compile et passe les tests avant de déployer. Les pull requests restent possibles pour se faire relire, sans obligation. Cela remplace la règle de Q11, et GitHub Pro n'est pas nécessaire.
- **Q16. Sauvegardes conservées 7 jours**, la durée par défaut d'Azure. On peut revenir à n'importe quelle minute des 7 derniers jours ; les données de la base, elles, restent toujours là et modifiables. Conséquence : une erreur découverte plus de 7 jours après ne peut plus être réparée à partir des sauvegardes.

### Questions du tour 3 (répondues le 31/10/2026)

- **Q14 (précisée). Surveillance.** Rien n'est envoyé tant que tout fonctionne.
  - **Disponibilité** : un robot gratuit (par exemple UptimeRobot) ouvre l'adresse de l'application toutes les 5 minutes. S'il n'obtient pas de réponse, il envoie **une seule alerte**, puis un second message quand l'application répond de nouveau.
  - **Erreurs** : Application Insights enregistre chaque erreur (par exemple un plantage à l'impression d'un reçu) avec son détail. Il envoie une alerte seulement si les erreurs dépassent un seuil, par exemple plus de 5 en 5 minutes.

  Qui reçoit les alertes, et par quel canal ? ➡️ Taha et Lassad, par email et par notification de l'application mobile gratuite du robot.
- **Q15. Règle de validation des pull requests.** GitHub ne compte jamais l'approbation de l'auteur de la pull request. Les pull requests préparées avec Claude sont ouvertes sous le compte de Taha : seul Lassad peut alors les valider.
  - (a) **L'autre valide** : Taha et Lassad sont déclarés responsables du code (fichier `CODEOWNERS`) ; chaque pull request demande l'approbation de celui des deux qui ne l'a pas écrite.
  - (b) **Les deux valident toujours** : impossible quand l'un d'eux est l'auteur, sauf à ouvrir les pull requests depuis un troisième compte.

  Les tests se lancent-ils automatiquement sur chaque pull request, et bloquent-ils la fusion s'ils échouent ? ➡️ (a), et oui pour les tests. Lassad a déjà accès au dépôt (compte `lassadsmaoui92-bit`). *Fait vérifié le 31/10/2026 : `main` n'est pas encore protégée. Sur un dépôt privé, les règles de protection de branche demandent un compte GitHub Pro (environ 4 $ par mois) si le compte est gratuit : à vérifier dans les réglages de facturation de GitHub.*
- **Q16. Conservation des sauvegardes.** Azure garde de quoi restaurer la base pendant 7 jours par défaut, et jusqu'à 35 jours en option, pour un faible coût de stockage en plus. ➡️ 35 jours : une erreur découverte tard, au tableau de bord de fin de mois par exemple, reste réparable. Un essai de restauration chaque mois (Q7).

  **Explication (31/10/2026).** Azure sauvegarde la base tout seul : une copie complète chaque jour, plus chaque modification au fil de l'eau. On peut ainsi retrouver la base telle qu'elle était à n'importe quelle minute, dans la limite de la **durée de conservation**.
  - *Ce que la durée ne change pas* : les données de la base restent toujours là, et toujours modifiables selon les règles de l'application. La durée dit seulement jusqu'où on peut **revenir en arrière**, comme une caméra de surveillance qui garde 7 ou 35 jours d'enregistrement.
  - *Exemple* : le 28 novembre, la secrétaire découvre que les paiements d'un groupe ont disparu le 3 novembre, à cause d'une fausse manœuvre ou d'un bug. Avec 7 jours de conservation, on ne peut remonter qu'au 21 novembre : les données du 3 sont perdues. Avec 35 jours, on remonte au 2 novembre au soir et on les récupère.
  - *Sans risque pour la base en service* : la restauration crée une copie à part. On y reprend les lignes perdues, puis on supprime la copie. Le centre continue de travailler pendant ce temps.
  - *Coût* : Azure inclut un volume de sauvegarde égal à la taille de la base, et la base d'un centre pèse peu. Les 35 jours devraient donc coûter zéro ou quelques centimes par mois, à vérifier sur la facture.
  - *Essai mensuel* : une fois par mois, le développeur restaure une copie pour vérifier que les sauvegardes fonctionnent vraiment, puis la supprime. Cela coûte quelques centimes.

## Réponse

Résolu le 31/10/2026, par grilling avec Taha. Le détail des échanges est dans « Avancement » ci-dessus. Les décisions difficiles à défaire ont chacune leur ADR dans [docs/adr/](../../../adr/).

### Forme de l'application

- **Une seule application .NET 10**, qui sert aussi les pages Angular 22, à **une seule adresse pour tous les centres**. Le code .NET est un seul projet découpé en modules : élèves et inscriptions, groupes et séances, caisse et reçus, bibliothèque, professeurs, RH et achats, journal. [ADR 0003](../../../adr/0003-un-seul-deploiement-decoupe-en-modules.md).
- **Une base PostgreSQL partagée** : chaque ligne porte l'identifiant de son centre, avec un filtre automatique dans EF Core et des tests d'isolation. [ADR 0001](../../../adr/0001-une-base-partagee-entre-les-centres.md), [ADR 0002](../../../adr/0002-postgresql.md).
- **Connexion par ASP.NET Core Identity** : email et mot de passe, lien d'invitation, mot de passe oublié. Deux rôles fixes, et un compte appartient à un seul centre.
- **Web responsive installable (PWA)**, sans store et sans fonctionnement hors connexion.
- **Français seulement.** [ADR 0004](../../../adr/0004-francais-seulement.md).

### Choix techniques

- **Reçus PDF** au format A5 avec QuestPDF ; **import et export Excel** avec ClosedXML.
- **Formulaire public d'inscription** à l'adresse `…/inscription/<code-du-centre>`, avec son QR code, protégé par une limite d'envois et un champ piège.
- **Photos de factures** dans le stockage d'Azure, derrière une interface de stockage.
- **Journal** : une écriture explicite pour chaque action listée au [ticket 03](03-roles-comptes-acces.md).
- **Numéros de reçu** : un compteur par centre, pris dans la même transaction que le paiement, pour qu'il n'y ait jamais de trou.
- **Notifications** : une abstraction d'envoi, avec liens `wa.me` et emails par Brevo dans le MVP. Les emails partent du domaine du produit (SPF, DKIM).

### Hébergement et exploitation

- **Azure, région France** : App Service, Azure Database for PostgreSQL et stockage Azure. Environ 30 à 40 $ par mois pour le pilote. [ADR 0005](../../../adr/0005-hebergement-azure.md).
- **Production seulement.** Chaque version est vérifiée sur le poste du développeur, avec PostgreSQL dans Docker.
- **Mise en production manuelle** : un pipeline GitHub Actions lancé à la main compile, passe les tests et déploie, le soir après la clôture de caisse. En cas de problème, on remet la version précédente et, si besoin, on restaure la base.
- **Push direct sur `main` autorisé**, sans approbation obligatoire ; les pull requests restent possibles pour se faire relire.
- **Sauvegardes** : restauration à la minute près sur les 7 derniers jours, et un essai de restauration chaque mois.
- **Surveillance** : un robot vérifie la disponibilité toutes les 5 minutes, et Application Insights suit les erreurs avec un seuil d'alerte. Les alertes vont à Taha et Lassad, par email et par notification sur téléphone.

### À faire avant le pilote

- Choisir et acheter le nom de domaine, puis configurer SPF et DKIM pour Brevo.
- Ouvrir l'abonnement Azure, payé par carte internationale.

### Plus tard

- Double authentification du propriétaire.
- Environnement de test.
- Arabe.
- Fonctionnement hors connexion.
- API WhatsApp et SMS.
- Sauvegardes conservées plus longtemps.
