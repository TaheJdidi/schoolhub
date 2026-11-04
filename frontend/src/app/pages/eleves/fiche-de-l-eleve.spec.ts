import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { AssuranceDeLEleve, InscriptionDeLEleve } from '../inscriptions/inscription';
import { FicheDeLEleve } from './eleve';
import { PageDeLEleve } from './fiche-de-l-eleve';
import { NIVEAUX } from './nouvel-eleve.spec';

describe('la fiche d’un élève', () => {
  const ADEM: FicheDeLEleve = {
    id: 'e1',
    prenom: 'Adem',
    nom: 'Miled',
    niveau: { id: 'n9', nom: '9ᵉ année de base' },
    majeur: false,
    telephoneParent: '98 401 220',
    emailParent: null,
    telephoneEleve: '22 345 678',
    emailEleve: 'adem@exemple.tn',
    etablissement: 'Collège Ibn Khaldoun',
  };

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  async function preparer(): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'eleves/:id', component: PageDeLEleve },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  }

  const INSCRIPTION: InscriptionDeLEleve = {
    id: 'i1',
    eleve: { id: 'e1', prenom: 'Adem', nom: 'Miled' },
    groupe: { id: 'g1', nom: 'Groupe Samedi 10h → 12h', type: 'Etude', matiere: 'Mathématiques' },
    professeur: 'Karim Ben Salah',
    mode: 'ParMois',
    prixNormal: 40,
    nombreDeTranches: 0,
    premiereSeance: { date: '2027-11-20', debut: '10:00:00' },
  };

  async function ouvrir(
    reponse: FicheDeLEleve | 404 = ADEM,
    inscriptions: InscriptionDeLEleve[] = [],
    assurance: AssuranceDeLEleve | null = null,
  ): Promise<void> {
    await preparer();
    await harness.navigateByUrl('/eleves/e1');
    await repondre(reponse, inscriptions, assurance);
  }

  /** La fiche, ses inscriptions et son assurance (204 : sans objet), demandées ensemble. */
  async function repondre(
    reponse: FicheDeLEleve | 404,
    inscriptions: InscriptionDeLEleve[] = [],
    assurance: AssuranceDeLEleve | null = null,
    id = 'e1',
  ): Promise<void> {
    page = harness.routeNativeElement as HTMLElement;
    const fiche = await vi.waitFor(() => http.expectOne(`/api/eleves/${id}`));
    const deLEleve = http.expectOne((r) => r.url === '/api/inscriptions' && r.params.get('eleve') === id);
    const sonAssurance = http.expectOne(`/api/eleves/${id}/assurance`);
    if (reponse === 404) {
      for (const requete of [fiche, deLEleve, sonAssurance]) requete.flush({ message: 'Élève introuvable' }, { status: 404, statusText: 'Not Found' });
    } else {
      fiche.flush(reponse);
      deLEleve.flush(inscriptions);
      if (assurance) sonAssurance.flush(assurance);
      else sonAssurance.flush(null, { status: 204, statusText: 'No Content' });
    }
    await vi.waitFor(() => expect(page.querySelector('h1')).not.toBeNull());
  }

  afterEach(() => http.verify());

  const attendre = (texte: string) => vi.waitFor(() => expect(page.textContent).toContain(texte));
  const bouton = (texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte) as HTMLButtonElement;

  async function cliquer(texte: string): Promise<void> {
    bouton(texte).click();
    await harness.fixture.whenStable();
  }

  it('montre ses contacts, et aucune inscription', async () => {
    await ouvrir();

    expect(page.querySelector('h1')?.textContent).toContain('Adem Miled');
    expect(page.querySelector('.sub')?.textContent).toContain('9ᵉ année de base');
    const contacts = page.querySelector('.contacts')?.textContent ?? '';
    expect(contacts).toContain('22 345 678');
    expect(contacts).toContain('adem@exemple.tn');
    expect(contacts).toContain('98 401 220');
    expect(contacts).toContain('Collège Ibn Khaldoun');
    expect(contacts).not.toContain('Élève majeur');
    expect(page.textContent).toContain('Aucune inscription : cet élève vient seulement à la bibliothèque.');
    expect(page.querySelector('.assurance')?.textContent).toContain('Sans objet : aucune inscription en Étude cette année.');
    expect(page.querySelector('.inscriptions a[href="/eleves/e1/inscription"]')?.textContent).toContain('Inscrire dans un groupe');
  });

  it('montre ses inscriptions et son assurance, et ne propose plus de supprimer la fiche', async () => {
    await ouvrir(ADEM, [INSCRIPTION], { annee: '2027-2028', montant: 20, dueLe: '2027-11-20' });

    const inscription = page.querySelector('.inscription') as HTMLElement;
    expect(inscription.querySelector('a[href="/professeurs/groupes/g1"]')?.textContent).toContain('Groupe Samedi 10h → 12h');
    expect(inscription.textContent).toContain('Mathématiques · Karim Ben Salah');
    expect(inscription.textContent).toContain('Étude, 40,000 TND par Mois');
    expect(inscription.textContent).toContain('Depuis le samedi 20/11/2027');
    expect(page.textContent).not.toContain('Aucune inscription');
    expect(page.querySelector('.assurance')?.textContent).toContain('Année 2027-2028 : 20,000 TND, due le 20/11/2027');
    expect([...page.querySelectorAll('button')].some((b) => b.textContent?.includes('Supprimer la fiche'))).toBe(false);
  });

  it('dit si l’élève est majeur', async () => {
    await ouvrir({ ...ADEM, majeur: true, telephoneParent: null });

    expect(page.querySelector('.contacts')?.textContent).toContain('Élève majeur');
  });

  it('modifie la fiche, et prévient d’un doublon possible', async () => {
    await ouvrir();

    await cliquer('Modifier');
    (await vi.waitFor(() => http.expectOne('/api/niveaux'))).flush(NIVEAUX);
    await vi.waitFor(() => expect((page.querySelector('[formcontrolname="prenom"]') as HTMLInputElement)?.value).toBe('Adem'));
    saisir(page, '[formcontrolname="etablissement"]', 'Lycée Pilote');
    envoyer(page);

    const requete = http.expectOne({ method: 'PUT', url: '/api/eleves/e1' });
    expect(requete.request.body.etablissement).toBe('Lycée Pilote');
    requete.flush({ eleve: { ...ADEM, etablissement: 'Lycée Pilote' }, avertissement: 'Doublon possible : Adem Miled (8ᵉ année de base)' });

    await attendre('Fiche enregistrée');
    expect(page.querySelector('.note.warn')?.textContent).toContain('Doublon possible : Adem Miled (8ᵉ année de base)');
    expect(page.querySelector('form')).toBeNull();
    expect(page.querySelector('.contacts')?.textContent).toContain('Lycée Pilote');
  });

  it('montre l’avertissement d’une création qui vient d’avoir lieu', async () => {
    await preparer();
    await TestBed.inject(Router).navigate(['/eleves', 'e1'], { state: { avertissement: 'Doublon possible : Adem Miled (9ᵉ année de base)' } });
    await repondre(ADEM);

    await vi.waitFor(() => expect(page.querySelector('.note.warn')?.textContent).toContain('Doublon possible'));
    // Lu une fois : un rechargement de la page ou un retour en arrière ne le montre plus.
    expect((TestBed.inject(Location).getState() as { avertissement?: string } | null)?.avertissement).toBeUndefined();
  });

  it('supprime la fiche après confirmation', async () => {
    await ouvrir();

    await cliquer('Supprimer la fiche');
    expect(page.textContent).toContain("Supprimer la fiche d'Adem Miled ? Seulement en cas d'erreur de saisie.");
    await cliquer('Non');
    expect(bouton('Oui')).toBeUndefined();

    await cliquer('Supprimer la fiche');
    await cliquer('Oui');
    http.expectOne({ method: 'DELETE', url: '/api/eleves/e1' }).flush(null, { status: 204, statusText: 'No Content' });

    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/eleves'));
  });

  it("dit quand l'élève n'existe pas", async () => {
    await ouvrir(404);

    expect(page.textContent).toContain("Cet élève n'existe pas.");
    expect(page.querySelector('a[href="/eleves"]')).not.toBeNull();
  });

  it("suit l'adresse quand on passe d'un élève à un autre, et supprime celui qui est affiché", async () => {
    await ouvrir();

    await harness.navigateByUrl('/eleves/e2');
    await repondre({ ...ADEM, id: 'e2', prenom: 'Hédi', nom: 'Ben Salah' }, [], null, 'e2');
    await vi.waitFor(() => expect(page.querySelector('h1')?.textContent).toContain('Hédi Ben Salah'));

    await cliquer('Supprimer la fiche');
    expect(page.textContent).toContain("Supprimer la fiche d'Hédi Ben Salah ?");
    await cliquer('Oui');
    http.expectOne({ method: 'DELETE', url: '/api/eleves/e2' }).flush(null, { status: 204, statusText: 'No Content' });
    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/eleves'));
  });
});
