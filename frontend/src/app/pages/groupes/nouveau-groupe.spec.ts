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

  async function ouvrir(niveaux: typeof NIVEAUX | 'erreur' = NIVEAUX): Promise<void> {
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
    (await vi.waitFor(() => http.expectOne('/api/professeurs'))).flush([KARIM, SAMI, LEILA]);
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
    element.dispatchEvent(new Event('input'));
    await harness.fixture.whenStable();
  }

  async function remplir(): Promise<void> {
    saisir(page, '[formcontrolname="matiere"]', 'Mathématiques');
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
    expect([...page.querySelectorAll('datalist option')].map((o) => o.getAttribute('value'))).toEqual(['Mathématiques']);
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
});
