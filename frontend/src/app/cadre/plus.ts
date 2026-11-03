import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icone } from '../socle/icone';
import { menuDe } from '../socle/menu';
import { Session } from '../socle/session/session';
import { BoutonDeDeconnexion } from './bouton-de-deconnexion';

/** L'onglet « Plus » du téléphone : tout le menu de la personne, puis « Se déconnecter ». */
@Component({
  selector: 'sh-plus',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icone, BoutonDeDeconnexion],
  template: `
    <div class="pagehead"><div><h1 class="h1">Plus</h1></div></div>
    <nav class="card liste" aria-label="Menu">
      @for (entree of menu(); track entree.chemin) {
        @if ($index === premiereReservee()) {
          <p class="titre">Propriétaire</p>
        }
        <a class="ligne" [routerLink]="'/' + entree.chemin">
          <sh-icone [nom]="entree.icone" />
          <span>{{ entree.libelle }}</span>
        </a>
      }
    </nav>
    <sh-bouton-de-deconnexion class="fin" />
  `,
  styles: `
    .liste { display: grid; padding: 6px; }
    .ligne { display: flex; align-items: center; gap: 10px; padding: 11px 10px; border-radius: var(--r);
      color: var(--ink); text-decoration: none; }
    .ligne:hover { background: var(--sunk); }
    .titre { margin: 12px 10px 4px; font-size: 11px; letter-spacing: .07em; text-transform: uppercase;
      color: var(--ink-3); font-weight: 600; }
    .fin { margin-top: 12px; }
  `,
})
export class Plus {
  private readonly session = inject(Session);

  protected readonly menu = computed(() => menuDe(this.session.personne()?.role ?? 'Secretaire'));

  /** Le titre « Propriétaire » se place avant la première entrée réservée (-1 : aucune). */
  protected readonly premiereReservee = computed(() => this.menu().findIndex((entree) => entree.reservee));
}
