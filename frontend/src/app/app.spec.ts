import { formatDate } from '@angular/common';
import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { appConfig } from './app.config';

describe('App', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: appConfig.providers });
  });

  it("règle l'application en français", () => {
    expect(TestBed.inject(LOCALE_ID)).toBe('fr');
    expect(formatDate(new Date(2026, 10, 3), 'EEEE d MMMM y', 'fr')).toBe('mardi 3 novembre 2026');
  });

  it("n'affiche que les pages", async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const racine = fixture.nativeElement as HTMLElement;
    expect(racine.children.length).toBe(1);
    expect(racine.querySelector('router-outlet')).not.toBeNull();
  });
});
