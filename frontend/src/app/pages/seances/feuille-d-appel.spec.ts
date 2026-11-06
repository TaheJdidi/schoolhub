import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { FeuilleDAppel as Feuille } from './appel';
import { FeuilleDAppel } from './feuille-d-appel';

describe("la feuille d'appel", () => {
  const FEUILLE: Feuille = {
    centre: 'Centre Les Jasmins',
    annee: '2027-2028',
    groupe: 'Groupe Samedi 10h → 12h',
    matiere: 'Mathématiques',
    niveau: '9ᵉ année de base',
    professeur: 'Karim Ben Salah',
    seances: [
      { date: '2027-11-13', debut: '10:00' },
      { date: '2027-11-27', debut: '10:00' },
      { date: '2027-12-02', debut: '17:00' },
    ],
    eleves: [
      { nom: 'Ayari', prenom: 'Sara' },
      { nom: 'Miled', prenom: 'Adem' },
    ],
  };

  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'feuille-d-appel/:id', component: FeuilleDAppel },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/feuille-d-appel/g1?du=2027-11-13&au=2027-12-04');
    page = harness.routeNativeElement as HTMLElement;
  });

  afterEach(() => http.verify());

  it("demande la feuille de la période, puis montre l'en-tête, les cases, les lignes de rattrapage et la consigne", async () => {
    (await vi.waitFor(() => http.expectOne('/api/groupes/g1/feuille-d-appel?du=2027-11-13&au=2027-12-04'))).flush(FEUILLE);
    await vi.waitFor(() => expect(page.querySelector('table')).not.toBeNull());

    expect(page.querySelector('.entete')?.textContent).toContain("Feuille d'appel · année 2027-2028");
    expect(page.querySelector('.groupe')?.textContent).toContain('Mathématiques · 9ᵉ année de base');
    expect(page.querySelector('.groupe')?.textContent).toContain('Professeur : Karim Ben Salah');
    // La date et l'heure sont sur deux lignes : chaque morceau est lu à part.
    const enTete = (th: Element) => [...th.childNodes].map((n) => n.textContent?.trim()).filter(Boolean).join(' ');
    expect([...page.querySelectorAll('thead th')].map(enTete)).toEqual([
      'N°',
      'Nom',
      'Prénom',
      '13/11 10h',
      '27/11 10h',
      '02/12 17h',
      'Signature',
    ]);
    const lignes = [...page.querySelectorAll('tbody tr')];
    expect(lignes[0].textContent).toContain('Ayari');
    expect(lignes[1].textContent).toContain('Miled');
    expect(lignes[2].textContent).toContain('Élèves en rattrapage');
    expect(page.querySelectorAll('tbody tr.vide')).toHaveLength(3);
    expect(page.querySelector('.pied')?.textContent).toContain("Écrivez A dans la case d'un élève absent. Les autres sont présents.");
  });

  it('montre le refus du serveur', async () => {
    (await vi.waitFor(() => http.expectOne('/api/groupes/g1/feuille-d-appel?du=2027-11-13&au=2027-12-04'))).flush(
      { message: 'La période fait au plus 3 mois' },
      { status: 400, statusText: 'Bad Request' },
    );

    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('La période fait au plus 3 mois'));
  });
});
