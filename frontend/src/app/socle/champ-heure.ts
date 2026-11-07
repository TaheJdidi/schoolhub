import { Directive, inject } from '@angular/core';
import { NgControl, ValidatorFn } from '@angular/forms';

/**
 * Une heure écrite à la main : « 8 », « 8h », « 08:00 », « 8h30 », « 830 ». Rend « 08:00 », « 08:30 » ; `null` si rien
 * n'est écrit, `undefined` si l'heure ne se lit pas (« 25h », « 8h7 »). Sans minutes, ce sont 00 minutes.
 */
export function lireUneHeure(saisie: string): string | null | undefined {
  const texte = saisie.trim().toLowerCase().replace(/\s+/g, '');
  if (texte === '') return null;
  const lu = /^(\d{1,2})(?:[h:.](\d{2})?)?$/.exec(texte) ?? /^(\d{1,2})(\d{2})$/.exec(texte);
  if (!lu) return undefined;
  const heures = Number(lu[1]);
  const minutes = Number(lu[2] ?? 0);
  if (heures > 23 || minutes > 59) return undefined;
  return `${String(heures).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`;
}

/** Une heure écrite mais illisible est invalide : le formulaire envoyé l'entoure de rouge. */
export const uneHeure: ValidatorFn = (champ) => (lireUneHeure(`${champ.value ?? ''}`) === undefined ? { heure: true } : null);

/**
 * `<input shHeure formControlName="debut">` : on écrit l'heure seule (« 10 »), les minutes se mettent à 00 en sortant
 * du champ (« 10:00 »). Un champ texte plutôt qu'un champ d'heure du navigateur, qui laisse les minutes vides.
 */
@Directive({
  selector: 'input[shHeure]',
  host: { type: 'text', inputmode: 'numeric', autocomplete: 'off', placeholder: '8h30', '(blur)': 'completer()' },
})
export class ChampHeure {
  private readonly champ = inject(NgControl, { self: true });

  protected completer(): void {
    const controle = this.champ.control;
    if (!controle) return;
    const heure = lireUneHeure(`${controle.value ?? ''}`);
    if (heure && heure !== controle.value) controle.setValue(heure);
  }
}
