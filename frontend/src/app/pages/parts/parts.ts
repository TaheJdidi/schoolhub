import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant, lireUnNombre } from '../../socle/montants';
import { dateDuJourLocale } from '../groupes/groupe';
import { ecrirePourcent } from './part-du-centre';

/** PRT-01 : une ligne : un groupe et une période. */
export interface LigneDePart {
  groupeId: string;
  groupe: string;
  matiere: string;
  cle: string;
  periode: string;
  debut: string;
  fin: string;
  montantDuGroupe: number;
  pourcentage: number;
  partDuCentre: number;
  avanceDeduite: number;
  aVerser: number;
  payee: boolean;
  payeeLe: string | null;
}

/** PRT-01 : les parts d'un professeur. */
export interface PartsDUnProfesseur {
  id: string;
  nom: string;
  pourcentage: number;
  resteAVerser: number;
  dejaVerse: number;
  avancesRestantes: number;
  lignes: LigneDePart[];
}

/** PRT-01 : GET /api/parts. */
export interface PartsDuCentre {
  resteAVerser: number;
  dejaVerse: number;
  avancesRestantes: number;
  professeurs: PartsDUnProfesseur[];
}

/**
 * PRT-01 à PRT-03 : les parts des professeurs, pour le propriétaire. Le reste à verser, le déjà versé et les avances pas
 * encore déduites ; « Enregistrer une avance » ; un bloc par professeur, une ligne par groupe et période finie, avec
 * « Payée » à cocher.
 */
