import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { MotDePasseOublie } from './mot-de-passe-oublie';

describe('la page « Mot de passe oublié »', () => {
  const LIEN_ENVOYE = 'Si un compte existe pour cet email, un lien vient de partir';

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'mot-de-passe-oublie', component: MotDePasseOublie },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/mot-de-passe-oublie');
    await harness.fixture.whenStable();
    page = harness.routeNativeElement as HTMLElement;
  });

  afterEach(() => http.verify());

  it('répond toujours le même message, qui remplace le formulaire', async () => {
    saisir(page, 'input[type="email"]', '  amira@exemple.tn ');
    envoyer(page);

    const requete = http.expectOne('/api/mot-de-passe-oublie');
    expect(requete.request.method).toBe('POST');
    expect(requete.request.body).toEqual({ email: 'amira@exemple.tn' });
    requete.flush({ message: LIEN_ENVOYE });

    await vi.waitFor(() => expect(page.textContent).toContain(LIEN_ENVOYE));
    expect(page.querySelector('input[type="email"]')).toBeNull();
  });

  it('un email vide ne part pas', async () => {
    saisir(page, 'input[type="email"]', '   ');
    envoyer(page);
    await harness.fixture.whenStable();

    expect(page.textContent).toContain('Saisissez votre email');
  });

  it('ramène à la connexion', () => {
    expect(page.querySelector('a[href="/connexion"]')?.textContent).toContain('Retour à la connexion');
  });
});
