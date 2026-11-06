import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { DemandeDeLaListe } from './demande';
import { Demandes } from './demandes';

describe('la page « Demandes d’inscription »', () => {
  const LIEN = 'https://app.schoolhub.tn/inscription/les-jasmins';

  const demande = (id: string, autres: Partial<DemandeDeLaListe>): DemandeDeLaListe => ({
    id,
    prenom: 'Adem',
    nom: 'Miled',
    niveau: { id: 'n9', nom: '9ᵉ année de base' },
    matieres: [{ matiere: 'Mathématiques', professeur: 'Karim Ben Salah', creneaux: [{ jour: 'Samedi', debut: '10:00', fin: '12:00' }] }],
    amis: null,
    recueLe: '2027-11-27T09:00:00+00:00',
    doublonPossible: false,
    etat: 'ATraiter',
    motif: null,
    traiteeLe: null,
    ...autres,
  });

  const ADEM = demande('d1', { amis: 'Sami Chaabane', doublonPossible: true });
  const LINA = demande('d2', {
    prenom: 'Lina',
    nom: 'Gharbi',
    niveau: { id: 'n4', nom: '4ᵉ année secondaire' },
    matieres: [{ matiere: 'Physique', professeur: 'Lina Gharbi', creneaux: [{ jour: 'Mardi', debut: '17:30', fin: '19:00' }] }],
  });

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;
  let ecrire: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    ecrire = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: ecrire }, configurable: true });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'demandes', component: Demandes },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/demandes');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/demandes/lien'))).flush({ lien: LIEN });
    http.expectOne('/api/demandes').flush([ADEM, LINA]);
    await vi.waitFor(() => expect(page.querySelector('.lien')).not.toBeNull());
    await vi.waitFor(() => expect(page.querySelectorAll('a.demande')).toHaveLength(2));
  });

  afterEach(() => http.verify());

  const bouton = (texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte);
  const lignes = () => [...page.querySelectorAll('a.demande')].map((a) => a.textContent?.replace(/\s+/g, ' ').trim() ?? '');
  const choisir = (libelle: string, valeur: string) => {
    const liste = page.querySelector(`select[aria-label="${libelle}"]`) as HTMLSelectElement;
    liste.value = valeur;
    liste.dispatchEvent(new Event('change'));
  };

  it('montre les demandes à traiter, dans l’ordre du serveur, avec leur nombre', () => {
    expect(page.querySelector('.sub')?.textContent).toContain('2 demandes à traiter');
    expect(lignes()[0]).toContain('Adem Miled');
    expect(lignes()[0]).toContain('Doublon possible');
    expect(lignes()[0]).toContain('9ᵉ année de base · Mathématiques (samedi 10h)');
    expect(lignes()[0]).toContain('Amis : Sami Chaabane');
    expect(lignes()[0]).toContain('Reçue le 27/11/2027');
    expect(lignes()[1]).toContain('Physique (mardi 17h30)');
    expect(lignes()[1]).not.toContain('Doublon possible');
    expect(page.querySelector('a.demande')?.getAttribute('href')).toBe('/demandes/d1');
  });

  it('filtre par niveau et par matière', async () => {
    choisir('Niveau', '4ᵉ année secondaire');
    await vi.waitFor(() => expect(lignes()).toHaveLength(1));
    expect(lignes()[0]).toContain('Lina Gharbi');

    choisir('Matière', 'Mathématiques');
    await vi.waitFor(() => expect(page.textContent).toContain('Aucune demande pour ce filtre.'));

    choisir('Niveau', '');
    await vi.waitFor(() => expect(lignes()).toHaveLength(1));
    expect(lignes()[0]).toContain('Adem Miled');
  });

  it('montre les demandes traitées, avec leur motif', async () => {
    bouton('Traitées')!.click();
    http.expectOne('/api/demandes?traitees=true').flush([
      demande('d3', { etat: 'Refusee', motif: 'FausseDemande', traiteeLe: '2027-11-28T08:00:00+00:00' }),
      demande('d4', { prenom: 'Sara', etat: 'Inscrite', traiteeLe: '2027-11-28T09:00:00+00:00' }),
    ]);

    await vi.waitFor(() => expect(lignes()[0]).toContain('Refusée · Fausse demande'));
    expect(lignes()).toHaveLength(2);
    expect(lignes()[0]).toContain('Traitée le 28/11/2027');
    expect(lignes()[1]).toContain('Inscrit');
  });

  it('dit quand il n’y a rien à traiter', async () => {
    bouton('Traitées')!.click();
    http.expectOne('/api/demandes?traitees=true').flush([]);

    await vi.waitFor(() => expect(page.textContent).toContain('Aucune demande traitée.'));
  });

  it('montre le lien public du centre et son QR code', () => {
    expect(page.querySelector('h1')?.textContent).toContain("Demandes d'inscription");
    expect(page.querySelector('.lien')?.textContent).toContain(LIEN);
    expect(page.querySelector('.lien')?.getAttribute('href')).toBe(LIEN);
    expect(page.querySelector('img.qr-code')?.getAttribute('src')).toBe('/api/demandes/qr-code');
    expect(page.querySelector('a[href="/demandes/affiche"]')?.textContent).toContain("Imprimer l'affiche");
  });

  it('copie le lien', async () => {
    bouton('Copier le lien')!.click();

    await vi.waitFor(() => expect(page.querySelector('[role="status"]')?.textContent).toContain('Lien copié'));
    expect(ecrire).toHaveBeenCalledWith(LIEN);
  });

  it('dit quand la copie échoue', async () => {
    ecrire.mockRejectedValue(new Error('refusé'));

    bouton('Copier le lien')!.click();

    await vi.waitFor(() =>
      expect(page.querySelector('[role="alert"]')?.textContent).toContain("Le lien n'a pas pu être copié : sélectionnez-le à la main."),
    );
  });
});
