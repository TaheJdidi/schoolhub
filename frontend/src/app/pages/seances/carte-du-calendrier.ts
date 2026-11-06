import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { FicheDuGroupe, heure, Jour, JOURS } from '../groupes/groupe';
import { ChangementDuCalendrier } from './appel';
import { MessagesDuChangement } from './messages-du-changement';

/** Un créneau en cours de saisie. */
interface CreneauSaisi {
  jour: Jour;
  debut: string;
  fin: string;
}

/**
 * SEA-02, SEA-05 et SEA-06 : le calendrier d'un groupe, sur sa fiche. Les séances annulées (avec leur motif et leur
 * remplacement) et supplémentaires ; « Ajouter une séance supplémentaire » ; « Changer l'horaire du groupe » à partir
 * d'une date. Après un changement, les messages WhatsApp sont prêts, et la fiche se recharge.
 */
@Component({
  selector: 'sh-carte-du-calendrier',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MessagesDuChangement],
  template: `
    @let leGroupe = fiche();
    <section class="card carte">
      <h2 class="titre">Le calendrier</h2>
      @if (leGroupe.horsCompte?.length) {
        <div class="liste-seances">
          @for (seance of leGroupe.horsCompte; track seance.id) {
            <a class="hors-compte" [routerLink]="['/professeurs/groupes', leGroupe.id, 'appel', seance.id]">
              <b>{{ jour(seance.date) }}</b>
              <span>{{ heure(seance.debut) }} → {{ heure(seance.fin) }}</span>
              @if (seance.annulee) {
                <span class="pill muted">Annulée</span>
                <small>{{ seance.motif }}{{ seance.remplaceeLe ? ' · remplacée le ' + jourCourt(seance.remplaceeLe) : '' }}</small>
              } @else {
                <span class="pill info">Supplémentaire</span>
                <small>offerte, hors des Mois</small>
              }
            </a>
          }
        </div>
      } @else {
        <p class="aide">Aucune séance annulée ni supplémentaire.</p>
      }

      @if (modifiable()) {
        @if (action() === null) {
          <div class="actions debut">
            <button type="button" class="btn" (click)="ouvrir('supplementaire')">Ajouter une séance supplémentaire</button>
            <button type="button" class="btn" (click)="ouvrir('horaire')">Changer l'horaire du groupe</button>
          </div>
        } @else if (action() === 'supplementaire') {
          <h3 class="tag">Séance supplémentaire</h3>
          <p class="aide">Elle est offerte : elle a son appel, mais ne compte pas dans le Mois.</p>
          <div class="champs">
            <label class="f">Date <input #date type="date" (input)="date_.set(date.value)" /></label>
            <label class="f">De <input #debut type="time" [value]="debut_()" (input)="debut_.set(debut.value)" /></label>
            <label class="f">À <input #fin type="time" [value]="fin_()" (input)="fin_.set(fin.value)" /></label>
          </div>
          <div class="actions">
            <button type="button" class="btn" [disabled]="envoi()" (click)="fermer()">Retour</button>
            <button type="button" class="btn primary" [disabled]="envoi()" (click)="ajouter()">Ajouter la séance</button>
          </div>
        } @else {
          <h3 class="tag">Nouvel horaire</h3>
          <p class="aide">Les séances à partir de cette date se recréent sur le nouvel horaire ; celles d'avant ne changent pas.</p>
          @for (creneau of creneaux(); track $index; let rang = $index) {
            <div class="champs creneau">
              <label class="f">
                Jour
                <select class="inp" (change)="changerLeCreneau(rang, 'jour', $any($event.target).value)">
                  @for (j of jours; track j.jour) {
                    <option [value]="j.jour" [selected]="j.jour === creneau.jour">{{ j.jour }}</option>
                  }
                </select>
              </label>
              <label class="f">De <input type="time" [value]="creneau.debut" (input)="changerLeCreneau(rang, 'debut', $any($event.target).value)" /></label>
              <label class="f">À <input type="time" [value]="creneau.fin" (input)="changerLeCreneau(rang, 'fin', $any($event.target).value)" /></label>
              @if (creneaux().length > 1) {
                <button type="button" class="btn ghost" (click)="retirerLeCreneau(rang)">Retirer</button>
              }
            </div>
          }
          <div class="champs">
            <label class="f">À partir du <input #depart type="date" (input)="date_.set(depart.value)" /></label>
          </div>
          <div class="actions">
            <button type="button" class="btn ghost" (click)="ajouterUnCreneau()">Ajouter un créneau</button>
            <button type="button" class="btn" [disabled]="envoi()" (click)="fermer()">Retour</button>
            <button type="button" class="btn primary" [disabled]="envoi()" (click)="changerLHoraire()">Changer l'horaire</button>
          </div>
        }
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </section>
    @if (changement(); as changement) {
      <sh-messages-du-changement [changement]="changement" />
    }
  `,
  styleUrls: ['../eleves/eleves.css', '../inscriptions/inscriptions.css'],
  styles: `
    .liste-seances { display: grid; }
    .hors-compte { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 12px; padding: 8px 0; border-top: 1px solid var(--line);
      color: var(--ink); text-decoration: none; }
    .hors-compte:first-child { border-top: 0; }
    .hors-compte span, .hors-compte small { color: var(--ink-2); }
    .actions.debut { justify-content: flex-start; }
    .creneau { align-items: end; }
    .aide { margin: 0; color: var(--ink-2); }
  `,
})
export class CarteDuCalendrier {
  private readonly http = inject(HttpClient);

