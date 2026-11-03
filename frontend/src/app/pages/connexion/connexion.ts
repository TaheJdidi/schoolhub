import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CartePublique } from '../../cadre/carte-publique';
import { ChampMotDePasse } from '../../socle/champ-mot-de-passe';
import { messageDErreur } from '../../socle/erreurs';
import { Moi } from '../../socle/session/moi';
import { adresseDeRetour } from '../../socle/session/retour';
import { Session } from '../../socle/session/session';

/** CPT-01 : se connecter avec son email et son mot de passe. */
@Component({
  selector: 'sh-connexion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, CartePublique, ChampMotDePasse],
  templateUrl: './connexion.html',
  styleUrl: './connexion.css',
})
export class Connexion {
  private readonly http = inject(HttpClient);
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  /** La page où revenir après la connexion (?retour=/caisse). */
  readonly retour = input<string>();
  /** Présent quand la session est tombée pendant le travail (?expiree=1). */
  readonly expiree = input<string>();

  protected readonly formulaire = new FormGroup({
    email: new FormControl('', { nonNullable: true }),
    motDePasse: new FormControl('', { nonNullable: true }),
  });
  protected readonly envoi = signal(false);
  protected readonly message = signal<string | null>(null);

  protected async seConnecter(): Promise<void> {
    if (this.envoi()) return;

    const email = this.formulaire.controls.email.value.trim();
    const motDePasse = this.formulaire.controls.motDePasse.value;
    if (!email || !motDePasse) {
      this.message.set('Saisissez votre email et votre mot de passe');
      return;
    }

    this.envoi.set(true);
    this.message.set(null);
    try {
      const moi = await firstValueFrom(this.http.post<Moi>('/api/connexion', { email, motDePasse }));
      this.session.connaitre(moi);
      await this.router.navigateByUrl(adresseDeRetour(this.retour()));
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
