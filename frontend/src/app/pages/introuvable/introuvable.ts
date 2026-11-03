import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Une adresse qui ne mène à aucune page. */
@Component({
  selector: 'sh-introuvable',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Cette page n'existe pas</h1>
        <p class="sub"><a class="lnk" routerLink="/aujourdhui">Aujourd'hui</a></p>
      </div>
    </div>
  `,
})
export class Introuvable {}
