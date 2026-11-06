import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BadgeDuGroupeComponent, BoutonDeRappel, PastilleDeCouleur } from './paiement';

describe('la pastille de couleur', () => {
  async function pastille(couleur: string | null): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(PastilleDeCouleur);
    fixture.componentRef.setInput('couleur', couleur);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('dit la couleur, avec sa teinte', async () => {
    expect((await pastille('AJour')).querySelector('.pill.ok')?.textContent).toContain('À jour');
    expect((await pastille('APayer')).querySelector('.pill.info')?.textContent).toContain('À payer');
    expect((await pastille('Orange')).querySelector('.pill.warn')?.textContent).toContain('Orange');
    expect((await pastille('Rouge')).querySelector('.pill.bad')?.textContent).toContain('Rouge');
  });

  it("ne montre rien sans couleur (prix spécial à 0 TND)", async () => {
    expect((await pastille(null)).querySelector('.pill')).toBeNull();
  });
});

describe('le badge du groupe', () => {
  async function badge(valeur: { etat: 'Paye' | 'Rappels' | 'APayer'; nombre: number } | null): Promise<string> {
    const fixture = TestBed.createComponent(BadgeDuGroupeComponent);
    fixture.componentRef.setInput('badge', valeur);
    await fixture.whenStable();
    return (fixture.nativeElement as HTMLElement).textContent?.trim() ?? '';
  }

  it('dit « Groupe payé », les rappels à envoyer, ou les élèves à payer', async () => {
    expect(await badge({ etat: 'Paye', nombre: 0 })).toBe('Groupe payé');
    expect(await badge({ etat: 'Rappels', nombre: 3 })).toBe('3 rappels à envoyer');
    expect(await badge({ etat: 'Rappels', nombre: 1 })).toBe('1 rappel à envoyer');
    expect(await badge({ etat: 'APayer', nombre: 2 })).toBe('2 à payer');
    expect(await badge(null)).toBe('');
  });
});

describe('le bouton de rappel de paiement', () => {
  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(BoutonDeRappel);
    fixture.componentRef.setInput('eleveId', 'e1');
    fixture.componentRef.setInput('dernierRappelLe', '2027-10-12T09:00:00+00:00');
    page = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('montre la date du dernier rappel, ouvre le rappel, puis le note quand WhatsApp s’ouvre', async () => {
    expect(page.textContent).toContain('rappel envoyé le 12/10');

    page.querySelector('button')!.click();
    http.expectOne('/api/eleves/e1/rappel').flush({
      couleur: 'Rouge',
      texte: 'Bonjour, il reste à payer pour Adem Miled : Mois 3 de mathématiques, 40,000 TND. Centre Les Jasmins.',
      messages: [{ destinataire: 'Parent', telephone: '98 401 220', lien: 'https://wa.me/21698401220?text=a' }],
      dernierRappelLe: '2027-10-12T09:00:00+00:00',
    });
    await vi.waitFor(() => expect(page.querySelector('.texte')?.textContent).toContain('il reste à payer pour Adem Miled'));
    expect(page.querySelector('.destinataire')?.textContent).toContain('Au parent · 98 401 220');

    const lien = page.querySelector('.destinataire a') as HTMLAnchorElement;
    lien.addEventListener('click', (evenement) => evenement.preventDefault());
    lien.click();
    http.expectOne({ method: 'POST', url: '/api/eleves/e1/rappel/envoye' }).flush(null, { status: 204, statusText: 'No Content' });

    const aujourdhui = new Date();
    const deuxChiffres = (n: number) => String(n).padStart(2, '0');
    await vi.waitFor(() =>
      expect(page.querySelector('.dernier')?.textContent).toContain(`rappel envoyé le ${deuxChiffres(aujourdhui.getDate())}/${deuxChiffres(aujourdhui.getMonth() + 1)}`),
    );
  });

  it('montre le refus du serveur', async () => {
    page.querySelector('button')!.click();
    http.expectOne('/api/eleves/e1/rappel').flush({ message: "Cet élève n'a rien à payer aujourd'hui" }, { status: 409, statusText: 'Conflict' });

    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain("Cet élève n'a rien à payer aujourd'hui"));
  });
});
