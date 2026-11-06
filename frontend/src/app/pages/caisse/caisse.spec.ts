import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { Caisse } from './caisse';
import { PageDEncaissement } from './page-d-encaissement';
import { PageDuRecu } from './page-du-recu';
import { RecuAImprimer } from './recu-a-imprimer';
import { Encaissable, RecuDecrit } from './recu';

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

  it('montre le reçu et les trois façons de le remettre', async () => {
    await ouvrir(RECU);

    expect(page.querySelector('.texte-du-message')?.textContent).toContain('Reçu n° 12 · Centre Les Jasmins');
    expect(page.querySelector('a[href="/recus/r1/impression"]')?.textContent).toContain('Imprimer (A5)');
    expect(page.querySelector('.destinataire a')?.getAttribute('href')).toBe('https://wa.me/21698401220?text=a');
    expect(page.textContent).toContain('Envoyé à parent.miled@exemple.tn.');

    bouton(page, "Renvoyer l'email")!.click();
    http.expectOne({ method: 'POST', url: '/api/recus/r1/email' }).flush(RECU);
    await vi.waitFor(() => expect(page.querySelector('[role="status"]')?.textContent).toContain('Email renvoyé'));
  });

  it('dit quand aucun email n’est connu', async () => {
    await ouvrir({ ...RECU, emails: [] });

    expect(page.textContent).toContain('Aucun email n’est connu pour cet élève. Ajoutez-en un sur sa fiche, ou utilisez WhatsApp.');
    expect(bouton(page, "Renvoyer l'email")).toBeUndefined();
  });

  it('annule le reçu avec un motif', async () => {
    await ouvrir(RECU);

    bouton(page, 'Annuler ce reçu')!.click();
    await vi.waitFor(() => expect(bouton(page, "Confirmer l'annulation")).toBeDefined());
    bouton(page, "Confirmer l'annulation")!.click();
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain("Écrivez le motif de l'annulation"));

    const motif = page.querySelector('input[placeholder="Mauvais élève sélectionné"]') as HTMLInputElement;
    motif.value = 'Mauvais élève sélectionné';
    motif.dispatchEvent(new Event('input'));
    bouton(page, "Confirmer l'annulation")!.click();
    const requete = http.expectOne({ method: 'POST', url: '/api/recus/r1/annulation' });
    expect(requete.request.body).toEqual({ motif: 'Mauvais élève sélectionné' });
    requete.flush({ ...RECU, annule: true, motifDAnnulation: 'Mauvais élève sélectionné', annulable: false });

    await vi.waitFor(() => expect(page.textContent).toContain('Reçu annulé : Mauvais élève sélectionné.'));
    expect(page.querySelector('h1 .pill')?.textContent).toContain('Annulé');
  });

  it("dit pourquoi le reçu ne s'annule pas", async () => {
    await ouvrir({ ...RECU, annulable: false, pourquoiPasAnnulable: "Ce reçu date d'avant aujourd'hui : seul le propriétaire peut l'annuler" });

    expect(page.textContent).toContain("Ce reçu date d'avant aujourd'hui : seul le propriétaire peut l'annuler");
    expect(bouton(page, 'Annuler ce reçu')).toBeUndefined();
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
    http.verify();
  });
});

describe('la page « Caisse »', () => {
  it('montre les reçus du jour, puis cherche par élève', async () => {
    const http = configurer();
    const harness = await RouterTestingHarness.create('/caisse');
    const page = harness.routeNativeElement as HTMLElement;
    const premiere = await vi.waitFor(() => http.expectOne((r) => r.url === '/api/recus'));
    expect(premiere.request.params.get('jour')).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    premiere.flush([
      { id: 'r2', numero: 13, jour: '2027-11-27', creeLe: '2027-11-27T09:30:00+00:00', eleve: 'Lina Gharbi', total: 40, secretaire: 'Amira Jlassi', annule: true },
      { id: 'r1', numero: 12, jour: '2027-11-27', creeLe: '2027-11-27T09:00:00+00:00', eleve: 'Adem Miled', total: 60, secretaire: 'Amira Jlassi', annule: false },
    ]);
    await vi.waitFor(() => expect(page.querySelectorAll('a.recu')).toHaveLength(2));

    expect(page.querySelector('h2')?.textContent).toContain('Reçus du jour');
    // Un reçu annulé ne compte pas dans le total.
    expect(page.querySelector('.compte')?.textContent).toContain('2 reçus · 60,000 TND');
    expect(page.querySelector('a.recu')?.textContent).toContain('Annulé');
    expect(page.querySelector('a.recu')?.getAttribute('href')).toBe('/caisse/recus/r2');

    const eleve = page.querySelectorAll('.filtres input')[1] as HTMLInputElement;
    eleve.value = 'miled';
    bouton(page, 'Chercher')!.click();
    const recherche = http.expectOne((r) => r.url === '/api/recus');
    expect(recherche.request.params.get('eleve')).toBe('miled');
    recherche.flush([]);
    await vi.waitFor(() => expect(page.textContent).toContain('Aucun reçu.'));
    expect(page.querySelector('h2')?.textContent).toContain('Reçus trouvés');
    http.verify();
  });
});
