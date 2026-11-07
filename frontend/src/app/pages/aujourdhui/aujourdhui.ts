import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { NombreDeDemandes } from '../../socle/nombre-de-demandes';
import { Session } from '../../socle/session/session';
import { heure } from '../groupes/groupe';
import { AppelARecopier } from '../seances/appel';
import { BoutonDeRappel, EleveEnRouge } from '../caisse/paiement';
import { ecrireUnMontant } from '../../socle/montants';
import { MaCaisse } from '../caisse/caisse-du-soir';

/** SEA-01 : une séance d'aujourd'hui (GET /api/seances/du-jour). */
export interface SeanceDuJour {
  seanceId: string;
  debut: string;
  fin: string;
  groupe: { id: string; nom: string; type: string; matiere: string };
  professeur: string;
  attendus: number;
  etat: 'AppelFait' | 'AppelAFaire' | 'Annulee';
}

const ETATS = { AppelFait: 'appel fait', AppelAFaire: 'appel à faire', Annulee: 'annulée' } as const;

/**
 * SEA-01 : l'accueil « Aujourd'hui ». Les séances du jour, heure par heure (un clic ouvre l'appel) ; ma caisse du jour et
 * les présents à la bibliothèque ; le nombre de demandes d'inscription à traiter (DEM-03), les appels à recopier
 * (SEA-09) et les élèves en rouge (CAI-08).
 */
