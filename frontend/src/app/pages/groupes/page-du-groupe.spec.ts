import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { InscriptionDeLEleve } from '../inscriptions/inscription';
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
    fermeLe: null,
    motifDeFermeture: null,
  };

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  const INSCRIPTION: InscriptionDeLEleve = {
    id: 'i1',
    eleve: { id: 'e1', prenom: 'Adem', nom: 'Miled' },
    groupe: { id: 'g1', nom: 'Groupe Samedi 10h → 12h', type: 'Revision', matiere: 'Mathématiques' },
    professeur: 'Karim Ben Salah',
    mode: 'EnTotalite',
    prixNormal: 100,
    nombreDeTranches: 0,
    premiereSeance: { date: '2027-09-25', debut: '10:00:00' },
    prixSpecial: null,
    raisonDuPrixSpecial: null,
    reglees: [],
    payablesAvantSchoolHub: [],
    peutRegrouperLeReste: false,
    arreteLe: null,
    report: 0,
  };

  /** La fiche du groupe et ses élèves, demandés ensemble. */
  async function ouvrir(reponse: FicheDuGroupe | 404 = FICHE, inscriptions: InscriptionDeLEleve[] = []): Promise<void> {
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
    const sesEleves = http.expectOne((r) => r.url === '/api/inscriptions' && r.params.get('groupe') === 'g1');
    if (reponse === 404) {
      for (const une of [requete, sesEleves]) une.flush({ message: 'Groupe introuvable' }, { status: 404, statusText: 'Not Found' });
    } else {
      requete.flush(reponse);
      sesEleves.flush(inscriptions);
    }
    await vi.waitFor(() => expect(page.querySelector('h1')).not.toBeNull());
  }

  /** L'appel de la séance choisie se demande à part (prototype, écran 3) : un test qui ne s'en sert pas le laisse sans réponse. */
  afterEach(() => {
    http.match((r) => /^\/api\/seances\/[^/]+\/appel$/.test(r.url)).forEach((r) => r.flush({ message: 'Séance introuvable' }, { status: 404, statusText: 'Not Found' }));
    http.match((r) => /^\/api\/groupes\/[^/]+\/encaissement$/.test(r.url)).forEach((r) => r.flush({ groupe: {}, aPayer: [], aJour: [] }));
    http.verify();
  });

  const attendre = (texte: string) => vi.waitFor(() => expect(page.textContent).toContain(texte));
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
    // Le badge de paiement n'est plus à côté du titre : il reste dans « Paiements du Mois ».
    expect(page.querySelector('h1 sh-badge-du-groupe')).toBeNull();
    expect(pastilles()).toEqual(['Mathématiques', '9ᵉ année de base', 'Karim Ben Salah', 'Étude, 45,000 TND par Mois', 'Samedi 10h → 12h Changer']);
    const fil = page.querySelector('.fil') as HTMLElement;
    expect(fil.querySelector('a')?.getAttribute('href')).toBe('/professeurs');
    expect(fil.textContent).toContain('Karim Ben Salah');
    // « Modifier » et « Fermer le groupe » se font depuis la page du professeur : plus de bloc « Le groupe » ici.
    expect(page.querySelector('.le-groupe')).toBeNull();
    // Le paiement se fait dans la colonne « Paiement » de l'appel : plus de bouton « Paiement du groupe ».
    expect([...page.querySelectorAll('a')].some((a) => a.textContent?.trim() === 'Paiement du groupe')).toBe(false);
  });

  /** Les pastilles du sous-titre, dans l'ordre. */
  const pastilles = () => [...page.querySelectorAll('.pastille')].map((p) => p.textContent?.replace(/\s+/g, ' ').trim());

  /** Les puces de la bande des séances : leur jour, sans leur état. */
  const puces = () => [...page.querySelectorAll('.puce-seance:not(.mois):not(.ajouter) .jour')].map((p) => p.textContent?.trim());
  const etats = () => [...page.querySelectorAll('.puce-seance:not(.mois):not(.ajouter) .choix-de-la-seance small')].map((p) => p.textContent?.trim());
  const choisie = () => page.querySelector('.choix-de-la-seance[aria-pressed="true"] .jour')?.textContent?.trim();

  it("montre les séances d'un Mois, et passe au Mois suivant", async () => {
    await ouvrir();

    // Les séances du Mois de la séance choisie (ici la première : elles sont toutes à venir), et la puce du Mois 2.
    expect(puces()).toEqual(['sam. 18/09', 'sam. 25/09', 'sam. 02/10', 'sam. 09/10']);
    expect(choisie()).toBe('sam. 18/09');
    const suivant = page.querySelector('.puce-seance.mois') as HTMLButtonElement;
    expect(suivant.textContent).toContain('Mois 2 ›');
    suivant.click();
    await harness.fixture.whenStable();
    expect(puces()[0]).toBe('sam. 16/10');
    expect(page.querySelector('.puce-seance.mois')?.textContent).toContain('‹ Mois 1');
    // La colonne « Paiement » montre ce qui sera dû à cette séance à venir, pas seulement aujourd'hui.
    const demandes = http.match((r) => /^\/api\/groupes\/[^/]+\/encaissement$/.test(r.url));
    expect(demandes.at(-1)?.request.params.get('au')).toMatch(/-10-16$/);
    demandes.forEach((r) => r.flush({ groupe: {}, aPayer: [], aJour: [] }));
    // SEA-07 : l'appel de la séance choisie s'ouvre sur la fiche même.
    expect(page.querySelector('sh-page-de-l-appel')).not.toBeNull();
  });

  /** L'appel de s1 (le 18/09), pour que les boutons de sa puce puissent ouvrir leur formulaire. */
  async function repondreALAppelDeS1(appelFait: boolean): Promise<void> {
    (await vi.waitFor(() => http.expectOne('/api/seances/s1/appel'))).flush({
      seanceId: 's1', date: '2027-09-18', debut: '10:00', fin: '12:00',
      groupe: { id: 'g1', nom: 'Groupe Samedi 10h → 12h', type: 'Etude', matiere: 'Mathématiques' },
      professeur: 'Karim Ben Salah', ouvert: !appelFait, appelFait, eleves: [],
    });
    await vi.waitFor(() => expect(page.querySelector('.appel-integre')).not.toBeNull());
  }
  const puceChoisie = () => page.querySelector('.puce-seance.choisie') as HTMLElement;

  it('la séance choisie porte Déplacer et Annuler, qui ouvrent leur formulaire sous la bande', async () => {
    await ouvrir();
    await repondreALAppelDeS1(false);

    // Plus de bloc « Cette séance » avec ses boutons, ni de carte « Le calendrier ».
    expect(page.textContent).not.toContain('Le calendrier');
    expect([...puceChoisie().querySelectorAll('.mini')].map((b) => b.getAttribute('aria-label'))).toEqual(['Déplacer cette séance', 'Annuler cette séance']);
    expect(page.querySelectorAll('.puce-seance:not(.choisie) .mini')).toHaveLength(0);

    (puceChoisie().querySelector('[aria-label="Annuler cette séance"]') as HTMLButtonElement).click();
    await harness.fixture.whenStable();

    // Le formulaire s'ouvre dans une fenêtre, au centre, et se referme avec Échap.
    const fenetre = page.querySelector('[role="dialog"]') as HTMLElement;
    expect(fenetre.querySelector('h2')?.textContent).toContain('Annuler la séance · sam. 18/09');
    expect(fenetre.textContent).toContain("Motif de l'annulation");
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await harness.fixture.whenStable();
    expect(page.querySelector('[role="dialog"]')).toBeNull();
  });

  it("une séance qui a son appel n'a plus de boutons ; « + Ajouter » ouvre la nouvelle séance sous la bande", async () => {
    const [premiere, ...autres] = FICHE.mois[0].seances;
    await ouvrir({ ...FICHE, mois: [{ ...FICHE.mois[0], seances: [{ ...premiere, appelFait: true }, ...autres] }, FICHE.mois[1]] });
    await repondreALAppelDeS1(true);

    expect(puceChoisie().querySelectorAll('.mini')).toHaveLength(0);
    (page.querySelector('.puce-seance.ajouter') as HTMLButtonElement).click();
    await harness.fixture.whenStable();

    // Le formulaire s'ouvre dans une fenêtre.
    expect(page.querySelector('[role="dialog"] h2')?.textContent).toContain('Ajouter une séance');
    expect(page.querySelector('[role="dialog"]')?.textContent).toContain('le Mois garde 4 séances');
  });

  it('montre une séance annulée dans la bande, barrée, et propose de la remplacer', async () => {
    await ouvrir({
      ...FICHE,
      horsCompte: [{ id: 'x1', date: '2027-09-25', debut: '10:00', fin: '12:00', nature: 'Prevue', annulee: true, motif: 'Férié', remplaceeLe: null, appelFait: false }],
    });

    expect(puces()).toContain('sam. 25/09');
    const annulee = page.querySelector('.puce-seance.annulee') as HTMLElement;
    expect(annulee.querySelector('small')?.textContent?.trim()).toBe('annulée');
    (annulee.querySelector('.choix-de-la-seance') as HTMLButtonElement).click();
    await harness.fixture.whenStable();
    expect([...puceChoisie().querySelectorAll('.mini')].map((b) => b.getAttribute('aria-label'))).toEqual(['Remplacer cette séance']);
  });

  it("n'a ni boutons ni « + Ajouter » pour un groupe fermé", async () => {
    await ouvrir({ ...FICHE, fermeLe: '2027-10-01', motifDeFermeture: "Trop peu d'élèves" });

    expect(page.querySelectorAll('.mini')).toHaveLength(0);
    expect(page.querySelector('.puce-seance.ajouter')).toBeNull();
  });

  it("« Changer », dans la pastille de l'horaire, ouvre le nouvel horaire dans le calendrier", async () => {
    await ouvrir();

    (page.querySelector('.pastille.horaire button[aria-label="Changer l\'horaire"]') as HTMLButtonElement).click();
    await harness.fixture.whenStable();

    expect(page.querySelector('[role="dialog"] h2')?.textContent).toContain("Changer l'horaire du groupe");
    // Le calendrier n'a plus son propre bouton.
    expect([...page.querySelectorAll('button')].some((b) => b.textContent?.trim() === "Changer l'horaire du groupe")).toBe(false);
  });

  it("ne propose pas de changer l'horaire d'un groupe fermé", async () => {
    await ouvrir({ ...FICHE, fermeLe: '2027-10-01', motifDeFermeture: "Trop peu d'élèves" });

    expect(page.querySelector('.pastille.horaire')?.textContent?.trim()).toBe('Samedi 10h → 12h');
    expect(page.querySelector('.pastille.horaire button')).toBeNull();
  });

  it('glisse une séance supplémentaire dans la bande, à sa date, dans le Mois en cours ce jour-là', async () => {
    await ouvrir({
      ...FICHE,
      horsCompte: [
        { id: 'x1', date: '2027-09-28', debut: '17:00', fin: '19:00', nature: 'Supplementaire', annulee: false, motif: null, remplaceeLe: null, appelFait: false },
        { id: 'x2', date: '2027-10-02', debut: '10:00', fin: '12:00', nature: 'Prevue', annulee: true, motif: 'Férié', remplaceeLe: null, appelFait: false },
      ],
    });

    // La séance annulée du 02/10 reste aussi dans la bande, barrée, à côté de la séance prévue ce jour-là.
    expect(puces()).toEqual(['sam. 18/09', 'sam. 25/09', 'mar. 28/09', 'sam. 02/10', 'sam. 02/10', 'sam. 09/10']);
    expect(etats()[2]).toBe('supplémentaire · à venir');
    expect(page.querySelectorAll('.puce-seance.annulee')).toHaveLength(1);
  });

  it("dit quand le groupe n'existe pas", async () => {
    await ouvrir(404);

    expect(page.textContent).toContain("Ce groupe n'existe pas.");
    expect(page.querySelector('a[href="/professeurs"]')).not.toBeNull();
    expect(page.querySelector('.le-groupe')).toBeNull();
  });

  // ----- Les élèves du groupe (ELV-03) -----

  /** L'appel de la séance choisie (s1), avec ces élèves attendus : il fait aussi la liste des élèves du groupe. */
  async function repondreALAppel(eleves: { inscriptionId: string; eleveId: string; prenom: string; nom: string }[]): Promise<void> {
    (await vi.waitFor(() => http.expectOne('/api/seances/s1/appel'))).flush({
      seanceId: 's1', date: '2027-09-18', debut: '10:00', fin: '12:00',
      groupe: { id: 'g1', nom: 'Groupe Samedi 10h → 12h', type: 'Etude', matiere: 'Mathématiques' },
      professeur: 'Karim Ben Salah', ouvert: true, appelFait: false,
      eleves: eleves.map((e) => ({ ...e, absent: false, prevenuLe: null, messages: [] })),
    });
    await vi.waitFor(() => expect(page.querySelector('.appel-integre')).not.toBeNull());
  }
  const ADEM = { inscriptionId: 'i1', eleveId: 'e1', prenom: 'Adem', nom: 'Miled' };
  const appel = () => page.querySelector('.appel-integre') as HTMLElement;

  it('montre ses élèves dans l’appel, et propose d’en ajouter, de les imprimer et de les exporter', async () => {
    await ouvrir(FICHE, [{ ...INSCRIPTION, couleur: 'Rouge' }]);
    await repondreALAppel([ADEM]);

    // Plus de bloc « Les élèves » à droite : l'appel le remplace.
    expect(page.querySelector('.les-eleves')).toBeNull();
    const nom = appel().querySelector('a[href="/eleves/e1"]') as HTMLElement;
    expect(nom.textContent).toContain('Adem Miled');
    expect(nom.getAttribute('title')).toBe('En totalité · depuis le 25/09/2027');
    expect(appel().querySelector('a[href="/professeurs/groupes/g1/inscription"]')?.getAttribute('aria-label')).toBe('Ajouter un élève');
    expect(appel().querySelector('[aria-label="Imprimer"]')).not.toBeNull();
    expect(appel().querySelector('[aria-label="Exporter (Excel)"]')).not.toBeNull();
    // SEA-08 : la feuille d'appel est à côté du tableau, plus en haut de la page.
    expect(appel().querySelector('a[href="/professeurs/groupes/g1/feuille-d-appel"]')?.textContent).toContain("Feuille d'appel");
    expect(page.querySelector('.pagehead a[href="/professeurs/groupes/g1/feuille-d-appel"]')).toBeNull();
    // CAI-08 : un élève en rouge a sa cloche de rappel, à côté de son nom.
    expect(appel().querySelector('[aria-label="Envoyer un rappel à Adem Miled"]')).not.toBeNull();
  });

  it('garde en bas, en gris, un élève qui n’est pas à cette séance', async () => {
    await ouvrir(FICHE, [{ ...INSCRIPTION, arreteLe: '2027-09-11' }]);
    await repondreALAppel([]);

    const ligne = appel().querySelector('.pas-a-cette-seance') as HTMLElement;
    expect(ligne.querySelector('a[href="/eleves/e1"]')?.textContent).toContain('Adem Miled');
    expect(ligne.textContent).toContain('Inscription arrêtée le 11/09');
    // Pas de rappel pour un élève sans retard.
    expect(ligne.querySelector('sh-bouton-de-rappel')).toBeNull();
  });

  it("ne propose plus d'ajouter un élève à un groupe fermé", async () => {
    await ouvrir({ ...FICHE, fermeLe: '2027-10-01', motifDeFermeture: "Trop peu d'élèves" }, [INSCRIPTION]);
    await repondreALAppel([ADEM]);

    expect(appel().querySelector('a[href="/professeurs/groupes/g1/inscription"]')).toBeNull();
    expect(appel().querySelector('a[href="/eleves/e1"]')).not.toBeNull();
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
  // Chaque élément d'une ligne est lu à part : l'écran les sépare par la mise en page, pas par des espaces. L'état de la
  // séance (SEA-02) dépend du jour : il a son propre test.
  const lignes = (selecteur: string) =>
    [...page.querySelectorAll(selecteur)].map((ligne) =>
      ligne.children.length > 0
        ? [...ligne.children].filter((morceau) => !morceau.classList.contains('etat')).map((morceau) => morceau.textContent?.trim()).join(' ')
        : ligne.textContent?.replace(/\s+/g, ' ').trim(),
    );

  describe("l'état des séances (SEA-02), le samedi 27/05/2028", () => {
    beforeEach(() => {
      vi.useFakeTimers({ toFake: ['Date'] });
      vi.setSystemTime(new Date(2028, 4, 27, 9, 0));
    });
    afterEach(() => vi.useRealTimers());

    it("montre l'avancement d'une Révision et l'état de chaque séance, avec un lien vers son appel", async () => {
      await ouvrir({ ...REVISION, seances: REVISION.seances.map((s, i) => ({ ...s, appelFait: i === 0 })) });

      expect(page.querySelector('.avancement')?.textContent).toContain('Séance 2 sur 3');
      expect(etats()).toEqual(['appel fait', "aujourd'hui", 'à venir']);
      // La séance du jour est choisie : son appel s'ouvre sur la fiche.
      expect(choisie()).toBe('sam. 27/05');
    });
  });

  it('montre une Révision, ses séances et leur prix', async () => {
    await ouvrir(REVISION);

    expect(pastilles()).toContain('Révision, 50,000 TND les 3 séances, ou 16,667 TND la séance');
    // Toutes les séances d'une Révision, sans puce de Mois.
    expect(page.querySelectorAll('.puce-seance.mois')).toHaveLength(0);
    expect(puces()).toEqual(['sam. 20/05', 'sam. 27/05', 'sam. 03/06']);
  });

  it('montre une Formation déjà en cours et ses tranches', async () => {
    await ouvrir(FORMATION);

    expect(pastilles().slice(0, 3)).toEqual(['Mathématiques', 'Karim Ben Salah', 'Formation, 300,000 TND, en une fois ou en 3 tranches']);
    expect(puces()).toEqual(['sam. 20/05', 'sam. 27/05']);
    expect(lignes('.tranche-du-groupe')).toEqual([
      'Tranche 1 · 100,000 TND, due à la séance 1',
      'Tranche 2 · 100,000 TND, due à la séance 5',
      'Tranche 3 · 100,000 TND, due à la séance 9',
    ]);
  });

  it('dit qu’une Formation sans tranche se paie en une fois', async () => {
    await ouvrir({ ...FORMATION, tranches: [] });

    expect(pastilles()).toContain('Formation, 300,000 TND, en une fois');
    expect(page.querySelectorAll('.tranche-du-groupe')).toHaveLength(0);
  });

  it('montre un groupe terminé', async () => {
    await ouvrir({ ...REVISION, termine: true });

    expect(page.querySelector('h1 .pill')?.textContent?.trim()).toBe('Terminé');
  });

  it('montre une Étude déjà en cours à partir de son Mois en cours', async () => {
    await ouvrir({ ...FICHE, premiereSeance: 17, mois: FICHE.mois.map((m, i) => ({ ...m, numero: 5 + i })) });

    // La bande montre le Mois 5 (le premier), et propose le Mois 6.
    expect(page.querySelector('.puce-seance.mois')?.textContent).toContain('Mois 6');
    expect(page.textContent).not.toContain('Mois 4');
  });

  // ----- Fermer le groupe (GRP-08) -----

  const FERME: FicheDuGroupe = { ...FICHE, fermeLe: '2027-10-02', motifDeFermeture: "Trop peu d'élèves" };

  it('montre un groupe fermé', async () => {
    await ouvrir(FERME);

    expect(page.querySelector('h1 .pill')?.textContent?.trim()).toBe('Fermé');
    expect(page.querySelector('.pagehead .note')?.textContent).toContain("Fermé le 02/10/2027 : Trop peu d'élèves");
  });
});
