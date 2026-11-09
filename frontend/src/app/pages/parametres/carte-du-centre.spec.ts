import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { CarteDuCentre, Reglages } from './carte-du-centre';

describe('la carte « Le centre »', () => {
  const ADRESSE = '/api/centre/parametres';
  const REGLAGES: Reglages = {
    nom: 'Centre Les Jasmins',
    telephone: '71 234 567',
    email: 'contact@jasmins.tn',
    anneeScolaire: { debut: '2027-09-15', fin: '2028-06-30' },
    montantDeLAssurance: 20,
    prixDeLEntreeALaBibliotheque: 2.5,
    capaciteDeLaBibliotheque: 40,
    partDuCentreParDefaut: 5,
  };

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  async function ouvrir(reglages: Reglages): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'carte', component: CarteDuCentre },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/carte');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne(ADRESSE))).flush(reglages);
    // Un centre qui a son année scolaire s'ouvre en lecture : « Modifier » ouvre les champs.
    if (reglages.anneeScolaire) await modifier();
    await vi.waitFor(() => expect(champ('nom').value).toBe(reglages.nom));
  }

  async function modifier(): Promise<void> {
    (await vi.waitFor(() => bouton('Modifier'))).click();
    await harness.fixture.whenStable();
  }

  afterEach(() => http.verify());

  const bouton = (texte: string) => {
    const trouve = [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte);
    if (!trouve) throw new Error(`Aucun bouton « ${texte} »`);
    return trouve as HTMLButtonElement;
  };

  const champ = (nom: string) => page.querySelector(`[formcontrolname="${nom}"]`) as HTMLInputElement;
  const attendre = (texte: string) => vi.waitFor(() => expect(page.textContent).toContain(texte));
  const enregistrement = () => http.expectOne((r) => r.method === 'PUT' && r.url === ADRESSE);

  it('montre les réglages enregistrés en lecture, avec « Modifier »', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'carte', component: CarteDuCentre }]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/carte');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne(ADRESSE))).flush(REGLAGES);
    await attendre('15/09/2027 → 30/06/2028');

    expect(page.querySelector('input')).toBeNull();
    const lecture = page.querySelector('.lecture')!.textContent!;
    for (const texte of ['Centre Les Jasmins', '71 234 567', '20,000 TND', '5 %', '2,500 TND', '40 places']) expect(lecture).toContain(texte);

    await modifier();
    expect(champ('nom').value).toBe('Centre Les Jasmins');
    saisir(page, '[formcontrolname="nom"]', 'Autre nom');
    bouton('Annuler').click();
    await harness.fixture.whenStable();
    // « Annuler » referme les champs sans rien envoyer, et oublie la saisie.
    expect(page.querySelector('.lecture')?.textContent).toContain('Centre Les Jasmins');
    http.expectNone((r) => r.method === 'PUT');
  });

  it("dit où en est l'année scolaire, vue d'un jour donné", () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const carte = TestBed.runInInjectionContext(() => new CarteDuCentre());
    const etat = (aujourdhui: string) =>
      (carte as unknown as { etatDeLAnnee(d: string, f: string, a: Date): { texte: string; ton: string; avance: number } })
        .etatDeLAnnee('2027-09-15', '2028-06-30', new Date(`${aujourdhui}T10:00:00`));

    expect(etat('2027-09-01')).toEqual({ texte: 'Commence le 15/09/2027', ton: 'muted', avance: 0 });
    expect(etat('2027-09-15')).toMatchObject({ texte: 'En cours · semaine 1 sur 42', ton: 'info', avance: 0 });
    expect(etat('2027-09-22').texte).toBe('En cours · semaine 2 sur 42');
    expect(etat('2028-06-30')).toEqual({ texte: "Se termine aujourd'hui", ton: 'warn', avance: 100 });
    expect(etat('2028-07-01')).toEqual({ texte: 'Terminée', ton: 'warn', avance: 100 });
  });

  it('remplit le formulaire avec les réglages', async () => {
    await ouvrir(REGLAGES);

    expect(champ('telephone').value).toBe('71 234 567');
    expect(champ('email').value).toBe('contact@jasmins.tn');
    expect(champ('debut').value).toBe('2027-09-15');
    expect(champ('fin').value).toBe('2028-06-30');
    expect(champ('assurance').value).toBe('20,000');
    expect(champ('entree').value).toBe('2,500');
    expect(champ('capacite').value).toBe('40');
    expect(champ('part').value).toBe('5');
    expect(page.textContent).not.toContain('Sans année scolaire');
  });

  it('envoie les montants en nombre et les champs vides en null', async () => {
    await ouvrir(REGLAGES);
    saisir(page, '[formcontrolname="telephone"]', '');
    saisir(page, '[formcontrolname="email"]', ' ');
    saisir(page, '[formcontrolname="capacite"]', '');
    saisir(page, '[formcontrolname="assurance"]', '20');
    saisir(page, '[formcontrolname="entree"]', '2,5');
    saisir(page, '[formcontrolname="part"]', '7,5');
    envoyer(page);

    const requete = enregistrement();
    expect(requete.request.body).toEqual({
      nom: 'Centre Les Jasmins',
      telephone: null,
      email: null,
      anneeScolaire: { debut: '2027-09-15', fin: '2028-06-30' },
      montantDeLAssurance: 20,
      prixDeLEntreeALaBibliotheque: 2.5,
      capaciteDeLaBibliotheque: null,
      partDuCentreParDefaut: 7.5,
    });
    requete.flush(REGLAGES);
    await attendre('Paramètres enregistrés');
  });

  it("une seule date part avec l'autre à null, et deux dates vides partent en null", async () => {
    await ouvrir(REGLAGES);
    saisir(page, '[formcontrolname="fin"]', '');
    envoyer(page);
    const uneDate = enregistrement();
    expect(uneDate.request.body.anneeScolaire).toEqual({ debut: '2027-09-15', fin: null });
    uneDate.flush(
      { message: "L'année scolaire doit avoir une date de début et une date de fin" },
      { status: 400, statusText: 'Bad Request' },
    );
    await attendre("L'année scolaire doit avoir une date de début et une date de fin");

    saisir(page, '[formcontrolname="debut"]', '');
    envoyer(page);
    const aucuneDate = enregistrement();
    expect(aucuneDate.request.body.anneeScolaire).toBeNull();
    aucuneDate.flush(REGLAGES);
    await attendre('Paramètres enregistrés');
  });

  it('un montant illisible ne part pas', async () => {
    await ouvrir(REGLAGES);
    saisir(page, '[formcontrolname="assurance"]', 'deux');
    envoyer(page);

    await attendre('Écrivez un montant, par exemple 2,500');
    http.expectNone((r) => r.method === 'PUT');
  });

  it("rappelle qu'il faut une année scolaire", async () => {
    await ouvrir({ ...REGLAGES, anneeScolaire: null });

    expect(page.textContent).toContain("Sans année scolaire, on ne peut pas créer de groupe d'Étude.");
  });

  it('confirme sous la carte, et montre le refus sous la carte', async () => {
    await ouvrir(REGLAGES);
    envoyer(page);
    enregistrement().flush(REGLAGES);
    await attendre('Paramètres enregistrés');
    expect(page.querySelector('.card [role="status"]')?.textContent).toContain('Paramètres enregistrés');
    // Enregistrés, les réglages repassent en lecture.
    expect(page.querySelector('.lecture')).not.toBeNull();

    await modifier();
    saisir(page, '[formcontrolname="telephone"]', '1234');
    envoyer(page);
    enregistrement().flush({ message: 'Le téléphone doit avoir 8 chiffres' }, { status: 400, statusText: 'Bad Request' });
    await attendre('Le téléphone doit avoir 8 chiffres');
    expect(page.querySelector('.card [role="alert"]')?.textContent).toContain('Le téléphone doit avoir 8 chiffres');
    expect(page.textContent).not.toContain('Paramètres enregistrés');
  });

  it("n'enregistre rien tant que les réglages ne sont pas chargés, et propose de réessayer", async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'carte', component: CarteDuCentre },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/carte');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne(ADRESSE))).error(new ProgressEvent('error'));
    await attendre('Pas de connexion à Internet : réessayez.');

    // Un formulaire vide enregistré effacerait l'assurance, les prix et la part du centre : il ne s'ouvre pas.
    expect(page.querySelector('button[type="submit"]')).toBeNull();
    http.expectNone((r) => r.method === 'PUT');

    const reessayer = [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Réessayer');
    reessayer!.click();
    (await vi.waitFor(() => http.expectOne(ADRESSE))).flush(REGLAGES);
    await modifier();
    await vi.waitFor(() => expect(champ('nom').value).toBe('Centre Les Jasmins'));
    expect((page.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBe(false);
    expect(page.textContent).not.toContain('Pas de connexion');
  });

  it("le bouton se bloque pendant l'envoi", async () => {
    await ouvrir(REGLAGES);
    envoyer(page);
    envoyer(page);
    await harness.fixture.whenStable();

    const bouton = page.querySelector('button[type="submit"]') as HTMLButtonElement;
    expect(bouton.textContent).toContain('Enregistrement…');
    expect(bouton.disabled).toBe(true);
    // Une seule requête est partie : expectOne échouerait s'il y en avait deux.
    enregistrement().flush(REGLAGES);
  });
});
