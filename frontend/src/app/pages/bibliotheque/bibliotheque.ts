import { ActionsDeListe } from '../../socle/actions-de-liste';
import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { EnregistrementDeLEleve, EleveTrouve } from '../eleves/eleve';
import { FormulaireDeLEleve } from '../eleves/formulaire-de-l-eleve';
import { dateDuJourLocale } from '../groupes/groupe';
import { RecuDecrit } from '../caisse/recu';

/** Une entrée dans la liste (GET /api/bibliotheque). */
export interface EntreeDecrite {
  id: string;
  eleveId: string;
  prenom: string;
  nom: string;
  exterieur: boolean;
  /** « 14:32 ». */
  arrivee: string;
  depart: string | null;
  recuId: string;
  numero: number;
  annule: boolean;
}

/** BIB-02 et BIB-03 : la liste d'un jour. */
export interface JourneeALaBibliotheque {
  jour: string;
  prix: number | null;
  capacite: number | null;
  presents: number;
  entrees: number;
  encaisse: number;
  liste: EntreeDecrite[];
}

/** BIB-01 : la réponse d'une entrée notée. */
export interface EntreeEnregistree {
  entree: EntreeDecrite;
  recu: RecuDecrit;
  avertissements: string[];
}

/**
 * BIB-01 à BIB-03 : la bibliothèque. Les présents en ce moment et les entrées du jour ; « Nouvelle entrée » pour un
 * élève du centre (cherché par son nom) ou une personne de l'extérieur (sa fiche, puis l'entrée) ; la liste du jour avec
 * « Noter le départ » ; un jour passé, et « Imprimer ».
 */
