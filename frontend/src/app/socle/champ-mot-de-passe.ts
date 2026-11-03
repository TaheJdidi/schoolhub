import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Icone } from './icone';

/** La seule règle d'un nouveau mot de passe (CPT-02), vérifiée aussi par le serveur, avec le même texte. */
export const LONGUEUR_MINIMALE = 8;
export const MOT_DE_PASSE_TROP_COURT = 'Le mot de passe doit faire au moins 8 caractères';

let numero = 0;

/**
 * Un seul champ de mot de passe, avec « Afficher » : sur téléphone, c'est plus simple que de le taper
 * deux fois, et on voit ses fautes de frappe.
 */
@Component({
  selector: 'sh-champ-mot-de-passe',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, Icone],
  templateUrl: './champ-mot-de-passe.html',
  styles: `
    .saisie { display: flex; gap: 6px; }
    .saisie input { flex: 1; min-width: 0; }
  `,
})
export class ChampMotDePasse {
  readonly controle = input.required<FormControl<string>>();
  readonly libelle = input('Mot de passe');
  readonly autocomplete = input<'current-password' | 'new-password'>('current-password');

  protected readonly identifiant = `mot-de-passe-${++numero}`;
  protected readonly visible = signal(false);
}
