import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, InjectionToken, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../socle/erreurs';
import { Icone } from '../socle/icone';

/** La pause après la frappe avant de chercher, en millisecondes : une requête par mot tapé, pas par lettre. */
export const DELAI_DE_RECHERCHE = new InjectionToken<number>('DELAI_DE_RECHERCHE', { factory: () => 250 });

/** Un élève trouvé (GET /api/eleves?recherche=…). */
interface EleveTrouve {
  id: string;
  prenom: string;
  nom: string;
  niveau: string;
}

const CARACTERES_AU_MOINS = 2;
const RESULTATS_AU_PLUS = 8;

/**
 * ELV-01 : la recherche d'un élève, en haut de chaque écran. Dès 2 caractères, après une courte pause, les élèves
 * s'affichent pendant la frappe ; un clic ouvre la fiche et vide le champ ; Échap ferme la liste. Une réponse arrivée
 * après une recherche plus récente est ignorée.
 */
@Component({
  selector: 'sh-recherche-d-eleves',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icone],
  template: `
    <div class="recherche" (keydown.escape)="fermer()">
      <sh-icone nom="search" />
      <input
        type="search"
        [value]="texte()"
        (input)="taper($event)"
        placeholder="Chercher un élève ou le téléphone d'un parent"
        aria-label="Chercher un élève"
        autocomplete="off"
      />
      @if (ouverte()) {
        <div class="resultats">
          @for (eleve of resultats(); track eleve.id) {
            <a class="resultat" [routerLink]="['/eleves', eleve.id]" (click)="choisir()">
              <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
              <small>{{ eleve.niveau }}</small>
            </a>
          } @empty {
            <p class="vide">{{ erreur() ?? 'Aucun élève trouvé' }}</p>
          }
        </div>
      }
    </div>
  `,
  styles: `
    :host { display: block; position: relative; min-width: 0; }
    .recherche { display: flex; align-items: center; gap: 8px; padding: 0 10px; border: 1px solid var(--line-2); border-radius: var(--r); background: var(--surface); }
    .recherche:focus-within { border-color: var(--accent); }
    input { flex: 1; min-width: 0; border: 0; outline: 0; background: none; font: inherit; font-size: 14px; color: var(--ink); padding: 8px 0; }
    .resultats { position: absolute; left: 0; right: 0; top: calc(100% + 6px); z-index: 30; display: grid; max-height: 60vh; overflow-y: auto;
      border: 1px solid var(--line-2); border-radius: 10px; background: var(--surface); box-shadow: var(--shadow); }
    .resultat { display: grid; padding: 9px 12px; color: var(--ink); text-decoration: none; border-bottom: 1px solid var(--line); }
    .resultat:last-child { border-bottom: 0; }
    .resultat:hover, .resultat:focus { background: var(--sunk); }
    .resultat small { color: var(--ink-2); font-size: 12.5px; }
    .vide { margin: 0; padding: 10px 12px; color: var(--ink-2); }
  `,
})
export class RechercheDEleves {
  private readonly http = inject(HttpClient);
  private readonly delai = inject(DELAI_DE_RECHERCHE);

  protected readonly texte = signal('');
  protected readonly resultats = signal<EleveTrouve[]>([]);
  protected readonly ouverte = signal(false);
  protected readonly erreur = signal<string | null>(null);
  private minuterie: ReturnType<typeof setTimeout> | undefined;
  /** Le numéro de la dernière recherche lancée : une réponse plus ancienne arrive trop tard. */
  private derniere = 0;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.minuterie));
  }

  protected taper(evenement: Event): void {
    const texte = (evenement.target as HTMLInputElement).value;
    this.texte.set(texte);
    clearTimeout(this.minuterie);
    if (texte.trim().length < CARACTERES_AU_MOINS) {
      this.derniere++;
      this.ouverte.set(false);
      return;
    }
    this.minuterie = setTimeout(() => this.chercher(texte.trim()), this.delai);
  }

  protected choisir(): void {
    this.texte.set('');
    this.fermer();
  }

  protected fermer(): void {
    this.derniere++;
    this.ouverte.set(false);
  }

  private async chercher(recherche: string): Promise<void> {
    const numero = ++this.derniere;
    try {
      const eleves = await firstValueFrom(this.http.get<EleveTrouve[]>('/api/eleves', { params: { recherche } }));
      if (numero !== this.derniere) return;
      this.resultats.set(eleves.slice(0, RESULTATS_AU_PLUS));
      this.erreur.set(null);
    } catch (erreur) {
      if (numero !== this.derniere) return;
      this.resultats.set([]);
      this.erreur.set(messageDErreur(erreur));
    }
    this.ouverte.set(true);
  }
}
