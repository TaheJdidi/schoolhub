import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, FormGroupDirective, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ETATS_DE_L_ABONNEMENT } from '../../socle/abonnement';
import {
  ChampEmail,
  ChampTelephone,
  estUnEmail,
  grouperUnTelephone,
  lireUnTelephone,
} from '../../socle/champs-de-contact';
import { requis } from '../../socle/champs-requis';
import { messageDErreur } from '../../socle/erreurs';
import { ecrireUnMontant } from '../../socle/montants';
import {
  aujourdhui,
  CANAUX,
  Envoi,
  FicheDuCentre,
  formaterDate,
  formaterMoment,
  formaterMontant,
  lendemain,
  LienWhatsApp,
  lireUnPrix,
  MODES,
  ModeDePaiement,
  OBJETS,
  ouvrirWhatsApp,
  Paiement,
  PaiementEnregistre,
} from './api';

/**
 * La fiche d'un centre dans le back office : son état et son prix (EDI-01, EDI-05), ses paiements d'abonnement et leur
 * confirmation (EDI-02, EDI-04), l'invitation de son premier propriétaire (EDI-03), et la suspension ou la réactivation
 * à la main (EDI-05), la relance (EDI-06), l'accès support (EDI-07) et, 12 mois après la suspension, la suppression (EDI-08).
 */
