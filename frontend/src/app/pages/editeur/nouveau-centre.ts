import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ChampEmail, ChampTelephone, estUnEmail, lireUnTelephone } from '../../socle/champs-de-contact';
import { requis } from '../../socle/champs-requis';
import { messageDErreur } from '../../socle/erreurs';
import { ecrireUnMontant } from '../../socle/montants';
import { FicheDuCentre, lireUnPrix } from './api';

/** Le code du lien public proposé depuis le nom : « Centre Les Jasmins » → « les-jasmins ». */
export function codePropose(nom: string): string {
  return nom
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/^centre\s+/, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

/**
 * EDI-01 : créer un centre avec son premier propriétaire. Il naît En attente ; l'invitation du propriétaire partira
 * après le premier paiement d'abonnement, depuis la fiche du centre.
 */
@Component({
  selector: 'sh-nouveau-centre',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, ChampEmail, ChampTelephone],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Créer un centre</h1>
        <p class="sub">Le centre sera En attente jusqu'au premier paiement d'abonnement.</p>
      </div>
      <a class="btn ghost" routerLink="/editeur">Retour aux centres</a>
    </div>

    @if (message(); as message) {
      <p class="note bad bandeau" role="alert">{{ message }}</p>
    }

    <form class="card formulaire" [formGroup]="formulaire" (ngSubmit)="creer()" novalidate>
      <h2 class="h2">Le centre</h2>
      <div class="champs">
        <label class="f">Nom du centre <input type="text" formControlName="nom" autocomplete="off" (blur)="proposerLeCode()" /></label>
        <label class="f">
          Code du lien public
          <input type="text" formControlName="code" autocomplete="off" spellcheck="false" />
          <small class="aide">Le formulaire d'inscription sera à l'adresse /inscription/{{ formulaire.controls.code.value || 'code' }}</small>
        </label>
        <label class="f">Prix mensuel (TND) <input type="text" formControlName="prixMensuel" inputmode="decimal" autocomplete="off" /></label>
      </div>
      <h2 class="h2">Le premier propriétaire</h2>
      <div class="champs">
        <label class="f">Prénom <input type="text" formControlName="prenom" autocomplete="off" /></label>
        <label class="f">Nom <input type="text" formControlName="nomDuProprietaire" autocomplete="off" /></label>
        <label class="f">Email <input shEmail type="email" formControlName="email" autocomplete="off" /></label>
        <label class="f">Téléphone <input shTelephone type="text" formControlName="telephone" autocomplete="off" /></label>
      </div>
      <button type="submit" class="btn primary" [disabled]="envoi()">Créer le centre</button>
    </form>
  `,
  styles: `
    .bandeau { margin: 0 0 14px; }
    .h2 { font-family: var(--font-display); font-size: 16px; font-weight: 600; margin: 0; }
    .formulaire { display: grid; gap: 14px; padding: 16px; justify-items: start; }
    .champs { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; width: 100%; align-items: start; }
    .aide { color: var(--ink-3); font-size: 12px; }
    @media (max-width: 760px) { .champs { grid-template-columns: minmax(0, 1fr); } }
  `,
})
export class NouveauCentre {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  protected readonly message = signal<string | null>(null);
  protected readonly envoi = signal(false);

  protected readonly formulaire = new FormGroup({
    nom: new FormControl('', { nonNullable: true, validators: requis() }),
    code: new FormControl('', { nonNullable: true, validators: requis() }),
    prixMensuel: new FormControl('', { nonNullable: true, validators: requis() }),
    prenom: new FormControl('', { nonNullable: true, validators: requis() }),
    nomDuProprietaire: new FormControl('', { nonNullable: true, validators: requis() }),
    email: new FormControl('', { nonNullable: true, validators: requis() }),
    telephone: new FormControl('', { nonNullable: true, validators: requis() }),
  });

  constructor() {
    void this.prendreLePrixDeLOffre();
  }

  /** Le prix de l'offre, prérempli : l'Éditeur le change pour une remise négociée. */
  private async prendreLePrixDeLOffre(): Promise<void> {
    try {
      const offre = await firstValueFrom(this.http.get<{ prixMensuel: number }>('/api/editeur/offre'));
      if (!this.formulaire.controls.prixMensuel.value) this.formulaire.controls.prixMensuel.setValue(ecrireUnMontant(offre.prixMensuel));
    } catch {
      // Sans l'offre, l'Éditeur saisit le prix lui-même.
    }
  }

  /** Le code se propose depuis le nom, tant que l'Éditeur ne l'a pas écrit lui-même. */
  protected proposerLeCode(): void {
    const code = this.formulaire.controls.code;
    if (!code.value.trim()) code.setValue(codePropose(this.formulaire.controls.nom.value));
  }

  protected async creer(): Promise<void> {
    if (this.envoi()) return;
    const valeurs = this.formulaire.getRawValue();
    if (Object.values(valeurs).some((valeur) => valeur.trim() === '')) {
      this.message.set("Saisissez le nom du centre, le code de son lien, son prix, et le prénom, le nom, l'email et le téléphone du propriétaire");
      return;
    }
    const telephone = lireUnTelephone(valeurs.telephone);
    if (!telephone) {
      this.message.set('Le téléphone du propriétaire doit avoir 8 chiffres (ex. 98 401 220)');
      return;
    }
    if (!estUnEmail(valeurs.email)) {
      this.message.set("Cet email n'est pas valable");
      return;
    }
    const prix = lireUnPrix(valeurs.prixMensuel);
    if (!prix) {
      this.message.set('Le prix mensuel doit être un montant en dinars, par exemple 90 ou 75,500');
      return;
    }

    this.envoi.set(true);
    this.message.set(null);
    try {
      const fiche = await firstValueFrom(
        this.http.post<FicheDuCentre>('/api/editeur/centres', {
          nom: valeurs.nom.trim(),
          code: valeurs.code.trim(),
          prenomDuProprietaire: valeurs.prenom.trim(),
          nomDuProprietaire: valeurs.nomDuProprietaire.trim(),
          emailDuProprietaire: valeurs.email.trim(),
          telephoneDuProprietaire: telephone,
          prixMensuel: prix,
        }),
      );
      await this.router.navigate(['/editeur/centres', fiche.id]);
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
