import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CartePublique } from '../../cadre/carte-publique';
import { ChampMotDePasse, LONGUEUR_MINIMALE, MOT_DE_PASSE_TROP_COURT } from '../../socle/champ-mot-de-passe';
import { messageDErreur } from '../../socle/erreurs';
import { lireLeLienDeLEmail } from '../../socle/lien-de-l-email';
import { Moi } from '../../socle/session/moi';
import { Session } from '../../socle/session/session';

/**
 * CPT-03 : choisir un nouveau mot de passe depuis le lien de l'email, puis être connecté.
 * Le serveur n'a pas d'adresse pour vérifier ce lien à l'ouverture : un lien expiré se voit à l'envoi.
 */
@Component({
  selector: 'sh-nouveau-mot-de-passe',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, CartePublique, ChampMotDePasse],
  templateUrl: './nouveau-mot-de-passe.html',
  styles: '.formulaire { display: grid; gap: 14px; } .note { margin: 0; } .lnk { justify-self: center; font-size: 13px; color: var(--accent); }',
})
export class NouveauMotDePasse {
  private readonly http = inject(HttpClient);
  private readonly session = inject(Session);
  private readonly router = inject(Router);
  private readonly lien = lireLeLienDeLEmail();

  protected readonly expire = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly envoi = signal(false);
  protected readonly motDePasse = new FormControl('', { nonNullable: true });
  protected readonly formulaire = new FormGroup({ motDePasse: this.motDePasse });

  protected async choisir(): Promise<void> {
    if (this.envoi()) return;

    const motDePasse = this.motDePasse.value;
    if (motDePasse.length < LONGUEUR_MINIMALE) {
      this.message.set(MOT_DE_PASSE_TROP_COURT);
      return;
    }

    this.envoi.set(true);
    this.message.set(null);
    try {
      const moi = await firstValueFrom(
        this.http.post<Moi>('/api/nouveau-mot-de-passe', { ...this.lien, motDePasse }),
      );
      this.session.connaitre(moi);
      await this.router.navigateByUrl('/aujourdhui');
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
      if (erreur instanceof HttpErrorResponse && erreur.status === 410) this.expire.set(true);
    } finally {
      this.envoi.set(false);
    }
  }
}
