import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderLeBrouillon } from '../../socle/brouillons';
import { requis } from '../../socle/champs-requis';
import { messageDErreur } from '../../socle/erreurs';
import { libelleDuRole } from '../../socle/personnes';
import { Role } from '../../socle/session/moi';
import { Session } from '../../socle/session/session';
import { CompteDuCentre, ETATS } from './compte-du-centre';

/**
 * CPT-04 et CPT-05 : le propriétaire invite une personne, renvoie une invitation, désactive ou réactive un compte.
 * La liste se charge une fois à l'ouverture. Après une action, la ligne prend le compte renvoyé par le serveur ;
 * après une invitation, la liste est rechargée.
 */
@Component({
  selector: 'sh-comptes',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, BrouillonRepris],
  templateUrl: './comptes.html',
  styleUrl: './comptes.css',
})
export class Comptes implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly session = inject(Session);

  protected readonly etats = ETATS;
  protected readonly libelleDuRole = libelleDuRole;
  protected readonly centre = computed(() => this.session.personne()?.centre ?? '');

  protected readonly comptes = signal<CompteDuCentre[]>([]);
  protected readonly formulaireOuvert = signal(false);
  /** Le compte dont la désactivation attend « Oui » ou « Non ». */
  protected readonly aConfirmer = signal<string | null>(null);
  /** Ce que l'action en cours bloque : « invitation », ou les boutons d'un compte (son identifiant). */
  protected readonly enCours = signal<string | null>(null);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  protected readonly formulaire = new FormGroup({
    prenom: new FormControl('', { nonNullable: true, validators: requis() }),
    nom: new FormControl('', { nonNullable: true, validators: requis() }),
    email: new FormControl('', { nonNullable: true, validators: requis() }),
    role: new FormControl<Role>('Secretaire', { nonNullable: true }),
  });

  /** Une invitation commencée, puis laissée pour une autre page, se retrouve en revenant. */
  protected readonly brouillon = garderLeBrouillon('invitation', this.formulaire, {
    aGarder: () => this.formulaireOuvert(),
    etat: { lire: () => this.formulaireOuvert(), ecrire: (ouvert) => this.formulaireOuvert.set(ouvert) },
  });

  async ngOnInit(): Promise<void> {
    await this.charger();
    this.brouillon.reprendre();
  }

  /** « Sonia Mabrouk », et « (vous) » après le nom de la personne connectée. */
  protected nomAffiche(compte: CompteDuCentre): string {
    return `${compte.prenom} ${compte.nom}${compte.moi ? ' (vous)' : ''}`;
  }

  protected ouvrirLeFormulaire(): void {
    this.formulaireOuvert.set(true);
    this.succes.set(null);
    this.erreur.set(null);
  }

  protected fermerLeFormulaire(): void {
    this.brouillon.oublier();
    this.formulaireOuvert.set(false);
    this.formulaire.reset();
    this.erreur.set(null);
  }

  protected async inviter(): Promise<void> {
    if (this.enCours()) return;

    const { prenom, nom, email, role } = this.formulaire.getRawValue();
    const invitation = { prenom: prenom.trim(), nom: nom.trim(), email: email.trim(), role };
    if (!invitation.prenom || !invitation.nom || !invitation.email) {
      this.erreur.set("Remplissez le prénom, le nom et l'email");
      return;
    }

    this.commencer('invitation');
    try {
      const compte = await firstValueFrom(this.http.post<CompteDuCentre>('/api/comptes', invitation));
      this.fermerLeFormulaire();
      await this.charger();
      this.succes.set(`Invitation envoyée à ${compte.email}`);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.enCours.set(null);
    }
  }

  protected renvoyerLInvitation(compte: CompteDuCentre): Promise<void> {
    return this.agir(compte, 'invitation', (c) => `Nouvelle invitation envoyée à ${c.email}`);
  }

  protected demanderConfirmation(compte: CompteDuCentre): void {
    this.aConfirmer.set(compte.id);
  }

  protected annulerLaConfirmation(): void {
    this.aConfirmer.set(null);
  }

  protected desactiver(compte: CompteDuCentre): Promise<void> {
    this.aConfirmer.set(null);
    return this.agir(compte, 'desactivation', (c) => `Compte de ${c.prenom} ${c.nom} désactivé`);
  }

  protected reactiver(compte: CompteDuCentre): Promise<void> {
    return this.agir(compte, 'reactivation', (c) => `Compte de ${c.prenom} ${c.nom} réactivé`);
  }

  private async charger(): Promise<void> {
    try {
      this.comptes.set(await firstValueFrom(this.http.get<CompteDuCentre[]>('/api/comptes')));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** Une action sur un compte : la ligne prend le compte renvoyé par le serveur, et le message le confirme. */
  private async agir(
    compte: CompteDuCentre,
    action: 'invitation' | 'desactivation' | 'reactivation',
    confirmation: (aJour: CompteDuCentre) => string,
  ): Promise<void> {
    if (this.enCours()) return;

    this.commencer(compte.id);
    try {
      const aJour = await firstValueFrom(this.http.post<CompteDuCentre>(`/api/comptes/${compte.id}/${action}`, null));
      this.comptes.update((liste) => liste.map((c) => (c.id === aJour.id ? aJour : c)));
      this.succes.set(confirmation(aJour));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.enCours.set(null);
    }
  }

  private commencer(action: string): void {
    this.enCours.set(action);
    this.succes.set(null);
    this.erreur.set(null);
  }
}
