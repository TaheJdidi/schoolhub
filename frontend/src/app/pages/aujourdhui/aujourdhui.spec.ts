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
    http.expectOne('/api/caisse').flush({
      ouvertes: [{ jour: '2027-11-27', rang: 1, encaisse: 845, sorties: 45, attendu: 800, recus: 12, oubliee: false, annules: 0, cheques: 50, nombreDeCheques: 1 }],
      clotures: [],
    });
    http.expectOne('/api/bibliotheque').flush({ presents: 7, capacite: 30, entrees: 11 });
    http.expectOne('/api/eleves/en-rouge').flush([]);
    http.expectOne('/api/groupes').flush([{ id: 'g1', badge: { etat: 'Rappels', nombre: 5 } }]);
    await vi.waitFor(() => expect(page.querySelectorAll('.seance')).toHaveLength(2));

    const premiere = page.querySelector('.seance') as HTMLAnchorElement;
    expect(premiere.textContent).toContain('Mathématiques');
    expect(premiere.textContent).toContain('Karim Ben Salah · Groupe Samedi 10h → 12h · 14 élèves');
    expect(premiere.textContent).toContain('5 à relancer');
    // Selon l'heure qu'il est : à venir, en cours, ou appel à faire.
    expect(premiere.textContent).toMatch(/À venir|En cours|Appel à faire/);
    // Comme depuis le menu Professeurs : l'appel s'ouvre dans la fiche du groupe, sur la séance du jour.
    expect(premiere.getAttribute('href')).toBe('/professeurs/groupes/g1?seance=s1');
    expect(page.querySelectorAll('.seance')[1].textContent).toContain('Annulée');
    // Seule la séance qui a lieu compte dans les élèves attendus.
    expect(page.textContent).toContain('2 séances · 14 élèves attendus');
    // La caisse : le total (espèces + chèques), puis le détail, puis les reçus.
    const caisse = [...page.querySelectorAll('.chiffre')][0];
    expect(caisse.querySelector('b')?.textContent).toContain('895,000');
    const parts = [...caisse.querySelectorAll('.part')].map((part) => [part.querySelector('small')?.textContent?.replace(/\s+/g, ' ').trim(), part.querySelector('b')?.textContent]);
    expect(parts).toEqual([['Espèces', '845,000'], ['Chèque · 1', '50,000']]);
    expect(caisse.querySelector('.pill')?.textContent?.trim()).toBe('12 reçus');
    expect(caisse.textContent).not.toContain('attendues');
    // SEA-09 : les appels à recopier sont masqués pour le moment, ni carte ni liste, et rien n'est demandé à l'API.
    expect(page.textContent).not.toContain('à recopier');
    http.expectNone('/api/seances/a-recopier');
    const bibliotheque = page.querySelector('a.chiffre[href="/bibliotheque"]');
    expect(bibliotheque?.querySelector('b .num')?.textContent).toBe('7');
    expect(bibliotheque?.querySelector('b small')?.textContent).toBe('/ 30 places');
    // 7 places sur 30 : la jauge est remplie à 23 %.
    expect((page.querySelector('.jauge i') as HTMLElement).style.width).toBe('23%');
    // Aucune demande : « Tout est traité ».
    expect(page.querySelector('.tout-est-traite')?.textContent).toContain('Tout est traité');
    http.verify();
  });

  it('la caisse sans chèque : le chèque à 0, en gris', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'aujourdhui', component: Aujourdhui }, { path: '**', component: PageDeTest }]), provideHttpClient(), provideHttpClientTesting()],
    });
    const http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/aujourdhui');
    const page = harness.routeNativeElement as HTMLElement;

    (await vi.waitFor(() => http.expectOne('/api/seances/du-jour'))).flush([]);
    http.expectOne('/api/caisse').flush({ ouvertes: [{ jour: '2027-11-27', rang: 1, encaisse: 150, sorties: 0, attendu: 150, recus: 2, oubliee: false, annules: 0 }], clotures: [] });
    http.expectOne('/api/bibliotheque').flush({ presents: 0, capacite: null, entrees: 0 });
    http.expectOne('/api/eleves/en-rouge').flush([]);
    http.expectOne('/api/groupes').flush([]);

    await vi.waitFor(() => expect(page.querySelector('.chiffre b')?.textContent).toContain('150,000'));
    const parts = [...page.querySelectorAll('.part')].map((part) => [part.querySelector('small')?.textContent?.replace(/\s+/g, ' ').trim(), part.querySelector('b')?.textContent]);
    expect(parts).toEqual([['Espèces', '150,000'], ['Chèque', '0,000']]);
    expect(page.querySelectorAll('.part b')[1].classList).toContain('zero');
    // Sans capacité connue, pas de jauge.
    expect(page.querySelector('.jauge')).toBeNull();
    http.verify();
  });
});
