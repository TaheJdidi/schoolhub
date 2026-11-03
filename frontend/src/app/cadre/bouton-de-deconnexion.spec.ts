import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Session } from '../socle/session/session';
import { PageDeTest } from '../test/page-de-test';
import { AMIRA } from '../test/personnes';
import { BoutonDeDeconnexion } from './bouton-de-deconnexion';

describe('« Se déconnecter »', () => {
  let http: HttpTestingController;
  let session: Session;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'connexion', component: PageDeTest },
          { path: '**', component: BoutonDeDeconnexion },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    session = TestBed.inject(Session);
    session.connaitre(AMIRA);
    harness = await RouterTestingHarness.create('/caisse');
  });

  afterEach(() => http.verify());

  function cliquer(): void {
    (harness.routeNativeElement as HTMLElement).querySelector('button')?.click();
  }

  it.each([
    [204, 'No Content'],
    [401, 'Unauthorized'],
  ])('mène à la connexion (%s)', async (status, statusText) => {
    cliquer();
    http.expectOne('/api/deconnexion').flush(null, { status, statusText });

    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/connexion'));
    expect(session.personne()).toBeNull();
  });

  it('sans Internet, la personne reste et lit le message', async () => {
    cliquer();
    http.expectOne('/api/deconnexion').error(new ProgressEvent('error'), { status: 0 });

    await vi.waitFor(() =>
      expect(harness.routeNativeElement?.textContent).toContain('Pas de connexion à Internet : réessayez.'),
    );
    expect(TestBed.inject(Router).url).toBe('/caisse');
    expect(session.personne()).toEqual(AMIRA);
  });
});
