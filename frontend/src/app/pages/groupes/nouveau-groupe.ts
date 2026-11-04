import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom, merge } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { lireUnNombre } from '../../socle/montants';
import { Niveau } from '../parametres/carte-des-niveaux';
import { nomComplet, Professeur } from '../professeurs/professeur';
import { FicheDuGroupe, Jour, JOURS, jourDeLaDate, nomPropose, TypeDeGroupe, TYPES } from './groupe';

const INCOMPLET: Record<TypeDeGroupe, string> = {
  Etude: 'Remplissez le professeur, la matière, le niveau, les créneaux, le prix et la date de début',
  Revision: 'Remplissez le professeur, la matière, le niveau, le prix total, le nombre de séances et les dates',
  Formation: 'Remplissez le professeur, la matière, le prix total, le nombre de séances et les dates',
};
const PRIX_ILLISIBLE = 'Écrivez le prix du Mois, par exemple 60,000';
const PRIX_TOTAL_ILLISIBLE = 'Écrivez le prix total, par exemple 50,000';
const NOMBRE_ILLISIBLE = 'Écrivez le nombre de séances, par exemple 10';
const TRANCHE_ILLISIBLE = 'Écrivez le montant et la séance de chaque tranche, par exemple 100,000 à la séance 1';
const CAPACITE_ILLISIBLE = 'Écrivez la capacité en nombre de places, par exemple 16';
const OU_EN_EST_IL = 'Indiquez où en est le groupe';

/** Une matière du centre, proposée pendant la frappe. */
interface Matiere {
  id: string;
  nom: string;
}

/** D'où viennent les dates d'une Révision ou d'une Formation. */
type ModeDesDates = 'creneaux' | 'dates';

const nouveauCreneau = (jour: Jour = 'Samedi') =>
  new FormGroup({
    jour: new FormControl<Jour>(jour, { nonNullable: true }),
    debut: new FormControl('', { nonNullable: true }),
    fin: new FormControl('', { nonNullable: true }),
  });

const nouvelleDate = () =>
  new FormGroup({
    date: new FormControl('', { nonNullable: true }),
    debutDeLaDate: new FormControl('', { nonNullable: true }),
    finDeLaDate: new FormControl('', { nonNullable: true }),
  });

const nouvelleTranche = () =>
  new FormGroup({
    montant: new FormControl('', { nonNullable: true }),
    seance: new FormControl('', { nonNullable: true }),
  });

/** Un nombre entier saisi : `null` si vide, `undefined` s'il est illisible (« dix », « 2,5 »). */
function lireUnEntier(saisie: string): number | null | undefined {
  const nombre = lireUnNombre(saisie);
  return nombre === null || nombre === undefined || Number.isInteger(nombre) ? nombre : undefined;
}