@Component({
  selector: 'sh-aujourdhui',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, BoutonDeRappel],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Bonjour {{ session.personne()?.prenom }}</h1>
        <p class="sub">{{ dateDuJour }}</p>
      </div>
    </div>
    <div class="blocs">
      <section class="card recopier" aria-labelledby="titre-seances">
        <h2 id="titre-seances" class="titre">Séances du jour · {{ seances().length }}</h2>
        @for (seance of seances(); track seance.seanceId) {
          <a class="ligne seance" [class.annulee]="seance.etat === 'Annulee'"
             [routerLink]="['/professeurs/groupes', seance.groupe.id, 'appel', seance.seanceId]">
            <span class="heures num">{{ heure(seance.debut) }} → {{ heure(seance.fin) }}</span>
            <span class="deux">
              <b>{{ seance.groupe.matiere }} · {{ seance.groupe.nom }}</b>
              <small>{{ seance.professeur }} · {{ seance.attendus }} élève{{ seance.attendus > 1 ? 's' : '' }} attendu{{ seance.attendus > 1 ? 's' : '' }}</small>
            </span>
            <span class="pill" [class.ok]="seance.etat === 'AppelFait'" [class.warn]="seance.etat === 'AppelAFaire'" [class.muted]="seance.etat === 'Annulee'">{{ etat(seance) }}</span>
          </a>
        } @empty {
          <p class="aide">Aucune séance aujourd'hui.</p>
        }
      </section>
      <div class="chiffres">
        @if (caisse(); as caisse) {
          <a class="card chiffre" routerLink="/caisse">
            <span>Ma caisse du jour</span>
            <b class="num">{{ montant(caisse.attendu) }} TND</b>
            <small>{{ caisse.recus }} reçu{{ caisse.recus > 1 ? 's' : '' }}{{ caisse.cloturees > 0 ? ' · ' + caisse.cloturees + ' clôture' + (caisse.cloturees > 1 ? 's' : '') : '' }}</small>
          </a>
        }
        @if (bibliotheque(); as bibliotheque) {
          <a class="card chiffre" routerLink="/bibliotheque">
            <span>Bibliothèque</span>
            <b class="num">{{ bibliotheque.presents }}{{ bibliotheque.capacite ? ' / ' + bibliotheque.capacite : '' }}</b>
            <small>{{ bibliotheque.entrees }} entrée{{ bibliotheque.entrees > 1 ? 's' : '' }} aujourd'hui</small>
          </a>
        }
      </div>
      @if (demandes(); as nombre) {
        <a class="card demandes" routerLink="/demandes">
          <b>{{ nombre === 1 ? '1 demande d’inscription à traiter' : nombre + ' demandes d’inscription à traiter' }}</b>
          <small>Voir les demandes</small>
        </a>
      }
      @if (aRecopier().length > 0) {
        <section class="card recopier" aria-labelledby="titre-a-recopier">
          <h2 id="titre-a-recopier" class="titre">Appels à recopier · {{ aRecopier().length }}</h2>
          <p class="aide">Les séances passées sans appel : recopiez les feuilles dès leur retour.</p>
          @for (seance of aRecopier(); track seance.seanceId) {
            <div class="ligne">
              <span class="deux">
                <b>{{ seance.groupe.matiere }} · {{ seance.groupe.nom }}</b>
                <small>{{ seance.professeur }} · {{ jour(seance.date) }}, {{ heure(seance.debut) }} → {{ heure(seance.fin) }}</small>
              </span>
              <a class="btn" [routerLink]="['/professeurs/groupes', seance.groupe.id, 'appel', seance.seanceId]">Recopier</a>
            </div>
          }
        </section>
      }
      <!-- CAI-08 : les élèves en rouge, avec ce qu'ils doivent, et leur rappel de paiement. -->
      @if (enRouge().length > 0) {
        <section class="card recopier" aria-labelledby="titre-en-rouge">
          <h2 id="titre-en-rouge" class="titre">Élèves en rouge · {{ enRouge().length }}</h2>
          @for (eleve of enRouge(); track eleve.id) {
            <div class="ligne en-rouge">
              <span class="deux">
                <a [routerLink]="['/eleves', eleve.id]"><b>{{ eleve.prenom }} {{ eleve.nom }}</b></a>
                <small>{{ eleve.dues.join(', ') }} · {{ montant(eleve.total) }} TND</small>
              </span>
              <sh-bouton-de-rappel [eleveId]="eleve.id" [dernierRappelLe]="eleve.dernierRappelLe" />
            </div>
          }
        </section>
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </div>
  `,
  styles: `
    .blocs { display: grid; gap: 16px; max-width: 720px; }
    .seance { color: var(--ink); text-decoration: none; justify-content: flex-start; }
    .seance:hover { background: var(--sunk); }
    .seance .deux { flex: 1; }
    .seance.annulee b { text-decoration: line-through; }
    .heures { min-width: 96px; color: var(--ink-2); font-size: 13px; }
    .chiffres { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; }
    .chiffre { display: grid; gap: 2px; padding: 14px 16px; color: var(--ink); text-decoration: none; }
    .chiffre:hover { background: var(--sunk); }
    .chiffre span, .chiffre small { color: var(--ink-2); font-size: 12.5px; }
    .chiffre b { font-size: 20px; }
    .demandes { display: grid; gap: 2px; padding: 14px 16px; color: var(--ink); text-decoration: none; max-width: 420px; }
    .demandes:hover { background: var(--sunk); }
    .demandes small { color: var(--accent); }
    .recopier { display: grid; gap: 8px; padding: 16px; }
    .titre { font-family: var(--font-display); font-size: 17px; font-weight: 600; margin: 0; }
    .aide { margin: 0; color: var(--ink-2); }
    .ligne { display: flex; align-items: center; justify-content: space-between; gap: 10px; padding: 8px 0; border-top: 1px solid var(--line); }
    .deux { display: grid; min-width: 0; }
    .deux b { font-weight: 500; overflow-wrap: anywhere; }
    .deux small { color: var(--ink-2); font-size: 12.5px; }
    .note { margin: 0; }
    .en-rouge { align-items: flex-start; flex-wrap: wrap; }
    .en-rouge a { color: var(--ink); text-decoration: none; }
  `,
})
export class Aujourdhui implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly session = inject(Session);
  protected readonly demandes = inject(NombreDeDemandes).aTraiter;
  protected readonly heure = heure;
  protected readonly dateDuJour = formatDate(new Date(), 'EEEE d MMMM y', FRANCAIS);
  protected readonly seances = signal<SeanceDuJour[]>([]);
  protected readonly caisse = signal<{ attendu: number; recus: number; cloturees: number } | null>(null);
  protected readonly bibliotheque = signal<{ presents: number; capacite: number | null; entrees: number } | null>(null);
  protected readonly aRecopier = signal<AppelARecopier[]>([]);
  protected readonly enRouge = signal<EleveEnRouge[]>([]);
  protected readonly montant = ecrireUnMontant;
  protected readonly erreur = signal<string | null>(null);

  /** Chaque liste s'affiche dès qu'elle arrive : l'une n'attend pas l'autre. */
  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.charger('/api/seances/du-jour', (liste: SeanceDuJour[]) => this.seances.set(liste)),
      this.chargerUn('/api/caisse', (caisse: MaCaisse) => {
        // La caisse du jour : la dernière ouverte (celle d'aujourd'hui), et ses clôtures du jour.
        const duJour = caisse.ouvertes.at(-1);
        this.caisse.set({ attendu: duJour?.attendu ?? 0, recus: duJour?.recus ?? 0, cloturees: caisse.clotures.length });
      }),
      this.chargerUn('/api/bibliotheque', (journee: { presents: number; capacite: number | null; entrees: number }) => this.bibliotheque.set(journee)),
      this.charger('/api/seances/a-recopier', (liste: AppelARecopier[]) => this.aRecopier.set(liste)),
      this.charger('/api/eleves/en-rouge', (liste: EleveEnRouge[]) => this.enRouge.set(liste)),
    ]);
  }

  protected etat(seance: SeanceDuJour): string {
    return ETATS[seance.etat];
  }

  private async chargerUn<T>(adresse: string, montrer: (valeur: T) => void): Promise<void> {
    try {
      montrer(await firstValueFrom(this.http.get<T>(adresse)));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  private async charger<T>(adresse: string, montrer: (liste: T[]) => void): Promise<void> {
    try {
      montrer(await firstValueFrom(this.http.get<T[]>(adresse)));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** « sam. 20/11 ». */
  protected jour(date: string): string {
    return formatDate(date, 'EEE dd/MM', FRANCAIS);
  }
}
