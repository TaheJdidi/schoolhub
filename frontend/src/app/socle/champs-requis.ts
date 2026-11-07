import { AbstractControl, FormArray, FormGroup, ValidatorFn } from '@angular/forms';

/**
 * Un champ obligatoire. Vide, il est invalide : une fois le formulaire envoyé (`form.ng-submitted`), le style commun
 * l'entoure de rouge, jusqu'à ce qu'il soit rempli. `quand` le rend obligatoire seulement dans certains cas (le niveau,
 * sauf pour une Formation). Le message de la page reste celui de la page : ce contour montre seulement où écrire.
 */
export function requis(quand?: () => boolean): ValidatorFn {
  return (champ) => ((quand && !quand()) || `${champ.value ?? ''}`.trim() !== '' ? null : { requis: true });
}

/**
 * Les cas de `requis(quand)` ont pu changer sans que le champ change (le type du groupe, la classe de l'élève) :
 * chaque champ du formulaire se revérifie, sans prévenir ceux qui suivent ses valeurs.
 */
export function reverifier(controle: AbstractControl): void {
  if (controle instanceof FormGroup || controle instanceof FormArray) Object.values(controle.controls).forEach(reverifier);
  controle.updateValueAndValidity({ onlySelf: true, emitEvent: false });
}
