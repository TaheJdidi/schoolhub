import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { MessageWhatsApp } from '../inscriptions/inscription';
import { Icone } from '../../socle/icone';

/** CAI-01 : où en est un élève avec ses paiements. */
export type Couleur = 'AJour' | 'APayer' | 'Orange' | 'Rouge';

/** CAI-02 : le badge d'un groupe. */
export interface BadgeDuGroupe {
  etat: 'Paye' | 'Rappels' | 'APayer';
  nombre: number;
}

/** CAI-08 : le rappel de paiement d'un élève (GET /api/eleves/{id}/rappel). */
export interface RappelDePaiement {
  couleur: Couleur;
  texte: string;
  messages: MessageWhatsApp[];
  dernierRappelLe: string | null;
  /** Ce que l'élève doit, une ligne par somme, pour la fenêtre du rappel. */
  lignes?: { libelle: string; montant: number }[];
  total?: number;
}

/** Un élève en rouge, pour l'accueil (GET /api/eleves/en-rouge). */
export interface EleveEnRouge {
  id: string;
  prenom: string;
  nom: string;
  dues: string[];
  total: number;
  dernierRappelLe: string | null;
}

const PASTILLES: Record<Couleur, { libelle: string; classe: string }> = {
  AJour: { libelle: 'À jour', classe: 'ok' },
  APayer: { libelle: 'À payer', classe: 'info' },
  Orange: { libelle: 'Orange', classe: 'warn' },
  Rouge: { libelle: 'Rouge', classe: 'bad' },
};

/** Un élève orange ou rouge reçoit un rappel de paiement. */
export const enRetard = (couleur: string | null | undefined) => couleur === 'Orange' || couleur === 'Rouge';

/** « rappel envoyé le 12/10 ». */
export function rappelEnvoyeLe(date: string): string {
  return `rappel envoyé le ${formatDate(date, 'dd/MM', FRANCAIS)}`;
}

/** CAI-01 : la pastille de la couleur d'un élève ou d'une inscription ; rien sans couleur (prix spécial à 0 TND). */
@Component({
  selector: 'sh-pastille-de-couleur',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (pastille(); as pastille) {
      <span class="pill" [class]="'pill ' + pastille.classe">{{ pastille.libelle }}</span>
    }
  `,
  styles: `
    .pill.bad { background: var(--bad-soft); color: var(--bad); }
  `,
})
export class PastilleDeCouleur {
  readonly couleur = input<string | null | undefined>(null);
  protected readonly pastille = computed(() => {
    const couleur = this.couleur();
    return couleur && couleur in PASTILLES ? PASTILLES[couleur as Couleur] : null;
  });
}

/** CAI-02 : « Groupe payé », « 3 rappels à envoyer » ou « 2 à payer ». Rien pour un groupe sans élève. */
@Component({
  selector: 'sh-badge-du-groupe',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (badge(); as badge) {
      <span class="pill" [class]="'pill ' + classe()">{{ libelle() }}</span>
    }
  `,
})
export class BadgeDuGroupeComponent {
  readonly badge = input<BadgeDuGroupe | null | undefined>(null);
  protected readonly libelle = computed(() => {
    const badge = this.badge();
    if (!badge) return '';
    if (badge.etat === 'Paye') return 'Groupe payé';
    if (badge.etat === 'Rappels') return badge.nombre > 1 ? `${badge.nombre} rappels à envoyer` : '1 rappel à envoyer';
    return `${badge.nombre} à payer`;
  });
  protected readonly classe = computed(() => {
    const etat = this.badge()?.etat;
    return etat === 'Paye' ? 'ok' : etat === 'Rappels' ? 'warn' : 'info';
  });
}

/**
 * CAI-08 : « Envoyer un rappel » pour un élève orange ou rouge. Une fenêtre s'ouvre : ce qu'il doit, ligne par ligne, le
 * total, le message, et un bouton WhatsApp par destinataire ; un message ouvert est noté, et « rappel envoyé le 12/10 » s'affiche pour ne pas l'envoyer deux fois.
 */
