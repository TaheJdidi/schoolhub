import { TestBed } from '@angular/core/testing';
import { DernieresPages, sectionDe } from './dernieres-pages';

describe('la dernière page de chaque section', () => {
  it('rouvre la dernière page ouverte dans une section, même après un rechargement', () => {
    const pages = TestBed.inject(DernieresPages);
    expect(pages.derniere('professeurs')).toBe('/professeurs');

    pages.noter('/professeurs');
    pages.noter('/professeurs/groupes/nouveau?professeur=k');
    pages.noter('/eleves');

    expect(pages.derniere('professeurs')).toBe('/professeurs/groupes/nouveau?professeur=k');
    expect(pages.derniere('eleves')).toBe('/eleves');
    TestBed.resetTestingModule();
    expect(TestBed.inject(DernieresPages).derniere('professeurs')).toBe('/professeurs/groupes/nouveau?professeur=k');
  });

  it('oublie tout à la déconnexion', () => {
    const pages = TestBed.inject(DernieresPages);
    pages.noter('/professeurs/groupes/nouveau');
    pages.toutOublier();
    expect(pages.derniere('professeurs')).toBe('/professeurs');
    expect(sessionStorage.getItem('schoolhub.dernieres-pages')).toBeNull();
  });

  it('trouve la section d’une adresse', () => {
    expect(sectionDe('/professeurs/groupes/nouveau?professeur=k')).toBe('professeurs');
    expect(sectionDe('/caisse?jour=2027-11-27')).toBe('caisse');
  });
});
