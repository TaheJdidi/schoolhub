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
    // Comme dans l'appel, le rappel s'ouvre dans une fenêtre.
    expect(page.querySelector('[role="dialog"]')).not.toBeNull();

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

describe('la cloche de rappel, dans l’appel', () => {
  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(BoutonDeRappel);
    fixture.componentRef.setInput('eleveId', 'e1');
    fixture.componentRef.setInput('compact', true);
    fixture.componentRef.setInput('pour', 'Adem Miled');
    page = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('ouvre une fenêtre avec le détail, le total et WhatsApp, puis se referme', async () => {
    (page.querySelector('[aria-label="Envoyer un rappel à Adem Miled"]') as HTMLButtonElement).click();
    http.expectOne('/api/eleves/e1/rappel').flush({
      couleur: 'Rouge',
      texte: 'Bonjour, il reste à payer pour Adem Miled : …',
      messages: [
        { destinataire: 'Parent', telephone: '98 401 220', lien: 'https://wa.me/21698401220?text=a' },
        { destinataire: 'Adem', telephone: '22 345 678', lien: 'https://wa.me/21622345678?text=a' },
      ],
      dernierRappelLe: null,
      lignes: [{ libelle: 'Mois 3 de mathématiques', montant: 40 }, { libelle: 'Assurance 2027-2028', montant: 20 }],
      total: 60,
    });

    const fenetre = await vi.waitFor(() => {
      const trouvee = page.querySelector('[role="dialog"]') as HTMLElement;
      expect(trouvee).not.toBeNull();
      return trouvee;
    });
    expect(fenetre.querySelector('h2')?.textContent).toContain('Rappel de paiement');
    expect(fenetre.textContent).toContain('Adem Miled');
    expect([...fenetre.querySelectorAll('.lignes li')].map((li) => [...li.children].map((c) => c.textContent?.trim()))).toEqual([
      ['Mois 3 de mathématiques', '40,000'],
      ['Assurance 2027-2028', '20,000'],
    ]);
    expect(fenetre.querySelector('.total')?.textContent).toContain('60,000 TND');
    expect([...fenetre.querySelectorAll('.destinataire span')].map((s) => s.textContent)).toEqual(['Au parent · 98 401 220', 'À Adem · 22 345 678']);
    expect(fenetre.textContent).toContain('Aucun rappel envoyé pour l’instant.');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await vi.waitFor(() => expect(page.querySelector('[role="dialog"]')).toBeNull());
  });
});
