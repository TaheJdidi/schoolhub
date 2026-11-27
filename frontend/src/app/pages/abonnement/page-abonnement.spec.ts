import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SituationDuCentre } from '../../socle/abonnement';
import { PageAbonnement } from './page-abonnement';

const SUSPENDU: SituationDuCentre = {
  etat: 'Suspendu',
  payeJusquAu: '2027-11-30',
  finDeLaPeriodeDeGrace: null,
  finProche: false,
  lectureSeule: true,
  supportJusquAu: null,
  exportPossible: true,
  exportPossibleJusquAu: '2028-12-15',
};

describe('la page Abonnement du propriétaire', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function ouvrir(situation: SituationDuCentre): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(PageAbonnement);
    http.expectOne('/api/abonnement').flush(situation);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it("propose l'export des données d'un centre suspendu, jusqu'à sa date", async () => {
    const page = await ouvrir(SUSPENDU);

    expect(page.textContent).toContain('Suspendu');
    expect(page.textContent).toContain("Jusqu'au 15/12/2028");
    expect(page.querySelector('a[href="/api/abonnement/export"]')).not.toBeNull();
  });

  it("autorise le support 24 h, puis montre jusqu'à quand", async () => {
    const page = await ouvrir({ ...SUSPENDU, etat: 'Actif', lectureSeule: false, exportPossible: false, exportPossibleJusquAu: null });
    expect(page.querySelector('a[href="/api/abonnement/export"]')).toBeNull();

    (page.querySelector('button.primary') as HTMLButtonElement).click();
    const requete = http.expectOne('/api/abonnement/support');
    expect(requete.request.method).toBe('POST');
    requete.flush({ ...SUSPENDU, etat: 'Actif', supportJusquAu: '2027-11-28T09:00:00+00:00', exportPossible: false });

    await vi.waitFor(() => expect(page.textContent).toContain("L'équipe SchoolUp peut voir votre centre"));
    expect(page.textContent).toContain('28/11/2027 à 10h00');
  });
});
