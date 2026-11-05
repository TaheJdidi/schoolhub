import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { Session } from '../../socle/session/session';
import { QR_CODE } from './demandes';

/**
 * DEM-02 : l'affiche du formulaire public, une page A4 sans le menu, à afficher au guichet : le nom du centre, le QR
 * code et le lien. « Imprimer » ne s'imprime pas.
 */
@Component({
  selector: 'sh-affiche',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="affiche">
      <h1 class="centre">{{ centre() }}</h1>
      <p class="titre">Demande d'inscription</p>
      <p class="consigne">Scannez le code ou ouvrez le lien, sans créer de compte.</p>
      <img class="qr-code" [src]="qrCode" alt="QR code du formulaire public du centre" />
      @if (lien(); as lien) {
        <p class="lien">{{ lien }}</p>
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
      <button type="button" class="btn primary imprimer" (click)="imprimer()">Imprimer</button>
    </main>
  `,
  styles: `
    :host { display: block; background: #fff; color: #111; min-height: 100vh; }
    .affiche { max-width: 180mm; margin: 0 auto; padding: 24mm 12mm; display: grid; justify-items: center; gap: 10mm; text-align: center; }
    .centre { font-family: var(--font-display); font-size: 34px; margin: 0; overflow-wrap: anywhere; }
    .titre { font-size: 26px; font-weight: 600; margin: 0; }
    .consigne { font-size: 18px; margin: 0; }
    .qr-code { width: 110mm; max-width: 100%; height: auto; image-rendering: pixelated; }
    .lien { font-size: 18px; margin: 0; overflow-wrap: anywhere; }
    @page { size: A4; margin: 0; }
    @media print { .imprimer { display: none; } }
  `,
})
export class Affiche implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly session = inject(Session);

  protected readonly qrCode = QR_CODE;
  protected readonly centre = computed(() => this.session.personne()?.centre ?? '');
  protected readonly lien = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.lien.set((await firstValueFrom(this.http.get<{ lien: string }>('/api/demandes/lien'))).lien);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected imprimer(): void {
    window.print();
  }
}
