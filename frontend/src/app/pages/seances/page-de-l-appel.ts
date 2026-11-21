import { formatDate, NgTemplateOutlet } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, Observable } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderUnBrouillon } from '../../socle/brouillons';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant, lireUnNombre } from '../../socle/montants';
import { EleveAEncaisser, ModeDePaiement, RecusDuGroupe, resteAPayer } from '../caisse/recu';
import { heure } from '../groupes/groupe';
import { InscriptionDeLEleve, MessageWhatsApp, modeCourt } from '../inscriptions/inscription';
import { ActionsDeListe } from '../../socle/actions-de-liste';
import { Icone } from '../../socle/icone';
import { BoutonDeRappel, enRetard } from '../caisse/paiement';
import { AppelDeLaSeance, ChangementDuCalendrier, EleveALAppel, EleveEnRattrapage, EleveRattrapable } from './appel';
import { MessagesDuChangement } from './messages-du-changement';

/** Un absent à prévenir : un élève du groupe ou venu d'un autre groupe (SEA-11, SEA-12). */
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
  imports: [RouterLink, NgTemplateOutlet, MessagesDuChangement, BrouillonRepris, ActionsDeListe, BoutonDeRappel, Icone],
  host: { '[class.integre]': 'integre()', '(document:keydown.escape)': 'fermerAuClavier()' },
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
              <span class="etat-et-outils">
                @if (appel.appelFait) {
                  <span class="pill ok">✓ Appel fait</span>
                } @else if (appel.ouvert) {
                  <span class="pill warn">Appel à faire</span>
                } @else {
                  <span class="pill muted">À venir</span>
                }
                <!-- Ce que faisait le bloc « Les élèves » : ajouter un élève, imprimer, exporter. -->
                @if (inscriptions(); as inscriptions) {
                  @if (ajoutPossible()) {
                    <a class="icone-seule" [routerLink]="['/professeurs/groupes', appel.groupe.id, 'inscription']"
                       aria-label="Ajouter un élève" title="Ajouter un élève"><sh-icone nom="user-plus" /></a>
                  }
                  <sh-actions-de-liste [compact]="true" [titre]="appel.groupe.nom + ' · élèves'" [nombre]="inscriptions.length"
                                       [exporter]="'/api/exports/groupes/' + appel.groupe.id" />
                  <!-- SEA-08 : la feuille d'appel, à côté du tableau qu'elle imprime. -->
                  <a class="feuille-d-appel" [routerLink]="['/professeurs/groupes', appel.groupe.id, 'feuille-d-appel']">
                    <sh-icone nom="file-text" /> Feuille d'appel
                  </a>
                }
              </span>
            </div>
            <sh-brouillon-repris [brouillon]="brouillon" texte="Vous reprenez l'appel que vous n'aviez pas enregistré." action="Annuler les changements" />
            @if (colonneDePaiement() && appel.eleves.length > 0) {
              <div class="ligne-appel titres-des-colonnes" aria-hidden="true">
                <span class="point vide-point"></span>
                <span class="deux">Élève</span>
                <span class="col-presence">Présence</span>
                <span class="col-paiement">Paiement</span>
                <span class="col-mode">Mode</span>
              </div>
            }
            @for (eleve of appel.eleves; track eleve.inscriptionId) {
              <div class="ligne-appel">
                <span class="point" [class]="'point ' + (paiementDe(eleve.eleveId)?.couleur ?? '')" aria-hidden="true"></span>
                <span class="deux">
                  @if (inscriptions()) {
                    <span class="nom-et-rappel">
                      <a class="nom-de-l-eleve" [routerLink]="['/eleves', eleve.eleveId]" [attr.title]="inscriptionEnBref(eleve.inscriptionId)"><b>{{ eleve.prenom }} {{ eleve.nom }}</b></a>
                      <!-- CAI-08 : un élève orange ou rouge reçoit un rappel de paiement. -->
                      @if (enRetard(inscriptionDe(eleve.inscriptionId)?.couleur)) {
                        <sh-bouton-de-rappel [compact]="true" [eleveId]="eleve.eleveId" [pour]="eleve.prenom + ' ' + eleve.nom" />
                      }
                    </span>
                  } @else {
                    <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                  }
                  @if (paiementDe(eleve.eleveId); as paiement) {
                    <small>{{ paiement.texte }}</small>
                  }
                  @if (eleve.rattrapeeLe; as le) {
                    <span class="venu-ailleurs">Venu le {{ court(le) }} avec un autre groupe</span>
                  }
                  @if (colonneDePaiement() && aPayerDe(eleve.eleveId) && partieDe(eleve.eleveId) === null) {
                    <button type="button" class="lien payer-une-partie" [disabled]="envoiDuPaiement()" (click)="ouvrirUnePartie(eleve.eleveId)">
                      Payer une partie
                    </button>
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
                        <span class="num">{{ montant(aPayerPour(du)) }}</span><small> TND</small>
                      </label>
                    } @else if (paiementDe(eleve.eleveId)?.couleur === 'AJour') {
                      <span class="a-jour">✓ Payé</span>
                    }
                  </span>
                  <span class="col-mode">
                    @if (aPayerDe(eleve.eleveId)) {
                      <select class="inp mode" [value]="modeDe(eleve.eleveId)" [disabled]="envoiDuPaiement()"
                              [attr.aria-label]="'Mode de paiement de ' + eleve.prenom + ' ' + eleve.nom"
                              (change)="choisirLeMode(eleve.eleveId, $event)">
                        <option value="Especes">Espèces</option>
                        <option value="Cheque">Chèque</option>
                      </select>
                    }
                  </span>
                }
              </div>
              <!-- Payer une partie : ce que l'élève donne, sur ce qu'il doit ; le reste reste à payer. -->
              @if (aPayerDe(eleve.eleveId); as du) {
                @if (partieDe(eleve.eleveId) !== null) {
                  <div class="ligne-partie">
                    <label>
                      Il donne
                      <input class="inp montant-partiel" inputmode="decimal" [value]="partieDe(eleve.eleveId)" [disabled]="envoiDuPaiement()"
                             [attr.aria-label]="'Montant donné par ' + eleve.prenom + ' ' + eleve.nom"
                             (input)="ecrireUnePartie(eleve.eleveId, $event)" />
                      TND sur {{ montant(du.total) }}
                    </label>
                    @if (erreurDeLaPartie(du); as erreur) {
                      <span class="erreur-partie">{{ erreur }}</span>
                    } @else {
                      <span class="reste">· reste {{ montant(du.total - aPayerPour(du)) }} TND</span>
                    }
                    <button type="button" class="lien" (click)="fermerUnePartie(eleve.eleveId)">Annuler</button>
                  </div>
                }
              }
            } @empty {
              <p class="vide">Aucun élève attendu à cette séance.</p>
            }
            @for (inscription of pasACetteSeance(); track inscription.id) {
              <div class="ligne-appel pas-a-cette-seance">
                <span class="point" [class]="'point ' + (inscription.couleur ?? '')" aria-hidden="true"></span>
                <span class="deux">
                  <span class="nom-et-rappel">
                    <a class="nom-de-l-eleve" [routerLink]="['/eleves', inscription.eleve.id]" [attr.title]="modeEtDebut(inscription)"><b>{{ inscription.eleve.prenom }} {{ inscription.eleve.nom }}</b></a>
                    @if (enRetard(inscription.couleur)) {
                      <sh-bouton-de-rappel [compact]="true" [eleveId]="inscription.eleve.id" [pour]="inscription.eleve.prenom + ' ' + inscription.eleve.nom" />
                    }
                  </span>
                  <small>{{ pourquoiPasLa(inscription) }}</small>
                </span>
                <span class="col-presence"><span class="tiret">—</span></span>
                @if (colonneDePaiement()) {
                  <span class="col-paiement"><span class="tiret">—</span></span>
                  <span class="col-mode"></span>
                }
              </div>
            }
            @if (appel.rattrapages?.length) {
              <p class="bande">Venus d'un autre groupe</p>
              @for (eleve of appel.rattrapages; track eleve.rattrapageId) {
                <div class="ligne-appel">
                  <span class="point" aria-hidden="true"></span>
                  <span class="deux">
                    <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                    <small>Vient du {{ eleve.groupeDOrigine }}, à la place du {{ court(eleve.seanceRattrapee) }} · paie dans son groupe</small>
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
                    <span class="col-mode"></span>
                  }
                </div>
              }
            }
            @if (aEncaisser().length > 0) {
              <div class="barre-paiement">
                <span><b>{{ aEncaisser().length }}</b> élève{{ aEncaisser().length > 1 ? 's' : '' }} · <b class="num">{{ montant(totalAEncaisser()) }}</b> TND{{ chequesCoches() > 0 ? ' · dont ' + chequesCoches() + ' par chèque' : '' }}</span>
                <button type="button" class="btn primary" [disabled]="envoiDuPaiement() || partieInvalide()" (click)="payerLesCoches()">Payer les cochés</button>
              </div>
            }
            @if (recusCrees(); as recus) {
              <p class="note ok paiement-fait" role="status">
                {{ recus.length }} reçu{{ recus.length > 1 ? 's' : '' }} créé{{ recus.length > 1 ? 's' : '' }} · {{ montant(totalDesRecus()) }} TND · {{ detailDesModes() }} ·
                @if (resteDesRecus(); as reste) {
                  reste à payer {{ montant(reste) }} TND ·
                }
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
                    <button type="button" class="btn ghost" (click)="ouvrirLeRattrapage()">+ Venus d'un autre groupe</button>
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
                    <span class="venu-ailleurs">Venu le {{ court(le) }} avec un autre groupe</span>
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
          <!-- SEA-12 : les élèves venus d'un autre groupe, à part. -->
          @if (appel.rattrapages?.length) {
            <h3 class="tag">Venus d'un autre groupe</h3>
            <div class="eleves">
              @for (eleve of appel.rattrapages; track eleve.rattrapageId) {
                <div class="en-rattrapage">
                  <div class="eleve" [class.absent]="etatDe(eleve) === 'absent'" [class.present]="etatDe(eleve) === 'present'">
                    <span class="deux">
                      <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                      <small class="etat">{{ libelleDeLEtat(eleve) }}</small>
                      <small class="origine">Paie dans son groupe d'origine · {{ eleve.groupeDOrigine }}, à la place du {{ court(eleve.seanceRattrapee) }}</small>
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

        <!-- SEA-12 : « Venus d'un autre groupe » : on touche leur groupe, puis on coche les élèves venus. -->
        @if (!appel.annulee && (!integre() || rattrapageOuvert())) {
          <section class="card carte">
            <h2 class="titre">Venus d'un autre groupe</h2>
            @if (rattrapables() === null) {
              <div class="actions debut">
                <button type="button" class="btn" (click)="chercherLesRattrapables()">Ajouter des élèves venus d'un autre groupe</button>
              </div>
            } @else {
              <p class="aide">Des élèves d'un autre groupe du même professeur, de la même matière et du même niveau. Ils paient dans leur groupe.</p>
              @if (groupesVenus().length === 0) {
                <p class="vide">Aucun autre groupe de ce professeur, de cette matière et de ce niveau.</p>
              } @else {
                <div class="groupes-venus" role="group" aria-label="Groupe d'origine">
                  @for (groupe of groupesVenus(); track groupe.id) {
                    <button type="button" class="btn puce" [class.choisi]="groupeVenu() === groupe.id" [attr.aria-pressed]="groupeVenu() === groupe.id"
                            (click)="choisirLeGroupeVenu(groupe.id)">
                      {{ groupe.nom }}
                    </button>
                  }
                </div>
                @if (groupeVenu()) {
                  @if (seancesDuGroupeVenu().length === 0) {
                    <p class="vide">Aucune séance de ce groupe à remplacer.</p>
                  } @else {
                    <p class="a-la-place">
                      À la place de la séance du <b>{{ jour(seanceRemplacee()) }}</b>
                      @if (!changerLaSeance()) {
                        · <button type="button" class="lien" (click)="changerLaSeance.set(true)">changer</button>
                      }
                    </p>
                    @if (changerLaSeance()) {
                      <label class="f">
                        Séance de leur groupe qu'ils remplacent
                        <select class="inp" aria-label="Séance remplacée" (change)="choisirLaSeanceRemplacee($any($event.target).value)">
                          @for (seance of seancesDuGroupeVenu(); track seance.id) {
                            <option [value]="seance.id" [selected]="seance.id === seanceRattrapee()">{{ jour(seance.date) }} · {{ heure(seance.debut) }}</option>
                          }
                        </select>
                      </label>
                    }
                    <div class="eleves-venus">
                      @for (eleve of elevesDuGroupeVenu(); track eleve.inscriptionId) {
                        <label class="eleve-venu" [class.coche]="venus().includes(eleve.inscriptionId)" [class.impossible]="!peutRemplacer(eleve)">
                          <input type="checkbox" [checked]="venus().includes(eleve.inscriptionId)" [disabled]="!peutRemplacer(eleve) || envoi()"
                                 [attr.aria-label]="eleve.prenom + ' ' + eleve.nom + ' est venu'"
                                 (change)="cocherLeVenu(eleve.inscriptionId)" />
                          <span>{{ eleve.prenom }} {{ eleve.nom }}</span>
                          @if (!peutRemplacer(eleve)) {
                            <small>cette séance est déjà remplacée</small>
                          }
                        </label>
                      }
                    </div>
                  }
                }
              }
              <div class="actions">
                <button type="button" class="btn" [disabled]="envoi()" (click)="fermerLesVenus()">Retour</button>
                @if (groupeVenu()) {
                  <button type="button" class="btn primary" [disabled]="envoi() || venus().length === 0" (click)="ajouterLeRattrapage()">
                    Ajouter{{ venus().length ? ' ' + venus().length : '' }} élève{{ venus().length > 1 ? 's' : '' }}
                  </button>
                }
              </div>
            }
            @if (erreurDuRattrapage(); as erreur) {
              <p class="note bad" role="alert">{{ erreur }}</p>
            }
          </section>
        }

        <!-- SEA-03 et SEA-04 : annuler ou déplacer la séance ; remplacer une séance annulée. SEA-05 : ajouter une séance. -->
        @let changeable = !appel.appelFait && !(appel.annulee && appel.nature === 'Supplementaire');
        <!-- Sur la fiche du groupe, les boutons sont sur la puce de la séance (la bande) : seul le formulaire s'affiche, en haut. -->
        @if (integre() ? action() !== null : changeable) {
          @if (integre()) {
            <div class="fond-de-fenetre" (click)="fermerLAction()"></div>
          }
          <section class="card carte" [class.fenetre-de-seance]="integre()" [attr.role]="integre() ? 'dialog' : null"
                   [attr.aria-modal]="integre() ? 'true' : null" [attr.aria-labelledby]="integre() ? 'titre-de-la-fenetre-de-seance' : null">
            @if (integre()) {
              <div class="haut-de-fenetre">
                <h2 class="titre" id="titre-de-la-fenetre-de-seance">{{ titreDuFormulaire() }} · {{ jourCourt(appel.date) }}</h2>
                <button type="button" class="fermer-la-fenetre" aria-label="Fermer" (click)="fermerLAction()">✕</button>
              </div>
            } @else {
              <h2 class="titre">Cette séance</h2>
            }
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
          <sh-messages-du-changement [class.en-haut]="integre()" [changement]="changement" />
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
    .col-mode { display: inline-flex; justify-content: flex-end; flex: none; width: 104px; }
    .col-mode .mode { width: 100%; padding: 5px 8px; font-size: 13px; }
    .titres-des-colonnes { padding-top: 8px; padding-bottom: 8px; background: var(--sunk); color: var(--ink-2); font-size: 12px; font-weight: 600; }
    .titres-des-colonnes .col-presence, .titres-des-colonnes .col-paiement, .titres-des-colonnes .col-mode { justify-content: flex-end; }
    /* Sur un téléphone, le nom prend toute la ligne ; présence, paiement et mode passent dessous, à droite. */
    @media (max-width: 560px) {
      .ligne-appel { flex-wrap: wrap; row-gap: 8px; padding-left: 12px; padding-right: 12px; }
      .ligne-appel .deux { flex-basis: calc(100% - 40px); }
      .ligne-appel .col-presence { margin-left: auto; min-width: 0; }
      .ligne-appel .col-paiement, .ligne-appel .col-mode { width: auto; }
      .titres-des-colonnes { display: none; }
    }
    .vide-point { visibility: hidden; }
    .case-paiement { display: inline-flex; align-items: center; gap: 6px; padding: 5px 10px; border: 1px solid var(--line-2); border-radius: 999px; font-size: 13.5px; cursor: pointer; white-space: nowrap; }
    .case-paiement input { width: 16px; height: 16px; margin: 0; accent-color: var(--accent); }
    .case-paiement small { color: var(--ink-2); font-size: 12px; }
    .case-paiement.coche { border-color: var(--accent); background: var(--accent-soft); color: var(--accent-ink); }
    .a-jour { color: var(--ok); font-size: 13px; font-weight: 500; }
    .lien { padding: 0; border: 0; background: none; color: var(--accent-ink); font: inherit; font-size: 12.5px; text-decoration: underline; cursor: pointer; }
    .payer-une-partie { justify-self: start; margin-top: 2px; }
    /* Sur la fiche du groupe : le formulaire de la séance et ses messages, juste sous la bande, avant l'appel. */
    .en-haut { order: -1; }
    /* Le formulaire s'ouvre dans une fenêtre, au centre, au-dessus de la page. */
    .fond-de-fenetre { position: fixed; inset: 0; z-index: 60; background: rgb(15 23 42 / 0.45); }
    .fenetre-de-seance { position: fixed; z-index: 61; top: 50%; left: 50%; transform: translate(-50%, -50%); margin: 0;
      width: min(560px, calc(100vw - 32px)); max-height: calc(100vh - 32px); overflow: auto; padding: 20px 22px; border-radius: 14px;
      box-shadow: 0 12px 40px rgb(15 23 42 / 0.25); }
    .fenetre-de-seance .haut-de-fenetre { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; margin-bottom: 12px; }
    .fenetre-de-seance .haut-de-fenetre h2 { margin: 0; }
    .fermer-la-fenetre { display: inline-flex; align-items: center; justify-content: center; width: 32px; height: 32px; padding: 0; border: 0;
      border-radius: 8px; background: none; color: var(--ink-2); font-size: 18px; cursor: pointer; }
    .fermer-la-fenetre:hover { background: var(--sunk); }
    .etat-et-outils { display: inline-flex; flex-wrap: wrap; align-items: center; justify-content: flex-end; gap: 6px; }
    .etat-et-outils .pill { margin-right: 4px; }
    /* Même bordure que les ronds P et A et la case du paiement. */
    .icone-seule { display: inline-flex; align-items: center; justify-content: center; width: 36px; height: 36px; padding: 0;
      border: 1px solid var(--line-2); border-radius: 10px; background: var(--surface); color: var(--ink-2); }
    .icone-seule:hover { background: var(--sunk); color: var(--ink); }
    .icone-seule svg { width: 17px; height: 17px; }
    .feuille-d-appel { display: inline-flex; align-items: center; gap: 6px; height: 36px; padding: 0 12px; border: 1px solid var(--line-2);
      border-radius: 10px; background: var(--surface); color: var(--ink); font-weight: 500; text-decoration: none; white-space: nowrap; }
    .feuille-d-appel:hover { background: var(--sunk); }
    .feuille-d-appel svg { width: 17px; height: 17px; color: var(--ink-2); }
    .nom-et-rappel { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 8px; min-width: 0; }
    .nom-et-rappel sh-bouton-de-rappel { max-width: 100%; }
    .nom-de-l-eleve b { white-space: nowrap; }
    .nom-de-l-eleve { color: inherit; text-decoration: none; }
    .nom-de-l-eleve:hover { color: var(--accent); text-decoration: underline; }
    .pas-a-cette-seance { background: var(--sunk); }
    .pas-a-cette-seance .nom-de-l-eleve b { color: var(--ink-2); }
    .tiret { color: var(--ink-3); }
    .ligne-partie { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 8px 18px 10px 44px; background: var(--accent-soft); font-size: 13px; }
    .ligne-partie label { display: inline-flex; align-items: center; gap: 6px; }
    .montant-partiel { width: 90px; padding: 4px 8px; font-size: 13px; }
    .ligne-partie .reste { color: var(--ink-2); }
    .ligne-partie .erreur-partie { color: var(--bad); }
    .ligne-partie .lien { margin-left: auto; }
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
    /* Dans son groupe : la séance qu'il a remplacée en venant avec un autre groupe. */
    .venu-ailleurs { justify-self: start; width: fit-content; margin-top: 2px; padding: 2px 8px; border-radius: 999px;
      background: var(--accent-soft); color: var(--accent-ink); font-size: 12px; font-weight: 600; }
    .groupes-venus { display: flex; flex-wrap: wrap; gap: 8px; }
    .puce { border-radius: 999px; }
    .puce.choisi { border-color: var(--accent); background: var(--accent-soft); color: var(--accent-ink); }
    .a-la-place { margin: 0; color: var(--ink-2); font-size: 13.5px; }
    .eleves-venus { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 8px; }
    .eleve-venu { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 10px 12px; border: 1px solid var(--line-2); border-radius: 10px; cursor: pointer; }
    .eleve-venu input { width: 18px; height: 18px; margin: 0; accent-color: var(--accent); }
    .eleve-venu.coche { border-color: var(--accent); background: var(--accent-soft); }
    .eleve-venu.impossible { cursor: default; opacity: 0.6; }
    .eleve-venu small { flex-basis: 100%; color: var(--ink-3); font-size: 12px; }
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
  /** La date de la séance à venir dont la colonne montre les sommes ; `null` : aujourd'hui. */
  readonly paiementAu = input<string | null>(null);
  /** Des reçus viennent d'être créés : la fiche du groupe recharge ce que chacun doit. */
  readonly paye = output<void>();
  /** Sur la fiche d'un groupe ouvert : « Ajouter une séance » (SEA-05), dont le formulaire est dans la carte du calendrier. */
  readonly ajoutPossible = input(false);
  /** Une séance déplacée, annulée ou remplacée : la fiche du groupe recharge sa bande. */
  readonly seanceChangee = output<void>();

  /** Depuis les boutons de la puce, dans la bande des séances : le formulaire s'ouvre en haut de l'appel. */
  ouvrirUneAction(action: ActionSurLaSeance): void {
    this.ouvrir(action);
  }

  /** « Déplacer la séance », « Annuler la séance », « Remplacer la séance ». */
  protected titreDuFormulaire(): string {
    const action = this.action();
    return action === 'annuler' ? 'Annuler la séance' : action === 'remplacer' ? 'Remplacer la séance' : 'Déplacer la séance';
  }
  /**
   * Sur la fiche du groupe, ses inscriptions (ELV-03) : l'appel fait aussi la liste des élèves, à la place de l'ancien bloc
   * « Les élèves ». `null` hors de la fiche : ni outils, ni liens, ni rappels.
   */
  readonly inscriptions = input<InscriptionDeLEleve[] | null>(null);
  protected readonly enRetard = enRetard;

  protected inscriptionDe(inscriptionId: string): InscriptionDeLEleve | undefined {
    return this.inscriptions()?.find((i) => i.id === inscriptionId);
  }

  /** « Par Mois · depuis le 19/09/2026 », au survol du nom. */
  protected inscriptionEnBref(inscriptionId: string): string | null {
    const inscription = this.inscriptionDe(inscriptionId);
    return inscription ? this.modeEtDebut(inscription) : null;
  }

  protected modeEtDebut(inscription: InscriptionDeLEleve): string {
    const debut = inscription.premiereSeance?.date;
    return `${modeCourt(inscription)}${debut ? ` · depuis le ${this.dateLongue(debut)}` : ''}`;
  }

  /** « Inscription arrêtée le 10/11 », ou « Inscrit à partir du 28/11 ». */
  protected pourquoiPasLa(inscription: InscriptionDeLEleve): string {
    if (inscription.arreteLe) return `Inscription arrêtée le ${this.court(inscription.arreteLe)}`;
    const debut = inscription.premiereSeance?.date;
    return debut ? `Inscrit à partir du ${this.court(debut)}` : 'Pas attendu à cette séance';
  }

  private dateLongue(texte: string): string {
    return formatDate(texte, 'dd/MM/yyyy', FRANCAIS);
  }

  /** Les inscrits qui ne sont pas à l'appel de cette séance : ils restent en bas du tableau, en gris. */
  protected readonly pasACetteSeance = computed(() => {
    const appel = this.appel();
    const inscriptions = this.inscriptions();
    if (!appel || !inscriptions) return [];
    const attendus = new Set(appel.eleves.map((e) => e.inscriptionId));
    return inscriptions.filter((i) => !attendus.has(i.id));
  });

  protected readonly montant = ecrireUnMontant;
  protected readonly colonneDePaiement = computed(() => this.integre() && !!this.groupeId());
  /** Les élèves cochés dans la colonne « Paiement ». */
  protected readonly aEncaisser = signal<string[]>([]);
  protected readonly envoiDuPaiement = signal(false);
  protected readonly erreurDuPaiement = signal<string | null>(null);
  protected readonly recusCrees = signal<RecusDuGroupe['recus'] | null>(null);
  private readonly cochables = computed(() => this.aPayer().filter((e) => this.aEncaisser().includes(e.eleveId)));
  protected readonly totalAEncaisser = computed(() => this.cochables().reduce((total, e) => total + this.aPayerPour(e), 0));
  /** « Payer une partie » : ce que chaque élève donne, tel qu'écrit (« 50 ») ; absent, il paie tout ce qu'il doit. */
  private readonly parties = signal<Readonly<Record<string, string>>>({});
  /** Un élève coché dont la partie n'est pas un montant possible : on ne paie pas. */
  protected readonly partieInvalide = computed(() => this.cochables().some((e) => this.erreurDeLaPartie(e) !== null));

  protected partieDe(eleveId: string): string | null {
    return this.parties()[eleveId] ?? null;
  }

  /** Ouvre la ligne « Il donne … TND » sous l'élève, et le coche. */
  protected ouvrirUnePartie(eleveId: string): void {
    this.parties.update((parties) => ({ ...parties, [eleveId]: '' }));
    if (!this.aEncaisser().includes(eleveId)) this.cocherLePaiement(eleveId);
  }

  protected ecrireUnePartie(eleveId: string, evenement: Event): void {
    const saisie = (evenement.target as HTMLInputElement).value;
    this.parties.update((parties) => ({ ...parties, [eleveId]: saisie }));
  }

  /** « Annuler » : il paie de nouveau tout ce qu'il doit. */
  protected fermerUnePartie(eleveId: string): void {
    this.parties.update(({ [eleveId]: _, ...autres }) => autres);
  }

  /** Ce que l'élève paie : la partie qu'il donne si elle est possible, sinon tout ce qu'il doit. */
  protected aPayerPour(eleve: EleveAEncaisser): number {
    const saisie = this.partieDe(eleve.eleveId);
    const donne = saisie === null ? null : lireUnNombre(saisie);
    return typeof donne === 'number' && this.erreurDeLaPartie(eleve) === null ? donne : eleve.total;
  }

  protected erreurDeLaPartie(eleve: EleveAEncaisser): string | null {
    const saisie = this.partieDe(eleve.eleveId);
    if (saisie === null) return null;
    const donne = lireUnNombre(saisie);
    if (donne === null) return 'Écrivez ce qu\'il donne';
    if (donne === undefined || donne <= 0 || Math.round(donne * 1000) !== donne * 1000) return 'Écrivez un montant, comme 50 ou 12,500';
    if (donne > eleve.total) return `Au plus ${ecrireUnMontant(eleve.total)} TND`;
    return null;
  }
  protected readonly totalDesRecus = computed(() => (this.recusCrees() ?? []).reduce((total, r) => total + r.total, 0));
  /** Ce qui reste à payer après les avances des reçus créés (« payé 70, reste 25 »). */
  protected readonly resteDesRecus = computed(() => (this.recusCrees() ?? []).reduce((total, r) => total + resteAPayer(r), 0));
  /** Le mode choisi pour chaque élève ; Espèces par défaut. */
  private readonly modes = signal<Readonly<Record<string, ModeDePaiement>>>({});
  protected readonly chequesCoches = computed(() => this.cochables().filter((e) => this.modeDe(e.eleveId) === 'Cheque').length);
  /** « en espèces », « par chèque », ou « 2 en espèces, 1 par chèque ». */
  protected readonly detailDesModes = computed(() => {
    const recus = this.recusCrees() ?? [];
    const cheques = recus.filter((r) => r.mode === 'Cheque').length;
    const especes = recus.length - cheques;
    if (cheques === 0) return 'en espèces';
    if (especes === 0) return 'par chèque';
    return `${especes} en espèces, ${cheques} par chèque`;
  });

  protected modeDe(eleveId: string): ModeDePaiement {
    return this.modes()[eleveId] ?? 'Especes';
  }

  protected choisirLeMode(eleveId: string, evenement: Event): void {
    const mode = (evenement.target as HTMLSelectElement).value === 'Cheque' ? 'Cheque' : 'Especes';
    this.modes.update((modes) => ({ ...modes, [eleveId]: mode }));
  }

  protected aPayerDe(eleveId: string): EleveAEncaisser | null {
    return this.aPayer().find((e) => e.eleveId === eleveId) ?? null;
  }

  protected cocherLePaiement(eleveId: string): void {
    this.erreurDuPaiement.set(null);
    this.recusCrees.set(null);
    this.aEncaisser.update((ids) => (ids.includes(eleveId) ? ids.filter((id) => id !== eleveId) : [...ids, eleveId]));
  }

  /** Un reçu par élève coché, en espèces ou par chèque, avec tout ce qu'il doit à ce jour, comme « Paiement du groupe ». */
  protected async payerLesCoches(): Promise<void> {
    const choisis = this.cochables();
    const groupeId = this.groupeId();
    if (this.envoiDuPaiement() || choisis.length === 0 || !groupeId) return;
    this.envoiDuPaiement.set(true);
    this.erreurDuPaiement.set(null);
    try {
      const reponse = await firstValueFrom(
        this.http.post<RecusDuGroupe>(`/api/groupes/${groupeId}/encaissement`, {
          eleves: choisis.map((e) => ({
            eleveId: e.eleveId,
            sommes: e.sommes.map((s) => ({ inscriptionId: s.inscriptionId, cle: s.cle })),
            mode: this.modeDe(e.eleveId),
            ...(this.partieDe(e.eleveId) !== null && this.aPayerPour(e) < e.total ? { montant: this.aPayerPour(e) } : {}),
          })),
          ...(this.paiementAu() ? { au: this.paiementAu() } : {}),
        }),
      );
      this.recusCrees.set(reponse.recus);
      this.aEncaisser.set([]);
      this.modes.set({});
      this.parties.set({});
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

  // ----- Venus d'un autre groupe (SEA-12) -----

  protected readonly rattrapables = signal<EleveRattrapable[] | null>(null);
  /** Le groupe d'où viennent les élèves, la séance de ce groupe qu'ils remplacent, et les élèves cochés. */
  protected readonly groupeVenu = signal<string | null>(null);
  protected readonly seanceRattrapee = signal('');
  protected readonly changerLaSeance = signal(false);
  protected readonly venus = signal<string[]>([]);
  protected readonly erreurDuRattrapage = signal<string | null>(null);

  /** Les autres groupes du professeur, même matière, même niveau, dans l'ordre de leur nom. */
  protected readonly groupesVenus = computed(() => {
    const groupes = new Map<string, { id: string; nom: string }>();
    for (const eleve of this.rattrapables() ?? []) groupes.set(eleve.groupe.id, eleve.groupe);
    return [...groupes.values()].sort((a, b) => a.nom.localeCompare(b.nom, 'fr'));
  });
  protected readonly elevesDuGroupeVenu = computed(() => (this.rattrapables() ?? []).filter((e) => e.groupe.id === this.groupeVenu()));
  /** Les séances du groupe que l'un au moins de ses élèves peut remplacer, par date. */
  protected readonly seancesDuGroupeVenu = computed(() => {
    const seances = new Map<string, EleveRattrapable['seances'][number]>();
    for (const eleve of this.elevesDuGroupeVenu()) for (const seance of eleve.seances) seances.set(seance.id, seance);
    return [...seances.values()].sort((a, b) => (a.date + a.debut).localeCompare(b.date + b.debut));
  });
  /** La date de la séance remplacée, pour « À la place de la séance du lun. 23/11 ». */
  protected readonly seanceRemplacee = computed(() => this.seancesDuGroupeVenu().find((s) => s.id === this.seanceRattrapee())?.date ?? '');

  protected peutRemplacer(eleve: EleveRattrapable): boolean {
    return eleve.seances.some((s) => s.id === this.seanceRattrapee());
  }

  protected async chercherLesRattrapables(): Promise<void> {
    this.erreurDuRattrapage.set(null);
    this.groupeVenu.set(null);
    try {
      this.rattrapables.set(await firstValueFrom(this.http.get<EleveRattrapable[]>(`/api/seances/${this.seanceId}/rattrapages/possibles`)));
      // Un seul autre groupe : il est déjà choisi.
      const groupes = this.groupesVenus();
      if (groupes.length === 1) this.choisirLeGroupeVenu(groupes[0].id);
    } catch (erreur) {
      this.erreurDuRattrapage.set(messageDErreur(erreur));
    }
  }

  /** Par défaut, ils remplacent la prochaine séance de leur groupe, à partir du jour de cette séance. */
  protected choisirLeGroupeVenu(groupeId: string): void {
    this.groupeVenu.set(groupeId);
    this.erreurDuRattrapage.set(null);
    this.changerLaSeance.set(false);
    this.venus.set([]);
    const seances = this.seancesDuGroupeVenu();
    const jour = this.appel()?.date ?? '';
    this.seanceRattrapee.set((seances.find((s) => s.date >= jour) ?? seances.at(-1))?.id ?? '');
  }

  protected choisirLaSeanceRemplacee(seanceId: string): void {
    this.seanceRattrapee.set(seanceId);
    // Un élève qui ne peut pas remplacer cette séance n'est plus coché.
    const possibles = this.elevesDuGroupeVenu().filter((e) => this.peutRemplacer(e)).map((e) => e.inscriptionId);
    this.venus.update((ids) => ids.filter((id) => possibles.includes(id)));
  }

  protected cocherLeVenu(inscriptionId: string): void {
    this.venus.update((ids) => (ids.includes(inscriptionId) ? ids.filter((id) => id !== inscriptionId) : [...ids, inscriptionId]));
  }

  protected fermerLesVenus(): void {
    this.rattrapables.set(null);
    this.groupeVenu.set(null);
    this.venus.set([]);
    this.erreurDuRattrapage.set(null);
  }

  protected async ajouterLeRattrapage(): Promise<void> {
    if (this.envoi()) return;
    if (this.venus().length === 0 || !this.seanceRattrapee()) {
      this.erreurDuRattrapage.set('Cochez les élèves venus');
      return;
    }
    await this.changerLeRattrapage(
      this.http.post<AppelDeLaSeance>(`/api/seances/${this.seanceId}/rattrapages`, {
        inscriptionIds: this.venus(),
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
      this.fermerLesVenus();
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

  /** Échap referme la fenêtre du formulaire, sur la fiche du groupe. */
  protected fermerAuClavier(): void {
    if (this.integre() && this.action() !== null && !this.envoi()) this.fermerLAction();
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
      this.seanceChangee.emit();
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
