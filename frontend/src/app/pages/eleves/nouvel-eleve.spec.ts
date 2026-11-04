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
