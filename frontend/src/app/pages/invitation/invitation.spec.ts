import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Session } from '../../socle/session/session';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { AMIRA } from '../../test/personnes';
import { Invitation } from './invitation';

describe("la page d'invitation", () => {
  const LIEN_EXPIRE = 'Ce lien a expiré : demandez au propriétaire une nouvelle invitation';

  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            { path: 'invitation', component: Invitation },
            { path: '**', component: PageDeTest },
          ],
          withComponentInputBinding(),
        ),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  /** Ouvre le lien de l'email ; le serveur accueille la personne (ou refuse le lien). */
  async function ouvrir(adresse = '/invitation?compte=c1&jeton=j%2B1', etat?: object): Promise<HTMLElement> {
    await TestBed.inject(Router).navigateByUrl(adresse, { state: etat });
    await harness.fixture.whenStable();
    return harness.routeNativeElement as HTMLElement;
  }

  async function accueillir(page: HTMLElement, adresseDeLApi = '/api/invitation?compte=c1&jeton=j%2B1') {
    http.expectOne(adresseDeLApi).flush({ prenom: 'Amira', centre: 'Centre Les Jasmins' });
    await vi.waitFor(() => expect(page.querySelector('form')).not.toBeNull());
  }

  function choisir(page: HTMLElement, motDePasse: string): void {
    saisir(page, 'input[autocomplete="new-password"]', motDePasse);
    envoyer(page);
  }

  it('accueille la personne du lien, et le lien quitte la barre d\'adresse', async () => {
    const page = await ouvrir();
    // Le + du jeton part bien en %2B : sinon le serveur lirait une espace, et le lien serait refusé.
    await accueillir(page);

    expect(page.textContent).toContain('Bonjour Amira, bienvenue au Centre Les Jasmins');
    await vi.waitFor(() => expect(TestBed.inject(Location).path()).toBe('/invitation'));
  });

  it('un lien expiré affiche son message, sans formulaire', async () => {
    const page = await ouvrir();
    http
      .expectOne('/api/invitation?compte=c1&jeton=j%2B1')
      .flush({ message: LIEN_EXPIRE }, { status: 410, statusText: 'Gone' });

    await vi.waitFor(() => expect(page.textContent).toContain(LIEN_EXPIRE));
    expect(page.querySelector('input[type="password"]')).toBeNull();
  });

  it('après un rechargement, le lien sert encore', async () => {
    // Le lien a quitté la barre d'adresse : il reste dans l'état de navigation, qui survit au rechargement.
    const page = await ouvrir('/invitation', { compte: 'c1', jeton: 'j1' });

    await accueillir(page, '/api/invitation?compte=c1&jeton=j1');
  });

  it('un mot de passe trop court ne part pas', async () => {
    const page = await ouvrir();
    await accueillir(page);

    choisir(page, '1234567');
    await harness.fixture.whenStable();

    expect(page.textContent).toContain('Le mot de passe doit faire au moins 8 caractères');
  });

  it("le bon mot de passe connecte et mène à « Aujourd'hui »", async () => {
    const page = await ouvrir();
    await accueillir(page);

    choisir(page, 'jasmins-2027');
    const requete = http.expectOne('/api/invitation');
    expect(requete.request.method).toBe('POST');
    expect(requete.request.body).toEqual({ compte: 'c1', jeton: 'j+1', motDePasse: 'jasmins-2027' });
    requete.flush(AMIRA);

    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/aujourdhui'));
    expect(TestBed.inject(Session).personne()).toEqual(AMIRA);
  });

  it("ne part qu'une fois", async () => {
    const page = await ouvrir();
    await accueillir(page);

    choisir(page, 'jasmins-2027');
    envoyer(page);
    await harness.fixture.whenStable();

    expect(page.querySelector('button[type="submit"]')?.textContent).toContain('Enregistrement…');
    http.expectOne((requete) => requete.method === 'POST').flush(AMIRA);
  });

  it('un lien utilisé entre-temps affiche son message', async () => {
    const page = await ouvrir();
    await accueillir(page);

    choisir(page, 'jasmins-2027');
    http
      .expectOne((requete) => requete.method === 'POST')
      .flush({ message: LIEN_EXPIRE }, { status: 410, statusText: 'Gone' });

    await vi.waitFor(() => expect(page.textContent).toContain(LIEN_EXPIRE));
    expect(page.querySelector('form')).toBeNull();
  });
});
