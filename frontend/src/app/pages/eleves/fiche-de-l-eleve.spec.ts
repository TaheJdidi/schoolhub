import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { envoyer, saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { Session } from '../../socle/session/session';
import { Moi } from '../../socle/session/moi';
import { AMIRA, HEDI } from '../../test/personnes';
import { dateDuJourLocale } from '../groupes/groupe';
import { APayer, AssuranceDeLEleve, InscriptionDeLEleve } from '../inscriptions/inscription';
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
    prixSpecial: null,
    raisonDuPrixSpecial: null,
    reglees: [],
    payablesAvantSchoolHub: [],
    peutRegrouperLeReste: false,
    arreteLe: null,
    report: 0,
  };

  const RIEN: APayer = { lignes: [], total: 0 };

  async function ouvrir(
    reponse: FicheDeLEleve | 404 = ADEM,
    inscriptions: InscriptionDeLEleve[] = [],
    assurance: AssuranceDeLEleve | null = null,
    aPayer: APayer = RIEN,
    moi: Moi = AMIRA,
  ): Promise<void> {
    await preparer();
    TestBed.inject(Session).connaitre(moi);
    await harness.navigateByUrl('/eleves/e1');
    await repondre(reponse, inscriptions, assurance, 'e1', aPayer);
  }

  /** La fiche, ses inscriptions, son assurance (204 : sans objet) et « À payer », demandés ensemble. */
  async function repondre(
    reponse: FicheDeLEleve | 404,
    inscriptions: InscriptionDeLEleve[] = [],
    assurance: AssuranceDeLEleve | null = null,
    id = 'e1',
    aPayer: APayer = RIEN,
  ): Promise<void> {
    page = harness.routeNativeElement as HTMLElement;
    const fiche = await vi.waitFor(() => http.expectOne(`/api/eleves/${id}`));
    const deLEleve = http.expectOne((r) => r.url === '/api/inscriptions' && r.params.get('eleve') === id);
    const sonAssurance = http.expectOne(`/api/eleves/${id}/assurance`);
    const aRegler = http.expectOne(`/api/eleves/${id}/a-payer`);
    if (reponse === 404) {
      for (const requete of [fiche, deLEleve, sonAssurance, aRegler]) {
        requete.flush({ message: 'Élève introuvable' }, { status: 404, statusText: 'Not Found' });
      }
    } else {
      fiche.flush(reponse);
      deLEleve.flush(inscriptions);
      if (assurance) sonAssurance.flush(assurance);
      else sonAssurance.flush(null, { status: 204, statusText: 'No Content' });
      aRegler.flush(aPayer);
    }
    await vi.waitFor(() => expect(page.querySelector('h1')).not.toBeNull());
  }

  afterEach(() => http.verify());

  const attendre = (texte: string) => vi.waitFor(() => expect(page.textContent).toContain(texte));
  const bouton = (texte: string) =>
    [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte) as HTMLButtonElement | undefined;

  async function cliquer(texte: string): Promise<void> {
    bouton(texte)!.click();
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
    await ouvrir(ADEM, [INSCRIPTION], { annee: '2027-2028', montant: 20, dueLe: '2027-11-20', etat: 'APayer', raisonDeLaDispense: null });

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

  // ----- À payer et l'assurance (ELV-04, ELV-06, ELV-07) -----

  const DU: APayer = {
    lignes: [
      { inscriptionId: 'i1', groupe: 'Mathématiques · Groupe Samedi 10h → 12h', libelle: 'Mois 3 (3 séances)', cle: 'Mois 3', montant: 30, dueLe: '2027-11-20' },
      { inscriptionId: null, groupe: 'Assurance', libelle: 'Assurance 2027-2028', cle: 'Assurance', montant: 20, dueLe: '2027-11-20' },
    ],
    total: 50,
  };
  const ASSURANCE: AssuranceDeLEleve = { annee: '2027-2028', montant: 20, dueLe: '2027-11-20', etat: 'APayer', raisonDeLaDispense: null };

  /** Après une action, la page relit les inscriptions, l'assurance et « À payer ». */
  async function rafraichir(inscriptions: InscriptionDeLEleve[], assurance: AssuranceDeLEleve | null, aPayer: APayer = RIEN): Promise<void> {
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/inscriptions' && r.params.get('eleve') === 'e1'))).flush(inscriptions);
    const sonAssurance = http.expectOne('/api/eleves/e1/assurance');
    if (assurance) sonAssurance.flush(assurance);
    else sonAssurance.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne('/api/eleves/e1/a-payer').flush(aPayer);
  }

  const carte = (classe: string) => page.querySelector(`.${classe}`) as HTMLElement;

  it("montre ce que l'élève doit à ce jour", async () => {
    await ouvrir(ADEM, [INSCRIPTION], ASSURANCE, DU);

    const lignes = [...carte('a-payer').querySelectorAll('.ligne-a-payer')].map((l) => l.textContent?.replace(/\s+/g, ' ').trim());
    expect(lignes[0]).toContain('Mathématiques · Groupe Samedi 10h → 12h');
    expect(lignes[0]).toContain('Mois 3 (3 séances)');
    expect(lignes[0]).toContain('30,000 TND');
    expect(lignes[0]).toContain('dû le 20/11/2027');
    expect(lignes[1]).toContain('Assurance 2027-2028');
    expect(carte('a-payer').querySelector('.total')?.textContent).toContain('50,000 TND');
  });

  it("dit quand il n'y a rien à payer", async () => {
    await ouvrir();

    expect(carte('a-payer').textContent).toContain("Rien à payer aujourd'hui.");
  });

  it("montre l'état de l'assurance", async () => {
    await ouvrir(ADEM, [INSCRIPTION], ASSURANCE);
    expect(carte('assurance').textContent).toContain('Année 2027-2028 : 20,000 TND, due le 20/11/2027');
    expect(carte('assurance').textContent).toContain('à payer');
    TestBed.resetTestingModule();

    await ouvrir(ADEM, [INSCRIPTION], { ...ASSURANCE, etat: 'PayeeAvantSchoolHub' });
    expect(carte('assurance').textContent).toContain('payée avant SchoolHub');
    expect(bouton('Déjà payée avant SchoolHub')).toBeUndefined();
    TestBed.resetTestingModule();

    await ouvrir(ADEM, [INSCRIPTION], { ...ASSURANCE, etat: 'Dispensee', raisonDeLaDispense: 'Orphelin' });
    expect(carte('assurance').textContent).toContain('dispensée (Orphelin)');
  });

  it("note l'assurance payée avant SchoolHub, après confirmation", async () => {
    await ouvrir(ADEM, [INSCRIPTION], ASSURANCE, DU);

    await cliquer('Déjà payée avant SchoolHub');
    expect(carte('assurance').textContent).toContain("Noter l'assurance 2027-2028 comme payée avant SchoolHub ?");
    await cliquer('Oui');
    const requete = http.expectOne({ method: 'POST', url: '/api/eleves/e1/assurance/payee-avant-schoolhub' });
    expect(bouton('Oui')?.disabled).toBe(true);
    requete.flush({ ...ASSURANCE, etat: 'PayeeAvantSchoolHub' });
    await rafraichir([INSCRIPTION], { ...ASSURANCE, etat: 'PayeeAvantSchoolHub' });

    await vi.waitFor(() => expect(carte('assurance').querySelector('.etat')?.textContent).toContain('payée avant SchoolHub'));
    expect(carte('a-payer').textContent).toContain("Rien à payer aujourd'hui.");
  });

  it("le propriétaire dispense de l'assurance", async () => {
    await ouvrir(ADEM, [INSCRIPTION], ASSURANCE, DU, HEDI);

    await cliquer('Dispenser');
    saisir(page, '[aria-label="Raison de la dispense"]', 'Orphelin');
    await cliquer("Dispenser l'assurance");
    const requete = http.expectOne({ method: 'POST', url: '/api/eleves/e1/assurance/dispense' });
    expect(requete.request.body).toEqual({ raison: 'Orphelin' });
    requete.flush({ ...ASSURANCE, etat: 'Dispensee', raisonDeLaDispense: 'Orphelin' });
    await rafraichir([INSCRIPTION], { ...ASSURANCE, etat: 'Dispensee', raisonDeLaDispense: 'Orphelin' });

    await vi.waitFor(() => expect(carte('assurance').textContent).toContain('dispensée (Orphelin)'));
  });

  it("la secrétaire ne dispense pas de l'assurance", async () => {
    await ouvrir(ADEM, [INSCRIPTION], ASSURANCE, DU, AMIRA);

    expect(carte('assurance').textContent).toContain("Seul le propriétaire dispense de l'assurance.");
    expect(bouton('Dispenser')).toBeUndefined();
  });

  it("montre le refus sous la carte de l'assurance", async () => {
    await ouvrir(ADEM, [INSCRIPTION], ASSURANCE, DU);

    await cliquer('Déjà payée avant SchoolHub');
    await cliquer('Oui');
    http.expectOne('/api/eleves/e1/assurance/payee-avant-schoolhub').flush({ message: "L'assurance est déjà payée" }, { status: 409, statusText: 'Conflict' });

    await vi.waitFor(() => expect(carte('assurance').querySelector('[role="alert"]')?.textContent).toContain("L'assurance est déjà payée"));
  });

  // ----- Le prix spécial, payé avant SchoolHub et le reste (ELV-05, ELV-07, ELV-10) -----

  const AVEC_PRIX_SPECIAL: InscriptionDeLEleve = { ...INSCRIPTION, prixSpecial: 20, raisonDuPrixSpecial: 'Famille en difficulté' };

  it('montre le prix spécial', async () => {
    await ouvrir(ADEM, [AVEC_PRIX_SPECIAL]);

    expect(carte('inscription').textContent).toContain('Prix spécial 20,000 TND au lieu de 40,000 TND (Famille en difficulté)');
  });

  it('le propriétaire accorde un prix spécial', async () => {
    await ouvrir(ADEM, [INSCRIPTION], null, RIEN, HEDI);

    await cliquer('Prix spécial');
    saisir(page, '[aria-label="Prix spécial (TND)"]', '20');
    saisir(page, '[aria-label="Raison du prix spécial"]', 'Famille en difficulté');
    await cliquer('Enregistrer le prix spécial');
    const requete = http.expectOne({ method: 'PUT', url: '/api/inscriptions/i1/prix-special' });
    expect(requete.request.body).toEqual({ montant: 20, raison: 'Famille en difficulté' });
    expect(bouton('Enregistrer le prix spécial')?.disabled).toBe(true);
    requete.flush(AVEC_PRIX_SPECIAL);
    await rafraichir([AVEC_PRIX_SPECIAL], null);

    await vi.waitFor(() => expect(carte('inscription').textContent).toContain('Prix spécial 20,000 TND au lieu de 40,000 TND'));
  });

  it('le propriétaire retire le prix spécial', async () => {
    await ouvrir(ADEM, [AVEC_PRIX_SPECIAL], null, RIEN, HEDI);

    await cliquer('Retirer le prix spécial');
    http.expectOne({ method: 'DELETE', url: '/api/inscriptions/i1/prix-special' }).flush(INSCRIPTION);
    await rafraichir([INSCRIPTION], null);

    await vi.waitFor(() => expect(carte('inscription').textContent).not.toContain('Prix spécial 20,000 TND'));
  });

  it("la secrétaire lit qu'elle n'accorde pas de prix spécial", async () => {
    await ouvrir(ADEM, [INSCRIPTION], null, RIEN, AMIRA);

    expect(carte('inscriptions').textContent).toContain('Seul le propriétaire accorde un prix spécial. Inscrivez au prix normal et prévenez-le.');
    expect(bouton('Prix spécial')).toBeUndefined();
  });

  it('note une somme payée avant SchoolHub, après confirmation', async () => {
    const revision: InscriptionDeLEleve = {
      ...INSCRIPTION,
      groupe: { ...INSCRIPTION.groupe, type: 'Revision', matiere: 'Physique' },
      mode: 'EnTotalite',
      prixNormal: 100,
      payablesAvantSchoolHub: [{ cle: 'Totalité', libelle: 'Totalité, séances 4 à 10', montant: 70 }],
    };
    await ouvrir(ADEM, [revision]);
    expect(carte('inscription').textContent).toContain('Totalité, séances 4 à 10 · 70,000 TND');

    await cliquer('Déjà payé avant SchoolHub');
    expect(carte('inscription').textContent).toContain('Noter « Totalité, séances 4 à 10 » comme payé avant SchoolHub ?');
    await cliquer('Oui');
    const requete = http.expectOne({ method: 'POST', url: '/api/inscriptions/i1/paye-avant-schoolhub' });
    expect(requete.request.body).toEqual({ cle: 'Totalité' });
    const reglee = { ...revision, payablesAvantSchoolHub: [], reglees: [{ libelle: 'Totalité, séances 4 à 10', montant: 70, origine: 'AvantSchoolHub' }] };
    requete.flush(reglee);
    await rafraichir([reglee], null);

    await vi.waitFor(() => expect(carte('inscription').textContent).toContain('Totalité, séances 4 à 10 · 70,000 TND · payé avant SchoolHub'));
    expect(bouton('Déjà payé avant SchoolHub')).toBeUndefined();
  });

  it('paie tout le reste d’une Formation, après confirmation', async () => {
    const formation: InscriptionDeLEleve = {
      ...INSCRIPTION,
      groupe: { ...INSCRIPTION.groupe, type: 'Formation', matiere: 'Informatique' },
      mode: 'EnTranches',
      prixNormal: 300,
      nombreDeTranches: 3,
      peutRegrouperLeReste: true,
    };
    await ouvrir(ADEM, [formation]);

    await cliquer('Payer tout le reste');
    expect(carte('inscription').textContent).toContain("Regrouper les tranches à venir en une somme due aujourd'hui ?");
    await cliquer('Oui');
    http.expectOne({ method: 'POST', url: '/api/inscriptions/i1/reste' }).flush({ ...formation, peutRegrouperLeReste: false });
    await rafraichir([{ ...formation, peutRegrouperLeReste: false }], null, {
      lignes: [{ inscriptionId: 'i1', groupe: 'Informatique · Groupe Samedi 10h → 12h', libelle: 'Reste de la Formation', cle: 'Reste', montant: 200, dueLe: '2027-11-27' }],
      total: 200,
    });

    await vi.waitFor(() => expect(carte('a-payer').textContent).toContain('Reste de la Formation'));
    expect(bouton('Payer tout le reste')).toBeUndefined();
  });

  it('montre le refus sous la carte des inscriptions', async () => {
    await ouvrir(ADEM, [INSCRIPTION], null, RIEN, HEDI);

    await cliquer('Prix spécial');
    await cliquer('Enregistrer le prix spécial');
    http.expectOne('/api/inscriptions/i1/prix-special').flush({ message: 'Écrivez la raison du prix spécial' }, { status: 400, statusText: 'Bad Request' });

    await vi.waitFor(() => expect(carte('inscriptions').querySelector('[role="alert"]')?.textContent).toContain('Écrivez la raison du prix spécial'));
  });

  // ----- Arrêter et changer de groupe (ELV-08, ELV-09) -----

  it("arrête une inscription, à la date choisie", async () => {
    await ouvrir(ADEM, [INSCRIPTION]);

    await cliquer("Arrêter l'inscription");
    const date = page.querySelector('[aria-label="Date de l\'arrêt"]') as HTMLInputElement;
    expect(date.value).toBe(dateDuJourLocale());
    expect(carte('inscription').textContent).toContain("Rien ne sera dû après cette date ; ce qui était dû avant reste dû. Pas de remboursement.");
    saisir(page, '[aria-label="Date de l\'arrêt"]', '2027-12-04');
    await cliquer("Arrêter l'inscription");
    const requete = http.expectOne({ method: 'POST', url: '/api/inscriptions/i1/arret' });
    expect(requete.request.body).toEqual({ date: '2027-12-04' });
    requete.flush({ ...INSCRIPTION, arreteLe: '2027-12-04' });
    await rafraichir([{ ...INSCRIPTION, arreteLe: '2027-12-04' }], null);

    await vi.waitFor(() => expect(carte('inscription').textContent).toContain('Inscription arrêtée le 04/12/2027'));
  });

  it("une inscription arrêtée n'a plus d'action", async () => {
    await ouvrir(ADEM, [{ ...INSCRIPTION, arreteLe: '2027-12-04' }], null, RIEN, HEDI);

    expect(carte('inscription').textContent).toContain('Inscription arrêtée le 04/12/2027');
    expect(bouton("Arrêter l'inscription")).toBeUndefined();
    expect(bouton('Prix spécial')).toBeUndefined();
    expect(carte('inscription').querySelector('a[href="/eleves/e1/changement/i1"]')).toBeNull();
  });

  it('montre le report et mène au changement de groupe', async () => {
    await ouvrir(ADEM, [{ ...INSCRIPTION, report: 30 }]);

    expect(carte('inscription').textContent).toContain("Report de 30,000 TND depuis l'ancien groupe");
    expect(carte('inscription').querySelector('a[href="/eleves/e1/changement/i1"]')?.textContent).toContain('Changer de groupe');
  });
});
