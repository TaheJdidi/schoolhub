import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { envoyer, saisir } from '../../test/formulaires';
import { ActionSurLeGroupe, FenetreDuGroupe } from './fenetre-du-groupe';
import { dateDuJourLocale, FicheDuGroupe, SeanceDuGroupe } from './groupe';

describe('la fenêtre « Modifier » ou « Fermer le groupe »', () => {
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
  const REVISION: FicheDuGroupe = { ...FICHE, type: 'Revision', prixDuMois: null, mois: [], prixTotal: 50, nombreDeSeances: 3 };

  let http: HttpTestingController;
  let fixture: ComponentFixture<FenetreDuGroupe>;
  let page: HTMLElement;
  let faits: string[];
  let abandons: number;

  async function ouvrir(action: ActionSurLeGroupe, fiche: FicheDuGroupe = FICHE): Promise<void> {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(FenetreDuGroupe);
    fixture.componentRef.setInput('groupeId', 'g1');
    fixture.componentRef.setInput('action', action);
    faits = [];
    abandons = 0;
    fixture.componentInstance.fait.subscribe((message) => faits.push(message));
    fixture.componentInstance.abandon.subscribe(() => abandons++);
    page = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
    http.expectOne(ADRESSE).flush(fiche);
    await vi.waitFor(() => expect(page.querySelector('form')).not.toBeNull());
  }

  afterEach(() => http.verify());

  const champ = (nom: string) => page.querySelector(`[formcontrolname="${nom}"]`) as HTMLInputElement;
  const bouton = (libelle: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === libelle) as HTMLButtonElement;
  const attendre = (texte: string) => vi.waitFor(() => expect(page.textContent).toContain(texte));
  const avertissement = () => page.querySelector('.aide')?.textContent?.trim();

  it('modifie le nom, la capacité et le prix', async () => {
    await ouvrir('modifier');

    expect(page.querySelector('[role="dialog"] h2')?.textContent).toContain('Modifier le groupe');
    expect([champ('nom').value, champ('capacite').value, champ('prix').value]).toEqual(['Groupe Samedi 10h → 12h', '16', '45,000']);
    expect(page.textContent).toContain("Le nouveau prix vaut pour les Mois qui n'ont pas encore commencé.");

    saisir(page, '[formcontrolname="capacite"]', '');
    saisir(page, '[formcontrolname="prix"]', '50');
    envoyer(page);
    const requete = http.expectOne({ method: 'PUT', url: ADRESSE });
    expect(requete.request.body).toEqual({ nom: 'Groupe Samedi 10h → 12h', capacite: null, prixDuMois: 50 });
    requete.flush({ ...FICHE, capacite: null, prixDuMois: 50 });

    await vi.waitFor(() => expect(faits).toEqual(['Groupe enregistré']));
  });

  it('refuse un prix illisible, et montre le refus du serveur', async () => {
    await ouvrir('modifier');

    saisir(page, '[formcontrolname="prix"]', '');
    envoyer(page);
    await attendre('Écrivez le prix du Mois, par exemple 60,000');

    saisir(page, '[formcontrolname="prix"]', '45');
    saisir(page, '[formcontrolname="nom"]', 'a'.repeat(101));
    envoyer(page);
    http.expectOne(ADRESSE).flush({ message: 'Le nom du groupe fait au plus 100 caractères' }, { status: 400, statusText: 'Bad Request' });

    await attendre('Le nom du groupe fait au plus 100 caractères');
    expect(faits).toEqual([]);
  });

  it('modifie une Révision sans toucher à son prix', async () => {
    await ouvrir('modifier', REVISION);

    expect(champ('prix')).toBeNull();
    expect(page.textContent).not.toContain('Le nouveau prix');
    saisir(page, '[formcontrolname="nom"]', 'Révision du bac');
    envoyer(page);

    const requete = http.expectOne({ method: 'PUT', url: ADRESSE });
    expect(requete.request.body).toEqual({ nom: 'Révision du bac', capacite: 16, prixDuMois: null });
    requete.flush({ ...REVISION, nom: 'Révision du bac' });
    await vi.waitFor(() => expect(faits).toEqual(['Groupe enregistré']));
  });

  it('se referme sans rien changer', async () => {
    await ouvrir('modifier');

    bouton('Annuler').click();
    (page.querySelector('.voile') as HTMLElement).click();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));

    expect(abandons).toBe(3);
  });

  it('ferme le groupe à une date, avec un motif', async () => {
    await ouvrir('fermer');

    expect(champ('dateDeFermeture').value).toBe(dateDuJourLocale());
    saisir(page, '[formcontrolname="dateDeFermeture"]', '2027-10-02');
    await fixture.whenStable();
    expect(avertissement()).toBe('Les 3 séances prévues après le 02/10/2027 seront supprimées.');
    saisir(page, '[formcontrolname="dateDeFermeture"]', '2027-10-16');
    await fixture.whenStable();
    expect(avertissement()).toBe('La séance prévue après le 16/10/2027 sera supprimée.');
    saisir(page, '[formcontrolname="dateDeFermeture"]', '2027-10-23');
    await fixture.whenStable();
    expect(avertissement()).toBe('Aucune séance prévue après le 23/10/2027.');

    saisir(page, '[formcontrolname="dateDeFermeture"]', '2027-10-02');
    saisir(page, '[formcontrolname="motif"]', "Trop peu d'élèves");
    envoyer(page);
    const requete = http.expectOne({ method: 'POST', url: `${ADRESSE}/fermeture` });
    expect(requete.request.body).toEqual({ date: '2027-10-02', motif: "Trop peu d'élèves" });
    await vi.waitFor(() => expect(bouton('Fermer le groupe').disabled).toBe(true));
    requete.flush({ ...FICHE, fermeLe: '2027-10-02', motifDeFermeture: "Trop peu d'élèves" });

    await vi.waitFor(() => expect(faits).toEqual(['Groupe fermé']));
  });

  it('ne ferme pas sans motif', async () => {
    await ouvrir('fermer');

    envoyer(page);

    await attendre('Choisissez la date et écrivez le motif');
  });

  it('montre un refus de la fermeture', async () => {
    await ouvrir('fermer');
    saisir(page, '[formcontrolname="motif"]', 'Erreur');

    envoyer(page);
    http.expectOne(`${ADRESSE}/fermeture`).flush({ message: 'Ce groupe est déjà fermé' }, { status: 409, statusText: 'Conflict' });

    await attendre('Ce groupe est déjà fermé');
    expect(page.querySelector('.note.bad')?.textContent).toContain('Ce groupe est déjà fermé');
  });
});
