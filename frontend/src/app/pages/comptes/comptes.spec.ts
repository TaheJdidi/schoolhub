import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Session } from '../../socle/session/session';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { HEDI } from '../../test/personnes';
import { CompteDuCentre } from './compte-du-centre';
import { Comptes } from './comptes';

describe('la page « Comptes »', () => {
  const COMPTE_DE_HEDI: CompteDuCentre = {
    id: 'h', prenom: 'Hedi', nom: 'Ben Salah', email: 'hedi@exemple.tn', role: 'Proprietaire', etat: 'Actif', moi: true,
  };
  const MEHDI: CompteDuCentre = {
    id: 'm', prenom: 'Mehdi', nom: 'Trabelsi', email: 'mehdi@exemple.tn', role: 'Proprietaire', etat: 'Actif', moi: false,
  };
  const SONIA: CompteDuCentre = {
    id: 's', prenom: 'Sonia', nom: 'Mabrouk', email: 'sonia.mabrouk@exemple.tn', role: 'Secretaire', etat: 'Invite', moi: false,
  };
  const AMIRA: CompteDuCentre = {
    id: 'a', prenom: 'Amira', nom: 'Jlassi', email: 'amira@exemple.tn', role: 'Secretaire', etat: 'Desactive', moi: false,
  };

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'comptes', component: Comptes },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    TestBed.inject(Session).connaitre(HEDI);
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/comptes');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/comptes'))).flush([COMPTE_DE_HEDI, MEHDI, SONIA, AMIRA]);
    await vi.waitFor(() => expect(page.querySelectorAll('li')).toHaveLength(4));
  });

  /** Attend qu'un texte s'affiche : la réponse du serveur arrive après le rendu en cours. */
  async function attendre(texte: string): Promise<void> {
    await vi.waitFor(() => expect(page.textContent).toContain(texte));
  }

  afterEach(() => http.verify());

  /** La ligne d'une personne de la liste, trouvée par son prénom. */
  function ligne(prenom: string): HTMLElement {
    const trouvee = [...page.querySelectorAll('li')].find((li) => li.textContent?.includes(prenom));
    if (!trouvee) throw new Error(`Aucune ligne pour ${prenom}`);
    return trouvee;
  }

  /** Les libellés des boutons d'un endroit de la page. */
  function boutons(endroit: HTMLElement): string[] {
    return [...endroit.querySelectorAll('button')].map((b) => b.textContent?.trim() ?? '');
  }

  function cliquer(endroit: HTMLElement, libelle: string): void {
    const bouton = [...endroit.querySelectorAll('button')].find((b) => b.textContent?.trim() === libelle);
    if (!bouton) throw new Error(`Aucun bouton « ${libelle} »`);
    bouton.click();
  }

  it('montre chaque compte avec son rôle et son état', () => {
    expect(page.textContent).toContain('Les personnes qui se connectent à SchoolHub pour le Centre Les Jasmins.');
    expect(ligne('Hedi').textContent).toContain('Hedi Ben Salah (vous)');
    expect(ligne('Hedi').textContent).toContain('hedi@exemple.tn');
    expect(ligne('Mehdi').textContent).toContain('Propriétaire');
    expect(ligne('Sonia').textContent).toContain('Secrétaire');
    expect(ligne('Mehdi').querySelector('.pill')?.classList).toContain('ok');
    expect(ligne('Mehdi').querySelector('.pill')?.textContent?.trim()).toBe('Actif');
    expect(ligne('Sonia').querySelector('.pill')?.classList).toContain('info');
    expect(ligne('Sonia').querySelector('.pill')?.textContent?.trim()).toBe('Invité');
    expect(ligne('Amira').querySelector('.pill')?.classList).toContain('muted');
    expect(ligne('Amira').querySelector('.pill')?.textContent?.trim()).toBe('Désactivé');
  });

  it("les boutons suivent l'état, et la ligne « (vous) » n'a pas « Désactiver »", () => {
    expect(boutons(ligne('Hedi'))).toEqual([]);
    expect(boutons(ligne('Mehdi'))).toEqual(['Désactiver']);
    expect(boutons(ligne('Sonia'))).toEqual(["Renvoyer l'invitation", 'Désactiver']);
    expect(boutons(ligne('Amira'))).toEqual(['Réactiver']);
  });

  it("« Désactiver » demande confirmation, et « Non » n'envoie rien", async () => {
    cliquer(ligne('Mehdi'), 'Désactiver');
    await harness.fixture.whenStable();

    expect(ligne('Mehdi').textContent).toContain('Désactiver le compte de Mehdi Trabelsi ?');
    cliquer(ligne('Mehdi'), 'Non');
    await harness.fixture.whenStable();

    expect(ligne('Mehdi').textContent).not.toContain('Désactiver le compte de');
    http.expectNone('/api/comptes/m/desactivation');
  });

  it('« Oui » désactive, met la ligne à jour et le confirme', async () => {
    cliquer(ligne('Mehdi'), 'Désactiver');
    await harness.fixture.whenStable();
    cliquer(ligne('Mehdi'), 'Oui');

    const requete = http.expectOne('/api/comptes/m/desactivation');
    expect(requete.request.method).toBe('POST');
    requete.flush({ ...MEHDI, etat: 'Desactive' });
    await attendre('Compte de Mehdi Trabelsi désactivé');

    expect(ligne('Mehdi').querySelector('.pill')?.textContent?.trim()).toBe('Désactivé');
    expect(boutons(ligne('Mehdi'))).toEqual(['Réactiver']);
  });

  it("« Renvoyer l'invitation » et « Réactiver » envoient et confirment", async () => {
    cliquer(ligne('Sonia'), "Renvoyer l'invitation");
    const renvoi = http.expectOne('/api/comptes/s/invitation');
    expect(renvoi.request.method).toBe('POST');
    renvoi.flush(SONIA);
    await attendre('Nouvelle invitation envoyée à sonia.mabrouk@exemple.tn');

    cliquer(ligne('Amira'), 'Réactiver');
    http.expectOne('/api/comptes/a/reactivation').flush({ ...AMIRA, etat: 'Actif' });
    await attendre('Compte de Amira Jlassi réactivé');
    expect(ligne('Amira').querySelector('.pill')?.textContent?.trim()).toBe('Actif');
  });

  it('inviter envoie les champs sans espaces, ferme le formulaire et recharge la liste', async () => {
    cliquer(page, 'Inviter une personne');
    await harness.fixture.whenStable();
    expect((page.querySelector('select[formcontrolname="role"]') as HTMLSelectElement).value).toBe('Secretaire');

    saisir(page, 'input[formcontrolname="prenom"]', ' Sonia ');
    saisir(page, 'input[formcontrolname="nom"]', 'Mabrouk ');
    saisir(page, 'input[formcontrolname="email"]', '  sonia.mabrouk@exemple.tn ');
    envoyer(page);

    const invitation = http.expectOne((r) => r.method === 'POST' && r.url === '/api/comptes');
    expect(invitation.request.body).toEqual({
      prenom: 'Sonia', nom: 'Mabrouk', email: 'sonia.mabrouk@exemple.tn', role: 'Secretaire',
    });
    invitation.flush(SONIA, { status: 201, statusText: 'Created' });
    (await vi.waitFor(() => http.expectOne((r) => r.method === 'GET' && r.url === '/api/comptes')))
      .flush([COMPTE_DE_HEDI, MEHDI, SONIA, AMIRA]);
    await attendre('Invitation envoyée à sonia.mabrouk@exemple.tn');

    expect(page.querySelector('form')).toBeNull();
  });

  it('des champs vides ne partent pas', async () => {
    cliquer(page, 'Inviter une personne');
    await harness.fixture.whenStable();
    saisir(page, 'input[formcontrolname="prenom"]', 'Sonia');
    saisir(page, 'input[formcontrolname="email"]', '   ');
    envoyer(page);
    await harness.fixture.whenStable();

    expect(page.textContent).toContain("Remplissez le prénom, le nom et l'email");
  });

  it("un refus du serveur s'affiche", async () => {
    cliquer(page, 'Inviter une personne');
    await harness.fixture.whenStable();
    saisir(page, 'input[formcontrolname="prenom"]', 'Hedi');
    saisir(page, 'input[formcontrolname="nom"]', 'Ben Salah');
    saisir(page, 'input[formcontrolname="email"]', 'hedi@exemple.tn');
    envoyer(page);

    http.expectOne((r) => r.method === 'POST' && r.url === '/api/comptes').flush(
      { message: 'Cet email est déjà utilisé sur SchoolHub' },
      { status: 409, statusText: 'Conflict' },
    );
    await attendre('Cet email est déjà utilisé sur SchoolHub');

    expect(page.querySelector('[role="alert"]')?.textContent).toContain('Cet email est déjà utilisé sur SchoolHub');
    expect(page.querySelector('form')).not.toBeNull();
  });

  it("le bouton se bloque pendant l'envoi", async () => {
    cliquer(page, 'Inviter une personne');
    await harness.fixture.whenStable();
    saisir(page, 'input[formcontrolname="prenom"]', 'Sonia');
    saisir(page, 'input[formcontrolname="nom"]', 'Mabrouk');
    saisir(page, 'input[formcontrolname="email"]', 'sonia.mabrouk@exemple.tn');
    envoyer(page);
    envoyer(page);
    await harness.fixture.whenStable();

    const bouton = page.querySelector('form button[type="submit"]') as HTMLButtonElement;
    expect(bouton.textContent).toContain('Envoi…');
    expect(bouton.disabled).toBe(true);
    // Une seule requête est partie : expectOne échouerait s'il y en avait deux.
    http.expectOne((r) => r.method === 'POST' && r.url === '/api/comptes').flush(
      { message: 'Cet email est déjà utilisé sur SchoolHub' },
      { status: 409, statusText: 'Conflict' },
    );
  });
});
