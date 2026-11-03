# Application Angular : le cadre et les pages de connexion

Conception établie le 03/11/2026 avec Lassad et Taha. C'est le troisième morceau du [socle](../../user-stories/travaux-techniques.md#socle-de-lapplication), après la séparation des centres et les [comptes et la connexion](2026-11-02-comptes-et-connexion-design.md), qui a laissé les écrans à cette étape.

## But

Une personne d'un centre ouvre SchoolHub dans son navigateur, se connecte et arrive dans le cadre de l'application. Elle y voit son centre, la date du jour et un menu qui dépend de son rôle. Les liens des emails (invitation, mot de passe oublié) ouvrent leurs pages.

## Périmètre

**Dans cette étape :**

- le projet Angular 22.2, dans `frontend/`, servi par .NET à la même adresse ([ADR 0003](../../adr/0003-un-seul-deploiement-decoupe-en-modules.md)) et installable sur l'écran d'accueil ;
- les écrans de [CPT-01](../../user-stories/01-comptes-acces.md#cpt-01--se-connecter-et-se-déconnecter), [CPT-02](../../user-stories/01-comptes-acces.md#cpt-02--choisir-son-mot-de-passe-depuis-le-lien-dinvitation) et [CPT-03](../../user-stories/01-comptes-acces.md#cpt-03--retrouver-laccès-par--mot-de-passe-oublié-) ;
- le menu selon le rôle et la page « Accès réservé au propriétaire » ([CPT-06](../../user-stories/01-comptes-acces.md#cpt-06--cacher-à-la-secrétaire-ce-qui-est-réservé-au-propriétaire)) ;
- le cadre : en-tête, menu, onglets sur téléphone, pages pas encore faites, page introuvable ;
- côté .NET : servir Angular, l'en-tête `Referrer-Policy`, et une adresse `/api` inconnue qui répond en `{ "message" }`.

**Plus tard :**

- la recherche d'un élève dans l'en-tête (ELV-01) et les séances d'« Aujourd'hui » (SEA-01) ;
- la page « Comptes » (CPT-04 et CPT-05), après le Journal ;
- les points serveur « avant le pilote » ([travaux techniques](../../user-stories/travaux-techniques.md#à-faire-avant-le-pilote)) ;
- le pipeline GitHub Actions et le déploiement sur Azure ;
- des tests qui pilotent un vrai navigateur (Playwright).

## Décisions

| Question | Décision |
|---|---|
| Que montre le menu pour les pages pas encore faites ? | Tout le menu de la maquette. Une page pas encore faite affiche « Cette page arrive bientôt ». Ainsi, CPT-06 se vérifie dès maintenant. |
| Quelle forme pour la page de connexion ? | Une carte au centre, la même sur téléphone et sur ordinateur. Les trois pages de mot de passe prennent la même forme. |
| Comment construire l'apparence ? | Avec le style de la [maquette validée](../../maquette/index.html) : ses couleurs, ses polices et ses règles CSS, sans bibliothèque de composants (ni Angular Material, ni Tailwind). |
| Comment travailler en développement ? | Angular tourne à part (port 4200) et transmet les appels `/api` au serveur .NET (port 5087). |
| Quels tests ? | Vitest pour Angular, les tests xUnit existants pour .NET, et une vérification à la main dans le navigateur. Pas de Playwright pour l'instant : la vérification à la main couvre le bouton « retour », et Lassad et Taha rejouent tous les critères avant la livraison. |

## 1. Les pages sans connexion

Elles prennent toutes la forme d'une carte au centre de l'écran, sous le logo SchoolHub.

| Adresse | Ce qu'on y voit | Appels à l'API | Ensuite |
|---|---|---|---|
| `/connexion` | Email, mot de passe, « Se connecter », lien « Mot de passe oublié » | `POST /api/connexion` | « Aujourd'hui », ou la page demandée avant la connexion |
| `/invitation?compte=…&jeton=…` | « Bonjour Amira, bienvenue au Centre Les Jasmins », puis le mot de passe à choisir | `GET /api/invitation` à l'ouverture, `POST /api/invitation` à l'envoi | Connectée, sur « Aujourd'hui » |
| `/mot-de-passe-oublie` | Email, « Recevoir un lien », puis le message « Si un compte existe pour cet email, un lien vient de partir » | `POST /api/mot-de-passe-oublie` | Lien « Retour à la connexion » |
| `/nouveau-mot-de-passe?compte=…&jeton=…` | Le nouveau mot de passe à choisir | `POST /api/nouveau-mot-de-passe` | Connectée, sur « Aujourd'hui » |

Les adresses des deux liens sont celles que le serveur écrit déjà dans les emails. Le choix du mot de passe et la connexion répondent tous deux avec la personne connectée (`Moi` : prénom, nom, rôle, centre) : l'application la garde sans redemander `/api/moi`.

**Les champs**

- **Un seul champ de mot de passe**, avec un bouton « Afficher » (l'œil), plutôt que de le taper deux fois.
- **Avant d'envoyer**, la page vérifie les champs vides (« Saisissez votre email et votre mot de passe ») et, sur les pages qui choisissent un mot de passe, sa longueur, avec le texte du serveur : « Le mot de passe doit faire au moins 8 caractères ». La règle reste vérifiée par le serveur.
- **Le bouton se bloque pendant l'envoi** (« Connexion… », « Enregistrement… ») : un double clic n'envoie pas deux fois.

**Les messages**

| Réponse | Ce que la page affiche |
|---|---|
| un refus avec `{ "message" }` | le message du serveur, tel quel : « Email ou mot de passe incorrect », « Trop d'essais : réessayez dans 15 minutes », « Ce compte est désactivé… », « Ce lien a expiré… » |
| pas de réponse (plus d'Internet) | « Pas de connexion à Internet : réessayez. » |
| toute autre erreur (un 500 vide, par exemple) | « Une erreur est survenue : réessayez. » |

Ces trois cas sont traités à un seul endroit, réutilisé par toutes les pages.

**Les liens des emails**

- **Un lien d'invitation expiré** affiche son message dès l'ouverture, sans formulaire : `GET /api/invitation` le vérifie.
- **Un lien de mot de passe oublié expiré** ne se voit qu'à l'envoi : le serveur n'a pas d'adresse pour le vérifier avant. Le lien ne vaut qu'1 heure, ce cas est rare : rien n'est ajouté au serveur. Le message remplace alors le formulaire.
- **Le lien disparaît de la barre d'adresse** dès que la page l'a lu : la page remplace son adresse par `/invitation` (ou `/nouveau-mot-de-passe`) et garde le compte et le jeton dans l'état de navigation, qui survit à un rechargement. Le lien ne revient donc pas avec le bouton « retour », et il n'est transmis à aucun autre site. L'historique général du navigateur peut tout de même garder l'adresse d'origine : on n'y peut rien depuis la page, mais le lien ne sert qu'une fois et expire (7 jours pour l'invitation, 1 heure pour le mot de passe oublié).

**Une personne déjà connectée** qui ouvre `/connexion` va directement sur « Aujourd'hui ». Les pages d'invitation et de mot de passe restent ouvertes : elles connectent la personne du lien.

## 2. Le cadre et le menu

### Le cadre

Il reprend celui de la maquette (`.shell`, `.top`, `.rail`, `.tabbar`) :

- **l'en-tête** : le logo, SchoolHub et le nom du centre, la date du jour (« Mardi 3 novembre 2026 »), les initiales, le nom et le rôle de la personne (« Propriétaire », « Secrétaire ») ;
- **le menu**, à gauche sur ordinateur, avec « Se déconnecter » en bas ;
- **sur téléphone**, les 4 onglets de la maquette en bas : Aujourd'hui, Guichet, Caisse, Plus. « Plus » est une page qui liste tout le menu de la personne, puis « Se déconnecter ».

La recherche d'un élève reste cachée jusqu'à ELV-01. Le mode sombre suit le réglage du téléphone ou de l'ordinateur, comme dans la maquette. L'onglet du navigateur porte le nom de la page : « Aujourd'hui · SchoolHub ».

L'application est réglée en français (`LOCALE_ID` `fr`) : les dates s'écrivent donc à la française, ici comme dans les écrans suivants (15/01/2028).

### Le menu

**Une seule liste** décrit le menu : libellé, adresse, icône, et si l'entrée est réservée au propriétaire. Le menu de gauche, la page « Plus » et les adresses de l'application sont construits à partir d'elle. Une story qui arrive remplace la page « bientôt » de son entrée par la vraie page.

| Entrée | Adresse | Réservée |
|---|---|---|
| Aujourd'hui | `/aujourdhui` (et `/`) | |
| Professeurs | `/professeurs` | |
| Élèves | `/eleves` | |
| Caisse | `/caisse` | |
| Bibliothèque | `/bibliotheque` | |
| Demandes d'inscription | `/demandes` | |
| Achats | `/achats` | |
| Parts des professeurs | `/parts` | oui |
| Tableau de bord | `/tableau-de-bord` | oui |
| Ressources humaines | `/ressources-humaines` | oui |
| Journal | `/journal` | oui |
| Comptes | `/comptes` | oui |
| Paramètres du centre | `/parametres` | oui |

Les onglets du téléphone ajoutent `/guichet` (une page « bientôt » jusqu'à ELV-01) et `/plus`. « Comptes » et « Paramètres du centre » ne sont pas dans la maquette : ils s'ajoutent à la partie Propriétaire, puisque CPT-06 les cite. Les icônes sont celles de la maquette (Lucide), copiées dans l'application avec deux nouvelles pour ces entrées.

### Les pages

| Page | Ce qu'elle affiche |
|---|---|
| Aujourd'hui | « Bonjour Amira » et « Les séances du jour s'afficheront ici. » |
| une page pas encore faite | son titre, puis « Cette page arrive bientôt » |
| une page réservée, ouverte par une secrétaire | « Accès réservé au propriétaire », à la place de la page, à la même adresse |
| une adresse inconnue | « Cette page n'existe pas », avec un lien vers « Aujourd'hui » |

**Pour une secrétaire, la page réservée n'est jamais créée** : l'adresse mène à la page « Accès réservé au propriétaire ». La vraie page ne demande donc rien au serveur, qui refuserait de toute façon (403). Si un jour une page reçoit quand même ce refus, elle affiche le même message.

## 3. La session

**Ce que l'application sait** : la personne connectée (prénom, nom, rôle, centre), ou personne. Elle le garde en mémoire, jamais dans le stockage du navigateur.

- **À l'ouverture d'une page du cadre**, si l'application ne sait pas encore qui est connecté, elle le demande une fois au serveur (`GET /api/moi`). Si quelqu'un est connecté, elle affiche la page. Sinon (401), elle va sur `/connexion?retour=<la page demandée>`.
- **Si la session tombe pendant le travail** (12 heures sans activité, compte désactivé, mot de passe changé ailleurs), le serveur répond 401 à la prochaine action. Un seul intercepteur, pour toute l'application, oublie alors la personne. Il l'envoie sur `/connexion?retour=…` avec le message « Votre session a expiré : reconnectez-vous ». Trois cas ne passent pas par cette règle :
  - la connexion et les pages de mot de passe, où un 401 est un refus normal ;
  - `GET /api/moi`, dont le 401 est traité par la garde, sans message : à la première ouverture, la connexion s'affiche sans dire qu'une session a expiré ;
  - la déconnexion (ci-dessous).
- **Le retour après la connexion** n'accepte qu'une adresse de l'application (qui commence par un seul `/`). Un lien piégé ne peut donc pas envoyer vers un autre site.
- **« Se déconnecter »** appelle `POST /api/deconnexion`, oublie la personne et affiche la connexion. Un 401 de la déconnexion veut dire que la session était déjà finie : le résultat est le même, sans message. Sans Internet, la page affiche « Pas de connexion à Internet : réessayez. » et la personne reste connectée.
- **Le bouton « retour » après la déconnexion** ramène à une page du cadre : la personne est oubliée, l'application redemande `/api/moi`, reçoit 401 et affiche la connexion. Les réponses de l'API ne sont jamais gardées en cache (`no-store`, déjà en place).
- **Rien n'appelle le serveur en tâche de fond** : aucun minuteur, aucune vérification régulière. Chaque appel vient d'une action de la personne. Sinon, chaque appel renouvellerait la session, et les 12 heures sans activité ne finiraient jamais.

Le cookie est `SameSite=Strict` : un autre site ne peut pas envoyer de requête avec la session. Il n'y a donc pas de jeton anti-falsification à ajouter.

## 4. Comment .NET sert l'application

### En développement

- `dotnet run --project src/SchoolHub` lance le serveur, comme aujourd'hui, sur le port 5087.
- `npm start`, dans `frontend/`, lance Angular sur http://localhost:4200. Un fichier `proxy.conf.json` transmet `/api` au serveur. On travaille sur cette adresse, qui se recharge à chaque modification.
- `Application:Adresse` passe à `http://localhost:4200` dans `appsettings.Development.json` : les liens des emails écrits dans la console ouvrent donc les pages Angular.
- Le cookie est `Secure`. Les navigateurs l'acceptent quand même sur `http://localhost`. C'est à vérifier dès la première connexion.

### En production

- **`dotnet publish` construit aussi Angular** : `npm ci`, puis `npm run build`, dans `frontend/`. Il range ensuite les fichiers produits dans le `wwwroot` publié. `dotnet build` et `dotnet test` ne lancent pas Angular : les tests du serveur n'ont pas besoin de Node.
- **Les fichiers d'Angular sont publics** : ils ne contiennent aucune donnée. Le serveur les envoie avant de regarder la session (`UtiliserLaSession`). Sinon, la page de connexion elle-même répondrait 401.
- **Toute adresse qui n'est ni `/api` ni un fichier renvoie `index.html`**, sans connexion : c'est Angular qui choisit la page, et les liens des emails marchent. `index.html` n'est jamais gardé en cache, pour qu'une mise à jour du soir soit vue dès le lendemain. Les autres fichiers ont un nom qui change à chaque version : ils peuvent rester en cache.
- **Une adresse `/api` inconnue** répond 404 `{ "message": "Adresse inconnue" }`, et pas la page Angular.
- **Chaque réponse porte `Referrer-Policy: no-referrer`** : aucune page ne transmet son adresse à un autre site, en particulier les pages qui contiennent un lien d'email.

### L'installation sur l'écran d'accueil

- Un fichier `manifest.webmanifest` : nom « SchoolHub », ouverture sur `/aujourdhui`, sans barre d'adresse, couleur `#1B5DA4`. Les icônes « SH » en blanc sur bleu, comme le logo de la maquette, viennent en 192 et 512 pixels, et en version adaptée à Android.
- **Pas de service worker** : rien n'est gardé en cache, et l'application ne fonctionne pas hors connexion (exigences communes). Chrome ne demande plus de service worker pour proposer l'installation : c'est à vérifier dans Chrome (outils de développement, onglet « Application »), puis sur un vrai téléphone au premier déploiement en HTTPS. Si un téléphone refuse, on ajoutera un service worker minimal, qui ne garde rien en cache.

### Les polices

IBM Plex Sans, IBM Plex Mono et Readex Pro, comme dans la maquette. Elles sont installées dans l'application (paquets `@fontsource`), au lieu d'être chargées chez Google : il n'y a pas d'appel à un site extérieur.

## 5. L'organisation du code

```
frontend/
├── package.json, angular.json, proxy.conf.json
├── public/                     manifest.webmanifest et les icônes
└── src/
    ├── index.html, main.ts
    ├── styles.css              les couleurs, les polices et les éléments communs de la maquette
    └── app/
        ├── app.config.ts       le français, l'intercepteur, les adresses
        ├── app.routes.ts       les adresses, construites à partir du menu
        ├── socle/
        │   ├── session/        la personne connectée, les gardes, l'intercepteur des 401
        │   ├── erreurs.ts      le message à afficher pour une réponse d'erreur
        │   ├── menu.ts         la liste du menu
        │   └── icones.ts       les icônes de la maquette
        ├── cadre/              l'en-tête, le menu, les onglets, la page « Plus »
        └── pages/
            ├── connexion/, invitation/, mot-de-passe-oublie/, nouveau-mot-de-passe/
            ├── aujourdhui/
            └── bientot/, acces-reserve/, introuvable/
```

Les noms sont en français, comme dans le serveur. Les composants sont autonomes (`standalone`) et lisent leur état dans des signaux, ce que fait Angular 22 par défaut.

Côté serveur :

```
backend/src/SchoolHub/
├── Program.cs                  sert les pages Angular, avant la session
├── Socle/Pages/                les fichiers d'Angular, la page par défaut, l'adresse /api inconnue,
│                               l'en-tête Referrer-Policy
├── SchoolHub.csproj            construit Angular à la publication
└── appsettings.Development.json   Application:Adresse sur le port 4200
```

`frontend/README.md` explique comment lancer Angular. `backend/README.md` renvoie vers lui. Un `.gitignore` dans `frontend/` écarte `node_modules/` et `dist/`.

## 6. Les tests

### Côté Angular (`npm test`, Vitest)

Les tests simulent les réponses du serveur (`HttpTestingController`) :

| Sujet | Ce que les tests vérifient |
|---|---|
| CPT-01 | Un bon mot de passe mène à « Aujourd'hui », ou à la page de `retour`. Un refus affiche le message du serveur. Le bouton se bloque pendant l'envoi. Les champs vides ne partent pas. Une personne déjà connectée passe directement à « Aujourd'hui ». |
| CPT-02 | Le lien valable affiche « Bonjour Amira, bienvenue au Centre Les Jasmins ». Un lien expiré affiche son message, sans formulaire. Un mot de passe trop court ne part pas. Le bon mot de passe mène à « Aujourd'hui ». Le lien disparaît de la barre d'adresse. |
| CPT-03 | La demande affiche toujours le même message. Le nouveau mot de passe mène à « Aujourd'hui ». Un lien expiré affiche son message à l'envoi. |
| CPT-06 | Une secrétaire voit le menu sans la partie Propriétaire, et lit « Accès réservé au propriétaire » sur `/journal`. Le propriétaire voit tout le menu. |
| Session | Sans session, une page du cadre mène à `/connexion?retour=…`. Un 401 pendant le travail ramène à la connexion avec « Votre session a expiré », puis à la page de départ. Un `retour` vers un autre site est ignoré. « Se déconnecter » marche aussi quand le serveur répond 401. |
| Erreurs | Pas d'Internet, un 500 vide et un refus avec message donnent chacun le bon texte. |

### Côté .NET (`dotnet test`)

Avec un faux `index.html`, dans un dossier de test :

- `/invitation?compte=…&jeton=…` et `/aujourdhui` renvoient `index.html` sans être connecté, avec `Cache-Control: no-cache` ;
- un fichier d'Angular est servi sans session ;
- `/api/inconnue` répond 404 `{ "message": "Adresse inconnue" }`, et `/api/moi` sans session répond toujours 401 ;
- chaque réponse porte `Referrer-Policy: no-referrer`.

### À la main, dans le navigateur

Avec le vrai serveur et PostgreSQL dans Docker :

1. créer un centre avec `creer-centre`, et ouvrir le lien d'invitation écrit dans la console ;
2. choisir le mot de passe, et arriver sur « Aujourd'hui » ;
3. se déconnecter, puis appuyer sur « retour » : rien ne s'affiche que la connexion ;
4. faire le mot de passe oublié, jusqu'au nouveau mot de passe ;
5. tout cela sur une largeur de téléphone (375 pixels) et d'ordinateur, en mode clair et en mode sombre ;
6. vérifier dans Chrome que l'application est installable ;
7. vérifier `dotnet publish` : l'application publiée sert les pages Angular sur le port 5087.

## Points d'attention

- **Node 26 est à installer sur le poste** : il a Node 12, alors qu'Angular 22.2 demande au moins Node 22.22, 24.15 ou 26. Node 26 est la version LTS depuis le 28/10/2026. L'Angular CLI 9 installé globalement n'est pas utilisé : les commandes passent par `npm`.
- **Le cookie `Secure` derrière le proxy de développement** : les navigateurs l'acceptent sur `localhost`. Si ce n'était pas le cas, le profil `https` du serveur (port 7058) servirait de cible au proxy.
- **Sans service worker**, l'installation sur l'écran d'accueil reste à confirmer sur un vrai téléphone.
- **`Referrer-Policy: no-referrer` vaut pour toutes les pages**, y compris les futurs liens `wa.me` : WhatsApp n'en a pas besoin.