@Component({
  selector: 'sh-fiche-du-centre',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, ChampEmail, ChampTelephone],
  templateUrl: './fiche-du-centre.html',
  styleUrl: './fiche-du-centre.css',
})
export class FicheDuCentreDuBackOffice implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  /** L'identifiant du Centre, dans l'adresse. */
  readonly id = input.required<string>();

  protected readonly fiche = signal<FicheDuCentre | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly succes = signal<string | null>(null);
  protected readonly envoi = signal(false);
  /** Le lien WhatsApp que le navigateur n'a pas pu ouvrir tout seul. */
  protected readonly lienAOuvrir = signal<string | null>(null);
  protected readonly correction = signal(false);
  protected readonly modificationDuPrix = signal(false);

  protected readonly modes = Object.entries(MODES) as [ModeDePaiement, string][];
  protected readonly date = formaterDate;
  protected readonly moment = formaterMoment;
  protected readonly montant = formaterMontant;
  protected readonly telephone = (chiffres: string | null) => (chiffres ? grouperUnTelephone(chiffres) : '');
  protected readonly canal = (envoi: Envoi) => CANAUX[envoi.canal];
  protected readonly objet = (envoi: Envoi) => OBJETS[envoi.objet];
  protected readonly libelleDuMode = (paiement: Paiement) => MODES[paiement.mode];

  protected readonly etat = computed(() => {
    const fiche = this.fiche();
    return fiche ? ETATS_DE_L_ABONNEMENT[fiche.etat] : null;
  });

  /** Les boutons de l'invitation ne servent qu'après le premier paiement, tant que le propriétaire n'est pas inscrit (EDI-03). */
  protected readonly invitationPossible = computed(() => {
    const fiche = this.fiche();
    return !!fiche && fiche.paiements.length > 0 && fiche.proprietaire?.etat === 'Invite';
  });

  protected readonly paiement = new FormGroup({
    date: new FormControl('', { nonNullable: true, validators: requis() }),
    montant: new FormControl('', { nonNullable: true, validators: requis() }),
    mode: new FormControl<ModeDePaiement>('Virement', { nonNullable: true }),
    du: new FormControl('', { nonNullable: true, validators: requis() }),
    au: new FormControl('', { nonNullable: true, validators: requis() }),
    numeroDeFacture: new FormControl('', { nonNullable: true, validators: requis() }),
  });

  /** Le formulaire du paiement : vidé après un enregistrement, sans garder le rouge des champs vides (`ng-submitted`). */
  private readonly directiveDuPaiement = viewChild('formulaireDuPaiement', { read: FormGroupDirective });

  protected readonly prix = new FormControl('', { nonNullable: true });

  protected readonly formulaireDuProprietaire = new FormGroup({
    prenom: new FormControl('', { nonNullable: true, validators: requis() }),
    nom: new FormControl('', { nonNullable: true, validators: requis() }),
    email: new FormControl('', { nonNullable: true, validators: requis() }),
    telephone: new FormControl('', { nonNullable: true, validators: requis() }),
  });

  protected readonly motif = new FormControl('', { nonNullable: true });
  protected readonly nomRetape = new FormControl('', { nonNullable: true });
  protected readonly nomRetapeValeur = signal('');

  constructor() {
    this.nomRetape.valueChanges.subscribe((valeur) => this.nomRetapeValeur.set(valeur));
  }

  ngOnInit(): void {
    void this.charger();
  }

  private async charger(): Promise<void> {
    try {
      this.montrer(await firstValueFrom(this.http.get<FicheDuCentre>(this.adresse(''))));
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    }
  }

  /** La fiche à jour, et les formulaires préremplis : le paiement commence le lendemain de la fin payée. */
  private montrer(fiche: FicheDuCentre): void {
    this.fiche.set(fiche);
    const debut = fiche.payeJusquAu ? lendemain(fiche.payeJusquAu) : aujourdhui();
    const vide = { date: aujourdhui(), montant: '', mode: 'Virement' as ModeDePaiement, du: debut, au: '', numeroDeFacture: '' };
    const directive = this.directiveDuPaiement();
    if (directive) directive.resetForm(vide);
    else this.paiement.reset(vide);
    this.prix.setValue(ecrireUnMontant(fiche.prixMensuel));
  }

  // ----- EDI-01 : le prix -----

  protected async changerLePrix(): Promise<void> {
    const prix = lireUnPrix(this.prix.value);
    if (!prix) {
      this.message.set('Le prix mensuel doit être un montant en dinars, par exemple 90 ou 75,500');
      return;
    }
    await this.agir(async () => {
      this.montrer(
        await firstValueFrom(
          this.http.put<FicheDuCentre>(this.adresse('/prix'), { prixMensuel: prix }),
        ),
      );
      this.modificationDuPrix.set(false);
      this.succes.set(`Le prix mensuel est maintenant de ${formaterMontant(prix)}`);
    });
  }

  // ----- EDI-02 et EDI-04 : les paiements -----

  protected async enregistrerLePaiement(): Promise<void> {
    const valeurs = this.paiement.getRawValue();
    if (
      !valeurs.date ||
      !valeurs.montant.trim() ||
      !valeurs.du ||
      !valeurs.au ||
      !valeurs.numeroDeFacture.trim()
    ) {
      this.message.set('Saisissez la date, le montant, le mode, la période et le n° de facture');
      return;
    }
    const montant = lireUnPrix(valeurs.montant);
    if (!montant) {
      this.message.set('Le montant doit être en dinars, par exemple 270 ou 270,500');
      return;
    }
    if (valeurs.au < valeurs.du) {
      this.message.set('La fin de la période doit venir après son début');
      return;
    }
    await this.agir(async () => {
      const resultat = await firstValueFrom(
        this.http.post<PaiementEnregistre>(this.adresse('/paiements'), {
          date: valeurs.date,
          montant,
          mode: valeurs.mode,
          du: valeurs.du,
          au: valeurs.au,
          numeroDeFacture: valeurs.numeroDeFacture.trim(),
        }),
      );
      this.montrer(resultat.fiche);
      if (resultat.confirmationEnvoyee) this.succes.set(resultat.message);
      else this.message.set(resultat.message);
    });
  }

  protected async confirmerParEmail(paiement: Paiement): Promise<void> {
    await this.agir(async () => {
      this.montrer(
        await firstValueFrom(
          this.http.post<FicheDuCentre>(
            this.adresse(`/paiements/${paiement.id}/confirmation/email`),
            null,
          ),
        ),
      );
      this.succes.set('La confirmation est repartie par email');
    });
  }

  protected async confirmerParWhatsApp(paiement: Paiement): Promise<void> {
    await this.whatsApp(`/paiements/${paiement.id}/confirmation/whatsapp`);
  }

  // ----- EDI-03 : l'invitation -----

  protected async inviterParEmail(): Promise<void> {
    await this.agir(async () => {
      this.montrer(
        await firstValueFrom(
          this.http.post<FicheDuCentre>(this.adresse('/invitation/email'), null),
        ),
      );
      this.succes.set(`L'invitation est partie à ${this.fiche()?.proprietaire?.email}`);
    });
  }

  protected async inviterParWhatsApp(): Promise<void> {
    await this.whatsApp('/invitation/whatsapp');
  }

  protected corriger(): void {
    const proprietaire = this.fiche()?.proprietaire;
    if (!proprietaire) return;
    this.formulaireDuProprietaire.reset({
      prenom: proprietaire.prenom,
      nom: proprietaire.nom,
      email: proprietaire.email,
      telephone: proprietaire.telephone ? grouperUnTelephone(proprietaire.telephone) : '',
    });
    this.correction.set(true);
  }

  protected async enregistrerLeProprietaire(): Promise<void> {
    const valeurs = this.formulaireDuProprietaire.getRawValue();
    const telephone = lireUnTelephone(valeurs.telephone);
    if (
      !valeurs.prenom.trim() ||
      !valeurs.nom.trim() ||
      !valeurs.email.trim() ||
      !valeurs.telephone.trim()
    ) {
      this.message.set("Saisissez le prénom, le nom, l'email et le téléphone du propriétaire");
      return;
    }
    if (!telephone) {
      this.message.set('Le téléphone du propriétaire doit avoir 8 chiffres (ex. 98 401 220)');
      return;
    }
    if (!estUnEmail(valeurs.email)) {
      this.message.set("Cet email n'est pas valable");
      return;
    }
    await this.agir(async () => {
      this.montrer(
        await firstValueFrom(
          this.http.put<FicheDuCentre>(this.adresse('/proprietaire'), {
            prenom: valeurs.prenom.trim(),
            nom: valeurs.nom.trim(),
            email: valeurs.email.trim(),
            telephone,
          }),
        ),
      );
      this.correction.set(false);
      this.succes.set("Le propriétaire est corrigé : renvoyez-lui l'invitation");
    });
  }

  // ----- EDI-05 : suspendre, réactiver -----

  protected async suspendreOuReactiver(): Promise<void> {
    const fiche = this.fiche();
    if (!fiche) return;
    const motif = this.motif.value.trim();
    if (!motif) {
      this.message.set('Saisissez le motif');
      return;
    }
    const action = fiche.etat === 'Suspendu' ? 'reactivation' : 'suspension';
    await this.agir(async () => {
      this.montrer(
        await firstValueFrom(
          this.http.post<FicheDuCentre>(this.adresse(`/${action}`), { texte: motif }),
        ),
      );
      this.motif.reset();
      this.succes.set(
        action === 'suspension'
          ? 'Le centre est suspendu : il est en lecture seule'
          : 'Le centre est réactivé',
      );
    });
  }

  // ----- EDI-06 : relancer -----

  protected async relancer(): Promise<void> {
    await this.whatsApp('/relance/whatsapp');
  }

  // ----- EDI-08 : supprimer -----

  protected async supprimer(): Promise<void> {
    const fiche = this.fiche();
    if (!fiche || this.nomRetape.value.trim() !== fiche.nom) {
      this.message.set('Retapez exactement le nom du centre pour confirmer la suppression');
      return;
    }
    await this.agir(async () => {
      await firstValueFrom(this.http.post(this.adresse('/suppression'), { nom: this.nomRetape.value.trim() }));
      await this.router.navigate(['/editeur'], { queryParams: { supprime: fiche.nom } });
    });
  }

  // ----- Outils -----

  private adresse(suite: string): string {
    return `/api/editeur/centres/${this.id()}${suite}`;
  }

  /** Ouvre WhatsApp avec le message écrit ; le lien reste affiché, au cas où le navigateur aurait bloqué l'onglet. */
  private async whatsApp(suite: string): Promise<void> {
    await this.agir(async () => {
      this.lienAOuvrir.set(null);
      const reponse = await ouvrirWhatsApp(() =>
        firstValueFrom(this.http.post<LienWhatsApp>(this.adresse(suite), null)),
      );
      this.montrer(reponse.fiche);
      this.lienAOuvrir.set(reponse.lien);
    });
  }

  /** Une action à la fois ; son refus s'affiche en haut de la page, avec le message du serveur. */
  private async agir(action: () => Promise<void>): Promise<void> {
    if (this.envoi()) return;
    this.envoi.set(true);
    this.message.set(null);
    this.succes.set(null);
    try {
      await action();
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
