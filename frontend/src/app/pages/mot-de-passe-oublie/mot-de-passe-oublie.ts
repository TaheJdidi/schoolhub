import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CartePublique } from '../../cadre/carte-publique';
import { messageDErreur } from '../../socle/erreurs';

/**
 * CPT-03 : demander un lien pour changer son mot de passe. Le serveur répond toujours le même message,
 * pour ne pas révéler qui a un compte : il remplace le formulaire.
 */
@Component({
  selector: 'sh-mot-de-passe-oublie',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, CartePublique],
  templateUrl: './mot-de-passe-oublie.html',
  styles: '.formulaire { display: grid; gap: 14px; } .note { margin: 0; } .retour { justify-self: center; font-size: 13px; color: var(--accent); }',
})
export class MotDePasseOublie {
  private readonly http = inject(HttpClient);

  protected readonly email = new FormControl('', { nonNullable: true });
  protected readonly formulaire = new FormGroup({ email: this.email });
  protected readonly envoi = signal(false);
  protected readonly message = signal<string | null>(null);
  /** La réponse du serveur, une fois la demande partie. */
  protected readonly reponse = signal<string | null>(null);

  protected async demander(): Promise<void> {
    if (this.envoi()) return;

    const email = this.email.value.trim();
    if (!email) {
      this.message.set('Saisissez votre email');
      return;
    }

    this.envoi.set(true);
    this.message.set(null);
    try {
      const avis = await firstValueFrom(this.http.post<{ message: string }>('/api/mot-de-passe-oublie', { email }));
      this.reponse.set(avis.message);
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
