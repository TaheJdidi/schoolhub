import { HttpClient, HttpResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CartePublique } from '../../cadre/carte-publique';
import { ChampMotDePasse } from '../../socle/champ-mot-de-passe';
import { messageDErreur } from '../../socle/erreurs';
import { EtapeSuivante, Moi, premierePage } from '../../socle/session/moi';
import { adresseDeRetour } from '../../socle/session/retour';
import { Session } from '../../socle/session/session';
import { ChampEmail } from '../../socle/champs-de-contact';
import { requis } from '../../socle/champs-requis';

/** Le QR code de la première connexion de l'Éditeur, et sa clé à taper à la main. */
interface Activation {
  qrCode: string;
  cle: string;
}

const MESSAGES_DES_ETAPES: Record<EtapeSuivante['etape'], string> = {
  'activation-2fa': "Scannez le QR code avec votre application d'authentification, puis saisissez le code qu'elle affiche",
  'code-2fa': "Saisissez le code à 6 chiffres de votre application d'authentification",
};

/**
 * CPT-01 : se connecter avec son email et son mot de passe. L'Éditeur saisit ensuite le code de son application
 * d'authentification, après avoir scanné le QR code à sa première connexion (EDI-00).
 */
@Component({
  selector: 'sh-connexion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, ChampEmail, RouterLink, CartePublique, ChampMotDePasse],
  templateUrl: './connexion.html',
  styleUrl: './connexion.css',
})
export class Connexion implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  /** La page où revenir après la connexion (?retour=/caisse). */
  readonly retour = input<string>();
  /** Présent quand la session est tombée pendant le travail (?expiree=1). */
  readonly expiree = input<string>();
  /** L'étape où reprendre, après un mot de passe choisi depuis un email par l'Éditeur (?etape=activation-2fa). */
  readonly etape = input<string>();

  protected readonly formulaire = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: requis() }),
    motDePasse: new FormControl('', { nonNullable: true, validators: requis() }),
  });
  protected readonly code = new FormControl('', { nonNullable: true, validators: requis() });
  protected readonly formulaireDuCode = new FormGroup({ code: this.code });
  protected readonly envoi = signal(false);
  protected readonly message = signal<string | null>(null);

  /** L'étape de la double authentification de l'Éditeur, ou `null` pour le mot de passe. */
  protected readonly etapeEnCours = signal<EtapeSuivante | null>(null);
  protected readonly activation = signal<Activation | null>(null);

  ngOnInit(): void {
    const etape = this.etape();
    if (etape === 'activation-2fa' || etape === 'code-2fa') void this.passerA({ etape, message: MESSAGES_DES_ETAPES[etape] });
  }

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
      const reponse = await firstValueFrom(
        this.http.post<Moi | EtapeSuivante>('/api/connexion', { email, motDePasse }, { observe: 'response' }),
      );
      await this.suivre(reponse);
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  protected async confirmerLeCode(): Promise<void> {
    if (this.envoi()) return;
    const code = this.code.value.replace(/\s/g, '');
    if (!/^\d{6}$/.test(code)) {
      this.message.set('Le code a 6 chiffres');
      return;
    }

    this.envoi.set(true);
    this.message.set(null);
    try {
      const moi = await firstValueFrom(this.http.post<Moi>('/api/connexion/code-2fa', { code }));
      this.session.connaitre(moi);
      await this.router.navigateByUrl(this.destination(moi));
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
      this.code.setValue('');
    } finally {
      this.envoi.set(false);
    }
  }

  /** Revenir au mot de passe : l'étape a expiré, ou ce n'est pas le bon compte. */
  protected recommencer(): void {
    this.etapeEnCours.set(null);
    this.activation.set(null);
    this.code.setValue('');
    this.message.set(null);
  }

  private async suivre(reponse: HttpResponse<Moi | EtapeSuivante>): Promise<void> {
    if (reponse.status === 202) {
      await this.passerA(reponse.body as EtapeSuivante);
      return;
    }
    const moi = reponse.body as Moi;
    this.session.connaitre(moi);
    await this.router.navigateByUrl(this.destination(moi));
  }

  private async passerA(etape: EtapeSuivante): Promise<void> {
    this.etapeEnCours.set(etape);
    if (etape.etape !== 'activation-2fa') return;
    try {
      this.activation.set(await firstValueFrom(this.http.get<Activation>('/api/connexion/activation-2fa')));
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    }
  }

  /** La page demandée avant la connexion, si elle est dans l'espace de la personne ; sinon sa première page. */
  private destination(moi: Moi): string {
    const retour = adresseDeRetour(this.retour());
    const dansLeBackOffice = retour.startsWith('/editeur');
    return moi.role === 'Editeur' ? (dansLeBackOffice ? retour : premierePage(moi)) : dansLeBackOffice ? premierePage(moi) : retour;
  }
}
