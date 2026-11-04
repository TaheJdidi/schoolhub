import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { FicheDuGroupe, SeanceDuGroupe } from './groupe';
import { PageDuGroupe } from './page-du-groupe';

describe('la fiche d’un groupe', () => {
  const ADRESSE = '/api/groupes/g1';
  const seance = (id: string, date: string): SeanceDuGroupe => ({ id, date, debut: '10:00', fin: '12:00' });
  const FICHE: FicheDuGroupe = {
    id: 'g1',
    nom: 'Groupe Samedi 10h → 12h',
    type: 'Etude',
    professeur: { id: 'k', prenom: 'Karim', nom: 'Ben Salah' },
    matiere: 'Mathématiques',
    niveau: { id: 'n9', nom: '9ᵉ année de base' },
    creneaux: [{ jour: 'Samedi', debut: '10:00', fin: '12:00' }],
    capacite: 16,
    eleves: 0,
    dateDeDebut: '2027-09-18',
    prixDuMois: 45,
    mois: [
      {
        numero: 1,
        prix: 40,
        commence: true,
        seances: [seance('s1', '2027-09-18'), seance('s2', '2027-09-25'), seance('s3', '2027-10-02'), seance('s4', '2027-10-09')],
      },
      { numero: 2, prix: 45, commence: false, seances: [seance('s5', '2027-10-16'), seance('s6', '2027-10-23')] },
    ],
  };

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  async function ouvrir(reponse: FicheDuGroupe | 404 = FICHE): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'professeurs/groupes/:id', component: PageDuGroupe },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/professeurs/groupes/g1');
    page = harness.routeNativeElement as HTMLElement;
    const requete = await vi.waitFor(() => http.expectOne(ADRESSE));
    if (reponse === 404) requete.flush({ message: 'Groupe introuvable' }, { status: 404, statusText: 'Not Found' });
    else requete.flush(reponse);
    await vi.waitFor(() => expect(page.querySelector('h1')).not.toBeNull());
  }

  afterEach(() => http.verify());

  const attendre = (texte: string) => vi.waitFor(() => expect(page.textContent).toContain(texte));
  const carte = () => page.querySelector('.le-groupe') as HTMLElement;
  const champ = (nom: string) => page.querySelector(`[formcontrolname="${nom}"]`) as HTMLInputElement;
  const bouton = (libelle: string) => {
    const trouve = [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === libelle);
    if (!trouve) throw new Error(`Aucun bouton « ${libelle} »`);
    return trouve as HTMLButtonElement;
  };

  async function cliquer(libelle: string): Promise<void> {
    bouton(libelle).click();
    await harness.fixture.whenStable();
  }

  it("montre l'en-tête, le fil et le groupe", async () => {
    await ouvrir();

    expect(page.querySelector('h1')?.textContent).toContain('Groupe Samedi 10h → 12h');
    expect(page.querySelector('.sub')?.textContent).toContain(
      'Mathématiques · 9ᵉ année de base · Karim Ben Salah · Étude, 45,000 TND par Mois',
    );
    const fil = page.querySelector('.fil') as HTMLElement;
    expect(fil.querySelector('a')?.getAttribute('href')).toBe('/professeurs');
    expect(fil.textContent).toContain('Karim Ben Salah');
    expect(carte().textContent).toContain('Samedi de 10h à 12h');
    expect(carte().textContent).toContain('depuis le 18/09/2027');
    expect(carte().textContent).toContain('0 élève sur 16');
  });

  it('montre les Mois, leur prix et leurs séances', async () => {
    await ouvrir();

    const mois = [...page.querySelectorAll('.mois')] as HTMLElement[];
    expect(mois).toHaveLength(2);
    expect(mois[0].textContent).toContain('Mois 1');
    expect(mois[0].textContent).toContain('40,000 TND');
    expect(mois[0].querySelector('.pill')?.textContent?.trim()).toBe('Commencé');
    expect([...mois[0].querySelectorAll('.seance')].map((s) => s.textContent?.trim())).toEqual([
      'sam. 18/09',
      'sam. 25/09',
      'sam. 02/10',
      'sam. 09/10',
    ]);
    expect(mois[1].textContent).toContain('45,000 TND');
    expect(mois[1].querySelector('.pill')).toBeNull();
  });

  it('modifie le nom, la capacité et le prix', async () => {
    await ouvrir();

    await cliquer('Modifier');
    expect([champ('nom').value, champ('capacite').value, champ('prix').value]).toEqual(['Groupe Samedi 10h → 12h', '16', '45,000']);
    expect(carte().textContent).toContain("Le nouveau prix vaut pour les Mois qui n'ont pas encore commencé.");

    saisir(page, '[formcontrolname="capacite"]', '');
    saisir(page, '[formcontrolname="prix"]', '50');
    envoyer(page);
    const requete = http.expectOne({ method: 'PUT', url: ADRESSE });
    expect(requete.request.body).toEqual({ nom: 'Groupe Samedi 10h → 12h', capacite: null, prixDuMois: 50 });
    await vi.waitFor(() => expect(bouton('Enregistrer').disabled).toBe(true));
    requete.flush({ ...FICHE, capacite: null, prixDuMois: 50, mois: [{ ...FICHE.mois[0] }, { ...FICHE.mois[1], prix: 50 }] });

    await attendre('Groupe enregistré');
    expect(page.querySelector('form')).toBeNull();
    expect(page.querySelector('.sub')?.textContent).toContain('50,000 TND par Mois');
    expect(carte().textContent).toContain('0 élève');
    expect(carte().textContent).not.toContain('sur 16');
  });

  it('annule la modification', async () => {
    await ouvrir();

    await cliquer('Modifier');
    await cliquer('Annuler');

    expect(page.querySelector('form')).toBeNull();
  });

  it('refuse un prix illisible, et montre le refus du serveur sous la carte', async () => {
    await ouvrir();
    await cliquer('Modifier');

    saisir(page, '[formcontrolname="prix"]', '');
    envoyer(page);
    await attendre('Écrivez le prix du Mois, par exemple 60,000');

    saisir(page, '[formcontrolname="prix"]', '45');
    saisir(page, '[formcontrolname="nom"]', 'a'.repeat(101));
    envoyer(page);
    http.expectOne(ADRESSE).flush({ message: 'Le nom du groupe fait au plus 100 caractères' }, { status: 400, statusText: 'Bad Request' });

    await attendre('Le nom du groupe fait au plus 100 caractères');
    expect(carte().querySelector('.note.bad')?.textContent).toContain('Le nom du groupe fait au plus 100 caractères');
    expect(page.querySelector('form')).not.toBeNull();
  });

  it("dit quand le groupe n'existe pas", async () => {
    await ouvrir(404);

    expect(page.textContent).toContain("Ce groupe n'existe pas.");
    expect(page.querySelector('a[href="/professeurs"]')).not.toBeNull();
    expect(page.querySelector('.le-groupe')).toBeNull();
  });
});
