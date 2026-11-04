import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { DELAI_DE_RECHERCHE } from '../../cadre/recherche-d-eleves';
import { saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { EleveTrouve } from './eleve';
import { Eleves } from './eleves';

describe('la page « Élèves »', () => {
  const ADEM: EleveTrouve = { id: 'e1', prenom: 'Adem', nom: 'Miled', niveau: '9ᵉ année de base' };
  const AMEL: EleveTrouve = { id: 'e2', prenom: 'Amel', nom: 'Chaabane', niveau: '6ᵉ année de base' };

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  async function ouvrir(eleves: EleveTrouve[]): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'eleves', component: Eleves },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: DELAI_DE_RECHERCHE, useValue: 0 },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/eleves');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/eleves'))).flush(eleves);
    await vi.waitFor(() => expect(page.querySelector('.liste')?.textContent?.trim()).not.toBe(''));
  }

  afterEach(() => http.verify());

  const lignes = () => [...page.querySelectorAll('.liste a')] as HTMLAnchorElement[];

  it('liste les élèves', async () => {
    await ouvrir([AMEL, ADEM]);

    expect(page.querySelector('.sub')?.textContent?.trim()).toBe('2 élèves');
    expect(lignes().map((l) => [...l.children].map((morceau) => morceau.textContent?.trim()).join(' '))).toEqual([
      'Amel Chaabane 6ᵉ année de base',
      'Adem Miled 9ᵉ année de base',
    ]);
    expect(lignes()[1].getAttribute('href')).toBe('/eleves/e1');
    expect(page.querySelector('a.btn.primary')?.getAttribute('href')).toBe('/eleves/nouveau');
  });

  it("dit quand il n'y a aucun élève", async () => {
    await ouvrir([]);

    expect(page.querySelector('.sub')?.textContent?.trim()).toBe('Aucun élève');
    expect(page.querySelector('.liste')?.textContent).toContain("Aucun élève pour l'instant. Ajoutez le premier.");
  });

  it('filtre la liste par la recherche', async () => {
    await ouvrir([AMEL, ADEM]);

    saisir(page, '[aria-label="Filtrer la liste"]', 'adem');
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/eleves' && r.params.get('recherche') === 'adem'))).flush([ADEM]);
    await vi.waitFor(() => expect(lignes()).toHaveLength(1));

    saisir(page, '[aria-label="Filtrer la liste"]', 'zz');
    (await vi.waitFor(() => http.expectOne((r) => r.params.get('recherche') === 'zz'))).flush([]);
    await vi.waitFor(() => expect(page.querySelector('.liste')?.textContent).toContain('Aucun élève trouvé.'));

    saisir(page, '[aria-label="Filtrer la liste"]', '');
    await vi.waitFor(() => expect(lignes()).toHaveLength(2));
  });
});
