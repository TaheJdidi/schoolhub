import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import {
  ChampEmail,
  ChampTelephone,
  EMAIL_PAS_VALABLE,
  estUnEmail,
  grouperUnTelephone,
  lireUnTelephone,
  TELEPHONE_PAS_VALABLE,
} from './champs-de-contact';

describe('lireUnTelephone', () => {
  it('lit 8 chiffres, avec ou sans espaces, tirets, points ou indicatif', () => {
    expect(lireUnTelephone('22345678')).toBe('22345678');
    expect(lireUnTelephone('22 345 678')).toBe('22345678');
    expect(lireUnTelephone('22-345.678')).toBe('22345678');
    expect(lireUnTelephone('+216 98 401 220')).toBe('98401220');
    expect(lireUnTelephone('00216 98401220')).toBe('98401220');
  });

  it('rend null pour un champ vide, undefined pour un numéro qui ne se lit pas', () => {
    expect(lireUnTelephone('')).toBeNull();
    expect(lireUnTelephone('  ')).toBeNull();
    expect(lireUnTelephone('22 345 67')).toBeUndefined();
    expect(lireUnTelephone('22 345 6789')).toBeUndefined();
    expect(lireUnTelephone('22a45678')).toBeUndefined();
  });
});

describe('grouperUnTelephone', () => {
  it('groupe les chiffres comme on les lit, même incomplets', () => {
    expect(grouperUnTelephone('22')).toBe('22');
    expect(grouperUnTelephone('2234')).toBe('22 34');
    expect(grouperUnTelephone('223456')).toBe('22 345 6');
    expect(grouperUnTelephone('22345678')).toBe('22 345 678');
  });
});

describe('estUnEmail', () => {
  it('accepte une adresse avec un domaine qui a un point', () => {
    expect(estUnEmail('sonia@exemple.tn')).toBe(true);
    expect(estUnEmail(' sonia.ben-ali@ecole.com.tn ')).toBe(true);
  });

  it('refuse une adresse sans @, sans point ou avec des espaces', () => {
    expect(estUnEmail('sonia')).toBe(false);
    expect(estUnEmail('sonia@exemple')).toBe(false);
    expect(estUnEmail('sonia@@exemple.tn')).toBe(false);
    expect(estUnEmail('so nia@exemple.tn')).toBe(false);
    expect(estUnEmail(`${'a'.repeat(250)}@exemple.tn`)).toBe(false);
  });
});

@Component({
  imports: [ReactiveFormsModule, ChampTelephone, ChampEmail],
  template: `<form><label class="f">Téléphone <input shTelephone [formControl]="telephone" /></label>
    <label class="f">Email <input shEmail [formControl]="email" /></label></form>`,
})
class Formulaire {
  readonly telephone = new FormControl('', { nonNullable: true });
  readonly email = new FormControl('', { nonNullable: true });
}

describe('ChampTelephone et ChampEmail', () => {
  function monter() {
    const fixture = TestBed.createComponent(Formulaire);
    fixture.detectChanges();
    const [telephone, email] = Array.from(fixture.nativeElement.querySelectorAll('input')) as HTMLInputElement[];
    const taper = (champ: HTMLInputElement, texte: string) => {
      champ.value = texte;
      champ.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    const sortir = (champ: HTMLInputElement) => {
      champ.dispatchEvent(new Event('blur'));
      fixture.detectChanges();
    };
    return { fixture, composant: fixture.componentInstance, telephone, email, taper, sortir };
  }

  it('laisse les champs vides, sans exemple qu’on prendrait pour un vrai numéro', () => {
    const { telephone, email } = monter();
    expect(telephone.placeholder).toBe('');
    expect(email.placeholder).toBe('');
  });

  it('groupe les chiffres du numéro, sans lettres ni plus de 8 chiffres, et retire l’indicatif', () => {
    const { composant, telephone, taper } = monter();
    taper(telephone, '22a3456');
    expect(telephone.value).toBe('22 345 6');
    taper(telephone, '2234567899');
    expect(telephone.value).toBe('22 345 678');
    expect(composant.telephone.value).toBe('22 345 678');
    taper(telephone, '+21698401220');
    expect(telephone.value).toBe('98 401 220');
  });

  it('dit sous le champ, en sortant, que le numéro n’a pas 8 chiffres, jusqu’à ce qu’il soit corrigé', () => {
    const { fixture, composant, telephone, taper, sortir } = monter();
    taper(telephone, '22345');
    expect(fixture.nativeElement.textContent).not.toContain(TELEPHONE_PAS_VALABLE);
    sortir(telephone);
    expect(fixture.nativeElement.textContent).toContain(TELEPHONE_PAS_VALABLE);
    expect(telephone.getAttribute('aria-invalid')).toBe('true');
    expect(composant.telephone.hasError('telephone')).toBe(true);

    taper(telephone, '22345678');
    expect(fixture.nativeElement.textContent).not.toContain(TELEPHONE_PAS_VALABLE);
    expect(composant.telephone.valid).toBe(true);
  });

  it('enlève les espaces d’un email et dit, en sortant, qu’il ne se lit pas', () => {
    const { fixture, composant, email, taper, sortir } = monter();
    taper(email, 'sonia @exemple');
    expect(email.value).toBe('sonia@exemple');
    sortir(email);
    expect(fixture.nativeElement.textContent).toContain(EMAIL_PAS_VALABLE);
    expect(composant.email.hasError('email')).toBe(true);

    taper(email, 'sonia@exemple.tn');
    expect(fixture.nativeElement.textContent).not.toContain(EMAIL_PAS_VALABLE);
  });

  it('laisse un champ vide sans message : c’est « requis » qui dit s’il est obligatoire', () => {
    const { fixture, composant, telephone, email, sortir } = monter();
    sortir(telephone);
    sortir(email);
    expect(fixture.nativeElement.querySelector('.erreur-du-champ')).toBeNull();
    expect(composant.telephone.valid).toBe(true);
    expect(composant.email.valid).toBe(true);
  });
});

@Component({
  imports: [ReactiveFormsModule, ChampTelephone],
  template: `<form [formGroup]="formulaire"><input shTelephone formControlName="telephone" /></form>`,
})
class FormulaireParNom {
  readonly formulaire = new FormGroup({ telephone: new FormControl('', { nonNullable: true }) });
}

describe('ChampTelephone avec formControlName', () => {
  it('enlève le message dès que le numéro est corrigé, sans attendre de sortir du champ', () => {
    const fixture = TestBed.createComponent(FormulaireParNom);
    fixture.detectChanges();
    const champ = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    const taper = (texte: string) => {
      champ.value = texte;
      champ.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    taper('98 401 2');
    champ.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(TELEPHONE_PAS_VALABLE);

    taper('98 401 220');
    expect(fixture.nativeElement.textContent).not.toContain(TELEPHONE_PAS_VALABLE);
    expect(fixture.componentInstance.formulaire.value.telephone).toBe('98 401 220');

    // Une valeur remise par la page (un brouillon repris) met aussi le message à jour.
    fixture.componentInstance.formulaire.setValue({ telephone: '1234' });
    expect(fixture.nativeElement.textContent).toContain(TELEPHONE_PAS_VALABLE);
  });
});
