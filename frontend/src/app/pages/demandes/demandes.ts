import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { DemandeDeLaListe, matiereEtHoraire, MOTIFS } from './demande';

/** L'adresse du QR code du formulaire public, en PNG (DEM-02). */
export const QR_CODE = '/api/demandes/qr-code';

/**
 * DEM-03 : la page « Demandes d'inscription ». Les demandes à traiter, les plus anciennes en premier, filtrées par
 * niveau et par matière (les filtres tiennent lieu de liste d'attente), avec « Doublon possible » ; les demandes
 * traitées dans « Traitées ». DEM-02 : le lien public du centre, son QR code et l'affiche.
 */
@Component({
  selector: 'sh-demandes',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Demandes d'inscription</h1>
        <p class="sub">{{ sousTitre() }}</p>
      </div>
    </div>

    <div class="colonnes">
      <section class="card carte" aria-labelledby="titre-des-demandes">
        <div class="entete">
          <div class="onglets" role="tablist" aria-label="Demandes">
            <button type="button" role="tab" class="onglet" [attr.aria-selected]="!traitees()" (click)="montrer(false)">À traiter</button>
            <button type="button" role="tab" class="onglet" [attr.aria-selected]="traitees()" (click)="montrer(true)">Traitées</button>
          </div>
          <h2 id="titre-des-demandes" class="sr">{{ traitees() ? 'Demandes traitées' : 'Demandes à traiter' }}</h2>
          <div class="filtres">
            <label>
              <span class="sr">Niveau</span>
              <select class="inp" aria-label="Niveau" [disabled]="aucuneDemande()" (change)="filtrerLeNiveau($event)">
                <option value="">Tous les niveaux</option>
                @for (niveau of niveaux(); track niveau) {
                  <option [value]="niveau" [selected]="niveau === niveau_()">{{ niveau }}</option>
                }
              </select>
            </label>
            <label>
              <span class="sr">Matière</span>
              <select class="inp" aria-label="Matière" [disabled]="aucuneDemande()" (change)="filtrerLaMatiere($event)">
                <option value="">Toutes les matières</option>
                @for (matiere of matieres(); track matiere) {
                  <option [value]="matiere" [selected]="matiere === matiere_()">{{ matiere }}</option>
                }
              </select>
            </label>
          </div>
        </div>

        @if (demandes(); as demandes) {
          <div class="liste">
            @for (demande of filtrees(); track demande.id) {
              <a class="demande" [routerLink]="['/demandes', demande.id]">
                <span class="deux">
                  <b>
                    {{ demande.prenom }} {{ demande.nom }}
                    @if (demande.doublonPossible) {
                      <span class="pill warn">Doublon possible</span>
                    }
                    @if (demande.etat === 'Inscrite') {
                      <span class="pill ok">Inscrit</span>
                    } @else if (demande.etat === 'Refusee') {
                      <span class="pill muted">Refusée · {{ motif(demande) }}</span>
                    }
                  </b>
                  <small>{{ demande.niveau.nom }} · {{ matieresDe(demande) }}</small>
                  @if (demande.amis) {
                    <small>Amis : {{ demande.amis }}</small>
                  }
                </span>
                <small class="date">{{ quand(demande) }}</small>
              </a>
            } @empty {
              <p class="vide">{{ demandes.length === 0 ? (traitees() ? 'Aucune demande traitée.' : 'Aucune demande à traiter.') : 'Aucune demande pour ce filtre.' }}</p>
            }
          </div>
        }
        @if (erreur(); as erreur) {
          <p class="note bad" role="alert">{{ erreur }}</p>
        }
      </section>

      <section class="card carte">
        <h2 class="titre">Le lien public du centre</h2>
        @if (lien(); as lien) {
          <!-- Comme le prototype (écran 10) : le QR code à gauche ; le lien, « Copier » et l'usage à droite. -->
          <div class="lien-et-qr">
            <img class="qr-code" [src]="qrCode" alt="QR code du formulaire public du centre" width="104" height="104" />
            <div class="cote-du-lien">
              <a class="lien" [href]="lien" target="_blank" rel="noopener">{{ lien }}</a>
              <button type="button" class="btn sm" (click)="copier(lien)">Copier le lien</button>
              <p class="aide">À afficher au guichet ou à partager sur la page Facebook du centre.</p>
            </div>
          </div>
          <div class="actions">
            <a class="btn large" [href]="lien" target="_blank" rel="noopener">Ouvrir le formulaire public</a>
            <a class="btn primary large" routerLink="/demandes/affiche">Imprimer l'affiche</a>
          </div>
        }
        @if (copie()) {
          <p class="note ok" role="status">Lien copié</p>
        }
        @if (erreurDuLien(); as erreur) {
          <p class="note bad" role="alert">{{ erreur }}</p>
        }
      </section>
    </div>
  `,
  styles: `
    .colonnes { display: grid; gap: 16px; grid-template-columns: minmax(0, 3fr) minmax(0, 2fr); align-items: start; }
    @media (max-width: 900px) { .colonnes { grid-template-columns: minmax(0, 1fr); } }
    .carte { display: grid; gap: 14px; padding: 16px; justify-items: stretch; min-width: 0; }
    .titre { font-family: var(--font-display); font-size: 17px; font-weight: 600; margin: 0; }
    .entete { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; }
    .onglets { display: flex; gap: 4px; }
    .onglet { border: 1px solid var(--line); background: none; border-radius: 999px; padding: 5px 12px; cursor: pointer; color: var(--ink-2); font: inherit; }
    .onglet[aria-selected='true'] { border-color: var(--accent); color: var(--ink); background: var(--accent-soft); }
    .filtres { display: flex; flex-wrap: wrap; gap: 8px; }
    .filtres .inp { width: auto; max-width: 100%; padding: 5px 8px; }
    .liste { display: grid; }
    .demande { display: flex; align-items: flex-start; gap: 10px; padding: 10px 4px; border-top: 1px solid var(--line); color: var(--ink); text-decoration: none; min-width: 0; }
    .demande:first-child { border-top: 0; }
    .demande:hover { background: var(--sunk); }
    .deux { display: grid; flex: 1; min-width: 0; }
    .deux b { font-weight: 500; overflow-wrap: anywhere; }
    .deux small { color: var(--ink-2); font-size: 12.5px; overflow-wrap: anywhere; }
    .date { color: var(--ink-3); font-size: 12.5px; white-space: nowrap; }
    .vide { margin: 0; color: var(--ink-2); }
    .lien-et-qr { display: flex; gap: 14px; align-items: flex-start; }
    .cote-du-lien { display: grid; gap: 8px; justify-items: start; min-width: 0; }
    .lien { padding: 6px 10px; border-radius: 8px; background: var(--sunk); color: var(--ink); font-family: var(--font-mono); font-size: 13px; text-decoration: none; overflow-wrap: anywhere; }
    .lien:hover { text-decoration: underline; }
    .aide { margin: 0; color: var(--ink-2); font-size: 12.5px; }
    .qr-code { flex: none; width: 104px; height: 104px; padding: 6px; border: 1px solid var(--line); border-radius: 8px; background: #fff; image-rendering: pixelated; }
    .actions { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 8px; }
    /* Les cartes ont leur titre séparé par un trait, comme le prototype. */
    .carte > .titre, .carte > .entete { margin: -16px -16px 0; padding: 12px 16px; border-bottom: 1px solid var(--line); }
    .titre { font-size: 15px !important; }
    .note { margin: 0; }
  `,
})
export class Demandes implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly qrCode = QR_CODE;
  protected readonly lien = signal<string | null>(null);
  protected readonly copie = signal(false);
  protected readonly erreurDuLien = signal<string | null>(null);

  protected readonly traitees = signal(false);
  protected readonly demandes = signal<DemandeDeLaListe[] | null>(null);
  protected readonly erreur = signal<string | null>(null);
  protected readonly niveau_ = signal('');
  protected readonly matiere_ = signal('');
  private derniereListe = 0;

  /** Les niveaux et les matières des demandes de l'onglet, pour les filtres. */
  /** Aucune demande dans cet onglet : rien à filtrer. */
  protected readonly aucuneDemande = computed(() => (this.demandes() ?? []).length === 0);
  protected readonly niveaux = computed(() => [...new Set((this.demandes() ?? []).map((d) => d.niveau.nom))].sort((a, b) => a.localeCompare(b, 'fr')));
  protected readonly matieres = computed(() =>
    [...new Set((this.demandes() ?? []).flatMap((d) => d.matieres.map((m) => m.matiere)))].sort((a, b) => a.localeCompare(b, 'fr')),
  );

  protected readonly filtrees = computed(() =>
    (this.demandes() ?? []).filter(
      (d) => (!this.niveau_() || d.niveau.nom === this.niveau_()) && (!this.matiere_() || d.matieres.some((m) => m.matiere === this.matiere_())),
    ),
  );

  protected readonly sousTitre = computed(() => {
    const demandes = this.demandes();
    if (this.traitees() || demandes === null) return 'Les demandes du formulaire public. Les filtres tiennent lieu de liste d’attente.';
    const nombre = demandes.length;
    const combien = nombre === 0 ? 'Aucune demande à traiter' : nombre === 1 ? '1 demande à traiter' : `${nombre} demandes à traiter`;
    return `${combien} · les filtres tiennent lieu de liste d’attente`;
  });

  protected readonly matieresDe = (demande: DemandeDeLaListe) => demande.matieres.map(matiereEtHoraire).join(', ');
  protected readonly motif = (demande: DemandeDeLaListe) => (demande.motif ? MOTIFS[demande.motif] : '');

  async ngOnInit(): Promise<void> {
    await Promise.all([this.chargerLeLien(), this.chargerLesDemandes()]);
  }

  protected montrer(traitees: boolean): void {
    if (this.traitees() === traitees) return;
    this.traitees.set(traitees);
    this.niveau_.set('');
    this.matiere_.set('');
    void this.chargerLesDemandes();
  }

  protected filtrerLeNiveau(evenement: Event): void {
    this.niveau_.set((evenement.target as HTMLSelectElement).value);
  }

  protected filtrerLaMatiere(evenement: Event): void {
    this.matiere_.set((evenement.target as HTMLSelectElement).value);
  }

  /** « Reçue le 27/11/2027 », ou pour une demande traitée, « Traitée le 28/11/2027 ». */
  protected quand(demande: DemandeDeLaListe): string {
    const date = demande.traiteeLe ?? demande.recueLe;
    return `${demande.traiteeLe ? 'Traitée' : 'Reçue'} le ${formatDate(date, 'dd/MM/yyyy', FRANCAIS)}`;
  }

  protected async copier(lien: string): Promise<void> {
    this.copie.set(false);
    this.erreurDuLien.set(null);
    try {
      await navigator.clipboard.writeText(lien);
      this.copie.set(true);
    } catch {
      this.erreurDuLien.set("Le lien n'a pas pu être copié : sélectionnez-le à la main.");
    }
  }

  private async chargerLeLien(): Promise<void> {
    try {
      this.lien.set((await firstValueFrom(this.http.get<{ lien: string }>('/api/demandes/lien'))).lien);
    } catch (erreur) {
      this.erreurDuLien.set(messageDErreur(erreur));
    }
  }

  /** Seule la dernière liste demandée compte : un clic rapide d'un onglet à l'autre n'affiche pas l'ancienne. */
  private async chargerLesDemandes(): Promise<void> {
    const numero = ++this.derniereListe;
    this.demandes.set(null);
    this.erreur.set(null);
    try {
      const params = this.traitees() ? { traitees: 'true' } : undefined;
      const demandes = await firstValueFrom(this.http.get<DemandeDeLaListe[]>('/api/demandes', { params }));
      if (numero === this.derniereListe) this.demandes.set(demandes);
    } catch (erreur) {
      if (numero === this.derniereListe) this.erreur.set(messageDErreur(erreur));
    }
  }
}
