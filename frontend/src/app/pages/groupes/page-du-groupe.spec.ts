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
    termine: false,
    premiereSeance: 1,
    prixTotal: null,
    nombreDeSeances: null,
    seances: [],
    tranches: [],
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

  // ----- Révision, Formation, groupe déjà en cours (GRP-05 à GRP-07) -----

  const numerotee = (numero: number, date: string, prix: number | null) => ({ id: `n${numero}`, numero, date, debut: '10:00', fin: '12:00', prix });
  const REVISION: FicheDuGroupe = {
    ...FICHE,
    type: 'Revision',
    prixDuMois: null,
    mois: [],
    prixTotal: 50,
    nombreDeSeances: 3,
    seances: [numerotee(1, '2028-05-20', 16.667), numerotee(2, '2028-05-27', 16.667), numerotee(3, '2028-06-03', 16.666)],
  };
  const FORMATION: FicheDuGroupe = {
    ...FICHE,
    type: 'Formation',
    niveau: null,
    prixDuMois: null,
    mois: [],
    prixTotal: 300,
    nombreDeSeances: 10,
    premiereSeance: 9,
    seances: [numerotee(9, '2028-05-20', null), numerotee(10, '2028-05-27', null)],
    tranches: [
      { montant: 100, seance: 1 },
      { montant: 100, seance: 5 },
      { montant: 100, seance: 9 },
    ],
  };
  // Chaque élément d'une ligne est lu à part : l'écran les sépare par la mise en page, pas par des espaces.
  const lignes = (selecteur: string) =>
    [...page.querySelectorAll(selecteur)].map((ligne) =>
      ligne.children.length > 0
        ? [...ligne.children].map((morceau) => morceau.textContent?.trim()).join(' ')
        : ligne.textContent?.replace(/\s+/g, ' ').trim(),
    );

  it('montre une Révision, ses séances et leur prix', async () => {
    await ouvrir(REVISION);

    expect(page.querySelector('.sub')?.textContent).toContain('Révision, 50,000 TND les 3 séances, ou 16,667 TND la séance');
    expect(page.querySelectorAll('.mois')).toHaveLength(0);
    expect(lignes('.seance-numerotee')).toEqual([
      'Séance 1 sam. 20/05 10h → 12h 16,667 TND',
      'Séance 2 sam. 27/05 10h → 12h 16,667 TND',
      'Séance 3 sam. 03/06 10h → 12h 16,666 TND',
    ]);
  });

  it('montre une Formation déjà en cours et ses tranches', async () => {
    await ouvrir(FORMATION);

    expect(page.querySelector('.sub')?.textContent).toContain('Mathématiques · Karim Ben Salah · Formation, 300,000 TND, en une fois ou en 3 tranches');
    expect(lignes('.seance-numerotee')).toEqual(['Séance 9 sam. 20/05 10h → 12h', 'Séance 10 sam. 27/05 10h → 12h']);
    expect(lignes('.tranche-du-groupe')).toEqual([
      'Tranche 1 · 100,000 TND, due à la séance 1',
      'Tranche 2 · 100,000 TND, due à la séance 5',
      'Tranche 3 · 100,000 TND, due à la séance 9',
    ]);
  });

  it('dit qu’une Formation sans tranche se paie en une fois', async () => {
    await ouvrir({ ...FORMATION, tranches: [] });

    expect(page.querySelector('.sub')?.textContent?.trim()).toMatch(/Formation, 300,000 TND, en une fois$/);
    expect(page.querySelectorAll('.tranche-du-groupe')).toHaveLength(0);
  });

  it('montre un groupe terminé', async () => {
    await ouvrir({ ...REVISION, termine: true });

    expect(page.querySelector('h1 .pill')?.textContent?.trim()).toBe('Terminé');
  });

  it('modifie une Révision sans toucher à son prix', async () => {
    await ouvrir(REVISION);

    await cliquer('Modifier');
    expect(champ('prix')).toBeNull();
    expect(carte().textContent).not.toContain('Le nouveau prix');
    saisir(page, '[formcontrolname="nom"]', 'Révision du bac');
    envoyer(page);

    const requete = http.expectOne({ method: 'PUT', url: ADRESSE });
    expect(requete.request.body).toEqual({ nom: 'Révision du bac', capacite: 16, prixDuMois: null });
    requete.flush({ ...REVISION, nom: 'Révision du bac' });
    await attendre('Groupe enregistré');
  });

  it('montre une Étude déjà en cours à partir de son Mois en cours', async () => {
    await ouvrir({ ...FICHE, premiereSeance: 17, mois: FICHE.mois.map((m, i) => ({ ...m, numero: 5 + i })) });

    expect(page.querySelector('.mois')?.textContent).toContain('Mois 5');
  });
});
