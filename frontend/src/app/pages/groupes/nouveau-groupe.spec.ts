import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { Professeur } from '../professeurs/professeur';
import { NouveauGroupe } from './nouveau-groupe';

describe('la page « Nouveau groupe »', () => {
  const KARIM: Professeur = { id: 'k', prenom: 'Karim', nom: 'Ben Salah', telephone: '98 401 220', email: null, parti: false, groupesOuverts: 0 };
  const LEILA: Professeur = { ...KARIM, id: 'l', prenom: 'Leila', nom: 'Khelifi' };
  const SAMI: Professeur = { ...KARIM, id: 's', prenom: 'Sami', nom: 'Chaabane', parti: true };
  const NIVEAUX = [
    { id: 'n9', nom: '9ᵉ année de base', dOffice: true },
    { id: 'u1', nom: 'Université, 1ʳᵉ année', dOffice: false },
  ];
  const MATIERES = [{ id: 'm1', nom: 'Mathématiques' }];

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  async function ouvrir(niveaux: typeof NIVEAUX | 'erreur' = NIVEAUX, professeurs: Professeur[] = [KARIM, SAMI, LEILA]): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'professeurs/groupes/nouveau', component: NouveauGroupe },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/professeurs/groupes/nouveau?professeur=k');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/professeurs'))).flush(professeurs);
    const requeteDesNiveaux = http.expectOne('/api/niveaux');
    if (niveaux === 'erreur') requeteDesNiveaux.flush({ message: 'Erreur du serveur' }, { status: 500, statusText: 'Erreur' });
    else requeteDesNiveaux.flush(niveaux);
    http.expectOne('/api/matieres').flush(MATIERES);
    // Les listes sont arrivées (le professeur de l'adresse est choisi), ou leur refus s'affiche.
    await vi.waitFor(() => expect(page.querySelector('option[value="k"], .note.bad')).not.toBeNull());
  }

  afterEach(() => http.verify());

  const attendre = (texte: string) => vi.waitFor(() => expect(page.textContent).toContain(texte));
  const champ = (nom: string, rang = 0) => page.querySelectorAll(`[formcontrolname="${nom}"]`)[rang] as HTMLInputElement;
  const options = (nom: string) => [...champ(nom).querySelectorAll('option')].map((o) => o.textContent?.trim());
  const bouton = (libelle: string) => {
    const trouve = [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === libelle);
    if (!trouve) throw new Error(`Aucun bouton « ${libelle} »`);
    return trouve as HTMLButtonElement;
  };
  const boutons = () => [...page.querySelectorAll('button')].map((b) => b.textContent?.trim());

  async function cliquer(libelle: string): Promise<void> {
    bouton(libelle).click();
    await harness.fixture.whenStable();
  }

  async function choisirOption(nom: string, valeur: string, rang = 0): Promise<void> {
    const liste = champ(nom, rang) as unknown as HTMLSelectElement;
    liste.value = valeur;
    liste.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();
  }

  async function taper(nom: string, valeur: string, rang = 0): Promise<void> {
    const element = champ(nom, rang);
    element.value = valeur;
    element.dispatchEvent(new Event(element.tagName === 'SELECT' ? 'change' : 'input'));
    await harness.fixture.whenStable();
  }

  const listeDesMatieres = () => page.querySelector('select[name="choix-de-la-matiere"]') as HTMLSelectElement;

  async function choisirLaMatiere(valeur: string): Promise<void> {
    const liste = listeDesMatieres();
    liste.value = valeur;
    liste.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();
  }

  async function remplir(): Promise<void> {
    await choisirLaMatiere('Mathématiques');
    await choisirOption('niveauId', 'n9');
    await taper('debut', '10:00');
    await taper('fin', '12:00');
    saisir(page, '[formcontrolname="prix"]', '60,5');
    saisir(page, '[formcontrolname="dateDeDebut"]', '2027-09-18');
    await harness.fixture.whenStable();
  }

  it("charge les listes, et choisit le professeur de l'adresse", async () => {
    await ouvrir();

    expect(options('professeurId')).toEqual(['Choisissez le professeur', 'Karim Ben Salah', 'Leila Khelifi']);
    expect(champ('professeurId').value).toBe('k');
    expect(options('niveauId')).toEqual(['Choisissez le niveau', '9ᵉ année de base', 'Université, 1ʳᵉ année']);
    expect([...listeDesMatieres().options].map((o) => o.value)).toContain('Mathématiques');
    expect(page.querySelectorAll('.creneau')).toHaveLength(1);
    expect(boutons()).not.toContain('Retirer');
  });

  it("propose le nom depuis le premier créneau, jusqu'à ce que la secrétaire le change", async () => {
    await ouvrir();

    await taper('debut', '10:00');
    await taper('fin', '12:00');
    expect(champ('nom').value).toBe('Groupe Samedi 10h → 12h');

    await choisirOption('jour', 'Dimanche');
    expect(champ('nom').value).toBe('Groupe Dimanche 10h → 12h');

    await taper('nom', 'Maths du dimanche');
    await taper('debut', '10:30');
    expect(champ('nom').value).toBe('Maths du dimanche');

    // Vidé, le nom suit de nouveau le jour et les horaires.
    await taper('nom', '');
    await taper('debut', '08:00');
    await taper('fin', '10:00');
    expect(champ('nom').value).toBe('Groupe Dimanche 8h → 10h');
    // Effleuré sans être changé, il les suit encore.
    await taper('nom', 'Groupe Dimanche 8h → 10h');
    await choisirOption('jour', 'Samedi');
    expect(champ('nom').value).toBe('Groupe Samedi 8h → 10h');
  });

  it('ajoute et retire un créneau', async () => {
    await ouvrir();

    await cliquer('Ajouter un créneau');
    expect(page.querySelectorAll('.creneau')).toHaveLength(2);
    expect(boutons().filter((b) => b === 'Retirer')).toHaveLength(2);

    await cliquer('Retirer');
    expect(page.querySelectorAll('.creneau')).toHaveLength(1);
    expect(boutons()).not.toContain('Retirer');
  });

  it('envoie le groupe et ouvre sa fiche', async () => {
    await ouvrir();
    await remplir();

    envoyer(page);
    const requete = http.expectOne({ method: 'POST', url: '/api/groupes' });
    expect(requete.request.body).toEqual({
      type: 'Etude',
      professeurId: 'k',
      matiere: 'Mathématiques',
      niveauId: 'n9',
      creneaux: [{ jour: 'Samedi', debut: '10:00', fin: '12:00' }],
      prixDuMois: 60.5,
      dateDeDebut: '2027-09-18',
      capacite: null,
      nom: 'Groupe Samedi 10h → 12h',
    });
    requete.flush({ id: 'g1' }, { status: 201, statusText: 'Created' });

    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/professeurs/groupes/g1'));
  });

  it('envoie la capacité en nombre', async () => {
    await ouvrir();
    await remplir();
    saisir(page, '[formcontrolname="capacite"]', ' 16 ');

    envoyer(page);

    const requete = http.expectOne('/api/groupes');
    expect(requete.request.body.capacite).toBe(16);
    requete.flush({ id: 'g1' }, { status: 201, statusText: 'Created' });
  });

  it('refuse un formulaire incomplet, un prix ou une capacité illisibles', async () => {
    await ouvrir();
    await remplir();

    saisir(page, '[formcontrolname="dateDeDebut"]', '');
    envoyer(page);
    await attendre('Remplissez le professeur, la matière, le niveau, les créneaux, le prix et la date de début');

    saisir(page, '[formcontrolname="dateDeDebut"]', '2027-09-18');
    saisir(page, '[formcontrolname="prix"]', '60 TND');
    envoyer(page);
    await attendre('Écrivez le prix du Mois, par exemple 60,000');

    saisir(page, '[formcontrolname="prix"]', '60');
    saisir(page, '[formcontrolname="capacite"]', '16 places');
    envoyer(page);
    await attendre('Écrivez la capacité en nombre de places, par exemple 16');
    expect(page.querySelector('.note.bad')).not.toBeNull();
  });

  it("montre le refus du serveur sous le formulaire, et bloque le bouton pendant l'envoi", async () => {
    await ouvrir();
    await remplir();

    envoyer(page);
    await vi.waitFor(() => expect(bouton('Créer le groupe').disabled).toBe(true));
    http.expectOne('/api/groupes').flush({ message: 'Ce professeur a quitté le centre' }, { status: 409, statusText: 'Conflict' });

    await attendre('Ce professeur a quitté le centre');
    expect(page.querySelector('form + .note.bad, form .note.bad')?.textContent).toContain('Ce professeur a quitté le centre');
    expect(bouton('Créer le groupe').disabled).toBe(false);
  });

  it("bloque la création si une liste n'a pas pu se charger", async () => {
    await ouvrir('erreur');

    await attendre('Erreur du serveur');
    expect(bouton('Créer le groupe').disabled).toBe(true);
  });

  // ----- Révision, Formation, groupe déjà en cours (GRP-05 à GRP-07) -----

  const champs = (nom: string) => [...page.querySelectorAll(`[formcontrolname="${nom}"]`)] as HTMLInputElement[];
  const libelles = () => [...page.querySelectorAll('label')].map((l) => l.textContent?.replace(/\s+/g, ' ').trim());

  async function choisirLeType(libelle: string): Promise<void> {
    await cliquer(libelle);
    expect(bouton(libelle).getAttribute('aria-pressed')).toBe('true');
  }

  async function remplirUneRevision(): Promise<void> {
    await choisirLaMatiere('Mathématiques');
    await choisirOption('niveauId', 'n9');
    saisir(page, '[formcontrolname="prixTotal"]', '50');
    saisir(page, '[formcontrolname="nombreDeSeances"]', '3');
    await harness.fixture.whenStable();
  }

  async function cocherDejaCommence(): Promise<void> {
    (page.querySelector('input[type="checkbox"]') as HTMLInputElement).click();
    await harness.fixture.whenStable();
  }

  it("montre les champs d'une Révision", async () => {
    await ouvrir();

    await choisirLeType('Révision');

    expect(champ('prixTotal')).not.toBeUndefined();
    expect(champ('nombreDeSeances')).not.toBeUndefined();
    expect(champ('prix')).toBeUndefined();
    expect(boutons()).toContain('Créneaux par semaine');
    expect(boutons()).toContain('Dates une à une');
  });

  it('envoie une Révision par créneaux', async () => {
    await ouvrir();
    await choisirLeType('Révision');
    await remplirUneRevision();
    await taper('debut', '10:00');
    await taper('fin', '12:00');
    saisir(page, '[formcontrolname="dateDeDebut"]', '2028-05-20');

    envoyer(page);

    const requete = http.expectOne({ method: 'POST', url: '/api/groupes' });
    expect(requete.request.body).toEqual({
      type: 'Revision',
      professeurId: 'k',
      matiere: 'Mathématiques',
      niveauId: 'n9',
      prixTotal: 50,
      nombreDeSeances: 3,
      creneaux: [{ jour: 'Samedi', debut: '10:00', fin: '12:00' }],
      dateDeDebut: '2028-05-20',
      capacite: null,
      nom: 'Groupe Samedi 10h → 12h',
    });
    requete.flush({ id: 'g1' }, { status: 201, statusText: 'Created' });
  });

  it('envoie une Révision avec des dates une à une', async () => {
    await ouvrir();
    await choisirLeType('Révision');
    await remplirUneRevision();
    await cliquer('Dates une à une');

    expect(page.querySelectorAll('.date-saisie')).toHaveLength(1);
    await taper('date', '2028-05-24');
    await taper('debutDeLaDate', '17:00');
    await taper('finDeLaDate', '19:00');
    expect(champ('nom').value).toBe('Groupe Mercredi 17h → 19h');
    await cliquer('Ajouter une date');
    await cliquer('Ajouter une date');
    expect(page.querySelectorAll('.date-saisie')).toHaveLength(3);
    await taper('date', '2028-05-20', 1);
    await taper('debutDeLaDate', '10:00', 1);
    await taper('finDeLaDate', '12:00', 1);
    [...page.querySelectorAll('.date-saisie')][2].querySelector('button')?.click();
    await harness.fixture.whenStable();
    expect(page.querySelectorAll('.date-saisie')).toHaveLength(2);

    envoyer(page);

    const requete = http.expectOne('/api/groupes');
    expect(requete.request.body.dates).toEqual([
      { date: '2028-05-24', debut: '17:00', fin: '19:00' },
      { date: '2028-05-20', debut: '10:00', fin: '12:00' },
    ]);
    expect(requete.request.body.creneaux).toBeUndefined();
    expect(requete.request.body.dateDeDebut).toBeUndefined();
    requete.flush({ id: 'g1' }, { status: 201, statusText: 'Created' });
  });

  it('envoie une Formation avec ses tranches, sans niveau', async () => {
    await ouvrir();
    await choisirLeType('Formation');
    expect(libelles().some((l) => l?.startsWith('Niveau (facultatif)'))).toBe(true);
    await choisirLaMatiere('Informatique');
    saisir(page, '[formcontrolname="prixTotal"]', '300');
    saisir(page, '[formcontrolname="nombreDeSeances"]', '10');
    await taper('debut', '10:00');
    await taper('fin', '12:00');
    saisir(page, '[formcontrolname="dateDeDebut"]', '2027-10-02');
    await cliquer('Ajouter une tranche');
    await cliquer('Ajouter une tranche');
    await taper('montant', '100');
    await taper('seance', '1');
    await taper('montant', '200,000', 1);
    await taper('seance', '5', 1);

    envoyer(page);

    const requete = http.expectOne('/api/groupes');
    expect(requete.request.body.type).toBe('Formation');
    expect(requete.request.body.niveauId).toBeNull();
    expect(requete.request.body.tranches).toEqual([
      { montant: 100, seance: 1 },
      { montant: 200, seance: 5 },
    ]);
    requete.flush({ id: 'g1' }, { status: 201, statusText: 'Created' });
  });

  it('crée une Étude déjà commencée, à partir de son Mois en cours', async () => {
    await ouvrir();
    await remplir();

    await cocherDejaCommence();
    expect(libelles().some((l) => l?.startsWith('Première séance dans SchoolHub'))).toBe(true);
    envoyer(page);
    await attendre('Indiquez où en est le groupe');

    saisir(page, '[formcontrolname="moisEnCours"]', '5');
    envoyer(page);

    const requete = http.expectOne('/api/groupes');
    expect(requete.request.body.moisEnCours).toBe(5);
    requete.flush({ id: 'g1' }, { status: 201, statusText: 'Created' });
  });

  it('crée une Révision déjà commencée, à partir de sa séance en cours', async () => {
    await ouvrir();
    await choisirLeType('Révision');
    await remplirUneRevision();
    await taper('debut', '10:00');
    await taper('fin', '12:00');
    saisir(page, '[formcontrolname="dateDeDebut"]', '2028-01-15');
    saisir(page, '[formcontrolname="nombreDeSeances"]', '10');

    await cocherDejaCommence();
    expect(libelles().some((l) => l?.startsWith('Date de cette séance'))).toBe(true);
    saisir(page, '[formcontrolname="seanceEnCours"]', '4');
    envoyer(page);

    const requete = http.expectOne('/api/groupes');
    expect(requete.request.body.seanceEnCours).toBe(4);
    expect(requete.request.body.nombreDeSeances).toBe(10);
    requete.flush({ id: 'g1' }, { status: 201, statusText: 'Created' });
  });

  it("refuse une Révision incomplète ou des montants illisibles", async () => {
    await ouvrir();
    await choisirLeType('Révision');
    await remplirUneRevision();
    await taper('debut', '10:00');
    await taper('fin', '12:00');
    saisir(page, '[formcontrolname="dateDeDebut"]', '2028-05-20');

    saisir(page, '[formcontrolname="nombreDeSeances"]', '');
    envoyer(page);
    await attendre('Remplissez le professeur, la matière, le niveau, le prix total, le nombre de séances et les dates');

    saisir(page, '[formcontrolname="nombreDeSeances"]', 'dix');
    envoyer(page);
    await attendre('Écrivez le nombre de séances, par exemple 10');

    saisir(page, '[formcontrolname="nombreDeSeances"]', '3');
    saisir(page, '[formcontrolname="prixTotal"]', '50 TND');
    envoyer(page);
    await attendre('Écrivez le prix total, par exemple 50,000');
  });

  it('refuse une tranche illisible', async () => {
    await ouvrir();
    await choisirLeType('Formation');
    await choisirLaMatiere('Informatique');
    saisir(page, '[formcontrolname="prixTotal"]', '300');
    saisir(page, '[formcontrolname="nombreDeSeances"]', '10');
    await taper('debut', '10:00');
    await taper('fin', '12:00');
    saisir(page, '[formcontrolname="dateDeDebut"]', '2027-10-02');
    await cliquer('Ajouter une tranche');
    await taper('montant', 'cent');
    await taper('seance', '1');

    envoyer(page);

    await attendre('Écrivez le montant et la séance de chaque tranche, par exemple 100,000 à la séance 1');
    expect(champs('montant')).toHaveLength(1);
  });

  it("reprend le groupe commencé après une visite d'une autre page, et « Recommencer » le vide", async () => {
    await ouvrir();
    await choisirLeType('Formation');
    await choisirLaMatiere('Informatique');
    await cliquer('Ajouter une tranche');
    await taper('montant', '100');

    await harness.navigateByUrl('/eleves');
    harness.fixture.autoDetectChanges();
    await harness.navigateByUrl('/professeurs/groupes/nouveau');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/professeurs'))).flush([KARIM, LEILA]);
    http.expectOne('/api/niveaux').flush(NIVEAUX);
    http.expectOne('/api/matieres').flush(MATIERES);

    await attendre('Vous reprenez le groupe que vous aviez commencé.');
    expect(page.querySelector('[aria-pressed="true"]')?.textContent?.trim()).toBe('Formation');
    expect(listeDesMatieres().value).toBe('Informatique');
    expect(champ('professeurId').value).toBe('k');
    expect(champs('montant').map((m) => m.value)).toEqual(['100']);

    await cliquer('Recommencer');
    expect(page.textContent).not.toContain('Vous reprenez');
    expect(page.querySelector('[aria-pressed="true"]')?.textContent?.trim()).toBe('Étude');
    expect(listeDesMatieres().value).toBe('');
  });

  it('propose les matières courantes et celles du centre, et « Autre matière… » pour en écrire une', async () => {
    await ouvrir();
    const matieres = [...listeDesMatieres().options].map((o) => o.textContent?.trim());
    expect(matieres[0]).toBe('Choisissez la matière');
    expect(matieres).toEqual(expect.arrayContaining(['Arabe', 'Français', 'Mathématiques', 'Philosophie', 'Physique', 'Sciences expérimentales']));
    expect(matieres.filter((m) => m === 'Mathématiques')).toHaveLength(1);
    expect(matieres.at(-1)).toBe('Autre matière…');
    expect(champ('matiere')).toBeUndefined();

    await choisirLaMatiere('autre');
    saisir(page, '[formcontrolname="matiere"]', 'Robotique');
    await harness.fixture.whenStable();
    expect(listeDesMatieres().value).toBe('autre');
    expect(champ('matiere').value).toBe('Robotique');

    await choisirLaMatiere('Arabe');
    expect(champ('matiere')).toBeUndefined();
  });
  it('entoure de rouge les champs obligatoires vides après « Créer le groupe » ; les heures se choisissent dans une liste', async () => {
    await ouvrir();
    envoyer(page);
    await attendre('Remplissez le professeur, la matière');
    expect(page.querySelector('form')!.classList).toContain('ng-submitted');
    expect(champ('prix').classList).toContain('ng-invalid');
    expect(champ('debut').classList).toContain('ng-invalid');
    expect(listeDesMatieres().classList).toContain('manquant');
    // Le professeur vient de l'adresse : il est rempli.
    expect(champ('professeurId').classList).not.toContain('ng-invalid');

    saisir(page, '[formcontrolname="prix"]', '60');
    await choisirLaMatiere('Arabe');
    expect(champ('prix').classList).not.toContain('ng-invalid');
    expect(listeDesMatieres().classList).not.toContain('manquant');

    expect(options('debut')).toContain('8h30');
    await taper('debut', '10:00');
    expect(champ('debut').value).toBe('10:00');
    // La fin se met 2 heures après le début, et suit le début tant qu'on ne l'a pas choisie.
    expect(champ('fin').value).toBe('12:00');
    await taper('debut', '08:30');
    expect(champ('fin').value).toBe('10:30');
    await taper('fin', '11:00');
    await taper('debut', '09:00');
    expect(champ('fin').value).toBe('11:00');
    expect(champ('debut').classList).not.toContain('ng-invalid');
  });
  it('prend la matière du professeur, sans la faire choisir ; un professeur sans matière la fait choisir', async () => {
    await ouvrir(NIVEAUX, [{ ...KARIM, matiere: 'Physique' }, SAMI, LEILA]);

    // Karim vient de l'adresse : sa matière ne s'affiche pas, elle se prend toute seule.
    expect([...page.querySelectorAll('.f')].map((f) => f.textContent?.trim())).not.toContain('Matière');
    expect(listeDesMatieres()).toBeNull();

    // Leila n'a pas de matière : la liste revient, vide.
    await choisirOption('professeurId', LEILA.id);
    expect(listeDesMatieres().value).toBe('');
  });
});
