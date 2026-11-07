import { ActionsDeListe } from '../../socle/actions-de-liste';
import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderLeBrouillon } from '../../socle/brouillons';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant, lireUnNombre } from '../../socle/montants';
import { dateDuJourLocale } from '../groupes/groupe';

/** Un achat (GET /api/achats). */
export interface AchatDecrit {
  id: string;
  jour: string;
  libelle: string;
  categorie: 'Materiel' | 'Service';
  montant: number;
  fournisseur: string | null;
  payeDepuisLaCaisse: boolean;
  auteur: string;
  aUneFacture: boolean;
  modifiable: boolean;
  pourquoiPasModifiable: string | null;
}

/** RHA-06 : les achats et leurs totaux. */
export interface AchatsEtTotaux {
  achats: AchatDecrit[];
  totalMateriel: number;
  totalService: number;
  total: number;
}

const CATEGORIES = { Materiel: 'Matériel', Service: 'Service' } as const;

/**
 * RHA-04 à RHA-06 : les achats. Les filtres (mois ou année, catégorie) et les totaux ; « Nouvel achat », avec « Payé
 * depuis ma caisse » (une sortie de caisse) et la facture à joindre ; « Modifier », « Supprimer », « Refaire cet achat ».
 */
@Component({
  selector: 'sh-achats',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, ActionsDeListe, BrouillonRepris],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Achats</h1>
        <p class="sub">Les dépenses du centre. Un achat payé depuis votre caisse est une sortie de caisse.</p>
      </div>
      @if (!ouvert()) {
        <button type="button" class="btn primary" (click)="nouveau()">Nouvel achat</button>
      }
    </div>

    @if (ouvert()) {
      <form class="card carte formulaire" [formGroup]="formulaire" (ngSubmit)="enregistrer()" novalidate>
        <h2 class="titre">{{ enModification() ? "Modifier l'achat" : 'Nouvel achat' }}</h2>
        <sh-brouillon-repris [brouillon]="brouillon" texte="Vous reprenez l'achat que vous aviez commencé. La facture est à rejoindre." action="Abandonner" />
        <div class="champs">
          <label class="f">Date <input type="date" formControlName="jour" /></label>
          <label class="f">Libellé <input formControlName="libelle" autocomplete="off" placeholder="Ramettes de papier" /></label>
          <label class="f">
            Catégorie
            <select class="inp" formControlName="categorie">
              <option value="Materiel">Matériel</option>
              <option value="Service">Service</option>
            </select>
          </label>
          <label class="f">Montant (TND) <input formControlName="montant" inputmode="decimal" autocomplete="off" placeholder="45,000" /></label>
          <label class="f">Fournisseur (facultatif) <input formControlName="fournisseur" autocomplete="off" /></label>
          <label class="f">
            Facture (facultative)
            <input type="file" accept="image/jpeg,image/png,image/webp,application/pdf" (change)="choisirLaFacture($event)" />
          </label>
        </div>
        <label class="caisse"><input type="checkbox" formControlName="payeDepuisLaCaisse" /> Payé depuis ma caisse</label>
        <div class="actions">
          <button type="button" class="btn" [disabled]="envoi()" (click)="fermer()">Annuler</button>
          <button type="submit" class="btn primary" [disabled]="envoi()">Enregistrer</button>
        </div>
      </form>
    }

    <section class="card carte">
      <!-- « Imprimer » et « Exporter » à gauche, les filtres en haut à droite. -->
      <div class="barre">
      <sh-actions-de-liste titre="Achats" [nombre]="liste()?.achats?.length ?? 0" exporter="/api/exports/achats" [filtres]="filtresCourants()" />
      <div class="filtres">
        <label class="f">
          Période
          <select class="inp" #periode (change)="filtrer(periode.value, valeur.value, categorie.value)">
            <option value="mois">Un mois</option>
            <option value="annee">Une année</option>
          </select>
        </label>
        <label class="f">
          {{ parAnnee() ? 'Année' : 'Mois' }}
          <input class="inp" #valeur [type]="parAnnee() ? 'number' : 'month'" [value]="parAnnee() ? annee : mois" (change)="filtrer(periode.value, valeur.value, categorie.value)" />
        </label>
        <label class="f">
          Catégorie
          <select class="inp" #categorie (change)="filtrer(periode.value, valeur.value, categorie.value)">
            <option value="">Toutes</option>
            <option value="Materiel">Matériel</option>
            <option value="Service">Service</option>
          </select>
        </label>
      </div>
      </div>
      @if (liste(); as liste) {
        <div class="defile">
          <table>
            <thead>
              <tr><th>Date</th><th>Libellé</th><th>Catégorie</th><th class="r">Montant</th><th></th></tr>
            </thead>
            <tbody>
              @for (achat of liste.achats; track achat.id) {
                <tr>
                  <td class="num">{{ date(achat.jour) }}</td>
                  <td>
                    {{ achat.libelle }}
                    @if (achat.payeDepuisLaCaisse) {
                      <span class="pill warn">sortie de caisse</span>
                    }
                    <br /><small>{{ achat.fournisseur ? achat.fournisseur + ' · ' : '' }}{{ achat.auteur }}</small>
                  </td>
                  <td>{{ libelleDe(achat.categorie) }}</td>
                  <td class="r num">{{ montant(achat.montant) }}</td>
                  <td class="boutons">
                    @if (achat.aUneFacture) {
                      <a class="btn sm" [href]="'/api/achats/' + achat.id + '/facture'" target="_blank" rel="noopener">Facture</a>
                    }
                    <button type="button" class="btn sm" (click)="refaire(achat)">Refaire</button>
                    @if (achat.modifiable) {
                      <button type="button" class="btn sm" (click)="modifier(achat)">Modifier</button>
                      <button type="button" class="btn sm" [disabled]="envoi()" (click)="supprimer(achat)">Supprimer</button>
                    }
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="5" class="vide">Aucun achat sur cette période.</td></tr>
              }
            </tbody>
            <tfoot>
              <tr><td colspan="3">Matériel</td><td class="r num">{{ montant(liste.totalMateriel) }}</td><td></td></tr>
              <tr><td colspan="3">Service</td><td class="r num">{{ montant(liste.totalService) }}</td><td></td></tr>
              <tr class="total"><td colspan="3">Total</td><td class="r num">{{ montant(liste.total) }} TND</td><td></td></tr>
            </tfoot>
          </table>
        </div>
      }
    </section>

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
    .champs { display: grid; max-width: 880px; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 10px; align-items: end; }
    .barre { display: flex; flex-wrap: wrap; align-items: end; justify-content: space-between; gap: 10px; }
    .filtres { display: flex; flex-wrap: wrap; justify-content: flex-end; align-items: end; gap: 10px; margin-left: auto; }
    .filtres .f { width: 170px; }
    .caisse { display: inline-flex; gap: 8px; align-items: center; font-weight: 500; }
    .actions { display: flex; gap: 8px; justify-content: flex-end; }
    .defile { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    th, td { text-align: left; padding: 8px 6px; border-top: 1px solid var(--line); vertical-align: top; }
    th { color: var(--ink-2); font-weight: 500; border-top: 0; }
    td small { color: var(--ink-2); }
    .r { text-align: right; white-space: nowrap; }
    .boutons { display: flex; gap: 6px; flex-wrap: wrap; justify-content: flex-end; }
    tfoot td { color: var(--ink-2); }
    tfoot .total td { color: var(--ink); font-weight: 600; }
  `,
})
export class Achats implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly aujourdhui = dateDuJourLocale();

  protected readonly montant = ecrireUnMontant;
  protected readonly mois = this.aujourdhui.slice(0, 7);
  protected readonly annee = this.aujourdhui.slice(0, 4);
  protected readonly liste = signal<AchatsEtTotaux | null>(null);
  protected readonly ouvert = signal(false);
  protected readonly enModification = signal<string | null>(null);
  protected readonly parAnnee = signal(false);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);
  protected readonly filtresCourants = signal<Record<string, string>>({ mois: this.mois });
  private facture: File | null = null;

  protected readonly formulaire = new FormGroup({
    jour: new FormControl(this.aujourdhui, { nonNullable: true }),
    libelle: new FormControl('', { nonNullable: true }),
    categorie: new FormControl<'Materiel' | 'Service'>('Materiel', { nonNullable: true }),
    montant: new FormControl('', { nonNullable: true }),
    fournisseur: new FormControl('', { nonNullable: true }),
    payeDepuisLaCaisse: new FormControl(false, { nonNullable: true }),
  });

  /** Un achat commencé, puis laissé pour une autre page, se retrouve en revenant (sauf la facture, à rejoindre). */
  protected readonly brouillon = garderLeBrouillon('achat', this.formulaire, {
    aGarder: () => this.ouvert(),
    etat: {
      lire: () => ({ ouvert: this.ouvert(), enModification: this.enModification() }),
      ecrire: (etat) => {
        this.ouvert.set(etat.ouvert);
        this.enModification.set(etat.enModification);
      },
    },
  });

  async ngOnInit(): Promise<void> {
    await this.charger();
    this.brouillon.reprendre();
  }

  protected libelleDe(categorie: 'Materiel' | 'Service'): string {
    return CATEGORIES[categorie];
  }

  protected nouveau(): void {
    this.formulaire.reset({ jour: this.aujourdhui, libelle: '', categorie: 'Materiel', montant: '', fournisseur: '', payeDepuisLaCaisse: false });
    this.enModification.set(null);
    this.facture = null;
    this.ouvert.set(true);
  }

  /** RHA-05 : un nouvel achat avec le même libellé, la même catégorie, le même montant et le même fournisseur, daté du jour. */
  protected refaire(achat: AchatDecrit): void {
    this.nouveau();
    this.formulaire.patchValue({
      libelle: achat.libelle,
      categorie: achat.categorie,
      montant: ecrireUnMontant(achat.montant),
      fournisseur: achat.fournisseur ?? '',
    });
  }

  protected modifier(achat: AchatDecrit): void {
    this.formulaire.reset({
      jour: achat.jour,
      libelle: achat.libelle,
      categorie: achat.categorie,
      montant: ecrireUnMontant(achat.montant),
      fournisseur: achat.fournisseur ?? '',
      payeDepuisLaCaisse: achat.payeDepuisLaCaisse,
    });
    this.enModification.set(achat.id);
    this.facture = null;
    this.ouvert.set(true);
  }

  protected fermer(): void {
    this.ouvert.set(false);
    this.enModification.set(null);
  }

  protected choisirLaFacture(evenement: Event): void {
    this.facture = (evenement.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected async enregistrer(): Promise<void> {
    const valeurs = this.formulaire.getRawValue();
    const montant = lireUnNombre(valeurs.montant);
    const saisie = { ...valeurs, montant: typeof montant === 'number' ? montant : null, jour: valeurs.jour || null };
    const id = this.enModification();
    this.envoi.set(true);
    this.erreur.set(null);
    this.succes.set(null);
    try {
      const achat = await firstValueFrom(
        id ? this.http.put<AchatDecrit>(`/api/achats/${id}`, saisie) : this.http.post<AchatDecrit>('/api/achats', saisie),
      );
      if (this.facture) {
        const donnees = new FormData();
        donnees.append('facture', this.facture);
        await firstValueFrom(this.http.put<AchatDecrit>(`/api/achats/${achat.id}/facture`, donnees));
      }
      this.brouillon.oublier();
      this.succes.set(achat.payeDepuisLaCaisse ? `L'achat « ${achat.libelle} » est noté en sortie de caisse.` : `L'achat « ${achat.libelle} » est enregistré.`);
      this.fermer();
      await this.charger();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  protected async supprimer(achat: AchatDecrit): Promise<void> {
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      await firstValueFrom(this.http.delete(`/api/achats/${achat.id}`));
      this.succes.set(`L'achat « ${achat.libelle} » est supprimé.`);
      await this.charger();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  protected async filtrer(periode: string, valeur: string, categorie: string): Promise<void> {
    const parAnnee = periode === 'annee';
    if (parAnnee !== this.parAnnee()) {
      this.parAnnee.set(parAnnee);
      valeur = parAnnee ? this.annee : this.mois;
    }
    const filtres: Record<string, string> = parAnnee ? { annee: valeur || this.annee } : { mois: valeur || this.mois };
    if (categorie) filtres['categorie'] = categorie;
    this.filtresCourants.set(filtres);
    await this.charger();
  }

  /** « 27/11/2027 ». */
  protected date(texte: string): string {
    return formatDate(texte, 'dd/MM/yyyy', FRANCAIS);
  }

  private async charger(): Promise<void> {
    try {
      this.liste.set(await firstValueFrom(this.http.get<AchatsEtTotaux>('/api/achats', { params: this.filtresCourants() })));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }
}
