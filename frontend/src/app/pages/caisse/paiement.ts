import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { MessageWhatsApp } from '../inscriptions/inscription';

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
 * CAI-08 : « Envoyer un rappel » pour un élève orange ou rouge. Le rappel s'ouvre avec son texte et un bouton WhatsApp
 * par destinataire ; un message ouvert est noté, et « rappel envoyé le 12/10 » s'affiche pour ne pas l'envoyer deux fois.
 */
@Component({
  selector: 'sh-bouton-de-rappel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rappel">
      @if (rappel(); as rappel) {
        <p class="texte">{{ rappel.texte }}</p>
        @for (message of rappel.messages; track message.lien) {
          <div class="destinataire">
            <span>{{ pourQui(message) }}</span>
            <a class="btn" [href]="message.lien" target="_blank" rel="noopener" (click)="noter()">Ouvrir WhatsApp</a>
          </div>
        } @empty {
          <p class="aide">Aucun téléphone pour envoyer le rappel.</p>
        }
      } @else {
        <button type="button" class="btn" [disabled]="envoi()" (click)="ouvrir()">Envoyer un rappel</button>
      }
      @if (dernier(); as dernier) {
        <small class="dernier">{{ envoyeLe(dernier) }}</small>
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </div>
  `,
  styles: `
    .rappel { display: grid; gap: 6px; justify-items: start; }
    .texte { margin: 0; padding: 8px 10px; border-radius: 10px; background: var(--sunk); white-space: pre-line; font-size: 13px; }
    .destinataire { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .dernier { color: var(--ink-2); font-size: 12.5px; }
    .aide { margin: 0; color: var(--ink-2); }
    .note { margin: 0; }
  `,
})
export class BoutonDeRappel {
  private readonly http = inject(HttpClient);

  readonly eleveId = input.required<string>();
  /** La date du dernier rappel, déjà connue de la page. */
  readonly dernierRappelLe = input<string | null | undefined>(null);

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
