import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';

/** L'adresse du QR code du formulaire public, en PNG (DEM-02). */
export const QR_CODE = '/api/demandes/qr-code';

/**
 * DEM-02 : la page « Demandes d'inscription ». Pour l'instant, le lien public du centre : le copier, voir son QR code,
 * et imprimer l'affiche. La liste des demandes viendra avec DEM-03.
 */
@Component({
  selector: 'sh-demandes',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Demandes d'inscription</h1>
        <p class="sub">Partagez le lien ou affichez le QR code : les demandes du formulaire public arriveront ici.</p>
      </div>
    </div>

    <section class="card carte">
      <h2 class="titre">Le lien public du centre</h2>
      @if (lien(); as lien) {
        <a class="lien" [href]="lien" target="_blank" rel="noopener">{{ lien }}</a>
        <img class="qr-code" [src]="qrCode" alt="QR code du formulaire public du centre" width="220" height="220" />
        <div class="actions">
          <button type="button" class="btn" (click)="copier(lien)">Copier le lien</button>
          <a class="btn primary" routerLink="/demandes/affiche">Imprimer l'affiche</a>
        </div>
      }
      @if (copie()) {
        <p class="note ok" role="status">Lien copié</p>
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </section>
  `,
  styles: `
    .carte { display: grid; gap: 14px; padding: 16px; justify-items: start; }
    .titre { font-family: var(--font-display); font-size: 17px; font-weight: 600; margin: 0; }
    .lien { color: var(--accent); overflow-wrap: anywhere; }
    .qr-code { width: 220px; max-width: 100%; height: auto; image-rendering: pixelated; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .note { margin: 0; }
  `,
})
export class Demandes implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly qrCode = QR_CODE;
  protected readonly lien = signal<string | null>(null);
  protected readonly copie = signal(false);
  protected readonly erreur = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.lien.set((await firstValueFrom(this.http.get<{ lien: string }>('/api/demandes/lien'))).lien);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected async copier(lien: string): Promise<void> {
    this.copie.set(false);
    this.erreur.set(null);
    try {
      await navigator.clipboard.writeText(lien);
      this.copie.set(true);
    } catch {
      this.erreur.set("Le lien n'a pas pu être copié : sélectionnez-le à la main.");
    }
  }
}
