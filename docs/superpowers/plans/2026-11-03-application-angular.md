# Application Angular : plan de travail

> **Pour les agents :** sous-compétence OBLIGATOIRE : utiliser superpowers:subagent-driven-development (recommandé) ou superpowers:executing-plans pour exécuter ce plan tâche par tâche. Les étapes utilisent des cases à cocher (`- [ ]`) pour le suivi.

**But :** une personne d'un centre ouvre SchoolHub, se connecte, et arrive dans le cadre de l'application, avec son centre, la date du jour et un menu selon son rôle. Les liens des emails ouvrent leurs pages.

**Architecture :** une application Angular 22.2 dans `frontend/`, avec des composants autonomes, sans zone.js, sous Vitest. Elle reprend le CSS de la maquette, sans bibliothèque de composants. Un service `Session` garde la personne connectée en mémoire. Deux gardes de navigation et un intercepteur des 401 s'appuient sur lui. Le menu est une seule liste, qui construit les adresses. En développement, `ng serve` transmet `/api` au serveur .NET. En production, `dotnet publish` construit Angular et le range dans `wwwroot`, servi sans connexion avant la session.

**Technologies :** Angular 22.2.5, TypeScript strict, Vitest et jsdom (installés par `ng new`), `@fontsource` 5.3.0, icônes `lucide-static` 0.460.0 copiées. Côté serveur : .NET 10, `UseStaticFiles`, `MapFallbackToFile`, et les tests xUnit existants.

**Conception :** [docs/superpowers/specs/2026-11-03-application-angular-design.md](../specs/2026-11-03-application-angular-design.md). Le plan s'appuie dessus : lire les deux.

## Contraintes globales

