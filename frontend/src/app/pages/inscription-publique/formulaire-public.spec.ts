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
    ],
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
  const matieres = () => [...page.querySelectorAll('.matiere > label')].map((l) => l.textContent?.trim());

  async function choisirLeNiveau(id: string): Promise<void> {
    const niveau = page.querySelector('select[aria-label="Niveau scolaire"]') as HTMLSelectElement;
    niveau.value = id;
    niveau.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();
  }

  async function cocher(matiere: string): Promise<void> {
    (page.querySelector(`input[type="checkbox"][aria-label="${matiere}"]`) as HTMLInputElement).click();
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

  it('propose les matières du niveau, et les Formations ouvertes à tous', async () => {
    await ouvrir();

    await choisirLeNiveau('n9');
    expect(matieres()).toEqual(['Informatique', 'Mathématiques']);

    await cocher('Mathématiques');
    const groupes = page.querySelector('select[aria-label="Groupe préféré en Mathématiques"]') as HTMLSelectElement;
    expect([...groupes.options].map((o) => o.textContent?.trim())).toEqual([
      'Karim Ben Salah · Samedi 10h → 12h',
      'Sami Trabelsi · Mardi 17h → 19h · Révision',
    ]);
    await cocher('Informatique');
    const informatique = page.querySelector('select[aria-label="Groupe préféré en Informatique"]') as HTMLSelectElement;
    expect(informatique.options[0].textContent?.trim()).toBe('Karim Ben Salah · Samedi 10h → 12h · Formation');
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
    await cocher('Mathématiques');
    const groupes = page.querySelector('select[aria-label="Groupe préféré en Mathématiques"]') as HTMLSelectElement;
    groupes.value = 'g4';
    groupes.dispatchEvent(new Event('change'));
    await cocher('Informatique');
    saisir(page, '[aria-label="Amis qui suivront le même groupe"]', 'Sami Chaabane');

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
      etablissement: null,
      groupes: ['g3', 'g4'],
      amis: 'Sami Chaabane',
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