@Component({
  selector: 'sh-parts',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Parts des professeurs</h1>
        <p class="sub">Le centre garde sa part sur le montant du groupe, payé ou non. Le professeur reçoit le reste, moins ses avances.</p>
      </div>
      @if (!avance()) {
        <button type="button" class="btn" (click)="avance.set(true)">Enregistrer une avance</button>
      }
    </div>

    @if (parts(); as parts) {
      <div class="chiffres">
        <div [class.warn]="parts.resteAVerser > 0">
          <span>Reste à verser</span><b><span class="num">{{ montant(parts.resteAVerser) }}</span><small> TND</small></b>
        </div>
        <div><span>Déjà versé</span><b><span class="num">{{ montant(parts.dejaVerse) }}</span><small> TND</small></b></div>
        <div><span>Avances pas encore déduites</span><b><span class="num">{{ montant(parts.avancesRestantes) }}</span><small> TND</small></b></div>
      </div>

      @if (avance()) {
        <section class="card carte avance" aria-labelledby="t-avance">
          <h2 class="titre" id="t-avance">Nouvelle avance</h2>
          <div class="champs">
            <label class="f">
              Professeur
              <select class="inp" #professeur>
                @for (p of professeursConnus(); track p.id) {
                  <option [value]="p.id">{{ p.nom }}</option>
                }
              </select>
            </label>
            <label class="f">Montant (TND) <input #somme inputmode="decimal" autocomplete="off" placeholder="150" /></label>
            <label class="f">Date <input #le type="date" [value]="aujourdhui" /></label>
          </div>
          <div class="actions">
            <button type="button" class="btn" [disabled]="envoi()" (click)="avance.set(false)">Annuler</button>
            <button type="button" class="btn primary" [disabled]="envoi()" (click)="avancer(professeur.value, somme.value, le.value)">Enregistrer l'avance</button>
          </div>
        </section>
      }

      <!-- Comme le prototype (écran 12) : un seul tableau, chaque professeur ouvert par une bande grise. -->
      @if (parts.professeurs.length > 0) {
      <div class="card tableau-des-parts">
      @for (professeur of parts.professeurs; track professeur.id) {
        <section class="professeur" [attr.aria-label]="professeur.nom">
          <div class="entete">
            <h2 class="titre">{{ professeur.nom }} · part du centre {{ pourcent(professeur.pourcentage) }}</h2>
            <span class="resume">
              À verser {{ montant(professeur.resteAVerser) }} · versé {{ montant(professeur.dejaVerse) }}
              @if (professeur.avancesRestantes > 0) {
                · avances {{ montant(professeur.avancesRestantes) }}
              }
            </span>
          </div>
          <div class="defile">
            <table>
              <colgroup>
                <col class="c-groupe" /><col class="c-periode" /><col class="c-montant" /><col class="c-montant" />
                <col class="c-montant" /><col class="c-montant" /><col class="c-payee" />
              </colgroup>
              <thead>
                <tr>
                  <th>Groupe</th><th>Période</th><th class="r">Montant du groupe</th><th class="r">Part du centre</th>
                  <th class="r">Avance déduite</th><th class="r">À verser</th><th>Payée</th>
                </tr>
              </thead>
              <tbody>
                @for (ligne of professeur.lignes; track ligne.groupeId + ligne.cle) {
                  <tr>
                    <td>{{ ligne.groupe }}<br /><small>{{ ligne.matiere }}</small></td>
                    <td>{{ ligne.periode }}<br /><small>{{ date(ligne.debut) }} → {{ date(ligne.fin) }}</small></td>
                    <td class="r num">{{ montant(ligne.montantDuGroupe) }}</td>
                    <td class="r num">{{ montant(ligne.partDuCentre) }}<br /><small>{{ pourcent(ligne.pourcentage) }}</small></td>
                    <td class="r num">{{ ligne.avanceDeduite > 0 ? '−' + montant(ligne.avanceDeduite) : '—' }}</td>
                    <td class="r num"><b>{{ montant(ligne.aVerser) }}</b></td>
                    <td>
                      <label class="coche">
                        <input type="checkbox" [checked]="ligne.payee" [disabled]="envoi()" (change)="marquer(ligne)" />
                        {{ ligne.payee ? 'Payée' : 'Non payée' }}
                      </label>
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="7" class="vide">Pas encore de période finie.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </section>
      }
      </div>
      } @else {
        <p class="vide">Pas encore de part : une ligne naît quand un Mois, une Révision ou une Formation est fini.</p>
      }
    }
    @if (succes(); as succes) {
      <p class="note ok" role="status">{{ succes }}</p>
    }
    @if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    :host { display: grid; gap: 16px; }
    .pagehead { margin-bottom: 0; }
    .chiffres { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; }
    .chiffres div { display: grid; gap: 4px; padding: 16px 18px; border-radius: 14px; background: var(--sunk); }
    .chiffres div > span { color: var(--ink-2); font-size: 13px; }
    .chiffres b { font-family: var(--font-display); font-size: 26px; font-weight: 600; line-height: 1.2; }
    .chiffres b small { font-family: var(--font-body); font-size: 14px; font-weight: 500; color: var(--ink-2); }
    .chiffres div.warn b { color: var(--warn); }
    .tableau-des-parts { padding: 0; overflow: hidden; }
    .professeur .entete { padding: 9px 12px; background: var(--sunk); border-top: 1px solid var(--line); border-bottom: 1px solid var(--line); }
    .professeur:first-child .entete { border-top: 0; }
    .professeur .titre { font-size: 13.5px; font-weight: 600; }
    /* Les en-têtes de colonnes une seule fois, en haut : les tableaux des professeurs ont les mêmes colonnes. */
    .professeur:not(:first-child) thead { display: none; }
    table { table-layout: fixed; }
    .c-groupe { width: 24%; } .c-periode { width: 22%; } .c-montant { width: 11%; } .c-payee { width: 11%; }
    th, td { padding: 9px 12px !important; white-space: normal !important; }
    .champs { display: grid; max-width: 880px; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 10px; }
    .actions { display: flex; gap: 8px; justify-content: flex-end; }
    .entete { display: flex; justify-content: space-between; align-items: baseline; flex-wrap: wrap; gap: 8px; }
    .resume { color: var(--ink-2); font-size: 13px; }
    .defile { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    th, td { text-align: left; padding: 8px 6px; border-top: 1px solid var(--line); vertical-align: top; white-space: nowrap; }
    th { color: var(--ink-2); font-weight: 500; border-top: 0; }
    td small { color: var(--ink-2); }
    .r { text-align: right; }
    .coche { display: inline-flex; align-items: center; gap: 6px; cursor: pointer; }
  `,
})
export class Parts implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly montant = ecrireUnMontant;
  protected readonly pourcent = ecrirePourcent;
  protected readonly aujourdhui = dateDuJourLocale();
  protected readonly parts = signal<PartsDuCentre | null>(null);
  protected readonly professeursConnus = signal<{ id: string; nom: string }[]>([]);
  protected readonly avance = signal(false);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      const [parts, professeurs] = await Promise.all([
        firstValueFrom(this.http.get<PartsDuCentre>('/api/parts')),
        firstValueFrom(this.http.get<{ id: string; prenom: string; nom: string; parti: boolean }[]>('/api/professeurs')),
      ]);
      this.parts.set(parts);
      this.professeursConnus.set(professeurs.filter((p) => !p.parti).map((p) => ({ id: p.id, nom: `${p.prenom} ${p.nom}` })));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected async marquer(ligne: LigneDePart): Promise<void> {
    await this.envoyer(() =>
      firstValueFrom(this.http.post<PartsDuCentre>('/api/parts/paiement', { groupeId: ligne.groupeId, cle: ligne.cle, payee: !ligne.payee })),
    );
  }

  protected async avancer(professeurId: string, somme: string, le: string): Promise<void> {
    const montant = lireUnNombre(somme);
    if (typeof montant !== 'number' || montant <= 0) {
      this.erreur.set("Le montant de l'avance est positif, au millime près");
      return;
    }
    const enregistree = await this.envoyer(() =>
      firstValueFrom(this.http.post<PartsDuCentre>('/api/parts/avances', { professeurId, montant, le: le || null })),
    );
    if (enregistree) {
      this.avance.set(false);
      this.succes.set(`Avance de ${ecrireUnMontant(montant)} TND enregistrée : elle se déduit des parts suivantes.`);
    }
  }

  /** « 18/09 ». */
  protected date(texte: string): string {
    return formatDate(texte, 'dd/MM', FRANCAIS);
  }

  private async envoyer(action: () => Promise<PartsDuCentre>): Promise<boolean> {
    this.envoi.set(true);
    this.erreur.set(null);
    this.succes.set(null);
    try {
      this.parts.set(await action());
      return true;
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
      return false;
    } finally {
      this.envoi.set(false);
    }
  }
}
