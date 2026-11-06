import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { Session } from '../../socle/session/session';
import { heure } from '../groupes/groupe';
import { FeuilleDAppel as Feuille } from './appel';

/** Les lignes vides pour les élèves en rattrapage. */
const LIGNES_DE_RATTRAPAGE = 3;

/**
 * SEA-08 : la feuille d'appel d'un groupe, une page A4 sans le menu : le centre, l'année scolaire, le groupe, sa matière,
 * son niveau et son professeur ; pour chaque élève, son nom, son prénom, une case par séance et une case de signature ;
 * des lignes vides pour les élèves en rattrapage ; la consigne. Ni téléphone, ni paiement. « Imprimer » ne s'imprime pas.
 */
@Component({
  selector: 'sh-feuille-d-appel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <main class="feuille">
      <div class="outils">
        <a class="btn" [routerLink]="['/professeurs/groupes', groupeId]">Retour au groupe</a>
        <button type="button" class="btn primary" (click)="imprimer()">Imprimer</button>
      </div>
      @if (feuille(); as feuille) {
        <header class="entete">
          <div>
            <h1 class="centre">{{ feuille.centre }}</h1>
            <p>Feuille d'appel{{ feuille.annee ? ' · année ' + feuille.annee : '' }}</p>
          </div>
          <div class="groupe">
            <b>{{ feuille.groupe }}</b>
            <p>{{ feuille.matiere }}{{ feuille.niveau ? ' · ' + feuille.niveau : '' }}</p>
            <p>Professeur : {{ feuille.professeur }}</p>
          </div>
        </header>
        <table>
          <thead>
            <tr>
              <th class="numero">N°</th>
              <th>Nom</th>
              <th>Prénom</th>
              @for (seance of feuille.seances; track $index) {
                <th class="case">{{ court(seance.date) }}<br /><small>{{ heure(seance.debut) }}</small></th>
              }
              <th class="signature">Signature</th>
            </tr>
          </thead>
          <tbody>
            @for (eleve of feuille.eleves; track $index) {
              <tr>
                <td class="numero">{{ $index + 1 }}</td>
                <td>{{ eleve.nom }}</td>
                <td>{{ eleve.prenom }}</td>
                @for (seance of feuille.seances; track $index) {
                  <td class="case"></td>
                }
                <td></td>
              </tr>
            }
            <tr class="titre-rattrapage">
              <td [attr.colspan]="4 + feuille.seances.length">Élèves en rattrapage</td>
            </tr>
            @for (ligne of lignesVides; track ligne) {
              <tr class="vide">
                <td></td>
                <td></td>
                <td></td>
                @for (seance of feuille.seances; track $index) {
                  <td class="case"></td>
                }
                <td></td>
              </tr>
            }
          </tbody>
        </table>
        <footer class="pied">
          <span>Écrivez A dans la case d'un élève absent. Les autres sont présents.</span>
          <span>Imprimée le {{ aujourdhui }}{{ personne() ? ' par ' + personne() : '' }}</span>
        </footer>
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </main>
  `,
  styles: `
    :host { display: block; background: #fff; color: #111; min-height: 100vh; }
    .feuille { max-width: 277mm; margin: 0 auto; padding: 10mm; display: grid; gap: 6mm; }
    .outils { display: flex; gap: 8px; justify-content: flex-end; }
    .entete { display: flex; justify-content: space-between; gap: 8mm; align-items: flex-start; }
    .entete p { margin: 2px 0 0; font-size: 13px; }
    .centre { font-family: var(--font-display); font-size: 20px; margin: 0; }
    .groupe { text-align: right; }
    table { width: 100%; border-collapse: collapse; font-size: 12.5px; }
    th, td { border: 1px solid #555; padding: 5px 6px; text-align: left; height: 22px; }
    th { background: #f1f1f1; font-weight: 600; }
    th small { font-weight: 400; }
    .numero { width: 26px; text-align: center; }
    .case { width: 46px; text-align: center; }
    .signature { width: 110px; }
    .titre-rattrapage td { background: #f7f7f7; font-weight: 600; }
    .pied { display: flex; justify-content: space-between; gap: 8mm; font-size: 12px; }
    @page { size: A4 landscape; margin: 8mm; }
    @media print { .outils { display: none; } .feuille { padding: 0; max-width: none; } }
  `,
})
export class FeuilleDAppel implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly session = inject(Session);

  protected readonly heure = heure;
  protected readonly groupeId = this.route.snapshot.paramMap.get('id') ?? '';
  protected readonly feuille = signal<Feuille | null>(null);
  protected readonly erreur = signal<string | null>(null);
  protected readonly lignesVides = Array.from({ length: LIGNES_DE_RATTRAPAGE }, (_, i) => i);
  protected readonly aujourdhui = formatDate(new Date(), 'dd/MM/yyyy', FRANCAIS);
  protected readonly personne = () => {
    const moi = this.session.personne();
    return moi ? `${moi.prenom} ${moi.nom}` : '';
  };

  async ngOnInit(): Promise<void> {
    const parametres = this.route.snapshot.queryParamMap;
    try {
      this.feuille.set(
        await firstValueFrom(
          this.http.get<Feuille>(`/api/groupes/${this.groupeId}/feuille-d-appel`, {
            params: { du: parametres.get('du') ?? '', au: parametres.get('au') ?? '' },
          }),
        ),
      );
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** « 27/11 ». */
  protected court(date: string): string {
    return formatDate(date, 'dd/MM', FRANCAIS);
  }

  protected imprimer(): void {
    window.print();
  }
}
