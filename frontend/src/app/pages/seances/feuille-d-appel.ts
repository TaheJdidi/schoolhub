import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { Session } from '../../socle/session/session';
import { FicheDuGroupe, heure } from '../groupes/groupe';
import { FeuilleDAppel as Feuille } from './appel';
import { PeriodeDeLaFeuille, periodesDeLaFeuille } from './carte-du-calendrier';

/** Les lignes vides pour les élèves en rattrapage. */
const LIGNES_DE_RATTRAPAGE = 3;

/**
 * SEA-08 : la feuille d'appel d'un groupe, comme le prototype (écran 4) : dans l'application, la période à choisir (le
 * Mois en cours d'office, ou la séance du jour) et « Imprimer » ; puis la page A4 : le centre, l'année scolaire, le
 * groupe, sa matière, son niveau et son professeur ; pour chaque élève, son nom, son prénom, une case par séance et une
 * case de signature ; des lignes vides pour les élèves en rattrapage ; la consigne. Ni téléphone, ni paiement. Seule la
 * page A4 s'imprime.
 */
@Component({
  selector: 'sh-feuille-d-appel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="pagehead sans-impression">
      <div>
        <nav class="fil" aria-label="Fil d'Ariane">
          <a routerLink="/professeurs">Professeurs</a> ›
          <a [routerLink]="['/professeurs/groupes', groupeId]">{{ fiche()?.nom ?? 'Groupe' }}</a> › Feuille d'appel
        </nav>
        <h1 class="h1">Feuille d'appel</h1>
        @if (fiche(); as fiche) {
          <p class="sub">{{ fiche.nom }} · {{ fiche.matiere }}{{ fiche.niveau ? ' · ' + fiche.niveau.nom : '' }}</p>
        }
      </div>
      <div class="outils">
        @if (periodesMontrees().length > 1) {
          <div class="bascule" role="group" aria-label="Période de la feuille">
            @for (periode of periodesMontrees(); track periode.valeur) {
              <button type="button" [attr.aria-pressed]="periode.du === du() && periode.au === au()" (click)="choisir(periode)">
                {{ periode.libelle }}
              </button>
            }
          </div>
        }
        <button type="button" class="btn primary" [disabled]="!feuille()" (click)="imprimer()">Imprimer</button>
      </div>
    </div>
    <p class="note info sans-impression">La feuille ne montre ni téléphone ni paiement. Le professeur peut la remplir, puis la secrétaire recopie l'appel dans SchoolHub.</p>
    <main class="feuille">
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
    :host { display: grid; gap: 14px; }
    .pagehead { margin-bottom: 0; }
    .fil { font-size: 13px; color: var(--ink-2); margin-bottom: 4px; }
    .fil a { color: var(--accent); text-decoration: none; }
    .outils { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; justify-content: flex-end; }
    .bascule { display: inline-flex; padding: 3px; border: 1px solid var(--line-2); border-radius: 10px; background: var(--surface); }
    .bascule button { padding: 6px 12px; border: 0; border-radius: 7px; background: none; color: var(--ink-2); font: inherit; font-size: 13px; cursor: pointer; }
    .bascule button[aria-pressed='true'] { background: var(--sunk); color: var(--ink); font-weight: 600; box-shadow: inset 0 0 0 1px var(--line-2); }
    .note.info { margin: 0; background: var(--sunk); color: var(--ink-2); }
    /* La page A4, posée comme une feuille de papier ; seule elle s'imprime. */
    .feuille { width: 100%; max-width: 277mm; margin: 0 auto; padding: 10mm; display: grid; gap: 6mm; background: #fff; color: #111;
      box-shadow: 0 12px 32px rgba(16, 36, 56, 0.16); border-radius: 4px; }
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
    @media print { .feuille { padding: 0; max-width: none; box-shadow: none; } }
  `,
})
export class FeuilleDAppel implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
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

  protected readonly fiche = signal<FicheDuGroupe | null>(null);
  protected readonly du = signal('');
  protected readonly au = signal('');
  /** Le Mois en cours (ou les prochaines séances) et la séance du jour, comme le prototype. */
  protected readonly periodesMontrees = computed(() => {
    const fiche = this.fiche();
    if (!fiche) return [];
    const toutes = periodesDeLaFeuille(fiche);
    const jour = toutes.find((p) => p.valeur === 'jour');
    const principale = toutes.find((p) => p.valeur !== 'jour');
    return [principale, jour]
      .filter((p): p is PeriodeDeLaFeuille => !!p)
      .map((p) => ({ ...p, libelle: p.libelle.replace(' · en cours', '').replace('La séance du jour', 'Séance du jour') }));
  });

  /** La période de l'adresse ; sans elle, le Mois en cours (ou les prochaines séances). */
  async ngOnInit(): Promise<void> {
    const parametres = this.route.snapshot.queryParamMap;
    try {
      const fiche = await firstValueFrom(this.http.get<FicheDuGroupe>(`/api/groupes/${this.groupeId}`));
      this.fiche.set(fiche);
      const parDefaut = this.periodesMontrees()[0];
      await this.charger(parametres.get('du') ?? parDefaut?.du ?? '', parametres.get('au') ?? parDefaut?.au ?? '');
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected async choisir(periode: PeriodeDeLaFeuille): Promise<void> {
    void this.router.navigate([], { relativeTo: this.route, queryParams: { du: periode.du, au: periode.au }, replaceUrl: true });
    try {
      await this.charger(periode.du, periode.au);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  private async charger(du: string, au: string): Promise<void> {
    this.du.set(du);
    this.au.set(au);
    this.erreur.set(null);
    this.feuille.set(await firstValueFrom(this.http.get<Feuille>(`/api/groupes/${this.groupeId}/feuille-d-appel`, { params: { du, au } })));
  }

  /** « 27/11 ». */
  protected court(date: string): string {
    return formatDate(date, 'dd/MM', FRANCAIS);
  }

  protected imprimer(): void {
    window.print();
  }
}
