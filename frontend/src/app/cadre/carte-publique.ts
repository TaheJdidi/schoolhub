import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * La forme des pages sans connexion (connexion, invitation, mots de passe) :
 * le logo SchoolHub au-dessus d'une carte centrée, la même sur téléphone et sur ordinateur.
 */
@Component({
  selector: 'sh-carte-publique',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="colonne">
        <p class="marque"><span class="logo" aria-hidden="true">SH</span><span>SchoolHub</span></p>
        <div class="card carte"><ng-content /></div>
      </div>
    </div>
  `,
  styles: `
    .page { min-height: 100dvh; display: grid; place-items: center; padding: 24px 16px; }
    .colonne { width: 100%; max-width: 360px; }
    .marque { display: flex; align-items: center; justify-content: center; gap: 10px; margin: 0 0 16px;
      font-family: var(--font-display); font-weight: 600; font-size: 18px; }
    .logo { width: 34px; height: 34px; border-radius: 9px; background: var(--accent); color: var(--on-accent);
      display: grid; place-items: center; font-size: 14px; letter-spacing: .02em; }
    .carte { padding: 20px; display: grid; gap: 14px; }
  `,
})
export class CartePublique {}
