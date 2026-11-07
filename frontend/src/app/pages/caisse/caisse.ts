import { ActionsDeListe } from '../../socle/actions-de-liste';
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
import { AchatDecrit, AchatsEtTotaux } from '../achats/achats';

/**
 * CAI-06 : la page « Caisse ». Les reçus du jour, les plus récents en premier, avec leur total ; puis la recherche d'un
 * reçu par numéro, par élève ou par date, parmi tous les reçus du centre. En tête, « Ma caisse » et sa clôture du soir
 * (CAI-09) ; pour le propriétaire, le contrôle des clôtures (CAI-10).
 */
@Component({
  selector: 'sh-caisse',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MaCaisseComponent, ControleDesCloturesComponent, ActionsDeListe],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Ma caisse</h1>
        <p class="sub">{{ personne() }} · {{ dateDuJour }}</p>
      </div>
    </div>

    <!-- Comme le prototype (écran 7) : les chiffres en haut ; les reçus et les sorties à gauche ; la clôture à droite. -->
    <div class="grille-de-la-caisse">
      <sh-ma-caisse (cloturee)="controle()?.recharger()" />
      <section class="card carte recus-du-jour">
        <div class="entete">
          <h2 class="titre">{{ titre() }}</h2>
          @if (recus(); as recus) {
            <span class="compte">{{ recus.length }} {{ recus.length > 1 ? 'reçus' : 'reçu' }} · {{ montant(totalValable()) }} TND</span>
          }
          <sh-actions-de-liste [titre]="titre()" [nombre]="recus()?.length ?? 0" exporter="/api/exports/recus" [filtres]="filtresDExport()" />
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
          <p class="aide indication">Touchez un reçu pour le remettre ou l'annuler.</p>
          <div class="liste">
            @if (recus.length > 0) {
              <div class="recu en-tete-du-tableau" aria-hidden="true"><span>N°</span><span>Heure</span><span>Élève</span><span class="r">TND</span></div>
            }
            @for (recu of recus; track recu.id) {
              <a class="recu" [class.annule]="recu.annule" [routerLink]="['/caisse/recus', recu.id]">
                <span class="num">{{ recu.numero }}</span>
                <span class="num">{{ heure(recu.creeLe) }}</span>
                <span class="deux">
                  <b>
                    {{ recu.eleve }}
                    @if (recu.annule) {
                      <span class="pill muted">Annulé</span>
                    }
                  </b>
                  <small>Reçu n° {{ recu.numero }} · {{ date(recu.jour) }} · {{ recu.secretaire }}</small>
                </span>
                <b class="montant num r">{{ montant(recu.total) }}</b>
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

      <!-- Les achats payés aujourd'hui depuis ma caisse : ils sont déduits des espèces attendues. -->
      <section class="card carte sorties">
        <div class="entete">
          <h2 class="titre">Sorties de caisse</h2>
          <a class="btn" routerLink="/achats">+ Saisir un achat</a>
        </div>
        @for (achat of sorties(); track achat.id) {
          <div class="sortie">
            <span class="num">{{ montant(achat.montant) }} TND</span>
            <span>{{ achat.libelle }}{{ achat.fournisseur ? ' (' + achat.fournisseur + ')' : '' }}</span>
          </div>
        } @empty {
          <p class="aide">Aucune sortie de caisse aujourd'hui.</p>
        }
      </section>

      @if (proprietaire()) {
        <div class="controle"><sh-controle-des-clotures /></div>
      }
    </div>
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    .grille-de-la-caisse { display: grid; grid-template-columns: minmax(0, 1.65fr) minmax(0, 1fr); gap: 16px; align-items: start;
      grid-template-areas: 'chiffres chiffres' 'recus cloture' 'sorties cloture' 'controle controle'; }
    @media (max-width: 1000px) {
      .grille-de-la-caisse { grid-template-columns: minmax(0, 1fr); grid-template-areas: 'chiffres' 'cloture' 'recus' 'sorties' 'controle'; }
    }
    .recus-du-jour { grid-area: recus; }
    .sorties { grid-area: sorties; }
    .controle { grid-area: controle; }
    .indication { margin: 0; font-size: 13px; }
    .sortie { display: flex; gap: 14px; padding: 8px 0; border-top: 1px solid var(--line); }
    .sortie:first-of-type { border-top: 0; }
    .compte { color: var(--ink-2); font-size: 13px; }
    .filtres { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 10px; align-items: end; }
    .liste { display: grid; }
    .recu { display: grid; grid-template-columns: 56px 64px minmax(0, 1fr) auto; align-items: center; gap: 10px; padding: 9px 4px; border-top: 1px solid var(--line); color: var(--ink); text-decoration: none; }
    .en-tete-du-tableau { color: var(--ink-2); font-size: 12.5px; }
    .r { text-align: right; }
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
  /** « Taha Jdidi », pour « Ma caisse ». */
  protected readonly personne = computed(() => {
    const moi = this.session.personne();
    return moi ? `${moi.prenom} ${moi.nom}` : '';
  });
  /** « samedi 7 novembre 2026 ». */
  protected readonly dateDuJour = formatDate(new Date(), 'EEEE d MMMM y', FRANCAIS);
  /** Les achats payés aujourd'hui depuis ma caisse. */
  protected readonly sorties = signal<AchatDecrit[]>([]);
  protected readonly erreur = signal<string | null>(null);
  private readonly recherche = signal<{ numero: string; eleve: string; jour: string }>({ numero: '', eleve: '', jour: this.aujourdhui });
  /** LST-02 : l'export suit la recherche en cours. */
  protected readonly filtresDExport = computed(() => this.recherche());

  protected readonly titre = computed(() => {
    const { numero, eleve, jour } = this.recherche();
    if (!numero && !eleve && jour === this.aujourdhui) return 'Reçus du jour';
    return 'Reçus trouvés';
  });

  /** Le total des reçus qui valent : un reçu annulé ne compte pas. */
  protected readonly totalValable = computed(() => (this.recus() ?? []).filter((r) => !r.annule).reduce((total, r) => total + r.total, 0));

  async ngOnInit(): Promise<void> {
    await Promise.all([this.charger(), this.chargerLesSorties()]);
  }

  /** Les achats du mois, puis ceux d'aujourd'hui payés depuis ma caisse ; sans eux, la page reste utile. */
  private async chargerLesSorties(): Promise<void> {
    try {
      const { achats } = await firstValueFrom(this.http.get<AchatsEtTotaux>('/api/achats', { params: { mois: this.aujourdhui.slice(0, 7) } }));
      this.sorties.set(achats.filter((a) => a.payeDepuisLaCaisse && a.jour === this.aujourdhui && a.auteur === this.personne()));
    } catch {
      this.sorties.set([]);
    }
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
