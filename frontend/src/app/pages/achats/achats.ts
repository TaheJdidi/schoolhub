import { ActionsDeListe } from '../../socle/actions-de-liste';
import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderLeBrouillon } from '../../socle/brouillons';
import { Icone } from '../../socle/icone';
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
  imports: [ReactiveFormsModule, ActionsDeListe, BrouillonRepris, Icone],
  // Un clic ailleurs referme le menu « ⋮ » ouvert.
  host: { '(document:click)': 'fermerLesMenus($event)' },
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

    @if (liste(); as liste) {
      <!-- Le résumé de la période, en haut (croquis A) : le total, chaque catégorie, et ce qui est sorti de la caisse. -->
      <div class="chiffres">
        <div>
          <span>Total {{ parAnnee() ? "de l'année" : 'du mois' }}</span>
          <b class="num">{{ montant(liste.total) }} <small>TND</small></b>
        </div>
        <div>
          <span>Matériel</span>
          <b class="num">{{ montant(liste.totalMateriel) }}</b>
        </div>
        <div>
          <span>Service</span>
          <b class="num">{{ montant(liste.totalService) }}</b>
        </div>
        <div class="caisse-sortie">
          <span>Sorti de la caisse</span>
          <b class="num">{{ montant(sortiDeLaCaisse()) }}</b>
        </div>
      </div>
    }

    <section class="card carte">
      <!-- Les filtres en un clic : Mois ou Année, ‹ la période ›, la catégorie en puces ; « Imprimer » et « Exporter » à droite. -->
      <div class="barre">
        <div class="filtres">
          <div class="segments" role="group" aria-label="Période">
            <button type="button" [attr.aria-pressed]="!parAnnee()" (click)="choisirLaPeriode(false)">Mois</button>
            <button type="button" [attr.aria-pressed]="parAnnee()" (click)="choisirLaPeriode(true)">Année</button>
          </div>
          <div class="navigation">
            <button type="button" [attr.aria-label]="parAnnee() ? 'Année précédente' : 'Mois précédent'" (click)="decaler(-1)"><sh-icone nom="chevron-left" /></button>
            <span class="periode" aria-live="polite">{{ libelleDeLaPeriode() }}</span>
            <button type="button" [attr.aria-label]="parAnnee() ? 'Année suivante' : 'Mois suivant'" (click)="decaler(1)"><sh-icone nom="chevron-right" /></button>
          </div>
          <div class="puces" role="group" aria-label="Catégorie">
            <button type="button" [attr.aria-pressed]="categorie() === ''" (click)="choisirLaCategorie('')">Toutes</button>
            <button type="button" [attr.aria-pressed]="categorie() === 'Materiel'" (click)="choisirLaCategorie('Materiel')">Matériel</button>
            <button type="button" [attr.aria-pressed]="categorie() === 'Service'" (click)="choisirLaCategorie('Service')">Service</button>
          </div>
        </div>
        <sh-actions-de-liste titre="Achats" [nombre]="liste()?.achats?.length ?? 0" exporter="/api/exports/achats" [filtres]="filtresCourants()" />
      </div>
      @if (liste(); as liste) {
        @if (liste.achats.length === 0) {
          <div class="vide">
            <span class="icone-du-vide"><sh-icone nom="shopping-cart" /></span>
            <b>Aucun achat en {{ libelleDeLaPeriode().toLowerCase() }}</b>
            <small>Les dépenses du centre apparaîtront ici.</small>
            @if (!ouvert()) {
              <button type="button" class="btn primary" (click)="nouveau()"><sh-icone nom="plus" />Nouvel achat</button>
            }
          </div>
        } @else {
        <div>
          <table>
            <thead>
              <tr><th>Date</th><th>Libellé</th><th class="categorie">Catégorie</th><th class="r">Montant</th><th class="menu-col" aria-label="Actions"></th></tr>
            </thead>
            <tbody>
              @for (achat of liste.achats; track achat.id) {
                <tr>
                  <td class="num date">{{ date(achat.jour) }}</td>
                  <td>
                    <span class="libelle">{{ achat.libelle }}</span>
                    @if (achat.payeDepuisLaCaisse) {
                      <span class="pill warn">sortie de caisse</span>
                    }
                    <br /><small>{{ achat.fournisseur ? achat.fournisseur + ' · ' : '' }}{{ achat.auteur }}</small>
                  </td>
                  <td class="categorie"><span class="badge" [class.service]="achat.categorie === 'Service'">{{ libelleDe(achat.categorie) }}</span></td>
                  <td class="r num">{{ montant(achat.montant) }}</td>
                  <td class="menu-col">
                    <!-- Les actions de la ligne, rangées dans un menu « ⋮ » pour laisser la place au tableau. -->
                    <details class="menu" #menu>
                      <summary class="btn sm ghost" [attr.aria-label]="'Actions pour « ' + achat.libelle + ' »'"><sh-icone nom="ellipsis-vertical" /></summary>
                      <div class="liste-menu" role="menu">
                        @if (achat.aUneFacture) {
                          <a role="menuitem" [href]="'/api/achats/' + achat.id + '/facture'" target="_blank" rel="noopener" (click)="menu.open = false">Voir la facture</a>
                        }
                        <button type="button" role="menuitem" (click)="menu.open = false; refaire(achat)">Refaire</button>
                        @if (achat.modifiable) {
                          <button type="button" role="menuitem" (click)="menu.open = false; modifier(achat)">Modifier</button>
                          <button type="button" role="menuitem" class="danger" [disabled]="envoi()" (click)="menu.open = false; supprimer(achat)">Supprimer</button>
                        }
                      </div>
                    </details>
                  </td>
                </tr>
              }
            </tbody>
            <tfoot>
              <tr class="fin">
                <td colspan="2">Total · {{ liste.achats.length }} achat{{ liste.achats.length > 1 ? 's' : '' }}</td>
                <td class="categorie"></td>
                <td class="r num">{{ montant(liste.total) }} <small>TND</small></td>
                <td></td>
              </tr>
            </tfoot>
          </table>
        </div>
        }
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
    .chiffres { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; }
    /* Comme les chiffres de la Bibliothèque : sur fond gris, en grand. */
    .chiffres div { display: grid; gap: 4px; padding: 14px 18px; border-radius: 14px; background: var(--sunk); }
    .chiffres span { color: var(--ink-2); font-size: 13px; }
    .chiffres b { font-family: var(--font-display); font-size: 24px; font-weight: 600; line-height: 1.2; }
    .chiffres b small { font-size: 13px; color: var(--ink-2); font-weight: 500; }
    .chiffres .caisse-sortie b { color: var(--warn); }
    /* La barre de filtres (croquis A) : des boutons d'un clic, pas de listes déroulantes. */
    .barre { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px; margin-bottom: 10px; }
    .filtres { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .segments { display: inline-flex; padding: 3px; border-radius: 10px; background: var(--sunk); }
    .segments button { padding: 5px 12px; border: 0; border-radius: 8px; background: none; color: var(--ink-2); font: inherit; font-size: 13px; font-weight: 500; cursor: pointer; }
    .segments button[aria-pressed='true'] { background: var(--surface); color: var(--ink); box-shadow: 0 1px 2px rgb(0 0 0 / 12%); }
    .navigation { display: inline-flex; align-items: center; border: 1px solid var(--line-2); border-radius: 10px; overflow: hidden; background: var(--surface); }
    .navigation button { display: grid; place-items: center; width: 32px; height: 32px; border: 0; background: none; color: var(--ink-2); cursor: pointer; }
    .navigation button:hover { background: var(--sunk); color: var(--ink); }
    .periode { min-width: 130px; padding: 0 10px; text-align: center; font-family: var(--font-display); font-weight: 600; font-size: 13.5px; border-inline: 1px solid var(--line); line-height: 32px; }
    .puces { display: inline-flex; flex-wrap: wrap; gap: 6px; }
    .puces button { padding: 5px 12px; border: 1px solid var(--line-2); border-radius: 999px; background: var(--surface); color: var(--ink-2); font: inherit; font-size: 13px; cursor: pointer; }
    .puces button[aria-pressed='true'] { border-color: var(--accent); background: var(--accent); color: var(--on-accent); font-weight: 500; }
    .vide { display: grid; justify-items: center; gap: 6px; padding: 28px 16px; text-align: center; }
    .vide b { font-family: var(--font-display); font-size: 15px; font-weight: 600; }
    .vide small { color: var(--ink-2); font-size: 13px; }
    .vide .btn { margin-top: 6px; }
    .vide .btn svg.lucide { width: 16px; height: 16px; }
    .icone-du-vide { display: grid; place-items: center; width: 46px; height: 46px; border-radius: 50%; background: var(--sunk); color: var(--ink-3); }
    .icone-du-vide svg.lucide { width: 22px; height: 22px; }
    .libelle { font-weight: 500; }
    .date { font-weight: 500; }
    .badge { display: inline-block; padding: 2px 9px; border-radius: 999px; font-size: 12px; font-weight: 500; background: var(--accent-soft); color: var(--accent-ink); }
    .badge.service { background: var(--sunk); color: var(--ink-2); }
    .caisse { display: inline-flex; gap: 8px; align-items: center; font-weight: 500; }
    .actions { display: flex; gap: 8px; justify-content: flex-end; }
    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    th, td { text-align: left; padding: 10px 10px; border-top: 1px solid var(--line); vertical-align: middle; }
    th { color: var(--ink-3); font-size: 11.5px; font-weight: 600; letter-spacing: 0.05em; text-transform: uppercase; background: var(--bg); border-top: 0; }
    tbody tr:hover { background: var(--bg); }
    td small { color: var(--ink-2); }
    th:first-child, td:first-child { width: 96px; white-space: nowrap; }
    .categorie { width: 120px; }
    .r { text-align: right; white-space: nowrap; width: 110px; }
    .menu-col { width: 44px; text-align: right; }
    tfoot td { border-top: 1px solid var(--line-2); font-weight: 600; background: var(--bg); padding-block: 12px; }
    tfoot .num { font-size: 16px; }
    tfoot small { font-family: var(--font-body); font-size: 13px; color: var(--ink-2); font-weight: 500; }
    .menu { position: relative; display: inline-block; }
    .menu summary { list-style: none; padding: 4px 6px; color: var(--ink-2); }
    .menu summary::-webkit-details-marker { display: none; }
    .liste-menu { position: absolute; right: 0; top: calc(100% + 4px); z-index: 10; min-width: 160px; display: grid; padding: 4px;
      background: var(--surface); border: 1px solid var(--line-2); border-radius: var(--r); box-shadow: 0 6px 18px rgb(0 0 0 / 12%); }
    .liste-menu a, .liste-menu button { display: block; text-align: left; padding: 7px 10px; border: 0; border-radius: 6px; background: none;
      color: var(--ink); font: inherit; text-decoration: none; cursor: pointer; }
    .liste-menu a:hover, .liste-menu button:hover { background: var(--sunk); }
    .liste-menu .danger { color: var(--bad); }
    .liste-menu button:disabled { opacity: .55; cursor: not-allowed; }
    /* Sur un téléphone, la catégorie se lit déjà dans les chiffres du haut : la colonne laisse la place. */
    @media (max-width: 640px) {
      .categorie { display: none; }
      th:first-child, td:first-child { width: auto; }
      th, td { padding: 8px 4px; }
      .filtres { width: 100%; }
      .periode { min-width: 0; flex: 1; }
    }
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
  /** La période affichée : « 2026-11 » ou 2026, et la catégorie (vide : toutes). */
  private readonly moisChoisi = signal(this.mois);
  private readonly anneeChoisie = signal(Number(this.annee));
  protected readonly categorie = signal<'' | 'Materiel' | 'Service'>('');
  /** « Novembre 2026 », ou « 2026 ». */
  protected readonly libelleDeLaPeriode = computed(() => {
    if (this.parAnnee()) return String(this.anneeChoisie());
    const texte = formatDate(`${this.moisChoisi()}-01`, 'MMMM y', FRANCAIS);
    return texte.charAt(0).toUpperCase() + texte.slice(1);
  });
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

  /** Ce qui est sorti de la caisse sur la période : les achats « Payé depuis ma caisse ». */
  protected readonly sortiDeLaCaisse = computed(() =>
    (this.liste()?.achats ?? []).filter((achat) => achat.payeDepuisLaCaisse).reduce((somme, achat) => somme + achat.montant, 0),
  );

  /** Un clic hors d'un menu « ⋮ » le referme. */
  protected fermerLesMenus(evenement: Event): void {
    for (const menu of document.querySelectorAll<HTMLDetailsElement>('sh-achats details.menu[open]')) {
      if (!menu.contains(evenement.target as Node)) menu.open = false;
    }
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

  /** Mois ou Année : on repart du mois (ou de l'année) en cours. */
  protected async choisirLaPeriode(parAnnee: boolean): Promise<void> {
    if (parAnnee === this.parAnnee()) return;
    this.parAnnee.set(parAnnee);
    this.moisChoisi.set(this.mois);
    this.anneeChoisie.set(Number(this.annee));
    await this.filtrer();
  }

  /** ‹ et › : le mois (ou l'année) d'avant ou d'après. */
  protected async decaler(pas: number): Promise<void> {
    if (this.parAnnee()) {
      this.anneeChoisie.update((annee) => annee + pas);
    } else {
      const [annee, mois] = this.moisChoisi().split('-').map(Number);
      const date = new Date(annee, mois - 1 + pas, 1);
      this.moisChoisi.set(`${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`);
    }
    await this.filtrer();
  }

  protected async choisirLaCategorie(categorie: '' | 'Materiel' | 'Service'): Promise<void> {
    this.categorie.set(categorie);
    await this.filtrer();
  }

  private async filtrer(): Promise<void> {
    const filtres: Record<string, string> = this.parAnnee() ? { annee: String(this.anneeChoisie()) } : { mois: this.moisChoisi() };
    if (this.categorie()) filtres['categorie'] = this.categorie();
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
