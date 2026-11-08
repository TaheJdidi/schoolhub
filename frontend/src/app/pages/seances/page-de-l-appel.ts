import { formatDate, NgTemplateOutlet } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, Observable } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderUnBrouillon } from '../../socle/brouillons';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { EleveAEncaisser, RecusDuGroupe } from '../caisse/recu';
import { heure } from '../groupes/groupe';
import { MessageWhatsApp } from '../inscriptions/inscription';
import { AppelDeLaSeance, ChangementDuCalendrier, EleveALAppel, EleveEnRattrapage, EleveRattrapable } from './appel';
import { MessagesDuChangement } from './messages-du-changement';

/** Un absent à prévenir : un élève du groupe ou en rattrapage (SEA-11, SEA-12). */
type Prevenable = EleveALAppel | EleveEnRattrapage;

/** Ce que la secrétaire fait de la séance (SEA-03, SEA-04). */
type ActionSurLaSeance = 'annuler' | 'deplacer' | 'remplacer';

/** Les élèves marqués sur la page : absents, et présents ; ceux qui ne sont dans aucune liste sont à marquer. */
type MarquesDeLAppel = { absents: string[]; presents: string[] };

/** Les deux listes ont les mêmes identifiants, dans n'importe quel ordre. */
function memes(a: string[], b: string[]): boolean {
  return a.length === b.length && a.every((id) => b.includes(id));
}

/**
 * SEA-07, SEA-10 et SEA-11 : l'appel d'une séance. Aucun élève n'est marqué d'avance : P le marque présent, A absent ;
 * l'appel s'enregistre quand chacun est marqué. « Enregistrer l'appel » fait passer la séance à « appel fait » ; ensuite, « Corriger l'appel » reste
 * possible à tout moment. Après l'enregistrement, « Prévenir les parents des absents » liste les messages WhatsApp prêts ;
 * un message ouvert est marqué, pour ne pas l'envoyer deux fois.
 */
