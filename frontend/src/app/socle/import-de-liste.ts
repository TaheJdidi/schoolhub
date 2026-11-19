import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { telechargerUnFichier } from './actions-de-liste';
import { messageDErreur } from './erreurs';
import { Icone } from './icone';

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
  imports: [Icone],
  template: `
    <section class="card import" [attr.aria-label]="'Importer des ' + quoi()">
      <div class="entete">
        <span class="pictogramme"><sh-icone nom="user-plus" /></span>
        <div class="textes">
          <h2 class="titre">Importer des {{ quoi() }}</h2>
          <p class="aide">Ajoutez-en plusieurs d'un coup depuis le fichier modèle, une ligne par {{ unite() }}.</p>
        </div>
        <button type="button" class="fermer" aria-label="Fermer" (click)="ferme.emit()"><sh-icone nom="x" /></button>
      </div>

      <!-- On glisse le fichier dans la zone, ou on clique dessus pour le choisir. -->
      <label
        class="depot"
        [class.survol]="survol()"
        [class.choisi]="fichierChoisi()"
        (dragover)="$event.preventDefault(); survol.set(true)"
        (dragleave)="survol.set(false)"
        (drop)="deposer($event)"
      >
        <input #fichier type="file" class="cache" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" aria-label="Fichier rempli" (change)="choisir(fichier)" />
        <span class="rond"><sh-icone nom="file-spreadsheet" /></span>
        @if (fichierChoisi(); as choisi) {
          <b>{{ choisi.name }}</b>
          <small>Prêt à importer · cliquez pour changer de fichier</small>
        } @else {
          <b>Glissez votre fichier ici</b>
          <small>ou cliquez pour le choisir · .xlsx</small>
        }
      </label>
      <p class="regle">Rien n'est importé tant qu'une ligne a une erreur : vous verrez les lignes à corriger.</p>

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

      <div class="pied">
        <button type="button" class="lien-modele" [disabled]="envoi()" (click)="telechargerLeModele()"><sh-icone nom="download" />Modèle Excel</button>
        <button type="button" class="btn" (click)="ferme.emit()">Annuler</button>
        <button type="button" class="btn primary" [disabled]="envoi()" (click)="importer(fichier)"><sh-icone nom="upload" />Importer</button>
      </div>
    </section>
  `,
  styles: `
    .import { display: grid; gap: 12px; padding: 0; overflow: hidden; }
    .entete { display: flex; align-items: center; gap: 14px; padding: 16px 18px 0; }
    .pictogramme { width: 44px; height: 44px; border-radius: 12px; display: grid; place-items: center; flex: none; background: var(--accent-soft); color: var(--accent); }
    .pictogramme svg.lucide, .rond svg.lucide { width: 22px; height: 22px; }
    .textes { flex: 1; min-width: 0; }
    .titre { margin: 0; }
    .aide { margin: 2px 0 0; color: var(--ink-2); font-size: 13px; }
    .fermer { width: 32px; height: 32px; display: grid; place-items: center; flex: none; border: 0; border-radius: 8px; background: none; color: var(--ink-2); cursor: pointer; }
    .fermer:hover { background: var(--sunk); color: var(--ink); }
    .depot { margin: 0 18px; display: grid; justify-items: center; gap: 4px; padding: 22px 16px; text-align: center; cursor: pointer;
      border: 1.5px dashed var(--accent); border-radius: 14px; background: var(--accent-soft); color: var(--accent-ink); transition: background .15s, border-color .15s; }
    .depot:hover, .depot.survol { border-style: solid; filter: brightness(0.98); }
    .depot:focus-within { outline: 2px solid var(--accent); outline-offset: 2px; }
    .depot.choisi { border-style: solid; border-color: var(--ok); background: var(--ok-soft); color: var(--ok); }
    .depot.choisi small, .depot.choisi .rond { color: var(--ok); }
    .depot b { font-weight: 600; font-size: 14px; word-break: break-all; }
    .depot small { color: var(--accent); font-size: 12.5px; }
    .rond { width: 42px; height: 42px; border-radius: 50%; display: grid; place-items: center; margin-bottom: 4px; background: var(--surface); color: var(--accent); }
    .cache { position: absolute; width: 1px; height: 1px; opacity: 0; pointer-events: none; }
    .regle { margin: 0 18px; color: var(--ink-3); font-size: 12.5px; }
    .import > .note { margin: 0 18px; }
    .pied { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; padding: 12px 18px; border-top: 1px solid var(--line); background: var(--sunk); }
    .lien-modele { display: inline-flex; align-items: center; gap: 6px; margin-right: auto; padding: 4px 0; border: 0; background: none; color: var(--accent); font: inherit; font-weight: 500; cursor: pointer; }
    .lien-modele:hover { text-decoration: underline; }
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
  /** Le fichier choisi en cliquant ou glissé dans la zone ; son nom s'affiche à la place de l'invitation. */
  protected readonly fichierChoisi = signal<File | null>(null);
  protected readonly survol = signal(false);

  protected choisir(champ: HTMLInputElement): void {
    this.fichierChoisi.set(champ.files?.[0] ?? null);
    this.erreur.set(null);
  }

  protected deposer(evenement: DragEvent): void {
    evenement.preventDefault();
    this.survol.set(false);
    const fichier = evenement.dataTransfer?.files?.[0];
    if (fichier) {
      this.fichierChoisi.set(fichier);
      this.erreur.set(null);
    }
  }

  protected async telechargerLeModele(): Promise<void> {
    this.erreur.set(null);
    try {
      await telechargerUnFichier(this.http, `${this.adresse()}/modele`);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected async importer(champ: HTMLInputElement): Promise<void> {
    const fichier = this.fichierChoisi() ?? champ.files?.[0];
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
