import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CartePublique } from '../../cadre/carte-publique';
import { ChampMotDePasse, LONGUEUR_MINIMALE, MOT_DE_PASSE_TROP_COURT } from '../../socle/champ-mot-de-passe';
import { messageDErreur } from '../../socle/erreurs';
import { lireLeLienDeLEmail } from '../../socle/lien-de-l-email';
import { Moi } from '../../socle/session/moi';
import { Session } from '../../socle/session/session';

/** Ce que le serveur dit d'un lien d'invitation valable : à qui il est destiné. */
interface Accueil {
  prenom: string;
  centre: string;
}

/** CPT-02 : choisir son mot de passe depuis le lien d'invitation, puis être connecté. */
@Component({
  selector: 'sh-invitation',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, CartePublique, ChampMotDePasse],
  templateUrl: './invitation.html',
  styles: '.formulaire { display: grid; gap: 14px; } .note, p { margin: 0; }',
})
export class Invitation {
  private readonly http = inject(HttpClient);
  private readonly session = inject(Session);
  private readonly router = inject(Router);
  private readonly lien = lireLeLienDeLEmail();

  /** Le lien est vérifié à l'ouverture : un lien expiré affiche son message, sans formulaire. */
  protected readonly etat = signal<'verification' | 'formulaire' | 'refus'>('verification');
  protected readonly accueil = signal<Accueil | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly envoi = signal(false);
  protected readonly motDePasse = new FormControl('', { nonNullable: true });
  protected readonly formulaire = new FormGroup({ motDePasse: this.motDePasse });

  constructor() {
    this.verifierLeLien();
  }

  private async verifierLeLien(): Promise<void> {
    // L'adresse est écrite à la main : le jeton contient des « + », qui doivent partir en %2B
    // pour que le serveur ne les lise pas comme des espaces.
    const { compte, jeton } = this.lien;
    const adresse = `/api/invitation?compte=${encodeURIComponent(compte)}&jeton=${encodeURIComponent(jeton)}`;
    try {
      this.accueil.set(await firstValueFrom(this.http.get<Accueil>(adresse)));
      this.etat.set('formulaire');
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
      this.etat.set('refus');
    }
  }

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
      const moi = await firstValueFrom(this.http.post<Moi>('/api/invitation', { ...this.lien, motDePasse }));
      this.session.connaitre(moi);
      await this.router.navigateByUrl('/aujourdhui');
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
      // Le lien a été utilisé ou a expiré entre-temps : le formulaire ne servirait plus à rien.
      if (erreur instanceof HttpErrorResponse && erreur.status === 410) this.etat.set('refus');
    } finally {
      this.envoi.set(false);
    }
  }
}
