import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { FicheDuGroupe } from '../groupes/groupe';
import { ChangementDuCalendrier } from './appel';
import { CarteDuCalendrier } from './carte-du-calendrier';

describe('la carte du calendrier d’un groupe', () => {
  const FICHE: FicheDuGroupe = {
    id: 'g1',
    nom: 'Groupe Samedi 10h → 12h',
    type: 'Etude',
    professeur: { id: 'p1', prenom: 'Karim', nom: 'Ben Salah' },
    matiere: 'Mathématiques',
    niveau: { id: 'n9', nom: '9ᵉ année de base' },
    creneaux: [{ jour: 'Samedi', debut: '10:00', fin: '12:00' }],
    capacite: null,
    eleves: 2,
    dateDeDebut: '2027-09-18',
    prixDuMois: 40,
    mois: [],
    termine: false,
    premiereSeance: 1,
    prixTotal: null,
    nombreDeSeances: null,
    seances: [],
    tranches: [],
    fermeLe: null,
    motifDeFermeture: null,
    horsCompte: [
      { id: 's1', date: '2027-12-04', debut: '10:00', fin: '12:00', nature: 'Prevue', annulee: true, motif: 'Déplacée au 07/12/2027',
        remplaceeLe: '2027-12-07', appelFait: false },
      { id: 's2', date: '2027-12-02', debut: '17:00', fin: '19:00', nature: 'Supplementaire', annulee: false, motif: null,
        remplaceeLe: null, appelFait: false },
    ],
  };

  const CHANGEMENT: ChangementDuCalendrier = {
    texte: 'Bonjour, une séance supplémentaire de mathématiques est ajoutée le jeudi 09/12/2027, de 17h à 19h. Elle est offerte. Centre Les Jasmins.',
    eleves: [
      { eleveId: 'e1', prenom: 'Adem', nom: 'Miled', messages: [{ destinataire: 'Parent', telephone: '98 401 220', lien: 'https://wa.me/21698401220?text=a' }] },
    ],
  };

  let http: HttpTestingController;
  let fixture: ComponentFixture<CarteDuCalendrier>;
  let page: HTMLElement;
  let modifie: number;

  async function ouvrir(fiche: FicheDuGroupe, modifiable = true): Promise<void> {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CarteDuCalendrier);
    fixture.componentRef.setInput('fiche', fiche);
    fixture.componentRef.setInput('modifiable', modifiable);
    modifie = 0;
    fixture.componentInstance.modifie.subscribe(() => modifie++);
    page = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  const bouton = (texte: string) => [...page.querySelectorAll('button')].find((b) => b.textContent?.trim() === texte);
  const saisir = (selecteur: string, valeur: string, rang = 0) => {
    const champ = page.querySelectorAll(selecteur)[rang] as HTMLInputElement | HTMLSelectElement;
    champ.value = valeur;
    champ.dispatchEvent(new Event(champ instanceof HTMLSelectElement ? 'change' : 'input'));
  };

  it('montre les séances annulées et supplémentaires', async () => {
    await ouvrir(FICHE);

    const lignes = [...page.querySelectorAll('.hors-compte')].map((l) => l.textContent?.replace(/\s+/g, ' ').trim());
    expect(lignes[0]).toContain('samedi 04/12/2027');
    expect(lignes[0]).toContain('Annulée');
    expect(lignes[0]).toContain('Déplacée au 07/12/2027 · remplacée le 07/12');
    expect(lignes[1]).toContain('Supplémentaire');
    expect(page.querySelector('.hors-compte')?.getAttribute('href')).toBe('/professeurs/groupes/g1/appel/s1');
  });

  it('ajoute une séance supplémentaire, puis montre les messages', async () => {
    await ouvrir(FICHE);

    bouton('Ajouter une séance supplémentaire')!.click();
    await fixture.whenStable();
    saisir('input[type="date"]', '2027-12-09');
    saisir('input[type="time"]', '17:00', 0);
    saisir('input[type="time"]', '19:00', 1);
    bouton('Ajouter la séance')!.click();
    const requete = http.expectOne({ method: 'POST', url: '/api/groupes/g1/seances' });
    expect(requete.request.body).toEqual({ date: '2027-12-09', debut: '17:00', fin: '19:00' });
    requete.flush(CHANGEMENT);

    await vi.waitFor(() => expect(page.querySelector('.texte-du-message')?.textContent).toContain('séance supplémentaire'));
    expect(page.textContent).toContain('Adem Miled');
    expect(page.querySelector('.destinataire a')?.getAttribute('href')).toBe('https://wa.me/21698401220?text=a');
    expect(modifie).toBe(1);
  });

  it("change l'horaire à partir d'une date, avec les créneaux du groupe proposés", async () => {
    await ouvrir(FICHE);

    bouton("Changer l'horaire du groupe")!.click();
    await fixture.whenStable();
    saisir('select', 'Mardi');
    saisir('.creneau input[type="time"]', '17:00', 0);
    saisir('.creneau input[type="time"]', '19:00', 1);
    saisir('input[type="date"]', '2028-01-08');
    bouton("Changer l'horaire")!.click();
    const requete = http.expectOne({ method: 'POST', url: '/api/groupes/g1/horaire' });
    expect(requete.request.body).toEqual({ creneaux: [{ jour: 'Mardi', debut: '17:00', fin: '19:00' }], aPartirDu: '2028-01-08' });
    requete.flush(CHANGEMENT);

    await vi.waitFor(() => expect(modifie).toBe(1));
  });

  it('montre le refus du serveur', async () => {
    await ouvrir(FICHE);

    bouton("Changer l'horaire du groupe")!.click();
    await fixture.whenStable();
    saisir('input[type="date"]', '2027-11-27');
    bouton("Changer l'horaire")!.click();
    http.expectOne({ method: 'POST', url: '/api/groupes/g1/horaire' }).flush(
      { message: 'Le nouvel horaire part au plus tôt demain' },
      { status: 400, statusText: 'Bad Request' },
    );

    await vi.waitFor(() => expect(page.querySelector('[role="alert"]')?.textContent).toContain('Le nouvel horaire part au plus tôt demain'));
    expect(modifie).toBe(0);
  });

  it('ne propose aucun changement pour un groupe fermé', async () => {
    await ouvrir({ ...FICHE, horsCompte: [] }, false);

    expect(page.textContent).toContain('Aucune séance annulée ni supplémentaire.');
    expect(bouton('Ajouter une séance supplémentaire')).toBeUndefined();
  });
});
