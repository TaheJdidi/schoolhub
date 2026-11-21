import { formatDate } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from './erreurs';
import { FRANCAIS } from './francais';
import { Session } from './session/session';
import { Icone } from './icone';

/** Les filtres d'un export : seuls ceux qui ont une valeur partent. */
export type FiltresDExport = Record<string, string | number | boolean | null | undefined>;

/** Télécharge le fichier que le serveur prépare, sous le nom qu'il donne (« eleves-2027-11-27.xlsx »). */
export async function telechargerUnFichier(http: HttpClient, adresse: string, filtres: FiltresDExport = {}): Promise<void> {
  let params = new HttpParams();
  for (const [cle, valeur] of Object.entries(filtres)) {
    if (valeur !== null && valeur !== undefined && valeur !== '') params = params.set(cle, String(valeur));
  }
  const reponse = await firstValueFrom(http.get(adresse, { params, responseType: 'blob', observe: 'response' }));
  const nom = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(reponse.headers.get('Content-Disposition') ?? '')?.[1] ?? 'liste.xlsx';
  const lien = document.createElement('a');
  lien.href = URL.createObjectURL(reponse.body!);
  lien.download = decodeURIComponent(nom);
  lien.click();
  URL.revokeObjectURL(lien.href);
}

/**
 * LST-01 et LST-02 : « Imprimer » et « Exporter (Excel) » d'une liste, avec le filtre en cours. À l'impression, une page
 * A4 porte le nom du centre, le nom de la liste, la date et le nombre de lignes ; les boutons, les menus et les champs
 * ne s'impriment pas. L'export télécharge le fichier que le serveur prépare (`/api/exports/…`). Une liste vide n'a rien
 * à imprimer ni à exporter : les deux boutons sont grisés.
 */
@Component({
  selector: 'sh-actions-de-liste',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icone],
  template: `
    <div class="actions-de-liste sans-impression">
      @if (compact()) {
        <button type="button" class="icone-seule" aria-label="Imprimer" [attr.title]="nombre() === 0 ? 'La liste est vide' : 'Imprimer'"
                [disabled]="nombre() === 0" (click)="imprimer()"><sh-icone nom="printer" /></button>
        @if (exporter()) {
          <button type="button" class="icone-seule" aria-label="Exporter (Excel)" [attr.title]="nombre() === 0 ? 'La liste est vide' : 'Exporter (Excel)'"
                  [disabled]="envoi() || nombre() === 0" (click)="telecharger()"><sh-icone nom="file-spreadsheet" /></button>
        }
      } @else {
        <button type="button" class="btn sm" [disabled]="nombre() === 0" [attr.title]="nombre() === 0 ? 'La liste est vide' : null" (click)="imprimer()">
          Imprimer
        </button>
        @if (exporter()) {
          <button type="button" class="btn sm" [disabled]="envoi() || nombre() === 0" [attr.title]="nombre() === 0 ? 'La liste est vide' : null" (click)="telecharger()">
            Exporter (Excel)
          </button>
        }
      }
    </div>
    @if (erreur(); as erreur) {
      <p class="note bad sans-impression" role="alert">{{ erreur }}</p>
    }
    <div class="entete-d-impression">
      <b>{{ session.personne()?.centre }}</b>
      <span>{{ titre() }}</span>
      <span>{{ aujourdhui }} · {{ nombre() }} ligne{{ nombre() > 1 ? 's' : '' }}</span>
    </div>
  `,
  styles: `
    :host { display: contents; }
    .actions-de-liste { display: inline-flex; gap: 6px; flex-wrap: wrap; }
    .icone-seule { display: inline-flex; align-items: center; justify-content: center; width: 36px; height: 36px; padding: 0;
      border: 1px solid var(--line-2); border-radius: 10px; background: var(--surface); color: var(--ink-2); cursor: pointer; }
    .icone-seule:hover:not(:disabled) { background: var(--sunk); color: var(--ink); }
    .icone-seule:disabled { opacity: 0.5; cursor: not-allowed; }
    .icone-seule svg { width: 17px; height: 17px; }
    .entete-d-impression { justify-content: space-between; gap: 12px; padding-bottom: 6mm; border-bottom: 1px solid #999; margin-bottom: 4mm; font-size: 12pt; }
  `,
})
export class ActionsDeListe {
  private readonly http = inject(HttpClient);

  protected readonly session = inject(Session);

  /** Le nom de la liste, imprimé en tête : « Élèves de 9ᵉ année de base ». */
  readonly titre = input.required<string>();
  readonly nombre = input(0);
  /** L'adresse de l'export (`/api/exports/eleves`) ; sans elle, seulement « Imprimer ». */
  readonly exporter = input<string | null>(null);
  readonly filtres = input<FiltresDExport>({});
  /** Deux icônes, sans texte (l'en-tête de l'appel) ; le nom s'affiche au survol. */
  readonly compact = input(false);

  protected readonly aujourdhui = formatDate(new Date(), 'dd/MM/yyyy', FRANCAIS);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);

  protected imprimer(): void {
    window.print();
  }

  protected async telecharger(): Promise<void> {
    const adresse = this.exporter();
    if (!adresse) return;
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      await telechargerUnFichier(this.http, adresse, this.filtres());
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