- **Node 26** au moins (LTS 26.13.0) ; Angular CLI `@angular/cli@22.2.5`, jamais le CLI 9 global du poste : passer par `npx` ou `npm run`.
- **Le style de nom de fichiers 2025** : `connexion.ts`, `connexion.html`, `connexion.css`, `connexion.spec.ts`. Les classes n'ont pas le suffixe `Component` (`export class Connexion`). Le préfixe des sélecteurs est `sh` (`sh-icone`).
- **Les noms sont en français**, sans accents dans les identifiants, comme dans le serveur.
- **Aucune bibliothèque de composants** (ni Angular Material, ni Tailwind), **aucun service worker**, **aucun appel au serveur en tâche de fond** (ni `setInterval`, ni `timer`, ni nouvelle tentative automatique).
- **La personne connectée reste en mémoire**, jamais dans `localStorage`, `sessionStorage` ou un cookie lisible.
- **Les couleurs** viennent de `docs/maquette/index.html`, lignes 10 à 33 : le bloc `:root`, puis le bloc `@media (prefers-color-scheme: dark)` sans le sélecteur `:not([data-theme="light"])`. Le bloc `:root[data-theme="dark"]` n'est pas repris, car l'application n'a pas de bouton de thème.
- **Les règles CSS** sont copiées de la maquette sous les mêmes noms de classes (`.shell`, `.top`, `.brand`, `.logo`, `.bt`, `.who`, `.user`, `.av`, `.un`, `.rail`, `.railh`, `.nav`, `.main`, `.tabbar`, `.tab`, `.pagehead`, `.h1`, `.sub`, `.btn`, `.lnk`, `.card`, `.note`, `.f`). Les règles `@container app (max-width: 760px)` deviennent `@media (max-width: 760px)`, puisque le cadre occupe toute la fenêtre.
- **Les textes exacts**, à copier tels quels :
  - « Pas de connexion à Internet : réessayez. »
  - « Une erreur est survenue : réessayez. »
  - « Saisissez votre email et votre mot de passe »
  - « Saisissez votre email »
  - « Le mot de passe doit faire au moins 8 caractères » (le même texte que le serveur)
  - « Votre session a expiré : reconnectez-vous »
  - « Bonjour {prénom}, bienvenue au {centre} » (« Bonjour Amira, bienvenue au Centre Les Jasmins »)
  - « Bonjour {prénom} » et « Les séances du jour s'afficheront ici. »
  - « Cette page arrive bientôt »
  - « Accès réservé au propriétaire »
  - « Cette page n'existe pas »
  - « Adresse inconnue » (le 404 de l'API)
  - Les boutons : « Se connecter » / « Connexion… », « Choisir ce mot de passe » / « Enregistrement… », « Recevoir un lien » / « Envoi… », « Afficher » / « Masquer », « Se déconnecter ». Les liens : « Mot de passe oublié », « Retour à la connexion », « Refaire une demande », « Aujourd'hui ».
- **Les titres d'onglet** prennent la forme « {Page} · SchoolHub » : « Aujourd'hui · SchoolHub », « Caisse · SchoolHub », « Connexion · SchoolHub », « Choisir son mot de passe · SchoolHub », « Mot de passe oublié · SchoolHub », « Nouveau mot de passe · SchoolHub », « Accès réservé · SchoolHub », « Page introuvable · SchoolHub ».
- **L'API telle qu'elle existe** (JSON en camelCase) :
  - `POST /api/connexion` `{ email, motDePasse }` → 200 `Moi` ou 401/403 `{ message }` ;
  - `GET /api/moi` → 200 `Moi` ou 401 ;
  - `POST /api/deconnexion` → 204 ;
  - `GET /api/invitation?compte=…&jeton=…` → 200 `{ prenom, centre }` ou 410 `{ message }` ;
  - `POST /api/invitation` `{ compte, jeton, motDePasse }` → 200 `Moi`, 400 ou 410 `{ message }` ;
  - `POST /api/mot-de-passe-oublie` `{ email }` → 200 `{ message }` ;
  - `POST /api/nouveau-mot-de-passe` `{ compte, jeton, motDePasse }` → 200 `Moi`, 400 ou 410 `{ message }`.

  `Moi` vaut `{ prenom, nom, role: "Proprietaire" | "Secretaire", centre }`.
- **Les commandes** : `npm …` se lance depuis `frontend/`, `dotnet …` depuis `backend/`. Les tests Angular se lancent par `npm test -- --watch=false`. Les tests .NET demandent Docker démarré.
- **Les commits** : git n'a pas d'identité sur ce poste. Demander l'auteur avant le premier commit, puis `git -c user.name="…" -c user.email="…" commit`, sans toucher à la configuration. Les messages sont en français et finissent par `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Points de vigilance (relecture)

Ce que la conception ne dit pas, mais qui fâcherait une vraie utilisatrice :

1. **Un email fait seulement d'espaces**, ou entouré d'espaces, à la connexion et au mot de passe oublié. Un email vide d'espaces compte comme vide (message, rien ne part), et un email entouré d'espaces part sans eux. Tests en tâches 6 et 8.
2. **Le propriétaire qui ouvre directement `/journal`**, en première page après un rechargement. La garde du rôle (`canMatch`) passe avant celle du cadre (`canActivate`), à un moment où la session n'est pas encore chargée. Il doit voir sa page, et pas « Accès réservé au propriétaire ». Tests en tâches 3 et 4.
3. **Un double envoi** (double clic, ou Entrée appuyée deux fois) ne part qu'une fois. Tests en tâches 6 et 7.
4. **Le rechargement d'une page de lien** après que le lien a quitté la barre d'adresse : le formulaire marche encore, avec le même compte et le même jeton. Tests en tâches 7 et 8.
5. **Un jeton qui contient `+`, `/` ou `=`**, comme tous les jetons d'Identity, arrive intact au serveur dans `GET /api/invitation`. Si `+` partait tel quel, le serveur le lirait comme une espace, et chaque lien d'invitation répondrait « Ce lien a expiré ». Test en tâche 7.

---

## Avant de commencer (Lassad et Taha)

- [ ] **Installer Node 26 LTS** sur le poste, à la place de Node 12, depuis https://nodejs.org (fichier `.msi` « LTS ») ou avec `winget install OpenJS.NodeJS.LTS`. Rouvrir le terminal, puis vérifier que `node --version` affiche `v26.` suivi d'un numéro.
- [ ] **Démarrer Docker Desktop.**

---

## Fichiers

```
frontend/                                        créé par ng new (tâche 1)
├── angular.json                                 outputPath "dist", proxyConfig, polices
├── package.json, package-lock.json
├── proxy.conf.json                              /api → http://localhost:5087
├── README.md                                    tâche 10
├── outils/creer-icones.ps1                      tâche 10 : fabrique les icônes PNG
├── public/manifest.webmanifest, public/icones/  tâche 10
└── src/
    ├── index.html                               lang="fr", manifeste, icônes
    ├── styles.css                               couleurs et règles de la maquette
    └── app/
        ├── app.ts, app.config.ts, app.routes.ts (+ .spec.ts)
        ├── socle/
        │   ├── francais.ts                      enregistre le français
        │   ├── dates.ts, personnes.ts, erreurs.ts, lien-de-l-email.ts (+ .spec.ts)
        │   ├── menu.ts, icones.ts, icone.ts (+ .spec.ts)
        │   ├── champ-mot-de-passe.ts (+ .html, .spec.ts)
        │   └── session/ moi.ts, retour.ts, session.ts, gardes.ts, intercepteur.ts (+ .spec.ts)
        ├── cadre/ cadre.ts/.html/.css, plus.ts, bouton-de-deconnexion.ts, carte-publique.ts (+ .spec.ts)
        ├── pages/ aujourdhui, bientot, acces-reserve, introuvable, connexion, invitation,
        │          mot-de-passe-oublie, nouveau-mot-de-passe (un dossier chacune)
        └── test/ page-de-test.ts, personnes.ts       outils des tests
backend/src/SchoolHub/
├── Program.cs                                   modifié (tâche 9)
├── SchoolHub.csproj                             modifié : construit Angular à la publication
├── appsettings.Development.json                 modifié : Application:Adresse sur 4200
└── Socle/Pages/PagesDeLApplication.cs           créé
backend/tests/SchoolHub.Tests/
├── SchoolHub.Tests.csproj                       modifié : copie wwwroot-de-test
├── Outils/ApplicationDeTest.cs                  modifié : UseWebRoot
├── wwwroot-de-test/                             créé : index.html, main-TEST1234.js, manifest.webmanifest
├── Socle/PagesDeLApplicationTests.cs            créé
├── Comptes/InvitationTests.cs                   modifié : 5087 → 4200 (ligne 54)
└── Comptes/MotDePasseOublieTests.cs             modifié : 5087 → 4200 (ligne 76)
backend/README.md                                modifié (tâche 10)
```

**Les outils des tests Angular** (créés en tâche 3, utilisés ensuite) :
- `src/app/test/personnes.ts` : `export const AMIRA: Moi = { prenom: 'Amira', nom: 'Jlassi', role: 'Secretaire', centre: 'Centre Les Jasmins' }` et `export const HEDI: Moi = { prenom: 'Hedi', nom: 'Ben Salah', role: 'Proprietaire', centre: 'Centre Les Jasmins' }`.
- `src/app/test/page-de-test.ts` : `@Component({ template: '' }) export class PageDeTest {}`, la destination neutre des tables d'adresses des tests.

**La forme des tests de pages** : `TestBed.configureTestingModule({ providers: [provideRouter(<table d'adresses du test>, withComponentInputBinding()), provideHttpClient(withInterceptors([intercepteurDeSession])), provideHttpClientTesting()] })`, puis `RouterTestingHarness.create()` et `harness.navigateByUrl(…)`. Le serveur est simulé par `HttpTestingController` (`expectOne`, `flush`, `verify` en fin de test). Ensuite, `await harness.fixture.whenStable()` après chaque action, puisque l'application n'a pas de zone.js.

---

### Tâche 1 : le projet Angular, en français, avec le style de la maquette

**Fichiers :**
- Créer : `frontend/` (par `ng new`), `frontend/proxy.conf.json`, `frontend/src/app/socle/francais.ts`
- Modifier : `frontend/angular.json`, `frontend/package.json`, `frontend/src/index.html`, `frontend/src/styles.css`, `frontend/src/app/app.ts`, `frontend/src/app/app.config.ts`
- Supprimer : `frontend/src/app/app.html`, `frontend/src/app/app.css` (la page d'accueil d'Angular)
- Test : `frontend/src/app/app.spec.ts`

**Interfaces :**
- Produit :
  - `socle/francais.ts` : appelle `registerLocaleData(localeFr)` au chargement, et exporte `export const FRANCAIS = 'fr';`
  - `app.config.ts` : `appConfig: ApplicationConfig`, avec `{ provide: LOCALE_ID, useValue: FRANCAIS }`, `provideRouter(routes, withComponentInputBinding())` et `provideHttpClient()`. La tâche 3 y ajoutera l'intercepteur.
  - `App` (`sh-root`), dont le gabarit vaut `<router-outlet />`.
  - `app.routes.ts` : `export const routes: Routes = [];`, rempli à partir de la tâche 4.

- [ ] **Étape 1 : vérifier Node**

Lancer : `node --version`
Résultat attendu : `v26.` suivi d'un numéro. Sinon, s'arrêter : voir « Avant de commencer ».

- [ ] **Étape 2 : créer le projet**

Depuis `schoolhub/`, avec `NG_CLI_ANALYTICS=false` :

```bash
npx --yes @angular/cli@22.2.5 new schoolhub --directory frontend --style css --ssr false --routing true --zoneless true --test-runner vitest --prefix sh --skip-git true --ai-config none --package-manager npm --interactive false
```

Résultat attendu : `frontend/package.json`, `frontend/src/app/app.ts` et `frontend/.gitignore` existent, et `npm test -- --watch=false` passe sur le test généré.

- [ ] **Étape 3 : écrire le test qui échoue**

Remplacer le contenu de `app.spec.ts` :

```ts
it('règle l\'application en français', () => {
  TestBed.configureTestingModule({ providers: appConfig.providers });
  expect(TestBed.inject(LOCALE_ID)).toBe('fr');
  expect(formatDate(new Date(2026, 10, 3), 'EEEE d MMMM y', 'fr')).toBe('mardi 3 novembre 2026');
});

it('n\'affiche que les pages', async () => {
  // App créée avec appConfig.providers : le seul élément rendu est <router-outlet>
  expect(fixture.nativeElement.children.length).toBe(1);
  expect(fixture.nativeElement.querySelector('router-outlet')).not.toBeNull();
});
```

- [ ] **Étape 4 : lancer le test et vérifier qu'il échoue**

Lancer : `npm test -- --watch=false`
Résultat attendu : ÉCHEC (la locale vaut `en-US`, et la page d'accueil d'Angular est rendue).

- [ ] **Étape 5 : écrire le code**

- `francais.ts` et `app.config.ts`, avec les interfaces ci-dessus. `App` n'a plus que `<router-outlet />` : supprimer `app.html` et `app.css`.
- `npm install @fontsource/ibm-plex-sans@5.3.0 @fontsource/ibm-plex-mono@5.3.0 @fontsource/readex-pro@5.3.0`. Dans `angular.json`, le tableau `styles` du build (et celui du test, si sa configuration en a un) liste, avant `src/styles.css` : `@fontsource/ibm-plex-sans/400.css`, `/500.css`, `/600.css`, `@fontsource/ibm-plex-mono/400.css`, `/500.css`, `@fontsource/readex-pro/500.css` et `/600.css`.
- Dans `angular.json`, mettre `"outputPath": "dist"` dans les options du build : la construction produit `dist/browser/`. Mettre aussi `"proxyConfig": "proxy.conf.json"` dans les options de `serve`.
- `proxy.conf.json` : `{ "/api": { "target": "http://localhost:5087", "secure": false } }`.
- `index.html` : `<html lang="fr">`, `<title>SchoolHub</title>`, `<meta name="theme-color" content="#1B5DA4">`.
- `styles.css` : les couleurs et les polices de la maquette (voir les contraintes globales), plus `*{box-sizing:border-box}`, `button{font:inherit;color:inherit}`, `:focus-visible`, `svg.lucide`, `.sr` et `.num`. `body` vaut `margin:0` et `background:var(--bg)`, sans les marges de la maquette, qui encadraient la fenêtre de démonstration.

- [ ] **Étape 6 : lancer les tests et la construction**

Lancer : `npm test -- --watch=false`, puis `npm run build`
Résultat attendu : les tests passent, et `dist/browser/index.html` existe.

- [ ] **Étape 7 : committer**

Vérifier d'abord que `git status` ne montre ni `node_modules/` ni `dist/`.
Message : « Application Angular : le projet, en français, avec le style de la maquette ».

---

### Tâche 2 : les règles sans écran

**Fichiers :**
- Créer : `socle/session/moi.ts`, `socle/session/retour.ts`, `socle/erreurs.ts`, `socle/dates.ts`, `socle/personnes.ts`
- Test : `retour.spec.ts`, `erreurs.spec.ts`, `dates.spec.ts`, `personnes.spec.ts`, chacun à côté de son fichier

**Interfaces :**
- Produit :
  - `moi.ts` : `export type Role = 'Proprietaire' | 'Secretaire';` et `export interface Moi { prenom: string; nom: string; role: Role; centre: string; }`
  - `retour.ts` : `export function adresseDeRetour(retour: string | null | undefined): string`
  - `erreurs.ts` : `export const PAS_D_INTERNET = 'Pas de connexion à Internet : réessayez.';`, `export const ERREUR_INCONNUE = 'Une erreur est survenue : réessayez.';` et `export function messageDErreur(erreur: unknown): string`
  - `dates.ts` : `export function dateDuJour(maintenant: Date): string` ; importe `./francais`
  - `personnes.ts` : `export function initiales(prenom: string, nom: string): string` et `export function libelleDuRole(role: Role): string`

- [ ] **Étape 1 : écrire les tests qui échouent**

```ts
// retour.spec.ts
it.each([
  [undefined, '/aujourdhui'], ['', '/aujourdhui'], ['/caisse', '/caisse'], ['/eleves?groupe=3', '/eleves?groupe=3'],
  ['//exemple.com', '/aujourdhui'], ['/\\exemple.com', '/aujourdhui'], ['https://exemple.com', '/aujourdhui'],
  ['caisse', '/aujourdhui'], ['/connexion?retour=%2Fcaisse', '/aujourdhui'],
])('adresseDeRetour(%s) → %s', (retour, attendu) => expect(adresseDeRetour(retour)).toBe(attendu));

// erreurs.spec.ts
// new HttpErrorResponse({ status: 0 })                                         → PAS_D_INTERNET
// new HttpErrorResponse({ status: 401, error: { message: 'Email ou mot de passe incorrect' } }) → 'Email ou mot de passe incorrect'
// new HttpErrorResponse({ status: 500, error: null })                          → ERREUR_INCONNUE
// new HttpErrorResponse({ status: 500, error: 'Internal Server Error' })       → ERREUR_INCONNUE
// new Error('panne')                                                           → ERREUR_INCONNUE

// dates.spec.ts
expect(dateDuJour(new Date(2026, 10, 3))).toBe('Mardi 3 novembre 2026');
expect(dateDuJour(new Date(2028, 0, 15))).toBe('Samedi 15 janvier 2028');

// personnes.spec.ts
expect(initiales('Amira', 'Jlassi')).toBe('AJ');
expect(initiales('Hedi', 'Ben Salah')).toBe('HB');
expect(libelleDuRole('Proprietaire')).toBe('Propriétaire');
expect(libelleDuRole('Secretaire')).toBe('Secrétaire');
```

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `npm test -- --watch=false`
Résultat attendu : ÉCHEC (les fichiers n'existent pas).

- [ ] **Étape 3 : écrire le code**

- `adresseDeRetour` n'accepte qu'une adresse qui commence par un seul `/`, sans `\` en deuxième caractère, et qui ne mène pas à `/connexion`. Sinon, elle répond `/aujourdhui`.
- `messageDErreur` : un `HttpErrorResponse` de statut 0 donne `PAS_D_INTERNET`. Un `error.message` de type texte donne ce texte. Tout le reste donne `ERREUR_INCONNUE`.
- `dateDuJour` : `formatDate(maintenant, 'EEEE d MMMM y', FRANCAIS)`, avec la première lettre en majuscule.

- [ ] **Étape 4 : lancer les tests**

Lancer : `npm test -- --watch=false`
Résultat attendu : tout passe.

- [ ] **Étape 5 : committer**

Message : « Application Angular : messages d'erreur, adresse de retour, date du jour ».

---

### Tâche 3 : la session

**Fichiers :**
- Créer : `socle/session/session.ts`, `socle/session/gardes.ts`, `socle/session/intercepteur.ts`, `test/personnes.ts`, `test/page-de-test.ts`
- Modifier : `app.config.ts` (`provideHttpClient(withInterceptors([intercepteurDeSession]))`)
- Test : `session.spec.ts`, `gardes.spec.ts`, `intercepteur.spec.ts`

**Interfaces :**
- Consomme : `Moi`, `Role` (tâche 2).
- Produit :
  - `@Injectable({ providedIn: 'root' }) export class Session` :
    - `readonly personne: Signal<Moi | null>` vaut `null` tant que la personne n'est pas connue ;
    - `connaitre(moi: Moi): void` ;
    - `oublier(): void` : la personne redevient *inconnue*, et le prochain `charger()` redemande au serveur ;
    - `charger(): Promise<Moi | null>` : si la personne est connue, la renvoie sans requête. Sinon, fait `GET /api/moi`. Un 200 la rend connue. Un 401, ou toute autre erreur, renvoie `null` et retient *personne de connecté* jusqu'au prochain `connaitre` ou `oublier`. Deux appels simultanés partagent la même requête ;
    - `seDeconnecter(): Promise<void>` : `POST /api/deconnexion`. Un 204 ou un 401 appelle `oublier()`, puis résout. Toute autre erreur rejette avec l'erreur reçue, et la personne reste connue.
  - `gardes.ts` :
    - `export const estConnecte: CanActivateFn` : `true` si `charger()` renvoie une personne, sinon `UrlTree` vers `/connexion?retour=<state.url>` ;
    - `export const estProprietaire: CanMatchFn` : `(await charger())?.role === 'Proprietaire'`. Elle **appelle `charger()`** elle-même, car elle passe avant `estConnecte` (point de vigilance 2) ;
    - `export const nonConnecte: CanActivateFn` : `UrlTree` vers `/aujourdhui` si `charger()` renvoie une personne, sinon `true`.
  - `intercepteur.ts` : `export const intercepteurDeSession: HttpInterceptorFn`. Sur un `HttpErrorResponse` 401, dont le chemin (sans les paramètres) n'est pas l'un de `/api/connexion`, `/api/deconnexion`, `/api/moi`, `/api/invitation`, `/api/mot-de-passe-oublie` ou `/api/nouveau-mot-de-passe` :
    1. `session.oublier()` ;
    2. `router.navigate(['/connexion'], { queryParams: { retour: router.url, expiree: 1 } })` ;
    3. l'erreur est relancée.

- [ ] **Étape 1 : écrire les tests qui échouent**

```ts
// session.spec.ts (provideHttpClient(), provideHttpClientTesting())
it('ne demande qu\'une fois qui est connecté', async () => {
  const a = session.charger(); const b = session.charger();
  http.expectOne('/api/moi').flush(AMIRA);
  expect(await a).toEqual(AMIRA); expect(await b).toEqual(AMIRA);
  expect(await session.charger()).toEqual(AMIRA);           // aucune nouvelle requête (verify)
});
it('un 401 veut dire personne', ...)                         // flush 401 → null, personne() null
it('connaitre évite la requête', ...)                        // connaitre(HEDI) → charger() sans requête
it('après oublier, redemande au serveur', ...)               // charger (AMIRA), oublier, charger → 2e expectOne('/api/moi')
it.each([204, 401])('se déconnecter avec %s oublie la personne', ...)  // POST /api/deconnexion → personne() null
it('sans Internet, la déconnexion échoue et la personne reste', ...)   // status 0 → rejette, personne() AMIRA

// gardes.spec.ts (TestBed.runInInjectionContext)
// estConnecte vers /caisse : 401 → UrlTree '/connexion?retour=%2Fcaisse' ; AMIRA → true
// estProprietaire : session inconnue et /api/moi → HEDI → true (une seule requête) ; AMIRA → false
// nonConnecte : AMIRA → UrlTree '/aujourdhui' ; 401 → true

// intercepteur.spec.ts (provideRouter([{ path: '**', component: PageDeTest }]), intercepteur branché)
// sur /caisse avec AMIRA connue : GET /api/eleves → 401
//   → personne() null, router.url === '/connexion?retour=%2Fcaisse&expiree=1', l'abonné reçoit l'erreur
// GET /api/moi → 401 et POST /api/connexion → 401 : router.url ne change pas
// GET /api/eleves → 500 : router.url ne change pas
```

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `npm test -- --watch=false`
Résultat attendu : ÉCHEC (les fichiers n'existent pas).

- [ ] **Étape 3 : écrire le code** avec les interfaces ci-dessus. La requête en cours de `charger()` est gardée dans un champ privé, et effacée quand elle se termine.

- [ ] **Étape 4 : lancer les tests**

Lancer : `npm test -- --watch=false`
Résultat attendu : tout passe.

- [ ] **Étape 5 : committer**

Message : « Application Angular : la session, ses gardes et l'intercepteur des 401 ».

---

### Tâche 4 : le menu, les adresses et les pages simples (CPT-06)

**Fichiers :**
- Créer : `socle/menu.ts`, `socle/icones.ts`, `socle/icone.ts`, `cadre/cadre.ts` (provisoire : `<router-outlet />`, rempli en tâche 5), `pages/aujourdhui/aujourdhui.ts`, `pages/bientot/bientot.ts`, `pages/acces-reserve/acces-reserve.ts`, `pages/introuvable/introuvable.ts`
- Modifier : `app.routes.ts`
- Test : `menu.spec.ts`, `icone.spec.ts`, `app.routes.spec.ts`

**Interfaces :**
- Consomme : `Session`, `estConnecte`, `estProprietaire` (tâche 3) ; `Role` (tâche 2).
- Produit :
  - `icones.ts` : `export type NomDIcone = 'calendar' | 'graduation-cap' | 'users' | 'wallet' | 'book-open' | 'inbox' | 'shopping-cart' | 'percent' | 'trending-up' | 'contact' | 'history' | 'user-cog' | 'settings' | 'search' | 'menu' | 'log-out' | 'eye' | 'eye-off';` et `export const ICONES: Record<NomDIcone, string>`. Chaque valeur contient les éléments intérieurs du SVG, copiés de `https://unpkg.com/lucide-static@0.460.0/icons/<nom>.svg` (licence ISC, à rappeler en commentaire).
  - `icone.ts` : `@Component({ selector: 'sh-icone' }) export class Icone { readonly nom = input.required<NomDIcone>(); }`. Il rend `<svg class="lucide" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">` avec le contenu de `ICONES[nom()]`. Ce contenu passe par `DomSanitizer.bypassSecurityTrustHtml`, ce qui est sûr ici car ce sont nos propres constantes (à dire en commentaire).
  - `menu.ts` :
    - `export interface EntreeDuMenu { libelle: string; chemin: string; icone: NomDIcone; reservee: boolean; }` ;
    - `export const MENU: readonly EntreeDuMenu[]` : les 13 entrées, dans l'ordre et avec les chemins du tableau de la conception, section 2. Les icônes sont `calendar`, `graduation-cap`, `users`, `wallet`, `book-open`, `inbox`, `shopping-cart` (toutes les personnes), puis `percent`, `trending-up`, `contact`, `history`, `user-cog` et `settings` (réservées) ;
    - `export const ONGLETS: readonly EntreeDuMenu[]` : Aujourd'hui (`aujourdhui`, `calendar`), Guichet (`guichet`, `search`), Caisse (`caisse`, `wallet`) et Plus (`plus`, `menu`) ;
    - `export function menuDe(role: Role): EntreeDuMenu[]`.
  - `Bientot` : `readonly titre = input.required<string>()` (lu dans `data` grâce à `withComponentInputBinding`). Il affiche `<h1 class="h1">{{ titre() }}</h1>` puis « Cette page arrive bientôt ».
  - `AccesReserve` affiche « Accès réservé au propriétaire ». `Introuvable` affiche « Cette page n'existe pas » et un lien « Aujourd'hui » vers `/aujourdhui`. `Aujourdhui` affiche « Bonjour {prénom} », lu dans `Session.personne()`, puis « Les séances du jour s'afficheront ici. ».
  - `app.routes.ts` : les pages publiques viendront en tête (tâches 6 à 8). Puis `{ path: '', component: Cadre, canActivate: [estConnecte], children: [...] }`, avec dans l'ordre :
    1. `''` (`pathMatch: 'full'`), qui redirige vers `aujourdhui` ;
    2. `aujourdhui` → `Aujourdhui` ;
    3. pour chaque autre entrée de `MENU` : `{ path: chemin, component: Bientot, data: { titre: libelle }, title: '<libellé> · SchoolHub' }`. Une entrée réservée ajoute avant ce chemin `canMatch: [estProprietaire]`, et elle est suivie d'une seconde adresse `{ path: chemin, component: AccesReserve, title: 'Accès réservé · SchoolHub' }` ;
    4. `guichet` → `Bientot` (titre « Guichet ») ;
    5. `**` → `Introuvable`. La tâche 5 ajoutera `plus` avant `**`.

- [ ] **Étape 1 : écrire les tests qui échouent**

```ts
// menu.spec.ts
expect(menuDe('Secretaire').map(e => e.libelle)).toEqual(
  ["Aujourd'hui", 'Professeurs', 'Élèves', 'Caisse', 'Bibliothèque', "Demandes d'inscription", 'Achats']);
expect(menuDe('Proprietaire').map(e => e.libelle).slice(7)).toEqual(
  ['Parts des professeurs', 'Tableau de bord', 'Ressources humaines', 'Journal', 'Comptes', 'Paramètres du centre']);

// icone.spec.ts : <sh-icone nom="calendar"> rend un svg.lucide avec au moins un élément enfant

// app.routes.spec.ts (provideRouter(routes, withComponentInputBinding()), HttpTestingController)
it('une secrétaire lit « Accès réservé au propriétaire » sur /journal', ...)
  // navigateByUrl('/journal') ; expectOne('/api/moi').flush(AMIRA)
  // texte contient 'Accès réservé au propriétaire' ; location.path() === '/journal' ; titre 'Accès réservé · SchoolHub'
it('le propriétaire qui ouvre /journal en premier voit sa page', ...)   // point de vigilance 2
  // une seule requête /api/moi → HEDI ; texte contient 'Journal' et 'Cette page arrive bientôt'
it('/ mène à « Aujourd\'hui »', ...)        // AMIRA → path '/aujourdhui', texte 'Bonjour Amira', titre "Aujourd'hui · SchoolHub"
it('/caisse est une page bientôt', ...)     // titre 'Caisse · SchoolHub', texte 'Cette page arrive bientôt'
it('une adresse inconnue', ...)             // '/nimporte' → 'Cette page n\'existe pas'
```

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `npm test -- --watch=false`
Résultat attendu : ÉCHEC.

- [ ] **Étape 3 : écrire le code** avec les interfaces ci-dessus. Le titre de chaque page est un `<h1 class="h1">` dans un `.pagehead`.

- [ ] **Étape 4 : lancer les tests**

Lancer : `npm test -- --watch=false`
Résultat attendu : tout passe.

- [ ] **Étape 5 : committer**

Message : « CPT-06 : le menu selon le rôle, et les pages réservées au propriétaire ».

---

### Tâche 5 : le cadre

**Fichiers :**
- Créer : `cadre/cadre.html`, `cadre/cadre.css`, `cadre/plus.ts`, `cadre/bouton-de-deconnexion.ts`
- Modifier : `cadre/cadre.ts`, `app.routes.ts` (ajoute `{ path: 'plus', component: Plus, title: 'Plus · SchoolHub' }` avant `**`)
- Test : `cadre/cadre.spec.ts`, `cadre/plus.spec.ts`, `cadre/bouton-de-deconnexion.spec.ts`

**Interfaces :**
- Consomme : `Session` (tâche 3) ; `menuDe`, `ONGLETS`, `Icone` (tâche 4) ; `dateDuJour`, `initiales`, `libelleDuRole`, `messageDErreur` (tâche 2).
- Produit :
  - `Cadre` reprend les classes de la maquette :
    - `.top` : le logo `SH`, puis « SchoolHub » et le nom du centre ; la date `dateDuJour(new Date())`, calculée une fois à la création ; les initiales, le nom complet et `libelleDuRole` ;
    - `.rail` : les entrées de `menuDe(role)` en `routerLink` avec `routerLinkActive` (`aria-current="page"`). Le titre `.railh` « Propriétaire » est placé avant la première entrée réservée, et `<sh-bouton-de-deconnexion>` en bas ;
    - `<main class="main">` qui contient le `<router-outlet />` ;
    - `.tabbar` avec les 4 `ONGLETS`. « Plus » est actif quand aucun des trois autres ne l'est.

    Sur téléphone (`@media (max-width: 760px)`), les règles de la maquette s'appliquent : le menu est caché, les onglets restent visibles en bas (`position: sticky; bottom: 0`), et l'en-tête montre le nom du centre et la date. Un nom de centre trop long est coupé avec `text-overflow: ellipsis`.
  - `Plus` : un `.pagehead` « Plus », la liste de `menuDe(role)` avec leurs icônes, puis `<sh-bouton-de-deconnexion>`.
  - `BoutonDeDeconnexion` (`sh-bouton-de-deconnexion`) : le bouton « Se déconnecter », avec l'icône `log-out`. Au clic :
    1. il appelle `session.seDeconnecter()` ;
    2. puis `router.navigateByUrl('/connexion', { replaceUrl: true })` ;
    3. en cas d'échec, il affiche `messageDErreur(erreur)` dans une `.note.bad`, à côté du bouton.

- [ ] **Étape 1 : écrire les tests qui échouent**

```ts
// cadre.spec.ts (Session.connaitre(AMIRA) avant la navigation ; table : routes de l'application + { path: 'connexion', component: PageDeTest } en tête)
// l'en-tête contient 'Centre Les Jasmins', 'AJ', 'Amira Jlassi', 'Secrétaire', dateDuJour(new Date())
// .rail : 7 liens, pas de texte 'Propriétaire', pas de lien vers /journal
// avec HEDI : .rail contient 'Propriétaire' et 13 liens, dont /parametres
// .tabbar : liens vers /aujourdhui, /guichet, /caisse, /plus ; sur /caisse, le lien /caisse a aria-current="page"

// plus.spec.ts : AMIRA → 7 liens et le bouton « Se déconnecter », aucun lien vers /journal

// bouton-de-deconnexion.spec.ts
it.each([204, 401])('« Se déconnecter » mène à la connexion (%s)', ...)
  // clic → expectOne POST '/api/deconnexion' → flush → router.url === '/connexion', personne() null
it('sans Internet, la personne reste et lit le message', ...)
  // status 0 → texte 'Pas de connexion à Internet : réessayez.', router.url inchangé
```

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `npm test -- --watch=false`
Résultat attendu : ÉCHEC.

- [ ] **Étape 3 : écrire le code** avec les interfaces ci-dessus.

- [ ] **Étape 4 : lancer les tests**

Lancer : `npm test -- --watch=false`
Résultat attendu : tout passe.

- [ ] **Étape 5 : committer**

Message : « Application Angular : le cadre, son menu, ses onglets et la déconnexion ».

---

### Tâche 6 : la page de connexion (CPT-01)

**Fichiers :**
- Créer : `cadre/carte-publique.ts`, `socle/champ-mot-de-passe.ts`, `socle/champ-mot-de-passe.html`, `pages/connexion/connexion.ts`, `connexion.html`, `connexion.css`
- Modifier : `app.routes.ts`. En tête : `{ path: 'connexion', component: Connexion, canActivate: [nonConnecte], title: 'Connexion · SchoolHub' }`.
- Test : `champ-mot-de-passe.spec.ts`, `connexion.spec.ts`, plus un cas dans `app.routes.spec.ts`

**Interfaces :**
- Consomme : `Session`, `nonConnecte`, `adresseDeRetour` (tâches 2 et 3) ; `messageDErreur` ; `Icone`.
- Produit :
  - `CartePublique` (`sh-carte-publique`) : le logo `SH` et « SchoolHub » centrés au-dessus d'une `.card` de 360 pixels de large au plus, centrée dans la page. Le contenu passe par `<ng-content />`. Sur téléphone, la carte prend la largeur, moins 16 pixels de chaque côté.
  - `ChampMotDePasse` (`sh-champ-mot-de-passe`) :
    - `readonly controle = input.required<FormControl<string>>()`, `readonly libelle = input('Mot de passe')`, `readonly autocomplete = input<'current-password' | 'new-password'>('current-password')` ;
    - il rend un `label.f`, un `input` de type `password` ou `text`, et un bouton « Afficher » / « Masquer » (`type="button"`, `aria-pressed`, icône `eye` ou `eye-off`).
  - `Connexion` :
    - `readonly retour = input<string>()` et `readonly expiree = input<string>()`, deux paramètres d'adresse ;
    - un `FormGroup` non nullable `{ email, motDePasse }`, plus `envoi = signal(false)` et `message = signal<string | null>(null)`.

    À l'envoi (`(ngSubmit)`) :
    1. rien ne se passe si `envoi()` est déjà vrai ;
    2. l'email est rogné. S'il est vide, ou si le mot de passe est vide, le message devient « Saisissez votre email et votre mot de passe » ;
    3. sinon, `POST /api/connexion` part. Une réponse appelle `session.connaitre(moi)`, puis `router.navigateByUrl(adresseDeRetour(retour()))`. Une erreur affiche `messageDErreur(erreur)`.

    Le bouton vaut « Se connecter », ou « Connexion… » et `disabled` pendant l'envoi. Le lien « Mot de passe oublié » mène à `/mot-de-passe-oublie`. Si `expiree` est présent, la page affiche « Votre session a expiré : reconnectez-vous » au-dessus du formulaire (`.note`). Les messages d'erreur sont dans une `.note.bad` `role="alert"`. L'email a `type="email"` et `autocomplete="username"`.

- [ ] **Étape 1 : écrire les tests qui échouent**

```ts
// champ-mot-de-passe.spec.ts : clic sur « Afficher » → input type 'text', bouton « Masquer », aria-pressed 'true'

// connexion.spec.ts (table : connexion, plus '**' → PageDeTest)
it('un bon mot de passe mène à « Aujourd\'hui »', ...)
  // saisir '  amira@exemple.tn ' et 'jasmins-2027', envoyer
  // expectOne POST '/api/connexion', body { email: 'amira@exemple.tn', motDePasse: 'jasmins-2027' } → flush AMIRA
  // router.url === '/aujourdhui', session.personne() AMIRA
it('revient à la page demandée', ...)            // '/connexion?retour=%2Fcaisse' → '/caisse'
it('affiche le refus du serveur', ...)           // 401 { message: 'Email ou mot de passe incorrect' } → texte affiché
it('ne part qu\'une fois', ...)                  // deux envois → un seul expectOne ; bouton 'Connexion…' disabled
it('un email fait d\'espaces ne part pas', ...)  // '   ' → 'Saisissez votre email et votre mot de passe', verify()
it('annonce une session expirée', ...)           // '/connexion?expiree=1' → 'Votre session a expiré : reconnectez-vous'

// app.routes.spec.ts
it('une personne déjà connectée passe à « Aujourd\'hui »', ...)  // '/connexion', /api/moi → AMIRA → path '/aujourdhui'
it('sans session, /connexion s\'affiche sans message', ...)      // /api/moi → 401 → bouton 'Se connecter', pas de 'expiré'
```

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `npm test -- --watch=false`
Résultat attendu : ÉCHEC.

- [ ] **Étape 3 : écrire le code** avec les interfaces ci-dessus (`ReactiveFormsModule`).

- [ ] **Étape 4 : lancer les tests**

Lancer : `npm test -- --watch=false`
Résultat attendu : tout passe.

- [ ] **Étape 5 : committer**

Message : « CPT-01 : la page de connexion ».

---

### Tâche 7 : la page d'invitation (CPT-02)

**Fichiers :**
- Créer : `socle/lien-de-l-email.ts`, `pages/invitation/invitation.ts`, `invitation.html`
- Modifier : `app.routes.ts`. En tête : `{ path: 'invitation', component: Invitation, title: 'Choisir son mot de passe · SchoolHub' }`, sans garde.
- Test : `lien-de-l-email.spec.ts`, `invitation.spec.ts`

**Interfaces :**
- Consomme : `CartePublique`, `ChampMotDePasse` (tâche 6) ; `Session` ; `messageDErreur`.
- Produit :
  - `lien-de-l-email.ts` : `export interface LienDeLEmail { compte: string; jeton: string; }` et `export function lireLeLienDeLEmail(): LienDeLEmail`, à appeler dans un contexte d'injection (constructeur) :
    - si `compte` ou `jeton` sont dans les paramètres de l'adresse (`ActivatedRoute.snapshot.queryParamMap`), la fonction les renvoie. Avant, elle appelle `router.navigate([], { relativeTo: route, queryParams: {}, replaceUrl: true, state: { compte, jeton } })` : le lien quitte la barre d'adresse et reste dans l'état de navigation ;
    - sinon, elle les lit dans `Location.getState()` (après un rechargement) ;
    - à défaut, elle renvoie `''` pour chacun.
  - `Invitation` : `etat = signal<'verification' | 'formulaire' | 'refus'>('verification')`, `accueil = signal<{ prenom: string; centre: string } | null>(null)`, `message`, `envoi`.
    - À la création : `` GET `/api/invitation?compte=${encodeURIComponent(compte)}&jeton=${encodeURIComponent(jeton)}` ``. L'adresse est écrite ainsi, et pas avec `HttpParams`, pour que `+` parte bien en `%2B` (point de vigilance 5). Une réponse fait passer à `formulaire`. Une erreur affiche `messageDErreur` en `refus`, sans formulaire.
    - À l'envoi :
      1. rien ne se passe si `envoi()` est vrai ;
      2. moins de 8 caractères donne « Le mot de passe doit faire au moins 8 caractères » ;
      3. sinon, `POST /api/invitation` part avec `{ compte, jeton, motDePasse }`. Une réponse appelle `connaitre`, puis mène à `/aujourdhui`. Un 410 passe à `refus` avec le message du serveur. Toute autre erreur affiche le message sous le champ.
    - Le gabarit : « Bonjour {prenom}, bienvenue au {centre} », `<sh-champ-mot-de-passe autocomplete="new-password">`, et le bouton « Choisir ce mot de passe » / « Enregistrement… ».

- [ ] **Étape 1 : écrire les tests qui échouent**

```ts
// invitation.spec.ts (table : invitation, plus '**' → PageDeTest)
it('accueille la personne du lien', ...)
  // navigateByUrl('/invitation?compte=c1&jeton=j%2B1')
  // expectOne('/api/invitation?compte=c1&jeton=j%2B1')       // le + part en %2B (point de vigilance 5)
  //   .flush({ prenom: 'Amira', centre: 'Centre Les Jasmins' })
  // texte 'Bonjour Amira, bienvenue au Centre Les Jasmins' ; TestBed.inject(Location).path() === '/invitation'
it('un lien expiré affiche son message sans formulaire', ...)
  // 410 { message: 'Ce lien a expiré : demandez au propriétaire une nouvelle invitation' } → texte, aucun input[type=password]
it('après un rechargement, le lien sert encore', ...)          // point de vigilance 4
  // navigateByUrl('/invitation', { state: { compte: 'c1', jeton: 'j1' } }) → expectOne('/api/invitation?compte=c1&jeton=j1')
it('un mot de passe trop court ne part pas', ...)              // '1234567' → 'Le mot de passe doit faire au moins 8 caractères', verify()
it('le bon mot de passe connecte et mène à « Aujourd\'hui »', ...)
  // POST '/api/invitation', body { compte: 'c1', jeton: 'j+1', motDePasse: 'jasmins-2027' } → AMIRA → router.url '/aujourdhui'
it('ne part qu\'une fois', ...)                                // deux envois → un seul POST
it('un lien utilisé entre-temps affiche son message', ...)     // POST → 410 → message, plus de formulaire
```

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `npm test -- --watch=false`
Résultat attendu : ÉCHEC.

- [ ] **Étape 3 : écrire le code** avec les interfaces ci-dessus.

- [ ] **Étape 4 : lancer les tests**

Lancer : `npm test -- --watch=false`
Résultat attendu : tout passe.

- [ ] **Étape 5 : committer**

Message : « CPT-02 : la page d'invitation, et le lien qui quitte la barre d'adresse ».

---

### Tâche 8 : le mot de passe oublié (CPT-03)

**Fichiers :**
- Créer : `pages/mot-de-passe-oublie/mot-de-passe-oublie.ts` et `.html`, `pages/nouveau-mot-de-passe/nouveau-mot-de-passe.ts` et `.html`
- Modifier : `app.routes.ts`. En tête : `mot-de-passe-oublie` (titre « Mot de passe oublié · SchoolHub ») et `nouveau-mot-de-passe` (titre « Nouveau mot de passe · SchoolHub »), sans garde.
- Test : `mot-de-passe-oublie.spec.ts`, `nouveau-mot-de-passe.spec.ts`

**Interfaces :**
- Consomme : `CartePublique`, `ChampMotDePasse`, `lireLeLienDeLEmail`, `Session`, `messageDErreur`.
- Produit :
  - `MotDePasseOublie` : un champ email et « Recevoir un lien » / « Envoi… ».
    1. Un email rogné vide donne « Saisissez votre email ».
    2. Sinon, `POST /api/mot-de-passe-oublie` part avec `{ email }`. Le `message` reçu remplace le formulaire.
    3. Une erreur affiche `messageDErreur`.

    Le lien « Retour à la connexion » mène à `/connexion`.
  - `NouveauMotDePasse` : `lireLeLienDeLEmail()` à la création, puis le même formulaire et les mêmes règles que l'invitation. L'envoi part vers `POST /api/nouveau-mot-de-passe`. Un 410 remplace le formulaire par le message du serveur et le lien « Refaire une demande », vers `/mot-de-passe-oublie`. Il n'y a pas d'appel à l'ouverture.

- [ ] **Étape 1 : écrire les tests qui échouent**

```ts
// mot-de-passe-oublie.spec.ts
it('répond toujours le même message', ...)
  // '  amira@exemple.tn ' → POST body { email: 'amira@exemple.tn' }
  // flush { message: 'Si un compte existe pour cet email, un lien vient de partir' } → texte affiché, plus de champ email
it('un email vide ne part pas', ...)            // '   ' → 'Saisissez votre email', verify()

// nouveau-mot-de-passe.spec.ts
it('le lien quitte la barre d\'adresse', ...)   // '/nouveau-mot-de-passe?compte=c1&jeton=j1' → path '/nouveau-mot-de-passe', aucune requête
it('le nouveau mot de passe connecte', ...)      // POST body { compte: 'c1', jeton: 'j1', motDePasse: 'jasmins-2028' } → HEDI → '/aujourdhui'
it('un lien expiré propose de refaire une demande', ...)
  // 410 { message: 'Ce lien a expiré : refaites une demande de mot de passe oublié' } → texte, lien vers /mot-de-passe-oublie
it('après un rechargement, le lien sert encore', ...)   // state { compte, jeton } → le POST les contient
```

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `npm test -- --watch=false`
Résultat attendu : ÉCHEC.

- [ ] **Étape 3 : écrire le code** avec les interfaces ci-dessus.

- [ ] **Étape 4 : lancer les tests**

Lancer : `npm test -- --watch=false`
Résultat attendu : tout passe.

- [ ] **Étape 5 : committer**

Message : « CPT-03 : les pages du mot de passe oublié ».

---

### Tâche 9 : .NET sert l'application

**Fichiers :**
- Créer : `backend/src/SchoolHub/Socle/Pages/PagesDeLApplication.cs`
- Modifier :
  - `Program.cs` ;
  - `SchoolHub.csproj` ;
  - `appsettings.Development.json` (`"Adresse": "http://localhost:4200"`) ;
  - `tests/…/Outils/ApplicationDeTest.cs` ;
  - `tests/…/SchoolHub.Tests.csproj` ;
  - `tests/…/Comptes/InvitationTests.cs:54` et `tests/…/Comptes/MotDePasseOublieTests.cs:76` (`5087` → `4200`).
- Créer (tests) : `tests/SchoolHub.Tests/wwwroot-de-test/index.html` (contient `<!-- index de test -->`), `main-TEST1234.js` (`// fichier de test`), `manifest.webmanifest` (`{ "name": "SchoolHub" }`), et `tests/…/Socle/PagesDeLApplicationTests.cs`

**Interfaces :**
- Consomme : `Avis` (`SchoolHub.Socle.Comptes`).
- Produit :

```csharp
public static class PagesDeLApplication
{
    public const string AdresseInconnue = "Adresse inconnue";

    /// Avant UtiliserLaSession : Referrer-Policy: no-referrer sur chaque réponse, puis les fichiers d'Angular, publics.
    public static IApplicationBuilder UtiliserLesFichiersDeLApplication(this IApplicationBuilder app);

    /// Après les adresses de l'API : une adresse /api inconnue répond 404 Avis(AdresseInconnue) ;
    /// toute autre adresse sans extension renvoie index.html, sans connexion.
    public static WebApplication MapPagesDeLApplication(this WebApplication app, RouteGroupBuilder api);
}
```

- [ ] **Étape 1 : écrire les tests qui échouent**

- Dans `ApplicationDeTest.ConfigureWebHost`, ajouter `builder.UseWebRoot(Path.Combine(AppContext.BaseDirectory, "wwwroot-de-test"))`.
- Dans le projet de tests, ajouter `<None Update="wwwroot-de-test\**" CopyToOutputDirectory="PreserveNewest" />`. C'est un `Update`, car le SDK inclut déjà ces fichiers dans `None`.
- `PagesDeLApplicationTests` porte `[Collection(CollectionPostgreSql.Nom)]` et utilise la base `pages_application`. Ses tests n'ont pas besoin de `InitialiserLaBaseAsync` :

```csharp
[Theory]
[InlineData("/invitation?compte=1&jeton=2")]
[InlineData("/aujourdhui")]
public async Task Une_page_de_l_application_renvoie_index_html_sans_connexion(string adresse)
// 200, Content-Type text/html, corps contient "<!-- index de test -->", Cache-Control: no-cache

[Fact] public async Task Les_fichiers_de_l_application_sont_servis_sans_connexion()
// /main-TEST1234.js → 200 ; /manifest.webmanifest → 200, Content-Type application/manifest+json

[Fact] public async Task Une_adresse_de_l_api_inconnue_repond_introuvable()
// /api/inconnue → 404, JSON { "message": "Adresse inconnue" }

[Fact] public async Task L_api_reste_fermee_sans_connexion()
// /api/moi → 401

[Theory]
[InlineData("/aujourdhui")] [InlineData("/main-TEST1234.js")] [InlineData("/api/moi")] [InlineData("/api/sante")]
public async Task Chaque_reponse_porte_Referrer_Policy_no_referrer(string adresse)
```

- Remplacer `http://localhost:5087` par `http://localhost:4200` aux deux lignes de `InvitationTests` et de `MotDePasseOublieTests`.

- [ ] **Étape 2 : lancer les tests et vérifier qu'ils échouent**

Lancer : `dotnet test`
Résultat attendu : ÉCHEC sur les nouveaux tests (404 sur `/aujourdhui`, pas d'en-tête), et sur les deux tests d'email (l'adresse est encore 5087).

- [ ] **Étape 3 : écrire le code**

- `appsettings.Development.json` : `"Adresse": "http://localhost:4200"`.
- `PagesDeLApplication` utilise un seul `StaticFileOptions`, pour `UseStaticFiles` comme pour `MapFallbackToFile`. Son `ContentTypeProvider` est un `FileExtensionContentTypeProvider` qui associe `.webmanifest` à `application/manifest+json`. Son `OnPrepareResponse` met `Cache-Control: no-cache` sur `index.html`.
- `MapPagesDeLApplication` :
  - `api.MapFallback(() => TypedResults.Json(new Avis(AdresseInconnue), statusCode: 404)).AllowAnonymous()` ;
  - `app.MapFallbackToFile("index.html", options).AllowAnonymous()`.

  Sans `AllowAnonymous`, la politique par défaut, qui exige d'être connecté, répondrait 401.
- `Program.cs` : `app.UtiliserLesFichiersDeLApplication();` juste avant `app.UtiliserLaSession();`, et `app.MapPagesDeLApplication(api);` après les `Map…` de l'API. Ajouter un commentaire : les fichiers passent avant la session, sinon la page de connexion répondrait 401.
- `SchoolHub.csproj` : la construction d'Angular à la publication seulement :

```xml
<PropertyGroup>
  <DossierDuFrontend>$([MSBuild]::NormalizeDirectory('$(MSBuildProjectDirectory)', '..', '..', '..', 'frontend'))</DossierDuFrontend>
</PropertyGroup>

<!-- À la publication seulement : dotnet build et dotnet test n'ont pas besoin de Node. -->
<Target Name="ConstruireAngular" AfterTargets="ComputeFilesToPublish">
  <Exec WorkingDirectory="$(DossierDuFrontend)" Command="npm ci" />
  <Exec WorkingDirectory="$(DossierDuFrontend)" Command="npm run build" />
  <ItemGroup>
    <FichiersAngular Include="$(DossierDuFrontend)dist\browser\**" />
    <ResolvedFileToPublish Include="@(FichiersAngular->'%(FullPath)')" Exclude="@(ResolvedFileToPublish)">
      <RelativePath>wwwroot\%(RecursiveDir)%(FileName)%(Extension)</RelativePath>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
      <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
    </ResolvedFileToPublish>
  </ItemGroup>
</Target>
```

- [ ] **Étape 4 : lancer tous les tests**

Lancer : `dotnet test`
Résultat attendu : tous passent, les anciens comme les nouveaux.

- [ ] **Étape 5 : vérifier la publication**

Lancer : `dotnet publish src/SchoolHub -c Release -o <dossier temporaire hors du dépôt>/publication`
Résultat attendu : `publication/wwwroot/index.html` et `publication/wwwroot/main-*.js` existent.

- [ ] **Étape 6 : committer**

Message : « Application Angular servie par .NET : fichiers publics, page par défaut, adresse /api inconnue ».

---

### Tâche 10 : l'installation sur l'écran d'accueil, et la documentation

**Fichiers :**
- Créer :
  - `frontend/outils/creer-icones.ps1` ;
  - `frontend/public/icones/sh-32.png`, `sh-180.png`, `sh-192.png`, `sh-512.png` et `sh-512-adaptee.png` (fabriqués par le script) ;
  - `frontend/public/manifest.webmanifest` ;
  - `frontend/README.md`.
- Modifier : `frontend/src/index.html`, `backend/README.md`
- Supprimer : `frontend/public/favicon.ico` (le logo d'Angular)

**Interfaces :** aucune.

- [ ] **Étape 1 : fabriquer les icônes**

`creer-icones.ps1` utilise Windows PowerShell 5.1 et `System.Drawing`.

- **Les icônes ordinaires** : un carré de N pixels, fond `#1B5DA4` avec des coins arrondis de rayon 0,26 × N (le logo de la maquette), et « SH » en blanc, en Segoe UI Semibold de 0,40 × N, centré, lissé.
- **`sh-512-adaptee.png`** : le fond plein, sans coins arrondis, et « SH » en 0,30 × N, pour rester dans la zone sûre d'Android.

Le script écrit dans `public/icones/`.

Lancer : `powershell -NoProfile -ExecutionPolicy Bypass -File outils/creer-icones.ps1`
Résultat attendu : les 5 PNG existent. Ouvrir `sh-192.png` pour vérifier le rendu.

- [ ] **Étape 2 : écrire le manifeste et les liens**

`manifest.webmanifest` :

```json
{
  "name": "SchoolHub",
  "short_name": "SchoolHub",
  "lang": "fr",
  "start_url": "/aujourdhui",
  "scope": "/",
  "display": "standalone",
  "background_color": "#F1F4F7",
  "theme_color": "#1B5DA4",
  "icons": [
    { "src": "icones/sh-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "icones/sh-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "icones/sh-512-adaptee.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ]
}
```

Dans `index.html` : `<link rel="manifest" href="manifest.webmanifest">`, `<link rel="icon" type="image/png" href="icones/sh-32.png">` et `<link rel="apple-touch-icon" href="icones/sh-180.png">`. Supprimer `public/favicon.ico` et son lien.

- [ ] **Étape 3 : vérifier la construction**

Lancer : `npm run build`, puis `npm test -- --watch=false`
Résultat attendu : `dist/browser/manifest.webmanifest` et `dist/browser/icones/sh-512.png` existent, et les tests passent.

- [ ] **Étape 4 : écrire la documentation**

- `frontend/README.md`, en français, comme `backend/README.md` :
  - ce qu'il faut sur le poste (Node 26) ;
  - lancer (`npm ci`, puis `npm start` avec le serveur lancé à côté, et ouvrir http://localhost:4200) ;
  - lancer les tests (`npm test`) ;
  - construire (`npm run build`, inutile à la main : `dotnet publish` le fait) ;
  - l'organisation de `src/app/` ;
  - trois règles : une seule liste de menu (`socle/menu.ts`) ; aucun appel au serveur en tâche de fond ; la personne connectée reste en mémoire.
- `backend/README.md` :
  - « Lancer l'application » renvoie vers `frontend/README.md` pour les pages, et précise que les liens des emails pointent vers le port 4200 en développement ;
  - une section « Publier » : `dotnet publish` construit aussi Angular et demande Node 26 ;
  - « Organisation du code » ajoute `Socle/Pages/`.

- [ ] **Étape 5 : committer**

Message : « Application Angular : installable sur l'écran d'accueil, et documentation ».

---

### Tâche 11 : la vérification à la main

Rien à committer, sauf une correction si une vérification échoue (elle repasse alors par un test qui échoue d'abord). Le mot de passe d'essai est choisi pendant l'essai, n'est écrit nulle part ailleurs que dans le champ, et n'est pas répété dans la conversation.

- [ ] **Étape 1 : lancer le serveur et Angular**

Depuis `backend/` : `docker compose up -d`, `dotnet ef database update --project src/SchoolHub`, puis `dotnet run --project src/SchoolHub`, en tâche de fond. Depuis `frontend/` : `npm start`, en tâche de fond.

- [ ] **Étape 2 : créer un centre d'essai**

```bash
dotnet run --project src/SchoolHub -- creer-centre --nom "Centre Les Jasmins" --code jasmins-essai --prenom Hedi --nom-proprietaire "Ben Salah" --email hedi@exemple.tn
```

Résultat attendu : le lien d'invitation, qui commence par `http://localhost:4200/invitation?`, est écrit dans la console. Si le code ou l'email est déjà pris, prendre un autre code et un autre email.

- [ ] **Étape 3 : jouer le parcours dans le navigateur intégré**

1. Ouvrir le lien : « Bonjour Hedi, bienvenue au Centre Les Jasmins », et la barre d'adresse montre `/invitation`. Recharger la page : le formulaire est toujours là.
2. Choisir le mot de passe d'essai : arrivée sur « Aujourd'hui », avec le menu Propriétaire. Si la connexion semble réussir mais que la page suivante renvoie à la connexion, le cookie `Secure` a été refusé derrière le proxy : appliquer la solution de repli de la conception (points d'attention), et le signaler.
3. Ouvrir `/journal`, puis recharger : la page « Journal » s'affiche.
4. « Se déconnecter », puis le bouton « retour » du navigateur : seule la connexion s'affiche.
5. « Mot de passe oublié » avec `hedi@exemple.tn` : le message s'affiche, et le lien est écrit dans la console du serveur. L'ouvrir, choisir un nouveau mot de passe : arrivée sur « Aujourd'hui ».
6. Refaire 1 à 4 en largeur téléphone (`resize_window` mobile), puis en mode sombre (`colorScheme: dark`). Vérifier les onglets en bas, la page « Plus », et qu'aucune page ne déborde en largeur.
7. Revenir en largeur ordinateur (`resize_window` desktop).

- [ ] **Étape 4 : vérifier l'application publiée**

Lancer l'application publiée en tâche 9 (`dotnet SchoolHub.dll --urls http://localhost:5099`, avec `ASPNETCORE_ENVIRONMENT=Development`, depuis le dossier `publication`). Ouvrir `http://localhost:5099/connexion` : la page de connexion s'affiche, et `http://localhost:5099/manifest.webmanifest` répond.

- [ ] **Étape 5 : demander à Lassad et Taha de vérifier l'installation**

Dans leur Chrome, sur `http://localhost:4200` : outils de développement, onglet « Application », puis « Manifest ». Aucune erreur ne doit bloquer l'installation, et l'icône « SH » doit s'afficher. L'essai sur un vrai téléphone se fera au premier déploiement en HTTPS (conception, points d'attention).

- [ ] **Étape 6 : arrêter ce qui a été lancé**

Arrêter le serveur, Angular et l'application publiée. Laisser PostgreSQL tourner.
