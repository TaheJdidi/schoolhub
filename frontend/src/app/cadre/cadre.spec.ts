import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app.routes';
import { dateDuJour } from '../socle/dates';
import { Moi } from '../socle/session/moi';
import { Session } from '../socle/session/session';
import { PageDeTest } from '../test/page-de-test';
import { AMIRA, HEDI } from '../test/personnes';

describe('le cadre', () => {
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'connexion', component: PageDeTest }, ...routes], withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    harness = await RouterTestingHarness.create();
  });

  // La personne est déjà connue : seul le nombre de demandes à traiter est relu, à chaque page ; l'accueil et la caisse lisent leurs listes.
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match('/api/demandes/nombre').forEach((requete) => requete.flush({ aTraiter: 0 }));
    http.match('/api/seances/a-recopier').forEach((requete) => requete.flush([]));
    http.match((requete) => requete.url === '/api/recus').forEach((requete) => requete.flush([]));
    http.match('/api/eleves/en-rouge').forEach((requete) => requete.flush([]));
    http.match('/api/caisse').forEach((requete) => requete.flush({ ouvertes: [], clotures: [] }));
    http.verify();
  });

  async function ouvrir(adresse: string, moi: Moi): Promise<HTMLElement> {
    TestBed.inject(Session).connaitre(moi);
    await harness.navigateByUrl(adresse);
    await harness.fixture.whenStable();
    return harness.fixture.nativeElement as HTMLElement;
  }

  const adresses = (liens: NodeListOf<HTMLAnchorElement>) => [...liens].map((lien) => lien.getAttribute('href'));

  it("a la recherche d'un élève en haut de chaque écran", async () => {
    const page = await ouvrir('/caisse', AMIRA);

    expect(page.querySelector('.top sh-recherche-d-eleves input')?.getAttribute('aria-label')).toBe('Chercher un élève');
  });

  it('montre le centre, la date du jour et la personne connectée', async () => {
    const page = await ouvrir('/aujourdhui', AMIRA);
    const entete = page.querySelector('.top')?.textContent ?? '';

    expect(entete).toContain('Centre Les Jasmins');
    expect(entete).toContain(dateDuJour(new Date()));
    expect(entete).toContain('AJ');
    expect(entete).toContain('Amira Jlassi');
    expect(entete).toContain('Secrétaire');
  });

  describe('la date du jour, quand le cadre reste ouvert la nuit', () => {
    // Seule l'horloge est simulée : les minuteurs restent réels, pour le routeur.
    beforeEach(() => {
      vi.useFakeTimers({ toFake: ['Date'] });
      vi.setSystemTime(new Date(2026, 10, 3, 20, 0));
    });
    afterEach(() => vi.useRealTimers());

    const entete = (page: HTMLElement) => page.querySelector('.top')?.textContent ?? '';

    it('change à la page suivante', async () => {
      const page = await ouvrir('/aujourdhui', AMIRA);
      expect(entete(page)).toContain('Mardi 3 novembre 2026');

      vi.setSystemTime(new Date(2026, 10, 4, 8, 0));
      await harness.navigateByUrl('/caisse');
      await harness.fixture.whenStable();

      expect(entete(page)).toContain('Mercredi 4 novembre 2026');
    });

    it("change quand on revient sur l'onglet", async () => {
      const page = await ouvrir('/aujourdhui', AMIRA);

      vi.setSystemTime(new Date(2026, 10, 4, 8, 0));
      document.dispatchEvent(new Event('visibilitychange'));
      await harness.fixture.whenStable();

      expect(entete(page)).toContain('Mercredi 4 novembre 2026');
    });
  });

  it('montre à la secrétaire le menu sans la partie Propriétaire', async () => {
    const page = await ouvrir('/aujourdhui', AMIRA);
    const menu = page.querySelector('.rail') as HTMLElement;

    expect(menu.querySelectorAll('a')).toHaveLength(7);
    expect(menu.textContent).not.toContain('Propriétaire');
    expect(menu.querySelector('a[href="/journal"]')).toBeNull();
  });

  it('montre au propriétaire tout le menu, avec sa partie', async () => {
    const page = await ouvrir('/aujourdhui', HEDI);
    const menu = page.querySelector('.rail') as HTMLElement;

    expect(menu.querySelectorAll('a')).toHaveLength(13);
    expect(menu.textContent).toContain('Propriétaire');
    expect(menu.querySelector('a[href="/parametres"]')).not.toBeNull();
  });

  it("montre le nombre de demandes d'inscription à traiter, relu à chaque page", async () => {
    const page = await ouvrir('/caisse', AMIRA);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/demandes/nombre').flush({ aTraiter: 3 });

    const lien = page.querySelector('.rail a[href="/demandes"]') as HTMLElement;
    await vi.waitFor(() => expect(lien.querySelector('.compte')?.textContent?.trim()).toBe('3'));
    expect(lien.querySelector('.compte')?.getAttribute('aria-label')).toBe('3 à traiter');

    await harness.navigateByUrl('/journal');
    http.expectOne('/api/demandes/nombre').flush({ aTraiter: 0 });

    await vi.waitFor(() => expect(lien.querySelector('.compte')).toBeNull());
  });

  it('marque la page en cours dans le menu', async () => {
    const page = await ouvrir('/caisse', AMIRA);

    expect(page.querySelector('.rail a[href="/caisse"]')?.getAttribute('aria-current')).toBe('page');
  });

  it('a les quatre onglets du téléphone', async () => {
    const page = await ouvrir('/caisse', AMIRA);

    expect(adresses(page.querySelectorAll('.tabbar a'))).toEqual(['/aujourdhui', '/guichet', '/caisse', '/plus']);
    expect(page.querySelector('.tabbar a[href="/caisse"]')?.getAttribute('aria-current')).toBe('page');
  });

  it("allume l'onglet « Plus » pour les autres pages du menu", async () => {
    const page = await ouvrir('/journal', AMIRA);

    expect(page.querySelector('.tabbar a[href="/plus"]')?.getAttribute('aria-current')).toBe('page');
    expect(page.querySelector('.tabbar a[href="/caisse"]')?.getAttribute('aria-current')).toBeNull();
  });
});
