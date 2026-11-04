# SchoolHub — frontend

L'application Angular 22 de SchoolHub. En production, le serveur .NET la sert à la même adresse que l'API ([ADR 0003](../docs/adr/0003-un-seul-deploiement-decoupe-en-modules.md)) : `dotnet publish` la construit et la range dans son `wwwroot`.

## Ce qu'il faut sur le poste

- Node 26 (la version LTS) : `node --version` doit afficher `v26.` suivi d'un numéro ;
- le serveur .NET et PostgreSQL, comme dans le [README du backend](../backend/README.md).

## Lancer l'application

Le serveur d'abord, depuis `backend/` :

```bash
dotnet run --project src/SchoolHub
```

Puis Angular, depuis `frontend/` :

```bash
npm ci
npm start
```

Ouvrir http://localhost:4200. La page se recharge à chaque modification. Angular transmet les appels `/api` au serveur (port 5087), grâce à `proxy.conf.json`. En développement, les liens des emails (invitation, mot de passe oublié) pointent aussi vers le port 4200.

## Lancer les tests

```bash
npm test
```

Les tests utilisent Vitest. Ils simulent les réponses du serveur : ni le serveur ni la base ne sont nécessaires. Pour un seul passage, sans surveiller les fichiers : `npm test -- --watch=false`.

## Construire

```bash
npm run build
```

La construction va dans `dist/browser/`. C'est inutile à la main : `dotnet publish` la lance.

## Organisation du code

```
src/app/
├── app.routes.ts       les adresses : les pages sans connexion, puis le cadre et ses pages
├── socle/              ce qui sert à toutes les pages
│   ├── session/        la personne connectée, les gardes des adresses, l'intercepteur des 401
│   ├── menu.ts         la liste du menu
│   ├── icones.ts       les icônes de la maquette (Lucide), et icone.ts pour les afficher
│   ├── erreurs.ts      le message à afficher pour une erreur de l'API
│   └── …               dates, personnes, lien de l'email, champ de mot de passe
├── cadre/              l'en-tête, le menu, les onglets du téléphone, la page « Plus »,
│                       et la carte des pages sans connexion
├── pages/              une page par dossier : connexion, invitation, mots de passe,
│                       aujourd'hui, professeurs, groupes (création et fiche), élèves
│                       (liste, création, fiche), inscriptions (depuis l'élève ou le
│                       groupe, et la confirmation WhatsApp), comptes,
│                       paramètres du centre, bientôt, accès réservé, introuvable
└── test/               les outils des tests
```

## Les règles

- **Une seule liste de menu** (`socle/menu.ts`). Le menu de gauche, la page « Plus » et les adresses en sont construits. Une story qui construit une page l'ajoute à `PAGES`, dans `app.routes.ts` : elle remplace la page « bientôt » de son entrée.
- **Aucun appel au serveur en tâche de fond** : ni minuteur, ni vérification régulière. Chaque appel renouvelle la session, et les 12 heures sans activité ne finiraient jamais.
- **La personne connectée reste en mémoire** (`Session`), jamais dans le stockage du navigateur.
- **Le style vient de la maquette validée** (`docs/maquette/index.html`), sans bibliothèque de composants : les couleurs et les éléments communs sont dans `src/styles.css`.
- **Tout est en français**, les noms dans le code aussi (sans accents dans les identifiants).
- **Pas de service worker** : l'application s'installe sur l'écran d'accueil grâce à `public/manifest.webmanifest`, mais rien n'est gardé en cache. Les icônes « SH » se refont avec `outils/creer-icones.ps1`.
