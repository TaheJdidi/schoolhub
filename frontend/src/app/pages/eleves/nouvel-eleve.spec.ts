import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { NouvelEleve } from './nouvel-eleve';

/** Les 9 années de base, rangées comme le serveur les envoie, et un niveau ajouté. */
export const NIVEAUX = [
  ...Array.from({ length: 9 }, (_, i) => ({ id: `n${i + 1}`, nom: `${i + 1}ᵉ année de base`, dOffice: true })),
  { id: 'u', nom: 'Université', dOffice: false },
];

/**
 * Karim a deux groupes ouverts, un de 9ᵉ et un d'un autre niveau, et un fermé qui ne se propose pas ; Leila enseigne la
 * même matière à la même heure ; Sami n'a qu'un groupe fermé, et Nadia est partie.
 */
const GROUPES = [
  { id: 'g9', nom: 'Groupe Samedi 10h → 12h', type: 'Etude', professeurId: 'k', matiere: 'Mathématiques', niveau: '9ᵉ année de base', creneaux: [], capacite: 2, eleves: 2, termine: false, ferme: false },
  { id: 'g7', nom: 'Groupe Lundi 8h → 10h', type: 'Etude', professeurId: 'k', matiere: 'Arabe', niveau: '7ᵉ année de base', creneaux: [], capacite: null, eleves: 0, termine: false, ferme: false },
  { id: 'gf', nom: 'Groupe fermé', type: 'Etude', professeurId: 'k', matiere: 'Arabe', niveau: '9ᵉ année de base', creneaux: [], capacite: null, eleves: 0, termine: false, ferme: true },
  { id: 'gl', nom: 'Groupe Samedi 10h → 12h', type: 'Etude', professeurId: 'l', matiere: 'Mathématiques', niveau: '9ᵉ année de base', creneaux: [], capacite: null, eleves: 0, termine: false, ferme: false },
  { id: 'gs', nom: 'Groupe Mardi', type: 'Etude', professeurId: 's', matiere: 'Physique', niveau: '9ᵉ année de base', creneaux: [], capacite: null, eleves: 0, termine: false, ferme: true },
  { id: 'gn', nom: 'Groupe Jeudi', type: 'Etude', professeurId: 'n', matiere: 'Physique', niveau: '9ᵉ année de base', creneaux: [], capacite: null, eleves: 0, termine: false, ferme: false },
];
const professeur = (id: string, prenom: string, nom: string, parti = false) => ({ id, prenom, nom, telephone: '98 000 000', email: null, parti, groupesOuverts: 0 });
/** La fiche de la Révision de Karim, « g7 » : deux séances à venir, un mode à choisir. */
const seance = (date: string, numero: number) => ({ id: `s${numero}`, numero, date, debut: '08:00:00', fin: '10:00:00', prix: null });
const REVISION = {
  id: 'g7',
  nom: 'Groupe Lundi 8h → 10h',
  type: 'Revision',
  professeur: { id: 'k', prenom: 'Karim', nom: 'Ben Salah' },
  matiere: 'Arabe',
  niveau: { id: 'n7', nom: '7ᵉ année de base' },
  creneaux: [],
  capacite: null,
  eleves: 0,
  dateDeDebut: null,
  prixDuMois: null,
  mois: [],
  termine: false,
  premiereSeance: 1,
  prixTotal: 100,
  nombreDeSeances: 2,
  seances: [seance('2099-01-05', 1), seance('2099-01-12', 2)],
  tranches: [],
  fermeLe: null,
  motifDeFermeture: null,
};
const PROFESSEURS = [professeur('l', 'Leila', 'Trabelsi'), professeur('k', 'Karim', 'Ben Salah'), professeur('s', 'Sami', 'Gharbi'), professeur('n', 'Nadia', 'Ayari', true)];

