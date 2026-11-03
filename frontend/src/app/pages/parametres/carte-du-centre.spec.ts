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
    await vi.waitFor(() => expect(champ('nom').value).toBe(reglages.nom));
  }

  afterEach(() => http.verify());

  const champ = (nom: string) => page.querySelector(`[formcontrolname="${nom}"]`) as HTMLInputElement;
  const attendre = (texte: string) => vi.waitFor(() => expect(page.textContent).toContain(texte));
  const enregistrement = () => http.expectOne((r) => r.method === 'PUT' && r.url === ADRESSE);

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

    saisir(page, '[formcontrolname="telephone"]', '1234');
    envoyer(page);
    enregistrement().flush({ message: 'Le téléphone doit avoir 8 chiffres' }, { status: 400, statusText: 'Bad Request' });
    await attendre('Le téléphone doit avoir 8 chiffres');
    expect(page.querySelector('.card [role="alert"]')?.textContent).toContain('Le téléphone doit avoir 8 chiffres');
    expect(page.textContent).not.toContain('Paramètres enregistrés');
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
