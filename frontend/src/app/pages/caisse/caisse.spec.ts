import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { Caisse } from './caisse';
import { EncaissementGroupe } from './encaissement-groupe';
import { PageDEncaissement } from './page-d-encaissement';
import { PageDuRecu } from './page-du-recu';
import { RecuAImprimer } from './recu-a-imprimer';
import { Moi } from '../../socle/session/moi';
import { Session } from '../../socle/session/session';
import { CaisseOuverte, Cloture, ControleDesClotures, MaCaisse } from './caisse-du-soir';
import { Encaissable, EncaissementDuGroupe, RecuDecrit } from './recu';

const RECU: RecuDecrit = {
  id: 'r1',
  numero: 12,
  jour: '2027-11-27',
  creeLe: '2027-11-27T09:00:00+00:00',
  centre: { nom: 'Centre Les Jasmins', telephone: '71 234 567' },
  eleve: { id: 'e1', prenom: 'Adem', nom: 'Miled' },
  lignes: [
    { groupe: 'Mathématiques · Groupe Samedi 10h → 12h', libelle: 'Mois 3', montant: 40 },
    { groupe: 'Assurance', libelle: 'Assurance 2027-2028', montant: 20 },
  ],
  total: 60,
  secretaire: 'Amira Jlassi',
  annule: false,
  motifDAnnulation: null,
  annuleLe: null,
  texte: 'Reçu n° 12 · Centre Les Jasmins, 71 234 567\nLe 27/11/2027',
  messages: [{ destinataire: 'Parent', telephone: '98 401 220', lien: 'https://wa.me/21698401220?text=a' }],
  emails: ['parent.miled@exemple.tn'],
  annulable: true,
  pourquoiPasAnnulable: null,
};

function configurer(): HttpTestingController {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'caisse', component: Caisse },
        { path: 'caisse/recus/:id', component: PageDuRecu },
        { path: 'eleves/:id/encaissement', component: PageDEncaissement },
        { path: 'professeurs/groupes/:id/encaissement', component: EncaissementGroupe },
        { path: 'recus/impression', component: RecuAImprimer },
        { path: 'recus/:id/impression', component: RecuAImprimer },
        { path: '**', component: PageDeTest },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  return TestBed.inject(HttpTestingController);
}

const bouton = (page: HTMLElement, texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte);

describe("l'encaissement d'un élève", () => {
  const ENCAISSABLE: Encaissable = {
    eleve: { id: 'e1', prenom: 'Adem', nom: 'Miled' },
    sommes: [
      { inscriptionId: 'i1', cle: 'Mois 3', groupe: 'Mathématiques · Groupe Samedi 10h → 12h', libelle: 'Mois 3', montant: 40, dueLe: '2027-11-13', due: true },
      { inscriptionId: null, cle: 'Assurance', groupe: 'Assurance', libelle: 'Assurance 2027-2028', montant: 20, dueLe: '2027-11-13', due: true },
      { inscriptionId: 'i1', cle: 'Mois 4', groupe: 'Mathématiques · Groupe Samedi 10h → 12h', libelle: 'Mois 4', montant: 40, dueLe: '2027-12-11', due: false },
    ],
    aUnEmail: true,
  };

  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    http = configurer();
    const harness = await RouterTestingHarness.create('/eleves/e1/encaissement');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/eleves/e1/encaissable'))).flush(ENCAISSABLE);
    await vi.waitFor(() => expect(page.querySelector('h1')?.textContent).toContain('Encaisser Adem Miled'));
  });

  afterEach(() => http.verify());

  const cases = () => [...page.querySelectorAll('input[type="checkbox"]')] as HTMLInputElement[];

  it('coche ce qui est dû aujourd’hui, et en fait le total', () => {
    expect(cases().map((c) => c.checked)).toEqual([true, true]);
    expect(page.querySelector('.total')?.textContent).toContain('60,000 TND');
    expect(page.textContent).not.toContain('Mois 4');
  });

  it("ajoute le Mois 4 d'avance, puis encaisse et ouvre le reçu", async () => {
    bouton(page, "Payer d'avance")!.click();
    await vi.waitFor(() => expect(cases()).toHaveLength(3));
    cases()[1].click();
    cases()[2].click();
    await vi.waitFor(() => expect(page.querySelector('.total')?.textContent).toContain('80,000 TND'));

    bouton(page, 'Encaisser en espèces')!.click();
    const requete = http.expectOne({ method: 'POST', url: '/api/eleves/e1/encaissement' });
    expect(requete.request.body).toEqual({
      sommes: [
        { inscriptionId: 'i1', cle: 'Mois 3' },
        { inscriptionId: 'i1', cle: 'Mois 4' },
      ],
    });
    requete.flush(RECU);

    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/caisse/recus/r1'));
    // La page du reçu s'ouvre et le charge.
    (await vi.waitFor(() => http.expectOne('/api/recus/r1'))).flush(RECU);
  });

  it('montre le refus du serveur', async () => {
    bouton(page, 'Encaisser en espèces')!.click();
    http.expectOne({ method: 'POST', url: '/api/eleves/e1/encaissement' }).flush(
      { message: "Une somme cochée n'est plus à payer : rechargez la page" },
      { status: 409, statusText: 'Conflict' },
    );

    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain("n'est plus à payer"));
  });
});