@Component({
  selector: 'sh-page-de-l-appel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NgTemplateOutlet, MessagesDuChangement, BrouillonRepris],
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
          <h1 class="h1">Appel · {{ appel.groupe.nom }}</h1>
          <div class="etiquettes">
            <span class="etiquette">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><rect x="3" y="5" width="18" height="16" rx="2" /><path d="M3 10h18M8 3v4M16 3v4" /></svg>
              {{ jourCourt(appel.date) }}
            </span>
            <span class="etiquette matiere">{{ appel.groupe.matiere }}</span>
            <span class="etiquette">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="12" cy="8" r="4" /><path d="M4 21c0-4 4-6 8-6s8 2 8 6" /></svg>
              {{ appel.professeur }}
            </span>
            <span class="etiquette">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></svg>
              {{ heure(appel.debut) }} → {{ heure(appel.fin) }}
            </span>
          </div>
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
                  <p class="aide">Marquez chaque élève : P présent, A absent.</p>
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
            @if (colonneDePaiement() && appel.eleves.length > 0) {
              <div class="ligne-appel titres-des-colonnes" aria-hidden="true">
                <span class="point vide-point"></span>
                <span class="deux">Élève</span>
                <span class="col-presence">Présence</span>
                <span class="col-paiement">Paiement</span>
              </div>
            }
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
                <span class="col-presence">
                  @if (modifiable()) {
                    <ng-container *ngTemplateOutlet="ronds; context: { $implicit: eleve }" />
                  } @else if (appel.appelFait) {
                    @if (absents().includes(eleve.inscriptionId)) {
                      <span class="pill bad">Absent</span>
                    } @else {
                      <span class="present">Présent</span>
                    }
                  }
                </span>
                @if (colonneDePaiement()) {
                  <span class="col-paiement">
                    @if (aPayerDe(eleve.eleveId); as du) {
                      <label class="case-paiement" [class.coche]="aEncaisser().includes(eleve.eleveId)">
                        <input type="checkbox" [checked]="aEncaisser().includes(eleve.eleveId)" [disabled]="envoiDuPaiement()"
                               [attr.aria-label]="'Payer ' + montant(du.total) + ' TND pour ' + eleve.prenom + ' ' + eleve.nom"
                               (change)="cocherLePaiement(eleve.eleveId)" />
                        <span class="num">{{ montant(du.total) }}</span><small> TND</small>
                      </label>
                    } @else if (paiementDe(eleve.eleveId)?.couleur === 'AJour') {
                      <span class="a-jour">✓ Payé</span>
                    }
                  </span>
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
                  <span class="col-presence">
                    @if (modifiable()) {
                      <ng-container *ngTemplateOutlet="ronds; context: { $implicit: eleve }" />
                    } @else if (appel.appelFait) {
                      <span [class]="absents().includes(eleve.inscriptionId) ? 'pill bad' : 'present'">{{ absents().includes(eleve.inscriptionId) ? 'Absent' : 'Présent' }}</span>
                    }
                  </span>
                  @if (colonneDePaiement()) {
                    <span class="col-paiement"><small class="ailleurs">dans son groupe</small></span>
                  }
                </div>
              }
            }
            @if (aEncaisser().length > 0) {
              <div class="barre-paiement">
                <span><b>{{ aEncaisser().length }}</b> élève{{ aEncaisser().length > 1 ? 's' : '' }} · <b class="num">{{ montant(totalAEncaisser()) }}</b> TND</span>
                <button type="button" class="btn primary" [disabled]="envoiDuPaiement()" (click)="payerLesCoches()">Payer les cochés</button>
              </div>
            }
            @if (recusCrees(); as recus) {
              <p class="note ok paiement-fait" role="status">
                {{ recus.length }} reçu{{ recus.length > 1 ? 's' : '' }} créé{{ recus.length > 1 ? 's' : '' }} · {{ montant(totalDesRecus()) }} TND en espèces ·
                <a [href]="impressionDesRecus()" target="_blank" rel="noopener">Imprimer</a>
              </p>
            }
            @if (erreurDuPaiement(); as erreur) {
              <p class="note bad paiement-fait" role="alert">{{ erreur }}</p>
            }
            @if (appel.ouvert) {
              <div class="pied-appel">
                <span class="compte">{{ compte() }}</span>
                <span class="boutons">
                  @if (modifiable() && aMarquer() > 0) {
                    <button type="button" class="btn ghost" [disabled]="envoi()" (click)="tousPresents()">Tous présents</button>
                  }
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
              <span class="compte">{{ compte() }}</span>
            }
          </div>
          <sh-brouillon-repris [brouillon]="brouillon" texte="Vous reprenez l'appel que vous n'aviez pas enregistré." action="Annuler les changements" />
          @if (appel.annulee) {
            <p class="note warn" role="status">Cette séance est annulée : {{ appel.motifDAnnulation }}.</p>
          } @else if (!appel.ouvert) {
            <p class="note" role="status">L'appel s'ouvre le jour de la séance.</p>
          } @else {
            <p class="aide">Marquez chaque élève : P présent, A absent.</p>
            @if (aMarquer() > 0) {
              <div><button type="button" class="btn ghost" [disabled]="envoi()" (click)="tousPresents()">Tous présents</button></div>
            }
          }
          <div class="eleves">
            @for (eleve of appel.eleves; track eleve.inscriptionId) {
              <div class="eleve" [class.absent]="etatDe(eleve) === 'absent'" [class.present]="etatDe(eleve) === 'present'">
                <span class="deux">
                  <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                  <small class="etat">{{ libelleDeLEtat(eleve) }}</small>
                  @if (eleve.rattrapeeLe; as le) {
                    <small class="origine">Rattrapée le {{ court(le) }}</small>
                  }
                </span>
                @if (appel.ouvert && !appel.annulee) {
                  <ng-container *ngTemplateOutlet="ronds; context: { $implicit: eleve }" />
                }
              </div>
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
                  <div class="eleve" [class.absent]="etatDe(eleve) === 'absent'" [class.present]="etatDe(eleve) === 'present'">
                    <span class="deux">
                      <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                      <small class="etat">{{ libelleDeLEtat(eleve) }}</small>
                      <small class="origine">Paie dans son groupe d'origine · {{ eleve.groupeDOrigine }}, rattrape le {{ court(eleve.seanceRattrapee) }}</small>
                    </span>
                    @if (appel.ouvert && !appel.annulee) {
                      <ng-container *ngTemplateOutlet="ronds; context: { $implicit: eleve }" />
                    }
                  </div>
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
  
    <ng-template #ronds let-eleve>
      <span class="ronds" role="group" [attr.aria-label]="'Présence de ' + eleve.prenom + ' ' + eleve.nom">
        <button type="button" class="rond p" title="Présent" aria-label="Présent" [class.choisi]="etatDe(eleve) === 'present'"
                [attr.aria-pressed]="etatDe(eleve) === 'present'" [disabled]="envoi()" (click)="marquer(eleve, 'present')">P</button>
        <button type="button" class="rond a" title="Absent" aria-label="Absent" [class.choisi]="etatDe(eleve) === 'absent'"
                [attr.aria-pressed]="etatDe(eleve) === 'absent'" [disabled]="envoi()" (click)="marquer(eleve, 'absent')">A</button>
      </span>
    </ng-template>
  `,
  styleUrls: ['../eleves/eleves.css', '../inscriptions/inscriptions.css'],
  styles: `
    /* L'en-tête (style A) : un titre court, puis la matière, le professeur et l'horaire en étiquettes. */
    .etiquettes { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 8px; }
    .etiquette { display: inline-flex; align-items: center; gap: 6px; padding: 3px 11px; border: 1px solid var(--line); border-radius: 999px; background: var(--surface); color: var(--ink-2); font-size: 13px; }
    .etiquette.matiere { border-color: transparent; background: var(--accent-soft); color: var(--accent-ink); font-weight: 500; }
    .etiquette svg { width: 14px; height: 14px; }
    .compte { color: var(--ink-2); font-size: 13px; }
    .eleves { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: 8px; }
    .eleve { display: grid; gap: 2px; text-align: left; padding: 10px 12px; border: 1px solid var(--line); border-radius: 10px;
      background: var(--surface); color: var(--ink); font: inherit; cursor: pointer; min-height: 52px; }
    .eleve { display: flex !important; align-items: center; justify-content: space-between; gap: 10px; }
    .eleve .deux { display: grid; gap: 2px; min-width: 0; }
    .eleve small.etat { color: var(--warn); font-size: 12.5px; }
    .eleve.present small.etat { color: var(--ok); }
    .eleve.absent { border-color: var(--bad); background: var(--bad-soft); }
    .eleve.absent small.etat { color: var(--bad); font-weight: 600; }
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
    /* Les ronds P et A (style C) : rien de coloré tant que l'élève n'est pas marqué. */
    .ronds { display: inline-flex; flex: none; gap: 8px; }
    .rond { display: inline-flex; align-items: center; justify-content: center; width: 36px; height: 36px; padding: 0; border: 1px solid var(--line-2); border-radius: 50%; background: var(--surface); color: var(--ink-2); font: inherit; font-size: 14px; font-weight: 600; cursor: pointer; }
    .rond:hover:not(:disabled) { background: var(--sunk); }
    .rond.p.choisi { border-color: var(--ok); background: var(--ok); color: #fff; }
    .rond.a.choisi { border-color: var(--bad); background: var(--bad); color: #fff; }
    .rond:disabled { cursor: default; opacity: 0.6; }
    .present { color: var(--ink-3); font-size: 13.5px; }
    .bande { margin: 0; padding: 8px 18px; background: var(--sunk); border-top: 1px solid var(--line); color: var(--ink-2); font-size: 12px; font-weight: 600; letter-spacing: 0.04em; text-transform: uppercase; }
    .pied-appel { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px; padding: 12px 18px; border-top: 1px solid var(--line); }
    .pied-appel .boutons { display: flex; flex-wrap: wrap; gap: 8px; }
    .appel-integre > .note { margin: 0 18px 14px; }
    /* Les colonnes « Présence » et « Paiement », alignées d'une ligne à l'autre. */
    .col-presence { display: inline-flex; justify-content: flex-end; flex: none; min-width: 84px; }
    .col-paiement { display: inline-flex; justify-content: flex-end; flex: none; width: 132px; }
    .titres-des-colonnes { padding-top: 8px; padding-bottom: 8px; background: var(--sunk); color: var(--ink-2); font-size: 12px; font-weight: 600; }
    .titres-des-colonnes .col-presence, .titres-des-colonnes .col-paiement { justify-content: flex-end; }
    .vide-point { visibility: hidden; }
    .case-paiement { display: inline-flex; align-items: center; gap: 6px; padding: 5px 10px; border: 1px solid var(--line-2); border-radius: 999px; font-size: 13.5px; cursor: pointer; white-space: nowrap; }
    .case-paiement input { width: 16px; height: 16px; margin: 0; accent-color: var(--accent); }
    .case-paiement small { color: var(--ink-2); font-size: 12px; }
    .case-paiement.coche { border-color: var(--accent); background: var(--accent-soft); color: var(--accent-ink); }
    .a-jour { color: var(--ok); font-size: 13px; font-weight: 500; }
    .ailleurs { color: var(--ink-3); font-size: 12px; }
    .barre-paiement { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px; margin: 0; padding: 10px 18px; border-top: 1px solid var(--line); background: var(--accent-soft); color: var(--accent-ink); }
    .appel-integre > .paiement-fait { margin: 10px 18px; }
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
  /** Dans la fiche du groupe : ce que chaque élève doit à ce jour ; la colonne « Paiement » les fait payer d'un coup. */
  readonly groupeId = input<string | null>(null);
  readonly aPayer = input<readonly EleveAEncaisser[]>([]);
  /** Des reçus viennent d'être créés : la fiche du groupe recharge ce que chacun doit. */
  readonly paye = output<void>();

  protected readonly montant = ecrireUnMontant;
  protected readonly colonneDePaiement = computed(() => this.integre() && !!this.groupeId());
  /** Les élèves cochés dans la colonne « Paiement ». */
  protected readonly aEncaisser = signal<string[]>([]);
  protected readonly envoiDuPaiement = signal(false);
  protected readonly erreurDuPaiement = signal<string | null>(null);
  protected readonly recusCrees = signal<RecusDuGroupe['recus'] | null>(null);
  private readonly cochables = computed(() => this.aPayer().filter((e) => this.aEncaisser().includes(e.eleveId)));
  protected readonly totalAEncaisser = computed(() => this.cochables().reduce((total, e) => total + e.total, 0));
  protected readonly totalDesRecus = computed(() => (this.recusCrees() ?? []).reduce((total, r) => total + r.total, 0));

  protected aPayerDe(eleveId: string): EleveAEncaisser | null {
    return this.aPayer().find((e) => e.eleveId === eleveId) ?? null;
  }

  protected cocherLePaiement(eleveId: string): void {
    this.erreurDuPaiement.set(null);
    this.recusCrees.set(null);
    this.aEncaisser.update((ids) => (ids.includes(eleveId) ? ids.filter((id) => id !== eleveId) : [...ids, eleveId]));
  }

  /** Un reçu en espèces par élève coché, avec tout ce qu'il doit à ce jour, comme « Paiement du groupe ». */
  protected async payerLesCoches(): Promise<void> {
    const choisis = this.cochables();
    const groupeId = this.groupeId();
    if (this.envoiDuPaiement() || choisis.length === 0 || !groupeId) return;
    this.envoiDuPaiement.set(true);
    this.erreurDuPaiement.set(null);
    try {
      const reponse = await firstValueFrom(
        this.http.post<RecusDuGroupe>(`/api/groupes/${groupeId}/encaissement`, {
          eleves: choisis.map((e) => ({ eleveId: e.eleveId, sommes: e.sommes.map((s) => ({ inscriptionId: s.inscriptionId, cle: s.cle })) })),
        }),
      );
      this.recusCrees.set(reponse.recus);
      this.aEncaisser.set([]);
      this.paye.emit();
    } catch (erreur) {
      this.erreurDuPaiement.set(messageDErreur(erreur));
    } finally {
      this.envoiDuPaiement.set(false);
    }
  }

  /** La page sans le menu, un reçu A5 par page. */
  protected impressionDesRecus(): string {
    return `/recus/impression?ids=${(this.recusCrees() ?? []).map((r) => r.id).join(',')}`;
  }
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
  /** Les inscriptions marquées absentes, et celles marquées présentes, sur la page, enregistrées ou pas ; les autres sont à marquer. */
  protected readonly absents = signal<string[]>([]);
  protected readonly presentsMarques = signal<string[]>([]);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  /** Toutes les inscriptions attendues à la séance, élèves en rattrapage compris. */
  private readonly attendus = computed(() => [...(this.appel()?.eleves ?? []), ...(this.appel()?.rattrapages ?? [])].map((e) => e.inscriptionId));
  protected readonly presents = computed(() => this.presentsMarques().length);
  protected readonly aMarquer = computed(() => this.attendus().length - this.presents() - this.absents().length);
  /** « 1 présent · 1 absent · 2 à marquer ». */
  protected readonly compte = computed(() => {
    const presents = this.presents();
    const absents = this.absents().length;
    const reste = this.aMarquer();
    return `${presents} présent${presents > 1 ? 's' : ''} · ${absents} absent${absents > 1 ? 's' : ''}${reste > 0 ? ` · ${reste} à marquer` : ''}`;
  });
  /** Les absents tels qu'enregistrés, élèves en rattrapage compris : ceux à prévenir. */
  protected readonly absentsEnregistres = computed<Prevenable[]>(() => [
    ...(this.appel()?.eleves.filter((e) => e.absent) ?? []),
    ...(this.appel()?.rattrapages?.filter((e) => e.absent) ?? []),
  ]);

  /** Un appel coché puis laissé pour une autre page sans l'enregistrer se retrouve en revenant. */
  protected readonly brouillon = garderUnBrouillon<MarquesDeLAppel | string[]>(() => `appel-${this.seanceId}`, {
    lire: () => ({ absents: this.absents(), presents: this.presentsMarques() }),
    // Un ancien brouillon ne gardait que les absents.
    ecrire: (marques) => this.garderLesMarques(Array.isArray(marques) ? { absents: marques, presents: [] } : marques),
    aGarder: () => {
      const enregistre = this.marquesEnregistrees();
      return !memes(enregistre.absents, this.absents()) || !memes(enregistre.presents, this.presentsMarques());
    },
  });

  /** Ce que l'appel enregistré marque : un appel fait a ses absents, et tous les autres présents ; sinon, rien. */
  private marquesEnregistrees(): MarquesDeLAppel {
    const appel = this.appel();
    const tous = appel ? [...appel.eleves, ...(appel.rattrapages ?? [])] : [];
    return {
      absents: tous.filter((e) => e.absent).map((e) => e.inscriptionId),
      presents: appel?.appelFait ? tous.filter((e) => !e.absent).map((e) => e.inscriptionId) : [],
    };
  }

  /** Seulement les élèves encore attendus à cette séance. */
  private garderLesMarques(marques: MarquesDeLAppel): void {
    const attendus = new Set(this.attendus());
    this.absents.set(marques.absents.filter((id) => attendus.has(id)));
    this.presentsMarques.set(marques.presents.filter((id) => attendus.has(id) && !marques.absents.includes(id)));
  }

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
      const marques = { absents: this.absents(), presents: this.presentsMarques() };
      this.appel.set(await firstValueFrom(requete));
      this.garderLesMarques(marques);
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
    this.garderLesMarques(this.marquesEnregistrees());
  }

  protected etatDe(eleve: { inscriptionId: string }): 'present' | 'absent' | null {
    if (this.absents().includes(eleve.inscriptionId)) return 'absent';
    return this.presentsMarques().includes(eleve.inscriptionId) ? 'present' : null;
  }

  protected libelleDeLEtat(eleve: { inscriptionId: string }): string {
    const etat = this.etatDe(eleve);
    return etat === 'present' ? 'Présent' : etat === 'absent' ? 'Absent' : 'À marquer';
  }

  /** P ou A ; toucher de nouveau le même rond enlève la marque. */
  protected marquer(eleve: { inscriptionId: string }, etat: 'present' | 'absent'): void {
    this.succes.set(null);
    this.erreur.set(null);
    const id = eleve.inscriptionId;
    const dejaLa = this.etatDe(eleve) === etat;
    this.absents.update((ids) => ids.filter((x) => x !== id));
    this.presentsMarques.update((ids) => ids.filter((x) => x !== id));
    if (dejaLa) return;
    (etat === 'absent' ? this.absents : this.presentsMarques).update((ids) => [...ids, id]);
  }

  /** Marque présents tous les élèves pas encore marqués. */
  protected tousPresents(): void {
    this.succes.set(null);
    this.erreur.set(null);
    const absents = this.absents();
    this.presentsMarques.set(this.attendus().filter((id) => !absents.includes(id)));
  }

  protected async enregistrer(): Promise<void> {
    const appel = this.appel();
    if (this.envoi() || !appel) return;
    const reste = this.aMarquer();
    if (reste > 0) {
      this.succes.set(null);
      this.erreur.set(`Marquez encore ${reste} élève${reste > 1 ? 's' : ''} : présent (P) ou absent (A)`);
      return;
    }
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
