import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { NombreDeDemandes } from '../../socle/nombre-de-demandes';
import { Session } from '../../socle/session/session';
import { heure } from '../groupes/groupe';
import { AppelARecopier } from '../seances/appel';
import { BoutonDeRappel, EleveEnRouge } from '../caisse/paiement';
import { ecrireUnMontant } from '../../socle/montants';

/**
 * L'accueil, avec le nombre de demandes d'inscription à traiter (DEM-03), les appels à recopier (SEA-09) et les élèves
 * en rouge (CAI-08). Les séances du jour, la caisse et la bibliothèque viendront avec SEA-01.
 */
@Component({
  selector: 'sh-aujourdhui',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, BoutonDeRappel],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Bonjour {{ session.personne()?.prenom }}</h1>
        <p class="sub">Les séances du jour s'afficheront ici.</p>
      </div>
    </div>
    <div class="blocs">
      @if (demandes(); as nombre) {
        <a class="card demandes" routerLink="/demandes">
          <b>{{ nombre === 1 ? '1 demande d’inscription à traiter' : nombre + ' demandes d’inscription à traiter' }}</b>
          <small>Voir les demandes</small>
        </a>
      }
      @if (aRecopier().length > 0) {
        <section class="card recopier" aria-labelledby="titre-a-recopier">
          <h2 id="titre-a-recopier" class="titre">Appels à recopier · {{ aRecopier().length }}</h2>
          <p class="aide">Les séances passées sans appel : recopiez les feuilles dès leur retour.</p>
          @for (seance of aRecopier(); track seance.seanceId) {
            <div class="ligne">
              <span class="deux">
                <b>{{ seance.groupe.matiere }} · {{ seance.groupe.nom }}</b>
                <small>{{ seance.professeur }} · {{ jour(seance.date) }}, {{ heure(seance.debut) }} → {{ heure(seance.fin) }}</small>
              </span>
              <a class="btn" [routerLink]="['/professeurs/groupes', seance.groupe.id, 'appel', seance.seanceId]">Recopier</a>
            </div>
          }
        </section>
      }
      <!-- CAI-08 : les élèves en rouge, avec ce qu'ils doivent, et leur rappel de paiement. -->
      @if (enRouge().length > 0) {
        <section class="card recopier" aria-labelledby="titre-en-rouge">
          <h2 id="titre-en-rouge" class="titre">Élèves en rouge · {{ enRouge().length }}</h2>
          @for (eleve of enRouge(); track eleve.id) {
            <div class="ligne en-rouge">
              <span class="deux">
                <a [routerLink]="['/eleves', eleve.id]"><b>{{ eleve.prenom }} {{ eleve.nom }}</b></a>
                <small>{{ eleve.dues.join(', ') }} · {{ montant(eleve.total) }} TND</small>
              </span>
              <sh-bouton-de-rappel [eleveId]="eleve.id" [dernierRappelLe]="eleve.dernierRappelLe" />
            </div>
          }
        </section>
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </div>
  `,
  styles: `
    .blocs { display: grid; gap: 16px; max-width: 720px; }
    .demandes { display: grid; gap: 2px; padding: 14px 16px; color: var(--ink); text-decoration: none; max-width: 420px; }
    .demandes:hover { background: var(--sunk); }
    .demandes small { color: var(--accent); }
    .recopier { display: grid; gap: 8px; padding: 16px; }
    .titre { font-family: var(--font-display); font-size: 17px; font-weight: 600; margin: 0; }
    .aide { margin: 0; color: var(--ink-2); }
    .ligne { display: flex; align-items: center; justify-content: space-between; gap: 10px; padding: 8px 0; border-top: 1px solid var(--line); }
    .deux { display: grid; min-width: 0; }
    .deux b { font-weight: 500; overflow-wrap: anywhere; }
    .deux small { color: var(--ink-2); font-size: 12.5px; }
    .note { margin: 0; }
    .en-rouge { align-items: flex-start; flex-wrap: wrap; }
    .en-rouge a { color: var(--ink); text-decoration: none; }
  `,
})
export class Aujourdhui implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly session = inject(Session);
  protected readonly demandes = inject(NombreDeDemandes).aTraiter;
  protected readonly heure = heure;
  protected readonly aRecopier = signal<AppelARecopier[]>([]);
  protected readonly enRouge = signal<EleveEnRouge[]>([]);
  protected readonly montant = ecrireUnMontant;
  protected readonly erreur = signal<string | null>(null);

  /** Chaque liste s'affiche dès qu'elle arrive : l'une n'attend pas l'autre. */
  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.charger('/api/seances/a-recopier', (liste: AppelARecopier[]) => this.aRecopier.set(liste)),
      this.charger('/api/eleves/en-rouge', (liste: EleveEnRouge[]) => this.enRouge.set(liste)),
    ]);
  }

  private async charger<T>(adresse: string, montrer: (liste: T[]) => void): Promise<void> {
    try {
      montrer(await firstValueFrom(this.http.get<T[]>(adresse)));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** « sam. 20/11 ». */
  protected jour(date: string): string {
    return formatDate(date, 'EEE dd/MM', FRANCAIS);
  }
}