@Component({
  selector: 'sh-bouton-de-rappel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icone],
  host: { '(document:keydown.escape)': 'fermer()' },
  template: `
    <!-- La cloche (dans l'appel) ou « Envoyer un rappel » (ailleurs) ouvrent la même fenêtre. -->
    <span class="rappel">
      @if (compact()) {
        <button type="button" class="cloche" [disabled]="envoi()" [attr.aria-label]="'Envoyer un rappel' + (pour() ? ' à ' + pour() : '')"
                title="Envoyer un rappel" (click)="ouvrir()"><sh-icone nom="bell" /></button>
      } @else {
        <button type="button" class="btn" [disabled]="envoi()" (click)="ouvrir()">Envoyer un rappel</button>
        @if (dernier(); as dernier) {
          <small class="dernier">{{ envoyeLe(dernier) }}</small>
        }
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </span>
    @if (rappel(); as rappel) {
      <div class="fond-de-fenetre" (click)="fermer()"></div>
      <section class="fenetre-du-rappel" role="dialog" aria-modal="true" aria-labelledby="titre-du-rappel">
        <div class="haut">
          <div>
            <h2 id="titre-du-rappel">Rappel de paiement</h2>
            @if (pour()) {
              <p class="qui">{{ pour() }}</p>
            }
          </div>
          <button type="button" class="fermer" aria-label="Fermer" (click)="fermer()"><sh-icone nom="x" /></button>
        </div>
        @if (rappel.lignes?.length) {
          <ul class="lignes">
            @for (ligne of rappel.lignes; track $index) {
              <li><span>{{ ligne.libelle }}</span><b class="num">{{ montant(ligne.montant) }}</b></li>
            }
          </ul>
          <p class="total"><span>Total à payer</span><b class="num">{{ montant(rappel.total ?? 0) }} TND</b></p>
          <details class="apercu">
            <summary>Voir le message</summary>
            <p class="texte">{{ rappel.texte }}</p>
          </details>
        } @else {
          <p class="texte">{{ rappel.texte }}</p>
        }
        <div class="destinataires">
          @for (message of rappel.messages; track message.lien) {
            <div class="destinataire">
              <span>{{ pourQui(message) }}</span>
              <a class="btn whatsapp" [href]="message.lien" target="_blank" rel="noopener" (click)="noter()">Ouvrir WhatsApp</a>
            </div>
          } @empty {
            <p class="aide">Aucun téléphone pour envoyer le rappel.</p>
          }
        </div>
        <p class="pied">{{ dernier() ? envoyeLe(dernier()!) : 'Aucun rappel envoyé pour l’instant.' }}</p>
      </section>
    }
  `,
  styles: `
    .rappel { display: inline-grid; gap: 6px; justify-items: start; }
    .apercu summary { cursor: pointer; color: var(--accent); font-size: 13px; }
    .apercu .texte { margin-top: 8px; }
    .texte { margin: 0; padding: 8px 10px; border-radius: 10px; background: var(--sunk); white-space: pre-line; font-size: 13px; }
    .destinataire { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .dernier { color: var(--ink-2); font-size: 12.5px; }
    .aide { margin: 0; color: var(--ink-2); }
    .note { margin: 0; }
    .cloche { display: inline-flex; align-items: center; justify-content: center; width: 26px; height: 26px; padding: 0;
      border: 1px solid var(--line-2); border-radius: 8px; background: var(--surface); color: var(--bad); cursor: pointer; }
    .cloche:hover:not(:disabled) { background: var(--bad-soft); }
    .cloche svg { width: 15px; height: 15px; }
    .fond-de-fenetre { position: fixed; inset: 0; z-index: 60; background: rgb(15 23 42 / 0.45); }
    .fenetre-du-rappel { position: fixed; z-index: 61; top: 50%; left: 50%; transform: translate(-50%, -50%);
      width: min(440px, calc(100vw - 32px)); max-height: calc(100vh - 32px); overflow: auto; display: grid; gap: 14px;
      padding: 20px 22px; border: 1px solid var(--line); border-radius: 14px; background: var(--surface); color: var(--ink);
      box-shadow: 0 12px 40px rgb(15 23 42 / 0.25); text-align: left; white-space: normal; }
    .haut { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
    .haut h2 { margin: 0; font-size: 18px; }
    .qui { margin: 2px 0 0; color: var(--ink-2); }
    .fermer { display: inline-flex; padding: 6px; border: 0; border-radius: 8px; background: none; color: var(--ink-2); cursor: pointer; }
    .fermer:hover { background: var(--sunk); }
    .fermer svg { width: 18px; height: 18px; }
    .lignes { display: grid; margin: 0; padding: 0; list-style: none; border-top: 1px solid var(--line); }
    .lignes li { display: flex; justify-content: space-between; gap: 12px; padding: 8px 0; border-bottom: 1px solid var(--line); font-size: 14px; }
    .lignes b { font-weight: 500; white-space: nowrap; }
    .total { display: flex; justify-content: space-between; margin: -6px 0 0; font-size: 15px; font-weight: 600; }
    .destinataires { display: grid; gap: 8px; padding: 12px; border-radius: 10px; background: var(--sunk); }
    .destinataires .destinataire { justify-content: space-between; }
    .whatsapp { border-color: var(--ok); color: var(--ok); }
    .whatsapp:hover { background: var(--ok-soft); }
    .pied { margin: 0; color: var(--ink-2); font-size: 13px; }
  `,
})
export class BoutonDeRappel {
  private readonly http = inject(HttpClient);

  readonly eleveId = input.required<string>();
  /** La date du dernier rappel, déjà connue de la page. */
  readonly dernierRappelLe = input<string | null | undefined>(null);
  /** Une cloche seule, à côté du nom (l'appel) ; sinon « Envoyer un rappel ». Les deux ouvrent la même fenêtre. */
  readonly compact = input(false);
  /** « Adem Miled », pour le nom du bouton lu à voix haute. */
  readonly pour = input<string | null>(null);

  protected readonly rappel = signal<RappelDePaiement | null>(null);
  private readonly noteLe = signal<string | null>(null);
  protected readonly dernier = computed(() => this.noteLe() ?? this.rappel()?.dernierRappelLe ?? this.dernierRappelLe() ?? null);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);
  protected readonly envoyeLe = rappelEnvoyeLe;

  protected async ouvrir(): Promise<void> {
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      this.rappel.set(await firstValueFrom(this.http.get<RappelDePaiement>(`/api/eleves/${this.eleveId()}/rappel`)));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  protected readonly montant = ecrireUnMontant;

  /** La fenêtre se referme (✕, Échap ou un toucher à côté). */
  protected fermer(): void {
    this.rappel.set(null);
  }

  /** Le message s'ouvre dans WhatsApp ; le rappel est noté, sans attendre. */
  protected async noter(): Promise<void> {
    try {
      await firstValueFrom(this.http.post(`/api/eleves/${this.eleveId()}/rappel/envoye`, null));
      this.noteLe.set(new Date().toISOString());
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** « Au parent · 98 401 220 », « À Adem · 22 345 678 ». */
  protected pourQui(message: MessageWhatsApp): string {
    const qui = message.destinataire === 'Parent' ? 'Au parent' : `À ${message.destinataire}`;
    return `${qui} · ${message.telephone}`;
  }
}
