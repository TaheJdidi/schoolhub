import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { ICONES, NomDIcone } from './icones';

/** Une icône de la maquette, décorative : le texte à côté dit ce qu'elle veut dire. */
@Component({
  selector: 'sh-icone',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<svg
    class="lucide"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    [innerHTML]="contenu()"
  ></svg>`,
  styles: ':host { display: contents; }',
})
export class Icone {
  private readonly nettoyeur = inject(DomSanitizer);

  readonly nom = input.required<NomDIcone>();

  // Le contenu vient de nos propres constantes (icones.ts), jamais d'une saisie ni du serveur :
  // il peut passer sans le nettoyage d'Angular, qui retirerait les éléments SVG.
  protected readonly contenu = computed(() => this.nettoyeur.bypassSecurityTrustHtml(ICONES[this.nom()]));
}
