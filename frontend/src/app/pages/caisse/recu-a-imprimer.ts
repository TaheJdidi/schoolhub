import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { RecuDecrit, totalPaye } from './recu';

/**
 * CAI-05 : le reçu à imprimer, une page A5 sans le menu : le centre et son téléphone, le numéro, la date, l'élève, une
 * ligne par somme (groupe et ce qui est payé), le total, la secrétaire. « ANNULÉ » s'il l'est. L'impression du
 * navigateur en fait aussi un PDF. « Imprimer » ne s'imprime pas. CAI-04 : avec `?ids=a,b,c`, plusieurs reçus, un
 * par page A5 : « Imprimer tous les reçus » donne un seul fichier.
 */
@Component({
  selector: 'sh-recu-a-imprimer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="feuilles">
      <button type="button" class="btn primary imprimer" (click)="imprimer()">Imprimer</button>
      @for (recu of recus(); track recu.id) {
        <article class="recu">
          @if (recu.annule) {
            <p class="annule">ANNULÉ · {{ recu.motifDAnnulation }}</p>
          }
          <header class="entete">
            <div>
              <h1 class="centre">{{ recu.centre.nom }}</h1>
              @if (recu.centre.telephone) {
                <p>{{ recu.centre.telephone }}</p>
              }
            </div>
            <div class="numero">
              <b>Reçu n° {{ recu.numero }}</b>
              <p>Le {{ date(recu.jour) }}</p>
            </div>
          </header>
          <p class="eleve">Élève : <b>{{ recu.eleve.prenom }} {{ recu.eleve.nom }}</b></p>
          <table>
            <thead>
              <tr><th>Groupe</th><th>Payé</th><th class="montant">Montant</th></tr>
            </thead>
            <tbody>
              @for (ligne of recu.lignes; track $index) {
                <tr>
                  <td>{{ ligne.groupe }}</td>
                  <td>{{ ligne.libelle }}</td>
                  <td class="montant">{{ montant(ligne.montant) }} TND</td>
                </tr>
              }
            </tbody>
            <tfoot>
              <tr><td colspan="2">{{ totalPaye(recu.mode) }}</td><td class="montant">{{ montant(recu.total) }} TND</td></tr>
            </tfoot>
          </table>
          <p class="secretaire">Reçu par {{ recu.secretaire }}</p>
        </article>
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </main>
  `,
  styles: `
    :host { display: block; background: #fff; color: #111; min-height: 100vh; }
    .feuilles { max-width: 128mm; margin: 0 auto; padding: 10mm; display: grid; gap: 10mm; }
    .recu { display: grid; gap: 5mm; position: relative; }
    .recu + .recu { border-top: 1px dashed #999; padding-top: 10mm; }
    .imprimer { justify-self: end; }
    .entete { display: flex; justify-content: space-between; gap: 6mm; align-items: flex-start; }
    .entete p { margin: 2px 0 0; font-size: 13px; }
    .centre { font-family: var(--font-display); font-size: 19px; margin: 0; }
    .numero { text-align: right; }
    .eleve, .secretaire { margin: 0; font-size: 14px; }
    table { width: 100%; border-collapse: collapse; font-size: 13px; }
    th, td { border-bottom: 1px solid #999; padding: 5px 4px; text-align: left; vertical-align: top; }
    tfoot td { font-weight: 700; border-bottom: 0; border-top: 2px solid #111; }
    .montant { text-align: right; white-space: nowrap; }
    .annule { margin: 0; padding: 3mm; border: 2px solid #b00; color: #b00; font-weight: 700; text-align: center; letter-spacing: 0.08em; }
    @page { size: A5; margin: 8mm; }
    @media print {
      .imprimer { display: none; }
      .feuilles { padding: 0; gap: 0; }
      .recu + .recu { border-top: 0; padding-top: 0; }
      .recu { break-after: page; }
      .recu:last-of-type { break-after: auto; }
    }
  `,
})
export class RecuAImprimer implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute).snapshot;

  protected readonly montant = ecrireUnMontant;
  protected readonly totalPaye = totalPaye;
  protected readonly recus = signal<RecuDecrit[]>([]);
  protected readonly erreur = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      // Un reçu (`/recus/{id}/impression`), ou plusieurs (`/recus/impression?ids=a,b`), dans cet ordre.
      const id = this.route.paramMap.get('id');
      const ids = id ? [id] : (this.route.queryParamMap.get('ids') ?? '').split(',').filter((i) => i.length > 0);
      this.recus.set(await Promise.all(ids.map((i) => firstValueFrom(this.http.get<RecuDecrit>(`/api/recus/${i}`)))));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** « 27/11/2027 ». */
  protected date(texte: string): string {
    return formatDate(texte, 'dd/MM/yyyy', FRANCAIS);
  }

  protected imprimer(): void {
    window.print();
  }
}
