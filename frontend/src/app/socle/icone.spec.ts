import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Icone } from './icone';

@Component({ imports: [Icone], template: '<sh-icone nom="calendar" />' })
class Hote {}

describe('Icone', () => {
  it("dessine l'icône de la maquette, cachée aux lecteurs d'écran", async () => {
    const fixture = TestBed.createComponent(Hote);
    await fixture.whenStable();

    const svg = (fixture.nativeElement as HTMLElement).querySelector('svg.lucide');
    expect(svg).not.toBeNull();
    expect(svg?.getAttribute('aria-hidden')).toBe('true');
    expect(svg?.querySelector('path')).not.toBeNull();
  });
});
