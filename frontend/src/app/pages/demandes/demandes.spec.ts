import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { Demandes } from './demandes';

describe('la page « Demandes d’inscription »', () => {
  const LIEN = 'https://app.schoolhub.tn/inscription/les-jasmins';

  let http: HttpTestingController;
  let page: HTMLElement;
  let harness: RouterTestingHarness;
  let ecrire: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    ecrire = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: ecrire }, configurable: true });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'demandes', component: Demandes },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/demandes');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/demandes/lien'))).flush({ lien: LIEN });
    await vi.waitFor(() => expect(page.querySelector('.lien')).not.toBeNull());
  });

  afterEach(() => http.verify());

  const bouton = (texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte);

  it('montre le lien public du centre et son QR code', () => {
    expect(page.querySelector('h1')?.textContent).toContain("Demandes d'inscription");
    expect(page.querySelector('.lien')?.textContent).toContain(LIEN);
    expect(page.querySelector('.lien')?.getAttribute('href')).toBe(LIEN);
    expect(page.querySelector('img.qr-code')?.getAttribute('src')).toBe('/api/demandes/qr-code');
    expect(page.querySelector('a[href="/demandes/affiche"]')?.textContent).toContain("Imprimer l'affiche");
  });

  it('copie le lien', async () => {
    bouton('Copier le lien')!.click();

    await vi.waitFor(() => expect(page.querySelector('[role="status"]')?.textContent).toContain('Lien copié'));
    expect(ecrire).toHaveBeenCalledWith(LIEN);
  });

  it('dit quand la copie échoue', async () => {
    ecrire.mockRejectedValue(new Error('refusé'));

    bouton('Copier le lien')!.click();

    await vi.waitFor(() =>
      expect(page.querySelector('[role="alert"]')?.textContent).toContain("Le lien n'a pas pu être copié : sélectionnez-le à la main."),
    );
  });
});
