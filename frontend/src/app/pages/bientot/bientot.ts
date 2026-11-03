import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Une page du menu pas encore construite : la story qui la fait la remplacera à son adresse. */
@Component({
  selector: 'sh-bientot',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">{{ titre() }}</h1>
        <p class="sub">Cette page arrive bientôt</p>
      </div>
    </div>
  `,
})
export class Bientot {
  /** Le libellé du menu, donné par l'adresse (data.titre). */
  readonly titre = input.required<string>();
}