describe('la page d’un reçu', () => {
  let http: HttpTestingController;
  let page: HTMLElement;

  async function ouvrir(recu: RecuDecrit): Promise<void> {
    http = configurer();
    const harness = await RouterTestingHarness.create('/caisse/recus/r1');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/recus/r1'))).flush(recu);
    await vi.waitFor(() => expect(page.querySelector('h1')?.textContent).toContain('Reçu n° 12'));
  }

  afterEach(() => http.verify());

  /** « Imprimer », « WhatsApp » ou « Email » : les trois façons de remettre le reçu (prototype, écran 6). */
  async function onglet(libelle: string): Promise<void> {
    ([...page.querySelectorAll('[role="tab"]')].find((o) => o.textContent?.trim() === libelle) as HTMLButtonElement).click();
    await vi.waitFor(() => expect(page.querySelector('[role="tab"][aria-selected="true"]')?.textContent?.trim()).toBe(libelle));
  }

  it('montre le reçu et les trois façons de le remettre', async () => {
    await ouvrir(RECU);

    // WhatsApp d'abord : l'aperçu du message, puis un bouton par destinataire.
    expect(page.querySelector('.texte-du-message')?.textContent).toContain('Reçu n° 12 · Centre Les Jasmins');
    expect(page.querySelector('.destinataire a')?.getAttribute('href')).toBe('https://wa.me/21698401220?text=a');
    expect(page.querySelector('.souche')?.textContent).toContain('12');
    await onglet('Imprimer');
    expect(page.querySelector('a[href="/recus/r1/impression"]')?.textContent).toContain('Imprimer (A5)');
    await onglet('Email');
    expect(page.textContent).toContain('Envoyé à parent.miled@exemple.tn.');

    bouton(page, "Renvoyer l'email")!.click();
    http.expectOne({ method: 'POST', url: '/api/recus/r1/email' }).flush(RECU);
    await vi.waitFor(() => expect(page.querySelector('[role="status"]')?.textContent).toContain('Email renvoyé'));
  });

  it('dit quand aucun email n’est connu', async () => {
    await ouvrir({ ...RECU, emails: [] });
    await onglet('Email');

    expect(page.textContent).toContain('Aucun email n’est connu pour cet élève. Ajoutez-en un sur sa fiche, ou utilisez WhatsApp.');
    expect(bouton(page, "Renvoyer l'email")).toBeUndefined();
  });

  it('annule le reçu avec un motif', async () => {
    await ouvrir(RECU);

    // Le motif d'abord, comme le prototype : sans lui, rien ne part.
    bouton(page, 'Annuler le reçu n° 12')!.click();
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain("Écrivez le motif de l'annulation"));

    const motif = page.querySelector('input[placeholder="Mauvais élève sélectionné"]') as HTMLInputElement;
    motif.value = 'Mauvais élève sélectionné';
    motif.dispatchEvent(new Event('input'));
    bouton(page, 'Annuler le reçu n° 12')!.click();
    const requete = http.expectOne({ method: 'POST', url: '/api/recus/r1/annulation' });
    expect(requete.request.body).toEqual({ motif: 'Mauvais élève sélectionné' });
    requete.flush({ ...RECU, annule: true, motifDAnnulation: 'Mauvais élève sélectionné', annulable: false });

    await vi.waitFor(() => expect(page.textContent).toContain('Reçu annulé : Mauvais élève sélectionné.'));
    expect(page.querySelector('h1 .pill')?.textContent).toContain('Annulé');
  });

  it("dit pourquoi le reçu ne s'annule pas", async () => {
    await ouvrir({ ...RECU, annulable: false, pourquoiPasAnnulable: "Ce reçu date d'avant aujourd'hui : seul le propriétaire peut l'annuler" });

    expect(page.textContent).toContain("Ce reçu date d'avant aujourd'hui : seul le propriétaire peut l'annuler");
    expect(bouton(page, 'Annuler le reçu n° 12')).toBeUndefined();
  });
});

