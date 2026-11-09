import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { Icone } from '../../socle/icone';
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
  imports: [Icone],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Parts des professeurs</h1>
        <p class="sub">Le centre garde sa part sur le montant du groupe, payé ou non. Le professeur reçoit le reste, moins ses avances.</p>
      </div>
      @if (!avance()) {
        <button type="button" class="btn" (click)="avance.set(true)"><sh-icone nom="plus" />Enregistrer une avance</button>
      }
    </div>

    @if (parts(); as parts) {
      <div class="chiffres">
        <div class="chiffre" [class.warn]="parts.resteAVerser > 0" [class.zero]="parts.resteAVerser === 0">
          <span class="tete"><span class="icone warn"><sh-icone nom="wallet" /></span>Reste à verser</span>
          <b><span class="valeur">{{ montant(parts.resteAVerser) }}</span><small> TND</small></b>
        </div>
        <div class="chiffre" [class.zero]="parts.dejaVerse === 0">
          <span class="tete"><span class="icone ok"><sh-icone nom="circle-check" /></span>Déjà versé</span>
          <b><span class="valeur">{{ montant(parts.dejaVerse) }}</span><small> TND</small></b>
        </div>
        <div class="chiffre" [class.zero]="parts.avancesRestantes === 0">
          <span class="tete"><span class="icone info"><sh-icone nom="receipt" /></span>Avances pas encore déduites</span>
          <b><span class="valeur">{{ montant(parts.avancesRestantes) }}</span><small> TND</small></b>
        </div>
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
            <span class="avatar" aria-hidden="true">{{ initiales(professeur.nom) }}</span>
            <h2 class="titre">{{ professeur.nom }}<span class="part"> · part du centre {{ pourcent(professeur.pourcentage) }}</span></h2>
            <span class="resume">
              @if (professeur.resteAVerser > 0) {
                <span class="pill warn">À verser {{ montant(professeur.resteAVerser) }}</span>
              } @else {
                <span class="pill ok"><sh-icone nom="circle-check" />Tout versé</span>
              }
              <span class="detail">versé {{ montant(professeur.dejaVerse) }}</span>
              @if (professeur.avancesRestantes > 0) {
                <span class="detail">· avances {{ montant(professeur.avancesRestantes) }}</span>
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
                  <th>Groupe</th><th>Période</th><th class="r">Montant</th><th class="r">Part du centre</th>
                  <th class="r">Avance</th><th class="r">À verser</th><th class="c">Payée</th>
                </tr>
              </thead>
              <tbody>
                @for (ligne of professeur.lignes; track ligne.groupeId + ligne.cle) {
                  <tr [class.vide-de-montant]="ligne.montantDuGroupe === 0">
                    <td><span class="groupe">{{ ligne.groupe }}</span><small>{{ ligne.matiere }}</small></td>
                    <td><span class="periode">{{ ligne.periode }}</span><small>{{ date(ligne.debut) }} → {{ date(ligne.fin) }}</small></td>
                    <td class="r chiffre-de-ligne">{{ montant(ligne.montantDuGroupe) }}</td>
                    <td class="r chiffre-de-ligne">{{ montant(ligne.partDuCentre) }}<small>{{ pourcent(ligne.pourcentage) }}</small></td>
                    <td class="r chiffre-de-ligne" [class.deduite]="ligne.avanceDeduite > 0">{{ ligne.avanceDeduite > 0 ? '−' + montant(ligne.avanceDeduite) : '—' }}</td>
                    <td class="r chiffre-de-ligne"><b>{{ montant(ligne.aVerser) }}</b></td>
                    <td class="c">
                      <label class="coche" [class.oui]="ligne.payee">
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
    svg.lucide { width: 16px; height: 16px; flex: none; }

    /* Les trois chiffres : des cartes blanches, une icône, le montant en grand. Un chiffre à 0 passe en gris. */
    .chiffres { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; }
    .chiffre { display: grid; gap: 10px; padding: 14px 16px; border-radius: 12px; background: var(--surface); border: 1px solid var(--line); }
    .chiffre .tete { display: flex; align-items: center; gap: 8px; color: var(--ink-2); font-size: 13px; font-weight: 500; }
    .icone { display: grid; place-items: center; width: 28px; height: 28px; border-radius: 8px; flex: none; }
    .icone.warn { background: var(--warn-soft); color: var(--warn); }
    .icone.ok { background: var(--ok-soft); color: var(--ok); }
    .icone.info { background: var(--accent-soft); color: var(--accent); }
    .chiffre b { font-family: var(--font-display); font-size: 26px; font-weight: 600; line-height: 1.1; font-variant-numeric: tabular-nums; }
    .chiffre b small { font-family: var(--font-body); font-size: 13px; font-weight: 500; color: var(--ink-2); }
    .chiffre.warn { border-color: var(--warn); box-shadow: inset 3px 0 0 var(--warn); }
    .chiffre.warn b { color: var(--warn); }
    .chiffre.zero b { color: var(--ink-3); }

    /* Le tableau : une bande par professeur, puis ses lignes. */
    .tableau-des-parts { padding: 0; overflow: hidden; }
    .professeur .entete { display: flex; align-items: center; flex-wrap: wrap; gap: 8px 12px; padding: 10px 16px; background: var(--sunk); border-top: 1px solid var(--line); border-bottom: 1px solid var(--line); }
    .professeur:first-child .entete { border-top: 0; }
    .avatar { display: grid; place-items: center; width: 30px; height: 30px; border-radius: 50%; background: var(--surface); color: var(--accent-ink); border: 1px solid var(--line); font-size: 12px; font-weight: 600; flex: none; }
    .professeur .titre { flex: 1 1 200px; margin: 0; font-size: 14px; font-weight: 600; }
    .professeur .titre .part { color: var(--ink-2); font-weight: 400; }
    .resume { display: flex; align-items: center; flex-wrap: wrap; gap: 6px; font-size: 13px; }
    .resume .detail { color: var(--ink-2); }
    .resume .pill { font-variant-numeric: tabular-nums; }
    /* Les en-têtes de colonnes une seule fois, en haut : les tableaux des professeurs ont les mêmes colonnes. */
    .professeur:not(:first-child) thead { display: none; }

    .defile { overflow-x: auto; }
    table { width: 100%; min-width: 640px; table-layout: fixed; border-collapse: collapse; font-size: 13.5px; }
    .c-groupe { width: 26%; } .c-periode { width: 18%; } .c-montant { width: 11%; } .c-payee { width: 12%; }
    th, td { text-align: left; padding: 10px 16px 10px 0; border-top: 1px solid var(--line); vertical-align: middle; }
    th:first-child, td:first-child { padding-left: 16px; }
    th { color: var(--ink-3); font-size: 12px; font-weight: 500; border-top: 0; white-space: nowrap; }
    tbody tr:hover { background: color-mix(in srgb, var(--sunk) 50%, transparent); }
    td small { display: block; margin-top: 1px; color: var(--ink-3); font-size: 12px; }
    .groupe, .periode { font-weight: 500; }
    .chiffre-de-ligne { font-variant-numeric: tabular-nums; white-space: nowrap; color: var(--ink-2); }
    .chiffre-de-ligne b { color: var(--ink); font-size: 14px; font-weight: 600; }
    .chiffre-de-ligne.deduite { color: var(--warn); }
    /* Un groupe sans encaissement : la ligne reste, en gris, pour ne pas cacher le Mois. */
    tr.vide-de-montant .chiffre-de-ligne, tr.vide-de-montant .chiffre-de-ligne b { color: var(--ink-3); font-weight: 400; }
    .r { text-align: right; }
    .c { text-align: center; }

    /* « Payée » : une pastille cliquable, verte une fois cochée. Elle reste sur une ligne. */
    .coche { display: inline-flex; align-items: center; gap: 6px; padding: 3px 10px 3px 6px; border-radius: 999px; border: 1px solid var(--line-2); color: var(--ink-2); font-size: 12px; font-weight: 500; white-space: nowrap; cursor: pointer; }
    .coche:hover { background: var(--sunk); }
    .coche input { margin: 0; accent-color: var(--ok); }
    .coche.oui { background: var(--ok-soft); border-color: transparent; color: var(--ok); }

    .champs { display: grid; max-width: 880px; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 10px; }
    .actions { display: flex; gap: 8px; justify-content: flex-end; }
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

  /** « HK » pour Hassen Kharrat : la première lettre des deux premiers mots du nom. */
  protected initiales(nom: string): string {
    return nom.trim().split(/\s+/).slice(0, 2).map((mot) => mot.charAt(0)).join('').toUpperCase();
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
