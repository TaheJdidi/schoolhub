import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, output, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { Icone } from '../../socle/icone';
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
  imports: [Icone],
  template: `
    @if (caisse(); as caisse) {
      @if (aCloturer(); as ouverte) {
        <!-- Le calcul se lit tout seul : encaissé − sorties = espèces attendues ; les chèques, à part, en dessous. -->
        <div class="chiffres">
          <dl class="chiffre">
            <dt><span class="icone info"><sh-icone nom="receipt" /></span>Encaissé en espèces</dt>
            <dd class="montant"><span class="num">{{ montant(ouverte.encaisse) }}</span><small> TND</small></dd>
            <dd class="detail">{{ ouverte.recus }} reçu{{ ouverte.recus > 1 ? 's' : '' }}</dd>
          </dl>
          <span class="operateur" aria-hidden="true">−</span>
          <dl class="chiffre">
            <dt><span class="icone bad"><sh-icone nom="shopping-cart" /></span>Sorties de caisse</dt>
            <dd class="montant" [class.zero]="!ouverte.sorties"><span class="num">{{ montant(ouverte.sorties) }}</span><small> TND</small></dd>
            <dd class="detail">Achats payés en espèces</dd>
          </dl>
          <span class="operateur" aria-hidden="true">=</span>
          <dl class="chiffre attendues">
            <dt><span class="icone ok"><sh-icone nom="wallet" /></span>Espèces attendues</dt>
            <dd class="montant"><span class="num">{{ montant(ouverte.attendu) }}</span><small> TND</small></dd>
            <dd class="detail">À trouver dans le tiroir</dd>
          </dl>
          <!-- Les chèques, toujours là : à remettre à part, ou « Aucun chèque » en gris. -->
          <dl class="cheques" [class.aucun]="!ouverte.nombreDeCheques">
            <dt>
              <span class="icone info"><sh-icone nom="file-text" /></span>
              @if (ouverte.nombreDeCheques; as nombre) {
                Chèques, à remettre à part · <b>{{ nombre }} chèque{{ nombre > 1 ? 's' : '' }}</b>
              } @else {
                Chèques · <b>aucun chèque reçu</b>
              }
            </dt>
            <dd class="num">{{ montant(ouverte.cheques ?? 0) }}<small> TND</small></dd>
          </dl>
        </div>
      }
      <div class="colonne-cloture">
      @if (aCloturer(); as ouverte) {
        <section class="card carte" [class.oubliee]="ouverte.oubliee" aria-labelledby="t-ma-caisse">
          <div class="entete">
            <h2 class="titre" id="t-ma-caisse">{{ ouverte.oubliee ? titre(ouverte) : 'Clôture du soir' }}</h2>
            @if (ouverte.oubliee) {
              <span class="pill warn">Pas clôturée</span>
            }
          </div>
          @if (ouverte.oubliee) {
            <p class="note warn">Votre caisse du {{ jourCourt(ouverte.jour) }} n'est pas clôturée. Comptez ses espèces et clôturez-la.</p>
          }
          <dl class="kv">
            <dt>Encaissé en espèces</dt><dd class="num">{{ montant(ouverte.encaisse) }} TND</dd>
            <dt>Sorties de caisse</dt><dd class="num">{{ ouverte.sorties > 0 ? '−' : '' }}{{ montant(ouverte.sorties) }} TND</dd>
            <dt><b>Espèces attendues</b></dt><dd class="num"><b>{{ montant(ouverte.attendu) }} TND</b></dd>
            @if (ouverte.nombreDeCheques) {
              <dt>Chèques, à remettre à part</dt><dd class="num">{{ montant(ouverte.cheques ?? 0) }} TND ({{ ouverte.nombreDeCheques }})</dd>
            }
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
            <p class="aide">Rien à clôturer pour le moment : votre caisse s'ouvre avec votre premier paiement.</p>
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
            @if (cloture.nombreDeCheques) {
              <dt>Chèques</dt><dd class="num">{{ montant(cloture.cheques ?? 0) }} TND ({{ cloture.nombreDeCheques }})</dd>
            }
            <dt>Clôturée à</dt><dd>{{ heure(cloture.clotureeLe) }}</dd>
          </dl>
          <p class="aide">Le propriétaire voit votre clôture et marque « argent reçu » quand il récupère les espèces.</p>
        </section>
      }
      </div>
    } @else if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    /* Les chiffres et la clôture se rangent dans la grille de la page Caisse (prototype, écran 7). */
    :host { display: contents; }
    .chiffres { grid-area: chiffres; }
    .colonne-cloture { grid-area: cloture; display: grid; gap: 16px; align-content: start; }
    .carte.oubliee { border-color: var(--warn); }
    .entete { display: flex; justify-content: space-between; align-items: center; gap: 10px; }
    /* Trois cartes reliées par « − » et « = » ; les espèces attendues, en vert, un peu plus larges. Les chèques dessous. */
    .chiffres { display: grid; grid-template-columns: minmax(0, 1fr) 24px minmax(0, 1fr) 24px minmax(0, 1.15fr); gap: 6px 6px; align-items: stretch; }
    @media (max-width: 760px) { .chiffres { grid-template-columns: minmax(0, 1fr); } .operateur { height: 18px; } }
    .chiffre { display: grid; gap: 10px; align-content: start; margin: 0; padding: 16px 18px; border-radius: 16px; background: var(--surface); border: 1px solid var(--line); }
    .chiffre dt { display: flex; align-items: center; gap: 10px; font-size: 13px; font-weight: 500; color: var(--ink-2); }
    .chiffre dd { margin: 0; }
    .icone { display: grid; place-items: center; flex: none; width: 34px; height: 34px; border-radius: 10px; }
    .icone svg.lucide { width: 19px; height: 19px; }
    .icone.info { background: var(--accent-soft); color: var(--accent); }
    .icone.bad { background: var(--bad-soft); color: var(--bad); }
    .icone.ok { background: var(--surface); color: var(--ok); }
    .montant { font-family: var(--font-mono); font-size: 30px; font-weight: 600; line-height: 1.1; }
    .montant small { margin-left: 2px; font-family: var(--font-body); font-size: 14px; font-weight: 500; color: var(--ink-2); }
    .montant.zero { color: var(--ink-3); }
    .detail { font-size: 12px; color: var(--ink-3); }
    .operateur { display: grid; place-items: center; font-family: var(--font-mono); font-size: 22px; font-weight: 600; color: var(--ink-3); }
    .chiffre.attendues { background: var(--ok-soft); border-color: transparent; }
    .chiffre.attendues dt, .chiffre.attendues .detail, .chiffre.attendues .montant small { color: var(--ok); }
    .chiffre.attendues .montant { color: var(--ok); }
    .cheques { grid-column: 1 / -1; display: flex; align-items: center; justify-content: space-between; gap: 10px; margin: 4px 0 0; padding: 10px 14px; border: 1px dashed var(--line-2); border-radius: 12px; background: var(--surface); }
    .cheques dt { display: flex; align-items: center; gap: 10px; font-size: 13px; color: var(--ink-2); }
    .cheques dt b { color: var(--ink); font-weight: 500; }
    .cheques .icone { width: 28px; height: 28px; }
    .cheques .icone svg.lucide { width: 16px; height: 16px; }
    .cheques dd { margin: 0; font-family: var(--font-mono); font-size: 16px; font-weight: 600; white-space: nowrap; }
    .cheques dd small { font-family: var(--font-body); font-size: 13px; font-weight: 500; color: var(--ink-2); }
    .cheques.aucun dt b, .cheques.aucun dd { color: var(--ink-3); font-weight: 500; }
    .cheques.aucun .icone { background: var(--sunk); color: var(--ink-3); }
    .cloture { display: grid; gap: 10px; }
    .ecart { margin: 0; font-weight: 500; color: var(--ink-3); }
    .ecart.ok, .kv .ok { color: var(--ok); }
    .ecart.bad, .kv .bad { color: var(--bad); }
    .kv { display: grid; grid-template-columns: auto 1fr; gap: 6px 16px; margin: 0; }
    .kv dt { color: var(--ink-2); }
    .kv dd { margin: 0; text-align: right; }
    .cloture .btn.primary { width: 100%; }
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
