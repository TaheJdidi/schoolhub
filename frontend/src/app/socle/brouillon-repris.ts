import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { BrouillonDUnFormulaire } from './brouillons';

/** La note « Vous reprenez ce que vous aviez commencé », avec « Recommencer », au-dessus d'un formulaire repris. */
@Component({
  selector: 'sh-brouillon-repris',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (brouillon().repris()) {
      <p class="note brouillon" role="status">
        <span>{{ texte() }}</span>
        <button type="button" class="btn ghost" (click)="recommencer()">{{ action() }}</button>
      </p>
    }
  `,
  styles: `
    .brouillon { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; margin: 0 0 12px; }
    .brouillon .btn { padding: 4px 10px; }
  `,
})
export class BrouillonRepris {
  readonly brouillon = input.required<BrouillonDUnFormulaire>();
  readonly texte = input('Vous reprenez ce que vous aviez commencé.');
  /** « Recommencer » vide le formulaire ; « Abandonner » le referme, pour un formulaire qui s'ouvre sur la page. */
  readonly action = input('Recommencer');
  /** Après « Recommencer », pour ce que la page doit remettre elle-même. */
  readonly recommence = output<void>();

  protected recommencer(): void {
    this.brouillon().recommencer();
    this.recommence.emit();
  }
}