/**
 * GRP-04 à GRP-07 : créer un groupe d'Étude, de Révision ou de Formation, éventuellement déjà en cours. La page charge
 * les professeurs au centre, les niveaux et les matières ; si l'une des listes manque, rien ne part. Le nom suit le
 * premier créneau (ou la première date une à une) tant que la secrétaire ne l'a pas écrit elle-même. Réussie, la
 * création ouvre la fiche du nouveau groupe.
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
  protected readonly types = (Object.keys(TYPES) as TypeDeGroupe[]).map((type) => ({ type, libelle: TYPES[type] }));
  protected readonly nomComplet = nomComplet;

  protected readonly professeurs = signal<Professeur[]>([]);
  protected readonly niveaux = signal<Niveau[]>([]);
  protected readonly matieres = signal<Matiere[]>([]);
  /** Les trois listes sont arrivées : sans elles, rien ne part. */
  protected readonly charge = signal(false);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);
  protected readonly type = signal<TypeDeGroupe>('Etude');
  protected readonly modeDesDates = signal<ModeDesDates>('creneaux');
  /** GRP-07 : le groupe a commencé avant SchoolHub. */
  protected readonly dejaCommence = signal(false);
  /** La secrétaire a écrit le nom elle-même : il ne suit plus le premier créneau. */
  private nomChoisi = false;

  protected readonly formulaire = new FormGroup({
    professeurId: new FormControl('', { nonNullable: true }),
    matiere: new FormControl('', { nonNullable: true }),
    niveauId: new FormControl('', { nonNullable: true }),
    creneaux: new FormArray([nouveauCreneau()]),
    prix: new FormControl('', { nonNullable: true }),
    prixTotal: new FormControl('', { nonNullable: true }),
    nombreDeSeances: new FormControl('', { nonNullable: true }),
    dates: new FormArray([nouvelleDate()]),
    tranches: new FormArray<ReturnType<typeof nouvelleTranche>>([]),
    dateDeDebut: new FormControl('', { nonNullable: true }),
    moisEnCours: new FormControl('', { nonNullable: true }),
    seanceEnCours: new FormControl('', { nonNullable: true }),
    capacite: new FormControl('', { nonNullable: true }),
    nom: new FormControl('', { nonNullable: true }),
  });

  protected get creneaux(): FormArray<ReturnType<typeof nouveauCreneau>> {
    return this.formulaire.controls.creneaux;
  }

  protected get dates(): FormArray<ReturnType<typeof nouvelleDate>> {
    return this.formulaire.controls.dates;
  }

  protected get tranches(): FormArray<ReturnType<typeof nouvelleTranche>> {
    return this.formulaire.controls.tranches;
  }

  async ngOnInit(): Promise<void> {
    merge(this.creneaux.valueChanges, this.dates.valueChanges)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.proposerLeNom());
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

  /** Changer de type garde ce qui est commun : le professeur, la matière, le niveau, les créneaux, le nom. */
  protected choisirLeType(type: TypeDeGroupe): void {
    this.type.set(type);
    this.erreur.set(null);
    this.proposerLeNom();
  }

  protected choisirLeModeDesDates(mode: ModeDesDates): void {
    this.modeDesDates.set(mode);
    this.proposerLeNom();
  }

  protected basculerDejaCommence(evenement: Event): void {
    this.dejaCommence.set((evenement.target as HTMLInputElement).checked);
  }

  protected ajouterUnCreneau(): void {
    this.creneaux.push(nouveauCreneau(this.creneaux.at(-1)?.controls.jour.value));
  }

  protected retirerLeCreneau(rang: number): void {
    this.creneaux.removeAt(rang);
  }

  protected ajouterUneDate(): void {
    this.dates.push(nouvelleDate());
  }

  protected retirerLaDate(rang: number): void {
    this.dates.removeAt(rang);
  }

  protected ajouterUneTranche(): void {
    this.tranches.push(nouvelleTranche());
  }

  protected retirerLaTranche(rang: number): void {
    this.tranches.removeAt(rang);
  }

  protected nomSaisi(): void {
    this.nomChoisi = true;
  }

  protected async creer(): Promise<void> {
    if (this.envoi() || !this.charge()) return;

    const corps = this.type() === 'Etude' ? this.corpsDUneEtude() : this.corpsDUneRevisionOuFormation();
    if (typeof corps === 'string') {
      this.erreur.set(corps);
      return;
    }

    this.envoi.set(true);
    this.erreur.set(null);
    try {
      const fiche = await firstValueFrom(this.http.post<FicheDuGroupe>('/api/groupes', corps));
      await this.router.navigate(['/professeurs/groupes', fiche.id]);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** Ce qui part pour une Étude, ou le refus de la page. */
  private corpsDUneEtude(): object | string {
    const valeurs = this.formulaire.getRawValue();
    const prix = lireUnNombre(valeurs.prix);
    const creneauxComplets = valeurs.creneaux.every((c) => c.debut && c.fin);
    if (!valeurs.professeurId || !valeurs.matiere.trim() || !valeurs.niveauId || !creneauxComplets || prix === null || !valeurs.dateDeDebut) {
      return INCOMPLET.Etude;
    }
    if (prix === undefined) return PRIX_ILLISIBLE;
    const commun = this.champsCommuns();
    if (typeof commun === 'string') return commun;
    const moisEnCours = this.ouEnEstIl(valeurs.moisEnCours);
    if (moisEnCours === undefined) return OU_EN_EST_IL;

    return {
      type: 'Etude',
      professeurId: valeurs.professeurId,
      matiere: valeurs.matiere.trim(),
      niveauId: valeurs.niveauId,
      creneaux: valeurs.creneaux,
      prixDuMois: prix,
      dateDeDebut: valeurs.dateDeDebut,
      ...commun,
      moisEnCours: moisEnCours ?? undefined,
    };
  }

  /** Ce qui part pour une Révision ou une Formation, ou le refus de la page. */
  private corpsDUneRevisionOuFormation(): object | string {
    const type = this.type();
    const valeurs = this.formulaire.getRawValue();
    const parCreneaux = this.modeDesDates() === 'creneaux';
    const prixTotal = lireUnNombre(valeurs.prixTotal);
    const nombre = lireUnEntier(valeurs.nombreDeSeances);
    const datesCompletes = parCreneaux
      ? valeurs.creneaux.every((c) => c.debut && c.fin) && !!valeurs.dateDeDebut
      : valeurs.dates.length > 0 && valeurs.dates.every((d) => d.date && d.debutDeLaDate && d.finDeLaDate);
    const niveauManquant = type === 'Revision' && !valeurs.niveauId;
    if (!valeurs.professeurId || !valeurs.matiere.trim() || niveauManquant || prixTotal === null || nombre === null || !datesCompletes) {
      return INCOMPLET[type];
    }
    if (prixTotal === undefined) return PRIX_TOTAL_ILLISIBLE;
    if (nombre === undefined) return NOMBRE_ILLISIBLE;
    const tranches = valeurs.tranches.map((t) => ({ montant: lireUnNombre(t.montant), seance: lireUnEntier(t.seance) }));
    if (type === 'Formation' && tranches.some((t) => t.montant == null || t.seance == null)) return TRANCHE_ILLISIBLE;
    const commun = this.champsCommuns();
    if (typeof commun === 'string') return commun;
    const seanceEnCours = this.ouEnEstIl(valeurs.seanceEnCours);
    if (seanceEnCours === undefined) return OU_EN_EST_IL;

    return {
      type,
      professeurId: valeurs.professeurId,
      matiere: valeurs.matiere.trim(),
      niveauId: valeurs.niveauId || null,
      prixTotal,
      nombreDeSeances: nombre,
      ...(parCreneaux
        ? { creneaux: valeurs.creneaux, dateDeDebut: valeurs.dateDeDebut }
        : { dates: valeurs.dates.map((d) => ({ date: d.date, debut: d.debutDeLaDate, fin: d.finDeLaDate })) }),
      tranches: type === 'Formation' && tranches.length > 0 ? tranches : undefined,
      ...commun,
      seanceEnCours: seanceEnCours ?? undefined,
    };
  }

  /** La capacité et le nom, communs aux trois types, ou le refus de la page. */
  private champsCommuns(): { capacite: number | null; nom: string } | string {
    const valeurs = this.formulaire.getRawValue();
    const capacite = lireUnEntier(valeurs.capacite);
    if (capacite === undefined) return CAPACITE_ILLISIBLE;
    return { capacite, nom: valeurs.nom.trim() };
  }

  /** Le Mois ou la séance en cours si le groupe a déjà commencé ; `undefined` s'il manque ou ne se lit pas. */
  private ouEnEstIl(saisie: string): number | null | undefined {
    if (!this.dejaCommence()) return null;
    return lireUnEntier(saisie) ?? undefined;
  }

  /** « Groupe Samedi 10h → 12h », depuis le premier créneau ou la première date, sauf si la secrétaire a écrit le nom. */
  private proposerLeNom(): void {
    if (this.nomChoisi) return;
    if (this.type() !== 'Etude' && this.modeDesDates() === 'dates') {
      const premiere = this.dates.at(0)?.getRawValue();
      if (!premiere?.date || !premiere.debutDeLaDate || !premiere.finDeLaDate) return;
      const jour = jourDeLaDate(premiere.date);
      this.formulaire.controls.nom.setValue(nomPropose({ jour, debut: premiere.debutDeLaDate, fin: premiere.finDeLaDate }));
      return;
    }
    const premier = this.creneaux.at(0)?.getRawValue();
    if (!premier?.debut || !premier.fin) return;
    this.formulaire.controls.nom.setValue(nomPropose(premier));
  }
}
