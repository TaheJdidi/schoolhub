import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { dateDuJourLocale } from '../groupes/groupe';

export interface LigneDuTableau {
  groupeId: string;
  groupe: string;
  matiere: string;
  type: string;
  professeurId: string;
  professeur: string;
  montantDuGroupe: number | null;
  encaisse: number;
  impayes: number;
  partDuProfesseur: number | null;
  partDuCentre: number | null;
}

/** TDB-01 : GET /api/tableau-de-bord. */
export interface TableauDUnePeriode {
  periode: string;
  du: string;
  au: string;
  encaisse: number;
  encaisseParOrigine: { libelle: string; valeur: number }[];
  impayes: number;
  impayesDAssurance: number;
  depenses: number;
  partsDesProfesseurs: number;
  salaires: number;
  achats: number;
  benefice: number;
  parGroupe: LigneDuTableau[];
}

/** TDB-03 : GET /api/tableau-de-bord/prix-speciaux. */
export interface PrixSpeciauxEtDispenses {
  prixSpeciaux: {
    eleveId: string;
    eleve: string;
    groupe: string;
    unite: string;
    prixNormal: number | null;
    prixSpecial: number;
    raison: string | null;
    le: string | null;
    par: string | null;
  }[];
  dispenses: { eleveId: string; eleve: string; montant: number; raison: string | null; le: string; par: string | null }[];
}

interface Totaux {
  montantDuGroupe: number;
  encaisse: number;
  impayes: number;
  partDuProfesseur: number;
  partDuCentre: number;
}

const totaux = (lignes: LigneDuTableau[]): Totaux => ({
  montantDuGroupe: lignes.reduce((t, l) => t + (l.montantDuGroupe ?? 0), 0),
  encaisse: lignes.reduce((t, l) => t + l.encaisse, 0),
  impayes: lignes.reduce((t, l) => t + l.impayes, 0),
  partDuProfesseur: lignes.reduce((t, l) => t + (l.partDuProfesseur ?? 0), 0),
  partDuCentre: lignes.reduce((t, l) => t + (l.partDuCentre ?? 0), 0),
});

/**
 * TDB-01 à TDB-03 : le tableau de bord du propriétaire. Un mois ou l'année scolaire : encaissé, impayés, dépenses et
 * bénéfice (en rouge s'il est négatif) ; l'encaissé par origine et les dépenses ; le détail par groupe, regroupable par
 * professeur ; les prix spéciaux et les dispenses, à imprimer.
 */
