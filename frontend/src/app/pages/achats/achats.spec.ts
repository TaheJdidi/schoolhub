import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { AchatDecrit, Achats, AchatsEtTotaux } from './achats';

const RAMETTES: AchatDecrit = {
  id: 'a1',
  jour: '2027-11-27',
  libelle: 'Ramettes de papier',
  categorie: 'Materiel',
  montant: 45,
  fournisseur: 'Librairie du Centre',
  payeDepuisLaCaisse: true,
  auteur: 'Amira Jlassi',
  aUneFacture: true,
  modifiable: true,
  pourquoiPasModifiable: null,
};

const LISTE: AchatsEtTotaux = {
  achats: [RAMETTES, { ...RAMETTES, id: 'a2', libelle: 'Ménage', categorie: 'Service', montant: 450, payeDepuisLaCaisse: false, aUneFacture: false, modifiable: false }],
  totalMateriel: 45,
  totalService: 450,
  total: 495,
};

describe('les achats', () => {
  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'achats', component: Achats }, { path: '**', component: PageDeTest }]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/achats');
    page = harness.routeNativeElement as HTMLElement;
    const premiere = await vi.waitFor(() => http.expectOne((r) => r.url === '/api/achats'));
    expect(premiere.request.params.get('mois')).toMatch(/^\d{4}-\d{2}$/);
    premiere.flush(LISTE);
    await vi.waitFor(() => expect(page.querySelectorAll('tbody tr')).toHaveLength(2));
  });

  afterEach(() => http.verify());

  const bouton = (texte: string, dans: ParentNode = page) => [...dans.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte)!;
  const champ = (nom: string) => page.querySelector(`[formControlName="${nom}"]`) as HTMLInputElement;

  it('montre la liste, la sortie de caisse, la facture et les totaux', () => {
    const lignes = page.querySelectorAll('tbody tr');
    expect(lignes[0].textContent).toContain('sortie de caisse');
    expect(lignes[0].textContent).toContain('Librairie du Centre · Amira Jlassi');
    expect(lignes[0].querySelector('a')?.getAttribute('href')).toBe('/api/achats/a1/facture');
    // Un achat que la personne ne peut plus changer n'a ni « Modifier » ni « Supprimer ».
    expect(bouton('Modifier', lignes[1])).toBeUndefined();
    expect(page.querySelector('tfoot')?.textContent).toContain('495,000 TND');
    // Le résumé du haut : le total, chaque catégorie, et seuls les achats payés depuis la caisse comme sortie de caisse.
    const chiffres = [...page.querySelectorAll('.chiffres > div')].map((carte) => [...carte.children].map((partie) => partie.textContent?.replace(/\s+/g, ' ').trim()).join(' : '));
    expect(chiffres).toEqual(['Total du mois : 495,000 TND', 'Matériel : 45,000', 'Service : 450,000', 'Sorti de la caisse : 45,000']);
  });

  it('enregistre un nouvel achat payé depuis la caisse', async () => {
    bouton('Nouvel achat').click();
    await vi.waitFor(() => expect(page.querySelector('form')).not.toBeNull());
    champ('libelle').value = 'Ramettes de papier';
    champ('libelle').dispatchEvent(new Event('input'));
    champ('montant').value = '45,000';
    champ('montant').dispatchEvent(new Event('input'));
    champ('payeDepuisLaCaisse').click();

    bouton('Enregistrer').click();
    const requete = http.expectOne({ method: 'POST', url: '/api/achats' });
    expect(requete.request.body).toEqual(expect.objectContaining({ libelle: 'Ramettes de papier', categorie: 'Materiel', montant: 45, payeDepuisLaCaisse: true }));
    requete.flush(RAMETTES);
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/achats'))).flush(LISTE);
    await vi.waitFor(() => expect(page.textContent).toContain('est noté en sortie de caisse'));
  });

  it('refait un achat à la date du jour, et le modifie', async () => {
    bouton('Refaire', page.querySelectorAll('tbody tr')[1]).click();
    await vi.waitFor(() => expect(champ('libelle').value).toBe('Ménage'));
    expect(champ('montant').value).toBe('450,000');
    expect(champ('jour').value).not.toBe('2027-11-27');
    bouton('Annuler').click();

    bouton('Modifier', page.querySelectorAll('tbody tr')[0]).click();
    await vi.waitFor(() => expect(champ('jour').value).toBe('2027-11-27'));
    bouton('Enregistrer').click();
    (await vi.waitFor(() => http.expectOne({ method: 'PUT', url: '/api/achats/a1' }))).flush(RAMETTES);
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/achats'))).flush(LISTE);
  });

  it('supprime un achat, et filtre par année et catégorie', async () => {
    bouton('Supprimer', page.querySelectorAll('tbody tr')[0]).click();
    http.expectOne({ method: 'DELETE', url: '/api/achats/a1' }).flush(null, { status: 204, statusText: 'No Content' });
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/achats'))).flush(LISTE);

    const [periode, categorie] = [...page.querySelectorAll('.filtres select')] as HTMLSelectElement[];
    periode.value = 'annee';
    periode.dispatchEvent(new Event('change'));
    const parAnnee = await vi.waitFor(() => http.expectOne((r) => r.url === '/api/achats'));
    expect(parAnnee.request.params.get('annee')).toMatch(/^\d{4}$/);
    parAnnee.flush(LISTE);

    categorie.value = 'Service';
    categorie.dispatchEvent(new Event('change'));
    const parCategorie = await vi.waitFor(() => http.expectOne((r) => r.url === '/api/achats'));
    expect(parCategorie.request.params.get('categorie')).toBe('Service');
    parCategorie.flush(LISTE);
  });
});
