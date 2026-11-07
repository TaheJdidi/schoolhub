import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { Journal, JournalFiltre } from './journal';
import { TableauDeBord, TableauDUnePeriode } from './tableau-de-bord';

const TABLEAU: TableauDUnePeriode = {
  periode: 'Novembre 2027',
  du: '2027-11-01',
  au: '2027-11-30',
  encaisse: 65,
  encaisseParOrigine: [
    { libelle: 'Étude', valeur: 40 },
    { libelle: 'Bibliothèque', valeur: 5 },
    { libelle: 'Assurance', valeur: 20 },
  ],
  impayes: 40,
  impayesDAssurance: 0,
  depenses: 1021,
  partsDesProfesseurs: 76,
  salaires: 900,
  achats: 45,
  benefice: -956,
  parGroupe: [
    {
      groupeId: 'g1',
      groupe: 'Groupe Samedi 10h → 12h',
      matiere: 'Mathématiques',
      type: 'Étude',
      professeurId: 'k1',
      professeur: 'Karim Ben Salah',
      montantDuGroupe: 80,
      encaisse: 40,
      impayes: 40,
      partDuProfesseur: 76,
      partDuCentre: 4,
    },
  ],
};

function configurer(): HttpTestingController {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'tableau-de-bord', component: TableauDeBord },
        { path: 'journal', component: Journal },
        { path: '**', component: PageDeTest },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  return TestBed.inject(HttpTestingController);
}

describe('le tableau de bord', () => {
  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    http = configurer();
    const harness = await RouterTestingHarness.create('/tableau-de-bord');
    page = harness.routeNativeElement as HTMLElement;
    const premiere = await vi.waitFor(() => http.expectOne((r) => r.url === '/api/tableau-de-bord'));
    expect(premiere.request.params.get('mois')).toMatch(/^\d{4}-\d{2}$/);
    premiere.flush(TABLEAU);
    http.expectOne('/api/tableau-de-bord/prix-speciaux').flush({
      prixSpeciaux: [
        { eleveId: 'e2', eleve: 'Lina Gharbi', groupe: 'Mathématiques · Groupe Samedi', unite: 'par Mois', prixNormal: 40, prixSpecial: 20, raison: 'Famille', le: '2027-11-27', par: 'Hedi Ben Salah' },
      ],
      dispenses: [],
    });
    await vi.waitFor(() => expect(page.querySelector('.chiffres')).not.toBeNull());
  });

  afterEach(() => http.verify());

  it('montre les quatre chiffres, le bénéfice négatif en rouge, et le détail par groupe', async () => {
    expect(page.querySelector('.sub')?.textContent).toContain('Novembre 2027');
    expect(page.querySelector('.chiffres')?.textContent).toContain('65,000 TND');
    expect(page.querySelector('.benefice')?.closest('div')?.classList).toContain('bad');
    expect(page.querySelector('tbody')?.textContent).toContain('Mathématiques · Groupe Samedi 10h → 12h');
    expect(page.querySelector('tfoot')?.textContent).toContain('76,000');
    expect(page.textContent).toContain('Lina Gharbi');
    expect(page.textContent).toContain('27/11/2027 · Hedi Ben Salah');
  });

  it('regroupe par professeur, et passe à l’année scolaire', async () => {
    (page.querySelector('.coche input') as HTMLInputElement).click();
    await vi.waitFor(() => expect(page.querySelector('.sous-total')?.textContent).toContain('Karim Ben Salah'));

    ([...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Année scolaire') as HTMLButtonElement).click();
    const annee = await vi.waitFor(() => http.expectOne((r) => r.url === '/api/tableau-de-bord'));
    expect(annee.request.params.get('annee')).toBe('true');
    annee.flush({ ...TABLEAU, periode: 'Année 2027-2028' });
    await vi.waitFor(() => expect(page.querySelector('.sub')?.textContent).toContain('Année 2027-2028'));
  });
});

describe('le journal', () => {
  it('liste les lignes avec leur avant et après, et se filtre par action', async () => {
    const http = configurer();
    const harness = await RouterTestingHarness.create('/journal');
    const page = harness.routeNativeElement as HTMLElement;
    const journal: JournalFiltre = {
      lignes: [{ quand: '2027-10-12T15:42:00+00:00', auteur: 'Amira Jlassi', action: 'RecuAnnule', detail: 'Reçu n° 142 · Adem Miled · mauvais élève', avant: 'Encaissé, 40,000 TND', apres: 'Annulé' }],
      auteurs: [{ id: 'a1', nom: 'Amira Jlassi' }],
      actions: ['Encaissement', 'RecuAnnule'],
    };
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/journal'))).flush(journal);
    await vi.waitFor(() => expect(page.querySelector('.ligne')).not.toBeNull());

    expect(page.querySelector('.ligne')?.textContent).toContain('Amira Jlassi · Reçu annulé');
    expect(page.querySelector('.ligne')?.textContent).toContain('Encaissé, 40,000 TND → Annulé');

    const action = page.querySelectorAll('.filtres select')[1] as HTMLSelectElement;
    action.value = 'Encaissement';
    action.dispatchEvent(new Event('change'));
    const filtre = await vi.waitFor(() => http.expectOne((r) => r.url === '/api/journal'));
    expect(filtre.request.params.get('action')).toBe('Encaissement');
    filtre.flush({ ...journal, lignes: [] });
    await vi.waitFor(() => expect(page.textContent).toContain('Aucune ligne.'));
    http.verify();
  });
});
