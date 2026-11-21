import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderLeBrouillon } from '../../socle/brouillons';
import { requis } from '../../socle/champs-requis';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant, lireUnNombre } from '../../socle/montants';
import { dateDuJourLocale, FicheDuGroupe } from './groupe';

const PRIX_ILLISIBLE = 'Écrivez le prix du Mois, par exemple 60,000';
const CAPACITE_ILLISIBLE = 'Écrivez la capacité en nombre de places, par exemple 16';
const FERMETURE_INCOMPLETE = 'Choisissez la date et écrivez le motif';

export type ActionSurLeGroupe = 'modifier' | 'fermer';

/**
 * GRP-04 et GRP-08, depuis la carte du groupe sur la page du professeur, sans entrer dans le groupe : une fenêtre pour
 * « Modifier » (le nom, la capacité et, pour une Étude seulement, le prix du Mois, qui vaut pour les Mois pas encore
 * commencés) ou « Fermer le groupe » (à une date, avec un motif : ses séances après cette date disparaissent).
 * Une saisie laissée en cours se retrouve en rouvrant la même fenêtre.
 */
@Component({
  selector: 'sh-fenetre-du-groupe',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, BrouillonRepris],
  host: { '(document:keydown.escape)': 'annuler()' },
  template: `
    <div class="voile" (click)="annuler()"></div>
    <section class="fenetre card" role="dialog" aria-modal="true" aria-labelledby="titre-de-la-fenetre">
      <div class="entete">
        <h2 id="titre-de-la-fenetre">{{ action() === 'modifier' ? 'Modifier le groupe' : 'Fermer le groupe' }}</h2>
        <button type="button" class="btn ghost fermer-la-fenetre" aria-label="Fermer la fenêtre" (click)="annuler()">✕</button>
      </div>
      @if (fiche(); as fiche) {
        <p class="sous-titre">{{ fiche.nom }} · {{ fiche.matiere }}</p>
        @if (action() === 'modifier') {
          <form class="formulaire" [formGroup]="formulaire" (ngSubmit)="enregistrer()" novalidate>
            <sh-brouillon-repris [brouillon]="brouillonDeLaModification" texte="Vous reprenez la modification que vous aviez commencée." />
            <div class="champs">
              <label class="f">
                Nom du groupe
                <input formControlName="nom" autocomplete="off" />
              </label>
              <label class="f">
                Capacité (facultatif)
                <input formControlName="capacite" inputmode="numeric" autocomplete="off" />
              </label>
              @if (fiche.type === 'Etude') {
                <label class="f">
                  Prix du Mois (TND)
                  <input formControlName="prix" inputmode="decimal" autocomplete="off" />
                </label>
              }
            </div>
            @if (fiche.type === 'Etude') {
              <p class="aide">Le nouveau prix vaut pour les Mois qui n'ont pas encore commencé.</p>
            }
            <div class="actions">
              <button type="button" class="btn" [disabled]="envoi()" (click)="annuler()">Annuler</button>
              <button type="submit" class="btn primary" [disabled]="envoi()">Enregistrer</button>
            </div>
          </form>
        } @else {
          <form class="formulaire" [formGroup]="fermeture" (ngSubmit)="fermer()" novalidate>
            <sh-brouillon-repris [brouillon]="brouillonDeLaFermeture" texte="Vous reprenez la fermeture que vous aviez commencée." />
            <div class="champs">
              <label class="f">
                Date de fermeture
                <input type="date" formControlName="dateDeFermeture" />
              </label>
              <label class="f">
                Motif
                <input formControlName="motif" autocomplete="off" placeholder="Trop peu d'élèves" />
              </label>
            </div>
            <p class="aide">{{ avertissementDeFermeture() }}</p>
            <div class="actions">
              <button type="button" class="btn" [disabled]="envoi()" (click)="annuler()">Annuler</button>
              <button type="submit" class="btn primary danger" [disabled]="envoi()">Fermer le groupe</button>
            </div>
          </form>
        }
      } @else if (!erreur()) {
        <p class="aide">Chargement…</p>
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </section>
  `,
  styleUrl: './groupes.css',
  styles: `
    :host { position: fixed; inset: 0; z-index: 50; display: grid; place-items: center; padding: 16px; }
    .voile { position: absolute; inset: 0; background: rgb(15 23 42 / 0.45); }
    .fenetre { position: relative; width: min(560px, 100%); max-height: calc(100vh - 32px); overflow: auto; margin: 0; padding: 20px 22px; border-radius: 14px; }
    .entete { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .champs { grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); max-width: none; }
    .actions { margin-top: 16px; }
    .entete h2 { margin: 0; font-size: 18px; }
    .fermer-la-fenetre { padding: 4px 10px; }
    .sous-titre { margin: 4px 0 14px; color: var(--ink-2); }
    .aide { margin: 8px 0 0; color: var(--ink-2); }
    .btn.primary.danger { background: var(--bad); border-color: var(--bad); color: #fff; }
  `,
})
export class FenetreDuGroupe implements OnInit {
  private readonly http = inject(HttpClient);