describe('le reçu à imprimer', () => {
  it('montre le centre, le numéro, l’élève, les lignes et le total ; « ANNULÉ » s’il l’est', async () => {
    const http = configurer();
    const harness = await RouterTestingHarness.create('/recus/r1/impression');
    const page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/recus/r1'))).flush({ ...RECU, annule: true, motifDAnnulation: 'Erreur' });
    await vi.waitFor(() => expect(page.querySelector('table')).not.toBeNull());

    expect(page.querySelector('.annule')?.textContent).toContain('ANNULÉ · Erreur');
    expect(page.querySelector('.entete')?.textContent).toContain('71 234 567');
    expect(page.querySelector('.numero')?.textContent).toContain('Reçu n° 12');
    expect(page.querySelector('.numero')?.textContent).toContain('Le 27/11/2027');
    expect(page.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(page.querySelector('tfoot')?.textContent).toContain('60,000 TND');
    expect(page.querySelector('.secretaire')?.textContent).toContain('Reçu par Amira Jlassi');
    // Tout est payé en entier : pas de « Reste à payer ».
    expect(page.textContent).not.toContain('Reste à payer');
    http.verify();
  });

  it('une avance : le reçu montre ce qui est payé et ce qui reste à payer', async () => {
    const http = configurer();
    const harness = await RouterTestingHarness.create('/recus/r1/impression');
    const page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/recus/r1'))).flush({
      ...RECU,
      lignes: [{ groupe: 'Mathématiques · Groupe Samedi 10h → 12h', libelle: 'Mois 2 (avance)', montant: 70, reste: 25 }],
      total: 70,
    });
    await vi.waitFor(() => expect(page.querySelector('table')).not.toBeNull());

    expect(page.querySelector('tbody')?.textContent).toContain('Reste à payer : 25,000 TND');
    const pied = [...page.querySelectorAll('tfoot tr')].map((tr) => [...tr.children].map((td) => td.textContent?.trim()));
    expect(pied).toEqual([
      ['Total payé en espèces', '70,000 TND'],
      ['Reste à payer', '25,000 TND'],
    ]);
    http.verify();
  });

  it('montre plusieurs reçus, un par page, dans l’ordre des numéros', async () => {
    const http = configurer();
    const harness = await RouterTestingHarness.create('/recus/impression?ids=r1,r2');
    const page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/recus/r1'))).flush(RECU);
    http.expectOne('/api/recus/r2').flush({ ...RECU, id: 'r2', numero: 13, eleve: { id: 'e2', prenom: 'Lina', nom: 'Gharbi' } });
    await vi.waitFor(() => expect(page.querySelectorAll('article.recu')).toHaveLength(2));

    expect([...page.querySelectorAll('.numero b')].map((b) => b.textContent?.trim())).toEqual(['Reçu n° 12', 'Reçu n° 13']);
    expect(page.querySelectorAll('.eleve')[1].textContent).toContain('Lina Gharbi');
    http.verify();
  });
});

