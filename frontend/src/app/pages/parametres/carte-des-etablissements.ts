import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormControl, FormGroup, FormGroupDirective, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { garderLeBrouillon } from '../../socle/brouillons';
import { messageDErreur } from '../../socle/erreurs';
import { Icone } from '../../socle/icone';
import { requis } from '../../socle/champs-requis';

/** Un établissement de la liste du centre (GET /api/etablissements). */
export interface Etablissement {
  id: string;
  nom: string;
}

const ADRESSE = '/api/etablissements';
const NOM_VIDE = "Saisissez le nom de l'établissement";

/**
 * Les écoles, collèges et lycées que le formulaire public propose dans sa liste « Établissement scolaire ». Un élève
 * dont l'établissement manque choisit « Autre » et l'écrit. Retirer un établissement ne change aucune fiche.
 */
@Component({
  selector: 'sh-carte-des-etablissements',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, Icone],
  template: `
    <section class="card carte">
      <div class="entete">
        <h2 class="titre">Les établissements</h2>
        <span class="pill muted">{{ etablissements().length }} établissement{{ etablissements().length > 1 ? 's' : '' }}</span>
      </div>
      <p class="intro">
        Les écoles, collèges et lycées de votre quartier. Le formulaire d'inscription les propose dans une liste ; un élève dont
        l'établissement n'y est pas choisit « Autre » et l'écrit.
      </p>

      <div class="bloc">
        @if (etablissements().length === 0) {
          <p class="vide">Aucun établissement pour l'instant</p>
        }
        <ul class="liste ajoutes">
          @for (etablissement of etablissements(); track etablissement.id) {
            <li class="ligne">
              @if (aConfirmer() === etablissement.id) {
                <span class="question">Retirer « {{ etablissement.nom }} » ?</span>
                <div class="actions">
                  <button type="button" class="btn primary" [disabled]="envoi()" (click)="retirer(etablissement)">Oui</button>
                  <button type="button" class="btn" [disabled]="envoi()" (click)="aConfirmer.set(null)">Non</button>
                </div>
              } @else {
                <span class="nom">{{ etablissement.nom }}</span>
                <div class="actions">
                  <button type="button" class="btn ghost retirer" [disabled]="envoi()" (click)="aConfirmer.set(etablissement.id)">Retirer</button>
                </div>
              }
            </li>
          }
        </ul>

        <form class="ajout" [formGroup]="ajout" (ngSubmit)="ajouter()" novalidate>
          <input class="inp" formControlName="nouveau" placeholder="Par exemple : Collège Ibn Khaldoun" aria-label="Nouvel établissement" />
          <button type="submit" class="btn primary" [disabled]="envoi()"><sh-icone nom="plus" />Ajouter</button>
        </form>
      </div>

      @if (succes(); as succes) {
        <p class="note ok" role="status">{{ succes }}</p>
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </section>
  `,
  styleUrl: './parametres.css',
})
export class CarteDesEtablissements implements OnInit {
  /** Le formulaire tel qu'Angular le suit : le vider par lui oublie aussi l'envoi, et les champs vides ne restent pas en rouge. */
  private readonly directiveDuFormulaire = viewChild(FormGroupDirective);

  /** Vide le formulaire ; s'il est à l'écran, Angular oublie aussi qu'il a été envoyé. */
  private viderLeFormulaire(valeur?: Parameters<typeof this.ajout.reset>[0]): void {
    const directive = this.directiveDuFormulaire();
    if (directive) directive.resetForm(valeur);
    else this.ajout.reset(valeur);
  }
  private readonly http = inject(HttpClient);

  protected readonly etablissements = signal<Etablissement[]>([]);
  protected readonly ajout = new FormGroup({ nouveau: new FormControl('', { nonNullable: true, validators: requis() }) });
  /** Le nom commencé, puis laissé pour une autre page, se retrouve en revenant. */
  private readonly brouillon = garderLeBrouillon('etablissement', this.ajout);
  /** L'établissement dont le retrait attend « Oui » ou « Non ». */
  protected readonly aConfirmer = signal<string | null>(null);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      await this.charger();
      this.brouillon.reprendre();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected ajouter(): Promise<void> {
    const nom = this.ajout.controls.nouveau.value.trim();
    if (!nom) {
      this.succes.set(null);
      this.erreur.set(NOM_VIDE);
      return Promise.resolve();
    }
    return this.agir(async () => {
      await firstValueFrom(this.http.post<Etablissement>(ADRESSE, { nom }));
      this.brouillon.oublier();
      this.viderLeFormulaire();
      await this.charger();
      return 'Établissement ajouté';
    });
  }

  protected retirer(etablissement: Etablissement): Promise<void> {
    this.aConfirmer.set(null);
    return this.agir(async () => {
      await firstValueFrom(this.http.delete(`${ADRESSE}/${etablissement.id}`));
      this.etablissements.update((liste) => liste.filter((e) => e.id !== etablissement.id));
      return 'Établissement retiré';
    });
  }

  private async charger(): Promise<void> {
    this.etablissements.set(await firstValueFrom(this.http.get<Etablissement[]>(ADRESSE)));
  }

  /** Une action vers le serveur : le message vert la confirme, ou le refus s'affiche en rouge. */
  private async agir(action: () => Promise<string>): Promise<void> {
    if (this.envoi()) return;
    this.envoi.set(true);
    this.succes.set(null);
    this.erreur.set(null);
    try {
      this.succes.set(await action());
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