@Component({
  selector: 'sh-bibliotheque',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, FormulaireDeLEleve, ActionsDeListe],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Bibliothèque</h1>
        <p class="sub">
          {{ estAujourdhui() ? 'Liste du jour' : 'Liste du ' + date(jour()) }}
          @if (journee()?.prix; as prix) {
            · entrée à la journée {{ montant(prix) }} TND, payée en arrivant, même prix pour tous
          }
        </p>
      </div>
      <div class="actions">
        <label class="f jour">Jour <input type="date" [value]="jour()" (change)="changerDeJour($any($event.target).value)" /></label>
        <sh-actions-de-liste [titre]="'Bibliothèque · ' + date(jour())" [nombre]="journee()?.liste?.length ?? 0" exporter="/api/exports/bibliotheque"
          [filtres]="{ jour: jour() }" />
      </div>
    </div>

    @if (journee(); as journee) {
      <div class="chiffres">
        @if (estAujourdhui()) {
          <div [class.plein]="plein()">
            <span>Présents en ce moment</span>
            <b class="num">{{ journee.presents }}@if (journee.capacite) { / {{ journee.capacite }} }</b>
            <small>{{ placesLibres() }}</small>
          </div>
        }
        <div>
          <span>{{ estAujourdhui() ? "Entrées aujourd'hui" : 'Entrées' }}</span>
          <b class="num">{{ journee.entrees }}</b>
          <small>{{ montant(journee.encaisse) }} TND encaissés</small>
        </div>
      </div>
    }

    <div class="cartes" [class.deux-colonnes]="estAujourdhui()">
      <section class="card carte" aria-labelledby="t-liste">
        <h2 class="titre" id="t-liste">{{ estAujourdhui() ? 'Liste du jour' : 'Liste du ' + date(jour()) }}</h2>
        <p class="aide">L'heure de départ est facultative.</p>
        @if (journee(); as journee) {
          <div class="defile">
            <table>
              <thead>
                <tr><th>Nom</th><th>Arrivée</th><th>Départ</th><th>Reçu</th></tr>
              </thead>
              <tbody>
                @for (entree of journee.liste; track entree.id) {
                  <tr [class.annulee]="entree.annule">
                    <td>
                      <a [routerLink]="['/eleves', entree.eleveId]">{{ entree.prenom }} {{ entree.nom }}</a><br />
                      <small>{{ entree.exterieur ? 'Extérieur' : 'Élève du centre' }}</small>
                    </td>
                    <td class="num">{{ entree.arrivee }}</td>
                    <td>
                      @if (entree.depart) {
                        <span class="num">{{ entree.depart }}</span>
                      } @else if (entree.annule) {
                        —
                      } @else if (corrige() === entree.id) {
                        <span class="depart">
                          <input #heure type="time" [value]="maintenant()" aria-label="Heure de départ" />
                          <button type="button" class="btn sm primary" [disabled]="envoi()" (click)="noterLeDepart(entree, heure.value)">OK</button>
                        </span>
                      } @else {
                        <span class="depart">
                          <button type="button" class="btn sm" [disabled]="envoi()" (click)="noterLeDepart(entree, null)">Noter le départ</button>
                          <button type="button" class="lien" (click)="corrige.set(entree.id)">autre heure</button>
                        </span>
                      }
                    </td>
                    <td>
                      <a class="num" [routerLink]="['/caisse/recus', entree.recuId]">n° {{ entree.numero }}</a>
                      @if (entree.annule) {
                        <span class="pill muted">reçu annulé</span>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="4" class="vide">Aucune entrée ce jour-là.</td></tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>

      @if (estAujourdhui()) {
        <section class="card carte nouvelle" aria-labelledby="t-nouvelle">
          <h2 class="titre" id="t-nouvelle">Nouvelle entrée</h2>
          @if (plein()) {
            <p class="note warn">La bibliothèque est pleine. Vous pouvez quand même noter l'entrée.</p>
          }
          @if (exterieur()) {
            <sh-formulaire-de-l-eleve brouillonDeLaCreation="bibliotheque-exterieur" (enregistre)="entrerLExterieur($event)" (annule)="exterieur.set(false)" />
          } @else {
            <label class="f">
              Élève du centre
              <input #recherche autocomplete="off" placeholder="Nom ou prénom" [value]="saisie()" (input)="chercher(recherche.value)" />
            </label>
            @for (eleve of trouves(); track eleve.id) {
              <button type="button" class="trouve" [disabled]="envoi()" (click)="entrer(eleve.id)">
                <span class="deux"><b>{{ eleve.prenom }} {{ eleve.nom }}</b><small>{{ eleve.niveau }}</small></span>
                <span class="btn sm primary">{{ montant(journee()?.prix ?? 0) }} TND</span>
              </button>
            } @empty {
              @if (saisie().trim().length >= 2) {
                <p class="aide">Aucun élève trouvé. Utilisez « Personne de l'extérieur ».</p>
              }
            }
            <button type="button" class="btn" (click)="exterieur.set(true)">Personne de l'extérieur</button>
            <p class="aide">Une personne de l'extérieur est enregistrée comme élève sans groupe. Le reçu se remet seulement si elle le demande.</p>
          }
          @for (avertissement of avertissements(); track avertissement) {
            <p class="note warn" role="status">{{ avertissement }}</p>
          }
          @if (succes(); as succes) {
            <p class="note ok" role="status">{{ succes }}</p>
          }
        </section>
      }
    </div>
    @if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    .pagehead .actions { display: flex; gap: 10px; align-items: end; flex-wrap: wrap; }
    .jour { min-width: 150px; }
    .chiffres { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; margin-bottom: 16px; }
    .chiffres div { display: grid; gap: 2px; padding: 12px 14px; border-radius: 12px; background: var(--surface); border: 1px solid var(--line); }
    .chiffres div.plein { border-color: var(--warn); }
    .chiffres span, .chiffres small { color: var(--ink-2); font-size: 12.5px; }
    .chiffres b { font-size: 22px; }
    .deux-colonnes { grid-template-columns: minmax(0, 2fr) minmax(0, 1fr); align-items: start; }
    @media (max-width: 900px) { .deux-colonnes { grid-template-columns: minmax(0, 1fr); } }
    .defile { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    th, td { text-align: left; padding: 8px 6px; border-top: 1px solid var(--line); vertical-align: top; }
    th { color: var(--ink-2); font-weight: 500; border-top: 0; }
    td small { color: var(--ink-2); }
    tr.annulee td:first-child a { text-decoration: line-through; }
    .depart { display: inline-flex; gap: 6px; align-items: center; flex-wrap: wrap; }
    .lien { background: none; border: 0; padding: 0; color: var(--ink-2); text-decoration: underline; cursor: pointer; font-size: 12.5px; }
    .nouvelle { display: grid; gap: 10px; }
    .trouve { display: flex; align-items: center; gap: 10px; padding: 8px 10px; border: 1px solid var(--line); border-radius: 10px; background: var(--surface); cursor: pointer; text-align: left; }
    .deux { display: grid; flex: 1; min-width: 0; }
    .deux small { color: var(--ink-2); font-size: 12.5px; }
    @media print { .jour, .nouvelle, .depart { display: none; } }
  `,
})
export class Bibliotheque implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly aujourdhui = dateDuJourLocale();
  private minuteur: ReturnType<typeof setTimeout> | undefined;

  protected readonly montant = ecrireUnMontant;
  protected readonly jour = signal(this.aujourdhui);
  protected readonly journee = signal<JourneeALaBibliotheque | null>(null);
  protected readonly trouves = signal<EleveTrouve[]>([]);
  protected readonly saisie = signal('');
  protected readonly exterieur = signal(false);
  protected readonly corrige = signal<string | null>(null);
  protected readonly envoi = signal(false);
  protected readonly avertissements = signal<string[]>([]);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  protected readonly estAujourdhui = computed(() => this.jour() === this.aujourdhui);
  protected readonly plein = computed(() => {
    const journee = this.journee();
    return !!journee?.capacite && journee.presents >= journee.capacite;
  });
  protected readonly placesLibres = computed(() => {
    const journee = this.journee();
    if (!journee?.capacite) return '';
    return this.plein() ? "Capacité atteinte : l'entrée reste possible" : `${journee.capacite - journee.presents} place${journee.capacite - journee.presents > 1 ? 's' : ''} libre${journee.capacite - journee.presents > 1 ? 's' : ''}`;
  });

  async ngOnInit(): Promise<void> {
    await this.charger();
  }

  ngOnDestroy(): void {
    clearTimeout(this.minuteur);
  }

  protected async changerDeJour(jour: string): Promise<void> {
    this.jour.set(jour || this.aujourdhui);
    await this.charger();
  }

  /** Dès 2 caractères, après une courte pause. */
  protected chercher(saisie: string): void {
    clearTimeout(this.minuteur);
    this.saisie.set(saisie);
    const recherche = saisie.trim();
    if (recherche.length < 2) {
      this.trouves.set([]);
      return;
    }
    this.minuteur = setTimeout(async () => {
      try {
        const trouves = await firstValueFrom(this.http.get<EleveTrouve[]>('/api/eleves', { params: { recherche } }));
        this.trouves.set(trouves.slice(0, 5));
      } catch (erreur) {
        this.erreur.set(messageDErreur(erreur));
      }
    }, 250);
  }

  protected async entrer(eleveId: string): Promise<void> {
    if (this.envoi()) return;
    this.envoi.set(true);
    this.erreur.set(null);
    this.succes.set(null);
    try {
      const enregistree = await firstValueFrom(this.http.post<EntreeEnregistree>('/api/bibliotheque/entrees', { eleveId }));
      this.avertissements.set(enregistree.avertissements);
      this.succes.set(`Entrée notée pour ${enregistree.entree.prenom} ${enregistree.entree.nom}. Reçu n° ${enregistree.recu.numero}, remis sur demande.`);
      this.trouves.set([]);
      this.saisie.set('');
      this.exterieur.set(false);
      await this.charger();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** La fiche de la personne de l'extérieur est créée : son entrée suit. */
  protected async entrerLExterieur(enregistrement: EnregistrementDeLEleve): Promise<void> {
    await this.entrer(enregistrement.eleve.id);
  }

  /** « Noter le départ » : maintenant ; ou l'heure corrigée. */
  protected async noterLeDepart(entree: EntreeDecrite, heure: string | null): Promise<void> {
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      await firstValueFrom(this.http.put<EntreeDecrite>(`/api/bibliotheque/entrees/${entree.id}/depart`, { heure }));
      this.corrige.set(null);
      await this.charger();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** « 14:32 », l'heure de l'appareil, proposée quand la secrétaire corrige le départ. */
  protected maintenant(): string {
    return formatDate(new Date(), 'HH:mm', FRANCAIS);
  }

  /** « 27/11/2027 ». */
  protected date(texte: string): string {
    return formatDate(texte, 'dd/MM/yyyy', FRANCAIS);
  }

  private async charger(): Promise<void> {
    try {
      this.journee.set(await firstValueFrom(this.http.get<JourneeALaBibliotheque>('/api/bibliotheque', { params: { jour: this.jour() } })));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }
}
