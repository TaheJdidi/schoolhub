import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { messageDErreur } from '../socle/erreurs';
import { Icone } from '../socle/icone';
import { Session } from '../socle/session/session';

/** « Se déconnecter », en bas du menu sur ordinateur et de la page « Plus » sur téléphone. */
@Component({
  selector: 'sh-bouton-de-deconnexion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icone],
  template: `
    <button type="button" class="deconnexion" [disabled]="envoi()" (click)="seDeconnecter()">
      <sh-icone nom="log-out" />
      <span>Se déconnecter</span>
    </button>
    @if (message(); as message) {
      <p class="note bad" role="alert">{{ message }}</p>
    }
  `,
  styles: `
    :host { display: grid; gap: 6px; }
    .deconnexion { display: flex; align-items: center; gap: 10px; padding: 8px 10px; border-radius: var(--r);
      color: var(--ink-2); background: none; border: 0; width: 100%; text-align: left; cursor: pointer; }
    .deconnexion:hover { background: var(--sunk); color: var(--ink); }
    .note { margin: 0; }
  `,
})
export class BoutonDeDeconnexion {
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  protected readonly envoi = signal(false);
  protected readonly message = signal<string | null>(null);

  protected async seDeconnecter(): Promise<void> {
    this.envoi.set(true);
    this.message.set(null);
    try {
      await this.session.seDeconnecter();
      // La page de connexion remplace la page en cours : « retour » ne ramène pas à une page du cadre.
      await this.router.navigateByUrl('/connexion', { replaceUrl: true });
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
