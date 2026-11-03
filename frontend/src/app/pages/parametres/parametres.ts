import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CarteDuCentre } from './carte-du-centre';

/** CEN-02 et CEN-03 : la page « Paramètres du centre », réservée au propriétaire. Chaque carte a ses propres messages. */
@Component({
  selector: 'sh-parametres',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CarteDuCentre],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Paramètres du centre</h1>
        <p class="sub">Les informations et les prix de votre centre.</p>
      </div>
    </div>
    <div class="cartes">
      <sh-carte-du-centre />
    </div>
  `,
  styles: '.cartes { display: grid; gap: 16px; }',
})
export class Parametres {}
