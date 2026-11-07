import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ImportDeListe, ResultatDImport } from './import-de-liste';

@Component({
  imports: [ImportDeListe],
  template: `<sh-import-de-liste quoi="élèves" unite="élève" adresse="/api/imports/eleves" (importe)="importe.set($event)" />`,
})
class Hote {
  readonly importe = signal<ResultatDImport | null>(null);
}

describe('importer une liste', () => {
  function ouvrir() {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Hote);
    fixture.autoDetectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const champ = element.querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(champ, 'files', { value: [new File(['xlsx'], 'eleves.xlsx')] });
    const importer = () => ([...element.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Importer') as HTMLButtonElement).click();
    return { http, fixture, element, importer };
  }

  it('montre les lignes à corriger quand rien n’est importé', async () => {
    const { http, element, importer } = ouvrir();

    importer();
    const requete = http.expectOne({ method: 'POST', url: '/api/imports/eleves' });
    expect((requete.request.body as FormData).get('fichier')).toBeInstanceOf(File);
    requete.flush(
      { importe: false, importes: 0, inscrits: 0, erreurs: ['ligne 14 : niveau inconnu « Terminale »'], doublons: ['ligne 3 : doublon possible avec la ligne 2'] },
      { status: 400, statusText: 'Bad Request' },
    );

    await vi.waitFor(() => expect(element.querySelector('.note.bad')?.textContent).toContain('ligne 14 : niveau inconnu « Terminale »'));
    expect(element.querySelector('.note.bad')?.textContent).toContain('Rien n’est importé'.replace('’', "'"));
    expect(element.querySelector('.note.warn')?.textContent).toContain('ligne 3 : doublon possible');
    http.verify();
  });

  it('dit combien d’élèves sont importés et inscrits, et prévient la page', async () => {
    const { http, fixture, element, importer } = ouvrir();

    importer();
    http.expectOne({ method: 'POST', url: '/api/imports/eleves' }).flush({ importe: true, importes: 120, inscrits: 95, erreurs: [], doublons: [] });

    await vi.waitFor(() => expect(element.querySelector('.note.ok')?.textContent).toContain('120 élèves importés, dont 95 inscrits dans un groupe.'));
    expect(fixture.componentInstance.importe()?.importes).toBe(120);
    http.verify();
  });
});
