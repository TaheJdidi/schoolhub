import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { Bibliotheque, EntreeDecrite, JourneeALaBibliotheque } from './bibliotheque';

const ADEM: EntreeDecrite = {
  id: 'b1',
  eleveId: 'e1',
  prenom: 'Adem',
  nom: 'Miled',
  exterieur: false,
  arrivee: '10:00',
  depart: null,
  recuId: 'r1',
  numero: 12,
  annule: false,
};

const JOURNEE: JourneeALaBibliotheque = {
  jour: '2027-11-27',
  prix: 5,
  capacite: 30,
  presents: 1,
  entrees: 2,
  encaisse: 10,
  liste: [ADEM, { ...ADEM, id: 'b2', eleveId: 'e2', prenom: 'Rim', nom: 'Saidi', exterieur: true, depart: '11:30', recuId: 'r2', numero: 13 }],
};

describe('la bibliothèque', () => {
  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'bibliotheque', component: Bibliotheque },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/bibliotheque');
    page = harness.routeNativeElement as HTMLElement;
  });

  afterEach(() => http.verify());

  async function charger(journee: JourneeALaBibliotheque = JOURNEE): Promise<void> {
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/bibliotheque'))).flush(journee);
    await vi.waitFor(() => expect(page.querySelectorAll('tbody tr').length).toBeGreaterThan(0));
  }

  const bouton = (texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte);

  it('montre les présents, les entrées du jour et la liste', async () => {
    await charger();

    expect(page.querySelector('.chiffres')?.textContent).toContain('1 / 30');
    expect(page.querySelector('.chiffres')?.textContent).toContain('29 places libres');
    expect(page.querySelector('.chiffres')?.textContent).toContain('10,000 TND encaissés');
    const lignes = page.querySelectorAll('tbody tr');
    expect(lignes[0].textContent).toContain('Élève du centre');
    expect(lignes[1].textContent).toContain('Extérieur');
    expect(lignes[1].textContent).toContain('11:30');
    expect(lignes[0].querySelector('a[href="/caisse/recus/r1"]')?.textContent).toContain('n° 12');
  });

  it('note le départ maintenant', async () => {
    await charger();

    bouton('Noter le départ')!.click();
    const requete = http.expectOne({ method: 'PUT', url: '/api/bibliotheque/entrees/b1/depart' });
    expect(requete.request.body).toEqual({ heure: null });
    requete.flush({ ...ADEM, depart: '12:15' });
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/bibliotheque'))).flush({ ...JOURNEE, presents: 0, liste: [{ ...ADEM, depart: '12:15' }] });
    await vi.waitFor(() => expect(page.querySelector('tbody')?.textContent).toContain('12:15'));
  });

  it('enregistre une entrée pour un élève du centre, avec ses avertissements', async () => {
    vi.useFakeTimers();
    try {
      await charger();
      const recherche = page.querySelector('.nouvelle input') as HTMLInputElement;
      recherche.value = 'mil';
      recherche.dispatchEvent(new Event('input'));
      await vi.advanceTimersByTimeAsync(300);
      const trouve = http.expectOne((r) => r.url === '/api/eleves');
      expect(trouve.request.params.get('recherche')).toBe('mil');
      trouve.flush([{ id: 'e1', prenom: 'Adem', nom: 'Miled', niveau: '9ᵉ année de base' }]);
      await vi.waitFor(() => expect(page.querySelector('.trouve')).not.toBeNull());

      (page.querySelector('.trouve') as HTMLButtonElement).click();
      const requete = http.expectOne({ method: 'POST', url: '/api/bibliotheque/entrees' });
      expect(requete.request.body).toEqual({ eleveId: 'e1' });
      requete.flush({ entree: ADEM, recu: { numero: 14 }, avertissements: ["Adem Miled a déjà une entrée aujourd'hui."] });
      (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/bibliotheque'))).flush(JOURNEE);

      await vi.waitFor(() => expect(page.textContent).toContain("Adem Miled a déjà une entrée aujourd'hui."));
      expect(page.textContent).toContain('Reçu n° 14, remis sur demande.');
    } finally {
      vi.useRealTimers();
    }
  });

  it('prévient quand la bibliothèque est pleine, sans bloquer', async () => {
    await charger({ ...JOURNEE, capacite: 1 });

    expect(page.querySelector('.chiffres')?.textContent).toContain("Capacité atteinte : l'entrée reste possible");
    expect(page.querySelector('.nouvelle .note.warn')?.textContent).toContain('pleine');
    expect(bouton("Personne de l'extérieur")).toBeDefined();
  });

  it("revoit la liste d'un jour passé, sans nouvelle entrée", async () => {
    await charger();

    const jour = page.querySelector('.pagehead input[type="date"]') as HTMLInputElement;
    jour.value = '2027-11-20';
    jour.dispatchEvent(new Event('change'));
    const requete = http.expectOne((r) => r.url === '/api/bibliotheque');
    expect(requete.request.params.get('jour')).toBe('2027-11-20');
    requete.flush({ ...JOURNEE, jour: '2027-11-20' });

    await vi.waitFor(() => expect(page.querySelector('h1 + .sub')?.textContent).toContain('Liste du 20/11/2027'));
    expect(page.querySelector('.nouvelle')).toBeNull();
    expect(page.textContent).toContain('10,000 TND encaissés');
  });
});
