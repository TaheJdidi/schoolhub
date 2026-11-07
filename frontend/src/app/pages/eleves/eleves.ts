import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { DELAI_DE_RECHERCHE } from '../../cadre/recherche-d-eleves';
import { ActionsDeListe } from '../../socle/actions-de-liste';
import { messageDErreur } from '../../socle/erreurs';
import { ImportDeListe } from '../../socle/import-de-liste';
import { Niveau } from '../parametres/carte-des-niveaux';
import { ResumeDuGroupe } from '../groupes/groupe';
import { EleveTrouve, nombreDEleves } from './eleve';

/** LST-05 : ce qu'une action sur la sélection a donné. */
export interface ResultatDeLaSelection {
  reussis: string[];
  dejaInscrits: string[];
  refuses: { eleveId: string; nom: string; raison: string }[];
}

/** Les modes d'une Révision ou d'une Formation, à choisir pour l'inscription. */
const MODES: Record<string, { valeur: string; libelle: string }[]> = {
  Revision: [
    { valeur: 'ALaSeance', libelle: 'À la séance' },
    { valeur: 'EnTotalite', libelle: 'En totalité' },
  ],
  Formation: [
    { valeur: 'EnUneFois', libelle: 'En une fois' },
    { valeur: 'EnTranches', libelle: 'En tranches' },
  ],
};

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
          <input class="inp" [disabled]="aucunEleve()" (input)="filtrer($event)" placeholder="Nom, prénom ou téléphone" aria-label="Filtrer la liste" autocomplete="off" />
        </label>
        <label class="f">
          Niveau
          <select class="inp" [disabled]="aucunEleve()" (change)="niveau.set($any($event.target).value)">
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
        @if (visibles().length > 0) {
          <label class="tout">
            <input type="checkbox" [checked]="toutSelectionne()" (change)="toutSelectionner()" /> Tout sélectionner
          </label>
        }
        @for (eleve of visibles(); track eleve.id) {
          <div class="rangee" [class.non-selectionne]="selection().size > 0 && !selection().has(eleve.id)">
            <input type="checkbox" [checked]="selection().has(eleve.id)" (change)="basculer(eleve.id)" [attr.aria-label]="'Sélectionner ' + eleve.prenom + ' ' + eleve.nom" />
            <a class="ligne-eleve" [routerLink]="['/eleves', eleve.id]">
              <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
              <small>{{ eleve.niveau }} <sh-pastille-de-couleur [couleur]="eleve.couleur" /></small>
            </a>
          </div>
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

    @if (selection().size > 0) {
      <section class="card carte selection sans-impression" aria-label="Sélection">
        <div class="entete-selection">
          <b>{{ selection().size }} élève{{ selection().size > 1 ? 's' : '' }} sélectionné{{ selection().size > 1 ? 's' : '' }}</b>
          <sh-actions-de-liste titre="Élèves sélectionnés" [nombre]="selection().size" exporter="/api/exports/eleves" [filtres]="{ ids: idsSelectionnes() }" />
          <button type="button" class="btn sm" (click)="selection.set(vide)">Tout désélectionner</button>
        </div>
        <div class="actions-selection">
          <label class="f">
            Inscrire dans un groupe
            <select class="inp" (change)="groupeChoisi.set($any($event.target).value)">
              <option value="">Choisissez le groupe</option>
              @for (g of groupes(); track g.id) {
                <option [value]="g.id">{{ g.matiere }} · {{ g.nom }}</option>
              }
            </select>
          </label>
          @if (modes().length > 0) {
            <label class="f">
              Mode
              <select class="inp" (change)="modeChoisi.set($any($event.target).value)">
                @for (m of modes(); track m.valeur) {
                  <option [value]="m.valeur">{{ m.libelle }}</option>
                }
              </select>
            </label>
          }
          <button type="button" class="btn primary" [disabled]="envoi() || !groupeChoisi()" (click)="inscrire()">Inscrire</button>
        </div>
        <div class="actions-selection">
          <label class="f">
            Changer le niveau
            <select class="inp" #niveauNouveau>
              <option value="">Choisissez le niveau</option>
              @for (n of tousLesNiveaux(); track n.id) {
                <option [value]="n.id">{{ n.nom }}</option>
              }
            </select>
          </label>
          <button type="button" class="btn" [disabled]="envoi()" (click)="changerLeNiveau(niveauNouveau.value)">Changer le niveau</button>
        </div>
        @if (resultat(); as resultat) {
          @if (resultat.reussis.length > 0) {
            <p class="note ok" role="status">{{ resultat.action }} : {{ resultat.reussis.join(', ') }}.</p>
          }
          @if (resultat.dejaInscrits.length > 0) {
            <p class="note">Déjà inscrits, laissés à part : {{ resultat.dejaInscrits.join(', ') }}.</p>
          }
          @for (refus of resultat.refuses; track refus.eleveId) {
            <p class="note bad">{{ refus.nom }} : {{ refus.raison }}</p>
          }
        }
        @if (erreurDeLaSelection(); as erreur) {
          <p class="note bad" role="alert">{{ erreur }}</p>
        }
      </section>
    }
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

  // ----- LST-05 : la sélection multiple -----
  protected readonly vide: ReadonlySet<string> = new Set();
  protected readonly selection = signal<ReadonlySet<string>>(this.vide);
  protected readonly idsSelectionnes = computed(() => [...this.selection()].join(','));
  protected readonly toutSelectionne = computed(() => this.visibles().length > 0 && this.visibles().every((e) => this.selection().has(e.id)));
  protected readonly groupes = signal<ResumeDuGroupe[]>([]);
  protected readonly groupeChoisi = signal('');
  protected readonly modes = computed(() => MODES[this.groupes().find((g) => g.id === this.groupeChoisi())?.type ?? ''] ?? []);
  protected readonly modeChoisi = signal('');
  protected readonly tousLesNiveaux = signal<Niveau[]>([]);
  protected readonly envoi = signal(false);
  protected readonly resultat = signal<(ResultatDeLaSelection & { action: string }) | null>(null);
  protected readonly erreurDeLaSelection = signal<string | null>(null);
  private listesChargees = false;
  /** Les niveaux des élèves du centre, dans l'ordre où ils viennent. */
  protected readonly niveaux = computed(() => [...new Set(this.tous().map((e) => e.niveau))].sort((a, b) => a.localeCompare(b, 'fr')));
  /** Aucun élève dans le centre : rien à filtrer, ni à imprimer ou exporter. */
  protected readonly aucunEleve = computed(() => this.tous().length === 0);
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

  protected basculer(id: string): void {
    this.selection.update((selection) => {
      const suivante = new Set(selection);
      if (suivante.has(id)) suivante.delete(id);
      else suivante.add(id);
      return suivante;
    });
    void this.chargerLesListes();
  }

  /** « Tout sélectionner » prend toutes les lignes du filtre en cours ; un second clic les retire. */
  protected toutSelectionner(): void {
    const visibles = this.visibles().map((e) => e.id);
    const retirer = this.toutSelectionne();
    this.selection.update((selection) => {
      const suivante = new Set(selection);
      if (retirer) visibles.forEach((id) => suivante.delete(id));
      else visibles.forEach((id) => suivante.add(id));
      return suivante;
    });
    void this.chargerLesListes();
  }

  protected async inscrire(): Promise<void> {
    const mode = this.modes().length > 0 ? this.modeChoisi() || this.modes()[0].valeur : null;
    await this.agir('Inscrits', '/api/selection/inscription', { eleveIds: [...this.selection()], groupeId: this.groupeChoisi(), mode });
  }

  protected async changerLeNiveau(niveauId: string): Promise<void> {
    if (!niveauId) {
      this.erreurDeLaSelection.set('Choisissez le niveau');
      return;
    }
    await this.agir('Niveau changé', '/api/selection/niveau', { eleveIds: [...this.selection()], niveauId });
    await this.recharger();
  }

  private async agir(action: string, adresse: string, corps: object): Promise<void> {
    this.envoi.set(true);
    this.erreurDeLaSelection.set(null);
    this.resultat.set(null);
    try {
      this.resultat.set({ ...(await firstValueFrom(this.http.post<ResultatDeLaSelection>(adresse, corps))), action });
    } catch (erreur) {
      this.erreurDeLaSelection.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** Les groupes ouverts et les niveaux, chargés à la première sélection. */
  private async chargerLesListes(): Promise<void> {
    if (this.listesChargees || this.selection().size === 0) return;
    this.listesChargees = true;
    try {
      const [groupes, niveaux] = await Promise.all([
        firstValueFrom(this.http.get<ResumeDuGroupe[]>('/api/groupes')),
        firstValueFrom(this.http.get<Niveau[]>('/api/niveaux')),
      ]);
      this.groupes.set(groupes.filter((g) => !g.ferme && !g.termine));
      this.tousLesNiveaux.set(niveaux);
    } catch (erreur) {
      this.listesChargees = false;
      this.erreurDeLaSelection.set(messageDErreur(erreur));
    }
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
