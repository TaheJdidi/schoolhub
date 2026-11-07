import { formatDate } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, Observable } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderUnBrouillon } from '../../socle/brouillons';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { heure } from '../groupes/groupe';
import { MessageWhatsApp } from '../inscriptions/inscription';
import { AppelDeLaSeance, ChangementDuCalendrier, EleveALAppel, EleveEnRattrapage, EleveRattrapable } from './appel';
import { MessagesDuChangement } from './messages-du-changement';

/** Un absent à prévenir : un élève du groupe ou en rattrapage (SEA-11, SEA-12). */
type Prevenable = EleveALAppel | EleveEnRattrapage;

/** Ce que la secrétaire fait de la séance (SEA-03, SEA-04). */
type ActionSurLaSeance = 'annuler' | 'deplacer' | 'remplacer';

/**
 * SEA-07, SEA-10 et SEA-11 : l'appel d'une séance. Tous les élèves sont présents ; un toucher marque un absent, un autre
 * le remet présent. « Enregistrer l'appel » fait passer la séance à « appel fait » ; ensuite, « Corriger l'appel » reste
 * possible à tout moment. Après l'enregistrement, « Prévenir les parents des absents » liste les messages WhatsApp prêts ;
 * un message ouvert est marqué, pour ne pas l'envoyer deux fois.
 */
