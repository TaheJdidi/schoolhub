import { formatDate } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant, lireUnNombre } from '../../socle/montants';
import { eleves, FicheDuGroupe, heure, TYPES } from './groupe';

const PRIX_ILLISIBLE = 'Écrivez le prix du Mois, par exemple 60,000';
const CAPACITE_ILLISIBLE = 'Écrivez la capacité en nombre de places, par exemple 16';

/**
 * GRP-04 : la fiche d'un groupe, avec ses Mois et leurs séances. « Modifier » change le nom, la capacité et le prix ;
 * le nouveau prix vaut pour les Mois qui n'ont pas encore commencé, et c'est le serveur qui le décide.
 */
@Component({
  selector: 'sh-page-du-groupe',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './page-du-groupe.html',
  styleUrl: './groupes.css',
})
export class PageDuGroupe implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  protected readonly eleves = eleves;
  protected readonly montant = ecrireUnMontant;

  protected readonly fiche = signal<FicheDuGroupe | null>(null);
  protected readonly introuvable = signal(false);
  protected readonly enModification = signal(false);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  protected readonly formulaire = new FormGroup({
    nom: new FormControl('', { nonNullable: true }),
    capacite: new FormControl('', { nonNullable: true }),
    prix: new FormControl('', { nonNullable: true }),
  });

  /** « Mathématiques · 9ᵉ année de base · Karim Ben Salah · Étude, 60,000 TND par Mois ». */
  protected readonly sousTitre = computed(() => {
    const fiche = this.fiche();
    if (!fiche) return '';
    const parties = [fiche.matiere, fiche.niveau?.nom, `${fiche.professeur.prenom} ${fiche.professeur.nom}`].filter(Boolean);
    return `${parties.join(' · ')} · ${TYPES[fiche.type]}, ${ecrireUnMontant(fiche.prixDuMois)} TND par Mois`;
  });

  /** « Samedi de 10h à 12h, Mardi de 17h à 19h ». */
  protected readonly horaire = computed(
    () => this.fiche()?.creneaux.map((c) => `${c.jour} de ${heure(c.debut)} à ${heure(c.fin)}`).join(', ') ?? '',
  );

  private get id(): string {
    return this.route.snapshot.paramMap.get('id') ?? '';
  }

  async ngOnInit(): Promise<void> {
    try {
      this.fiche.set(await firstValueFrom(this.http.get<FicheDuGroupe>(`/api/groupes/${this.id}`)));
    } catch (erreur) {
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) this.introuvable.set(true);
      else this.erreur.set(messageDErreur(erreur));
    }
  }

  /** « 18/09/2027 ». */
  protected date(texte: string): string {
    return formatDate(texte, 'dd/MM/yyyy', FRANCAIS);
  }

  /** « sam. 18/09 ». */
  protected jourDeSeance(texte: string): string {
    return formatDate(texte, 'EEE dd/MM', FRANCAIS);
  }

  protected modifier(fiche: FicheDuGroupe): void {
    this.formulaire.setValue({
      nom: fiche.nom,
      capacite: fiche.capacite === null ? '' : String(fiche.capacite),
      prix: ecrireUnMontant(fiche.prixDuMois),
    });
    this.succes.set(null);
    this.erreur.set(null);
    this.enModification.set(true);
  }

  protected annuler(): void {
    this.enModification.set(false);
    this.erreur.set(null);
  }

  protected async enregistrer(): Promise<void> {
    if (this.envoi()) return;

    const valeurs = this.formulaire.getRawValue();
    const prix = lireUnNombre(valeurs.prix);
    const capacite = lireUnNombre(valeurs.capacite);
    if (prix === null || prix === undefined) return this.refuser(PRIX_ILLISIBLE);
    if (capacite === undefined || (capacite !== null && !Number.isInteger(capacite))) return this.refuser(CAPACITE_ILLISIBLE);

    this.envoi.set(true);
    this.succes.set(null);
    this.erreur.set(null);
    try {
      const fiche = await firstValueFrom(
        this.http.put<FicheDuGroupe>(`/api/groupes/${this.id}`, { nom: valeurs.nom.trim(), capacite, prixDuMois: prix }),
      );
      this.fiche.set(fiche);
      this.enModification.set(false);
      this.succes.set('Groupe enregistré');
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  private refuser(message: string): void {
    this.succes.set(null);
    this.erreur.set(message);
  }
}