describe("l'encaissement groupé", () => {
  const ENCAISSEMENT: EncaissementDuGroupe = {
    groupe: { id: 'g1', nom: 'Groupe Samedi 10h → 12h', matiere: 'Mathématiques', type: 'Etude' },
    aPayer: [
      {
        eleveId: 'e2',
        prenom: 'Lina',
        nom: 'Gharbi',
        couleur: 'Rouge',
        sommes: [{ inscriptionId: 'i2', cle: 'Mois 3', libelle: 'Mois 3', montant: 40 }],
        total: 40,
      },
      {
        eleveId: 'e1',
        prenom: 'Adem',
        nom: 'Miled',
        couleur: 'Orange',
        sommes: [
          { inscriptionId: 'i1', cle: 'Mois 3', libelle: 'Mois 3', montant: 40 },
          { inscriptionId: null, cle: 'Assurance', libelle: 'Assurance 2027-2028', montant: 20 },
        ],
        total: 60,
      },
    ],
    aJour: [{ id: 'e3', prenom: 'Sami', nom: 'Chaabane' }],
  };

  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    http = configurer();
    const harness = await RouterTestingHarness.create('/professeurs/groupes/g1/encaissement');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/groupes/g1/encaissement'))).flush(ENCAISSEMENT);
    await vi.waitFor(() => expect(page.querySelector('h1')?.textContent).toContain('Paiement du groupe'));
  });

  afterEach(() => http.verify());

  const cases = () => [...page.querySelectorAll('input[type="checkbox"]')] as HTMLInputElement[];
  const encaisser = () => [...page.querySelectorAll('button')].find((b) => b.textContent?.includes('Encaisser et créer'))!;

  it('liste ce que chacun doit, avec sa couleur, et ceux qui sont déjà à jour', () => {
    expect(page.textContent).toContain('Mois 3 + Assurance 2027-2028');
    expect(page.textContent).toContain('60,000 TND');
    // La couleur se lit dans le point (prototype, écran 5), pour les lecteurs d'écran.
    expect(page.querySelector('.point.Rouge')?.getAttribute('aria-label')).toBe('Rouge');
    expect(page.textContent).toContain('Sami Chaabane');
    expect(cases().map((c) => c.checked)).toEqual([false, false]);
    expect(encaisser().disabled).toBe(true);
  });

  it('« Tout cocher » coche tout et fait le total ; « Tout décocher » défait', async () => {
    bouton(page, 'Tout cocher')!.click();
    await vi.waitFor(() => expect(cases().map((c) => c.checked)).toEqual([true, true]));
    expect(page.querySelector('.pied')?.textContent).toContain('100,000 TND');
    expect(encaisser().textContent).toContain('Encaisser et créer 2 reçus');

    bouton(page, 'Tout décocher')!.click();
    await vi.waitFor(() => expect(cases().map((c) => c.checked)).toEqual([false, false]));
  });

  it('envoie chaque élève coché avec ses sommes, puis montre les reçus créés', async () => {
    cases()[1].click();
    await vi.waitFor(() => expect(encaisser().disabled).toBe(false));

    encaisser().click();
    const requete = http.expectOne({ method: 'POST', url: '/api/groupes/g1/encaissement' });
    expect(requete.request.body).toEqual({
      eleves: [
        {
          eleveId: 'e1',
          sommes: [
            { inscriptionId: 'i1', cle: 'Mois 3' },
            { inscriptionId: null, cle: 'Assurance' },
          ],
        },
      ],
    });
    requete.flush({ recus: [RECU, { ...RECU, id: 'r2', numero: 13, emails: [] }] });

    await vi.waitFor(() => expect(page.querySelector('h1')?.textContent).toContain('2 reçus créés'));
    expect(page.textContent).toContain('n° 12');
    expect(page.textContent).toContain('Email envoyé');
    expect(page.textContent).toContain("Pas d'email");
    const imprimer = [...page.querySelectorAll('a')].find((a) => a.textContent?.includes('Imprimer les 2 reçus'));
    expect(imprimer?.getAttribute('href')).toBe('/recus/impression?ids=r1,r2');
  });

  it('montre le refus du serveur', async () => {
    cases()[0].click();
    await vi.waitFor(() => expect(encaisser().disabled).toBe(false));
    encaisser().click();
    http.expectOne({ method: 'POST', url: '/api/groupes/g1/encaissement' }).flush(
      { message: 'Un élève coché a payé entre-temps : rechargez la page' },
      { status: 409, statusText: 'Conflict' },
    );

    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('a payé entre-temps'));
  });
});

const OUVERTE: CaisseOuverte = { jour: '2027-11-27', rang: 1, encaisse: 845, sorties: 0, attendu: 845, recus: 12, oubliee: false, annules: 0 };

const CLOTURE: Cloture = {
  id: 'c1',
  jour: '2027-11-27',
  rang: 1,
  compteId: 'a1',
  secretaire: 'Amira Jlassi',
  encaisse: 845,
  sorties: 0,
  attendu: 845,
  compte: 840,
  ecart: -5,
  clotureeLe: '2027-11-27T17:47:00+00:00',
  argentRecu: false,
  argentRecuLe: null,
};

