import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageDeTest } from '../../test/page-de-test';
import { PartDuCentre } from './part-du-centre';
import { LigneDePart, Parts, PartsDuCentre } from './parts';

const MOIS_1: LigneDePart = {
  groupeId: 'g1',
  groupe: 'Groupe Samedi 10h → 12h',
  matiere: 'Mathématiques',
  cle: 'Mois 1',
  periode: 'Mois 1',
  debut: '2027-09-18',
  fin: '2027-10-09',
  montantDuGroupe: 2000,
  pourcentage: 5,
  partDuCentre: 100,
  avanceDeduite: 300,
  aVerser: 1600,
  payee: false,
  payeeLe: null,
};

const PARTS: PartsDuCentre = {
  resteAVerser: 1600,
  dejaVerse: 0,
  avancesRestantes: 0,
  professeurs: [{ id: 'k1', nom: 'Karim Ben Salah', pourcentage: 5, resteAVerser: 1600, dejaVerse: 0, avancesRestantes: 0, lignes: [MOIS_1] }],
};

function configurer(): HttpTestingController {
  TestBed.configureTestingModule({
    providers: [provideRouter([{ path: 'parts', component: Parts }, { path: '**', component: PageDeTest }]), provideHttpClient(), provideHttpClientTesting()],
  });
  return TestBed.inject(HttpTestingController);
}

describe('les parts des professeurs', () => {
  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    http = configurer();
    const harness = await RouterTestingHarness.create('/parts');
    page = harness.routeNativeElement as HTMLElement;
    (await vi.waitFor(() => http.expectOne('/api/parts'))).flush(PARTS);
    http.expectOne('/api/professeurs').flush([{ id: 'k1', prenom: 'Karim', nom: 'Ben Salah', parti: false }]);
    await vi.waitFor(() => expect(page.querySelectorAll('tbody tr')).toHaveLength(1));
  });

  afterEach(() => http.verify());

  const bouton = (texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte)!;

  it('montre le résumé et une ligne par période, avec l’avance déduite', () => {
    expect(page.querySelector('.chiffres')?.textContent).toContain('1600,000 TND');
    expect(page.querySelector('h2')?.textContent).toContain('Karim Ben Salah · part du centre 5 %');
    const ligne = page.querySelector('tbody tr')!.textContent!;
    expect(ligne).toContain('Mois 1');
    expect(ligne).toContain('18/09 → 09/10');
    expect(ligne).toContain('2000,000');
    expect(ligne).toContain('−300,000');
    expect(ligne).toContain('Non payée');
  });

  it('marque une part payée', async () => {
    (page.querySelector('tbody input[type="checkbox"]') as HTMLInputElement).click();

    const requete = http.expectOne({ method: 'POST', url: '/api/parts/paiement' });
    expect(requete.request.body).toEqual({ groupeId: 'g1', cle: 'Mois 1', payee: true });
    requete.flush({ ...PARTS, resteAVerser: 0, dejaVerse: 1600, professeurs: [{ ...PARTS.professeurs[0], lignes: [{ ...MOIS_1, payee: true }] }] });
    await vi.waitFor(() => expect(page.querySelector('tbody')?.textContent).toContain('Payée'));
  });

  it('enregistre une avance', async () => {
    bouton('Enregistrer une avance').click();
    await vi.waitFor(() => expect(page.querySelector('.avance')).not.toBeNull());
    (page.querySelector('.avance input[inputmode="decimal"]') as HTMLInputElement).value = '300';
    const date = page.querySelector('.avance input[type="date"]') as HTMLInputElement;
    date.value = '2027-11-05';

    bouton("Enregistrer l'avance").click();
    const requete = http.expectOne({ method: 'POST', url: '/api/parts/avances' });
    expect(requete.request.body).toEqual({ professeurId: 'k1', montant: 300, le: '2027-11-05' });
    requete.flush(PARTS);
    await vi.waitFor(() => expect(page.textContent).toContain('Avance de 300,000 TND enregistrée'));
  });
});

@Component({
  imports: [PartDuCentre],
  template: `<sh-part-du-centre [professeurId]="id()" />`,
})
class HoteDeLaPart {
  readonly id = signal('k1');
}

describe('la part du centre d’un professeur', () => {
  it('montre celle du centre, puis la change', async () => {
    const http = configurer();
    const fixture = TestBed.createComponent(HoteDeLaPart);
    fixture.autoDetectChanges();
    (await vi.waitFor(() => http.expectOne('/api/professeurs/k1/part-du-centre'))).flush({ pourcentage: null, parDefaut: 5, changeeLe: null });
    const element = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => expect(element.textContent).toContain('Part du centre : 5 % (celle du centre)'));

    (element.querySelector('.lien') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(element.querySelector('input')).not.toBeNull());
    (element.querySelector('input') as HTMLInputElement).value = '10';
    ([...element.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Enregistrer') as HTMLButtonElement).click();

    const requete = http.expectOne({ method: 'PUT', url: '/api/professeurs/k1/part-du-centre' });
    expect(requete.request.body).toEqual({ pourcentage: 10 });
    requete.flush({ pourcentage: 10, parDefaut: 5, changeeLe: '2027-11-27' });
    await vi.waitFor(() => expect(element.textContent).toContain('Part du centre : 10 % (propre à ce professeur)'));
    http.verify();
  });
});
