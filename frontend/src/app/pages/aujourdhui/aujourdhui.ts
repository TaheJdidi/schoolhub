import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { NombreDeDemandes } from '../../socle/nombre-de-demandes';
import { heure, ResumeDuGroupe } from '../groupes/groupe';
import { AppelARecopier, GroupeDeLAppel } from '../seances/appel';
import { BadgeDuGroupe, BoutonDeRappel, EleveEnRouge } from '../caisse/paiement';
import { ecrireUnMontant } from '../../socle/montants';
import { MaCaisse } from '../caisse/caisse-du-soir';

/** SEA-01 : une séance d'aujourd'hui (GET /api/seances/du-jour). */
export interface SeanceDuJour {
  seanceId: string;
  debut: string;
  fin: string;
  groupe: GroupeDeLAppel;
  professeur: string;
  attendus: number;
  etat: 'AppelFait' | 'AppelAFaire' | 'Annulee';
}

/** Où en est une séance du jour : son appel, ou l'heure qu'il est. */
type Moment = 'appel-fait' | 'annulee' | 'en-cours' | 'a-venir' | 'appel-a-faire';

const MOMENTS: Record<Moment, { libelle: string; classe: string }> = {
  'appel-fait': { libelle: 'Appel fait', classe: 'ok' },
  annulee: { libelle: 'Annulée', classe: 'muted' },
  'en-cours': { libelle: 'En cours', classe: 'info' },
  'a-venir': { libelle: 'À venir', classe: 'muted' },
  'appel-a-faire': { libelle: 'Appel à faire', classe: 'warn' },
};

/** « 10:42 » : les minutes depuis minuit, pour comparer aux heures des séances (« 10:00 »). */
const minutes = (texte: string) => {
  const [h, m] = texte.split(':').map(Number);
  return h * 60 + (m || 0);
};

/**
 * SEA-01 : l'accueil « Aujourd'hui », sur toute la largeur. En haut, quatre chiffres : la caisse du jour, les appels à
 * recopier (SEA-09), les demandes d'inscription (DEM-03) et la bibliothèque. Puis les séances du jour, heure par heure,
 * avec ce qu'il reste à relancer dans chaque groupe (CAI-02) et où en est la séance (un clic ouvre l'appel) ; à droite,
 * les appels à recopier et les élèves en rouge (CAI-08).
 */