@Component({
  selector: 'sh-page-de-l-appel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MessagesDuChangement, BrouillonRepris],
  host: { '[class.integre]': 'integre()' },
  template: `
    @if (introuvable()) {
      <div class="pagehead">
        <div>
          <nav class="fil" aria-label="Fil d'Ariane"><a routerLink="/professeurs">Professeurs</a> ›</nav>
          <h1 class="h1">Séance introuvable</h1>
          <p class="sub">Cette séance n'existe pas.</p>
        </div>
      </div>
    } @else if (appel(); as appel) {
      @if (!integre()) {
      <div class="pagehead">
        <div>
          <nav class="fil" aria-label="Fil d'Ariane">
            <a routerLink="/professeurs">Professeurs</a> ›
            <a [routerLink]="['/professeurs/groupes', appel.groupe.id]">{{ appel.groupe.nom }}</a> ›
          </nav>
          <h1 class="h1">Appel du {{ jour(appel.date) }}</h1>
          <p class="sub">{{ appel.groupe.matiere }} · {{ appel.professeur }} · {{ heure(appel.debut) }} → {{ heure(appel.fin) }}</p>
        </div>
      </div>
      }

      <div class="cartes">
        @if (integre()) {
          <!-- Comme le prototype (écran 3) : l'appel dans la fiche du groupe, une ligne par élève avec ce qu'il doit. -->
          <section class="card appel-integre">
            <div class="entete-appel">
              <div>
                <h2 class="titre">Appel · {{ jourCourt(appel.date) }}{{ libelleDeLaSeance() ? ' · ' + libelleDeLaSeance() : '' }}</h2>
                @if (appel.annulee) {
                  <p class="aide">Cette séance est annulée : {{ appel.motifDAnnulation }}.</p>
                } @else if (!appel.ouvert) {
                  <p class="aide">L'appel s'ouvre le jour de la séance.</p>
                } @else if (appel.appelFait && !correction()) {
                  <p class="aide">Fait{{ appel.appelFaitPar ? ' par ' + appel.appelFaitPar : '' }}{{ appel.appelFaitLe ? ' à ' + heureDuMoment(appel.appelFaitLe) : '' }}.</p>
                } @else {
                  <p class="aide">Tous les élèves sont présents par défaut : cochez seulement les absents.</p>
                }
              </div>
              @if (appel.appelFait) {
                <span class="pill ok">✓ Appel fait</span>
              } @else if (appel.ouvert) {
                <span class="pill warn">Appel à faire</span>
              } @else {
                <span class="pill muted">À venir</span>
              }
            </div>
            <sh-brouillon-repris [brouillon]="brouillon" texte="Vous reprenez l'appel que vous n'aviez pas enregistré." action="Annuler les changements" />
            @for (eleve of appel.eleves; track eleve.inscriptionId) {
              <div class="ligne-appel">
                <span class="point" [class]="'point ' + (paiementDe(eleve.eleveId)?.couleur ?? '')" aria-hidden="true"></span>
                <span class="deux">
                  <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                  @if (paiementDe(eleve.eleveId); as paiement) {
                    <small>{{ paiement.texte }}</small>
                  }
                  @if (eleve.rattrapeeLe; as le) {
                    <small>Rattrapée le {{ court(le) }}</small>
                  }
                </span>
                @if (modifiable()) {
                  <label class="case-absent" [class.coche]="absents().includes(eleve.inscriptionId)">
                    <input type="checkbox" [checked]="absents().includes(eleve.inscriptionId)" [disabled]="envoi()" (change)="basculer(eleve)" />
                    Absent
                  </label>
                } @else if (appel.appelFait) {
                  @if (absents().includes(eleve.inscriptionId)) {
                    <span class="pill bad">Absent</span>
                  } @else {
                    <span class="present">Présent</span>
                  }
                }
              </div>
            } @empty {
              <p class="vide">Aucun élève attendu à cette séance.</p>
            }
            @if (appel.rattrapages?.length) {
              <p class="bande">En rattrapage dans ce groupe</p>
              @for (eleve of appel.rattrapages; track eleve.rattrapageId) {
                <div class="ligne-appel">
                  <span class="point" aria-hidden="true"></span>
                  <span class="deux">
                    <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                    <small>Vient du {{ eleve.groupeDOrigine }} · paie dans son groupe</small>
                  </span>
                  @if (modifiable()) {
                    <label class="case-absent" [class.coche]="absents().includes(eleve.inscriptionId)">
                      <input type="checkbox" [checked]="absents().includes(eleve.inscriptionId)" [disabled]="envoi()" (change)="basculer(eleve)" />
                      Absent
                    </label>
                  } @else if (appel.appelFait) {
                    <span [class]="absents().includes(eleve.inscriptionId) ? 'pill bad' : 'present'">{{ absents().includes(eleve.inscriptionId) ? 'Absent' : 'Présent' }}</span>
                  }
                </div>
              }
            }
            @if (appel.ouvert) {
              <div class="pied-appel">
                <span class="compte">{{ presents() }} présent{{ presents() > 1 ? 's' : '' }} · {{ absents().length }} absent{{ absents().length > 1 ? 's' : '' }}</span>
                <span class="boutons">
                  @if (!appel.annulee) {
                    <button type="button" class="btn ghost" (click)="ouvrirLeRattrapage()">+ Rattrapage</button>
                  }
                  @if (modifiable()) {
                    <button type="button" class="btn primary" [disabled]="envoi()" (click)="enregistrer()">✓ {{ appel.appelFait ? "Enregistrer la correction" : "Valider l'appel" }}</button>
                  } @else {
                    <button type="button" class="btn" (click)="correction.set(true)">Corriger l'appel</button>
                  }
                </span>
              </div>
            }
            @if (succes(); as succes) {
              <p class="note ok" role="status">{{ succes }}</p>
            }
            @if (erreur(); as erreur) {
              <p class="note bad" role="alert">{{ erreur }}</p>
            }
          </section>
        } @else {
        <section class="card carte">
          <div class="entete">
            <h2 class="titre">
              @if (integre()) {
                Appel · {{ court(appel.date) }}
              } @else {
                {{ appel.appelFait ? 'Appel fait' : 'Appel à faire' }}
              }
            </h2>
            @if (integre()) {
              <span class="pill" [class.ok]="appel.appelFait" [class.warn]="!appel.appelFait && appel.ouvert">{{ appel.appelFait ? 'Appel fait' : appel.ouvert ? 'Appel à faire' : 'À venir' }}</span>
            }
            @if (appel.eleves.length > 0) {
              <span class="compte">{{ presents() }} présents · {{ absents().length }} {{ absents().length > 1 ? 'absents' : 'absent' }}</span>
            }
          </div>
          <sh-brouillon-repris [brouillon]="brouillon" texte="Vous reprenez l'appel que vous n'aviez pas enregistré." action="Annuler les changements" />
          @if (appel.annulee) {
            <p class="note warn" role="status">Cette séance est annulée : {{ appel.motifDAnnulation }}.</p>
          } @else if (!appel.ouvert) {
            <p class="note" role="status">L'appel s'ouvre le jour de la séance.</p>
          } @else {
            <p class="aide">Touchez un élève pour le marquer absent ; touchez-le de nouveau pour le remettre présent.</p>
          }
          <div class="eleves">
            @for (eleve of appel.eleves; track eleve.inscriptionId) {
              <button
                type="button"
                class="eleve"
                [class.absent]="absents().includes(eleve.inscriptionId)"
                [attr.aria-pressed]="absents().includes(eleve.inscriptionId)"
                [disabled]="!appel.ouvert || envoi()"
                (click)="basculer(eleve)"
              >
                <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                <small>{{ absents().includes(eleve.inscriptionId) ? 'Absent' : 'Présent' }}</small>
                @if (eleve.rattrapeeLe; as le) {
                  <small class="origine">Rattrapée le {{ court(le) }}</small>
                }
              </button>
            } @empty {
              <p class="vide">Aucun élève attendu à cette séance.</p>
            }
          </div>
          <!-- SEA-12 : les élèves en rattrapage, à part. -->
          @if (appel.rattrapages?.length) {
            <h3 class="tag">En rattrapage</h3>
            <div class="eleves">
              @for (eleve of appel.rattrapages; track eleve.rattrapageId) {
                <div class="en-rattrapage">
                  <button
                    type="button"
                    class="eleve"
                    [class.absent]="absents().includes(eleve.inscriptionId)"
                    [attr.aria-pressed]="absents().includes(eleve.inscriptionId)"
                    [disabled]="!appel.ouvert || envoi()"
                    (click)="basculer(eleve)"
                  >
                    <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                    <small>{{ absents().includes(eleve.inscriptionId) ? 'Absent' : 'Présent' }}</small>
                    <small class="origine">Paie dans son groupe d'origine · {{ eleve.groupeDOrigine }}, rattrape le {{ court(eleve.seanceRattrapee) }}</small>
                  </button>
                  <button type="button" class="btn ghost retirer" [disabled]="envoi()" (click)="retirerLeRattrapage(eleve.rattrapageId)">
                    Retirer {{ eleve.prenom }}
                  </button>
                </div>
              }
            </div>
          }
          @if (appel.ouvert) {
            <div class="actions">
              <button type="button" class="btn primary" [disabled]="envoi()" (click)="enregistrer()">
                {{ appel.appelFait ? "Corriger l'appel" : "Enregistrer l'appel" }}
              </button>
            </div>
          }
          @if (succes(); as succes) {
            <p class="note ok" role="status">{{ succes }}</p>
          }
          @if (erreur(); as erreur) {
            <p class="note bad" role="alert">{{ erreur }}</p>
          }
        </section>
        }

        @if (appel.appelFait && absentsEnregistres().length > 0) {
          <section class="card carte">
            <h2 class="titre">Prévenir les parents des absents</h2>
            @for (eleve of absentsEnregistres(); track eleve.inscriptionId) {
              <div class="absent-a-prevenir">
                <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                @if (eleve.prevenuLe) {
                  <span class="pill ok">Message ouvert</span>
                }
                @for (message of eleve.messages; track message.lien) {
                  <div class="destinataire">
                    <span>{{ pourQui(message) }}</span>
                    <a class="btn" [href]="message.lien" target="_blank" rel="noopener" (click)="prevenir(eleve)">Ouvrir WhatsApp</a>
                  </div>
                } @empty {
                  <p class="vide">Aucun téléphone pour le prévenir.</p>
                }
              </div>
            }
          </section>
        }

        <!-- SEA-12 : ajouter un élève en rattrapage ; dans la fiche du groupe, après « + Rattrapage ». -->
        @if (!appel.annulee && (!integre() || rattrapageOuvert())) {
          <section class="card carte">
            <h2 class="titre">Rattrapage</h2>
            @if (rattrapables() === null) {
              <div class="actions debut">
                <button type="button" class="btn" (click)="chercherLesRattrapables()">Ajouter un élève en rattrapage</button>
              </div>
            } @else {
              <p class="aide">Les élèves des autres groupes du même professeur, de la même matière et du même niveau. Ils paient dans leur groupe.</p>
              @if (rattrapables()!.length === 0) {
                <p class="vide">Aucun élève ne peut venir en rattrapage à cette séance.</p>
              } @else {
                <div class="champs">
                  <label class="f">
                    Élève
                    <select class="inp" aria-label="Élève en rattrapage" (change)="choisirLeRattrapable($any($event.target).value)">
                      <option value="">Choisissez l'élève</option>
                      @for (eleve of rattrapables(); track eleve.inscriptionId) {
                        <option [value]="eleve.inscriptionId">{{ eleve.prenom }} {{ eleve.nom }} · {{ eleve.groupe.nom }}</option>
                      }
                    </select>
                  </label>
                  @if (rattrapableChoisi(); as eleve) {
                    <label class="f">
                      Séance de son groupe qu'il rattrape
                      <select class="inp" aria-label="Séance rattrapée" (change)="seanceRattrapee.set($any($event.target).value)">
                        <option value="">Choisissez la séance</option>
                        @for (seance of eleve.seances; track seance.id) {
                          <option [value]="seance.id">{{ jour(seance.date) }} · {{ heure(seance.debut) }}</option>
                        }
                      </select>
                    </label>
                  }
                </div>
              }
              <div class="actions">
                <button type="button" class="btn" [disabled]="envoi()" (click)="rattrapables.set(null)">Retour</button>
                <button type="button" class="btn primary" [disabled]="envoi()" (click)="ajouterLeRattrapage()">Ajouter en rattrapage</button>
              </div>
            }
            @if (erreurDuRattrapage(); as erreur) {
              <p class="note bad" role="alert">{{ erreur }}</p>
            }
          </section>
        }

        <!-- SEA-03 et SEA-04 : annuler ou déplacer la séance ; remplacer une séance annulée. -->
        @if (!appel.appelFait && !(appel.annulee && appel.nature === 'Supplementaire')) {
          <section class="card carte">
            <h2 class="titre">Cette séance</h2>
            @if (action() === null) {
              <div class="actions debut">
                @if (appel.annulee) {
                  <button type="button" class="btn" (click)="ouvrir('remplacer')">Remplacer cette séance</button>
                } @else {
                  <button type="button" class="btn" (click)="ouvrir('deplacer')">Déplacer cette séance</button>
                  <button type="button" class="btn" (click)="ouvrir('annuler')">Annuler cette séance</button>
                }
              </div>
            } @else {
              <div class="champs">
                @if (action() === 'annuler') {
                  <label class="f">
                    Motif de l'annulation
                    <input #motif autocomplete="off" placeholder="Professeur absent" (input)="motifSaisi.set(motif.value)" />
                  </label>
                } @else {
                  <label class="f">
                    Nouvelle date
                    <input #nouvelleDate type="date" (input)="dateSaisie.set(nouvelleDate.value)" />
                  </label>
                  <label class="f">
                    De
                    <input #nouveauDebut type="time" [value]="debutSaisi()" (input)="debutSaisi.set(nouveauDebut.value)" />
                  </label>
                  <label class="f">
                    À
                    <input #nouvelleFin type="time" [value]="finSaisie()" (input)="finSaisie.set(nouvelleFin.value)" />
                  </label>
                }
              </div>
              @if (action() === 'annuler' && appel.groupe.type !== 'Etude') {
                <p class="aide">Une Révision ou une Formation garde son nombre de séances si vous remplacez celle-ci ensuite.</p>
              }
              <div class="actions">
                <button type="button" class="btn" [disabled]="envoi()" (click)="fermerLAction()">Retour</button>
                <button type="button" class="btn primary" [disabled]="envoi()" (click)="confirmer()">{{ libelleDeLAction() }}</button>
              </div>
            }
            @if (erreurDuCalendrier(); as erreur) {
              <p class="note bad" role="alert">{{ erreur }}</p>
            }
          </section>
        }

        @if (changement(); as changement) {
          <sh-messages-du-changement [changement]="changement" />
        }
      </div>
    } @else if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css', '../inscriptions/inscriptions.css'],
  styles: `
    .compte { color: var(--ink-2); font-size: 13px; }
    .eleves { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: 8px; }
    .eleve { display: grid; gap: 2px; text-align: left; padding: 10px 12px; border: 1px solid var(--line); border-radius: 10px;
      background: var(--surface); color: var(--ink); font: inherit; cursor: pointer; min-height: 52px; }
    .eleve small { color: var(--ok); font-size: 12.5px; }
    .eleve.absent { border-color: var(--bad); background: var(--bad-soft); }
    .eleve.absent small { color: var(--bad); font-weight: 600; }
    .eleve:disabled { cursor: default; }
    .absent-a-prevenir { display: grid; gap: 6px; padding: 10px 0; border-top: 1px solid var(--line); }
    .absent-a-prevenir:first-of-type { border-top: 0; }
    .absent-a-prevenir .pill { justify-self: start; }
    .actions.debut { justify-content: flex-start; }
    .eleve small.origine { color: var(--ink-2); font-weight: 400; }
    /* Dans la fiche du groupe (prototype, écran 3) : une ligne par élève, « Présent » ou « Absent » à droite. */
    .appel-integre { display: grid; padding: 0; overflow: hidden; }
    .entete-appel { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; padding: 14px 18px; border-bottom: 1px solid var(--line); }
    .entete-appel .titre { margin: 0; }
    .entete-appel .aide { margin: 2px 0 0; }
    .ligne-appel { display: flex; align-items: center; gap: 12px; padding: 10px 18px; border-top: 1px solid var(--line); }
    .entete-appel + .ligne-appel, sh-brouillon-repris + .ligne-appel, .bande + .ligne-appel { border-top: 0; }
    .ligne-appel .deux { display: grid; flex: 1; min-width: 0; }
    .ligne-appel .deux b { font-weight: 600; }
    .ligne-appel .deux small { color: var(--ink-2); font-size: 13px; }
    .point { flex: none; width: 10px; height: 10px; border-radius: 50%; background: var(--line-2); }
    .point.AJour { background: var(--ok); }
    .point.APayer { background: var(--surface); box-shadow: inset 0 0 0 2px var(--accent); }
    .point.Orange { background: var(--warn); }
    .point.Rouge { background: var(--bad); }
    .case-absent { display: inline-flex; align-items: center; gap: 8px; padding: 6px 14px; border: 1px solid var(--line-2); border-radius: 999px; color: var(--ink-2); font-size: 13.5px; cursor: pointer; }
    .case-absent input { width: 16px; height: 16px; margin: 0; accent-color: var(--bad); }
    .case-absent.coche { border-color: var(--bad); background: var(--bad-soft); color: var(--bad); }
    .present { color: var(--ink-3); font-size: 13.5px; }
    .bande { margin: 0; padding: 8px 18px; background: var(--sunk); border-top: 1px solid var(--line); color: var(--ink-2); font-size: 12px; font-weight: 600; letter-spacing: 0.04em; text-transform: uppercase; }
    .pied-appel { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px; padding: 12px 18px; border-top: 1px solid var(--line); }
    .pied-appel .boutons { display: flex; flex-wrap: wrap; gap: 8px; }
    .appel-integre > .note { margin: 0 18px 14px; }
    :host(.integre) .eleves { grid-template-columns: minmax(0, 1fr); gap: 0; }
    :host(.integre) .eleve { grid-template-columns: minmax(0, 1fr) auto; align-items: center; column-gap: 12px; padding: 10px 2px;
      border: 0; border-top: 1px solid var(--line); border-radius: 0; background: none; }
    :host(.integre) .eleve:first-child { border-top: 0; }
    :host(.integre) .eleve > small:first-of-type { grid-column: 2; grid-row: 1; padding: 3px 12px; border: 1px solid var(--line-2);
      border-radius: 999px; color: var(--ink-2); font-weight: 500; }
    :host(.integre) .eleve.absent { background: none; }
    :host(.integre) .eleve.absent > small:first-of-type { border-color: var(--bad); background: var(--bad-soft); color: var(--bad); }
    :host(.integre) .eleve small.origine { grid-column: 1; }
    .en-rattrapage { display: grid; gap: 4px; }
    .retirer { justify-self: start; font-size: 12.5px; padding: 2px 8px; }
  `,
})
export class PageDeLAppel implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  /** Dans la fiche du groupe (prototype, écran 3) : la séance choisie, sans l'en-tête de la page. */
  readonly seance = input<string | null>(null);
  readonly integre = input(false);
  /** Dans la fiche du groupe : « séance 2 du Mois 2 », et ce que chaque élève doit (point de couleur et texte). */
  readonly libelleDeLaSeance = input<string | null>(null);
  readonly paiements = input<Readonly<Record<string, { couleur: string | null; texte: string }>>>({});
  /** Dans la fiche du groupe : « Corriger l'appel » rouvre les cases d'un appel fait. */
  protected readonly correction = signal(false);
  protected readonly rattrapageOuvert = signal(false);
  protected readonly modifiable = computed(() => {
    const appel = this.appel();
    return !!appel && appel.ouvert && !appel.annulee && (!appel.appelFait || this.correction() || !this.integre());
  });

  protected paiementDe(eleveId: string): { couleur: string | null; texte: string } | null {
    return this.paiements()[eleveId] ?? null;
  }

  protected ouvrirLeRattrapage(): void {
    this.rattrapageOuvert.set(true);
    void this.chercherLesRattrapables();
  }

  /** « sam. 31/10 ». */
  protected jourCourt(date: string): string {
    return formatDate(date, 'EEE dd/MM', FRANCAIS);
  }

  /** « 10h44 ». */
  protected heureDuMoment(moment: string): string {
    return formatDate(moment, "H'h'mm", FRANCAIS);
  }
  private get seanceId(): string {
    return this.seance() ?? this.route.snapshot.paramMap.get('seance') ?? '';
  }

  protected readonly heure = heure;
  protected readonly appel = signal<AppelDeLaSeance | null>(null);
  protected readonly introuvable = signal(false);
  /** Les inscriptions marquées absentes sur la page, enregistrées ou pas. */
  protected readonly absents = signal<string[]>([]);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  protected readonly presents = computed(
    () => (this.appel()?.eleves.length ?? 0) + (this.appel()?.rattrapages?.length ?? 0) - this.absents().length,
  );
  /** Les absents tels qu'enregistrés, élèves en rattrapage compris : ceux à prévenir. */
  protected readonly absentsEnregistres = computed<Prevenable[]>(() => [
    ...(this.appel()?.eleves.filter((e) => e.absent) ?? []),
    ...(this.appel()?.rattrapages?.filter((e) => e.absent) ?? []),
  ]);

  /** Un appel coché puis laissé pour une autre page sans l'enregistrer se retrouve en revenant. */
  protected readonly brouillon = garderUnBrouillon(() => `appel-${this.seanceId}`, {
    lire: () => this.absents(),
    ecrire: (absents) => {
      // Seulement les élèves encore attendus à cette séance.
      const attendus = new Set([...(this.appel()?.eleves ?? []), ...(this.appel()?.rattrapages ?? [])].map((e) => e.inscriptionId));
      this.absents.set(absents.filter((id) => attendus.has(id)));
    },
    aGarder: () => {
      const enregistres = this.absentsEnregistres().map((e) => e.inscriptionId);
      const coches = this.absents();
      return coches.length !== enregistres.length || coches.some((id) => !enregistres.includes(id));
    },
  });

  // ----- Le rattrapage (SEA-12) -----

  protected readonly rattrapables = signal<EleveRattrapable[] | null>(null);
  private readonly inscriptionChoisie = signal('');
  protected readonly rattrapableChoisi = computed(() => this.rattrapables()?.find((e) => e.inscriptionId === this.inscriptionChoisie()) ?? null);
  protected readonly seanceRattrapee = signal('');
  protected readonly erreurDuRattrapage = signal<string | null>(null);

  protected async chercherLesRattrapables(): Promise<void> {
    this.erreurDuRattrapage.set(null);
    this.inscriptionChoisie.set('');
    this.seanceRattrapee.set('');
    try {
      this.rattrapables.set(await firstValueFrom(this.http.get<EleveRattrapable[]>(`/api/seances/${this.seanceId}/rattrapages/possibles`)));
    } catch (erreur) {
      this.erreurDuRattrapage.set(messageDErreur(erreur));
    }
  }

  protected choisirLeRattrapable(inscriptionId: string): void {
    this.inscriptionChoisie.set(inscriptionId);
    this.seanceRattrapee.set('');
  }

  protected async ajouterLeRattrapage(): Promise<void> {
    if (this.envoi()) return;
    if (!this.inscriptionChoisie() || !this.seanceRattrapee()) {
      this.erreurDuRattrapage.set("Choisissez l'élève et la séance qu'il rattrape");
      return;
    }
    await this.changerLeRattrapage(
      this.http.post<AppelDeLaSeance>(`/api/seances/${this.seanceId}/rattrapages`, {
        inscriptionId: this.inscriptionChoisie(),
        seanceRattrapeeId: this.seanceRattrapee(),
      }),
    );
  }

  protected async retirerLeRattrapage(rattrapageId: string): Promise<void> {
    if (this.envoi()) return;
    await this.changerLeRattrapage(this.http.delete<AppelDeLaSeance>(`/api/seances/${this.seanceId}/rattrapages/${rattrapageId}`));
  }

  /** L'appel revient à jour ; les absents marqués sur la page et pas encore enregistrés restent marqués. */
  private async changerLeRattrapage(requete: Observable<AppelDeLaSeance>): Promise<void> {
    this.envoi.set(true);
    this.erreurDuRattrapage.set(null);
    try {
      const marques = this.absents();
      const appel = await firstValueFrom(requete);
      const presentes = new Set([...appel.eleves, ...(appel.rattrapages ?? [])].map((e) => e.inscriptionId));
      this.appel.set(appel);
      this.absents.set(marques.filter((id) => presentes.has(id)));
      this.rattrapables.set(null);
    } catch (erreur) {
      this.erreurDuRattrapage.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** « 30/11 ». */
  protected court(date: string): string {
    return formatDate(date, 'dd/MM', FRANCAIS);
  }

  async ngOnInit(): Promise<void> {
    try {
      this.montrer(await firstValueFrom(this.http.get<AppelDeLaSeance>(`/api/seances/${this.seanceId}/appel`)));
      this.brouillon.reprendre();
    } catch (erreur) {
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) this.introuvable.set(true);
      else this.erreur.set(messageDErreur(erreur));
    }
  }

  private montrer(appel: AppelDeLaSeance): void {
    this.appel.set(appel);
    this.absents.set([...appel.eleves, ...(appel.rattrapages ?? [])].filter((e) => e.absent).map((e) => e.inscriptionId));
  }

  protected basculer(eleve: { inscriptionId: string }): void {
    this.succes.set(null);
    this.absents.update((absents) =>
      absents.includes(eleve.inscriptionId) ? absents.filter((id) => id !== eleve.inscriptionId) : [...absents, eleve.inscriptionId],
    );
  }

  protected async enregistrer(): Promise<void> {
    const appel = this.appel();
    if (this.envoi() || !appel) return;
    this.envoi.set(true);
    this.erreur.set(null);
    this.succes.set(null);
    try {
      const corrige = appel.appelFait;
      this.montrer(await firstValueFrom(this.http.put<AppelDeLaSeance>(`/api/seances/${appel.seanceId}/appel`, { absents: this.absents() })));
      this.brouillon.oublier();
      this.correction.set(false);
      this.succes.set(corrige ? 'Appel corrigé' : 'Appel enregistré');
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  // ----- Annuler, déplacer, remplacer (SEA-03, SEA-04) -----

  protected readonly action = signal<ActionSurLaSeance | null>(null);
  protected readonly motifSaisi = signal('');
  protected readonly dateSaisie = signal('');
  protected readonly debutSaisi = signal('');
  protected readonly finSaisie = signal('');
  protected readonly erreurDuCalendrier = signal<string | null>(null);
  protected readonly changement = signal<ChangementDuCalendrier | null>(null);

  protected readonly libelleDeLAction = computed(() => {
    switch (this.action()) {
      case 'annuler':
        return "Confirmer l'annulation";
      case 'deplacer':
        return 'Déplacer la séance';
      default:
        return 'Remplacer la séance';
    }
  });

  /** Les heures proposées sont celles de la séance. */
  protected ouvrir(action: ActionSurLaSeance): void {
    const appel = this.appel();
    this.action.set(action);
    this.motifSaisi.set('');
    this.dateSaisie.set('');
    this.debutSaisi.set(appel?.debut ?? '');
    this.finSaisie.set(appel?.fin ?? '');
    this.erreurDuCalendrier.set(null);
  }

  protected fermerLAction(): void {
    this.action.set(null);
    this.erreurDuCalendrier.set(null);
  }

  protected async confirmer(): Promise<void> {
    const appel = this.appel();
    const action = this.action();
    if (this.envoi() || !appel || !action) return;
    if (action === 'annuler' && !this.motifSaisi().trim()) {
      this.erreurDuCalendrier.set("Écrivez le motif de l'annulation");
      return;
    }
    if (action !== 'annuler' && (!this.dateSaisie() || !this.debutSaisi() || !this.finSaisie())) {
      this.erreurDuCalendrier.set('Choisissez la date et les heures');
      return;
    }

    this.envoi.set(true);
    this.erreurDuCalendrier.set(null);
    try {
      const adresse = `/api/seances/${appel.seanceId}/${action === 'annuler' ? 'annulation' : action === 'deplacer' ? 'deplacement' : 'remplacement'}`;
      const corps =
        action === 'annuler'
          ? { motif: this.motifSaisi().trim() }
          : { date: this.dateSaisie(), debut: this.debutSaisi(), fin: this.finSaisie() };
      this.changement.set(await firstValueFrom(this.http.post<ChangementDuCalendrier>(adresse, corps)));
      this.action.set(null);
      this.montrer(await firstValueFrom(this.http.get<AppelDeLaSeance>(`/api/seances/${appel.seanceId}/appel`)));
    } catch (erreur) {
      this.erreurDuCalendrier.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** Le message s'ouvre dans WhatsApp ; la page le marque, sans attendre. */
  protected async prevenir(eleve: Prevenable): Promise<void> {
    const appel = this.appel();
    if (!appel || eleve.prevenuLe) return;
    try {
      await firstValueFrom(this.http.post(`/api/seances/${appel.seanceId}/absences/${eleve.inscriptionId}/prevenu`, null));
      const maintenant = new Date().toISOString();
      this.appel.set({
        ...appel,
        eleves: appel.eleves.map((e) => (e.inscriptionId === eleve.inscriptionId ? { ...e, prevenuLe: maintenant } : e)),
        rattrapages: appel.rattrapages?.map((e) => (e.inscriptionId === eleve.inscriptionId ? { ...e, prevenuLe: maintenant } : e)),
      });
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** « samedi 27/11/2027 ». */
  protected jour(date: string): string {
    return formatDate(date, 'EEEE dd/MM/yyyy', FRANCAIS);
  }

  /** « Au parent · 98 401 220 », « À Adem · 22 345 678 ». */
  protected pourQui(message: MessageWhatsApp): string {
    const qui = message.destinataire === 'Parent' ? 'Au parent' : `À ${message.destinataire}`;
    return `${qui} · ${message.telephone}`;
  }
}
