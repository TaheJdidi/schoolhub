import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NombreDeDemandes } from '../../socle/nombre-de-demandes';
import { Session } from '../../socle/session/session';

/** L'accueil, avec le nombre de demandes d'inscription à traiter (DEM-03). Les séances du jour viendront avec SEA-01. */
@Component({
  selector: 'sh-aujourdhui',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Bonjour {{ session.personne()?.prenom }}</h1>
        <p class="sub">Les séances du jour s'afficheront ici.</p>
      </div>
    </div>
    @if (demandes(); as nombre) {
      <a class="card demandes" routerLink="/demandes">
        <b>{{ nombre === 1 ? '1 demande d’inscription à traiter' : nombre + ' demandes d’inscription à traiter' }}</b>
        <small>Voir les demandes</small>
      </a>
    }
  `,
  styles: `
    .demandes { display: grid; gap: 2px; padding: 14px 16px; color: var(--ink); text-decoration: none; max-width: 420px; }
    .demandes:hover { background: var(--sunk); }
    .demandes small { color: var(--accent); }
  `,
})
export class Aujourdhui {
  protected readonly session = inject(Session);
  protected readonly demandes = inject(NombreDeDemandes).aTraiter;
}
