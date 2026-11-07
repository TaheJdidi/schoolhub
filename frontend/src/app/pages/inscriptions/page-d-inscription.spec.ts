import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { DELAI_DE_RECHERCHE } from '../../cadre/recherche-d-eleves';
import { saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { FicheDeLEleve } from '../eleves/eleve';
import { FicheDuGroupe } from '../groupes/groupe';
import { EnregistrementDeLInscription, GroupePossible, InscriptionDeLEleve } from './inscription';
import { PageDInscription } from './page-d-inscription';

describe("la page d'inscription", () => {
  const ADEM: FicheDeLEleve = {
    id: 'e1',
    prenom: 'Adem',
    nom: 'Miled',
    niveau: { id: 'n9', nom: '9ᵉ année de base' },
    majeur: false,
    telephoneParent: '98 401 220',
    emailParent: null,
    telephoneEleve: '22 345 678',
    emailEleve: 'adem@exemple.tn',
    etablissement: null,
  };

  const possible = (id: string, autres: Partial<GroupePossible>): GroupePossible => ({
    id,
    nom: 'Groupe Samedi 10h → 12h',
    type: 'Etude',
    matiere: 'Mathématiques',
    professeur: 'Karim Ben Salah',
    niveau: '9ᵉ année de base',
    duNiveauDeLEleve: true,
    eleves: 12,
    capacite: 15,
    dejaInscrit: false,
    ...autres,
  });

  const MATHS = possible('g1', {});
  const PHYSIQUE = possible('g2', { type: 'Revision', matiere: 'Physique', eleves: 15 });
  const ANGLAIS = possible('g4', { matiere: 'Anglais', dejaInscrit: true });
  const INFORMATIQUE = possible('g3', { type: 'Formation', matiere: 'Informatique', niveau: null, duNiveauDeLEleve: false, eleves: 3, capacite: null });

  const seance = (date: string) => ({ id: `s-${date}`, date, debut: '10:00:00', fin: '12:00:00' });

  /** Le groupe d'Étude : son Mois 3 du 13/11 au 04/12/2027, puis le 11/12. */
  const FICHE_MATHS: FicheDuGroupe = {
    id: 'g1',
    nom: 'Groupe Samedi 10h → 12h',
    type: 'Etude',
    professeur: { id: 'p1', prenom: 'Karim', nom: 'Ben Salah' },
    matiere: 'Mathématiques',
    niveau: { id: 'n9', nom: '9ᵉ année de base' },
    creneaux: [{ jour: 'Samedi', debut: '10:00', fin: '12:00' }],
    capacite: 15,
    eleves: 12,
    dateDeDebut: '2027-11-13',
    prixDuMois: 40,
    mois: [
      { numero: 3, prix: 40, commence: true, seances: ['2027-11-13', '2027-11-20', '2027-11-27', '2027-12-04'].map(seance) },
      { numero: 4, prix: 40, commence: false, seances: ['2027-12-11'].map(seance) },
    ],
    termine: false,
    premiereSeance: 9,
    prixTotal: null,
    nombreDeSeances: null,
    seances: [],
    tranches: [],
    fermeLe: null,
    motifDeFermeture: null,
  };

  const numerotees = (dates: string[]) => dates.map((date, i) => ({ ...seance(date), numero: i + 1, prix: null }));

  const FICHE_PHYSIQUE: FicheDuGroupe = {
    ...FICHE_MATHS,
    id: 'g2',
    type: 'Revision',
    matiere: 'Physique',
    eleves: 15,
    prixDuMois: null,
    mois: [],
    premiereSeance: 1,
    prixTotal: 100,
    nombreDeSeances: 2,
    seances: numerotees(['2027-11-13', '2027-11-20']),
  };

  const FICHE_INFORMATIQUE: FicheDuGroupe = {
    ...FICHE_PHYSIQUE,
    id: 'g3',
    type: 'Formation',
    matiere: 'Informatique',
    niveau: null,
    capacite: null,
    eleves: 3,
    prixTotal: 300,
    tranches: [
      { montant: 100, seance: 1 },
      { montant: 100, seance: 1 },
      { montant: 100, seance: 2 },
    ],
  };

  const INSCRIPTION: InscriptionDeLEleve = {
    id: 'i1',
    eleve: { id: 'e1', prenom: 'Adem', nom: 'Miled' },
    groupe: { id: 'g1', nom: 'Groupe Samedi 10h → 12h', type: 'Etude', matiere: 'Mathématiques' },
    professeur: 'Karim Ben Salah',
    mode: 'ParMois',
    prixNormal: 40,
    nombreDeTranches: 0,
    premiereSeance: { date: '2027-11-20', debut: '10:00:00' },
    prixSpecial: null,
    raisonDuPrixSpecial: null,
    reglees: [],
    payablesAvantSchoolHub: [],
    peutRegrouperLeReste: false,
    arreteLe: null,
    report: 0,
  };

  const ENREGISTREMENT: EnregistrementDeLInscription = {
    inscription: INSCRIPTION,
    aPayerALArrivee: [
      { libelle: 'Mois 3 (3 séances)', montant: 30 },
      { libelle: 'Assurance 2027-2028', montant: 20 },
    ],
    avertissement: null,
    texte: "Bonjour, l'inscription d'Adem Miled au Centre Les Jasmins est enregistrée.",
    messages: [
      { destinataire: 'Parent', telephone: '98 401 220', lien: 'https://wa.me/21698401220?text=Bonjour' },
      { destinataire: 'Adem', telephone: '22 345 678', lien: 'https://wa.me/21622345678?text=Bonjour' },
    ],
  };

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  beforeEach(() => {
    // Le mercredi 17/11/2027 : la prochaine séance du groupe d'Étude est le samedi 20/11.
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2027, 10, 17, 9));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'eleves/:id/inscription', component: PageDInscription, data: { depuis: 'eleve' } },
          { path: 'professeurs/groupes/:id/inscription', component: PageDInscription, data: { depuis: 'groupe' } },
          { path: 'eleves/:id/changement/:inscription', component: PageDInscription, data: { depuis: 'changement' } },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: DELAI_DE_RECHERCHE, useValue: 0 },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  async function depuisLEleve(groupes: GroupePossible[] = [MATHS, PHYSIQUE, ANGLAIS, INFORMATIQUE]): Promise<void> {
    harness = await RouterTestingHarness.create('/eleves/e1/inscription');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/eleves/e1'))).flush(ADEM);
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/inscriptions/groupes-possibles' && r.params.get('eleve') === 'e1'))).flush(
      groupes,
    );
    await vi.waitFor(() => expect(page.querySelector('h1')?.textContent).toContain('Inscrire Adem Miled'));
  }

  async function choisirLeGroupe(id: string, fiche: FicheDuGroupe): Promise<void> {
    await vi.waitFor(() => expect(page.querySelector(`input[name="groupe"][value="${id}"]`)).not.toBeNull());
    (page.querySelector(`input[name="groupe"][value="${id}"]`) as HTMLInputElement).click();
    (await vi.waitFor(() => http.expectOne(`/api/groupes/${id}`))).flush(fiche);
    await vi.waitFor(() => expect(page.querySelector('select[aria-label="Première séance"]')).not.toBeNull());
  }

  const bouton = (texte: string) =>
    [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte) as HTMLButtonElement | undefined;
  const premiereSeance = () => page.querySelector('select[aria-label="Première séance"]') as HTMLSelectElement;
  const texte = () => page.textContent ?? '';
  /** Le texte du choix (groupe ou élève) dont la case porte cette valeur. */
  const etiquette = (valeur: string) => (page.querySelector(`input[value="${valeur}"]`) as HTMLElement).closest('label')!.textContent!;

  async function inscrire(): Promise<TestRequest> {
    bouton('Inscrire')!.click();
    return vi.waitFor(() => http.expectOne({ method: 'POST', url: '/api/inscriptions' }));
  }

  // ----- Depuis la fiche de l'élève -----

  it("propose d'abord les groupes de son niveau", async () => {
    await depuisLEleve();

    const titres = [...page.querySelectorAll('.liste-de-groupes h3')].map((t) => t.textContent?.trim());
    expect(titres).toEqual(['Groupes de son niveau', 'Autres groupes']);
    const maths = etiquette('g1')!;
    expect(maths).toContain('Mathématiques · Groupe Samedi 10h → 12h');
    expect(maths).toContain('Karim Ben Salah · Étude · 12 élèves sur 15');
    expect(etiquette('g2')).toContain('Complet');
    const anglais = page.querySelector('input[value="g4"]') as HTMLInputElement;
    expect(anglais.disabled).toBe(true);
    expect(etiquette('g4')).toContain('Déjà inscrit');
  });

  it('choisit le groupe pris dans « Nouvel élève », avec l’avertissement de la création', async () => {
    harness = await RouterTestingHarness.create();
    void TestBed.inject(Router).navigateByUrl('/eleves/e1/inscription?groupe=g1', { state: { avertissement: 'Doublon possible : Adem Miled' } });
    (await vi.waitFor(() => http.expectOne('/api/eleves/e1'))).flush(ADEM);
    page = harness.routeNativeElement as HTMLElement;
    http.expectOne((r) => r.url === '/api/inscriptions/groupes-possibles').flush([MATHS, PHYSIQUE]);
    (await vi.waitFor(() => http.expectOne('/api/groupes/g1'))).flush(FICHE_MATHS);

    await vi.waitFor(() => expect(premiereSeance()).not.toBeNull());
    expect((page.querySelector('input[name="groupe"][value="g1"]') as HTMLInputElement).checked).toBe(true);
    expect(page.querySelector('.note.warn')?.textContent).toContain('Doublon possible : Adem Miled');
  });

  it("dit quand aucun groupe n'est ouvert", async () => {
    await depuisLEleve([]);

    expect(texte()).toContain("Aucun groupe ouvert pour l'instant.");
  });

  it('prévient quand le groupe choisi est complet, et propose les modes d’une Révision', async () => {
    await depuisLEleve();

    await choisirLeGroupe('g2', FICHE_PHYSIQUE);

    expect(page.querySelector('.note.warn')?.textContent).toContain("Ce groupe a atteint sa capacité. L'inscription reste possible.");
    const modes = [...page.querySelectorAll('input[name="mode"]')].map((m) => m.parentElement?.textContent?.trim());
    expect(modes).toEqual(['À la séance', 'En totalité']);
  });

  it('propose les modes d’une Formation à tranches', async () => {
    await depuisLEleve();

    await choisirLeGroupe('g3', FICHE_INFORMATIQUE);

    const modes = [...page.querySelectorAll('input[name="mode"]')].map((m) => m.parentElement?.textContent?.trim());
    expect(modes).toEqual(['En une fois', 'En 3 tranches']);
  });

  it('inscrit dans une Étude, sans choix de mode, à la prochaine séance', async () => {
    await depuisLEleve();
    await choisirLeGroupe('g1', FICHE_MATHS);

    expect(page.querySelector('input[name="mode"]')).toBeNull();
    expect(premiereSeance().value).toBe('2027-11-20');
    expect([...premiereSeance().options].map((o) => o.textContent?.trim())[1]).toBe('samedi 20/11/2027 · 10h');

    const requete = await inscrire();
    expect(requete.request.body).toEqual({ eleveId: 'e1', groupeId: 'g1', mode: null, debutLe: '2027-11-20' });
    await vi.waitFor(() => expect(bouton('Inscrire')?.disabled).toBe(true));
    requete.flush(ENREGISTREMENT);

    await vi.waitFor(() => expect(texte()).toContain('Inscription enregistrée'));
    expect(texte()).toContain('Adem Miled');
    expect(texte()).toContain('À payer à sa première séance, le samedi 20/11/2027');
    const sommes = [...page.querySelectorAll('.somme')].map((s) => [s.querySelector('span')?.textContent?.trim(), s.querySelector('b')?.textContent?.trim()]);
    expect(sommes).toEqual([
      ['Mois 3 (3 séances)', '30,000 TND'],
      ['Assurance 2027-2028', '20,000 TND'],
    ]);
    expect(page.querySelector('.texte-du-message')?.textContent).toContain("l'inscription d'Adem Miled");
    const destinataires = [...page.querySelectorAll('.destinataire')];
    expect(destinataires.map((d) => d.querySelector('span')?.textContent?.trim())).toEqual(['Au parent · 98 401 220', 'À Adem · 22 345 678']);
    expect(destinataires.map((d) => d.querySelector('a')?.getAttribute('href'))).toEqual([
      'https://wa.me/21698401220?text=Bonjour',
      'https://wa.me/21622345678?text=Bonjour',
    ]);
    expect(page.querySelector('.actions a[href="/eleves/e1"]')?.textContent).toContain("Voir la fiche de l'élève");
    expect(page.querySelector('.actions a[href="/professeurs/groupes/g1"]')?.textContent).toContain('Voir le groupe');
  });

  it('confirme sans rien à payer ni téléphone, avec l’avertissement en orange', async () => {
    await depuisLEleve();
    await choisirLeGroupe('g1', FICHE_MATHS);

    (await inscrire()).flush({ ...ENREGISTREMENT, aPayerALArrivee: [], messages: [], avertissement: "Ce groupe a atteint sa capacité. L'inscription reste possible." });

    await vi.waitFor(() => expect(texte()).toContain('Rien à payer à sa première séance.'));
    expect(texte()).toContain('Aucun téléphone pour envoyer la confirmation.');
    expect(page.querySelector('.note.warn')?.textContent).toContain('Ce groupe a atteint sa capacité.');
  });

  it("propose la dernière séance quand il n'y en a plus à venir", async () => {
    vi.setSystemTime(new Date(2027, 11, 20, 9));
    await depuisLEleve();

    await choisirLeGroupe('g1', FICHE_MATHS);

    expect(premiereSeance().value).toBe('2027-12-11');
  });

  it('demande de choisir le groupe, puis le mode', async () => {
    await depuisLEleve();

    bouton('Inscrire')!.click();
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain("Choisissez l'élève et le groupe"));

    await choisirLeGroupe('g2', FICHE_PHYSIQUE);
    bouton('Inscrire')!.click();
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('Choisissez le mode de tarification'));
    http.expectNone({ method: 'POST', url: '/api/inscriptions' });
  });

  it('envoie le mode et la séance choisis, et montre le refus du serveur', async () => {
    await depuisLEleve();
    await choisirLeGroupe('g2', FICHE_PHYSIQUE);
    (page.querySelector('input[name="mode"][value="EnTotalite"]') as HTMLInputElement).click();
    premiereSeance().value = '2027-11-13';
    premiereSeance().dispatchEvent(new Event('change'));

    const requete = await inscrire();
    expect(requete.request.body).toEqual({ eleveId: 'e1', groupeId: 'g2', mode: 'EnTotalite', debutLe: '2027-11-13' });
    requete.flush({ message: 'Cet élève est déjà inscrit dans ce groupe' }, { status: 409, statusText: 'Conflict' });

    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('Cet élève est déjà inscrit dans ce groupe'));
    expect(bouton('Inscrire')?.disabled).toBe(false);
  });

  // ----- Depuis le groupe -----

  async function depuisLeGroupe(fiche: FicheDuGroupe = FICHE_MATHS): Promise<void> {
    harness = await RouterTestingHarness.create('/professeurs/groupes/g1/inscription');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/groupes/g1'))).flush(fiche);
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/inscriptions' && r.params.get('groupe') === 'g1'))).flush([INSCRIPTION]);
    await vi.waitFor(() => expect(page.querySelector('h1')?.textContent).toContain('Ajouter un élève'));
    await vi.waitFor(() => expect(page.querySelector('.sub')?.textContent).toContain('Groupe Samedi 10h → 12h'));
  }

  it('crée un nouvel élève depuis le groupe, au niveau du groupe, le choisit, et l’inscrit', async () => {
    await depuisLeGroupe();

    bouton('Nouvel élève')!.click();
    (await vi.waitFor(() => http.expectOne('/api/niveaux'))).flush([
      ...Array.from({ length: 8 }, (_, i) => ({ id: `n${i + 1}`, nom: `${i + 1}ᵉ année de base`, dOffice: true })),
      { id: 'n9', nom: '9ᵉ année de base', dOffice: true },
    ]);
    await vi.waitFor(() => expect(page.querySelector('[formcontrolname="prenom"]')).not.toBeNull());
    await vi.waitFor(() => expect((page.querySelector('form button[type="submit"]') as HTMLButtonElement).disabled).toBe(false));
    // Le niveau vient du groupe : il ne se choisit pas.
    expect(page.querySelector('[formcontrolname="niveauId"]')).toBeNull();
    expect((page.querySelector('input[name="niveau-du-groupe"]') as HTMLInputElement).value).toBe('9ᵉ année de base');
    saisir(page, '[formcontrolname="prenom"]', 'Nour');
    saisir(page, '[formcontrolname="nom"]', 'Hamdi');
    saisir(page, '[formcontrolname="telephoneEleve"]', '22 000 111');
    saisir(page, '[formcontrolname="emailEleve"]', 'nour@exemple.tn');
    saisir(page, '[formcontrolname="telephoneParent"]', '98 000 111');
    (page.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    const creation = await vi.waitFor(() => http.expectOne({ method: 'POST', url: '/api/eleves' }));
    expect(creation.request.body.niveauId).toBe('n9');
    creation.flush(
      { eleve: { id: 'e9', prenom: 'Nour', nom: 'Hamdi', niveau: { id: 'n9', nom: '9ᵉ année de base' } }, avertissement: null },
      { status: 201, statusText: 'Created' },
    );
    await vi.waitFor(() => expect((page.querySelector('input[name="eleve"][value="e9"]') as HTMLInputElement)?.checked).toBe(true));
    expect(etiquette('e9')).toContain('Nour Hamdi');

    const requete = await inscrire();
    expect(requete.request.body).toEqual({ eleveId: 'e9', groupeId: 'g1', mode: null, debutLe: '2027-11-20' });
    requete.flush({ ...ENREGISTREMENT, inscription: { ...INSCRIPTION, eleve: { id: 'e9', prenom: 'Nour', nom: 'Hamdi' } } });
    await vi.waitFor(() => expect(texte()).toContain('Inscription enregistrée'));
  });

  it('cherche un élève, marque celui qui est déjà inscrit, et inscrit celui qu’on choisit', async () => {
    await depuisLeGroupe();
    expect(page.querySelector('.sub')?.textContent).toContain('Groupe Samedi 10h → 12h · Mathématiques');

    saisir(page, '[aria-label="Chercher un élève"]', 'mi');
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/eleves' && r.params.get('recherche') === 'mi'))).flush([
      { id: 'e1', prenom: 'Adem', nom: 'Miled', niveau: '9ᵉ année de base' },
      { id: 'e2', prenom: 'Amira', nom: 'Miladi', niveau: '9ᵉ année de base' },
    ]);
    await vi.waitFor(() => expect(page.querySelector('input[name="eleve"][value="e2"]')).not.toBeNull());
    expect((page.querySelector('input[name="eleve"][value="e1"]') as HTMLInputElement).disabled).toBe(true);
    expect(etiquette('e1')).toContain('Déjà inscrit');

    (page.querySelector('input[name="eleve"][value="e2"]') as HTMLInputElement).click();
    const requete = await inscrire();
    expect(requete.request.body).toEqual({ eleveId: 'e2', groupeId: 'g1', mode: null, debutLe: '2027-11-20' });
    requete.flush({ ...ENREGISTREMENT, inscription: { ...INSCRIPTION, eleve: { id: 'e2', prenom: 'Amira', nom: 'Miladi' } } });
    await vi.waitFor(() => expect(texte()).toContain('Inscription enregistrée'));
    expect(page.querySelector('.resume b')?.textContent).toBe('Amira Miladi');
    expect(page.querySelector('.actions a[href="/eleves/e2"]')).not.toBeNull();
  });

  it("oublie l'élève choisi quand on cherche autre chose : on n'inscrit pas un élève qu'on ne voit plus", async () => {
    await depuisLeGroupe();
    saisir(page, '[aria-label="Chercher un élève"]', 'adem');
    (await vi.waitFor(() => http.expectOne((r) => r.params.get('recherche') === 'adem'))).flush([
      { id: 'e2', prenom: 'Adem', nom: 'Miled', niveau: '7ᵉ année de base' },
      { id: 'e3', prenom: 'Adem', nom: 'Miled', niveau: '9ᵉ année de base' },
    ]);
    await vi.waitFor(() => expect(page.querySelector('input[name="eleve"][value="e2"]')).not.toBeNull());
    (page.querySelector('input[name="eleve"][value="e2"]') as HTMLInputElement).click();

    saisir(page, '[aria-label="Chercher un élève"]', '22 345');
    (await vi.waitFor(() => http.expectOne((r) => r.params.get('recherche') === '22 345'))).flush([
      { id: 'e3', prenom: 'Adem', nom: 'Miled', niveau: '9ᵉ année de base' },
    ]);
    await vi.waitFor(() => expect(page.querySelector('input[name="eleve"][value="e2"]')).toBeNull());
    bouton('Inscrire')!.click();

    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain("Choisissez l'élève et le groupe"));
    http.expectNone({ method: 'POST', url: '/api/inscriptions' });
  });

  it("n'inscrit plus dans un groupe fermé", async () => {
    await depuisLeGroupe({ ...FICHE_MATHS, fermeLe: '2027-11-15', motifDeFermeture: "Trop peu d'élèves" });

    expect(texte()).toContain("Ce groupe est fermé : on n'y inscrit plus");
    expect(bouton('Inscrire')).toBeUndefined();
  });

  // ----- Changer de groupe (ELV-09) -----

  it("change l'élève de groupe, et montre le report", async () => {
    harness = await RouterTestingHarness.create('/eleves/e1/changement/i1');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/eleves/e1'))).flush(ADEM);
    http.expectOne((r) => r.url === '/api/inscriptions' && r.params.get('eleve') === 'e1').flush([{ ...INSCRIPTION, id: 'i1', groupe: { ...INSCRIPTION.groupe, id: 'g9' } }]);
    http.expectOne((r) => r.url === '/api/inscriptions/groupes-possibles' && r.params.get('eleve') === 'e1' && r.params.get('depuis') === 'i1').flush([MATHS]);
    await vi.waitFor(() => expect(page.querySelector('h1')?.textContent).toContain('Changer Adem Miled de groupe'));
    expect(page.querySelector('.sub')?.textContent).toContain('Depuis Groupe Samedi 10h → 12h · Mathématiques');

    await choisirLeGroupe('g1', FICHE_MATHS);
    bouton('Changer de groupe')!.click();
    const requete = await vi.waitFor(() => http.expectOne({ method: 'POST', url: '/api/inscriptions/i1/changement' }));
    expect(requete.request.body).toEqual({ groupeId: 'g1', mode: null, debutLe: '2027-11-20' });
    requete.flush({ ...ENREGISTREMENT, inscription: { ...INSCRIPTION, report: 30 } });

    await vi.waitFor(() => expect(texte()).toContain('Changement enregistré'));
    expect(texte()).toContain("Report de 30,000 TND depuis l'ancien groupe");
  });

  it("ne marque pas « Déjà inscrit » un élève dont l'inscription est arrêtée", async () => {
    harness = await RouterTestingHarness.create('/professeurs/groupes/g1/inscription');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/groupes/g1'))).flush(FICHE_MATHS);
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/inscriptions' && r.params.get('groupe') === 'g1'))).flush([
      { ...INSCRIPTION, arreteLe: '2027-11-27' },
    ]);
    await vi.waitFor(() => expect(page.querySelector('.sub')?.textContent).toContain('Groupe Samedi 10h → 12h'));

    saisir(page, '[aria-label="Chercher un élève"]', 'adem');
    (await vi.waitFor(() => http.expectOne((r) => r.params.get('recherche') === 'adem'))).flush([
      { id: 'e1', prenom: 'Adem', nom: 'Miled', niveau: '9ᵉ année de base' },
    ]);

    await vi.waitFor(() => expect(page.querySelector('input[name="eleve"][value="e1"]')).not.toBeNull());
    expect((page.querySelector('input[name="eleve"][value="e1"]') as HTMLInputElement).disabled).toBe(false);
  });
});
