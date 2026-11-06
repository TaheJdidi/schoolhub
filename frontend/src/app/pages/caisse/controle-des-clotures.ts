import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { ecrireUnMontant } from '../../socle/montants';
import { Cloture, ControleDesClotures, ecrireUnEcart, jourCourt } from './caisse-du-soir';

/**
 * CAI-10 : le contrôle des clôtures, pour le propriétaire. « À récupérer » (le total compté pas encore récupéré), les
 * caisses des jours passés pas clôturées, puis le tableau des clôtures (jour, secrétaire, attendu, compté, écart en rouge
 * s'il est négatif) avec « argent reçu » à cocher. Filtré par période et par secrétaire.
 */
@Component({
  selector: 'sh-controle-des-clotures',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card carte" aria-labelledby="t-controle">
      <div class="entete">
        <div>
          <h2 class="titre" id="t-controle">Contrôle des clôtures</h2>
          <p class="aide">Cochez « argent reçu » quand vous récupérez les espèces.</p>
        </div>
        @if (controle(); as controle) {
          <span class="pill" [class.warn]="controle.aRecuperer > 0" [class.ok]="controle.aRecuperer === 0">
            À récupérer : {{ montant(controle.aRecuperer) }} TND
          </span>
        }
      </div>
      <div class="filtres">
        <label class="f">Du <input #du type="date" (change)="filtrer(du.value, au.value, compte.value)" /></label>
        <label class="f">Au <input #au type="date" (change)="filtrer(du.value, au.value, compte.value)" /></label>
        <label class="f">
          Secrétaire
          <select class="inp" #compte (change)="filtrer(du.value, au.value, compte.value)">
            <option value="">Toutes</option>
            @for (c of controle()?.comptes ?? []; track c.id) {
              <option [value]="c.id">{{ c.nom }}</option>
            }
          </select>
        </label>
      </div>
      @if (controle(); as controle) {
        @if (controle.nonCloturees.length > 0) {
          <div class="note warn" role="status">
            <span>
              Caisses pas clôturées :
              @for (n of controle.nonCloturees; track n.compteId + n.jour; let dernier = $last) {
                {{ n.secretaire }} le {{ jourCourt(n.jour) }} ({{ montant(n.attendu) }} TND){{ dernier ? '.' : ',' }}
              }
            </span>
          </div>
        }
        <div class="defile">
          <table>
            <thead>
              <tr><th>Jour</th><th>Secrétaire</th><th class="r">Attendu</th><th class="r">Compté</th><th class="r">Écart</th><th>Argent reçu</th></tr>
            </thead>
            <tbody>
              @for (cloture of controle.clotures; track cloture.id) {
                <tr>
                  <td class="num">{{ jourCourt(cloture.jour) }}{{ cloture.rang > 1 ? ' (' + cloture.rang + ')' : '' }}</td>
                  <td>{{ cloture.secretaire }}</td>
                  <td class="r num">{{ montant(cloture.attendu) }}</td>
                  <td class="r num">{{ montant(cloture.compte) }}</td>
                  <td class="r num" [class.bad]="cloture.ecart < 0">{{ ecrireUnEcart(cloture.ecart) }}</td>
                  <td>
                    <label class="coche">
                      <input type="checkbox" [checked]="cloture.argentRecu" [disabled]="envoi()" (change)="marquer(cloture)" />
                      {{ cloture.argentRecu ? 'Reçu' : 'À récupérer' }}
                    </label>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="6" class="vide">Aucune clôture sur cette période.</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </section>
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    .entete { display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 10px; }
    .filtres { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 10px; align-items: end; }
    .defile { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    th, td { text-align: left; padding: 8px 6px; border-top: 1px solid var(--line); white-space: nowrap; }
    th { color: var(--ink-2); font-weight: 500; border-top: 0; }
    .r { text-align: right; }
    td.bad { color: var(--bad); font-weight: 600; }
    .coche { display: inline-flex; align-items: center; gap: 6px; cursor: pointer; }
  `,
})
export class ControleDesCloturesComponent implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly montant = ecrireUnMontant;
  protected readonly ecrireUnEcart = ecrireUnEcart;
  protected readonly jourCourt = jourCourt;
  protected readonly controle = signal<ControleDesClotures | null>(null);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);
  private filtres: Record<string, string> = {};

  async ngOnInit(): Promise<void> {
    await this.charger();
  }

  protected async filtrer(du: string, au: string, compteId: string): Promise<void> {
    this.filtres = Object.fromEntries(Object.entries({ du, au, compteId }).filter(([, valeur]) => valeur !== ''));
    await this.charger();
  }

  protected async marquer(cloture: Cloture): Promise<void> {
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      await firstValueFrom(this.http.put<Cloture>(`/api/caisse/clotures/${cloture.id}/argent-recu`, { recu: !cloture.argentRecu }));
      await this.charger();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** Relit les clôtures, avec les mêmes filtres (après une clôture sur la même page). */
  async recharger(): Promise<void> {
    await this.charger();
  }

  private async charger(): Promise<void> {
    try {
      this.controle.set(await firstValueFrom(this.http.get<ControleDesClotures>('/api/caisse/clotures', { params: this.filtres })));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }
}
