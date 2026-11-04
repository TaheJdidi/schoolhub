import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { DELAI_DE_RECHERCHE } from '../../cadre/recherche-d-eleves';
import { messageDErreur } from '../../socle/erreurs';
import { EleveTrouve, nombreDEleves } from './eleve';

/**
 * ELV-01 et ELV-02 : la page « Élèves ». La liste se charge une fois ; « Filtrer la liste » utilise la même recherche
 * que l'en-tête, sur le serveur, après une courte pause. Un filtre vide remet toute la liste.
 */
@Component({
  selector: 'sh-eleves',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Élèves</h1>
        <p class="sub">{{ sousTitre() }}</p>
      </div>
      <a class="btn primary" routerLink="/eleves/nouveau">Ajouter un élève</a>
    </div>

    <section class="card carte">
      <label class="f">
        Filtrer la liste
        <input class="inp" (input)="filtrer($event)" placeholder="Nom, prénom ou téléphone" aria-label="Filtrer la liste" autocomplete="off" />
      </label>
      <div class="liste">
        @for (eleve of visibles(); track eleve.id) {
          <a class="ligne-eleve" [routerLink]="['/eleves', eleve.id]">
            <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
            <small>{{ eleve.niveau }}</small>
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

  protected readonly visibles = computed(() => this.trouves() ?? this.tous());
  protected readonly sousTitre = computed(() => (this.charge() ? nombreDEleves(this.tous().length) : ''));

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.minuterie));
  }

  async ngOnInit(): Promise<void> {
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
      return;
    }
    this.minuterie = setTimeout(() => this.chercher(texte), this.delai);
  }

  private async chercher(recherche: string): Promise<void> {
    const numero = ++this.derniere;
    try {
      const eleves = await firstValueFrom(this.http.get<EleveTrouve[]>('/api/eleves', { params: { recherche } }));
      if (numero === this.derniere) this.trouves.set(eleves);
    } catch (erreur) {
      if (numero === this.derniere) this.erreur.set(messageDErreur(erreur));
    }
  }
}
