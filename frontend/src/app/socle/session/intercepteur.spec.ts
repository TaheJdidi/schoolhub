import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { AMIRA } from '../../test/personnes';
import { intercepteurDeSession } from './intercepteur';
import { Session } from './session';

describe('intercepteurDeSession', () => {
  let http: HttpTestingController;
  let session: Session;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', component: PageDeTest }]),
        provideHttpClient(withInterceptors([intercepteurDeSession])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    session = TestBed.inject(Session);
    harness = await RouterTestingHarness.create('/caisse');
    session.connaitre(AMIRA);
  });

  afterEach(() => http.verify());

  /** Fait un appel qui reçoit la réponse donnée, et renvoie l'erreur reçue par l'appelant. */
  async function appeler(methode: 'GET' | 'POST', adresse: string, status: number): Promise<unknown> {
    let recue: unknown;
    const client = TestBed.inject(HttpClient);
    const appel = methode === 'GET' ? client.get(adresse) : client.post(adresse, {});
    appel.subscribe({ error: (erreur) => (recue = erreur) });
    http.expectOne(adresse).flush(null, { status, statusText: 'Erreur' });
    await harness.fixture.whenStable();
    return recue;
  }

  it('une session tombée pendant le travail ramène à la connexion, avec la page de départ', async () => {
    const recue = await appeler('GET', '/api/eleves', 401);

    expect(session.personne()).toBeNull();
    expect(TestBed.inject(Router).url).toBe('/connexion?retour=%2Fcaisse&expiree=1');
    expect(recue).toBeInstanceOf(HttpErrorResponse);
  });

  it.each([
    ['GET', '/api/moi'],
    ['POST', '/api/connexion'],
    ['POST', '/api/deconnexion'],
    ['POST', '/api/invitation'],
    ['POST', '/api/mot-de-passe-oublie'],
    ['POST', '/api/nouveau-mot-de-passe'],
  ] as const)('un 401 de %s %s est un refus normal', async (methode, adresse) => {
    await appeler(methode, adresse, 401);

    expect(TestBed.inject(Router).url).toBe('/caisse');
  });

  it("une autre erreur ne touche pas à la session", async () => {
    await appeler('GET', '/api/eleves', 500);

    expect(session.personne()).toEqual(AMIRA);
    expect(TestBed.inject(Router).url).toBe('/caisse');
  });
});