  readonly groupeId = input.required<string>();
  readonly action = input.required<ActionSurLeGroupe>();
  /** Le groupe est enregistré ou fermé : « Groupe enregistré », « Groupe fermé ». */
  readonly fait = output<string>();
  /** La fenêtre se referme sans rien changer. */
  readonly abandon = output<void>();

  protected readonly fiche = signal<FicheDuGroupe | null>(null);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);

  protected readonly formulaire = new FormGroup({
    nom: new FormControl('', { nonNullable: true }),
    capacite: new FormControl('', { nonNullable: true }),
    prix: new FormControl('', { nonNullable: true, validators: requis() }),
  });

  protected readonly fermeture = new FormGroup({
    dateDeFermeture: new FormControl('', { nonNullable: true, validators: requis() }),
    motif: new FormControl('', { nonNullable: true, validators: requis() }),
  });

  /** Une saisie laissée en cours (autre page, rechargement) se retrouve en rouvrant la même fenêtre. */
  protected readonly brouillonDeLaModification = garderLeBrouillon(() => `groupe-${this.groupeId()}`, this.formulaire, {
    aGarder: () => this.action() === 'modifier' && this.fiche() !== null,
  });
  protected readonly brouillonDeLaFermeture = garderLeBrouillon(() => `fermeture-${this.groupeId()}`, this.fermeture, {
    aGarder: () => this.action() === 'fermer' && this.fiche() !== null,
  });

  private readonly dateDeFermeture = toSignal(this.fermeture.controls.dateDeFermeture.valueChanges, { initialValue: '' });

  /** « Les 3 séances prévues après le 02/10/2027 seront supprimées. » */
  protected readonly avertissementDeFermeture = computed(() => {
    const fiche = this.fiche();
    const date = this.dateDeFermeture();
    if (!fiche || !date) return '';
    const dates = [...fiche.mois.flatMap((m) => m.seances), ...fiche.seances].map((s) => s.date);
    const supprimees = dates.filter((d) => d > date).length;
    const le = formatDate(date, 'dd/MM/yyyy', FRANCAIS);
    if (supprimees === 0) return `Aucune séance prévue après le ${le}.`;
    if (supprimees === 1) return `La séance prévue après le ${le} sera supprimée.`;
    return `Les ${supprimees} séances prévues après le ${le} seront supprimées.`;
  });

  async ngOnInit(): Promise<void> {
    try {
      const fiche = await firstValueFrom(this.http.get<FicheDuGroupe>(`/api/groupes/${this.groupeId()}`));
      this.formulaire.setValue({
        nom: fiche.nom,
        capacite: fiche.capacite === null ? '' : String(fiche.capacite),
        prix: fiche.prixDuMois === null ? '' : ecrireUnMontant(fiche.prixDuMois),
      });
      this.fermeture.setValue({ dateDeFermeture: dateDuJourLocale(), motif: '' });
      this.fiche.set(fiche);
      if (this.action() === 'modifier') this.brouillonDeLaModification.reprendre();
      else this.brouillonDeLaFermeture.reprendre();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected annuler(): void {
    if (this.envoi()) return;
    this.brouillonDeLaModification.oublier();
    this.brouillonDeLaFermeture.oublier();
    this.abandon.emit();
  }

  protected async enregistrer(): Promise<void> {
    if (this.envoi()) return;
    const valeurs = this.formulaire.getRawValue();
    // Le prix d'une Révision ou d'une Formation est fixé à la création : seul celui d'une Étude se change.
    const etude = this.fiche()?.type === 'Etude';
    const prix = etude ? lireUnNombre(valeurs.prix) : null;
    const capacite = lireUnNombre(valeurs.capacite);
    if (etude && (prix === null || prix === undefined)) return this.erreur.set(PRIX_ILLISIBLE);
    if (capacite === undefined || (capacite !== null && !Number.isInteger(capacite))) return this.erreur.set(CAPACITE_ILLISIBLE);

    await this.envoyer(
      () => this.http.put<FicheDuGroupe>(`/api/groupes/${this.groupeId()}`, { nom: valeurs.nom.trim(), capacite, prixDuMois: prix }),
      'Groupe enregistré',
    );
  }

  protected async fermer(): Promise<void> {
    if (this.envoi()) return;
    const { dateDeFermeture, motif } = this.fermeture.getRawValue();
    if (!dateDeFermeture || !motif.trim()) return this.erreur.set(FERMETURE_INCOMPLETE);

    await this.envoyer(
      () => this.http.post<FicheDuGroupe>(`/api/groupes/${this.groupeId()}/fermeture`, { date: dateDeFermeture, motif: motif.trim() }),
      'Groupe fermé',
    );
  }

  private async envoyer(requete: () => ReturnType<HttpClient['put']>, succes: string): Promise<void> {
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      await firstValueFrom(requete());
      this.brouillonDeLaModification.oublier();
      this.brouillonDeLaFermeture.oublier();
      this.fait.emit(succes);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
