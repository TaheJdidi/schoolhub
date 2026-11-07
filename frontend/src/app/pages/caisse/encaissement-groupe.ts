import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { ecrireUnMontant } from '../../socle/montants';
import { EleveAEncaisser, EncaissementDuGroupe, RecuDecrit, RecusDuGroupe } from './recu';

/** « 18 reçus », « 1 reçu ». */
const recus = (nombre: number) => `${nombre} reçu${nombre > 1 ? 's' : ''}`;

/**
 * CAI-04 : l'encaissement groupé, depuis un groupe. « Qui paie maintenant ? » liste les élèves avec ce que chacun doit à
 * ce jour dans le groupe (assurance comprise si elle est due) ; la secrétaire coche ceux qui paient. Chacun paie tout et
 * reçoit son propre reçu. Ensuite, les reçus créés, et « Imprimer les reçus » : un seul fichier, un reçu A5 par page.
 */
@Component({
  selector: 'sh-encaissement-groupe',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    @if (introuvable()) {
      <div class="pagehead">
        <div>
          <h1 class="h1">Groupe introuvable</h1>
          <p class="sub">Ce groupe n'existe pas.</p>
        </div>
      </div>
      <a class="btn" routerLink="/professeurs">Revenir aux professeurs</a>
    } @else if (crees(); as crees) {
      <div class="pagehead">
        <div>
          <nav class="fil" aria-label="Fil d'Ariane">
            <a [routerLink]="['/professeurs/groupes', groupeId]">{{
              encaissement()?.groupe?.nom
            }}</a>
            ›
          </nav>
          <h1 class="h1">
            {{ nombreDeRecus(crees.length) }} {{ crees.length > 1 ? 'créés' : 'créé' }}
          </h1>
          <p class="sub">{{ montant(totalDe(crees)) }} TND encaissés en espèces</p>
        </div>
        <div class="actions">
          <a class="btn" [href]="impression(crees)" target="_blank" rel="noopener"
            >Imprimer les {{ nombreDeRecus(crees.length) }}</a
          >
          <a class="btn primary" [routerLink]="['/professeurs/groupes', groupeId]"
            >Retour au groupe</a
          >
        </div>
      </div>
      <section class="card carte">
        <h2 class="titre">Un reçu par élève</h2>
        <p class="aide">L'email part tout seul quand l'élève en a un, avec copie au centre.</p>
        @for (recu of crees; track recu.id) {
          <div class="ligne">
            <span class="numero">n° {{ recu.numero }}</span>
            <span class="deux">
              <b>{{ recu.eleve.prenom }} {{ recu.eleve.nom }}</b>
              <small>{{ libelles(recu) }}</small>
            </span>
            <b class="montant">{{ montant(recu.total) }} TND</b>
            <span
              class="pill"
              [class.ok]="recu.emails.length > 0"
              [class.muted]="recu.emails.length === 0"
            >
              {{ recu.emails.length > 0 ? 'Email envoyé' : "Pas d'email" }}
            </span>
            <a class="btn sm" [routerLink]="['/caisse/recus', recu.id]">Voir</a>
          </div>
        }
      </section>
    } @else if (encaissement(); as encaissement) {
      <div class="pagehead">
        <div>
          <nav class="fil" aria-label="Fil d'Ariane">
            <a routerLink="/professeurs">Professeurs</a> ›
            <a [routerLink]="['/professeurs/groupes', groupeId]">{{ encaissement.groupe.nom }}</a> ›
          </nav>
          <h1 class="h1">Encaissement groupé</h1>
          <p class="sub">{{ encaissement.groupe.nom }} · {{ encaissement.groupe.matiere }}</p>
        </div>
      </div>
      <div class="cartes">
        <section class="card carte">
          <div class="entete">
            <div>
              <h2 class="titre">Qui paie maintenant ?</h2>
              <p class="aide">
                Cochez les élèves qui paient. Chacun paie tout ce qu'il doit dans ce groupe et
                reçoit son propre reçu.
              </p>
            </div>
            @if (encaissement.aPayer.length > 0) {
              <button type="button" class="btn sm" (click)="toutCocher()">
                {{ tousCoches() ? 'Tout décocher' : 'Tout cocher' }}
              </button>
            }
          </div>
          @for (eleve of encaissement.aPayer; track eleve.eleveId) {
            <label class="ligne a-cocher">
              <input
                type="checkbox"
                [checked]="coches().has(eleve.eleveId)"
                (change)="basculer(eleve)"
              />
              <!-- Comme le prototype (écran 5) : un point de couleur, la couleur en toutes lettres pour les lecteurs d'écran. -->
              <span class="point" [class]="'point ' + (eleve.couleur ?? '')" role="img" [attr.aria-label]="libelleDeCouleur(eleve.couleur)"></span>
              <span class="deux">
                <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                <small>{{ sommes(eleve) }}</small>
              </span>
              <b class="montant">{{ montant(eleve.total) }} TND</b>
            </label>
          } @empty {
            <p class="vide">Tous les élèves du groupe sont à jour.</p>
          }
          <div class="pied">
            <span
              ><b>{{ choisis().length }}</b> élève{{ choisis().length > 1 ? 's' : '' }} ·
              <b>{{ montant(total()) }} TND</b></span
            >
            <button
              type="button"
              class="btn primary"
              [disabled]="envoi() || choisis().length === 0"
              (click)="encaisser()"
            >
              Encaisser et créer
              {{ choisis().length > 0 ? nombreDeRecus(choisis().length) : 'les reçus' }}
            </button>
          </div>
          @if (erreur(); as erreur) {
            <p class="note bad" role="alert">{{ erreur }}</p>
          }
        </section>
        <section class="card carte">
          <div class="entete">
            <h2 class="titre">Déjà à jour</h2>
            <small class="aide">{{ encaissement.aJour.length }} élève{{ encaissement.aJour.length > 1 ? 's' : '' }}</small>
          </div>
          <p class="a-jour">{{ aJour() || 'Personne pour le moment.' }}</p>
        </section>
      </div>
    } @else if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    .cartes {
      display: grid;
      gap: 16px;
      grid-template-columns: minmax(0, 2fr) minmax(0, 1fr);
      align-items: start;
    }
    @media (max-width: 900px) {
      .cartes {
        grid-template-columns: minmax(0, 1fr);
      }
    }
    .entete {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 12px;
    }
    .ligne {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 10px;
      padding: 9px 0;
      border-top: 1px solid var(--line);
      min-width: 0;
    }
    .a-cocher {
      cursor: pointer;
    }
    .deux {
      display: grid;
      flex: 1;
      min-width: 0;
    }
    .point { flex: none; width: 10px; height: 10px; border-radius: 50%; background: var(--line-2); }
    .point.AJour { background: var(--ok); }
    .point.APayer { background: var(--surface); box-shadow: inset 0 0 0 2px var(--accent); }
    .point.Orange { background: var(--warn); }
    .point.Rouge { background: var(--bad); }
    .deux b {
      font-weight: 600;
      overflow-wrap: anywhere;
    }
    .deux small {
      color: var(--ink-2);
      font-size: 12.5px;
      overflow-wrap: anywhere;
    }
    .numero {
      min-width: 56px;
      font-variant-numeric: tabular-nums;
    }
    .pied {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 10px;
      padding-top: 12px;
      border-top: 1px solid var(--line);
    }
    .a-jour {
      color: var(--ink-2);
      font-size: 13px;
      line-height: 1.7;
      margin: 0;
    }
    .pagehead .actions {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }
  `,
})
export class EncaissementGroupe implements OnInit {
  /** « À payer », « Orange », « Rouge » : ce que dit le point de couleur. */
  protected libelleDeCouleur(couleur: string | null | undefined): string {
    return couleur === 'APayer' ? 'À payer' : couleur === 'AJour' ? 'À jour' : (couleur ?? '');
  }
  private readonly http = inject(HttpClient);

  protected readonly groupeId = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  protected readonly montant = ecrireUnMontant;
  protected readonly nombreDeRecus = recus;
  protected readonly encaissement = signal<EncaissementDuGroupe | null>(null);
  protected readonly introuvable = signal(false);
  protected readonly coches = signal<ReadonlySet<string>>(new Set());
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);
  protected readonly crees = signal<RecuDecrit[] | null>(null);

  /** Les élèves cochés, dans l'ordre de la page. */
  protected readonly choisis = computed(() =>
    (this.encaissement()?.aPayer ?? []).filter((e) => this.coches().has(e.eleveId)),
  );
  protected readonly total = computed(() =>
    this.choisis().reduce((total, e) => total + e.total, 0),
  );
  protected readonly tousCoches = computed(() => {
    const aPayer = this.encaissement()?.aPayer ?? [];
    return aPayer.length > 0 && aPayer.every((e) => this.coches().has(e.eleveId));
  });
  protected readonly aJour = computed(() =>
    (this.encaissement()?.aJour ?? []).map((e) => `${e.prenom} ${e.nom}`).join(', '),
  );

  async ngOnInit(): Promise<void> {
    try {
      this.encaissement.set(
        await firstValueFrom(
          this.http.get<EncaissementDuGroupe>(`/api/groupes/${this.groupeId}/encaissement`),
        ),
      );
    } catch (erreur) {
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) this.introuvable.set(true);
      else this.erreur.set(messageDErreur(erreur));
    }
  }

  protected basculer(eleve: EleveAEncaisser): void {
    this.coches.update((coches) => {
      const suivants = new Set(coches);
      if (suivants.has(eleve.eleveId)) suivants.delete(eleve.eleveId);
      else suivants.add(eleve.eleveId);
      return suivants;
    });
  }

  protected toutCocher(): void {
    this.coches.set(
      this.tousCoches()
        ? new Set()
        : new Set((this.encaissement()?.aPayer ?? []).map((e) => e.eleveId)),
    );
  }

  /** « Mois 3 + Assurance 2027-2028 ». */
  protected sommes(eleve: EleveAEncaisser): string {
    return eleve.sommes.map((s) => s.libelle).join(' + ');
  }

  protected libelles(recu: RecuDecrit): string {
    return recu.lignes.map((l) => l.libelle).join(' + ');
  }

  protected totalDe(liste: RecuDecrit[]): number {
    return liste.reduce((total, r) => total + r.total, 0);
  }

  /** La page sans le menu, un reçu A5 par page : l'impression du navigateur en fait un seul fichier. */
  protected impression(liste: RecuDecrit[]): string {
    return `/recus/impression?ids=${liste.map((r) => r.id).join(',')}`;
  }

  /** Chaque élève coché, avec les sommes que la page montre : le serveur vérifie qu'elles sont encore à payer. */
  protected async encaisser(): Promise<void> {
    const choisis = this.choisis();
    if (this.envoi() || choisis.length === 0) return;
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      const reponse = await firstValueFrom(
        this.http.post<RecusDuGroupe>(`/api/groupes/${this.groupeId}/encaissement`, {
          eleves: choisis.map((e) => ({
            eleveId: e.eleveId,
            sommes: e.sommes.map((s) => ({ inscriptionId: s.inscriptionId, cle: s.cle })),
          })),
        }),
      );
      this.crees.set(reponse.recus);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
