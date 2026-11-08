import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';

/** TDB-04 : GET /api/journal. */
export interface JournalFiltre {
  lignes: { quand: string; auteur: string; action: string; detail: string; avant: string | null; apres: string | null }[];
  auteurs: { id: string; nom: string }[];
  actions: string[];
}

/** Le nom de chaque action, en français. */
export const ACTIONS: Record<string, string> = {
  CompteCree: 'Compte créé',
  CompteDesactive: 'Compte désactivé',
  CompteReactive: 'Compte réactivé',
  GroupeFerme: 'Groupe fermé',
  PrixSpecialAccorde: 'Prix spécial accordé',
  PrixSpecialRetire: 'Prix spécial retiré',
  AssuranceDispensee: 'Assurance dispensée',
  PayeAvantSchoolHub: 'Payé avant SchoolHub',
  InscriptionArretee: 'Inscription arrêtée',
  ChangementDeGroupe: 'Changement de groupe',
  PresenceCorrigee: 'Présence corrigée',
  SeanceAnnulee: 'Séance annulée',
  SeanceDeplacee: 'Séance déplacée',
  Encaissement: 'Paiement',
  RecuAnnule: 'Reçu annulé',
  CaisseCloturee: 'Caisse clôturée',
  SortieDeCaisse: 'Sortie de caisse',
};

/**
 * TDB-04 : le journal, pour le propriétaire : qui a fait quoi, et quand, les plus récentes en premier, avec la valeur
 * d'avant et celle d'après. Filtré par personne, par action et par période. Personne ne peut modifier ni effacer une ligne.
 */
@Component({
  selector: 'sh-journal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Journal</h1>
        <p class="sub">Qui a fait quoi, et quand, sur l'argent et la présence. Aucune ligne ne se modifie ni ne s'efface.</p>
      </div>
    </div>
    <section class="card carte">
      <div class="filtres">
        <label class="f">
          Personne
          <select class="inp" #auteur (change)="filtrer(auteur.value, action.value, du.value, au.value)">
            <option value="">Toutes</option>
            @for (a of journal()?.auteurs ?? []; track a.id) {
              <option [value]="a.id">{{ a.nom }}</option>
            }
          </select>
        </label>
        <label class="f">
          Action
          <select class="inp" #action (change)="filtrer(auteur.value, action.value, du.value, au.value)">
            <option value="">Toutes</option>
            @for (a of journal()?.actions ?? []; track a) {
              <option [value]="a">{{ nom(a) }}</option>
            }
          </select>
        </label>
        <label class="f">Du <input #du type="date" (change)="filtrer(auteur.value, action.value, du.value, au.value)" /></label>
        <label class="f">Au <input #au type="date" (change)="filtrer(auteur.value, action.value, du.value, au.value)" /></label>
      </div>
      @if (journal(); as journal) {
        @for (ligne of journal.lignes; track $index) {
          <div class="ligne">
            <span class="quand num">{{ quand(ligne.quand) }}</span>
            <span class="deux">
              <b>{{ ligne.auteur }} · {{ nom(ligne.action) }}</b>
              <small>{{ ligne.detail }}</small>
              @if (ligne.avant || ligne.apres) {
                <small class="avant-apres">{{ ligne.avant ?? '—' }} → {{ ligne.apres ?? '—' }}</small>
              }
            </span>
          </div>
        } @empty {
          <p class="vide">Aucune ligne.</p>
        }
      }
    </section>
    @if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    .filtres { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 10px; align-items: end; }
    .ligne { display: flex; gap: 12px; padding: 9px 0; border-top: 1px solid var(--line); }
    .quand { min-width: 112px; color: var(--ink-2); font-size: 12.5px; }
    .deux { display: grid; gap: 2px; min-width: 0; }
    .deux small { color: var(--ink-2); font-size: 12.5px; overflow-wrap: anywhere; }
    .avant-apres { font-style: italic; }
  `,
})
export class Journal implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly journal = signal<JournalFiltre | null>(null);
  protected readonly erreur = signal<string | null>(null);
  private filtres: Record<string, string> = {};

  async ngOnInit(): Promise<void> {
    await this.charger();
  }

  protected nom(action: string): string {
    return ACTIONS[action] ?? action;
  }

  protected async filtrer(compteId: string, action: string, du: string, au: string): Promise<void> {
    this.filtres = Object.fromEntries(Object.entries({ compteId, action, du, au }).filter(([, valeur]) => valeur !== ''));
    await this.charger();
  }

  /** « 12/10/2027 16h42 ». */
  protected quand(texte: string): string {
    return formatDate(texte, "dd/MM/yyyy HH'h'mm", FRANCAIS);
  }

  private async charger(): Promise<void> {
    try {
      this.journal.set(await firstValueFrom(this.http.get<JournalFiltre>('/api/journal', { params: this.filtres })));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }
}
