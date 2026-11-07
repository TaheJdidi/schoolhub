import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { Brouillons, garderLeBrouillon, garderUnBrouillon } from './brouillons';

const ligne = () => new FormControl('', { nonNullable: true });

@Component({ selector: 'sh-page-avec-formulaire', template: '' })
class PageAvecFormulaire {
  readonly ouvert = signal(true);
  readonly formulaire = new FormGroup({ nom: new FormControl('', { nonNullable: true }), lignes: new FormArray([ligne()]) });
  readonly brouillon = garderLeBrouillon('essai', this.formulaire, {
    lignes: { lignes: ligne },
    aGarder: () => this.ouvert(),
    etat: { lire: () => this.ouvert(), ecrire: (ouvert) => this.ouvert.set(ouvert) },
  });
}

@Component({ selector: 'sh-page-avec-cases', template: '' })
class PageAvecCases {
  readonly coches = signal<string[]>([]);
  readonly brouillon = garderUnBrouillon('cases', {
    lire: () => this.coches(),
    ecrire: (coches) => this.coches.set(coches),
    aGarder: () => this.coches().length > 0,
  });
}

describe('les brouillons des formulaires', () => {
  const ouvrir = <T,>(page: new () => T): T => TestBed.createComponent(page).componentInstance;

  function visiter<T>(page: new () => T, saisir: (instance: T) => void): void {
    const fixture = TestBed.createComponent(page);
    saisir(fixture.componentInstance);
    fixture.destroy();
  }

  it('garde un formulaire changé en quittant la page, avec ses lignes, et le reprend en revenant', () => {
    visiter(PageAvecFormulaire, (page) => {
      page.formulaire.controls.lignes.push(ligne());
      page.formulaire.setValue({ nom: 'Karim', lignes: ['a', 'b'] });
      page.formulaire.markAsDirty();
    });

    const page = ouvrir(PageAvecFormulaire);
    expect(page.formulaire.getRawValue()).toEqual({ nom: '', lignes: [''] });
    expect(page.brouillon.reprendre()).toBe(true);
    expect(page.formulaire.getRawValue()).toEqual({ nom: 'Karim', lignes: ['a', 'b'] });
    expect(page.brouillon.repris()).toBe(true);

    page.brouillon.recommencer();
    expect(page.formulaire.getRawValue()).toEqual({ nom: '', lignes: [''] });
    expect(TestBed.inject(Brouillons).reprendre('essai')).toBeNull();
  });

  it('ne garde ni un formulaire laissé tel quel, ni un formulaire fermé, ni un formulaire envoyé', () => {
    visiter(PageAvecFormulaire, () => undefined);
    expect(TestBed.inject(Brouillons).reprendre('essai')).toBeNull();

    visiter(PageAvecFormulaire, (page) => {
      page.formulaire.controls.nom.setValue('Karim');
      page.formulaire.markAsDirty();
      page.ouvert.set(false);
    });
    expect(TestBed.inject(Brouillons).reprendre('essai')).toBeNull();

    visiter(PageAvecFormulaire, (page) => {
      page.formulaire.controls.nom.setValue('Karim');
      page.formulaire.markAsDirty();
      page.brouillon.oublier();
    });
    expect(TestBed.inject(Brouillons).reprendre('essai')).toBeNull();
  });

  it('garde les choix d’une page sans formulaire', () => {
    visiter(PageAvecCases, (page) => page.coches.set(['e1', 'e2']));

    const page = ouvrir(PageAvecCases);
    expect(page.brouillon.reprendre()).toBe(true);
    expect(page.coches()).toEqual(['e1', 'e2']);
    page.brouillon.recommencer();
    expect(page.coches()).toEqual([]);
  });

  it('oublie tout à la déconnexion', () => {
    const brouillons = TestBed.inject(Brouillons);
    brouillons.garder('essai', { nom: 'Karim' });
    brouillons.toutOublier();
    expect(brouillons.reprendre('essai')).toBeNull();
  });
});