describe('la création d’un élève', () => {
  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  async function ouvrir(niveaux: typeof NIVEAUX | 'erreur' = NIVEAUX): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'eleves/nouveau', component: NouvelEleve },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/eleves/nouveau');
    page = harness.routeNativeElement as HTMLElement;
    const requete = await vi.waitFor(() => http.expectOne('/api/niveaux'));
    if (niveaux === 'erreur') requete.flush({ message: 'Erreur du serveur' }, { status: 500, statusText: 'Erreur' });
    else requete.flush(niveaux);
    http.expectOne('/api/groupes').flush(GROUPES);
    http.expectOne('/api/professeurs').flush(PROFESSEURS);
    await vi.waitFor(() => expect(page.querySelector('option[value="n9"], .note.bad')).not.toBeNull());
  }

  afterEach(() => http.verify());

  const attendre = (texte: string) => vi.waitFor(() => expect(page.textContent).toContain(texte));
  const libelle = (nom: string) => page.querySelector(`[formcontrolname="${nom}"]`)?.closest('label')?.textContent?.replace(/\s+/g, ' ').trim();
  const bouton = (texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte) as HTMLButtonElement;

  async function choisirLeNiveau(id: string): Promise<void> {
    const liste = page.querySelector('[formcontrolname="niveauId"]') as HTMLSelectElement;
    liste.value = id;
    liste.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();
  }

  async function remplir(): Promise<void> {
    saisir(page, '[formcontrolname="prenom"]', ' Adem ');
    saisir(page, '[formcontrolname="nom"]', 'Miled');
    await choisirLeNiveau('n9');
    saisir(page, '[formcontrolname="telephoneEleve"]', '22 345 678');
    saisir(page, '[formcontrolname="emailEleve"]', 'adem@exemple.tn');
    saisir(page, '[formcontrolname="telephoneParent"]', '98 401 220');
    await harness.fixture.whenStable();
  }

  it('marque les champs facultatifs selon le niveau et « majeur »', async () => {
    await ouvrir();

    await choisirLeNiveau('n6');
    expect(libelle('telephoneEleve')).toContain('(facultatif avant la 7ᵉ année)');
    expect(libelle('emailEleve')).toContain('(facultatif avant la 7ᵉ année)');
    await choisirLeNiveau('n7');
    expect(libelle('telephoneEleve')).not.toContain('facultatif');
    await choisirLeNiveau('u');
    expect(libelle('telephoneEleve')).not.toContain('facultatif');

    expect(libelle('telephoneParent')).not.toContain('facultatif');
    (page.querySelector('[formcontrolname="majeur"]') as HTMLInputElement).click();
    await harness.fixture.whenStable();
    expect(libelle('telephoneParent')).toContain('(facultatif pour un élève majeur)');
  });

  it('crée un élève et ouvre sa fiche', async () => {
    await ouvrir();
    await remplir();

    envoyer(page);

    const requete = http.expectOne({ method: 'POST', url: '/api/eleves' });
    expect(requete.request.body).toEqual({
      prenom: 'Adem',
      nom: 'Miled',
      niveauId: 'n9',
      majeur: false,
      telephoneEleve: '22 345 678',
      emailEleve: 'adem@exemple.tn',
      telephoneParent: '98 401 220',
      emailParent: '',
      etablissement: '',
    });
    requete.flush({ eleve: { id: 'e1' }, avertissement: null }, { status: 201, statusText: 'Created' });
    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/eleves/e1'));
  });

  async function choisir(nom: string, valeur: string): Promise<void> {
    const liste = page.querySelector(`[formcontrolname="${nom}"]`) as HTMLSelectElement;
    liste.value = valeur;
    liste.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();
  }

  /** Karim, puis sa Révision : sa fiche arrive, avec ses modes et ses séances. */
  async function choisirLaRevision(): Promise<void> {
    await choisir('professeurId', 'k');
    await choisir('groupeId', 'g7');
    http.expectOne('/api/groupes/g7').flush(REVISION);
    await vi.waitFor(() => expect(page.querySelector('[formcontrolname="debutLe"]')).not.toBeNull());
  }

  it("fait choisir le professeur, puis l'un de ses groupes ouverts, et inscrit l'élève avec sa fiche", async () => {
    await ouvrir();
    await remplir();

    const professeurs = page.querySelector('[formcontrolname="professeurId"]') as HTMLSelectElement;
    expect([...professeurs.options].map((o) => o.textContent?.trim())).toEqual(['Pas encore de groupe', 'Karim Ben Salah', 'Leila Trabelsi']);
    expect(page.querySelector('[formcontrolname="groupeId"]')).toBeNull();

    professeurs.value = 'k';
    professeurs.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();
    const liste = page.querySelector('[formcontrolname="groupeId"]') as HTMLSelectElement;
    const sections = [...liste.querySelectorAll('optgroup')].map((s) => [s.label, ...[...s.querySelectorAll('option')].map((o) => o.textContent?.trim())]);
    expect(sections).toEqual([
      ['Groupes de son niveau', 'Groupe Samedi 10h → 12h · Mathématiques · 9ᵉ année de base (complet)'],
      ['Autres groupes', 'Groupe Lundi 8h → 10h · Arabe · 7ᵉ année de base'],
    ]);

    await choisir('groupeId', 'g7');
    http.expectOne('/api/groupes/g7').flush(REVISION);
    await vi.waitFor(() => expect(page.querySelector('[formcontrolname="mode"]')).not.toBeNull());
    const options = (nom: string) => [...(page.querySelector(`[formcontrolname="${nom}"]`) as HTMLSelectElement).options].map((o) => o.textContent?.trim());
    expect(options('mode')).toEqual(['Choisissez le mode', 'À la séance', 'En totalité']);
    expect((page.querySelector('[formcontrolname="debutLe"]') as HTMLSelectElement).value).toBe('2099-01-05');

    // Sans mode, rien ne part.
    envoyer(page);
    await attendre('Remplissez les champs obligatoires');

    await choisir('mode', 'ALaSeance');
    envoyer(page);
    const requete = http.expectOne({ method: 'POST', url: '/api/eleves' });
    expect(requete.request.body.groupeId).toBeUndefined();
    requete.flush({ eleve: { id: 'e1' }, avertissement: null }, { status: 201, statusText: 'Created' });
    const inscription = await vi.waitFor(() => http.expectOne({ method: 'POST', url: '/api/inscriptions' }));
    expect(inscription.request.body).toEqual({ eleveId: 'e1', groupeId: 'g7', mode: 'ALaSeance', debutLe: '2099-01-05' });
    inscription.flush({ inscription: { id: 'i1' }, aPayerALArrivee: [], avertissement: null, texte: '', messages: [] });
    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/eleves/e1/inscription'));
    expect((TestBed.inject(Location).getState() as { confirmation?: { inscription: { id: string } } }).confirmation?.inscription.id).toBe('i1');
  });

  it("rouvre l'inscription dans le groupe, avec le refus, si le serveur refuse l'inscription", async () => {
    await ouvrir();
    await remplir();
    await choisirLaRevision();
    await choisir('mode', 'EnTotalite');

    envoyer(page);
    http.expectOne('/api/eleves').flush({ eleve: { id: 'e1' }, avertissement: null }, { status: 201, statusText: 'Created' });
    (await vi.waitFor(() => http.expectOne('/api/inscriptions'))).flush(
      { message: "Ce groupe est fermé : on n'y inscrit plus" },
      { status: 409, statusText: 'Conflict' },
    );
    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/eleves/e1/inscription?groupe=g7'));
    expect((TestBed.inject(Location).getState() as { erreur?: string }).erreur).toBe("Ce groupe est fermé : on n'y inscrit plus");
  });

  it('refuse un formulaire incomplet', async () => {
    await ouvrir();
    await remplir();

    saisir(page, '[formcontrolname="emailEleve"]', '');
    envoyer(page);
    await attendre('Remplissez les champs obligatoires');

    saisir(page, '[formcontrolname="emailEleve"]', 'adem@exemple.tn');
    saisir(page, '[formcontrolname="telephoneParent"]', '');
    envoyer(page);
    await attendre('Remplissez les champs obligatoires');
  });

  it('accepte un élève de 6ᵉ sans ses contacts, et un majeur sans parent', async () => {
    await ouvrir();
    await remplir();
    await choisirLeNiveau('n6');
    saisir(page, '[formcontrolname="telephoneEleve"]', '');
    saisir(page, '[formcontrolname="emailEleve"]', '');
    (page.querySelector('[formcontrolname="majeur"]') as HTMLInputElement).click();
    saisir(page, '[formcontrolname="telephoneParent"]', '');

    envoyer(page);

    const requete = http.expectOne('/api/eleves');
    expect(requete.request.body.majeur).toBe(true);
    requete.flush({ eleve: { id: 'e1' }, avertissement: null }, { status: 201, statusText: 'Created' });
  });

  it("montre le refus du serveur, et bloque le bouton pendant l'envoi", async () => {
    await ouvrir();
    await remplir();

    envoyer(page);
    await vi.waitFor(() => expect(bouton('Enregistrer').disabled).toBe(true));
    http.expectOne('/api/eleves').flush({ message: "Le téléphone de l'élève doit avoir 8 chiffres" }, { status: 400, statusText: 'Bad Request' });

    await attendre("Le téléphone de l'élève doit avoir 8 chiffres");
    expect(page.querySelector('form .note.bad')).not.toBeNull();
    expect(bouton('Enregistrer').disabled).toBe(false);
  });

  it("bloque l'envoi si les niveaux n'ont pas pu se charger", async () => {
    await ouvrir('erreur');

    await attendre('Erreur du serveur');
    expect(bouton('Enregistrer').disabled).toBe(true);
  });
});
