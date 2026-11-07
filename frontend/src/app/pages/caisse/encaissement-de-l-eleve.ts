import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { Encaissable, RecuDecrit, SommeAEncaisser } from './recu';

/** La clé d'une somme : son inscription et sa clé. */
const cleDe = (somme: SommeAEncaisser) => `${somme.inscriptionId ?? 'assurance'}|${somme.cle}`;

/**
 * CAI-03 sur la fiche de l'élève : « À payer », coché d'office, puis « Payer d'avance » (Mois et tranches à venir), à
 * cocher. « Encaisser » crée un seul reçu en espèces et l'ouvre pour le remettre. `actualisation` change quand la fiche
 * a changé ce que l'élève doit (prix spécial, arrêt…) : la carte se relit.
 */
@Component({
  selector: 'sh-encaissement-de-l-eleve',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="entete">
      <h2 class="titre">À payer</h2>
      @if (sommes().length > 0) {
        <small>Cochez ce que l'élève paie maintenant</small>
      }
    </div>
    @if (encaissable(); as encaissable) {
      @for (somme of dues(); track cle(somme)) {
        <label class="somme">
          <input type="checkbox" [checked]="cochees().has(cle(somme))" (change)="basculer(somme)" />
          <span class="deux">
            <b>{{ somme.libelle }}</b>
            <small>{{ detail(somme) }}</small>
          </span>
          <b class="num montant">{{ montant(somme.montant) }} TND</b>
        </label>
      } @empty {
        <p class="vide">Rien à payer aujourd'hui.</p>
      }
      @if (aVenir().length > 0) {
        <p class="bande">Payer d'avance</p>
        @for (somme of aVenirMontrees(); track cle(somme)) {
          <label class="somme">
            <input type="checkbox" [checked]="cochees().has(cle(somme))" (change)="basculer(somme)" />
            <span class="deux">
              <b>{{ somme.libelle }} <span class="avance">· d'avance</span></b>
              <small>{{ detail(somme) }}</small>
            </span>
            <b class="num montant">{{ montant(somme.montant) }} TND</b>
          </label>
        }
        @if (aVenirMontrees().length < aVenir().length) {
          <button type="button" class="btn ghost plus" (click)="toutVoir.set(true)">
            Voir les {{ aVenir().length - aVenirMontrees().length }} autres sommes à venir
          </button>
        }
      }
      @if (sommes().length > 0) {
        <div class="pied">
          <span><b class="num">{{ montant(total()) }} TND</b> en espèces</span>
          <button type="button" class="btn primary" [disabled]="envoi() || cochees().size === 0" (click)="encaisser(encaissable)">
            Encaisser {{ montant(total()) }} TND
          </button>
        </div>
        @if (!encaissable.aUnEmail) {
          <p class="aide">Aucun email n'est connu pour cet élève : le reçu se remettra imprimé ou par WhatsApp.</p>
        }
      }
    }
    @if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styles: `
    :host { display: grid; }
    .entete { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; padding: 14px 18px; border-bottom: 1px solid var(--line); }
    .entete small { color: var(--ink-2); }
    .titre { font-family: var(--font-display); font-size: 17px; font-weight: 600; margin: 0; }
    .somme { display: flex; align-items: center; gap: 12px; padding: 12px 18px; border-top: 1px solid var(--line); cursor: pointer; min-width: 0; }
    .entete + .somme, .bande + .somme { border-top: 0; }
    .somme input { width: 18px; height: 18px; flex: none; }
    .deux { display: grid; flex: 1; min-width: 0; }
    .deux b { font-weight: 600; overflow-wrap: anywhere; }
    .deux small { color: var(--ink-2); font-size: 13px; overflow-wrap: anywhere; }
    .avance { color: var(--ink-3); font-weight: 400; }
    .montant { white-space: nowrap; font-weight: 500; }
    .bande { margin: 0; padding: 8px 18px; background: var(--sunk); border-top: 1px solid var(--line); border-bottom: 1px solid var(--line); color: var(--ink-2); font-size: 12px; font-weight: 600; letter-spacing: 0.04em; text-transform: uppercase; }
    .plus { justify-self: start; margin: 4px 12px 8px; }
    .pied { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px; padding: 14px 18px; border-top: 1px solid var(--line); }
    .vide, .aide { margin: 0; padding: 12px 18px; color: var(--ink-2); }
    .aide { padding-top: 0; font-size: 13px; }
    .note { margin: 0 18px 14px; }
  `,
})
export class EncaissementDeLEleve {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly eleveId = input.required<string>();
  /** Change quand ce que l'élève doit a changé ailleurs sur la fiche. */
  readonly actualisation = input(0);

