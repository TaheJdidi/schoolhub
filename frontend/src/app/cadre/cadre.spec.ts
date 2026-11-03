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

  // La personne est déjà connue : aucune requête ne part.
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  async function ouvrir(adresse: string, moi: Moi): Promise<HTMLElement> {
    TestBed.inject(Session).connaitre(moi);
    await harness.navigateByUrl(adresse);
    await harness.fixture.whenStable();
    return harness.fixture.nativeElement as HTMLElement;
  }

  const adresses = (liens: NodeListOf<HTMLAnchorElement>) => [...liens].map((lien) => lien.getAttribute('href'));

  it('montre le centre, la date du jour et la personne connectée', async () => {
    const page = await ouvrir('/aujourdhui', AMIRA);
    const entete = page.querySelector('.top')?.textContent ?? '';

    expect(entete).toContain('Centre Les Jasmins');
    expect(entete).toContain(dateDuJour(new Date()));
    expect(entete).toContain('AJ');
    expect(entete).toContain('Amira Jlassi');
    expect(entete).toContain('Secrétaire');
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
    const page = await ouvrir('/professeurs', AMIRA);

    expect(page.querySelector('.tabbar a[href="/plus"]')?.getAttribute('aria-current')).toBe('page');
    expect(page.querySelector('.tabbar a[href="/caisse"]')?.getAttribute('aria-current')).toBeNull();
  });
});
