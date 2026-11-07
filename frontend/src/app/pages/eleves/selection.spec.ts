import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { DELAI_DE_RECHERCHE } from '../../cadre/recherche-d-eleves';
import { PageDeTest } from '../../test/page-de-test';
import { Eleves } from './eleves';

const ELEVES = [
  { id: 'e1', prenom: 'Adem', nom: 'Miled', niveau: '9ᵉ année de base' },
  { id: 'e2', prenom: 'Lina', nom: 'Gharbi', niveau: '6ᵉ année de base' },
  { id: 'e3', prenom: 'Sami', nom: 'Chaabane', niveau: '9ᵉ année de base' },
];

describe('la sélection de plusieurs élèves', () => {
  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'eleves', component: Eleves }, { path: '**', component: PageDeTest }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: DELAI_DE_RECHERCHE, useValue: 0 },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/eleves');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/eleves'))).flush(ELEVES);
    await vi.waitFor(() => expect(page.querySelectorAll('.rangee')).toHaveLength(3));
  });

  afterEach(() => http.verify());

  const bouton = (texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte)!;

  async function toutSelectionner(): Promise<void> {
    (page.querySelector('.tout input') as HTMLInputElement).click();
    (await vi.waitFor(() => http.expectOne('/api/groupes'))).flush([
      { id: 'g1', nom: 'Groupe Samedi 10h → 12h', type: 'Etude', matiere: 'Mathématiques', ferme: false, termine: false },
      { id: 'g2', nom: 'Groupe fermé', type: 'Etude', matiere: 'Physique', ferme: true, termine: false },
    ]);
    http.expectOne('/api/niveaux').flush([{ id: 'n10', nom: '1ʳᵉ année secondaire' }]);
    await vi.waitFor(() => expect(page.querySelector('.selection')?.textContent).toContain('3 élèves sélectionnés'));
  }

  it('« Tout sélectionner » prend les lignes du filtre en cours, et inscrit la sélection dans un groupe ouvert', async () => {
    await toutSelectionner();
    const groupe = page.querySelector('.actions-selection select') as HTMLSelectElement;
    expect([...groupe.options].map((o) => o.textContent?.trim())).toEqual(['Choisissez le groupe', 'Mathématiques · Groupe Samedi 10h → 12h']);
    groupe.value = 'g1';
    groupe.dispatchEvent(new Event('change'));
    await vi.waitFor(() => expect(bouton('Inscrire').disabled).toBe(false));

    bouton('Inscrire').click();
    const requete = http.expectOne({ method: 'POST', url: '/api/selection/inscription' });
    expect(requete.request.body).toEqual({ eleveIds: ['e1', 'e2', 'e3'], groupeId: 'g1', mode: null });
    requete.flush({ reussis: ['Sami Chaabane', 'Lina Gharbi'], dejaInscrits: ['Adem Miled'], refuses: [] });

    await vi.waitFor(() => expect(page.textContent).toContain('Inscrits : Sami Chaabane, Lina Gharbi.'));
    expect(page.textContent).toContain('Déjà inscrits, laissés à part : Adem Miled.');
  });

  it('change le niveau, et dit pour qui il est refusé', async () => {
    await toutSelectionner();
    const niveau = page.querySelectorAll('.actions-selection select')[1] as HTMLSelectElement;
    niveau.value = 'n10';

    bouton('Changer le niveau').click();
    const requete = http.expectOne({ method: 'POST', url: '/api/selection/niveau' });
    expect(requete.request.body).toEqual({ eleveIds: ['e1', 'e2', 'e3'], niveauId: 'n10' });
    requete.flush({ reussis: ['Adem Miled'], dejaInscrits: [], refuses: [{ eleveId: 'e2', nom: 'Lina Gharbi', raison: "Le téléphone de l'élève est obligatoire" }] });
    (await vi.waitFor(() => http.expectOne('/api/eleves'))).flush(ELEVES);

    await vi.waitFor(() => expect(page.textContent).toContain('Niveau changé : Adem Miled.'));
    expect(page.querySelector('.selection .note.bad')?.textContent).toContain("Lina Gharbi : Le téléphone de l'élève est obligatoire");
  });

  it('une case sélectionne un élève ; la sélection s’exporte seule', async () => {
    (page.querySelectorAll('.rangee input')[1] as HTMLInputElement).click();
    (await vi.waitFor(() => http.expectOne('/api/groupes'))).flush([]);
    http.expectOne('/api/niveaux').flush([]);
    await vi.waitFor(() => expect(page.querySelector('.selection')?.textContent).toContain('1 élève sélectionné'));
    expect(page.querySelectorAll('.rangee.non-selectionne')).toHaveLength(2);

    bouton('Tout désélectionner').click();
    await vi.waitFor(() => expect(page.querySelector('.selection')).toBeNull());
  });
});
