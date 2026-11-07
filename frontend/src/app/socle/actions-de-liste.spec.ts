import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActionsDeListe } from './actions-de-liste';
import { Session } from './session/session';
import { Moi } from './session/moi';

@Component({
  imports: [ActionsDeListe],
  template: `<sh-actions-de-liste titre="Élèves de 9ᵉ année de base" [nombre]="2" exporter="/api/exports/eleves" [filtres]="{ recherche: '', niveau: '9ᵉ année de base' }" />`,
})
class Hote {}

@Component({
  imports: [ActionsDeListe],
  template: `<sh-actions-de-liste titre="Élèves" [nombre]="0" exporter="/api/exports/eleves" />`,
})
class HoteDUneListeVide {}

describe('imprimer et exporter une liste', () => {
  it("prépare l'en-tête d'impression, et télécharge l'export avec le filtre en cours", async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    TestBed.inject(Session).connaitre({ prenom: 'Amira', nom: 'Jlassi', role: 'Secretaire', centre: 'Centre Les Jasmins' } as Moi);
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Hote);
    fixture.autoDetectChanges();
    const element = fixture.nativeElement as HTMLElement;

    const entete = element.querySelector('.entete-d-impression')!.textContent!;
    expect(entete).toContain('Centre Les Jasmins');
    expect(entete).toContain('Élèves de 9ᵉ année de base');
    expect(entete).toContain('2 lignes');

    const creerUnLien = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:liste');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const cliquer = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    ([...element.querySelectorAll('button')].find((b) => b.textContent?.includes('Exporter')) as HTMLButtonElement).click();

    const requete = http.expectOne((r) => r.url === '/api/exports/eleves');
    // Un filtre vide ne part pas.
    expect(requete.request.params.keys()).toEqual(['niveau']);
    requete.flush(new Blob(['xlsx']), { headers: { 'Content-Disposition': 'attachment; filename=eleves-2027-11-27.xlsx' } });
    await vi.waitFor(() => expect(cliquer).toHaveBeenCalled());
    expect(creerUnLien).toHaveBeenCalled();
    http.verify();
    vi.restoreAllMocks();
  });

  it("grise « Imprimer » et « Exporter » d'une liste vide", () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(HoteDUneListeVide);
    fixture.autoDetectChanges();

    const boutons = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')];
    expect(boutons.map((b) => [b.textContent?.trim(), b.disabled, b.title])).toEqual([
      ['Imprimer', true, 'La liste est vide'],
      ['Exporter (Excel)', true, 'La liste est vide'],
    ]);
  });
});
