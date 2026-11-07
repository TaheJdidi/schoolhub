import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { telechargerUnFichier } from './actions-de-liste';
import { messageDErreur } from './erreurs';

/** LST-03 et LST-04 : la réponse d'un import. */
export interface ResultatDImport {
  importe: boolean;
  importes: number;
  inscrits: number;
  erreurs: string[];
  doublons: string[];
}

/**
 * LST-03 et LST-04 : importer une liste depuis le fichier modèle. « Télécharger le modèle », puis le fichier rempli :
 * la plateforme vérifie chaque ligne ; s'il reste une erreur, rien n'est importé et la page montre les lignes à corriger.
 * Les doublons possibles sont signalés. Réussi, l'import prévient le parent, qui recharge sa liste.
 */
@Component({
  selector: 'sh-import-de-liste',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card carte import" [attr.aria-label]="'Importer des ' + quoi()">
      <h2 class="titre">Importer des {{ quoi() }}</h2>
      <p class="aide">
        Remplissez le fichier modèle, une ligne par {{ unite() }}, puis envoyez-le. Rien n'est importé tant qu'une ligne a une erreur.
      </p>
      <div class="etapes">
        <button type="button" class="btn" [disabled]="envoi()" (click)="telechargerLeModele()">Télécharger le modèle</button>
        <input #fichier type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" aria-label="Fichier rempli" />
        <button type="button" class="btn primary" [disabled]="envoi()" (click)="importer(fichier)">Importer</button>
        <button type="button" class="btn" (click)="ferme.emit()">Fermer</button>
      </div>
      @if (resultat(); as resultat) {
        @if (resultat.importe) {
          <p class="note ok" role="status">{{ bilan(resultat) }}</p>
        } @else {
          <div class="note bad" role="alert">
            <span>
              <b>Rien n'est importé. Corrigez votre fichier et envoyez-le de nouveau :</b>
              <ul>
                @for (erreur of resultat.erreurs; track $index) {
                  <li>{{ erreur }}</li>
                }
              </ul>
            </span>
          </div>
        }
        @if (resultat.doublons.length > 0) {
          <div class="note warn">
            <span>
              <b>Doublons possibles :</b>
              <ul>
                @for (doublon of resultat.doublons; track $index) {
                  <li>{{ doublon }}</li>
                }
              </ul>
            </span>
          </div>
        }
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </section>
  `,
  styles: `
    .import { display: grid; gap: 10px; }
    .titre { margin: 0; }
    .aide { margin: 0; color: var(--ink-2); font-size: 13px; }
    .etapes { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    ul { margin: 6px 0 0; padding-left: 18px; }
  `,
})
export class ImportDeListe {
  private readonly http = inject(HttpClient);

  /** « élèves », « professeurs ». */
  readonly quoi = input.required<string>();
  /** « élève », « professeur ». */
  readonly unite = input.required<string>();
  /** `/api/imports/eleves` : le modèle est à `…/modele`. */
  readonly adresse = input.required<string>();
  readonly importe = output<ResultatDImport>();
  readonly ferme = output<void>();

  protected readonly resultat = signal<ResultatDImport | null>(null);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);

  protected async telechargerLeModele(): Promise<void> {
    this.erreur.set(null);
    try {
      await telechargerUnFichier(this.http, `${this.adresse()}/modele`);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected async importer(champ: HTMLInputElement): Promise<void> {
    const fichier = champ.files?.[0];
    if (!fichier) {
      this.erreur.set('Choisissez le fichier rempli');
      return;
    }
    const donnees = new FormData();
    donnees.append('fichier', fichier);
    this.envoi.set(true);
    this.erreur.set(null);
    this.resultat.set(null);
    try {
      const resultat = await firstValueFrom(this.http.post<ResultatDImport>(this.adresse(), donnees));
      this.resultat.set(resultat);
      this.importe.emit(resultat);
    } catch (erreur) {
      // Un import refusé répond 400 avec ses lignes à corriger.
      if (erreur instanceof HttpErrorResponse && erreur.status === 400 && erreur.error?.erreurs) this.resultat.set(erreur.error as ResultatDImport);
      else this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** « 120 élèves importés, dont 95 inscrits dans un groupe ». */
  protected bilan(resultat: ResultatDImport): string {
    const pluriel = resultat.importes > 1;
    const base = `${resultat.importes} ${pluriel ? this.quoi() : this.unite()} importé${pluriel ? 's' : ''}`;
    return resultat.inscrits > 0 ? `${base}, dont ${resultat.inscrits} inscrit${resultat.inscrits > 1 ? 's' : ''} dans un groupe.` : `${base}.`;
  }
}
