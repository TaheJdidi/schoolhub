import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { dateDuJourLocale } from '../groupes/groupe';
import { Session } from '../../socle/session/session';
import { ControleDesCloturesComponent } from './controle-des-clotures';
import { MaCaisseComponent } from './ma-caisse';
import { RecuDeLaListe } from './recu';

/**
 * CAI-06 : la page « Caisse ». Les reçus du jour, les plus récents en premier, avec leur total ; puis la recherche d'un
 * reçu par numéro, par élève ou par date, parmi tous les reçus du centre. En tête, « Ma caisse » et sa clôture du soir
 * (CAI-09) ; pour le propriétaire, le contrôle des clôtures (CAI-10).
 */
@Component({
  selector: 'sh-caisse',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MaCaisseComponent, ControleDesCloturesComponent],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Caisse</h1>
        <p class="sub">Votre caisse et sa clôture du soir, puis les reçus. Encaissez depuis la fiche d'un élève ou d'un groupe.</p>
      </div>
    </div>

    <div class="cartes">
      <sh-ma-caisse (cloturee)="controle()?.recharger()" />
      @if (proprietaire()) {
        <sh-controle-des-clotures />
      }
      <section class="card carte">
        <div class="entete">
          <h2 class="titre">{{ titre() }}</h2>
          @if (recus(); as recus) {
            <span class="compte">{{ recus.length }} {{ recus.length > 1 ? 'reçus' : 'reçu' }} · {{ montant(totalValable()) }} TND</span>
          }
        </div>
        <div class="filtres">
          <label class="f">
            Numéro
            <input #numero inputmode="numeric" autocomplete="off" (keydown.enter)="chercher(numero.value, eleve.value, jour.value)" />
          </label>
          <label class="f">
            Élève
            <input #eleve autocomplete="off" (keydown.enter)="chercher(numero.value, eleve.value, jour.value)" />
          </label>
          <label class="f">
            Date
            <input #jour type="date" [value]="aujourdhui" />
          </label>
          <button type="button" class="btn" (click)="chercher(numero.value, eleve.value, jour.value)">Chercher</button>
        </div>
        @if (recus(); as recus) {
          <div class="liste">
            @for (recu of recus; track recu.id) {
              <a class="recu" [class.annule]="recu.annule" [routerLink]="['/caisse/recus', recu.id]">
                <span class="deux">
                  <b>
                    Reçu n° {{ recu.numero }} · {{ recu.eleve }}
                    @if (recu.annule) {
                      <span class="pill muted">Annulé</span>
                    }
                  </b>
                  <small>{{ date(recu.jour) }} à {{ heure(recu.creeLe) }} · {{ recu.secretaire }}</small>
                </span>
                <b class="montant">{{ montant(recu.total) }} TND</b>
              </a>
            } @empty {
              <p class="vide">Aucun reçu.</p>
            }
          </div>
        }
        @if (erreur(); as erreur) {
          <p class="note bad" role="alert">{{ erreur }}</p>
        }
      </section>
    </div>
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    .compte { color: var(--ink-2); font-size: 13px; }
    .filtres { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 10px; align-items: end; }
    .liste { display: grid; }
    .recu { display: flex; align-items: center; gap: 10px; padding: 9px 4px; border-top: 1px solid var(--line); color: var(--ink); text-decoration: none; }
    .recu:first-child { border-top: 0; }
    .recu:hover { background: var(--sunk); }
    .recu.annule .montant { text-decoration: line-through; color: var(--ink-3); }
    .deux { display: grid; flex: 1; min-width: 0; }
    .deux b { font-weight: 500; overflow-wrap: anywhere; }
    .deux small { color: var(--ink-2); font-size: 12.5px; }
  `,
})
export class Caisse implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly session = inject(Session);

  protected readonly controle = viewChild(ControleDesCloturesComponent);
  protected readonly proprietaire = computed(() => this.session.personne()?.role === 'Proprietaire');
  protected readonly montant = ecrireUnMontant;
  protected readonly aujourdhui = dateDuJourLocale();
  protected readonly recus = signal<RecuDeLaListe[] | null>(null);
  protected readonly erreur = signal<string | null>(null);
  private readonly recherche = signal<{ numero: string; eleve: string; jour: string }>({ numero: '', eleve: '', jour: this.aujourdhui });

  protected readonly titre = computed(() => {
    const { numero, eleve, jour } = this.recherche();
    if (!numero && !eleve && jour === this.aujourdhui) return 'Reçus du jour';
    return 'Reçus trouvés';
  });

  /** Le total des reçus qui valent : un reçu annulé ne compte pas. */
  protected readonly totalValable = computed(() => (this.recus() ?? []).filter((r) => !r.annule).reduce((total, r) => total + r.total, 0));

  async ngOnInit(): Promise<void> {
    await this.charger();
  }

  protected async chercher(numero: string, eleve: string, jour: string): Promise<void> {
    this.recherche.set({ numero: numero.trim(), eleve: eleve.trim(), jour });
    await this.charger();
  }

  private async charger(): Promise<void> {
    const { numero, eleve, jour } = this.recherche();
    const params: Record<string, string> = {};
    if (numero) params['numero'] = numero;
    if (eleve) params['eleve'] = eleve;
    if (jour) params['jour'] = jour;
    this.erreur.set(null);
    try {
      this.recus.set(await firstValueFrom(this.http.get<RecuDeLaListe[]>('/api/recus', { params })));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** « 27/11/2027 ». */
  protected date(texte: string): string {
    return formatDate(texte, 'dd/MM/yyyy', FRANCAIS);
  }

  /** « 10:42 ». */
  protected heure(texte: string): string {
    return formatDate(texte, 'HH:mm', FRANCAIS);
  }
}