/** La carte des reçus du jour (prototype, écran 7 : à gauche, sous les chiffres). */
const carteDesRecus = (page: HTMLElement) => page.querySelector('.recus-du-jour') as HTMLElement;

describe('la page « Caisse »', () => {
  it('montre les reçus du jour, puis cherche par élève', async () => {
    const http = configurer();
    const harness = await RouterTestingHarness.create('/caisse');
    const page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/caisse'))).flush({ ouvertes: [OUVERTE], clotures: [] });
    const premiere = await vi.waitFor(() => http.expectOne((r) => r.url === '/api/recus'));
    expect(premiere.request.params.get('jour')).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    premiere.flush([
      { id: 'r2', numero: 13, jour: '2027-11-27', creeLe: '2027-11-27T09:30:00+00:00', eleve: 'Lina Gharbi', total: 40, secretaire: 'Amira Jlassi', annule: true },
      { id: 'r1', numero: 12, jour: '2027-11-27', creeLe: '2027-11-27T09:00:00+00:00', eleve: 'Adem Miled', total: 60, secretaire: 'Amira Jlassi', annule: false },
    ]);
    // Les sorties de caisse du jour : les achats du mois, filtrés.
    http.expectOne((r) => r.url === '/api/achats').flush({ achats: [], totalMateriel: 0, totalService: 0, total: 0 });
    await vi.waitFor(() => expect(page.querySelectorAll('a.recu')).toHaveLength(2));

    const recus = carteDesRecus(page);
    expect(recus.querySelector('h2')?.textContent).toContain('Reçus du jour');
    // Un reçu annulé ne compte pas dans le total.
    expect(recus.querySelector('.compte')?.textContent).toContain('2 reçus · 60,000 TND');
    expect(page.querySelector('a.recu')?.textContent).toContain('Annulé');
    expect(page.querySelector('a.recu')?.getAttribute('href')).toBe('/caisse/recus/r2');
    // Pas de contrôle des clôtures pour une secrétaire.
    expect(page.textContent).not.toContain('Contrôle des clôtures');

    const eleve = recus.querySelectorAll('.filtres input')[1] as HTMLInputElement;
    eleve.value = 'miled';
    bouton(page, 'Chercher')!.click();
    const recherche = http.expectOne((r) => r.url === '/api/recus');
    expect(recherche.request.params.get('eleve')).toBe('miled');
    recherche.flush([]);
    await vi.waitFor(() => expect(page.textContent).toContain('Aucun reçu.'));
    expect(carteDesRecus(page).querySelector('h2')?.textContent).toContain('Reçus trouvés');
    http.verify();
  });
});

