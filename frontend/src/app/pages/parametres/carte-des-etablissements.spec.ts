import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CarteDesEtablissements } from './carte-des-etablissements';

describe('la carte des établissements', () => {
  const ADRESSE = '/api/etablissements';
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function ouvrir(liste = [{ id: 'e1', nom: 'Collège Ibn Khaldoun' }]) {
    const fixture = TestBed.createComponent(CarteDesEtablissements);
    fixture.autoDetectChanges();
    http.expectOne(ADRESSE).flush(liste);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const bouton = (texte: string) => [...element.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte) as HTMLButtonElement;
    return { fixture, element, bouton };
  }

  it('montre la liste, ou dit qu’elle est vide', async () => {
    const { element } = await ouvrir([]);
    expect(element.textContent).toContain("Aucun établissement pour l'instant");
    expect(element.textContent).toContain('0 établissement');
  });

  it('ajoute un établissement, puis recharge la liste', async () => {
    const { fixture, element, bouton } = await ouvrir([]);
    const champ = element.querySelector('[aria-label="Nouvel établissement"]') as HTMLInputElement;
    champ.value = 'Lycée Pilote Ariana';
    champ.dispatchEvent(new Event('input'));
    bouton('Ajouter').click();

    const ajout = http.expectOne({ method: 'POST', url: ADRESSE });
    expect(ajout.request.body).toEqual({ nom: 'Lycée Pilote Ariana' });
    ajout.flush({ id: 'e2', nom: 'Lycée Pilote Ariana' });
    await vi.waitFor(() => http.expectOne({ method: 'GET', url: ADRESSE }).flush([{ id: 'e2', nom: 'Lycée Pilote Ariana' }]));
    await fixture.whenStable();
    expect(element.textContent).toContain('Établissement ajouté');
    expect(element.querySelector('.nom')?.textContent).toBe('Lycée Pilote Ariana');
  });

  it('refuse un nom vide sans rien envoyer', async () => {
    const { fixture, element, bouton } = await ouvrir([]);
    bouton('Ajouter').click();
    await fixture.whenStable();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain("Saisissez le nom de l'établissement");
  });

  it('retire un établissement après « Oui »', async () => {
    const { fixture, element, bouton } = await ouvrir();
    await vi.waitFor(() => expect(bouton('Retirer')).toBeDefined());
    bouton('Retirer').click();
    await fixture.whenStable();
    expect(element.textContent).toContain('Retirer « Collège Ibn Khaldoun » ?');
    bouton('Oui').click();
    http.expectOne({ method: 'DELETE', url: `${ADRESSE}/e1` }).flush(null);
    await vi.waitFor(() => expect(element.textContent).toContain('Établissement retiré'));
    expect(element.querySelector('.nom')).toBeNull();
  });
});