@Component({
  selector: 'sh-tableau-de-bord',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Tableau de bord</h1>
        <p class="sub">{{ tableau()?.periode ?? '' }} · chaque somme compte dans le mois de sa date</p>
      </div>
      <div class="periode">
        <label class="f">Mois <input type="month" [value]="mois()" (change)="choisirLeMois($any($event.target).value)" /></label>
        <button type="button" class="btn" [class.primary]="annee()" (click)="choisirLAnnee()">Année scolaire</button>
      </div>
    </div>

    @if (tableau(); as t) {
      <div class="chiffres">
        <div><span>Encaissé</span><b class="num">{{ montant(t.encaisse) }} TND</b><small>Espèces reçues dans la période</small></div>
        <div [class.bad]="t.impayes > 0">
          <span>Impayés</span><b class="num">{{ montant(t.impayes) }} TND</b>
          <small>Dû dans la période, pas encore payé{{ t.impayesDAssurance > 0 ? ' · dont ' + montant(t.impayesDAssurance) + " d'assurance" : '' }}</small>
        </div>
        <div><span>Dépenses</span><b class="num">{{ montant(t.depenses) }} TND</b><small>Parts des professeurs, salaires, achats</small></div>
        <div [class.ok]="t.benefice >= 0" [class.bad]="t.benefice < 0">
          <span>Bénéfice</span><b class="num benefice">{{ montant(t.benefice) }} TND</b><small>Encaissé moins dépenses</small>
        </div>
      </div>

      <div class="deux-cartes">
        <section class="card carte">
          <h2 class="titre">Encaissé par origine</h2>
          @for (origine of t.encaisseParOrigine; track origine.libelle) {
            <div class="barre"><span>{{ origine.libelle }}</span><span class="piste"><span class="plein" [style.width.%]="part(origine.valeur, t.encaisseParOrigine)"></span></span><b class="num">{{ montant(origine.valeur) }}</b></div>
          }
        </section>
        <section class="card carte">
          <h2 class="titre">Dépenses</h2>
          @for (depense of depensesDe(t); track depense.libelle) {
            <div class="barre"><span>{{ depense.libelle }}</span><span class="piste"><span class="plein depense" [style.width.%]="part(depense.valeur, depensesDe(t))"></span></span><b class="num">{{ montant(depense.valeur) }}</b></div>
          }
        </section>
      </div>

      <section class="card carte">
        <div class="entete">
          <h2 class="titre">Par groupe</h2>
          <label class="coche"><input type="checkbox" [checked]="parProfesseur()" (change)="parProfesseur.set(!parProfesseur())" /> Regrouper par professeur</label>
        </div>
        <div class="defile">
          <table>
            <thead>
              <tr><th>Groupe</th><th class="r">Montant du groupe</th><th class="r">Encaissé</th><th class="r">Impayés</th><th class="r">Part du professeur</th><th class="r">Part du centre</th></tr>
            </thead>
            <tbody>
              @for (bloc of blocs(); track bloc.professeur) {
                @if (parProfesseur()) {
                  <tr class="sous-total">
                    <td>{{ bloc.professeur }}</td>
                    <td class="r num">{{ montant(bloc.totaux.montantDuGroupe) }}</td><td class="r num">{{ montant(bloc.totaux.encaisse) }}</td>
                    <td class="r num">{{ montant(bloc.totaux.impayes) }}</td><td class="r num">{{ montant(bloc.totaux.partDuProfesseur) }}</td>
                    <td class="r num">{{ montant(bloc.totaux.partDuCentre) }}</td>
                  </tr>
                }
                @for (ligne of bloc.lignes; track ligne.groupeId) {
                  <tr>
                    <td>{{ ligne.matiere }} · {{ ligne.groupe }}<br /><small>{{ ligne.professeur }} · {{ ligne.type }}</small></td>
                    <td class="r num">{{ ligne.montantDuGroupe === null ? '—' : montant(ligne.montantDuGroupe) }}</td>
                    <td class="r num">{{ montant(ligne.encaisse) }}</td>
                    <td class="r num" [class.bad]="ligne.impayes > 0">{{ montant(ligne.impayes) }}</td>
                    <td class="r num">{{ ligne.partDuProfesseur === null ? '—' : montant(ligne.partDuProfesseur) }}</td>
                    <td class="r num">{{ ligne.partDuCentre === null ? '—' : montant(ligne.partDuCentre) }}</td>
                  </tr>
                }
              } @empty {
                <tr><td colspan="6" class="vide">Rien sur cette période.</td></tr>
              }
            </tbody>
            <tfoot>
              <tr class="total">
                <td>Total</td>
                <td class="r num">{{ montant(total().montantDuGroupe) }}</td><td class="r num">{{ montant(total().encaisse) }}</td>
                <td class="r num">{{ montant(total().impayes) }}</td><td class="r num">{{ montant(total().partDuProfesseur) }}</td>
                <td class="r num">{{ montant(total().partDuCentre) }}</td>
              </tr>
            </tfoot>
          </table>
        </div>
        <p class="aide">Montant du groupe et parts : les Mois et les groupes finis dans la période.</p>
      </section>
    }

    @if (prix(); as prix) {
      <section class="card carte prix-speciaux">
        <div class="entete">
          <h2 class="titre">Prix spéciaux et dispenses</h2>
          <button type="button" class="btn sm" (click)="imprimer()">Imprimer</button>
        </div>
        <div class="defile">
          <table>
            <thead><tr><th>Élève</th><th>Groupe</th><th class="r">Prix normal</th><th class="r">Prix spécial</th><th>Raison</th><th>Accordé</th></tr></thead>
            <tbody>
              @for (p of prix.prixSpeciaux; track p.eleveId + p.groupe) {
                <tr>
                  <td>{{ p.eleve }}</td><td>{{ p.groupe }}</td>
                  <td class="r num">{{ p.prixNormal === null ? '—' : montant(p.prixNormal) }} <small>{{ p.unite }}</small></td>
                  <td class="r num">{{ montant(p.prixSpecial) }}</td>
                  <td>{{ p.raison }}</td>
                  <td>{{ p.le ? date(p.le) : '—' }}{{ p.par ? ' · ' + p.par : '' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="6" class="vide">Aucun prix spécial en cours.</td></tr>
              }
            </tbody>
          </table>
          <table>
            <thead><tr><th>Élève</th><th class="r">Assurance</th><th>Raison de la dispense</th><th>Accordée</th></tr></thead>
            <tbody>
              @for (d of prix.dispenses; track d.eleveId) {
                <tr><td>{{ d.eleve }}</td><td class="r num">{{ montant(d.montant) }}</td><td>{{ d.raison }}</td><td>{{ date(d.le) }}{{ d.par ? ' · ' + d.par : '' }}</td></tr>
              } @empty {
                <tr><td colspan="4" class="vide">Aucune dispense cette année.</td></tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    }
    @if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    :host { display: grid; gap: 16px; }
    .pagehead { margin-bottom: 0; }
    .periode { display: flex; gap: 8px; align-items: end; flex-wrap: wrap; }
    .chiffres { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; }
    .chiffres div { display: grid; gap: 2px; padding: 12px 14px; border-radius: 12px; background: var(--surface); border: 1px solid var(--line); }
    .chiffres span, .chiffres small { color: var(--ink-2); font-size: 12.5px; }
    .chiffres b { font-size: 20px; }
    .chiffres .bad .benefice, .chiffres div.bad b { color: var(--bad); }
    .chiffres div.ok .benefice { color: var(--ok); }
    .deux-cartes { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; }
    .barre { display: grid; grid-template-columns: 120px 1fr auto; gap: 10px; align-items: center; font-size: 13px; padding: 4px 0; }
    .piste { height: 8px; border-radius: 4px; background: var(--sunk); overflow: hidden; }
    .plein { display: block; height: 100%; background: var(--accent, #1d5ea8); }
    .plein.depense { background: var(--warn); }
    .entete { display: flex; justify-content: space-between; align-items: center; gap: 10px; flex-wrap: wrap; }
    .coche { display: inline-flex; gap: 6px; align-items: center; font-size: 13px; }
    .defile { overflow-x: auto; display: grid; gap: 14px; }
    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    th, td { text-align: left; padding: 8px 6px; border-top: 1px solid var(--line); vertical-align: top; }
    th { color: var(--ink-2); font-weight: 500; border-top: 0; }
    td small { color: var(--ink-2); }
    td.bad { color: var(--bad); }
    .r { text-align: right; white-space: nowrap; }
    .sous-total td { background: var(--sunk); font-weight: 600; }
    .total td { font-weight: 600; }
    @media print { .pagehead, .chiffres, .deux-cartes, .card:not(.prix-speciaux), .entete button { display: none; } }
  `,
})
export class TableauDeBord implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly montant = ecrireUnMontant;
  protected readonly mois = signal(dateDuJourLocale().slice(0, 7));
  protected readonly annee = signal(false);
  protected readonly parProfesseur = signal(false);
  protected readonly tableau = signal<TableauDUnePeriode | null>(null);
  protected readonly prix = signal<PrixSpeciauxEtDispenses | null>(null);
  protected readonly erreur = signal<string | null>(null);

  /** Les lignes, par professeur quand on regroupe ; un seul bloc sinon. */
  protected readonly blocs = computed(() => {
    const lignes = this.tableau()?.parGroupe ?? [];
    if (!this.parProfesseur()) return lignes.length ? [{ professeur: '', lignes, totaux: totaux(lignes) }] : [];
    const noms = [...new Set(lignes.map((l) => l.professeur))];
    return noms.map((professeur) => {
      const siennes = lignes.filter((l) => l.professeur === professeur);
      return { professeur, lignes: siennes, totaux: totaux(siennes) };
    });
  });
  protected readonly total = computed(() => totaux(this.tableau()?.parGroupe ?? []));

  async ngOnInit(): Promise<void> {
    await Promise.all([this.charger(), this.chargerLesPrix()]);
  }

  protected async choisirLeMois(mois: string): Promise<void> {
    if (!mois) return;
    this.mois.set(mois);
    this.annee.set(false);
    await this.charger();
  }

  protected async choisirLAnnee(): Promise<void> {
    this.annee.set(true);
    await this.charger();
  }

  protected depensesDe(t: TableauDUnePeriode): { libelle: string; valeur: number }[] {
    return [
      { libelle: 'Parts des professeurs', valeur: t.partsDesProfesseurs },
      { libelle: 'Salaires', valeur: t.salaires },
      { libelle: 'Achats', valeur: t.achats },
    ];
  }

  /** La longueur d'une barre, en pourcentage de la plus grande. */
  protected part(valeur: number, toutes: { valeur: number }[]): number {
    const plusGrande = Math.max(...toutes.map((t) => t.valeur), 1);
    return Math.round((Math.max(valeur, 0) / plusGrande) * 1000) / 10;
  }

  /** « 27/11/2027 ». */
  protected date(texte: string): string {
    return formatDate(texte, 'dd/MM/yyyy', FRANCAIS);
  }

  protected imprimer(): void {
    window.print();
  }

  private async charger(): Promise<void> {
    const params: Record<string, string> = this.annee() ? { annee: 'true' } : { mois: this.mois() };
    try {
      this.tableau.set(await firstValueFrom(this.http.get<TableauDUnePeriode>('/api/tableau-de-bord', { params })));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  private async chargerLesPrix(): Promise<void> {
    try {
      this.prix.set(await firstValueFrom(this.http.get<PrixSpeciauxEtDispenses>('/api/tableau-de-bord/prix-speciaux')));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }
}
