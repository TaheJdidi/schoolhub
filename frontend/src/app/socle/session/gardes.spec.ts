import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanMatchFn,
  provideRouter,
  Route,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { AMIRA, HEDI } from '../../test/personnes';
import { estConnecte, estProprietaire, nonConnecte } from './gardes';

describe('les gardes de navigation', () => {
  let http: HttpTestingController;
  const route = {} as ActivatedRouteSnapshot;
  const versLaCaisse = { url: '/caisse' } as RouterStateSnapshot;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const adresse = (resultat: unknown) => TestBed.inject(Router).serializeUrl(resultat as UrlTree);
  const nonConnecteAuServeur = () =>
    http.expectOne('/api/moi').flush(null, { status: 401, statusText: 'Unauthorized' });

  describe('estConnecte', () => {
    it('sans session, mène à la connexion en gardant la page demandée', async () => {
      const resultat = TestBed.runInInjectionContext(() => estConnecte(route, versLaCaisse));
      nonConnecteAuServeur();

      expect(adresse(await resultat)).toBe('/connexion?retour=%2Fcaisse');
    });

    it('avec une session, laisse passer', async () => {
      const resultat = TestBed.runInInjectionContext(() => estConnecte(route, versLaCaisse));
      http.expectOne('/api/moi').flush(AMIRA);

      expect(await resultat).toBe(true);
    });
  });

  describe('estProprietaire', () => {
    const journal: Route = { path: 'journal' };
    const instantane = {} as Parameters<CanMatchFn>[2];

    it('charge elle-même la session, car elle passe avant la garde du cadre', async () => {
      const resultat = TestBed.runInInjectionContext(() => estProprietaire(journal, [], instantane));
      http.expectOne('/api/moi').flush(HEDI);

      expect(await resultat).toBe(true);
    });

    it('refuse une secrétaire', async () => {
      const resultat = TestBed.runInInjectionContext(() => estProprietaire(journal, [], instantane));
      http.expectOne('/api/moi').flush(AMIRA);

      expect(await resultat).toBe(false);
    });
  });

  describe('nonConnecte', () => {
    it("envoie une personne déjà connectée sur « Aujourd'hui »", async () => {
      const resultat = TestBed.runInInjectionContext(() => nonConnecte(route, versLaCaisse));
      http.expectOne('/api/moi').flush(AMIRA);

      expect(adresse(await resultat)).toBe('/aujourdhui');
    });

    it('laisse passer sans session', async () => {
      const resultat = TestBed.runInInjectionContext(() => nonConnecte(route, versLaCaisse));
      nonConnecteAuServeur();

      expect(await resultat).toBe(true);
    });
  });
});
