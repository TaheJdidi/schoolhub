import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { Moi } from './socle/session/moi';
import { intercepteurDeSession } from './socle/session/intercepteur';
import { AMIRA, HEDI } from './test/personnes';

describe("les adresses de l'application", () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(withInterceptors([intercepteurDeSession])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  // Le cadre relit le nombre de demandes à traiter à chaque page ; l'accueil lit les appels à recopier.
  afterEach(() => {
    http.match('/api/demandes/nombre').forEach((requete) => requete.flush({ aTraiter: 0 }));
    http.match('/api/seances/a-recopier').forEach((requete) => requete.flush([]));
    http.match('/api/seances/du-jour').forEach((requete) => requete.flush([]));
    http.match((requete) => requete.url === '/api/bibliotheque').forEach((requete) => requete.flush({ presents: 0, capacite: null, entrees: 0 }));
    http.match((requete) => requete.url === '/api/recus').forEach((requete) => requete.flush([]));
    http.match('/api/eleves/en-rouge').forEach((requete) => requete.flush([]));
    http.match((requete) => requete.url === '/api/groupes').forEach((requete) => requete.flush([]));
    http.match((requete) => requete.url === '/api/achats').forEach((requete) => requete.flush({ achats: [], totalMateriel: 0, totalService: 0, total: 0 }));
    http.match('/api/caisse').forEach((requete) => requete.flush({ ouvertes: [], clotures: [] }));
    http.match((requete) => requete.url === '/api/journal').forEach((requete) => requete.flush({ lignes: [], auteurs: [], actions: [] }));
    http.verify();
  });

  /** Ouvre une adresse, comme au premier chargement : la session est demandée au serveur, qui répond `moi`. */
  async function ouvrir(adresse: string, moi: Moi): Promise<void> {
    const navigation = harness.navigateByUrl(adresse);
    const requete = await vi.waitFor(() => http.expectOne('/api/moi'));
    requete.flush(moi);
    await navigation;
    await harness.fixture.whenStable();
  }

  const texte = () => (harness.fixture.nativeElement as HTMLElement).textContent ?? '';
  const adresseActuelle = () => TestBed.inject(Router).url;
  const titre = () => TestBed.inject(Title).getTitle();

  it('une secrétaire lit « Accès réservé au propriétaire » sur /journal', async () => {
    await ouvrir('/journal', AMIRA);

    expect(texte()).toContain('Accès réservé au propriétaire');
    expect(adresseActuelle()).toBe('/journal');
    expect(titre()).toBe('Accès réservé · SchoolHub');
  });

  it('le propriétaire qui ouvre /journal en premier voit sa page', async () => {
    await ouvrir('/journal', HEDI);

    await vi.waitFor(() => expect(texte()).toContain('Qui a fait quoi, et quand'));
    expect(texte()).not.toContain('Accès réservé');
  });

  it('le propriétaire voit la page « Comptes » sur /comptes', async () => {
    await ouvrir('/comptes', HEDI);
    http.expectOne('/api/comptes').flush([]);
    await harness.fixture.whenStable();

    expect(texte()).toContain('Inviter une personne');
    expect(texte()).not.toContain('Cette page arrive bientôt');
    expect(titre()).toBe('Comptes · SchoolHub');
  });

  it('le propriétaire voit « Paramètres du centre » sur /parametres', async () => {
    await ouvrir('/parametres', HEDI);
    http.expectOne('/api/centre/parametres').flush({
      nom: 'Centre Les Jasmins', telephone: null, email: null, anneeScolaire: null,
      montantDeLAssurance: null, prixDeLEntreeALaBibliotheque: null, capaciteDeLaBibliotheque: null, partDuCentreParDefaut: null,
    });
    http.expectOne('/api/niveaux').flush([]);
    await harness.fixture.whenStable();

    expect(texte()).toContain('Les informations et les prix de votre centre.');
    expect(texte()).not.toContain('Cette page arrive bientôt');
    expect(titre()).toBe('Paramètres du centre · SchoolHub');
  });

  it('une secrétaire voit la page « Professeurs » sur /professeurs', async () => {
    await ouvrir('/professeurs', AMIRA);
    http.expectOne('/api/professeurs').flush([]);
    http.expectOne('/api/groupes').flush([]);
    await harness.fixture.whenStable();

    expect(texte()).toContain('Ajouter un professeur');
    expect(texte()).not.toContain('Cette page arrive bientôt');
    expect(titre()).toBe('Professeurs · SchoolHub');
  });

  it('une secrétaire crée un groupe sur /professeurs/groupes/nouveau, avec « Professeurs » allumé', async () => {
    await ouvrir('/professeurs/groupes/nouveau', AMIRA);
    http.expectOne('/api/professeurs').flush([]);
    http.expectOne('/api/niveaux').flush([]);
    http.expectOne('/api/matieres').flush([]);
    await harness.fixture.whenStable();

    expect(texte()).toContain('Nouveau groupe');
    expect(texte()).toContain('Créer le groupe');
    expect(titre()).toBe('Nouveau groupe · SchoolHub');
    const menu = harness.fixture.nativeElement as HTMLElement;
    expect(menu.querySelector('a.nav[href="/professeurs"]')?.getAttribute('aria-current')).toBe('page');
  });

  it('une secrétaire ouvre la fiche d’un groupe sur /professeurs/groupes/g1', async () => {
    await ouvrir('/professeurs/groupes/g1', AMIRA);
    http.expectOne('/api/groupes/g1').flush({ message: 'Groupe introuvable' }, { status: 404, statusText: 'Not Found' });
    http.expectOne((r) => r.url === '/api/inscriptions').flush({ message: 'Groupe introuvable' }, { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => expect(texte()).toContain("Ce groupe n'existe pas."));

    expect(titre()).toBe('Groupe · SchoolHub');
    const menu = harness.fixture.nativeElement as HTMLElement;
    expect(menu.querySelector('a.nav[href="/professeurs"]')?.getAttribute('aria-current')).toBe('page');
  });

  it('une secrétaire voit la page « Élèves » sur /eleves, et sa fiche sur /eleves/e1', async () => {
    await ouvrir('/eleves', AMIRA);
    http.expectOne('/api/eleves').flush([]);
    await vi.waitFor(() => expect(texte()).toContain('Ajouter un élève'));
    expect(texte()).not.toContain('Cette page arrive bientôt');
    expect(titre()).toBe('Élèves · SchoolHub');

    await harness.navigateByUrl('/eleves/e1');
    for (const adresse of ['/api/eleves/e1', '/api/eleves/e1/assurance', '/api/eleves/e1/a-payer']) {
      http.expectOne(adresse).flush({ message: 'Élève introuvable' }, { status: 404, statusText: 'Not Found' });
    }
    http.expectOne((r) => r.url === '/api/inscriptions').flush({ message: 'Élève introuvable' }, { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => expect(texte()).toContain("Cet élève n'existe pas."));
    expect(titre()).toBe('Élève · SchoolHub');
    const menu = harness.fixture.nativeElement as HTMLElement;
    expect(menu.querySelector('a.nav[href="/eleves"]')?.getAttribute('aria-current')).toBe('page');
  });

  it('une secrétaire inscrit un élève sur /eleves/e1/inscription, avec « Élèves » allumé', async () => {
    await ouvrir('/eleves/e1/inscription', AMIRA);
    (await vi.waitFor(() => http.expectOne('/api/eleves/e1'))).flush({ message: 'Élève introuvable' }, { status: 404, statusText: 'Not Found' });
    http.expectOne((r) => r.url === '/api/inscriptions/groupes-possibles').flush({ message: 'Élève introuvable' }, { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => expect(texte()).toContain("Cet élève n'existe pas."));

    expect(titre()).toBe('Inscription · SchoolHub');
    const menu = harness.fixture.nativeElement as HTMLElement;
    expect(menu.querySelector('a.nav[href="/eleves"]')?.getAttribute('aria-current')).toBe('page');
  });

  it('une secrétaire ajoute un élève à un groupe sur /professeurs/groupes/g1/inscription, avec « Professeurs » allumé', async () => {
    await ouvrir('/professeurs/groupes/g1/inscription', AMIRA);
    (await vi.waitFor(() => http.expectOne('/api/groupes/g1'))).flush({ message: 'Groupe introuvable' }, { status: 404, statusText: 'Not Found' });
    http.expectOne((r) => r.url === '/api/inscriptions').flush({ message: 'Groupe introuvable' }, { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => expect(texte()).toContain("Ce groupe n'existe pas."));

    expect(titre()).toBe('Inscription · SchoolHub');
    const menu = harness.fixture.nativeElement as HTMLElement;
    expect(menu.querySelector('a.nav[href="/professeurs"]')?.getAttribute('aria-current')).toBe('page');
  });

  it('une secrétaire change un élève de groupe sur /eleves/e1/changement/i1, avec « Élèves » allumé', async () => {
    await ouvrir('/eleves/e1/changement/i1', AMIRA);
    (await vi.waitFor(() => http.expectOne('/api/eleves/e1'))).flush({ message: 'Élève introuvable' }, { status: 404, statusText: 'Not Found' });
    http.expectOne((r) => r.url === '/api/inscriptions').flush({ message: 'Élève introuvable' }, { status: 404, statusText: 'Not Found' });
    http.expectOne((r) => r.url === '/api/inscriptions/groupes-possibles').flush({ message: 'Élève introuvable' }, { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => expect(texte()).toContain("Cet élève n'existe pas."));

    expect(titre()).toBe('Changement de groupe · SchoolHub');
    const menu = harness.fixture.nativeElement as HTMLElement;
    expect(menu.querySelector('a.nav[href="/eleves"]')?.getAttribute('aria-current')).toBe('page');
  });

  it('une secrétaire crée un élève sur /eleves/nouveau', async () => {
    await ouvrir('/eleves/nouveau', AMIRA);
    http.expectOne('/api/niveaux').flush([]);
    http.expectOne('/api/groupes').flush([]);
    http.expectOne('/api/professeurs').flush([]);
    await vi.waitFor(() => expect(texte()).toContain('Nouvel élève'));
    expect(titre()).toBe('Nouvel élève · SchoolHub');
  });

  it('une secrétaire lit « Accès réservé au propriétaire » sur /parametres', async () => {
    await ouvrir('/parametres', AMIRA);

    expect(texte()).toContain('Accès réservé au propriétaire');
  });

  it('une secrétaire lit « Accès réservé au propriétaire » sur /comptes', async () => {
    await ouvrir('/comptes', AMIRA);

    expect(texte()).toContain('Accès réservé au propriétaire');
  });

  it("/ mène à « Aujourd'hui »", async () => {
    await ouvrir('/', AMIRA);

    expect(adresseActuelle()).toBe('/aujourdhui');
    expect((harness.fixture.nativeElement as HTMLElement).querySelector('main h1')?.textContent).toContain("Aujourd'hui");
    expect(texte()).toContain('Séances du jour');
    expect(titre()).toBe("Aujourd'hui · SchoolHub");
  });

  it('une page pas encore faite le dit', async () => {
    await ouvrir('/guichet', AMIRA);

    expect(texte()).toContain('Guichet');
    expect(texte()).toContain('Cette page arrive bientôt');
    expect(titre()).toBe('Guichet · SchoolHub');
  });

  it('une secrétaire voit les reçus du jour sur /caisse, un reçu sur /caisse/recus/r1, et encaisse sur /eleves/e1/encaissement', async () => {
    await ouvrir('/caisse', AMIRA);
    http.expectOne((r) => r.url === '/api/recus').flush([]);
    await vi.waitFor(() => expect(texte()).toContain('Reçus du jour'));
    expect(titre()).toBe('Caisse · SchoolHub');

    await harness.navigateByUrl('/caisse/recus/r1');
    (await vi.waitFor(() => http.expectOne('/api/recus/r1'))).flush({ message: 'Reçu introuvable' }, { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => expect(texte()).toContain("Ce reçu n'existe pas."));
    expect(titre()).toBe('Reçu · SchoolHub');
    const menu = harness.fixture.nativeElement as HTMLElement;
    expect(menu.querySelector('a.nav[href="/caisse"]')?.getAttribute('aria-current')).toBe('page');

    await harness.navigateByUrl('/eleves/e1/encaissement');
    (await vi.waitFor(() => http.expectOne('/api/eleves/e1/encaissable'))).flush({ message: 'Élève introuvable' }, { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => expect(texte()).toContain("Cet élève n'existe pas."));
    expect(titre()).toBe('Encaissement · SchoolHub');

    // CAI-04 : l'encaissement groupé, depuis un groupe.
    await harness.navigateByUrl('/professeurs/groupes/g1/encaissement');
    (await vi.waitFor(() => http.expectOne('/api/groupes/g1/encaissement'))).flush({ message: 'Groupe introuvable' }, { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => expect(texte()).toContain("Ce groupe n'existe pas."));
    expect(titre()).toBe('Encaissement groupé · SchoolHub');
  });

  it("une personne déjà connectée qui ouvre la connexion passe à « Aujourd'hui »", async () => {
    await ouvrir('/connexion', AMIRA);

    expect(adresseActuelle()).toBe('/aujourdhui');
  });

  it('une secrétaire voit le lien public sur /demandes, et imprime l’affiche sur /demandes/affiche', async () => {
    await ouvrir('/demandes', AMIRA);
    http.expectOne('/api/demandes/lien').flush({ lien: 'http://localhost:4200/inscription/les-jasmins' });
    http.expectOne('/api/demandes').flush([]);
    await vi.waitFor(() => expect(texte()).toContain('Le lien public du centre'));
    expect(texte()).not.toContain('Cette page arrive bientôt');
    expect(titre()).toBe("Demandes d'inscription · SchoolHub");

    await harness.navigateByUrl('/demandes/affiche');
    (await vi.waitFor(() => http.expectOne('/api/demandes/lien'))).flush({ lien: 'http://localhost:4200/inscription/les-jasmins' });
    await vi.waitFor(() => expect(texte()).toContain('Scannez le code'));
    expect(titre()).toBe('Affiche · SchoolHub');
  });

  it("une secrétaire ouvre une demande sur /demandes/d1, avec « Demandes d'inscription » allumé", async () => {
    await ouvrir('/demandes/d1', AMIRA);
    (await vi.waitFor(() => http.expectOne('/api/demandes/d1'))).flush({ message: 'Demande introuvable' }, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/niveaux').flush([]);
    await vi.waitFor(() => expect(texte()).toContain("Cette demande n'existe pas."));

    expect(titre()).toBe("Demande d'inscription · SchoolHub");
    const menu = harness.fixture.nativeElement as HTMLElement;
    expect(menu.querySelector('a.nav[href="/demandes"]')?.getAttribute('aria-current')).toBe('page');
  });

  it("une secrétaire fait l'appel sur /professeurs/groupes/g1/appel/s1, avec « Professeurs » allumé", async () => {
    await ouvrir('/professeurs/groupes/g1/appel/s1', AMIRA);
    (await vi.waitFor(() => http.expectOne('/api/seances/s1/appel'))).flush({ message: 'Séance introuvable' }, { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => expect(texte()).toContain("Cette séance n'existe pas."));

    expect(titre()).toBe('Appel · SchoolHub');
    const menu = harness.fixture.nativeElement as HTMLElement;
    expect(menu.querySelector('a.nav[href="/professeurs"]')?.getAttribute('aria-current')).toBe('page');
  });

  it("une secrétaire ouvre la feuille d'appel dans l'application ; l'ancienne adresse /feuille-d-appel/g1 y mène", async () => {
    await ouvrir('/feuille-d-appel/g1?du=2027-11-13&au=2027-12-04', AMIRA);
    (await vi.waitFor(() => http.expectOne('/api/groupes/g1'))).flush({ message: 'Groupe introuvable' }, { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => expect(texte()).toContain('Groupe introuvable'));

    expect(adresseActuelle()).toBe('/professeurs/groupes/g1/feuille-d-appel?du=2027-11-13&au=2027-12-04');
    expect(titre()).toBe("Feuille d'appel · SchoolHub");
    // Comme le prototype : avec le menu, qui ne s'imprime pas.
    expect((harness.fixture.nativeElement as HTMLElement).querySelector('.rail')).not.toBeNull();
  });

  it("l'accueil montre les appels à recopier", async () => {
    await ouvrir('/aujourdhui', AMIRA);
    http.expectOne('/api/seances/a-recopier').flush([
      {
        seanceId: 's1',
        date: '2027-11-20',
        debut: '10:00',
        fin: '12:00',
        groupe: { id: 'g1', nom: 'Groupe Samedi 10h → 12h', type: 'Etude', matiere: 'Mathématiques' },
        professeur: 'Karim Ben Salah',
      },
    ]);

    await vi.waitFor(() => expect(texte()).toContain('sam. 20/11 · 10h → 12h'));
    expect(texte()).toContain('Mathématiques · Groupe Samedi 10h → 12h · Karim Ben Salah');
    expect((harness.fixture.nativeElement as HTMLElement).querySelector('a[href="/professeurs/groupes/g1/appel/s1"]')?.textContent).toContain('Recopier');
  });

  it("l'accueil montre les élèves en rouge, avec leur rappel de paiement", async () => {
    await ouvrir('/aujourdhui', AMIRA);
    http.expectOne('/api/eleves/en-rouge').flush([
      { id: 'e1', prenom: 'Adem', nom: 'Miled', dues: ['Mois 3 de mathématiques', 'assurance 2027-2028'], total: 60, dernierRappelLe: '2027-10-12T09:00:00+00:00' },
    ]);

    await vi.waitFor(() => expect(texte()).toContain('Adem Miled'));
    expect(texte()).toContain('Mois 3 de mathématiques, assurance 2027-2028');
    expect(texte()).toContain('60,000 TND');
    expect(texte()).toContain('rappel envoyé le 12/10');
    expect([...(harness.fixture.nativeElement as HTMLElement).querySelectorAll('main button')].map((b) => b.textContent?.trim())).toContain(
      'Envoyer un rappel',
    );
  });

  it("l'accueil montre les demandes à traiter", async () => {
    await ouvrir('/aujourdhui', AMIRA);
    http.expectOne('/api/demandes/nombre').flush({ aTraiter: 2 });

    const carte = () => (harness.fixture.nativeElement as HTMLElement).querySelector('main a[href="/demandes"]');
    await vi.waitFor(() => expect(carte()?.querySelector('b')?.textContent?.trim()).toBe('2'));
    expect(carte()?.textContent).toContain('demandes d’inscription à traiter');
  });

  it("le formulaire public s'ouvre sans connexion sur /inscription/les-jasmins", async () => {
    const navigation = harness.navigateByUrl('/inscription/les-jasmins');
    (await vi.waitFor(() => http.expectOne('/api/public/inscription/les-jasmins'))).flush(
      { message: "Ce lien d'inscription n'existe pas" },
      { status: 404, statusText: 'Not Found' },
    );
    await navigation;
    await vi.waitFor(() => expect(texte()).toContain("Ce lien d'inscription n'existe pas."));

    http.expectNone('/api/moi');
    expect(titre()).toBe("Demande d'inscription · SchoolHub");
  });

  it("sans session, la connexion s'affiche sans parler de session expirée", async () => {
    const navigation = harness.navigateByUrl('/connexion');
    const requete = await vi.waitFor(() => http.expectOne('/api/moi'));
    requete.flush(null, { status: 401, statusText: 'Unauthorized' });
    await navigation;
    await harness.fixture.whenStable();

    expect(texte()).toContain('Se connecter');
    expect(texte()).not.toContain('expiré');
    expect(titre()).toBe('Connexion · SchoolHub');
  });

  it("une adresse inconnue dit que la page n'existe pas", async () => {
    await ouvrir('/nimporte', AMIRA);

    expect(texte()).toContain("Cette page n'existe pas");
    expect(titre()).toBe('Page introuvable · SchoolHub');
  });
});
