import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Session } from '../../socle/session/session';
import { PageDeTest } from '../../test/page-de-test';
import { AMIRA } from '../../test/personnes';
import { Affiche } from './affiche';

describe("l'affiche du formulaire public", () => {
  const LIEN = 'https://app.schoolhub.tn/inscription/les-jasmins';

  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'demandes/affiche', component: Affiche },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(Session).connaitre(AMIRA);
    const harness = await RouterTestingHarness.create('/demandes/affiche');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/demandes/lien'))).flush({ lien: LIEN });
    await vi.waitFor(() => expect(page.textContent).toContain(LIEN));
  });

  afterEach(() => http.verify());

  it('montre le nom du centre, le QR code et le lien', () => {
    expect(page.querySelector('h1')?.textContent).toContain('Centre Les Jasmins');
    expect(page.textContent).toContain("Demande d'inscription");
    expect(page.textContent).toContain('Scannez le code ou ouvrez le lien, sans créer de compte.');
    expect(page.querySelector('img.qr-code')?.getAttribute('src')).toBe('/api/demandes/qr-code');
  });

  it("s'imprime", () => {
    const imprimer = vi.spyOn(window, 'print').mockImplementation(() => undefined);

    ([...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Imprimer') as HTMLButtonElement).click();

    expect(imprimer).toHaveBeenCalled();
    imprimer.mockRestore();
  });
});