describe('« Ma caisse » et sa clôture du soir', () => {
  let http: HttpTestingController;
  let page: HTMLElement;

  async function ouvrir(caisse: MaCaisse): Promise<void> {
    http = configurer();
    const harness = await RouterTestingHarness.create('/caisse');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/caisse'))).flush(caisse);
    http.expectOne((r) => r.url === '/api/recus').flush([]);
    http.expectOne((r) => r.url === '/api/achats').flush({ achats: [], totalMateriel: 0, totalService: 0, total: 0 });
    await vi.waitFor(() => expect(page.textContent).toContain('Espèces attendues'));
  }

  afterEach(() => http.verify());

  const compte = () => page.querySelector('sh-ma-caisse input') as HTMLInputElement;

  function saisir(valeur: string): void {
    compte().value = valeur;
    compte().dispatchEvent(new Event('input'));
  }

  it("montre l'encaissé, les sorties et l'attendu, et l'écart en direct", async () => {
    await ouvrir({ ouvertes: [OUVERTE], clotures: [] });

    expect(page.querySelector('h1')?.textContent).toContain('Ma caisse');
    expect(page.querySelector('sh-ma-caisse h2')?.textContent).toContain('Clôture du soir');
    expect(page.querySelector('.chiffres')?.textContent).toContain('845,000 TND');
    expect(page.querySelector('.chiffres')?.textContent).toContain('12 reçus');
    expect(page.querySelector('.ecart')?.textContent).toContain('Saisissez le montant compté');

    saisir('840');
    await vi.waitFor(() => expect(page.querySelector('.ecart')?.textContent).toContain('Écart : −5,000 TND'));
    saisir('845,000');
    await vi.waitFor(() => expect(page.querySelector('.ecart')?.textContent).toContain("Pas d'écart"));
  });

  it('clôture avec le montant compté, puis montre la caisse clôturée', async () => {
    await ouvrir({ ouvertes: [OUVERTE], clotures: [] });
    saisir('840');

    bouton(page, 'Clôturer ma caisse')!.click();
    const requete = http.expectOne({ method: 'POST', url: '/api/caisse/cloture' });
    expect(requete.request.body).toEqual({ jour: '2027-11-27', compte: 840 });
    requete.flush(CLOTURE);
    (await vi.waitFor(() => http.expectOne('/api/caisse'))).flush({
      ouvertes: [{ ...OUVERTE, rang: 2, encaisse: 0, attendu: 0, recus: 0 }],
      clotures: [CLOTURE],
    });

    await vi.waitFor(() => expect(page.textContent).toContain('Clôturée'));
    expect(page.textContent).toContain('−5,000 TND');
    expect(page.textContent).toContain('Rien à clôturer pour le moment');
  });

  it("montre en tête une caisse oubliée, à clôturer d'abord", async () => {
    await ouvrir({ ouvertes: [{ ...OUVERTE, jour: '2027-11-26', oubliee: true }, OUVERTE], clotures: [] });

    expect(page.querySelector('sh-ma-caisse h2')?.textContent).toContain('Caisse du 26/11');
    expect(page.querySelector('sh-ma-caisse .note.warn')?.textContent).toContain("n'est pas clôturée");
  });

  it("propose de clôturer une caisse du jour qui n'a que des reçus annulés", async () => {
    await ouvrir({ ouvertes: [{ ...OUVERTE, encaisse: 0, attendu: 0, recus: 0, annules: 1 }], clotures: [] });

    expect(bouton(page, 'Clôturer ma caisse')).toBeTruthy();
    expect(page.textContent).not.toContain('Rien à clôturer pour le moment');
  });
});

describe('le contrôle des clôtures, pour le propriétaire', () => {
  it("montre le tableau, l'écart négatif en rouge, et coche « argent reçu »", async () => {
    const http = configurer();
    TestBed.inject(Session).connaitre({ prenom: 'Hedi', nom: 'Ben Salah', role: 'Proprietaire' } as Moi);
    const harness = await RouterTestingHarness.create('/caisse');
    const page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/caisse'))).flush({ ouvertes: [OUVERTE], clotures: [] });
    const controle: ControleDesClotures = {
      clotures: [CLOTURE],
      aRecuperer: 840,
      nonCloturees: [{ compteId: 's1', secretaire: 'Sonia Mabrouk', jour: '2027-11-26', attendu: 120, recus: 3 }],
      comptes: [{ id: 'a1', nom: 'Amira Jlassi' }],
    };
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/caisse/clotures'))).flush(controle);
    http.expectOne((r) => r.url === '/api/recus').flush([]);
    http.expectOne((r) => r.url === '/api/achats').flush({ achats: [], totalMateriel: 0, totalService: 0, total: 0 });
    await vi.waitFor(() => expect(page.querySelector('td.bad')).not.toBeNull());

    expect(page.textContent).toContain('À récupérer : 840,000 TND');
    expect(page.textContent).toContain('Sonia Mabrouk le 26/11 (120,000 TND)');
    expect(page.querySelector('td.bad')?.textContent).toContain('−5,000');

    (page.querySelector('sh-controle-des-clotures tbody input[type="checkbox"]') as HTMLInputElement).click();
    const requete = http.expectOne({ method: 'PUT', url: '/api/caisse/clotures/c1/argent-recu' });
    expect(requete.request.body).toEqual({ recu: true });
    requete.flush({ ...CLOTURE, argentRecu: true });
    (await vi.waitFor(() => http.expectOne((r) => r.url === '/api/caisse/clotures'))).flush({
      ...controle,
      clotures: [{ ...CLOTURE, argentRecu: true }],
      aRecuperer: 0,
    });
    await vi.waitFor(() => expect(page.textContent).toContain('À récupérer : 0,000 TND'));

    const secretaire = page.querySelector('sh-controle-des-clotures select') as HTMLSelectElement;
    secretaire.value = 'a1';
    secretaire.dispatchEvent(new Event('change'));
    const filtree = http.expectOne((r) => r.url === '/api/caisse/clotures');
    expect(filtree.request.params.get('compteId')).toBe('a1');
    filtree.flush(controle);
    http.verify();
  });
});
