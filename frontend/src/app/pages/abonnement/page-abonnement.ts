import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Abonnement, ETATS_DE_L_ABONNEMENT, SituationDuCentre } from '../../socle/abonnement';
import { messageDErreur } from '../../socle/erreurs';
import { formaterDate, formaterMoment } from '../editeur/api';

/**
 * La page « Abonnement » du propriétaire : l'état de son abonnement, l'accès support qu'il donne à l'équipe SchoolUp
 * pour 24 h et peut retirer (EDI-07), et, pendant les 12 mois qui suivent une suspension, l'export Excel de ses données (EDI-08).
 */
@Component({
  selector: 'sh-page-abonnement',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Abonnement</h1>
        <p class="sub">Votre abonnement à SchoolUp, l'accès support et vos données.</p>
      </div>
    </div>

    @if (message(); as message) {
      <p class="note bad bandeau" role="alert">{{ message }}</p>
    }

    @if (situation(); as situation) {
      <section class="card bloc" aria-label="État de l'abonnement">
        <h2 class="h2">État</h2>
        <p class="ligne">
          <span class="pill {{ etat()?.classe }}">{{ etat()?.libelle }}</span>
          @if (situation.payeJusquAu) {
            <span>Payé jusqu'au {{ date(situation.payeJusquAu) }} inclus.</span>
          }
        </p>
      </section>

      <section class="card bloc" aria-label="Accès support">
        <h2 class="h2">Accès support</h2>
        @if (situation.supportJusquAu) {
          <p class="note ok">L'équipe SchoolUp peut voir votre centre, en lecture seule, jusqu'au {{ moment(situation.supportJusquAu) }}.</p>
          <button type="button" class="btn" [disabled]="envoi()" (click)="retirer()">Retirer l'autorisation</button>
        } @else {
          <p class="sub">
            Sans votre autorisation, l'équipe SchoolUp ne voit que le nom de votre centre, l'état de votre abonnement, vos paiements
            et le nombre de comptes. Pour qu'elle vous aide, autorisez-la à voir votre centre pendant 24 heures, en lecture seule :
            chaque page qu'elle consulte est notée au journal.
          </p>
          <button type="button" class="btn primary" [disabled]="envoi()" (click)="autoriser()">Autoriser le support 24 h</button>
        }
      </section>

      @if (situation.exportPossible) {
        <section class="card bloc" aria-label="Export de vos données">
          <h2 class="h2">Vos données</h2>
          <p class="sub">
            Votre abonnement est suspendu. Jusqu'au {{ date(situation.exportPossibleJusquAu) }}, vous pouvez télécharger vos données
            (élèves, professeurs, reçus, employés, achats, comptes) dans un fichier Excel. Ensuite, elles pourront être supprimées.
          </p>
          <a class="btn primary" href="/api/abonnement/export" download>Télécharger mes données (Excel)</a>
        </section>
      }
    } @else if (!message()) {
      <p class="sub">Chargement…</p>
    }
  `,
  styles: `
    .bandeau { margin: 0 0 14px; }
    .h2 { font-family: var(--font-display); font-size: 16px; font-weight: 600; margin: 0; }
    .bloc { display: grid; gap: 12px; padding: 16px; margin-bottom: 16px; max-width: 920px; justify-items: start; }
    .bloc .sub, .bloc .note { margin: 0; }
    .ligne { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin: 0; }
  `,
})
export class PageAbonnement {
  private readonly http = inject(HttpClient);
  private readonly abonnement = inject(Abonnement);

  protected readonly situation = this.abonnement.situation;
  protected readonly message = signal<string | null>(null);
  protected readonly envoi = signal(false);
  protected readonly etat = computed(() => {
    const situation = this.situation();
    return situation ? ETATS_DE_L_ABONNEMENT[situation.etat] : null;
  });
  protected readonly date = formaterDate;
  protected readonly moment = formaterMoment;

  constructor() {
    // Relue en ouvrant la page : l'accès support a pu prendre fin depuis le chargement du cadre.
    void this.abonnement.charger();
  }

  protected async autoriser(): Promise<void> {
    await this.agir(() => firstValueFrom(this.http.post<SituationDuCentre>('/api/abonnement/support', null)));
  }

  protected async retirer(): Promise<void> {
    await this.agir(() => firstValueFrom(this.http.delete<SituationDuCentre>('/api/abonnement/support')));
  }

  private async agir(action: () => Promise<SituationDuCentre>): Promise<void> {
    if (this.envoi()) return;
    this.envoi.set(true);
    this.message.set(null);
    try {
      this.abonnement.situation.set(await action());
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
