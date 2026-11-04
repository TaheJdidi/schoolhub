import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { PageDeTest } from '../test/page-de-test';
import { DELAI_DE_RECHERCHE, RechercheDEleves } from './recherche-d-eleves';

describe("la recherche d'un élève, en haut de chaque écran", () => {
  const ADEM = { id: 'e1', prenom: 'Adem', nom: 'Miled', niveau: '9ᵉ année de base' };

  let http: HttpTestingController;
  let fixture: ComponentFixture<RechercheDEleves>;
  let element: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', component: PageDeTest }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: DELAI_DE_RECHERCHE, useValue: 0 },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(RechercheDEleves);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const champ = () => element.querySelector('input') as HTMLInputElement;
  const resultats = () => [...element.querySelectorAll('.resultat')] as HTMLAnchorElement[];

  function taper(texte: string): void {
    champ().value = texte;
    champ().dispatchEvent(new Event('input'));
  }

  const requeteDeRecherche = (recherche: string) => (requete: { url: string; params: { get(nom: string): string | null } }) =>
    requete.url === '/api/eleves' && requete.params.get('recherche') === recherche;

  async function attendreLaRequete(recherche: string): Promise<TestRequest> {
    return vi.waitFor(() => http.expectOne(requeteDeRecherche(recherche)));
  }

  it('ne cherche rien avant 2 caractères', async () => {
    taper('a');
    await new Promise((resolve) => setTimeout(resolve, 20));

    http.expectNone((requete) => requete.url === '/api/eleves');
    expect(element.querySelector('.resultats')).toBeNull();
  });

  it("n'envoie qu'une requête quand on tape vite", async () => {
    taper('a');
    taper('ad');
    taper('ade');

    (await attendreLaRequete('ade')).flush([ADEM]);

    await vi.waitFor(() => expect(resultats()).toHaveLength(1));
    expect(resultats()[0].textContent).toContain('Adem Miled');
    expect(resultats()[0].textContent).toContain('9ᵉ année de base');
    expect(resultats()[0].getAttribute('href')).toBe('/eleves/e1');
  });

  it('dit quand aucun élève ne correspond', async () => {
    taper('zz');

    (await attendreLaRequete('zz')).flush([]);

    await vi.waitFor(() => expect(element.querySelector('.resultats')?.textContent).toContain('Aucun élève trouvé'));
  });

  it('montre 8 élèves au plus', async () => {
    taper('miled');

    (await attendreLaRequete('miled')).flush(Array.from({ length: 10 }, (_, i) => ({ ...ADEM, id: `e${i}` })));

    await vi.waitFor(() => expect(resultats()).toHaveLength(8));
  });

  it("ouvre la fiche de l'élève et vide le champ", async () => {
    taper('adem');
    (await attendreLaRequete('adem')).flush([ADEM]);
    await vi.waitFor(() => expect(resultats()).toHaveLength(1));

    resultats()[0].click();
    await fixture.whenStable();

    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/eleves/e1'));
    expect(champ().value).toBe('');
    expect(element.querySelector('.resultats')).toBeNull();
  });

  it('se ferme avec Échap', async () => {
    taper('adem');
    (await attendreLaRequete('adem')).flush([ADEM]);
    await vi.waitFor(() => expect(resultats()).toHaveLength(1));

    champ().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await fixture.whenStable();

    expect(element.querySelector('.resultats')).toBeNull();
  });
});