  protected readonly cle = cleDe;
  protected readonly montant = ecrireUnMontant;
  protected readonly encaissable = signal<Encaissable | null>(null);
  protected readonly cochees = signal<ReadonlySet<string>>(new Set());
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);

  protected readonly sommes = computed(() => this.encaissable()?.sommes ?? []);
  protected readonly dues = computed(() => this.sommes().filter((s) => s.due));
  protected readonly aVenir = computed(() => this.sommes().filter((s) => !s.due));
  /** « Payer d'avance » montre la prochaine somme de chaque inscription ; « Voir les autres » montre tout. */
  protected readonly toutVoir = signal(false);
  protected readonly aVenirMontrees = computed(() => {
    if (this.toutVoir()) return this.aVenir();
    const vues = new Set<string | null>();
    return this.aVenir().filter((s) => this.cochees().has(cleDe(s)) || (!vues.has(s.inscriptionId) && vues.add(s.inscriptionId)));
  });
  protected readonly total = computed(() => this.sommes().filter((s) => this.cochees().has(cleDe(s))).reduce((total, s) => total + s.montant, 0));

  private dernierChargement = 0;

  constructor() {
    effect(() => {
      const id = this.eleveId();
      this.actualisation();
      untracked(() => void this.charger(id));
    });
  }

  /** Ce qui est dû, coché d'office ; ce qui est à venir, à cocher. Seule la dernière réponse compte. */
  private async charger(id: string): Promise<void> {
    const numero = ++this.dernierChargement;
    this.erreur.set(null);
    try {
      const encaissable = await firstValueFrom(this.http.get<Encaissable>(`/api/eleves/${id}/encaissable`));
      if (numero !== this.dernierChargement) return;
      this.encaissable.set(encaissable);
      this.cochees.set(new Set(encaissable.sommes.filter((s) => s.due).map(cleDe)));
    } catch (erreur) {
      if (numero === this.dernierChargement) this.erreur.set(messageDErreur(erreur));
    }
  }

  protected basculer(somme: SommeAEncaisser): void {
    const cle = cleDe(somme);
    this.cochees.update((cochees) => {
      const suivantes = new Set(cochees);
      if (suivantes.has(cle)) suivantes.delete(cle);
      else suivantes.add(cle);
      return suivantes;
    });
  }

  protected async encaisser(encaissable: Encaissable): Promise<void> {
    if (this.envoi()) return;
    const sommes = encaissable.sommes.filter((s) => this.cochees().has(cleDe(s))).map((s) => ({ inscriptionId: s.inscriptionId, cle: s.cle }));
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      const recu = await firstValueFrom(this.http.post<RecuDecrit>(`/api/eleves/${encaissable.eleve.id}/encaissement`, { sommes }));
      void this.router.navigate(['/caisse/recus', recu.id]);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
      this.envoi.set(false);
    }
  }

  /** « Anglais, Groupe Samedi 12h → 14h · à payer le sam. 21/11 », ou « Une fois par an » pour l'assurance. */
  protected detail(somme: SommeAEncaisser): string {
    const quand = somme.dueLe ? `${somme.due ? 'dû le' : 'à payer le'} ${formatDate(somme.dueLe, 'EEE dd/MM', FRANCAIS)}` : '';
    if (!somme.inscriptionId) return quand ? `Une fois par an · ${quand}` : 'Une fois par an';
    return [somme.groupe, quand].filter(Boolean).join(' · ');
  }
}
