import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Session } from '../../socle/session/session';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { HEDI } from '../../test/personnes';
import { NouveauMotDePasse } from './nouveau-mot-de-passe';

describe('la page « Nouveau mot de passe »', () => {
  const LIEN_EXPIRE = 'Ce lien a expiré : refaites une demande de mot de passe oublié';

  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'nouveau-mot-de-passe', component: NouveauMotDePasse },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  async function ouvrir(adresse = '/nouveau-mot-de-passe?compte=c1&jeton=j1', etat?: object): Promise<HTMLElement> {
    await TestBed.inject(Router).navigateByUrl(adresse, { state: etat });
    await harness.fixture.whenStable();
    return harness.routeNativeElement as HTMLElement;
  }

  function choisir(page: HTMLElement, motDePasse: string): void {
    saisir(page, 'input[autocomplete="new-password"]', motDePasse);
    envoyer(page);
  }

  it("le lien quitte la barre d'adresse, sans appel à l'ouverture", async () => {
    await ouvrir();

    await vi.waitFor(() => expect(TestBed.inject(Location).path()).toBe('/nouveau-mot-de-passe'));
  });

  it('le nouveau mot de passe connecte', async () => {
    const page = await ouvrir();
    choisir(page, 'jasmins-2028');

    const requete = http.expectOne('/api/nouveau-mot-de-passe');
    expect(requete.request.method).toBe('POST');
    expect(requete.request.body).toEqual({ compte: 'c1', jeton: 'j1', motDePasse: 'jasmins-2028' });
    requete.flush(HEDI);

    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/aujourdhui'));
    expect(TestBed.inject(Session).personne()).toEqual(HEDI);
  });

  it('un mot de passe trop court ne part pas', async () => {
    const page = await ouvrir();
    choisir(page, 'court');
    await harness.fixture.whenStable();

    expect(page.textContent).toContain('Le mot de passe doit faire au moins 8 caractères');
  });

  it('un lien expiré propose de refaire une demande', async () => {
    const page = await ouvrir();
    choisir(page, 'jasmins-2028');
    http
      .expectOne('/api/nouveau-mot-de-passe')
      .flush({ message: LIEN_EXPIRE }, { status: 410, statusText: 'Gone' });

    await vi.waitFor(() => expect(page.textContent).toContain(LIEN_EXPIRE));
    expect(page.querySelector('form')).toBeNull();
    expect(page.querySelector('a[href="/mot-de-passe-oublie"]')?.textContent).toContain('Refaire une demande');
  });

  it('après un rechargement, le lien sert encore', async () => {
    const page = await ouvrir('/nouveau-mot-de-passe', { compte: 'c1', jeton: 'j1' });
    choisir(page, 'jasmins-2028');

    const requete = http.expectOne('/api/nouveau-mot-de-passe');
    expect(requete.request.body).toEqual({ compte: 'c1', jeton: 'j1', motDePasse: 'jasmins-2028' });
    requete.flush(HEDI);
  });
});
