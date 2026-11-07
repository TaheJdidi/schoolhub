import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, output, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant, lireUnNombre } from '../../socle/montants';
import { CaisseOuverte, Cloture, ecrireUnEcart, jourCourt, MaCaisse } from './caisse-du-soir';

/**
 * CAI-09 : « Ma caisse ». L'encaissé du jour, les sorties de caisse et les espèces attendues ; le soir, le montant compté,
 * l'écart en direct, et « Clôturer ma caisse ». Une caisse oubliée d'un jour passé vient en tête, à clôturer d'abord.
 * Un encaissement après la clôture ouvre une deuxième caisse du jour.
 */
@Component({
  selector: 'sh-ma-caisse',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (caisse(); as caisse) {
      @if (aCloturer(); as ouverte) {
        <section class="card carte" [class.oubliee]="ouverte.oubliee" aria-labelledby="t-ma-caisse">
          <div class="entete">
            <h2 class="titre" id="t-ma-caisse">{{ titre(ouverte) }}</h2>
            @if (ouverte.oubliee) {
              <span class="pill warn">Pas clôturée</span>
            }
          </div>
          @if (ouverte.oubliee) {
            <p class="note warn">Votre caisse du {{ jourCourt(ouverte.jour) }} n'est pas clôturée. Comptez ses espèces et clôturez-la.</p>
          }
          <dl class="chiffres">
            <div><dt>Encaissé</dt><dd class="num">{{ montant(ouverte.encaisse) }} TND</dd><small>{{ ouverte.recus }} reçu{{ ouverte.recus > 1 ? 's' : '' }}</small></div>
            <div><dt>Sorties de caisse</dt><dd class="num">{{ ouverte.sorties > 0 ? '−' : '' }}{{ montant(ouverte.sorties) }} TND</dd><small>Achats payés en espèces</small></div>
            <div><dt>Espèces attendues</dt><dd class="num"><b>{{ montant(ouverte.attendu) }} TND</b></dd><small>Encaissé moins les sorties</small></div>
          </dl>
          @if (ouverte.recus > 0 || ouverte.annules > 0 || ouverte.sorties > 0 || ouverte.oubliee) {
            <div class="cloture">
              <label class="f">
                Montant compté dans la caisse (TND)
                <input #compte inputmode="decimal" autocomplete="off" [placeholder]="montant(ouverte.attendu)" (input)="saisie.set(compte.value)" />
              </label>
              <p class="ecart" [class.ok]="ecart() === 0" [class.bad]="ecart() !== null && ecart() !== 0">{{ texteDeLEcart() }}</p>
              <button type="button" class="btn primary" [disabled]="envoi()" (click)="cloturer(ouverte)">Clôturer ma caisse</button>
              <p class="aide">Après la clôture, seul le propriétaire peut annuler un reçu de cette caisse.</p>
            </div>
          } @else {
            <p class="aide">Rien à clôturer pour le moment : votre caisse s'ouvre avec votre premier encaissement.</p>
          }
          @if (erreur(); as erreur) {
            <p class="note bad" role="alert">{{ erreur }}</p>
          }
        </section>
      }
      @for (cloture of caisse.clotures; track cloture.id) {
        <section class="card carte" aria-label="Caisse clôturée">
          <div class="entete">
            <h2 class="titre">Clôture du soir{{ cloture.rang > 1 ? ' (' + cloture.rang + 'ᵉ caisse)' : '' }}</h2>
            <span class="pill ok">Clôturée</span>
          </div>
          <dl class="kv">
            <dt>Espèces attendues</dt><dd class="num">{{ montant(cloture.attendu) }} TND</dd>
            <dt>Montant compté</dt><dd class="num">{{ montant(cloture.compte) }} TND</dd>
            <dt>Écart</dt><dd class="num" [class.bad]="cloture.ecart !== 0" [class.ok]="cloture.ecart === 0">{{ ecrireUnEcart(cloture.ecart) }} TND</dd>
            <dt>Clôturée à</dt><dd>{{ heure(cloture.clotureeLe) }}</dd>
          </dl>
          <p class="aide">Le propriétaire voit votre clôture et marque « argent reçu » quand il récupère les espèces.</p>
        </section>
      }
    } @else if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    :host { display: grid; gap: 16px; }
    .carte.oubliee { border-color: var(--warn); }
    .entete { display: flex; justify-content: space-between; align-items: center; gap: 10px; }
    .chiffres { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 12px; margin: 0; }
    .chiffres div { display: grid; gap: 2px; padding: 10px 12px; border-radius: 10px; background: var(--sunk); }
    .chiffres dt { font-size: 12.5px; color: var(--ink-2); }
    .chiffres dd { margin: 0; font-size: 18px; }
    .chiffres small { color: var(--ink-3); font-size: 12px; }
    .cloture { display: grid; gap: 10px; max-width: 360px; }
    .ecart { margin: 0; font-weight: 500; color: var(--ink-3); }
    .ecart.ok, .kv .ok { color: var(--ok); }
    .ecart.bad, .kv .bad { color: var(--bad); }
    .kv { display: grid; grid-template-columns: auto 1fr; gap: 6px 16px; margin: 0; }
    .kv dt { color: var(--ink-2); }
    .kv dd { margin: 0; }
  `,
})
export class MaCaisseComponent implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly montant = ecrireUnMontant;
  protected readonly ecrireUnEcart = ecrireUnEcart;
  protected readonly jourCourt = jourCourt;
  /** Une caisse vient d'être clôturée : le contrôle des clôtures se recharge. */
  readonly cloturee = output<void>();

  protected readonly caisse = signal<MaCaisse | null>(null);
  protected readonly saisie = signal('');
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);

  /** La caisse à clôturer d'abord : la plus ancienne oubliée, sinon celle du jour. */
  protected readonly aCloturer = computed(() => this.caisse()?.ouvertes[0] ?? null);

  /** Compté moins attendu ; `null` tant que le montant n'est pas lisible. */
  protected readonly ecart = computed(() => {
    const compte = lireUnNombre(this.saisie());
    const ouverte = this.aCloturer();
    return typeof compte === 'number' && ouverte ? Math.round((compte - ouverte.attendu) * 1000) / 1000 : null;
  });

  protected readonly texteDeLEcart = computed(() => {
    const ecart = this.ecart();
    if (ecart === null) return 'Saisissez le montant compté';
    return ecart === 0 ? "Pas d'écart" : `Écart : ${ecrireUnEcart(ecart)} TND`;
  });

  async ngOnInit(): Promise<void> {
    await this.charger();
  }

  protected titre(ouverte: CaisseOuverte): string {
    if (ouverte.oubliee) return `Caisse du ${jourCourt(ouverte.jour)}`;
    return ouverte.rang > 1 ? `Ma caisse (${ouverte.rang}ᵉ caisse du jour)` : 'Ma caisse';
  }

  protected async cloturer(ouverte: CaisseOuverte): Promise<void> {
    const compte = lireUnNombre(this.saisie());
    if (typeof compte !== 'number' || compte < 0) {
      this.erreur.set('Saisissez le montant compté, au millime près');
      return;
    }
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      await firstValueFrom(this.http.post<Cloture>('/api/caisse/cloture', { jour: ouverte.jour, compte }));
      this.saisie.set('');
      this.cloturee.emit();
      await this.charger();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** « 18h47 ». */
  protected heure(texte: string): string {
    return formatDate(texte, "HH'h'mm", FRANCAIS);
  }

  private async charger(): Promise<void> {
    try {
      this.caisse.set(await firstValueFrom(this.http.get<MaCaisse>('/api/caisse')));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }
}
