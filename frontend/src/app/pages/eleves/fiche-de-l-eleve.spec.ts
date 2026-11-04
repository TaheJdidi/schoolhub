import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
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

  async function ouvrir(reponse: FicheDeLEleve | 404 = ADEM): Promise<void> {
    await preparer();
    await harness.navigateByUrl('/eleves/e1');
    await repondre(reponse);
  }

  async function repondre(reponse: FicheDeLEleve | 404): Promise<void> {
    page = harness.routeNativeElement as HTMLElement;
    const requete = await vi.waitFor(() => http.expectOne('/api/eleves/e1'));
    if (reponse === 404) requete.flush({ message: 'Élève introuvable' }, { status: 404, statusText: 'Not Found' });
    else requete.flush(reponse);
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
    (await vi.waitFor(() => http.expectOne('/api/eleves/e2'))).flush({ ...ADEM, id: 'e2', prenom: 'Hédi', nom: 'Ben Salah' });
    await vi.waitFor(() => expect(page.querySelector('h1')?.textContent).toContain('Hédi Ben Salah'));

    await cliquer('Supprimer la fiche');
    expect(page.textContent).toContain("Supprimer la fiche d'Hédi Ben Salah ?");
    await cliquer('Oui');
    http.expectOne({ method: 'DELETE', url: '/api/eleves/e2' }).flush(null, { status: 204, statusText: 'No Content' });
    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/eleves'));
  });
});
