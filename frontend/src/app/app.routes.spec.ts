import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { Moi } from './socle/session/moi';
import { intercepteurDeSession } from './socle/session/intercepteur';
import { AMIRA, HEDI } from './test/personnes';

describe("les adresses de l'application", () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(withInterceptors([intercepteurDeSession])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  /** Ouvre une adresse, comme au premier chargement : la session est demandée au serveur, qui répond `moi`. */
  async function ouvrir(adresse: string, moi: Moi): Promise<void> {
    const navigation = harness.navigateByUrl(adresse);
    const requete = await vi.waitFor(() => http.expectOne('/api/moi'));
    requete.flush(moi);
    await navigation;
    await harness.fixture.whenStable();
  }

  const texte = () => (harness.fixture.nativeElement as HTMLElement).textContent ?? '';
  const adresseActuelle = () => TestBed.inject(Router).url;
  const titre = () => TestBed.inject(Title).getTitle();

  it('une secrétaire lit « Accès réservé au propriétaire » sur /journal', async () => {
    await ouvrir('/journal', AMIRA);

    expect(texte()).toContain('Accès réservé au propriétaire');
    expect(adresseActuelle()).toBe('/journal');
    expect(titre()).toBe('Accès réservé · SchoolHub');
  });

  it('le propriétaire qui ouvre /journal en premier voit sa page', async () => {
    await ouvrir('/journal', HEDI);

    expect(texte()).toContain('Journal');
    expect(texte()).toContain('Cette page arrive bientôt');
    expect(texte()).not.toContain('Accès réservé');
  });

  it('le propriétaire voit la page « Comptes » sur /comptes', async () => {
    await ouvrir('/comptes', HEDI);
    http.expectOne('/api/comptes').flush([]);
    await harness.fixture.whenStable();

    expect(texte()).toContain('Inviter une personne');
    expect(texte()).not.toContain('Cette page arrive bientôt');
    expect(titre()).toBe('Comptes · SchoolHub');
  });

  it('une secrétaire lit « Accès réservé au propriétaire » sur /comptes', async () => {
    await ouvrir('/comptes', AMIRA);

    expect(texte()).toContain('Accès réservé au propriétaire');
  });

  it("/ mène à « Aujourd'hui »", async () => {
    await ouvrir('/', AMIRA);

    expect(adresseActuelle()).toBe('/aujourdhui');
    expect(texte()).toContain('Bonjour Amira');
    expect(texte()).toContain("Les séances du jour s'afficheront ici.");
    expect(titre()).toBe("Aujourd'hui · SchoolHub");
  });

  it('une page pas encore faite le dit', async () => {
    await ouvrir('/caisse', AMIRA);

    expect(texte()).toContain('Caisse');
    expect(texte()).toContain('Cette page arrive bientôt');
    expect(titre()).toBe('Caisse · SchoolHub');
  });

  it("une personne déjà connectée qui ouvre la connexion passe à « Aujourd'hui »", async () => {
    await ouvrir('/connexion', AMIRA);

    expect(adresseActuelle()).toBe('/aujourdhui');
  });

  it("sans session, la connexion s'affiche sans parler de session expirée", async () => {
    const navigation = harness.navigateByUrl('/connexion');
    const requete = await vi.waitFor(() => http.expectOne('/api/moi'));
    requete.flush(null, { status: 401, statusText: 'Unauthorized' });
    await navigation;
    await harness.fixture.whenStable();

    expect(texte()).toContain('Se connecter');
    expect(texte()).not.toContain('expiré');
    expect(titre()).toBe('Connexion · SchoolHub');
  });

  it("une adresse inconnue dit que la page n'existe pas", async () => {
    await ouvrir('/nimporte', AMIRA);

    expect(texte()).toContain("Cette page n'existe pas");
    expect(titre()).toBe('Page introuvable · SchoolHub');
  });
});
