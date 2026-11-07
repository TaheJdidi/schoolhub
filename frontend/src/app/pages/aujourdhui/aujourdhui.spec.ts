import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { Aujourdhui, SeanceDuJour } from './aujourdhui';

const SEANCE: SeanceDuJour = {
  seanceId: 's1',
  debut: '10:00',
  fin: '12:00',
  groupe: { id: 'g1', nom: 'Groupe Samedi 10h → 12h', type: 'Etude', matiere: 'Mathématiques' },
  professeur: 'Karim Ben Salah',
  attendus: 14,
  etat: 'AppelAFaire',
};

describe("l'accueil « Aujourd'hui »", () => {
  it('montre les séances du jour, ma caisse et la bibliothèque', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'aujourdhui', component: Aujourdhui }, { path: '**', component: PageDeTest }]), provideHttpClient(), provideHttpClientTesting()],
    });
    const http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/aujourdhui');
    const page = harness.routeNativeElement as HTMLElement;

    (await vi.waitFor(() => http.expectOne('/api/seances/du-jour'))).flush([SEANCE, { ...SEANCE, seanceId: 's2', debut: '14:00', fin: '16:00', etat: 'Annulee' }]);
    http.expectOne('/api/caisse').flush({ ouvertes: [{ jour: '2027-11-27', rang: 1, encaisse: 845, sorties: 45, attendu: 800, recus: 12, oubliee: false, annules: 0 }], clotures: [] });
    http.expectOne('/api/bibliotheque').flush({ presents: 7, capacite: 30, entrees: 11 });
    http.expectOne('/api/seances/a-recopier').flush([]);
    http.expectOne('/api/eleves/en-rouge').flush([]);
    await vi.waitFor(() => expect(page.querySelectorAll('.seance')).toHaveLength(2));

    const premiere = page.querySelector('.seance') as HTMLAnchorElement;
    expect(premiere.textContent).toContain('Mathématiques · Groupe Samedi 10h → 12h');
    expect(premiere.textContent).toContain('Karim Ben Salah · 14 élèves attendus');
    expect(premiere.textContent).toContain('appel à faire');
    expect(premiere.getAttribute('href')).toBe('/professeurs/groupes/g1/appel/s1');
    expect(page.querySelectorAll('.seance')[1].textContent).toContain('annulée');
    expect(page.textContent).toContain('800,000 TND');
    expect(page.textContent).toContain('7 / 30');
    http.verify();
  });
});
