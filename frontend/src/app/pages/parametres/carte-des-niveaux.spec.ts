import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { CarteDesNiveaux, Niveau } from './carte-des-niveaux';

describe('la carte « Les niveaux »', () => {
  const ADRESSE = '/api/niveaux';
  const D_OFFICE: Niveau[] = Array.from({ length: 29 }, (_, i) => ({ id: `o${i + 1}`, nom: `Niveau d'office ${i + 1}`, dOffice: true }));
  const UNIVERSITE: Niveau = { id: 'u', nom: 'Université, 1ʳᵉ année', dOffice: false };

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  async function ouvrir(niveaux: Niveau[]): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'carte', component: CarteDesNiveaux },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/carte');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne(ADRESSE))).flush(niveaux);
    await vi.waitFor(() => expect(page.querySelectorAll('details li')).toHaveLength(29));
  }

  afterEach(() => http.verify());

  const attendre = (texte: string) => vi.waitFor(() => expect(page.textContent).toContain(texte));
  const ligne = () => page.querySelector('.ajoutes li') as HTMLElement;
  const boutons = (endroit: HTMLElement) => [...endroit.querySelectorAll('button')].map((b) => b.textContent?.trim());

  async function cliquer(endroit: HTMLElement, libelle: string): Promise<void> {
    const bouton = [...endroit.querySelectorAll('button')].find((b) => b.textContent?.trim() === libelle);
    if (!bouton) throw new Error(`Aucun bouton « ${libelle} »`);
    bouton.click();
    await harness.fixture.whenStable();
  }

  it("montre les niveaux ajoutés, et les niveaux d'office repliés", async () => {
    await ouvrir([...D_OFFICE, UNIVERSITE]);

    expect(ligne().textContent).toContain('Université, 1ʳᵉ année');
    expect(boutons(ligne())).toEqual(['Renommer', 'Retirer']);
    const details = page.querySelector('details') as HTMLDetailsElement;
    expect(details.open).toBe(false);
    expect(details.querySelector('summary')?.textContent).toContain("Les 29 niveaux d'office (ils ne se modifient pas)");
    expect(details.querySelectorAll('button')).toHaveLength(0);
  });

  it("dit quand aucun niveau n'est ajouté", async () => {
    await ouvrir(D_OFFICE);

    expect(page.textContent).toContain('Aucun niveau ajouté');
  });

  it('ajoute un niveau, et un nom vide ne part pas', async () => {
    await ouvrir(D_OFFICE);
    saisir(page, '[formcontrolname="nouveau"]', '   ');
    envoyer(page);
    await attendre('Saisissez le nom du niveau');
    http.expectNone((r) => r.method === 'POST');

    saisir(page, '[formcontrolname="nouveau"]', ' Adultes ');
    envoyer(page);
    const ajout = http.expectOne((r) => r.method === 'POST' && r.url === ADRESSE);
    expect(ajout.request.body).toEqual({ nom: 'Adultes' });
    ajout.flush({ id: 'a', nom: 'Adultes', dOffice: false }, { status: 201, statusText: 'Created' });
    (await vi.waitFor(() => http.expectOne((r) => r.method === 'GET' && r.url === ADRESSE)))
      .flush([...D_OFFICE, { id: 'a', nom: 'Adultes', dOffice: false }]);

    await attendre('Niveau ajouté');
    expect(ligne().textContent).toContain('Adultes');
    expect((page.querySelector('[formcontrolname="nouveau"]') as HTMLInputElement).value).toBe('');
  });

  it('renomme, ou annule', async () => {
    await ouvrir([...D_OFFICE, UNIVERSITE]);
    await cliquer(ligne(), 'Renommer');
    expect((ligne().querySelector('input') as HTMLInputElement).value).toBe('Université, 1ʳᵉ année');
    await cliquer(ligne(), 'Annuler');
    expect(ligne().textContent).toContain('Université, 1ʳᵉ année');
    http.expectNone((r) => r.method === 'PUT');

    await cliquer(ligne(), 'Renommer');
    saisir(ligne(), 'input', 'Université, première année');
    await cliquer(ligne(), 'Enregistrer');
    const renommage = http.expectOne((r) => r.method === 'PUT' && r.url === `${ADRESSE}/u`);
    expect(renommage.request.body).toEqual({ nom: 'Université, première année' });
    renommage.flush({ ...UNIVERSITE, nom: 'Université, première année' });

    await attendre('Niveau renommé');
    expect(ligne().textContent).toContain('Université, première année');
    expect(ligne().querySelector('input')).toBeNull();
  });

  it("retire après « Oui », et « Non » n'envoie rien", async () => {
    await ouvrir([...D_OFFICE, UNIVERSITE]);
    await cliquer(ligne(), 'Retirer');
    expect(ligne().textContent).toContain('Retirer « Université, 1ʳᵉ année » ?');
    await cliquer(ligne(), 'Non');
    http.expectNone((r) => r.method === 'DELETE');
    expect(boutons(ligne())).toEqual(['Renommer', 'Retirer']);

    await cliquer(ligne(), 'Retirer');
    await cliquer(ligne(), 'Oui');
    http.expectOne((r) => r.method === 'DELETE' && r.url === `${ADRESSE}/u`).flush(null, { status: 204, statusText: 'No Content' });

    await attendre('Niveau retiré');
    expect(page.querySelector('.ajoutes li')).toBeNull();
    expect(page.textContent).toContain('Aucun niveau ajouté');
  });

  it('bloque tous les boutons de la carte pendant un envoi', async () => {
    await ouvrir([...D_OFFICE, UNIVERSITE]);
    saisir(page, '[formcontrolname="nouveau"]', 'Adultes');
    envoyer(page);
    await harness.fixture.whenStable();

    expect([...page.querySelectorAll('button')].every((b) => b.disabled)).toBe(true);
    http.expectOne((r) => r.method === 'POST').flush({ message: 'Ce niveau existe déjà' }, { status: 409, statusText: 'Conflict' });
  });

  it('montre un refus sous la carte', async () => {
    await ouvrir([...D_OFFICE, UNIVERSITE]);
    saisir(page, '[formcontrolname="nouveau"]', 'université, 1ʳᵉ année');
    envoyer(page);
    http.expectOne((r) => r.method === 'POST').flush({ message: 'Ce niveau existe déjà' }, { status: 409, statusText: 'Conflict' });

    await attendre('Ce niveau existe déjà');
    expect(page.querySelector('.card [role="alert"]')?.textContent).toContain('Ce niveau existe déjà');
  });
});
