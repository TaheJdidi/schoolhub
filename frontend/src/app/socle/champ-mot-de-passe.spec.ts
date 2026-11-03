import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { ChampMotDePasse } from './champ-mot-de-passe';

@Component({
  imports: [ChampMotDePasse],
  template: '<sh-champ-mot-de-passe [controle]="controle" />',
})
class Hote {
  readonly controle = new FormControl('', { nonNullable: true });
}

describe('le champ de mot de passe', () => {
  async function creer() {
    const fixture = TestBed.createComponent(Hote);
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    return {
      fixture,
      champ: page.querySelector('input') as HTMLInputElement,
      bouton: page.querySelector('button') as HTMLButtonElement,
    };
  }

  it('cache le mot de passe, et « Afficher » le montre', async () => {
    const { fixture, champ, bouton } = await creer();
    expect(champ.type).toBe('password');
    expect(bouton.textContent).toContain('Afficher');
    expect(bouton.type).toBe('button');

    bouton.click();
    await fixture.whenStable();

    expect(champ.type).toBe('text');
    expect(bouton.textContent).toContain('Masquer');
    expect(bouton.getAttribute('aria-pressed')).toBe('true');
  });

  it('écrit dans son contrôle', async () => {
    const { fixture, champ } = await creer();

    champ.value = 'jasmins-2027';
    champ.dispatchEvent(new Event('input'));

    expect(fixture.componentInstance.controle.value).toBe('jasmins-2027');
  });
});