@Component({
  selector: 'sh-aujourdhui',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, BoutonDeRappel],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Aujourd'hui</h1>
        <p class="sub">{{ dateDuJour }} · il est {{ heureQuIlEst() }}</p>
      </div>
    </div>

    <div class="chiffres">
      <a class="chiffre" routerLink="/caisse">
        <span>Caisse du jour</span>
        <b><span class="num">{{ montant(caisse()?.encaisse ?? 0) }}</span> <small>TND</small></b>
        <small>
          {{ caisse()?.recus ?? 0 }} reçu{{ (caisse()?.recus ?? 0) > 1 ? 's' : '' }} ·
          {{ montant(caisse()?.attendu ?? 0) }} TND en espèces attendues
        </small>
      </a>
      <div class="chiffre">
        <span>Appels à recopier</span>
        <b class="num" [class.attention]="aRecopier().length > 0">{{ aRecopier().length }}</b>
        <small>Feuilles revenues, pas encore saisies</small>
      </div>
      <a class="chiffre" routerLink="/demandes">
        <span>Demandes d'inscription</span>
        <b class="num">{{ demandes() ?? 0 }}</b>
        <small>{{ demandes() === 1 ? 'demande d’inscription à traiter' : 'demandes d’inscription à traiter' }}</small>
      </a>
      <a class="chiffre" routerLink="/bibliotheque">
        <span>Bibliothèque</span>
        <b>
          <span class="num">{{ bibliotheque()?.presents ?? 0 }}</span>
          @if (bibliotheque()?.capacite; as capacite) {
            <small> / {{ capacite }}</small>
          }
        </b>
        <small>{{ bibliotheque()?.entrees ?? 0 }} entrée{{ (bibliotheque()?.entrees ?? 0) > 1 ? 's' : '' }} depuis ce matin</small>
      </a>
    </div>

    <div class="colonnes">
      <section class="card bloc" aria-labelledby="titre-seances">
        <div class="entete">
          <h2 id="titre-seances" class="titre">Séances du jour</h2>
          <small>{{ seances().length }} séance{{ seances().length > 1 ? 's' : '' }} · {{ attendus() }} élève{{ attendus() > 1 ? 's' : '' }} attendu{{ attendus() > 1 ? 's' : '' }}</small>
        </div>
        @for (seance of seances(); track seance.seanceId) {
          <a class="seance" [class.en-cours]="moment(seance) === 'en-cours'" [class.annulee]="seance.etat === 'Annulee'"
             [routerLink]="['/professeurs/groupes', seance.groupe.id]" [queryParams]="{ seance: seance.seanceId }">
            <span class="heures num"><b>{{ heure(seance.debut) }}</b><small>{{ heure(seance.fin) }}</small></span>
            <span class="deux">
              <b>{{ seance.groupe.matiere }}@if (seance.groupe.niveau) { · {{ seance.groupe.niveau }}}</b>
              <small>{{ seance.professeur }} · {{ seance.groupe.nom }} · {{ seance.attendus }} élève{{ seance.attendus > 1 ? 's' : '' }}</small>
            </span>
            <span class="etiquettes">
              @if (aRelancer(seance); as relance) {
                <span class="pill" [class]="'pill ' + relance.classe">{{ relance.libelle }}</span>
              }
              <span class="pill" [class]="'pill ' + moments[moment(seance)].classe">{{ moments[moment(seance)].libelle }}</span>
            </span>
            <span class="chevron" aria-hidden="true">›</span>
          </a>
        } @empty {
          <p class="vide">Aucune séance aujourd'hui.</p>
        }
      </section>

      <div class="cote">
        <section class="card bloc" aria-labelledby="titre-a-recopier">
          <div class="entete">
            <h2 id="titre-a-recopier" class="titre">Appels à recopier</h2>
            <span class="pill warn">{{ aRecopier().length }}</span>
          </div>
          @for (seance of aRecopier(); track seance.seanceId) {
            <div class="ligne">
              <span class="deux">
                <b>{{ jour(seance.date) }} · {{ heure(seance.debut) }} → {{ heure(seance.fin) }}</b>
                <small>{{ seance.groupe.matiere }}@if (seance.groupe.niveau) { · {{ seance.groupe.niveau }}} · {{ seance.groupe.nom }} · {{ seance.professeur }}</small>
              </span>
              <a class="btn sm" [routerLink]="['/professeurs/groupes', seance.groupe.id]" [queryParams]="{ seance: seance.seanceId }">Recopier</a>
            </div>
          } @empty {
            <p class="vide">Aucun appel à recopier.</p>
          }
        </section>

        <!-- CAI-08 : les élèves en rouge, avec ce qu'ils doivent, et leur rappel de paiement. -->
        <section class="card bloc" aria-labelledby="titre-en-rouge">
          <div class="entete">
            <h2 id="titre-en-rouge" class="titre">Élèves en rouge</h2>
            <small>2 séances de retard ou plus</small>
          </div>
          @for (eleve of enRouge(); track eleve.id) {
            <div class="ligne en-rouge">
              <span class="point" aria-hidden="true"></span>
              <span class="deux">
                <a [routerLink]="['/eleves', eleve.id]"><b>{{ eleve.prenom }} {{ eleve.nom }}</b></a>
                <small>{{ eleve.dues.join(', ') }}</small>
              </span>
              <span class="du">
                <b class="num">{{ montant(eleve.total) }} TND</b>
                <sh-bouton-de-rappel [eleveId]="eleve.id" [dernierRappelLe]="eleve.dernierRappelLe" />
              </span>
            </div>
          } @empty {
            <p class="vide">Aucun élève en rouge.</p>
          }
        </section>
      </div>
    </div>

    @if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styles: `
    :host { display: grid; gap: 16px; }
    .pagehead { margin-bottom: 0; }
    .chiffres { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; }
    .chiffre { display: grid; gap: 4px; align-content: start; padding: 16px 18px; border-radius: 14px; background: var(--sunk); color: var(--ink); text-decoration: none; }
    a.chiffre:hover { outline: 1px solid var(--line-2); }
    .chiffre > span, .chiffre > small { color: var(--ink-2); font-size: 13px; }
    .chiffre b { font-family: var(--font-display); font-size: 26px; font-weight: 600; line-height: 1.2; }
    .chiffre b small { margin-left: 4px; font-size: 14px; color: var(--ink-2); font-weight: 500; }
    .chiffre b.attention { color: var(--warn); }
    .colonnes { display: grid; grid-template-columns: minmax(0, 1.7fr) minmax(0, 1fr); gap: 16px; align-items: start; }
    @media (max-width: 960px) { .colonnes { grid-template-columns: minmax(0, 1fr); } }
    .cote { display: grid; gap: 16px; }
    .bloc { display: grid; padding: 0; overflow: hidden; }
    .entete { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 14px 18px; border-bottom: 1px solid var(--line); }
    .entete small { color: var(--ink-2); }
    .titre { font-family: var(--font-display); font-size: 17px; font-weight: 600; margin: 0; }
    .seance { display: grid; grid-template-columns: 64px minmax(0, 1fr) auto 14px; align-items: center; gap: 12px; padding: 12px 18px; border-top: 1px solid var(--line); color: var(--ink); text-decoration: none; }
    .seance:first-of-type { border-top: 0; }
    .seance:hover { background: var(--sunk); }
    .seance.en-cours { background: var(--accent-soft); }
    .seance.annulee .deux b { text-decoration: line-through; }
    .heures { display: grid; line-height: 1.2; }
    .heures b { font-weight: 500; }
    .heures small { color: var(--ink-2); font-size: 12px; }
    .etiquettes { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 6px; }
    .chevron { color: var(--ink-3); font-size: 18px; }
    .deux { display: grid; min-width: 0; }
    .deux b { font-weight: 600; overflow-wrap: anywhere; }
    .deux small { color: var(--ink-2); font-size: 13px; }
    .ligne { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 18px; border-top: 1px solid var(--line); }
    .ligne:first-of-type { border-top: 0; }
    .ligne .deux { flex: 1; }
    .vide { margin: 0; padding: 14px 18px; color: var(--ink-2); }
    .en-rouge a { color: var(--ink); text-decoration: none; }
    .point { flex: none; width: 10px; height: 10px; border-radius: 50%; background: var(--bad); }
    .du { display: grid; justify-items: end; gap: 4px; }
    .du b { color: var(--bad); font-weight: 500; }
    .note { margin: 0; }
  `,
})
export class Aujourdhui implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly demandes = inject(NombreDeDemandes).aTraiter;
  protected readonly heure = heure;
  protected readonly moments = MOMENTS;
  protected readonly dateDuJour = majuscule(formatDate(new Date(), 'EEEE d MMMM y', FRANCAIS));
  /** L'heure qu'il est, « 10h42 » : elle avance toute seule, et dit quelle séance est en cours. */
  private readonly maintenant = signal(new Date());
  protected readonly heureQuIlEst = computed(() => formatDate(this.maintenant(), "H'h'mm", FRANCAIS));
  protected readonly seances = signal<SeanceDuJour[]>([]);
  protected readonly attendus = computed(() => this.seances().filter((s) => s.etat !== 'Annulee').reduce((total, s) => total + s.attendus, 0));
  protected readonly caisse = signal<{ encaisse: number; attendu: number; recus: number } | null>(null);
  protected readonly bibliotheque = signal<{ presents: number; capacite: number | null; entrees: number } | null>(null);
  protected readonly aRecopier = signal<AppelARecopier[]>([]);
  protected readonly enRouge = signal<EleveEnRouge[]>([]);
  /** CAI-02 : le badge de chaque groupe, pour « 5 à relancer » sur ses séances du jour. */
  private readonly badges = signal<ReadonlyMap<string, BadgeDuGroupe>>(new Map());
  protected readonly montant = ecrireUnMontant;
  protected readonly erreur = signal<string | null>(null);

  constructor() {
    const minuterie = setInterval(() => this.maintenant.set(new Date()), 30_000);
    inject(DestroyRef).onDestroy(() => clearInterval(minuterie));
  }

  /** Chaque liste s'affiche dès qu'elle arrive : l'une n'attend pas l'autre. */
  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.charger('/api/seances/du-jour', (liste: SeanceDuJour[]) => this.seances.set(liste)),
      this.charger('/api/caisse', (caisse: MaCaisse) => {
        // La caisse du jour : la dernière ouverte (celle d'aujourd'hui).
        const duJour = caisse.ouvertes.at(-1);
        this.caisse.set({ encaisse: duJour?.encaisse ?? 0, attendu: duJour?.attendu ?? 0, recus: duJour?.recus ?? 0 });
      }),
      this.charger('/api/bibliotheque', (journee: { presents: number; capacite: number | null; entrees: number }) => this.bibliotheque.set(journee)),
      this.charger('/api/seances/a-recopier', (liste: AppelARecopier[]) => this.aRecopier.set(liste)),
      this.charger('/api/eleves/en-rouge', (liste: EleveEnRouge[]) => this.enRouge.set(liste)),
      this.charger('/api/groupes', (groupes: ResumeDuGroupe[]) =>
        this.badges.set(new Map(groupes.filter((g) => g.badge).map((g) => [g.id, g.badge!]))),
      ),
    ]);
  }

  /** Où en est la séance : son appel s'il est fait, sinon l'heure qu'il est. */
  protected moment(seance: SeanceDuJour): Moment {
    if (seance.etat === 'AppelFait') return 'appel-fait';
    if (seance.etat === 'Annulee') return 'annulee';
    const maintenant = minutes(formatDate(this.maintenant(), 'HH:mm', FRANCAIS));
    if (maintenant < minutes(seance.debut)) return 'a-venir';
    return maintenant < minutes(seance.fin) ? 'en-cours' : 'appel-a-faire';
  }

  /** « 5 à relancer » (des rappels à envoyer) ou « 2 à payer » dans le groupe de la séance ; rien s'il est payé. */
  protected aRelancer(seance: SeanceDuJour): { libelle: string; classe: string } | null {
    const badge = this.badges().get(seance.groupe.id);
    if (!badge || badge.etat === 'Paye') return null;
    return badge.etat === 'Rappels' ? { libelle: `${badge.nombre} à relancer`, classe: 'bad' } : { libelle: `${badge.nombre} à payer`, classe: 'warn' };
  }

  private async charger<T>(adresse: string, montrer: (valeur: T) => void): Promise<void> {
    try {
      montrer(await firstValueFrom(this.http.get<T>(adresse)));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** « jeu. 29/10 ». */
  protected jour(date: string): string {
    return formatDate(date, 'EEE dd/MM', FRANCAIS);
  }
}

/** « Samedi 31 octobre 2026 ». */
function majuscule(texte: string): string {
  return texte.charAt(0).toUpperCase() + texte.slice(1);
}
