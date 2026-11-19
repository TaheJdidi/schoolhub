import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { FormulaireDuCentre } from './formulaire-du-centre';
import { FormulairePublic } from './formulaire-public';

describe("le formulaire public d'inscription", () => {
  const ADRESSE = '/api/public/inscription/les-jasmins';
  const SAMEDI = [{ jour: 'Samedi' as const, debut: '10:00', fin: '12:00' }];

  /** Les niveaux d'office jusqu'à la 9ᵉ, dans l'ordre : la 7ᵉ année est le 7ᵉ. */
  const NIVEAUX = Array.from({ length: 9 }, (_, i) => ({ id: `n${i + 1}`, nom: `${i + 1}ᵉ année de base`, dOffice: true }));

  const FORMULAIRE: FormulaireDuCentre = {
    centre: 'Centre Les Jasmins',
    annee: '2027-2028',
    niveaux: NIVEAUX,
    groupes: [
      { id: 'g2', matiere: 'Arabe', niveauId: 'n6', type: 'Etude', professeur: 'Lina Gharbi', creneaux: SAMEDI },
      { id: 'g3', matiere: 'Informatique', niveauId: null, type: 'Formation', professeur: 'Karim Ben Salah', creneaux: SAMEDI },
      { id: 'g1', matiere: 'Mathématiques', niveauId: 'n9', type: 'Etude', professeur: 'Karim Ben Salah', creneaux: SAMEDI },
      { id: 'g4', matiere: 'Mathématiques', niveauId: 'n9', type: 'Revision', professeur: 'Sami Trabelsi', creneaux: [{ jour: 'Mardi', debut: '17:00', fin: '19:00' }] },
      { id: 'g5', matiere: 'Mathématiques', niveauId: 'n8', type: 'Etude', professeur: 'Sami Trabelsi', creneaux: SAMEDI },
    ],
    etablissements: [],
  };

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'inscription/:code', component: FormulairePublic },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function ouvrir(formulaire: FormulaireDuCentre | 404 = FORMULAIRE): Promise<void> {
    harness = await RouterTestingHarness.create('/inscription/les-jasmins');
    page = harness.routeNativeElement as HTMLElement;
    const requete = await vi.waitFor(() => http.expectOne(ADRESSE));
    if (formulaire === 404) requete.flush({ message: "Ce lien d'inscription n'existe pas" }, { status: 404, statusText: 'Not Found' });
    else requete.flush(formulaire);
    await vi.waitFor(() => expect(page.querySelector('h1')).not.toBeNull());
  }

  const texte = () => page.textContent ?? '';
  const bouton = () => page.querySelector('button[type="submit"]') as HTMLButtonElement;
  const liste = (nom: string) => page.querySelector(`select[aria-label="${nom}"]`) as HTMLSelectElement;
  const options = (nom: string) => [...liste(nom).options].map((o) => o.textContent?.trim());

  async function choisirLeNiveau(id: string): Promise<void> {
    const niveau = page.querySelector('select[aria-label="Niveau scolaire"]') as HTMLSelectElement;
    niveau.value = id;
    niveau.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();
  }

  async function choisir(nom: string, valeur: string): Promise<void> {
    liste(nom).value = valeur;
    liste(nom).dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();
  }

  async function envoyer(): Promise<TestRequest> {
    bouton().click();
    return vi.waitFor(() => http.expectOne({ method: 'POST', url: ADRESSE }));
  }

  it('montre le formulaire du centre', async () => {
    await ouvrir();

    expect(page.querySelector('h1')?.textContent).toContain("Demande d'inscription 2027-2028");
    expect(texte()).toContain('Centre Les Jasmins');
    expect(texte()).toContain(
      'Remplissez ce formulaire, sans créer de compte. Le centre vous confirmera le groupe et la date de la première séance sur WhatsApp.',
    );
    expect(texte()).toContain("Choisissez d'abord le niveau scolaire.");
  });

  it("n'écrit pas l'année quand le centre n'en a pas", async () => {
    await ouvrir({ ...FORMULAIRE, annee: null });

    expect(page.querySelector('h1')?.textContent?.trim()).toBe("Demande d'inscription");
  });

  it('propose les matières du niveau et les Formations ouvertes à tous, puis leurs professeurs', async () => {
    await ouvrir();

    await choisirLeNiveau('n9');
    expect(options('Matière souhaitée')).toEqual(['Choisir…', 'Informatique', 'Mathématiques']);
    expect(liste('Professeur souhaité').disabled).toBe(true);

    await choisir('Matière souhaitée', 'Mathématiques');
    expect(liste('Professeur souhaité').disabled).toBe(false);
    expect(options('Professeur souhaité')).toEqual(['Choisir…', 'Karim Ben Salah', 'Sami Trabelsi']);
    await choisir('Professeur souhaité', 'Sami Trabelsi');
    expect(texte()).toContain('Horaire : Mardi 17h → 19h · Révision');

    // Un seul professeur dans la matière : il est choisi d'office.
    await choisir('Matière souhaitée', 'Informatique');
    expect(liste('Professeur souhaité').value).toBe('Karim Ben Salah');
    expect(texte()).toContain('Horaire : Samedi 10h → 12h · Formation');
  });

  it("fait choisir l'horaire quand le professeur a plusieurs groupes de la matière", async () => {
    await ouvrir({
      ...FORMULAIRE,
      groupes: [
        ...FORMULAIRE.groupes,
        { id: 'g6', matiere: 'Mathématiques', niveauId: 'n9', type: 'Etude', professeur: 'Karim Ben Salah', creneaux: [{ jour: 'Lundi', debut: '17:00', fin: '19:00' }] },
      ],
    });
    await choisirLeNiveau('n9');
    await choisir('Matière souhaitée', 'Mathématiques');
    await choisir('Professeur souhaité', 'Karim Ben Salah');

    expect(options('Horaire préféré')).toEqual(['Samedi 10h → 12h', 'Lundi 17h → 19h']);
    await choisir('Horaire préféré', 'g6');
    const requete = await envoyer();
    expect(requete.request.body.groupes).toEqual(['g6']);
    requete.flush({ prenom: 'Adem' }, { status: 201, statusText: 'Created' });
  });

  it('garde la matière et le professeur quand le niveau change, s’ils y sont encore proposés', async () => {
    await ouvrir();
    await choisirLeNiveau('n8');
    await choisir('Matière souhaitée', 'Mathématiques');
    expect(liste('Professeur souhaité').value).toBe('Sami Trabelsi');

    // En 9ᵉ, Sami Trabelsi a la Révision de Mathématiques : c'est elle qui part.
    await choisirLeNiveau('n9');
    expect(liste('Matière souhaitée').value).toBe('Mathématiques');
    expect(liste('Professeur souhaité').value).toBe('Sami Trabelsi');
    const requete = await envoyer();
    expect(requete.request.body.groupes).toEqual(['g4']);
    requete.flush({ prenom: 'Adem' }, { status: 201, statusText: 'Created' });
  });

  it('demande la matière puis le professeur avant d’envoyer', async () => {
    await ouvrir();
    await choisirLeNiveau('n9');

    bouton().click();
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('Choisissez la matière souhaitée'));
    await choisir('Matière souhaitée', 'Mathématiques');
    bouton().click();
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('Choisissez le professeur souhaité'));
  });

  it("propose les établissements du centre, et « Autre… » pour écrire un nom absent de la liste", async () => {
    await ouvrir({ ...FORMULAIRE, etablissements: ['Collège Ibn Khaldoun', 'Lycée Pilote Ariana'] });
    await choisirLeNiveau('n9');
    await choisir('Matière souhaitée', 'Mathématiques');
    await choisir('Professeur souhaité', 'Sami Trabelsi');

    expect(options('Établissement scolaire')).toEqual(['Choisir…', 'Collège Ibn Khaldoun', 'Lycée Pilote Ariana', 'Autre…']);
    expect(page.querySelector(`[aria-label="Nom de l'établissement"]`)).toBeNull();
    await choisir('Établissement scolaire', 'Lycée Pilote Ariana');
    let requete = await envoyer();
    expect(requete.request.body.etablissement).toBe('Lycée Pilote Ariana');
    requete.flush({ message: 'Le prénom est obligatoire' }, { status: 400, statusText: 'Bad Request' });

    await choisir('Établissement scolaire', '__autre__');
    saisir(page, `[aria-label="Nom de l'établissement"]`, 'École primaire El Menzah 5');
    requete = await envoyer();
    expect(requete.request.body.etablissement).toBe('École primaire El Menzah 5');
    requete.flush({ prenom: 'Adem' }, { status: 201, statusText: 'Created' });
  });

  it('dit quand les contacts de l’élève sont facultatifs', async () => {
    await ouvrir();

    await choisirLeNiveau('n6');
    expect(texte().match(/\(facultatif avant la 7ᵉ année\)/g)).toHaveLength(2);
    await choisirLeNiveau('n9');
    expect(texte()).not.toContain('(facultatif avant la 7ᵉ année)');

    expect(texte()).not.toContain('(facultatif pour un élève majeur)');
    (page.querySelector('input[type="checkbox"][aria-label="L\'élève est majeur"]') as HTMLInputElement).click();
    await harness.fixture.whenStable();
    expect(texte()).toContain('(facultatif pour un élève majeur)');
  });

  it('envoie la demande, puis remercie', async () => {
    await ouvrir();
    saisir(page, '[aria-label="Prénom"]', 'Adem');
    saisir(page, '[aria-label="Nom"]', 'Miled');
    await choisirLeNiveau('n9');
    saisir(page, '[aria-label="Téléphone de l\'élève"]', '22 345 678');
    saisir(page, '[aria-label="Email de l\'élève"]', 'adem@exemple.tn');
    saisir(page, '[aria-label="Téléphone WhatsApp du parent"]', '98 401 220');
    saisir(page, '[aria-label="Établissement scolaire"]', 'Collège Ibn Khaldoun');
    await choisir('Matière souhaitée', 'Mathématiques');
    await choisir('Professeur souhaité', 'Sami Trabelsi');

    const requete = await envoyer();
    expect(requete.request.body).toEqual({
      prenom: 'Adem',
      nom: 'Miled',
      niveauId: 'n9',
      majeur: false,
      telephoneParent: '98 401 220',
      emailParent: null,
      telephoneEleve: '22 345 678',
      emailEleve: 'adem@exemple.tn',
      etablissement: 'Collège Ibn Khaldoun',
      groupes: ['g4'],
      siteWeb: null,
    });
    await vi.waitFor(() => expect(bouton().disabled).toBe(true));
    requete.flush({ prenom: 'Adem' }, { status: 201, statusText: 'Created' });

    await vi.waitFor(() =>
      expect(texte()).toContain(
        'Demande envoyée. Merci Adem ! Le centre vous confirmera le groupe et la date de la première séance sur WhatsApp.',
      ),
    );
    expect(page.querySelector('form')).toBeNull();
  });

  it('entoure de rouge les champs obligatoires encore vides après « Envoyer ma demande »', async () => {
    await ouvrir();
    const formulaire = page.querySelector('form') as HTMLFormElement;
    const prenom = page.querySelector('[aria-label="Prénom"]') as HTMLInputElement;
    expect(formulaire.classList).not.toContain('champs-verifies');
    expect(prenom.required).toBe(true);
    expect((page.querySelector('[aria-label="Email du parent"]') as HTMLInputElement).required).toBe(false);

    (await envoyer()).flush({ message: 'Le prénom est obligatoire' }, { status: 400, statusText: 'Bad Request' });
    await vi.waitFor(() => expect(formulaire.classList).toContain('champs-verifies'));
    expect(prenom.matches(':invalid')).toBe(true);
    saisir(page, '[aria-label="Prénom"]', 'Adem');
    expect(prenom.matches(':invalid')).toBe(false);
  });

  it('cache le champ piège aux personnes', async () => {
    await ouvrir();

    const piege = page.querySelector('.piege') as HTMLElement;
    expect(piege.getAttribute('aria-hidden')).toBe('true');
    expect((piege.querySelector('input[name="siteWeb"]') as HTMLInputElement).tabIndex).toBe(-1);
  });

  it('montre le refus du serveur sous le formulaire, et la limite des envois', async () => {
    await ouvrir();

    (await envoyer()).flush({ message: 'Le prénom est obligatoire' }, { status: 400, statusText: 'Bad Request' });
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('Le prénom est obligatoire'));
    expect(bouton().disabled).toBe(false);

    (await envoyer()).flush(
      { message: 'Trop de demandes envoyées depuis cette connexion. Réessayez dans une heure.' },
      { status: 429, statusText: 'Too Many Requests' },
    );
    await vi.waitFor(() =>
      expect(page.querySelector('[role="alert"]')?.textContent).toContain('Trop de demandes envoyées depuis cette connexion.'),
    );
  });

  it("dit quand le lien n'existe pas", async () => {
    await ouvrir(404);

    expect(texte()).toContain("Ce lien d'inscription n'existe pas.");
    expect(page.querySelector('form')).toBeNull();
  });
});
