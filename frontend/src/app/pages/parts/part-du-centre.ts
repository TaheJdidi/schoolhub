import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { lireUnNombre } from '../../socle/montants';

/** PRT-04 : la part du centre d'un professeur (vide : celle du centre). */
export interface PartDuCentreDUnProfesseur {
  pourcentage: number | null;
  parDefaut: number;
  changeeLe: string | null;
}

/** « 5 % », « 7,5 % ». */
export const ecrirePourcent = (valeur: number) => `${String(valeur).replace('.', ',')} %`;

/**
 * PRT-04 : sur la fiche d'un professeur, pour le propriétaire seulement : « Part du centre : 5 % (celle du centre) » et
 * « Changer ». La nouvelle part vaut pour les périodes qui commencent après aujourd'hui.
 */
@Component({
  selector: 'sh-part-du-centre',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (part(); as part) {
      @if (enModification()) {
        <div class="changer">
          <label class="f">
            Part du centre (%)
            <input #saisie inputmode="decimal" autocomplete="off" [value]="part.pourcentage ?? ''" [placeholder]="ecrire(part.parDefaut)" />
          </label>
          <div class="actions">
            <button type="button" class="btn" [disabled]="envoi()" (click)="enModification.set(false)">Annuler</button>
            <button type="button" class="btn primary" [disabled]="envoi()" (click)="enregistrer(saisie.value)">Enregistrer</button>
          </div>
          <p class="aide">Vide : celle du centre ({{ ecrire(part.parDefaut) }}). Elle vaut pour les périodes qui commencent après aujourd'hui.</p>
        </div>
      } @else {
        <p class="part">
          Part du centre : <b>{{ ecrire(part.pourcentage ?? part.parDefaut) }}</b>
          {{ part.pourcentage === null ? '(celle du centre)' : '(propre à ce professeur)' }}
          <button type="button" class="lien" (click)="enModification.set(true)">Changer</button>
        </p>
      }
    }
    @if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styles: `
    :host { display: grid; gap: 8px; }
    .part { margin: 0; font-size: 13.5px; }
    .lien { background: none; border: 0; padding: 0; margin-left: 6px; color: var(--ink-2); text-decoration: underline; cursor: pointer; }
    .changer { display: grid; gap: 8px; max-width: 320px; }
    .actions { display: flex; gap: 8px; }
    .aide { margin: 0; color: var(--ink-2); font-size: 12.5px; }
  `,
})
export class PartDuCentre {
  private readonly http = inject(HttpClient);

  readonly professeurId = input.required<string>();

  protected readonly ecrire = ecrirePourcent;
  protected readonly part = signal<PartDuCentreDUnProfesseur | null>(null);
  protected readonly enModification = signal(false);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.professeurId();
      this.part.set(null);
      this.enModification.set(false);
      void this.charger(id);
    });
  }

  protected async enregistrer(saisie: string): Promise<void> {
    const valeur = lireUnNombre(saisie);
    if (valeur === undefined) {
      this.erreur.set('La part du centre va de 0 à 100 %, au centième près');
      return;
    }
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      this.part.set(
        await firstValueFrom(this.http.put<PartDuCentreDUnProfesseur>(`/api/professeurs/${this.professeurId()}/part-du-centre`, { pourcentage: valeur })),
      );
      this.enModification.set(false);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  private async charger(id: string): Promise<void> {
    try {
      const part = await firstValueFrom(this.http.get<PartDuCentreDUnProfesseur>(`/api/professeurs/${id}/part-du-centre`));
      if (id === this.professeurId()) this.part.set(part);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }
}
