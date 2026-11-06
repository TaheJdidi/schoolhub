import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { saisir } from '../../test/formulaires';
import { PageDeTest } from '../../test/page-de-test';
import { DemandeEnregistree, DetailDeLaDemande, GroupeProposeALaDemande } from './demande';
import { PageDeLaDemande } from './page-de-la-demande';

describe("la page d'une demande d'inscription", () => {
  const NIVEAUX = [
    ...Array.from({ length: 8 }, (_, i) => ({ id: `n${i + 1}`, nom: `${i + 1}ᵉ année de base`, dOffice: true })),
    { id: 'n9', nom: '9ᵉ année de base', dOffice: true },
  ];

  const groupe = (id: string, autres: Partial<GroupeProposeALaDemande>): GroupeProposeALaDemande => ({
    id,
    nom: 'Groupe Samedi 10h → 12h',
    type: 'Etude',
    professeur: 'Karim Ben Salah',
    creneaux: [{ jour: 'Samedi', debut: '10:00', fin: '12:00' }],
    eleves: 12,
    capacite: 15,
    modes: [],
    nombreDeTranches: 0,
    premiereSeance: { date: '2027-11-27', debut: '10:00:00' },
    amis: [],
    ...autres,
  });

  const MATHS = groupe('g1', { amis: ['Sami Chaabane'] });
  const MATHS_MARDI = groupe('g2', { nom: 'Groupe Mardi 17h → 19h', professeur: 'Lina Gharbi', eleves: 15 });
  const REVISION = groupe('g3', { type: 'Revision', modes: ['ALaSeance', 'EnTotalite'], capacite: null, eleves: 3 });

  const ADEM: DetailDeLaDemande = {
    id: 'd1',
    prenom: 'Adem',
    nom: 'Miled',
    niveau: { id: 'n9', nom: '9ᵉ année de base' },
    majeur: false,
    telephoneParent: '98 401 220',
    emailParent: null,
    telephoneEleve: '22 345 678',
    emailEleve: 'adem@exemple.tn',
    etablissement: null,
    amis: 'Sami Chaabane',
    recueLe: '2027-11-27T09:00:00+00:00',
    etat: 'ATraiter',
    motif: null,
    traiteeLe: null,
    eleveId: null,
    fichesSemblables: [],
    autreDemandeSemblable: false,
    matieres: [
      { matiere: 'Mathématiques', groupePrefere: 'g1', groupes: [MATHS, MATHS_MARDI] },
      { matiere: 'Physique', groupePrefere: 'g3', groupes: [REVISION] },
    ],
  };

  const ENREGISTREE: DemandeEnregistree = {
    eleve: { id: 'e1', prenom: 'Adem', nom: 'Miled' },
    inscriptions: [],
    avertissement: null,
    texte: "Bonjour, l'inscription d'Adem Miled est enregistrée.",
    messages: [
      { destinataire: 'Parent', telephone: '98 401 220', lien: 'https://wa.me/21698401220?text=x' },
      { destinataire: 'Adem', telephone: '22 345 678', lien: 'https://wa.me/21622345678?text=x' },
    ],
  };

  let http: HttpTestingController;
  let page: HTMLElement;

  async function ouvrir(demande: DetailDeLaDemande | null): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'demandes/:id', component: PageDeLaDemande },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/demandes/d1');
    page = harness.routeNativeElement as HTMLElement;
    const requete = await vi.waitFor(() => http.expectOne('/api/demandes/d1'));
    if (demande) requete.flush(demande);
    else requete.flush({ message: 'Demande introuvable' }, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/niveaux').flush(NIVEAUX);
    await vi.waitFor(() => expect(page.querySelector('h1')?.textContent).not.toContain("Demande d'inscription"));
  }

  // Un enregistrement relit le nombre de demandes à traiter, pour le menu.
  afterEach(() => {
    http.match('/api/demandes/nombre').forEach((r) => r.flush({ aTraiter: 0 }));
    http.verify();
  });

  const bouton = (texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte);
  const radio = (texte: string) =>
    [...page.querySelectorAll('label.option, label.case')].find((l) => l.textContent?.includes(texte))?.querySelector('input') as HTMLInputElement;
  const cocher = (texte: string) => {
    const choix = radio(texte);
    choix.checked = true;
    choix.dispatchEvent(new Event('change'));
  };
  const enregistrement = (): TestRequest => http.expectOne({ method: 'POST', url: '/api/demandes/d1/inscription' });

  it('pré-remplit la fiche et propose le groupe préféré, avec les amis déjà inscrits', async () => {
    await ouvrir(ADEM);

    expect(page.querySelector('h1')?.textContent).toContain('Adem Miled');
    expect((page.querySelector('input[formcontrolname="prenom"]') as HTMLInputElement).value).toBe('Adem');
    expect((page.querySelector('input[formcontrolname="telephoneParent"]') as HTMLInputElement).value).toBe('98 401 220');
    expect(radio('Groupe Samedi 10h → 12h · préféré').checked).toBe(true);
    expect(page.textContent).toContain('Karim Ben Salah · Samedi 10h → 12h · Étude · 12 élèves sur 15');
    expect(page.textContent).toContain('Première séance : samedi 27/11/2027 à 10h');
    expect(page.textContent).toContain('Ami dans ce groupe : Sami Chaabane');
    expect(page.querySelector('.pill.warn')?.textContent).toContain('Complet');
    expect(page.textContent).not.toContain('Doublon');
  });

  it('enregistre la demande avec la fiche vérifiée et les groupes choisis', async () => {
    await ouvrir(ADEM);
    saisir(page, 'input[formcontrolname="etablissement"]', 'Collège Ibn Khaldoun');
    // La secrétaire n'inscrit pas en Physique : seulement en Mathématiques.
    const physique = page.querySelectorAll('fieldset.matiere')[1];
    const pasDePhysique = [...physique.querySelectorAll('label.option')].at(-1)!.querySelector('input')!;
    pasDePhysique.checked = true;
    pasDePhysique.dispatchEvent(new Event('change'));

    bouton("Enregistrer l'élève")!.click();
    const requete = enregistrement();

    expect(requete.request.body).toEqual({
      eleveId: null,
      fiche: {
        prenom: 'Adem',
        nom: 'Miled',
        niveauId: 'n9',
        majeur: false,
        telephoneEleve: '22 345 678',
        emailEleve: 'adem@exemple.tn',
        telephoneParent: '98 401 220',
        emailParent: '',
        etablissement: 'Collège Ibn Khaldoun',
      },
      groupes: [{ groupeId: 'g1', mode: null }],
    });
    requete.flush(ENREGISTREE);

    await vi.waitFor(() => expect(page.textContent).toContain('Élève enregistré'));
    expect(page.querySelector('.texte-du-message')?.textContent).toContain("Bonjour, l'inscription d'Adem Miled est enregistrée.");
    expect([...page.querySelectorAll('.destinataire')].map((d) => d.textContent?.replace(/\s+/g, ' ').trim())).toEqual([
      'Au parent · 98 401 220Ouvrir WhatsApp',
      'À Adem · 22 345 678Ouvrir WhatsApp',
    ]);
    expect(page.querySelector('a[href="/eleves/e1"]')?.textContent).toContain("Voir la fiche de l'élève");
  });

  it('demande le mode d’une Révision avant d’envoyer', async () => {
    await ouvrir(ADEM);

    bouton("Enregistrer l'élève")!.click();
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('Choisissez le mode de tarification'));

    cocher('En totalité');
    bouton("Enregistrer l'élève")!.click();
    const requete = enregistrement();
    expect(requete.request.body.groupes).toEqual([
      { groupeId: 'g1', mode: null },
      { groupeId: 'g3', mode: 'EnTotalite' },
    ]);
    requete.flush(ENREGISTREE);
    await vi.waitFor(() => expect(page.textContent).toContain('Élève enregistré'));
  });

  it('refuse sans groupe, et sans les champs obligatoires', async () => {
    await ouvrir({ ...ADEM, matieres: [ADEM.matieres[0]] });

    cocher('Ne pas l');
    bouton("Enregistrer l'élève")!.click();
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('Choisissez au moins un groupe'));

    cocher('Groupe Samedi 10h → 12h · préféré');
    saisir(page, 'input[formcontrolname="prenom"]', ' ');
    bouton("Enregistrer l'élève")!.click();
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('Remplissez les champs obligatoires'));
  });

  it('montre le refus du serveur', async () => {
    await ouvrir({ ...ADEM, matieres: [ADEM.matieres[0]] });

    bouton("Enregistrer l'élève")!.click();
    enregistrement().flush({ message: 'Ce groupe est fermé : on n\'y inscrit plus' }, { status: 409, statusText: 'Conflict' });

    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain("Ce groupe est fermé : on n'y inscrit plus"));
  });

  it('propose de choisir une fiche qui existe déjà', async () => {
    await ouvrir({
      ...ADEM,
      matieres: [ADEM.matieres[0]],
      fichesSemblables: [{ id: 'e7', prenom: 'Adem', nom: 'Miled', niveau: '9ᵉ année de base' }],
    });

    expect(page.querySelector('.note.warn')?.textContent).toContain("Vérifiez qu'il ne s'agit pas d'un doublon.");
    cocher('Utiliser la fiche de Adem Miled');
    await vi.waitFor(() => expect(page.querySelector('input[formcontrolname="prenom"]')).toBeNull());

    bouton("Enregistrer l'élève")!.click();
    const requete = enregistrement();
    expect(requete.request.body).toEqual({ eleveId: 'e7', fiche: null, groupes: [{ groupeId: 'g1', mode: null }] });
    requete.flush({ ...ENREGISTREE, eleve: { id: 'e7', prenom: 'Adem', nom: 'Miled' } });
    await vi.waitFor(() => expect(page.textContent).toContain('Élève enregistré'));
  });

  it('refuse une demande avec un motif, puis revient à la liste', async () => {
    await ouvrir(ADEM);

    bouton('Refuser')!.click();
    await vi.waitFor(() => expect(bouton('Confirmer le refus')).toBeDefined());
    bouton('Confirmer le refus')!.click();
    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('Choisissez le motif du refus'));

    const motif = page.querySelector('select[aria-label="Motif du refus"]') as HTMLSelectElement;
    motif.value = 'Doublon';
    motif.dispatchEvent(new Event('change'));
    bouton('Confirmer le refus')!.click();
    const requete = http.expectOne({ method: 'POST', url: '/api/demandes/d1/refus' });
    expect(requete.request.body).toEqual({ motif: 'Doublon' });
    requete.flush(null, { status: 204, statusText: 'No Content' });

    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/demandes'));
  });

  it('dit qu’une demande déjà traitée l’est', async () => {
    await ouvrir({ ...ADEM, etat: 'Refusee', motif: 'Doublon', traiteeLe: '2027-11-28T08:00:00+00:00' });

    expect(page.querySelector('[role="status"]')?.textContent).toContain('Demande refusée le 28/11/2027 · Doublon');
    expect(bouton("Enregistrer l'élève")).toBeUndefined();
  });

  it('dit qu’une demande inconnue n’existe pas', async () => {
    await ouvrir(null);

    await vi.waitFor(() => expect(page.textContent).toContain("Cette demande n'existe pas."));
  });
});
