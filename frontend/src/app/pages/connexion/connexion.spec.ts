import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Session } from '../../socle/session/session';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { AMIRA } from '../../test/personnes';
import { Connexion } from './connexion';

describe('la page de connexion', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            { path: 'connexion', component: Connexion },
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

  async function ouvrir(adresse = '/connexion'): Promise<HTMLElement> {
    await harness.navigateByUrl(adresse);
    await harness.fixture.whenStable();
    return harness.routeNativeElement as HTMLElement;
  }

  function seConnecter(page: HTMLElement, email = 'amira@exemple.tn', motDePasse = 'jasmins-2027'): void {
    saisir(page, 'input[type="email"]', email);
    saisir(page, 'input[autocomplete="current-password"]', motDePasse);
    envoyer(page);
  }

  const adresseActuelle = () => TestBed.inject(Router).url;

  it("un bon mot de passe mène à « Aujourd'hui »", async () => {
    const page = await ouvrir();
    seConnecter(page, '  amira@exemple.tn ');

    const requete = http.expectOne('/api/connexion');
    expect(requete.request.method).toBe('POST');
    expect(requete.request.body).toEqual({ email: 'amira@exemple.tn', motDePasse: 'jasmins-2027' });
    requete.flush(AMIRA);

    await vi.waitFor(() => expect(adresseActuelle()).toBe('/aujourdhui'));
    expect(TestBed.inject(Session).personne()).toEqual(AMIRA);
  });

  it('revient à la page demandée avant la connexion', async () => {
    const page = await ouvrir('/connexion?retour=%2Fcaisse');
    seConnecter(page);
    http.expectOne('/api/connexion').flush(AMIRA);

    await vi.waitFor(() => expect(adresseActuelle()).toBe('/caisse'));
  });

  it('affiche le refus du serveur', async () => {
    const page = await ouvrir();
    seConnecter(page);
    http
      .expectOne('/api/connexion')
      .flush({ message: 'Email ou mot de passe incorrect' }, { status: 401, statusText: 'Unauthorized' });

    await vi.waitFor(() => expect(page.textContent).toContain('Email ou mot de passe incorrect'));
    expect(adresseActuelle()).toBe('/connexion');
  });

  it("ne part qu'une fois, et le bouton le montre", async () => {
    const page = await ouvrir();
    seConnecter(page);
    envoyer(page);
    await harness.fixture.whenStable();

    const bouton = page.querySelector('button[type="submit"]') as HTMLButtonElement;
    expect(bouton.textContent).toContain('Connexion…');
    expect(bouton.disabled).toBe(true);
    http.expectOne('/api/connexion').flush(AMIRA);
  });

  it("un email fait d'espaces ne part pas", async () => {
    const page = await ouvrir();
    seConnecter(page, '   ');
    await harness.fixture.whenStable();

    expect(page.textContent).toContain('Saisissez votre email et votre mot de passe');
  });

  it('annonce une session expirée', async () => {
    const page = await ouvrir('/connexion?expiree=1');

    expect(page.textContent).toContain('Votre session a expiré : reconnectez-vous');
  });

  it('propose le mot de passe oublié', async () => {
    const page = await ouvrir();

    const lien = page.querySelector('a[href="/mot-de-passe-oublie"]');
    expect(lien?.textContent).toContain('Mot de passe oublié');
  });
  it("l'Éditeur scanne le QR code, saisit le code de son application, puis entre dans le back office (EDI-00)", async () => {
    const page = await ouvrir();
    seConnecter(page, 'nadia@schoolup.tn', 'back-office-2027');

    http.expectOne('/api/connexion').flush(
      { etape: 'activation-2fa', message: 'Scannez le QR code' },
      { status: 202, statusText: 'Accepted' },
    );
    // Le QR code se demande une fois la réponse 202 lue.
    const activation = await vi.waitFor(() => http.expectOne('/api/connexion/activation-2fa'));
    activation.flush({ qrCode: 'data:image/png;base64,AAAA', cle: 'ABCD EFGH' });
    await vi.waitFor(() => expect(page.querySelector('img.qr')).not.toBeNull());
    expect(page.textContent).toContain('Scannez le QR code');
    expect(page.querySelector('img.qr')?.getAttribute('src')).toBe('data:image/png;base64,AAAA');
    expect(page.textContent).toContain('ABCD EFGH');

    saisir(page, 'input[autocomplete="one-time-code"]', '123 456');
    envoyer(page);
    const code = http.expectOne('/api/connexion/code-2fa');
    expect(code.request.body).toEqual({ code: '123456' });
    code.flush({ prenom: 'Nadia', nom: 'Mansour', role: 'Editeur', centre: null });

    await vi.waitFor(() => expect(adresseActuelle()).toBe('/editeur'));
  });

  it("un code faux s'affiche et le champ se vide", async () => {
    const page = await ouvrir('/connexion?etape=code-2fa');
    expect(page.textContent).toContain('Saisissez le code à 6 chiffres');

    saisir(page, 'input[autocomplete="one-time-code"]', '000000');
    envoyer(page);
    http.expectOne('/api/connexion/code-2fa').flush(
      { message: "Ce code n'est pas le bon" },
      { status: 401, statusText: 'Unauthorized' },
    );

    await vi.waitFor(() => expect(page.textContent).toContain("Ce code n'est pas le bon"));
  });
});
