import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Session } from '../../socle/session/session';

/** L'accueil. Les séances du jour viendront avec SEA-01. */
@Component({
  selector: 'sh-aujourdhui',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Bonjour {{ session.personne()?.prenom }}</h1>
        <p class="sub">Les séances du jour s'afficheront ici.</p>
      </div>
    </div>
  `,
})
export class Aujourdhui {
  protected readonly session = inject(Session);
}
