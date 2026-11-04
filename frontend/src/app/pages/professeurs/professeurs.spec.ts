import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { Professeur } from './professeur';
import { Professeurs } from './professeurs';

describe('la page « Professeurs »', () => {
  const ADRESSE = '/api/professeurs';
  const KARIM: Professeur = {
    id: 'k',
    prenom: 'Karim',
    nom: 'Ben Salah',
    telephone: '98 401 220',
    email: 'karim@exemple.tn',
    parti: false,
    groupesOuverts: 0,
  };
  const LEILA: Professeur = { id: 'l', prenom: 'Leila', nom: 'Khelifi', telephone: '22 918 004', email: null, parti: false, groupesOuverts: 2 };
  const SAMI: Professeur = { id: 's', prenom: 'Sami', nom: 'Chaabane', telephone: '55 230 671', email: null, parti: true, groupesOuverts: 0 };

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;

  async function ouvrir(professeurs: Professeur[] | 'erreur'): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'professeurs', component: Professeurs },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/professeurs');
    page = harness.routeNativeElement as HTMLElement;
    const requete = await vi.waitFor(() => http.expectOne(ADRESSE));
    if (professeurs === 'erreur') requete.flush({ message: 'Erreur du serveur' }, { status: 500, statusText: 'Erreur' });
    else requete.flush(professeurs);
    await vi.waitFor(() => expect(page.querySelector('.liste')?.textContent?.trim()).not.toBe(''));
  }

  afterEach(() => http.verify());

  const attendre = (texte: string) => vi.waitFor(() => expect(page.textContent).toContain(texte));
  const liste = () => [...page.querySelectorAll('.choix')] as HTMLElement[];
  const noms = () => liste().map((choix) => choix.querySelector('.nom')?.textContent?.trim());
  const fiche = () => page.querySelector('.fiche') as HTMLElement;
  const boutons = (endroit: HTMLElement) => [...endroit.querySelectorAll('button')].map((b) => b.textContent?.trim());
  const bouton = (endroit: HTMLElement, libelle: string) => {
    const trouve = [...endroit.querySelectorAll('button')].find((b) => b.textContent?.trim() === libelle);
    if (!trouve) throw new Error(`Aucun bouton « ${libelle} »`);
    return trouve as HTMLButtonElement;
  };

  async function cliquer(endroit: HTMLElement, libelle: string): Promise<void> {
    bouton(endroit, libelle).click();
    await harness.fixture.whenStable();
  }

  async function choisir(nom: string): Promise<void> {
    const choix = liste().find((c) => c.textContent?.includes(nom));
    if (!choix) throw new Error(`« ${nom} » n'est pas dans la liste`);
    choix.click();
    await harness.fixture.whenStable();
  }

  it('liste les professeurs au centre, avec leurs groupes ouverts', async () => {
    await ouvrir([KARIM, LEILA, SAMI]);

    expect(noms()).toEqual(['Karim Ben Salah', 'Leila Khelifi']);
    expect(liste()[0].textContent).toContain('Aucun groupe ouvert');
    expect(liste()[1].textContent).toContain('2 groupes ouverts');
    expect(page.querySelector('.sub')?.textContent).toContain('2 professeurs');
    expect(liste()[0].getAttribute('aria-pressed')).toBe('true');
    expect(fiche().querySelector('h2')?.textContent).toContain('Karim Ben Salah');
    expect(fiche().textContent).toContain('98 401 220');
    expect(fiche().textContent).toContain('karim@exemple.tn');
    expect(fiche().textContent).toContain('Aucun groupe ouvert');
    expect(boutons(fiche())).toEqual(['Modifier', 'Marquer parti', 'Supprimer']);
  });

  it("dit quand il n'y a aucun professeur", async () => {
    await ouvrir([]);

    expect(page.querySelector('.liste')?.textContent).toContain("Aucun professeur pour l'instant. Ajoutez le premier.");
    expect(page.querySelector('.sub')?.textContent).toContain('Aucun professeur');
    expect(fiche()).toBeNull();
  });

  it('compte un seul professeur au singulier', async () => {
    await ouvrir([KARIM, SAMI]);

    expect(page.querySelector('.sub')?.textContent?.trim()).toBe('1 professeur');
  });

  it('montre les professeurs partis dans leur filtre', async () => {
    await ouvrir([KARIM, LEILA, SAMI]);

    await cliquer(page, 'Professeurs partis');

    expect(bouton(page, 'Professeurs partis').getAttribute('aria-pressed')).toBe('true');
    expect(noms()).toEqual(['Sami Chaabane']);
    expect(fiche().querySelector('h2')?.textContent).toContain('Sami Chaabane');
    expect(fiche().querySelector('.pill')?.textContent?.trim()).toBe('Parti');
    expect(boutons(fiche())).toEqual(['Modifier', 'Revenir au centre', 'Supprimer']);
  });

  it("dit quand aucun professeur n'est parti", async () => {
    await ouvrir([KARIM]);

    await cliquer(page, 'Professeurs partis');

    expect(page.querySelector('.liste')?.textContent).toContain('Aucun professeur parti.');
    expect(fiche()).toBeNull();
  });

  it('choisit un autre professeur', async () => {
    await ouvrir([KARIM, LEILA]);

    await choisir('Leila Khelifi');

    expect(liste()[1].getAttribute('aria-pressed')).toBe('true');
    expect(fiche().querySelector('h2')?.textContent).toContain('Leila Khelifi');
    expect(fiche().textContent).toContain('2 groupes ouverts');
    expect(fiche().textContent).not.toContain('@');
  });

  it('marque un professeur parti', async () => {
    await ouvrir([KARIM, LEILA]);

    await cliquer(fiche(), 'Marquer parti');
    expect(bouton(fiche(), 'Marquer parti').disabled).toBe(true);
    http.expectOne({ method: 'POST', url: `${ADRESSE}/k/depart` }).flush({ ...KARIM, parti: true });

    await attendre('Karim Ben Salah a quitté le centre');
    expect(bouton(page, 'Professeurs partis').getAttribute('aria-pressed')).toBe('true');
    expect(noms()).toEqual(['Karim Ben Salah']);
    expect(fiche().querySelector('h2')?.textContent).toContain('Karim Ben Salah');
    expect(page.querySelector('.cote .note.ok')?.textContent).toContain('Karim Ben Salah a quitté le centre');

    await cliquer(page, 'Au centre');
    expect(noms()).toEqual(['Leila Khelifi']);
  });

  it("ignore le second clic d'un double-clic, qui tombe sur le bouton inverse", async () => {
    await ouvrir([KARIM]);
    const secondClic = async (libelle: string) => {
      bouton(fiche(), libelle).dispatchEvent(new MouseEvent('click', { detail: 2, bubbles: true }));
      await harness.fixture.whenStable();
    };

    await cliquer(fiche(), 'Marquer parti');
    http.expectOne(`${ADRESSE}/k/depart`).flush({ ...KARIM, parti: true });
    await attendre('Karim Ben Salah a quitté le centre');
    await secondClic('Revenir au centre');
    http.expectNone(`${ADRESSE}/k/retour`);

    await cliquer(fiche(), 'Revenir au centre');
    http.expectOne(`${ADRESSE}/k/retour`).flush(KARIM);
    await attendre('Karim Ben Salah est de retour au centre');
    await secondClic('Marquer parti');
    http.expectNone(`${ADRESSE}/k/depart`);
  });

  it('fait revenir un professeur au centre', async () => {
    await ouvrir([KARIM, SAMI]);
    await cliquer(page, 'Professeurs partis');

    await cliquer(fiche(), 'Revenir au centre');
    http.expectOne({ method: 'POST', url: `${ADRESSE}/s/retour` }).flush({ ...SAMI, parti: false });

    await attendre('Sami Chaabane est de retour au centre');
    expect(bouton(page, 'Au centre').getAttribute('aria-pressed')).toBe('true');
    expect(noms()).toEqual(['Karim Ben Salah', 'Sami Chaabane']);
    expect(fiche().querySelector('h2')?.textContent).toContain('Sami Chaabane');
  });

  it('supprime un professeur après confirmation', async () => {
    await ouvrir([KARIM, LEILA]);

    await cliquer(fiche(), 'Supprimer');
    expect(fiche().textContent).toContain("Supprimer Karim Ben Salah ? Seulement en cas d'erreur de saisie.");
    await cliquer(fiche(), 'Non');
    expect(boutons(fiche())).toEqual(['Modifier', 'Marquer parti', 'Supprimer']);

    await cliquer(fiche(), 'Supprimer');
    await cliquer(fiche(), 'Oui');
    http.expectOne({ method: 'DELETE', url: `${ADRESSE}/k` }).flush(null, { status: 204, statusText: 'No Content' });

    await attendre('Fiche de Karim Ben Salah supprimée');
    expect(noms()).toEqual(['Leila Khelifi']);
    expect(fiche().querySelector('h2')?.textContent).toContain('Leila Khelifi');
  });

  it('montre un refus du serveur sous la fiche', async () => {
    await ouvrir([KARIM]);

    await cliquer(fiche(), 'Marquer parti');
    http
      .expectOne(`${ADRESSE}/k/depart`)
      .flush({ message: "Fermez d'abord ses groupes" }, { status: 409, statusText: 'Conflict' });

    await attendre("Fermez d'abord ses groupes");
    expect(page.querySelector('.cote .note.bad')?.textContent).toContain("Fermez d'abord ses groupes");
    expect(noms()).toEqual(['Karim Ben Salah']);
    expect(bouton(fiche(), 'Marquer parti').disabled).toBe(false);
  });

  // ----- Le formulaire -----

  const NADIA: Professeur = { id: 'n', prenom: 'Nadia', nom: 'Bouzid', telephone: '97 112 846', email: null, parti: false, groupesOuverts: 0 };
  const champ = (nom: string) => page.querySelector(`[formcontrolname="${nom}"]`) as HTMLInputElement;

  async function remplir(prenom: string, nom: string, telephone: string): Promise<void> {
    saisir(page, '[formcontrolname="prenom"]', prenom);
    saisir(page, '[formcontrolname="nom"]', nom);
    saisir(page, '[formcontrolname="telephone"]', telephone);
    await harness.fixture.whenStable();
  }

  async function ouvrirLAjout(): Promise<void> {
    await cliquer(page, 'Ajouter un professeur');
    expect(page.querySelector('form')).not.toBeNull();
  }

  it('ajoute un professeur', async () => {
    await ouvrir([KARIM, LEILA]);
    await ouvrirLAjout();
    expect(page.querySelector('form')?.textContent).toContain('Email (facultatif)');
    expect(fiche()).toBeNull();

    await remplir(' Nadia ', 'Bouzid', '97 112 846');
    envoyer(page);
    const requete = http.expectOne({ method: 'POST', url: ADRESSE });
    expect(requete.request.body).toEqual({ prenom: 'Nadia', nom: 'Bouzid', telephone: '97 112 846', email: '' });
    requete.flush({ professeur: NADIA, avertissement: null }, { status: 201, statusText: 'Created' });

    await attendre('Professeur ajouté');
    expect(page.querySelector('form')).toBeNull();
    expect(fiche().querySelector('h2')?.textContent).toContain('Nadia Bouzid');
    expect(noms()).toEqual(['Karim Ben Salah', 'Leila Khelifi', 'Nadia Bouzid']);
    expect(page.querySelector('.note.warn')).toBeNull();
  });

  it('refuse une fiche incomplète', async () => {
    await ouvrir([KARIM]);
    await ouvrirLAjout();

    await remplir('Nadia', 'Bouzid', '  ');
    envoyer(page);

    await attendre('Remplissez le prénom, le nom et le téléphone');
    expect(page.querySelector('.cote .note.bad')).not.toBeNull();
  });

  it("prévient d'un téléphone déjà pris", async () => {
    await ouvrir([KARIM]);
    await ouvrirLAjout();

    await remplir('Nadia', 'Bouzid', '98 401 220');
    envoyer(page);
    http
      .expectOne(ADRESSE)
      .flush(
        { professeur: { ...NADIA, telephone: '98 401 220' }, avertissement: 'Ce téléphone est aussi celui de Karim Ben Salah' },
        { status: 201, statusText: 'Created' },
      );

    await attendre('Professeur ajouté');
    expect(page.querySelector('.cote .note.warn')?.textContent).toContain('Ce téléphone est aussi celui de Karim Ben Salah');
  });

  it('modifie un professeur', async () => {
    await ouvrir([KARIM, LEILA]);

    await cliquer(fiche(), 'Modifier');
    expect([champ('prenom').value, champ('nom').value, champ('telephone').value, champ('email').value]).toEqual([
      'Karim',
      'Ben Salah',
      '98 401 220',
      'karim@exemple.tn',
    ]);

    saisir(page, '[formcontrolname="nom"]', 'Ben Salah Jr');
    envoyer(page);
    const requete = http.expectOne({ method: 'PUT', url: `${ADRESSE}/k` });
    expect(requete.request.body).toEqual({ prenom: 'Karim', nom: 'Ben Salah Jr', telephone: '98 401 220', email: 'karim@exemple.tn' });
    requete.flush({ professeur: { ...KARIM, nom: 'Ben Salah Jr' }, avertissement: null });

    await attendre('Fiche enregistrée');
    expect(noms()).toEqual(['Karim Ben Salah Jr', 'Leila Khelifi']);
    expect(fiche().querySelector('h2')?.textContent).toContain('Karim Ben Salah Jr');
  });

  it("remplit l'email vide d'un professeur sans email", async () => {
    await ouvrir([LEILA]);

    await cliquer(fiche(), 'Modifier');

    expect(champ('email').value).toBe('');
  });

  it('annule le formulaire', async () => {
    await ouvrir([KARIM]);
    await ouvrirLAjout();

    await cliquer(page, 'Annuler');

    expect(page.querySelector('form')).toBeNull();
    expect(fiche().querySelector('h2')?.textContent).toContain('Karim Ben Salah');
  });

  it("montre un refus sous le formulaire, et bloque le bouton pendant l'envoi", async () => {
    await ouvrir([KARIM]);
    await ouvrirLAjout();

    await remplir('Nadia', 'Bouzid', '9711284');
    envoyer(page);
    await vi.waitFor(() => expect(bouton(page, 'Enregistrer').disabled).toBe(true));
    http.expectOne(ADRESSE).flush({ message: 'Le téléphone doit avoir 8 chiffres' }, { status: 400, statusText: 'Bad Request' });

    await attendre('Le téléphone doit avoir 8 chiffres');
    expect(page.querySelector('.cote .note.bad')?.textContent).toContain('Le téléphone doit avoir 8 chiffres');
    expect(page.querySelector('form')).not.toBeNull();
    expect(bouton(page, 'Enregistrer').disabled).toBe(false);
  });

  it("bloque l'ajout si la liste n'a pas pu se charger", async () => {
    await ouvrir('erreur');

    expect(page.querySelector('.liste .note.bad')?.textContent).toContain('Erreur du serveur');
    expect(bouton(page, 'Ajouter un professeur').disabled).toBe(true);
  });
});