  readonly fiche = input.required<FicheDuGroupe>();
  /** Un groupe fermé ou terminé ne change plus. */
  readonly modifiable = input(true);
  /** Le calendrier a changé : la fiche se recharge. */
  readonly modifie = output<void>();

  protected readonly heure = heure;
  protected readonly jours = JOURS;
  protected readonly action = signal<'supplementaire' | 'horaire' | null>(null);
  protected readonly date_ = signal('');
  protected readonly debut_ = signal('');
  protected readonly fin_ = signal('');
  protected readonly creneaux = signal<CreneauSaisi[]>([]);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);
  protected readonly changement = signal<ChangementDuCalendrier | null>(null);

  /** Les heures et les créneaux proposés sont ceux du groupe. */
  protected ouvrir(action: 'supplementaire' | 'horaire'): void {
    const creneaux = this.fiche().creneaux;
    this.action.set(action);
    this.date_.set('');
    this.debut_.set(creneaux[0]?.debut ?? '');
    this.fin_.set(creneaux[0]?.fin ?? '');
    this.creneaux.set(creneaux.length > 0 ? creneaux.map((c) => ({ ...c })) : [{ jour: 'Samedi', debut: '', fin: '' }]);
    this.erreur.set(null);
  }

  protected fermer(): void {
    this.action.set(null);
    this.erreur.set(null);
  }

  protected changerLeCreneau(rang: number, champ: keyof CreneauSaisi, valeur: string): void {
    this.creneaux.update((creneaux) => creneaux.map((c, i) => (i === rang ? { ...c, [champ]: valeur } : c)));
  }

  protected ajouterUnCreneau(): void {
    this.creneaux.update((creneaux) => [...creneaux, { jour: 'Samedi', debut: '', fin: '' }]);
  }

  protected retirerLeCreneau(rang: number): void {
    this.creneaux.update((creneaux) => creneaux.filter((_, i) => i !== rang));
  }

  protected async ajouter(): Promise<void> {
    if (!this.date_() || !this.debut_() || !this.fin_()) {
      this.erreur.set('Choisissez la date et les heures');
      return;
    }
    await this.envoyer(`/api/groupes/${this.fiche().id}/seances`, { date: this.date_(), debut: this.debut_(), fin: this.fin_() });
  }

  protected async changerLHoraire(): Promise<void> {
    if (!this.date_()) {
      this.erreur.set('Choisissez la date de départ du nouvel horaire');
      return;
    }
    await this.envoyer(`/api/groupes/${this.fiche().id}/horaire`, { creneaux: this.creneaux(), aPartirDu: this.date_() });
  }

  private async envoyer(adresse: string, corps: unknown): Promise<void> {
    if (this.envoi()) return;
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      this.changement.set(await firstValueFrom(this.http.post<ChangementDuCalendrier>(adresse, corps)));
      this.action.set(null);
      this.modifie.emit();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** « samedi 04/12/2027 ». */
  protected jour(date: string): string {
    return formatDate(date, 'EEEE dd/MM/yyyy', FRANCAIS);
  }

  /** « 07/12 ». */
  protected jourCourt(date: string): string {
    return formatDate(date, 'dd/MM', FRANCAIS);
  }
}
