import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { lireUnNombre } from '../../socle/montants';
import { Niveau } from '../parametres/carte-des-niveaux';
import { nomComplet, Professeur } from '../professeurs/professeur';
import { FicheDuGroupe, Jour, JOURS, nomPropose } from './groupe';

const INCOMPLET = 'Remplissez le professeur, la matière, le niveau, les créneaux, le prix et la date de début';
const PRIX_ILLISIBLE = 'Écrivez le prix du Mois, par exemple 60,000';
const CAPACITE_ILLISIBLE = 'Écrivez la capacité en nombre de places, par exemple 16';

/** Une matière du centre, proposée pendant la frappe. */
interface Matiere {
  id: string;
  nom: string;
}

const nouveauCreneau = (jour: Jour = 'Samedi') =>
  new FormGroup({
    jour: new FormControl<Jour>(jour, { nonNullable: true }),
    debut: new FormControl('', { nonNullable: true }),
    fin: new FormControl('', { nonNullable: true }),
  });

/**
 * GRP-04 : créer un groupe d'Étude. La page charge les professeurs au centre, les niveaux et les matières ; si l'une
 * des listes manque, rien ne part. Le nom suit le premier créneau tant que la secrétaire ne l'a pas changé elle-même.
 * Réussie, la création ouvre la fiche du nouveau groupe.
 */
@Component({
  selector: 'sh-nouveau-groupe',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './nouveau-groupe.html',
  styleUrl: './groupes.css',
})
export class NouveauGroupe implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly jours = JOURS;
  protected readonly nomComplet = nomComplet;

  protected readonly professeurs = signal<Professeur[]>([]);
  protected readonly niveaux = signal<Niveau[]>([]);
  protected readonly matieres = signal<Matiere[]>([]);
  /** Les trois listes sont arrivées : sans elles, rien ne part. */
  protected readonly charge = signal(false);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);
  /** La secrétaire a écrit le nom elle-même : il ne suit plus le premier créneau. */
  private nomChoisi = false;

  protected readonly formulaire = new FormGroup({
    professeurId: new FormControl('', { nonNullable: true }),
    matiere: new FormControl('', { nonNullable: true }),
    niveauId: new FormControl('', { nonNullable: true }),
    creneaux: new FormArray([nouveauCreneau()]),
    prix: new FormControl('', { nonNullable: true }),
    dateDeDebut: new FormControl('', { nonNullable: true }),
    capacite: new FormControl('', { nonNullable: true }),
    nom: new FormControl('', { nonNullable: true }),
  });

  protected get creneaux(): FormArray<ReturnType<typeof nouveauCreneau>> {
    return this.formulaire.controls.creneaux;
  }

  async ngOnInit(): Promise<void> {
    this.creneaux.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.proposerLeNom());
    try {
      const [professeurs, niveaux, matieres] = await Promise.all([
        firstValueFrom(this.http.get<Professeur[]>('/api/professeurs')),
        firstValueFrom(this.http.get<Niveau[]>('/api/niveaux')),
        firstValueFrom(this.http.get<Matiere[]>('/api/matieres')),
      ]);
      const auCentre = professeurs.filter((p) => !p.parti);
      this.professeurs.set(auCentre);
      this.niveaux.set(niveaux);
      this.matieres.set(matieres);
      const demande = this.route.snapshot.queryParamMap.get('professeur');
      if (auCentre.some((p) => p.id === demande)) this.formulaire.controls.professeurId.setValue(demande!);
      this.charge.set(true);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected ajouterUnCreneau(): void {
    this.creneaux.push(nouveauCreneau(this.creneaux.at(-1)?.controls.jour.value));
  }

  protected retirerLeCreneau(rang: number): void {
    this.creneaux.removeAt(rang);
  }

  protected nomSaisi(): void {
    this.nomChoisi = true;
  }

  protected async creer(): Promise<void> {
    if (this.envoi() || !this.charge()) return;

    const valeurs = this.formulaire.getRawValue();
    const prix = lireUnNombre(valeurs.prix);
    const capacite = lireUnNombre(valeurs.capacite);
    const creneauxComplets = valeurs.creneaux.every((c) => c.debut && c.fin);
    if (!valeurs.professeurId || !valeurs.matiere.trim() || !valeurs.niveauId || !creneauxComplets || prix === null || !valeurs.dateDeDebut) {
      this.erreur.set(INCOMPLET);
      return;
    }
    if (prix === undefined) {
      this.erreur.set(PRIX_ILLISIBLE);
      return;
    }
    if (capacite === undefined || (capacite !== null && !Number.isInteger(capacite))) {
      this.erreur.set(CAPACITE_ILLISIBLE);
      return;
    }

    this.envoi.set(true);
    this.erreur.set(null);
    try {
      const fiche = await firstValueFrom(
        this.http.post<FicheDuGroupe>('/api/groupes', {
          professeurId: valeurs.professeurId,
          matiere: valeurs.matiere.trim(),
          niveauId: valeurs.niveauId,
          creneaux: valeurs.creneaux,
          prixDuMois: prix,
          dateDeDebut: valeurs.dateDeDebut,
          capacite,
          nom: valeurs.nom.trim(),
        }),
      );
      await this.router.navigate(['/professeurs/groupes', fiche.id]);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** « Groupe Samedi 10h → 12h » dès que le premier créneau a ses deux heures, sauf si la secrétaire a écrit le nom. */
  private proposerLeNom(): void {
    const premier = this.creneaux.at(0)?.getRawValue();
    if (this.nomChoisi || !premier?.debut || !premier.fin) return;
    this.formulaire.controls.nom.setValue(nomPropose(premier));
  }
}
