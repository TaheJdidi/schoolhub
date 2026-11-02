# Comptes et connexion : l'accès de base

Conception établie le 02/11/2026 avec Lassad et Taha. C'est le deuxième morceau du [socle](../../user-stories/travaux-techniques.md#socle-de-lapplication), après la séparation des centres.

## But

Une personne d'un centre se connecte avec son email et son mot de passe. Ensuite, tout ce qu'elle fait se limite à **son centre** et à **son rôle**. L'équipe SchoolHub ouvre un nouveau centre avec une commande.

## Périmètre

**Dans cette étape :**

- [CEN-01](../../user-stories/02-centre.md#cen-01--créer-un-centre-et-son-premier-propriétaire-par-une-commande) : créer un centre et son premier propriétaire par une commande ;
- [CPT-01](../../user-stories/01-comptes-acces.md#cpt-01--se-connecter-et-se-déconnecter) : se connecter et se déconnecter ;
- [CPT-02](../../user-stories/01-comptes-acces.md#cpt-02--choisir-son-mot-de-passe-depuis-le-lien-dinvitation) : choisir son mot de passe depuis le lien d'invitation ;
- [CPT-03](../../user-stories/01-comptes-acces.md#cpt-03--retrouver-laccès-par--mot-de-passe-oublié-) : retrouver l'accès par « mot de passe oublié » ;
- [CPT-06](../../user-stories/01-comptes-acces.md#cpt-06--cacher-à-la-secrétaire-ce-qui-est-réservé-au-propriétaire) : le refus côté serveur de ce qui est réservé au propriétaire ;
- le centre courant, pris du compte connecté.

**Côté serveur seulement** : des adresses d'API et leurs tests automatiques. Les écrans (connexion, choix du mot de passe, mot de passe oublié, menu selon le rôle) viendront avec l'application Angular, le morceau suivant du socle.

**Plus tard :**

- CPT-04 (inviter) et CPT-05 (désactiver) : après le Journal, car ils y écrivent.
- Brevo, pour envoyer les vrais emails : avec le nom de domaine, avant le pilote.
- La double authentification du propriétaire (ticket 12, « Plus tard »).

## Décisions

| Question | Décision |
|---|---|
| Quel morceau du socle d'abord ? | Comptes et connexion : le centre courant, le journal et les écrans en dépendent. |
| Jusqu'où va cette étape ? | L'accès de base, ci-dessus. |
| Sous quelle forme lancer la commande CEN-01 ? | Un mode commande de l'application elle-même. En production, on la lance depuis la console SSH de l'App Service, qui a déjà la base et la clé d'envoi des emails : aucun secret de production sur les postes. |
| Comment brancher ASP.NET Core Identity ? | Identity fait le travail délicat (hachage des mots de passe, blocage, jetons, cookie), derrière nos propres adresses d'API. Pas de `MapIdentityApi` : il est en anglais, ouvre une inscription libre et donne des jetons au lieu du cookie. |

## 1. Les données

### Le compte

`Compte` hérite de `IdentityUser<Guid>`, dans la table `comptes`. Il ajoute :

| Champ | Contenu |
|---|---|
| `Prenom`, `Nom` | le nom de la personne, affiché sur les reçus et les appels |
| `CentreId` | son centre, par une clé étrangère qui interdit de supprimer un centre qui a des comptes |
| `Role` | `Proprietaire` ou `Secretaire`, enregistré en texte |
| `Etat` | `Invite` (pas encore de mot de passe), `Actif` ou `Desactive`, enregistré en texte |

- **L'email est unique dans tout SchoolHub**, tous centres confondus (règle de CPT-04) : option `RequireUniqueEmail` d'Identity, plus un index unique sur l'email normalisé.
- **Le rôle est une simple colonne**, pas le système de rôles d'Identity, trop lourd pour deux rôles fixes. `SchoolHubDbContext` hérite donc de `IdentityUserContext<Compte, Guid>`, qui n'a pas de tables de rôles.
- Identity ajoute **3 tables techniques**, vides pour l'instant : `comptes_revendications`, `comptes_connexions_externes` et `comptes_jetons`.
- L'état `Desactive` existe dès maintenant, car un compte désactivé ne peut pas se connecter (CPT-01). L'action « désactiver » viendra avec CPT-05.

### Le compte est une donnée commune

À la connexion, on cherche le compte par son email **avant** de connaître son centre. Avec le filtre automatique, on ne le trouverait jamais. Le compte n'implémente donc pas `IAppartientAuCentre` : il rejoint les **données communes**, comme le centre.

Il garde sa colonne `centre_id`, et aucun autre centre ne le voit, car on ne le lit qu'à deux endroits :

1. à la connexion, par son email, et à chaque requête, par son identifiant (section 2) ;
2. plus tard, sur la page « Comptes » (CPT-04), qui filtrera **explicitement** sur le centre courant, avec son propre test d'isolation.

**Règle** : toute nouvelle lecture des comptes filtre sur le centre courant et a son test d'isolation. Dans `ModeleTests`, `Compte` et les 3 tables techniques d'Identity rejoignent la liste `DonneesCommunes`, avec un commentaire qui rappelle cette règle.

## 2. Connexion et session (CPT-01)

### Les adresses

| Adresse | Ce qu'elle fait |
|---|---|
| `POST /api/connexion` | reçoit l'email et le mot de passe, puis pose le cookie de session |
| `POST /api/deconnexion` | efface le cookie |
| `GET /api/moi` | dit qui est connecté : prénom, nom, rôle et nom du centre |

Les erreurs répondent `{ "message": "…" }`, en français.

### Ce que répond la connexion

| Cas | Réponse |
|---|---|
| Email inconnu, mauvais mot de passe, ou compte encore *invité* | 401 « Email ou mot de passe incorrect » |
| 5ᵉ essai faux de suite, et tant que dure le blocage | 401 « Trop d'essais : réessayez dans 15 minutes » |
| Bon mot de passe, mais compte *désactivé* | 403 « Ce compte est désactivé : adressez-vous au propriétaire du centre » |
| Bon mot de passe, compte *actif* | 200, avec le cookie, et la même réponse que `GET /api/moi` |

- Le blocage (5 essais, 15 minutes) est celui d'Identity. Un essai réussi remet le compteur à zéro.
- Le message de blocage laisse deviner que l'email existe, mais seulement après 5 essais faux sur ce compte. C'est un compromis accepté, plus clair pour la secrétaire qu'un message qui ne change jamais.
- Un compte *invité* n'a pas de mot de passe : tout essai échoue, avec le message général.
- Le compte *désactivé* est refusé par la vérification « peut-il se connecter ? » d'Identity (`CanSignInAsync`), qui exige l'état `Actif`. Ce message n'apparaît qu'avec le bon mot de passe, il ne révèle donc rien.

### La session

- **Un cookie protégé** : `HttpOnly` (illisible par le JavaScript), `Secure` (envoyé seulement en HTTPS), `SameSite=Strict` (jamais envoyé depuis un autre site, ce qui bloque aussi les attaques par faux formulaire).
- **Il expire après 12 heures sans activité.** Le cookie est renouvelé **à chaque requête**, et pas seulement à mi-parcours comme le fait ASP.NET par défaut. Sinon, une personne inactive pourrait être déconnectée au bout de 6 heures seulement.
- **À chaque requête**, le serveur relit le compte et vérifie son tampon de sécurité (`SecurityStamp`, vérifié sans délai). Désactiver un compte (CPT-05) changera ce tampon : la personne sera déconnectée dès sa prochaine action. Cela coûte une petite requête SQL par appel, ce qui est négligeable pour un centre.
- Le cookie contient l'identifiant du compte, son centre et son rôle. Il est reconstruit à chaque vérification, à partir de la base.
- **Aucune réponse de l'API n'est gardée en cache** (`Cache-Control: no-store`) : après « Se déconnecter », le bouton « retour » du navigateur ne montre plus aucune donnée.
- Sans cookie valable, l'API répond **401**, sans redirection vers une page de connexion. C'est l'application Angular qui affichera la page de connexion.

### Le centre courant

Juste après l'authentification, un middleware remplit `CentreCourant` avec le centre du compte connecté :

```
requête → cookie lu → compte connecté → CentreCourant = son centre → filtre automatique
requête sans cookie → pas de centre courant → on ne voit aucune donnée (comme aujourd'hui)
```

## 3. Les rôles (CPT-06)

- **Par défaut, toute adresse exige d'être connecté** (politique de repli d'ASP.NET). Une adresse oubliée est donc protégée d'office.
- **Adresses ouvertes sans connexion** : `/api/sante`, `/api/connexion`, `/api/invitation`, `/api/mot-de-passe-oublie` et `/api/nouveau-mot-de-passe`. Le formulaire public d'inscription (DEM-01) s'ajoutera à cette liste.
- **Une politique « Propriétaire »** : une adresse marquée ainsi répond **403 « Accès réservé au propriétaire »** à une secrétaire, sans aucune donnée.
- Aucune adresse réservée au propriétaire n'existe encore : les tests vérifient la politique sur une adresse de test.

## 4. L'invitation (CPT-02)

```
email « Rejoindre le Centre Les Jasmins sur SchoolHub »
  lien : <adresse de l'application>/invitation?compte=<id>&jeton=<jeton>

GET  /api/invitation?compte=…&jeton=…   le lien est-il valable ?
       → 200 : prénom et nom du centre (pour accueillir la personne)
       → 410 : « Ce lien a expiré : demandez au propriétaire une nouvelle invitation »

POST /api/invitation   { compte, jeton, motDePasse }
       → le mot de passe est enregistré, le compte devient Actif, la personne est connectée
```

- **Le lien est valable 7 jours.** Le jeton vient d'un fournisseur de jetons d'Identity réservé aux invitations, avec sa propre durée.
- **Le lien ne sert qu'une fois** : choisir son mot de passe change le tampon de sécurité du compte, ce qui rend le jeton caduc. « Renvoyer l'invitation » (CPT-04) s'appuiera sur le même mécanisme pour rendre l'ancien lien caduc.
- Un lien expiré, déjà utilisé, ou dont le compte n'est plus *invité* donne le même message « Ce lien a expiré… ».
- **Le mot de passe** a au moins 8 caractères, et c'est la seule règle : on désactive les exigences par défaut d'Identity (majuscule, chiffre, symbole), que la story ne demande pas. Sinon : 400 « Le mot de passe doit faire au moins 8 caractères ».

## 5. Le mot de passe oublié (CPT-03)

```
POST /api/mot-de-passe-oublie   { email }
       → toujours 200 « Si un compte existe pour cet email, un lien vient de partir »
       → l'email part seulement si le compte existe et est Actif
         lien : <adresse de l'application>/nouveau-mot-de-passe?compte=<id>&jeton=<jeton>

POST /api/nouveau-mot-de-passe   { compte, jeton, motDePasse }
       → le mot de passe est changé, le blocage éventuel est levé, la personne est connectée
       → lien expiré ou déjà utilisé : 410 « Ce lien a expiré : refaites une demande de mot de passe oublié »
```

- **Le lien est valable 1 heure** (le fournisseur de jetons de réinitialisation d'Identity, réglé à 1 heure) et **ne sert qu'une fois** (le tampon de sécurité change).
- **Pas de lien** pour un compte *désactivé* (la story le dit) ni pour un compte *invité*, qui a déjà son lien d'invitation.
- Après le changement, la personne est connectée directement, comme après l'invitation.

## 6. Les emails

- **Une petite interface** `IEnvoiEmail` : `EnvoyerAsync(destinataire, sujet, texte)`. Les emails sont en texte simple, en français.
- **En développement**, `EmailDansLaConsole` écrit l'email complet, lien compris, dans la console du serveur : on copie le lien pour le tester.
- **En production**, cet expéditeur n'est jamais utilisé, car il écrirait des liens de connexion dans les journaux du serveur. Tant que Brevo n'est pas branché, l'application refuse de démarrer en production, avec le message « Aucun envoi d'email configuré ».
- **L'adresse de l'application**, qui sert à fabriquer les liens, vient de la configuration (`Application:Adresse`) : `http://localhost:5087` en développement.

**Les deux emails :**

| Email | Sujet | Contenu |
|---|---|---|
| Invitation | « Rejoindre le Centre Les Jasmins sur SchoolHub » | Bonjour Hedi, le lien pour choisir son mot de passe, valable 7 jours. |
| Mot de passe oublié | « Changer votre mot de passe SchoolHub » | Le lien, valable 1 heure ; « Si vous n'avez rien demandé, ignorez cet email. » |

## 7. La commande de création d'un centre (CEN-01)

```bash
dotnet SchoolHub.dll creer-centre --nom "Centre Les Jasmins" --code les-jasmins --prenom Hedi --nom-proprietaire "Ben Salah" --email hedi@exemple.tn
```

En développement : `dotnet run --project src/SchoolHub -- creer-centre …`. En production, depuis la console SSH de l'App Service, qui a déjà la base et la configuration des emails.

- `Program.cs` lit le premier argument. Avec `creer-centre`, l'application exécute la commande puis s'arrête, avec le code de sortie 0 si tout va bien et 1 en cas d'erreur. Sans argument, elle démarre normalement.
- **Le tout ou rien** : dans une seule transaction, la commande crée le centre, puis le compte propriétaire (*invité*), fabrique le jeton d'invitation et envoie l'email. Elle ne valide la transaction qu'une fois l'email parti. Si l'envoi échoue, rien n'est créé, et on relance simplement la commande.
- **Les refus**, sans rien créer :
  - « Le code « les-jasmins » est déjà pris par un autre centre. »
  - « L'email hedi@exemple.tn est déjà utilisé sur SchoolHub. »
  - « Le code ne peut contenir que des lettres minuscules, des chiffres et des tirets. » (il va dans l'adresse du formulaire public)
  - « Il manque le nom du centre. », et de même pour chaque champ vide ;
  - « L'email hedi@exemple n'est pas valable. »
- **En cas de succès** : « Centre « Centre Les Jasmins » créé. Invitation envoyée à hedi@exemple.tn. »
- La commande ne règle rien d'autre : le propriétaire fait le reste (CEN-02).

## 8. L'organisation du code

```
src/SchoolHub/
├── Program.cs              démarre l'application, ou lance la commande creer-centre
└── Socle/
    ├── Centres/            existe déjà ; ajoute la commande CreerCentre
    ├── Comptes/            Compte, sa configuration EF, la connexion, l'invitation,
    │                       le mot de passe oublié, les rôles et le centre courant
    ├── Emails/             IEnvoiEmail et EmailDansLaConsole
    └── Donnees/            le contexte (devient IdentityUserContext) et la nouvelle migration
```

## 9. Les tests

Ils passent par de **vraies requêtes HTTP** (`WebApplicationFactory`), sur le PostgreSQL jetable dans Docker, comme les tests de séparation des centres.

**Les outils de test :**

- un **faux expéditeur** qui garde les emails envoyés, pour lire les liens et les suivre ;
- une **horloge de test** (`FakeTimeProvider`), passée au cookie et aux jetons, pour vérifier les délais (12 heures, 7 jours, 1 heure) sans attendre. Pour le blocage de 15 minutes, si Identity ne lit pas cette horloge, le test avance la date de fin de blocage en base ;
- un client HTTP qui garde les cookies, et qui parle en HTTPS, puisque le cookie est `Secure`.

**Un test par critère d'acceptation :**

| Story | Ce que les tests vérifient |
|---|---|
| CEN-01 | La commande crée le centre et le compte *invité*, et l'invitation part. Un code pris, un email utilisé ou un code mal formé sont refusés sans rien créer. Si l'email ne part pas, rien n'est créé. |
| CPT-01 | La bonne connexion, puis `GET /api/moi` (Amira, Centre Les Jasmins). Le même message pour un email inconnu et pour un mauvais mot de passe. Le blocage au 5ᵉ essai, puis le déblocage après 15 minutes. Le refus d'un compte désactivé. La session encore valable après 11 heures d'inactivité, et terminée après 12. Après la déconnexion, `GET /api/moi` répond 401. L'en-tête `no-store`. |
| CPT-02 | Le lien de l'email permet de choisir son mot de passe, puis de se connecter. Un mot de passe trop court est refusé. Un lien utilisé une fois, ou de plus de 7 jours, est refusé avec le bon message. |
| CPT-03 | La même réponse que le compte existe ou non. Aucun email pour un compte inconnu, désactivé ou invité. Le lien marche une fois, et pas après 1 heure. Il lève le blocage. |
| CPT-06 | Une secrétaire reçoit 403 « Accès réservé au propriétaire » sur l'adresse de test, et le propriétaire 200. Une adresse sans cookie répond 401. |
| Isolation | Connecté aux Jasmins, on ne voit jamais une donnée des Oliviers, même en demandant son identifiant. |

Les tests de `ModeleTests` continuent de vérifier le modèle et les migrations.

## Points d'attention

- **Le compte échappe au filtre automatique.** C'est voulu (section 1), mais chaque nouvelle lecture des comptes doit filtrer sur le centre courant, avec son test d'isolation.
- **Le message de blocage** révèle qu'un email existe après 5 essais faux. Le compromis est accepté.
- **Les vrais emails** demandent Brevo et le nom de domaine avant le pilote. D'ici là, l'application refuse de démarrer en production.
