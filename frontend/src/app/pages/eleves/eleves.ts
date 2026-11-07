import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { DELAI_DE_RECHERCHE } from '../../cadre/recherche-d-eleves';
import { ActionsDeListe } from '../../socle/actions-de-liste';
import { messageDErreur } from '../../socle/erreurs';
import { ImportDeListe } from '../../socle/import-de-liste';
import { EleveTrouve, nombreDEleves } from './eleve';

/**
 * ELV-01 et ELV-02 : la page « Élèves ». La liste se charge une fois ; « Filtrer la liste » utilise la même recherche
 * que l'en-tête, sur le serveur, après une courte pause. Un filtre vide remet toute la liste. Un niveau restreint la
 * liste ; « Imprimer » et « Exporter (Excel) » suivent les deux filtres (LST-01, LST-02).
 */
import { PastilleDeCouleur } from '../caisse/paiement';

@Component({
  selector: 'sh-eleves',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, PastilleDeCouleur, ActionsDeListe, ImportDeListe],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Élèves</h1>
        <p class="sub">{{ sousTitre() }}</p>
      </div>
      <div class="boutons-de-tete">
        <button type="button" class="btn" (click)="importOuvert.set(!importOuvert())">Importer</button>
        <a class="btn primary" routerLink="/eleves/nouveau">Ajouter un élève</a>
      </div>
    </div>

    @if (importOuvert()) {
      <sh-import-de-liste quoi="élèves" unite="élève" adresse="/api/imports/eleves" (importe)="recharger()" (ferme)="importOuvert.set(false)" />
    }

    <section class="card carte">
      <div class="filtres-eleves">
        <label class="f">
          Filtrer la liste
          <input class="inp" (input)="filtrer($event)" placeholder="Nom, prénom ou téléphone" aria-label="Filtrer la liste" autocomplete="off" />
        </label>
        <label class="f">
          Niveau
          <select class="inp" (change)="niveau.set($any($event.target).value)">
            <option value="">Tous</option>
            @for (n of niveaux(); track n) {
              <option [value]="n">{{ n }}</option>
            }
          </select>
        </label>
        <sh-actions-de-liste [titre]="titreDeLaListe()" [nombre]="visibles().length" exporter="/api/exports/eleves"
          [filtres]="{ recherche: filtre(), niveau: niveau() }" />
      </div>
      <div class="liste">
        @for (eleve of visibles(); track eleve.id) {
          <a class="ligne-eleve" [routerLink]="['/eleves', eleve.id]">
            <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
            <small>{{ eleve.niveau }} <sh-pastille-de-couleur [couleur]="eleve.couleur" /></small>
          </a>
        } @empty {
          @if (erreur(); as erreur) {
            <p class="note bad" role="alert">{{ erreur }}</p>
          } @else if (filtre()) {
            <p class="vide">Aucun élève trouvé.</p>
          } @else if (charge()) {
            <p class="vide">Aucun élève pour l'instant. Ajoutez le premier.</p>
          }
        }
      </div>
    </section>
  `,
  styleUrl: './eleves.css',
})
export class Eleves implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly delai = inject(DELAI_DE_RECHERCHE);

  private readonly tous = signal<EleveTrouve[]>([]);
  /** Les élèves trouvés par le filtre ; `null` sans filtre. */
  private readonly trouves = signal<EleveTrouve[] | null>(null);
  protected readonly charge = signal(false);
  protected readonly erreur = signal<string | null>(null);
  protected readonly filtre = signal('');
  private minuterie: ReturnType<typeof setTimeout> | undefined;
  private derniere = 0;

  protected readonly niveau = signal('');
  /** Les niveaux des élèves du centre, dans l'ordre où ils viennent. */
  protected readonly niveaux = computed(() => [...new Set(this.tous().map((e) => e.niveau))].sort((a, b) => a.localeCompare(b, 'fr')));
  protected readonly visibles = computed(() => (this.trouves() ?? this.tous()).filter((e) => !this.niveau() || e.niveau === this.niveau()));
  protected readonly titreDeLaListe = computed(() => (this.niveau() ? `Élèves de ${this.niveau()}` : 'Élèves'));
  protected readonly sousTitre = computed(() => (this.charge() ? nombreDEleves(this.tous().length) : ''));

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.minuterie));
  }

  /** LST-03 : la carte « Importer des élèves ». */
  protected readonly importOuvert = signal(false);

  async ngOnInit(): Promise<void> {
    await this.recharger();
  }

  /** La liste entière, au début et après un import. */
  protected async recharger(): Promise<void> {
    try {
      this.tous.set(await firstValueFrom(this.http.get<EleveTrouve[]>('/api/eleves')));
      this.charge.set(true);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected filtrer(evenement: Event): void {
    const texte = (evenement.target as HTMLInputElement).value.trim();
    this.filtre.set(texte);
    clearTimeout(this.minuterie);
    if (!texte) {
      this.derniere++;
      this.trouves.set(null);
      this.erreur.set(null);
      return;
    }
    this.minuterie = setTimeout(() => this.chercher(texte), this.delai);
  }

  /** Un filtre en échec vide la liste pour montrer son erreur ; le suivant qui réussit l'oublie. */
  private async chercher(recherche: string): Promise<void> {
    const numero = ++this.derniere;
    try {
      const eleves = await firstValueFrom(this.http.get<EleveTrouve[]>('/api/eleves', { params: { recherche } }));
      if (numero !== this.derniere) return;
      this.trouves.set(eleves);
      this.erreur.set(null);
    } catch (erreur) {
      if (numero !== this.derniere) return;
      this.trouves.set([]);
      this.erreur.set(messageDErreur(erreur));
    }
  }
}
