import { formatDate } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { Encaissable, RecuDecrit, SommeAEncaisser } from './recu';

/** La clé d'une somme sur la page : son inscription et sa clé. */
const cleDe = (somme: SommeAEncaisser) => `${somme.inscriptionId ?? 'assurance'}|${somme.cle}`;

/**
 * CAI-03 : encaisser un élève depuis sa fiche. « À payer » liste ce qui est dû à ce jour, coché d'office ; « Payer
 * d'avance » ajoute les sommes à venir (Mois, tranches). Chaque somme se paie en entier. « Encaisser en espèces » crée un
 * seul reçu, puis ouvre ce reçu pour le remettre.
 */
@Component({
  selector: 'sh-page-d-encaissement',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    @if (introuvable()) {
      <div class="pagehead">
        <div>
          <h1 class="h1">Élève introuvable</h1>
          <p class="sub">Cet élève n'existe pas.</p>
        </div>
      </div>
    } @else if (encaissable(); as encaissable) {
      <div class="pagehead">
        <div>
          <nav class="fil" aria-label="Fil d'Ariane">
            <a routerLink="/eleves">Élèves</a> ›
            <a [routerLink]="['/eleves', encaissable.eleve.id]">{{ encaissable.eleve.prenom }} {{ encaissable.eleve.nom }}</a> ›
          </nav>
          <h1 class="h1">Encaisser {{ encaissable.eleve.prenom }} {{ encaissable.eleve.nom }}</h1>
          <p class="sub">En espèces. Chaque somme se paie en entier.</p>
        </div>
      </div>

      <section class="card carte">
        <h2 class="titre">À payer</h2>
        @for (somme of dues(); track cle(somme)) {
          <label class="somme-a-cocher">
            <input type="checkbox" [checked]="cochees().has(cle(somme))" (change)="basculer(somme)" />
            <span class="deux">
              <b>{{ somme.libelle }}</b>
              <small>{{ somme.groupe }}{{ somme.dueLe ? ' · dû le ' + date(somme.dueLe) : '' }}</small>
            </span>
            <b class="montant">{{ montant(somme.montant) }} TND</b>
          </label>
        } @empty {
          <p class="vide">Rien à payer aujourd'hui.</p>
        }

        @if (aVenir().length > 0) {
          @if (!dAvance()) {
            <div class="actions debut">
              <button type="button" class="btn" (click)="dAvance.set(true)">Payer d'avance</button>
            </div>
          } @else {
            <h3 class="tag">Payer d'avance</h3>
            @for (somme of aVenir(); track cle(somme)) {
              <label class="somme-a-cocher">
                <input type="checkbox" [checked]="cochees().has(cle(somme))" (change)="basculer(somme)" />
                <span class="deux">
                  <b>{{ somme.libelle }}</b>
                  <small>{{ somme.groupe }}{{ somme.dueLe ? ' · dû le ' + date(somme.dueLe) : '' }}</small>
                </span>
                <b class="montant">{{ montant(somme.montant) }} TND</b>
              </label>
            }
          }
        }

        <p class="total">Total <b>{{ montant(total()) }} TND</b></p>
        <div class="actions">
          <a class="btn" [routerLink]="['/eleves', encaissable.eleve.id]">Retour</a>
          <button type="button" class="btn primary" [disabled]="envoi() || cochees().size === 0" (click)="encaisser()">Encaisser en espèces</button>
        </div>
        @if (!encaissable.aUnEmail) {
          <p class="aide">Aucun email n'est connu pour cet élève : le reçu se remettra imprimé ou par WhatsApp.</p>
        }
        @if (erreur(); as erreur) {
          <p class="note bad" role="alert">{{ erreur }}</p>
        }
      </section>
    } @else if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    .somme-a-cocher { display: flex; align-items: center; gap: 10px; padding: 9px 0; border-top: 1px solid var(--line); cursor: pointer; min-width: 0; }
    .somme-a-cocher:first-of-type { border-top: 0; }
    .deux { display: grid; flex: 1; min-width: 0; }
    .deux b { font-weight: 500; overflow-wrap: anywhere; }
    .deux small { color: var(--ink-2); font-size: 12.5px; overflow-wrap: anywhere; }
    .tag { margin: 8px 0 0; font-size: 12.5px; font-weight: 600; color: var(--ink-2); text-transform: uppercase; letter-spacing: 0.04em; }
    .actions.debut { justify-content: flex-start; }
  `,
})
export class PageDEncaissement implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly eleveId = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  protected readonly cle = cleDe;
  protected readonly montant = ecrireUnMontant;
  protected readonly encaissable = signal<Encaissable | null>(null);
  protected readonly introuvable = signal(false);
  protected readonly cochees = signal<ReadonlySet<string>>(new Set());
  protected readonly dAvance = signal(false);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);

  protected readonly dues = computed(() => this.encaissable()?.sommes.filter((s) => s.due) ?? []);
  protected readonly aVenir = computed(() => this.encaissable()?.sommes.filter((s) => !s.due) ?? []);
  protected readonly total = computed(() =>
    (this.encaissable()?.sommes ?? []).filter((s) => this.cochees().has(cleDe(s))).reduce((total, s) => total + s.montant, 0),
  );

  async ngOnInit(): Promise<void> {
    try {
      const encaissable = await firstValueFrom(this.http.get<Encaissable>(`/api/eleves/${this.eleveId}/encaissable`));
      this.encaissable.set(encaissable);
      this.cochees.set(new Set(encaissable.sommes.filter((s) => s.due).map(cleDe)));
    } catch (erreur) {
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) this.introuvable.set(true);
      else this.erreur.set(messageDErreur(erreur));
    }
  }

  protected basculer(somme: SommeAEncaisser): void {
    const cle = cleDe(somme);
    this.cochees.update((cochees) => {
      const suivantes = new Set(cochees);
      if (suivantes.has(cle)) suivantes.delete(cle);
      else suivantes.add(cle);
      return suivantes;
    });
  }

  protected async encaisser(): Promise<void> {
    const encaissable = this.encaissable();
    if (this.envoi() || !encaissable) return;
    const sommes = encaissable.sommes
      .filter((s) => this.cochees().has(cleDe(s)))
      .map((s) => ({ inscriptionId: s.inscriptionId, cle: s.cle }));
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      const recu = await firstValueFrom(this.http.post<RecuDecrit>(`/api/eleves/${encaissable.eleve.id}/encaissement`, { sommes }));
      void this.router.navigate(['/caisse/recus', recu.id]);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
      this.envoi.set(false);
    }
  }

  /** « 13/11/2027 ». */
  protected date(texte: string): string {
    return formatDate(texte, 'dd/MM/yyyy', FRANCAIS);
  }
}
