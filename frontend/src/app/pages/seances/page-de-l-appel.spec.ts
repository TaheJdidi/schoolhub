import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { AppelDeLaSeance, EleveALAppel } from './appel';
import { PageDeLAppel } from './page-de-l-appel';

describe("la page de l'appel", () => {
  const eleve = (id: string, prenom: string, nom: string, autres: Partial<EleveALAppel> = {}): EleveALAppel => ({
    inscriptionId: id,
    eleveId: `e-${id}`,
    prenom,
    nom,
    absent: false,
    prevenuLe: null,
    messages: [],
    ...autres,
  });

  const APPEL: AppelDeLaSeance = {
    seanceId: 's1',
    date: '2027-11-27',
    debut: '10:00',
    fin: '12:00',
    groupe: { id: 'g1', nom: 'Groupe Samedi 10h → 12h', type: 'Etude', matiere: 'Mathématiques' },
    professeur: 'Karim Ben Salah',
    ouvert: true,
    appelFait: false,
    eleves: [eleve('i1', 'Lina', 'Gharbi'), eleve('i2', 'Adem', 'Miled')],
  };

  const MESSAGES = [
    { destinataire: 'Parent', telephone: '98 401 220', lien: 'https://wa.me/21698401220?text=a' },
    { destinataire: 'Adem', telephone: '22 345 678', lien: 'https://wa.me/21622345678?text=a' },
  ];

  let http: HttpTestingController;
  let page: HTMLElement;

  async function ouvrir(appel: AppelDeLaSeance | null): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'professeurs/groupes/:id/appel/:seance', component: PageDeLAppel },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/professeurs/groupes/g1/appel/s1');
    page = harness.routeNativeElement as HTMLElement;
    const requete = await vi.waitFor(() => http.expectOne('/api/seances/s1/appel'));
    if (appel) requete.flush(appel);
    else requete.flush({ message: 'Séance introuvable' }, { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => expect(page.querySelector('h1')).not.toBeNull());
  }

  afterEach(() => http.verify());

  const bouton = (texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte);
  const eleveA = (prenom: string) => [...page.querySelectorAll('button.eleve')].find((b) => b.textContent?.includes(prenom)) as HTMLButtonElement;

  it('montre tous les élèves présents', async () => {
    await ouvrir(APPEL);

    expect(page.querySelector('h1')?.textContent).toContain('Appel du samedi 27/11/2027');
    expect(page.querySelector('.sub')?.textContent).toContain('Mathématiques · Karim Ben Salah · 10h → 12h');
    expect([...page.querySelectorAll('button.eleve')].map((b) => b.textContent?.replace(/\s+/g, ' ').trim())).toEqual([
      'Lina GharbiPrésent',
      'Adem MiledPrésent',
    ]);
    expect(page.textContent).toContain('2 présents · 0 absent');
  });

  it("marque un absent d'un toucher, le remet présent d'un autre, puis enregistre", async () => {
    await ouvrir(APPEL);

    eleveA('Lina').click();
    eleveA('Adem').click();
    await vi.waitFor(() => expect(eleveA('Lina').getAttribute('aria-pressed')).toBe('true'));
    eleveA('Lina').click();
    await vi.waitFor(() => expect(eleveA('Lina').getAttribute('aria-pressed')).toBe('false'));
    expect(page.textContent).toContain('1 présents · 1 absent');

    bouton("Enregistrer l'appel")!.click();
    const requete = http.expectOne({ method: 'PUT', url: '/api/seances/s1/appel' });
    expect(requete.request.body).toEqual({ absents: ['i2'] });
    requete.flush({ ...APPEL, appelFait: true, eleves: [APPEL.eleves[0], eleve('i2', 'Adem', 'Miled', { absent: true, messages: MESSAGES })] });

    await vi.waitFor(() => expect(page.querySelector('[role="status"]')?.textContent).toContain('Appel enregistré'));
    expect(page.textContent).toContain('Prévenir les parents des absents');
    expect(bouton("Corriger l'appel")).toBeDefined();
    expect([...page.querySelectorAll('.destinataire')].map((d) => d.textContent?.replace(/\s+/g, ' ').trim())).toEqual([
      'Au parent · 98 401 220Ouvrir WhatsApp',
      'À Adem · 22 345 678Ouvrir WhatsApp',
    ]);
  });

  it('marque un message ouvert', async () => {
    await ouvrir({ ...APPEL, appelFait: true, eleves: [eleve('i2', 'Adem', 'Miled', { absent: true, messages: MESSAGES })] });

    const lien = page.querySelector('.destinataire a') as HTMLAnchorElement;
    lien.addEventListener('click', (evenement) => evenement.preventDefault());
    lien.click();
    http.expectOne({ method: 'POST', url: '/api/seances/s1/absences/i2/prevenu' }).flush(null, { status: 204, statusText: 'No Content' });

    await vi.waitFor(() => expect(page.querySelector('.absent-a-prevenir .pill')?.textContent).toContain('Message ouvert'));
  });

  it('corrige un appel déjà fait', async () => {
    await ouvrir({ ...APPEL, appelFait: true, eleves: [eleve('i1', 'Lina', 'Gharbi'), eleve('i2', 'Adem', 'Miled', { absent: true })] });

    expect(page.querySelector('h2')?.textContent).toContain('Appel fait');
    eleveA('Adem').click();
    await vi.waitFor(() => expect(eleveA('Adem').getAttribute('aria-pressed')).toBe('false'));
    bouton("Corriger l'appel")!.click();
    const requete = http.expectOne({ method: 'PUT', url: '/api/seances/s1/appel' });
    expect(requete.request.body).toEqual({ absents: [] });
    requete.flush({ ...APPEL, appelFait: true });

    await vi.waitFor(() => expect(page.querySelector('[role="status"]')?.textContent).toContain('Appel corrigé'));
  });

  it("ne s'ouvre pas avant le jour de la séance", async () => {
    await ouvrir({ ...APPEL, ouvert: false });

    expect(page.textContent).toContain("L'appel s'ouvre le jour de la séance.");
    expect(eleveA('Lina').disabled).toBe(true);
    expect(bouton("Enregistrer l'appel")).toBeUndefined();
  });

  it('montre le refus du serveur', async () => {
    await ouvrir(APPEL);

    bouton("Enregistrer l'appel")!.click();
    http.expectOne({ method: 'PUT', url: '/api/seances/s1/appel' }).flush(
      { message: "L'appel s'ouvre le jour de la séance" },
      { status: 409, statusText: 'Conflict' },
    );

    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain("L'appel s'ouvre le jour de la séance"));
  });

  it('annule la séance avec un motif, puis montre les messages', async () => {
    await ouvrir({ ...APPEL, date: '2027-12-04', ouvert: false });

    bouton('Annuler cette séance')!.click();
    await vi.waitFor(() => expect(bouton("Confirmer l'annulation")).toBeDefined());
    bouton("Confirmer l'annulation")!.click();
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain("Écrivez le motif de l'annulation"));

    const motif = page.querySelector('input[placeholder="Professeur absent"]') as HTMLInputElement;
    motif.value = 'Professeur absent';
    motif.dispatchEvent(new Event('input'));
    bouton("Confirmer l'annulation")!.click();
    const requete = http.expectOne({ method: 'POST', url: '/api/seances/s1/annulation' });
    expect(requete.request.body).toEqual({ motif: 'Professeur absent' });
    requete.flush({ texte: 'Bonjour, la séance de mathématiques du samedi 04/12/2027, de 10h à 12h, est annulée. Centre Les Jasmins.', eleves: [] });
    (await vi.waitFor(() => http.expectOne('/api/seances/s1/appel'))).flush({ ...APPEL, ouvert: false, annulee: true, motifDAnnulation: 'Professeur absent' });

    await vi.waitFor(() => expect(page.textContent).toContain('Cette séance est annulée : Professeur absent.'));
    expect(page.querySelector('.texte-du-message')?.textContent).toContain('est annulée');
    expect(bouton('Remplacer cette séance')).toBeDefined();
  });

  it('déplace la séance, avec ses heures proposées', async () => {
    await ouvrir({ ...APPEL, date: '2027-12-04', ouvert: false });

    bouton('Déplacer cette séance')!.click();
    await vi.waitFor(() => expect(page.querySelector('input[type="date"]')).not.toBeNull());
    const date = page.querySelector('input[type="date"]') as HTMLInputElement;
    date.value = '2027-12-07';
    date.dispatchEvent(new Event('input'));
    bouton('Déplacer la séance')!.click();
    const requete = http.expectOne({ method: 'POST', url: '/api/seances/s1/deplacement' });
    expect(requete.request.body).toEqual({ date: '2027-12-07', debut: '10:00', fin: '12:00' });
    requete.flush({ texte: 'Bonjour, la séance est déplacée.', eleves: [] });
    (await vi.waitFor(() => http.expectOne('/api/seances/s1/appel'))).flush({ ...APPEL, ouvert: false, annulee: true, motifDAnnulation: 'Déplacée au 07/12/2027' });

    await vi.waitFor(() => expect(page.textContent).toContain('Cette séance est annulée : Déplacée au 07/12/2027.'));
  });

  it("ne propose ni d'annuler ni de déplacer une séance qui a son appel", async () => {
    await ouvrir({ ...APPEL, appelFait: true });

    expect(bouton('Annuler cette séance')).toBeUndefined();
    expect(bouton('Déplacer cette séance')).toBeUndefined();
  });
  it('montre à part les élèves en rattrapage, et la séance rattrapée ailleurs', async () => {
    await ouvrir({
      ...APPEL,
      eleves: [eleve('i2', 'Adem', 'Miled', { rattrapeeLe: '2027-11-30' })],
      rattrapages: [
        {
          rattrapageId: 'r1',
          inscriptionId: 'i9',
          eleveId: 'e9',
          prenom: 'Lina',
          nom: 'Gharbi',
          groupeDOrigine: 'Groupe Mardi 17h → 19h',
          seanceRattrapee: '2027-11-30',
          absent: false,
          prevenuLe: null,
          messages: [],
        },
      ],
    });

    expect(eleveA('Adem').textContent).toContain('Rattrapée le 30/11');
    expect(page.textContent).toContain('En rattrapage');
    expect(eleveA('Lina').textContent).toContain("Paie dans son groupe d'origine · Groupe Mardi 17h → 19h, rattrape le 30/11");
    expect(page.textContent).toContain('2 présents · 0 absent');

    eleveA('Lina').click();
    await vi.waitFor(() => expect(eleveA('Lina').getAttribute('aria-pressed')).toBe('true'));
    bouton("Enregistrer l'appel")!.click();
    const requete = http.expectOne({ method: 'PUT', url: '/api/seances/s1/appel' });
    expect(requete.request.body).toEqual({ absents: ['i9'] });
    requete.flush({ ...APPEL, appelFait: true });
  });

  it('ajoute un élève en rattrapage', async () => {
    await ouvrir(APPEL);

    bouton('Ajouter un élève en rattrapage')!.click();
    http.expectOne('/api/seances/s1/rattrapages/possibles').flush([
      {
        inscriptionId: 'i9',
        eleveId: 'e9',
        prenom: 'Lina',
        nom: 'Gharbi',
        groupe: { id: 'g2', nom: 'Groupe Mardi 17h → 19h' },
        seances: [{ id: 's9', date: '2027-11-30', debut: '17:00', fin: '19:00' }],
      },
    ]);
    await vi.waitFor(() => expect(page.querySelector('select[aria-label="Élève en rattrapage"]')).not.toBeNull());
    const choisir = (libelle: string, valeur: string) => {
      const liste = page.querySelector(`select[aria-label="${libelle}"]`) as HTMLSelectElement;
      liste.value = valeur;
      liste.dispatchEvent(new Event('change'));
    };
    choisir('Élève en rattrapage', 'i9');
    await vi.waitFor(() => expect(page.querySelector('select[aria-label="Séance rattrapée"]')).not.toBeNull());
    expect(page.querySelector('select[aria-label="Séance rattrapée"]')?.textContent).toContain('mardi 30/11/2027 · 17h');
    choisir('Séance rattrapée', 's9');

    bouton('Ajouter en rattrapage')!.click();
    const requete = http.expectOne({ method: 'POST', url: '/api/seances/s1/rattrapages' });
    expect(requete.request.body).toEqual({ inscriptionId: 'i9', seanceRattrapeeId: 's9' });
    requete.flush({
      ...APPEL,
      rattrapages: [
        { rattrapageId: 'r1', inscriptionId: 'i9', eleveId: 'e9', prenom: 'Lina', nom: 'Gharbi', groupeDOrigine: 'Groupe Mardi 17h → 19h',
          seanceRattrapee: '2027-11-30', absent: false, prevenuLe: null, messages: [] },
      ],
    });

    await vi.waitFor(() => expect(page.textContent).toContain('En rattrapage'));
    expect(bouton('Ajouter un élève en rattrapage')).toBeDefined();
  });

  it('dit quand aucun élève ne peut venir en rattrapage', async () => {
    await ouvrir(APPEL);

    bouton('Ajouter un élève en rattrapage')!.click();
    http.expectOne('/api/seances/s1/rattrapages/possibles').flush([]);

    await vi.waitFor(() => expect(page.textContent).toContain('Aucun élève ne peut venir en rattrapage à cette séance.'));
  });
  it("dit qu'une séance inconnue n'existe pas", async () => {
    await ouvrir(null);

    await vi.waitFor(() => expect(page.textContent).toContain("Cette séance n'existe pas."));
  });
});
