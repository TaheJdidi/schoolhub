import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { Employe, RessourcesHumaines, SalairesDuMois } from './ressources-humaines';

const FATMA: Employe = {
  id: 'e1',
  prenom: 'Fatma',
  nom: 'Ayari',
  email: null,
  telephone: '98 111 222',
  poste: 'Agent de ménage',
  horaires: 'Lundi à samedi, 8h → 12h',
  salaire: 450,
  partiLe: null,
  joursDeConge: 5,
};

const SALAIRES: SalairesDuMois = {
  mois: '2027-11',
  salaires: [{ employeId: 'e1', nom: 'Fatma Ayari', poste: 'Agent de ménage', montant: 450, paye: false, payeLe: null }],
  totalPaye: 0,
  totalAPayer: 450,
};

describe('les ressources humaines', () => {
  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'ressources-humaines', component: RessourcesHumaines }, { path: '**', component: PageDeTest }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/ressources-humaines');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/employes'))).flush([FATMA, { ...FATMA, id: 'e2', prenom: 'Ali', partiLe: '2027-10-31' }]);
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/salaires'))).flush(SALAIRES);
    await vi.waitFor(() => expect(page.querySelectorAll('.employe')).toHaveLength(1));
  });

  afterEach(() => http.verify());

  const bouton = (texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte)!;

  it('liste les employés au centre, et les partis à part', async () => {
    expect(page.querySelector('.employe')?.textContent).toContain('Agent de ménage · 450,000 TND par mois · 5 jours de congé cette année');

    bouton('Partis').click();
    await vi.waitFor(() => expect(page.querySelector('.employe')?.textContent).toContain('Ali Ayari'));
  });

  it('ajoute un employé', async () => {
    bouton('Ajouter un employé').click();
    await vi.waitFor(() => expect(page.querySelector('form')).not.toBeNull());
    for (const [nom, valeur] of [['prenom', 'Fatma'], ['nom', 'Ayari'], ['poste', 'Agent de ménage'], ['salaire', '450']]) {
      const champ = page.querySelector(`[formControlName="${nom}"]`) as HTMLInputElement;
      champ.value = valeur;
      champ.dispatchEvent(new Event('input'));
    }

    bouton('Enregistrer').click();
    const requete = http.expectOne({ method: 'POST', url: '/api/employes' });
    expect(requete.request.body).toEqual(expect.objectContaining({ prenom: 'Fatma', nom: 'Ayari', poste: 'Agent de ménage', salaire: 450 }));
    requete.flush(FATMA);
    (await vi.waitFor(() => http.expectOne('/api/employes'))).flush([FATMA]);
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/salaires'))).flush(SALAIRES);
    await vi.waitFor(() => expect(page.textContent).toContain('La fiche de Fatma Ayari est enregistrée.'));
  });

  it('ouvre la fiche et ajoute un congé', async () => {
    (page.querySelector('.nom') as HTMLButtonElement).click();
    (await vi.waitFor(() => http.expectOne('/api/employes/e1/conges'))).flush([]);
    await vi.waitFor(() => expect(page.textContent).toContain('Aucun congé noté.'));
    const [du, au] = [...page.querySelectorAll('.nouveau-conge input[type="date"]')] as HTMLInputElement[];
    du.value = '2027-08-02';
    au.value = '2027-08-06';

    bouton('Ajouter un congé').click();
    const requete = http.expectOne({ method: 'POST', url: '/api/employes/e1/conges' });
    expect(requete.request.body).toEqual({ du: '2027-08-02', au: '2027-08-06', note: '' });
    requete.flush({ id: 'c1', du: '2027-08-02', au: '2027-08-06', note: null, jours: 5 });
    (await vi.waitFor(() => http.expectOne('/api/employes/e1/conges'))).flush([{ id: 'c1', du: '2027-08-02', au: '2027-08-06', note: null, jours: 5 }]);
    (await vi.waitFor(() => http.expectOne('/api/employes'))).flush([FATMA]);
    await vi.waitFor(() => expect(page.textContent).toContain('Du 02/08/2027 au 06/08/2027 · 5 jours'));
  });

  it('marque un salaire du mois payé', async () => {
    expect(page.querySelector('.salaire')?.textContent).toContain('À payer');

    (page.querySelector('.salaire input[type="checkbox"]') as HTMLInputElement).click();
    const requete = http.expectOne({ method: 'POST', url: '/api/salaires' });
    expect(requete.request.body).toEqual(expect.objectContaining({ employeId: 'e1', paye: true }));
    requete.flush({ ...SALAIRES, salaires: [{ ...SALAIRES.salaires[0], paye: true, payeLe: '2027-11-27' }], totalPaye: 450, totalAPayer: 0 });
    await vi.waitFor(() => expect(page.querySelector('.salaire')?.textContent).toContain('Payé le 27/11/2027'));
  });
});
