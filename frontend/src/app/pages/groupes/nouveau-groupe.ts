import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom, merge } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderLeBrouillon } from '../../socle/brouillons';
import { ChampHeure, lireUneHeure, uneHeure } from '../../socle/champ-heure';
import { requis, reverifier } from '../../socle/champs-requis';
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
const HEURE_ILLISIBLE = "Écrivez l'heure, par exemple 8h ou 8h30";
const PRIX_ILLISIBLE = 'Écrivez le prix du Mois, par exemple 60,000';
const PRIX_TOTAL_ILLISIBLE = 'Écrivez le prix total, par exemple 50,000';
const NOMBRE_ILLISIBLE = 'Écrivez le nombre de séances, par exemple 10';
const TRANCHE_ILLISIBLE = 'Écrivez le montant et la séance de chaque tranche, par exemple 100,000 à la séance 1';
const CAPACITE_ILLISIBLE = 'Écrivez la capacité en nombre de places, par exemple 16';
const OU_EN_EST_IL = 'Indiquez où en est le groupe';

/** Les matières proposées à tout centre, même neuf ; celles du centre s'y ajoutent. */
export const MATIERES_PROPOSEES = [
  'Allemand',
  'Anglais',
  'Arabe',
  'Chimie',
  'Économie',
  'Éducation islamique',
  'Espagnol',
  'Éveil scientifique',
  'Français',
  'Gestion',
  'Histoire-Géographie',
  'Informatique',
  'Italien',
  'Mathématiques',
  'Philosophie',
  'Physique',
  'Sciences de la vie et de la terre',
  'Sciences expérimentales',
  'Sciences physiques',
  'Technologie',
];

/** L'option « Autre matière… » de la liste. */
export const AUTRE_MATIERE = 'autre';

/** « mathématiques » et « Mathématiques » sont la même matière, comme au serveur. */
const normaliser = (nom: string) => nom.trim().normalize('NFKC').toLowerCase();

/** Une matière du centre, proposée dans la liste. */
interface Matiere {
  id: string;
  nom: string;
}

/** D'où viennent les dates d'une Révision ou d'une Formation. */
type ModeDesDates = 'creneaux' | 'dates';

const nouveauCreneau = (jour: Jour = 'Samedi') =>
  new FormGroup({
    jour: new FormControl<Jour>(jour, { nonNullable: true }),
    debut: new FormControl('', { nonNullable: true, validators: [requis(), uneHeure] }),
    fin: new FormControl('', { nonNullable: true, validators: [requis(), uneHeure] }),
  });

const nouvelleDate = () =>
  new FormGroup({
    date: new FormControl('', { nonNullable: true, validators: requis() }),
    debutDeLaDate: new FormControl('', { nonNullable: true, validators: [requis(), uneHeure] }),
    finDeLaDate: new FormControl('', { nonNullable: true, validators: [requis(), uneHeure] }),
  });

const nouvelleTranche = () =>
  new FormGroup({
    montant: new FormControl('', { nonNullable: true, validators: requis() }),
    seance: new FormControl('', { nonNullable: true, validators: requis() }),
  });

type Creneau = { jour: Jour; debut: string; fin: string };
type DateDeSeance = { date: string; debut: string; fin: string };

/** Les créneaux, heures lues (« 10 » devient « 10:00 ») : `null` si une heure manque, `undefined` si une ne se lit pas. */
function lireLesCreneaux(creneaux: Creneau[]): Creneau[] | null | undefined {
  const lus = creneaux.map((c) => ({ jour: c.jour, debut: lireUneHeure(c.debut), fin: lireUneHeure(c.fin) }));
  if (lus.some((c) => c.debut === null || c.fin === null)) return null;
  if (lus.some((c) => c.debut === undefined || c.fin === undefined)) return undefined;
  return lus as Creneau[];
}

/** Les dates une à une, comme `lireLesCreneaux`. */
function lireLesDates(dates: { date: string; debutDeLaDate: string; finDeLaDate: string }[]): DateDeSeance[] | null | undefined {
  const lues = dates.map((d) => ({ date: d.date, debut: lireUneHeure(d.debutDeLaDate), fin: lireUneHeure(d.finDeLaDate) }));
  if (lues.length === 0 || lues.some((d) => !d.date || d.debut === null || d.fin === null)) return null;
  if (lues.some((d) => d.debut === undefined || d.fin === undefined)) return undefined;
  return lues as DateDeSeance[];
}

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
  imports: [ReactiveFormsModule, RouterLink, BrouillonRepris, ChampHeure],
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
  /** Les matières proposées et celles du centre, une seule fois chacune, dans l'ordre alphabétique. */
  protected readonly listeDesMatieres = computed(() => {
    const parNom = new Map(MATIERES_PROPOSEES.map((nom) => [normaliser(nom), nom]));
    for (const matiere of this.matieres()) parNom.set(normaliser(matiere.nom), matiere.nom);
    return [...parNom.values()].sort((a, b) => a.localeCompare(b, 'fr'));
  });
  /** « Autre matière… » est choisie : la matière s'écrit à la main. */
  protected readonly autreMatiere = signal(false);
  protected readonly AUTRE_MATIERE = AUTRE_MATIERE;
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
  /** Le dernier nom proposé d'après les créneaux. */
  private dernierNomPropose = '';

  protected readonly formulaire = new FormGroup({
    professeurId: new FormControl('', { nonNullable: true, validators: requis() }),
    matiere: new FormControl('', { nonNullable: true, validators: requis() }),
    // Le niveau est facultatif pour une Formation seulement.
    niveauId: new FormControl('', { nonNullable: true, validators: requis(() => this.type() !== 'Formation') }),
    creneaux: new FormArray([nouveauCreneau()]),
    prix: new FormControl('', { nonNullable: true, validators: requis() }),
    prixTotal: new FormControl('', { nonNullable: true, validators: requis() }),
    nombreDeSeances: new FormControl('', { nonNullable: true, validators: requis() }),
    dates: new FormArray([nouvelleDate()]),
    tranches: new FormArray<ReturnType<typeof nouvelleTranche>>([]),
    dateDeDebut: new FormControl('', { nonNullable: true, validators: requis() }),
    moisEnCours: new FormControl('', { nonNullable: true, validators: requis() }),
    seanceEnCours: new FormControl('', { nonNullable: true, validators: requis() }),
    capacite: new FormControl('', { nonNullable: true }),
    nom: new FormControl('', { nonNullable: true }),
  });

  /** Laissé pour une autre page, le formulaire reprend où il en était en revenant. */
  protected readonly brouillon = garderLeBrouillon('nouveau-groupe', this.formulaire, {
    lignes: { creneaux: () => nouveauCreneau(), dates: nouvelleDate, tranches: nouvelleTranche },
    etat: {
      lire: () => ({
        type: this.type(),
        modeDesDates: this.modeDesDates(),
        dejaCommence: this.dejaCommence(),
        nomChoisi: this.nomChoisi,
        autreMatiere: this.autreMatiere(),
      }),
      ecrire: (etat) => {
        this.type.set(etat.type);
        this.modeDesDates.set(etat.modeDesDates);
        this.dejaCommence.set(etat.dejaCommence);
        this.nomChoisi = etat.nomChoisi;
        this.autreMatiere.set(etat.autreMatiere ?? false);
      },
    },
  });

  private readonly matiereSaisie = toSignal(this.formulaire.controls.matiere.valueChanges, { initialValue: '' });
  /** La matière s'écrit à la main : « Autre matière… » est choisie, ou la matière reprise n'est pas dans la liste. */
  protected readonly matiereALaMain = computed(() => {
    const saisie = normaliser(this.matiereSaisie());
    return this.autreMatiere() || (saisie !== '' && !this.listeDesMatieres().some((nom) => normaliser(nom) === saisie));
  });
  /** L'option choisie dans la liste. */
  protected readonly optionDeLaMatiere = computed(() => (this.matiereALaMain() ? AUTRE_MATIERE : this.matiereSaisie()));

  protected choisirLaMatiere(evenement: Event): void {
    const choix = (evenement.target as HTMLSelectElement).value;
    const autre = choix === AUTRE_MATIERE;
    this.autreMatiere.set(autre);
    const matiere = this.formulaire.controls.matiere;
    matiere.setValue(autre ? '' : choix);
    matiere.markAsDirty();
  }

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
      if (this.brouillon.reprendre()) {
        // Un professeur parti, ou un niveau retiré, entre-temps : le champ redevient à choisir.
        const { professeurId, niveauId } = this.formulaire.controls;
        if (!auCentre.some((p) => p.id === professeurId.value)) professeurId.setValue('');
        if (!niveaux.some((n) => n.id === niveauId.value)) niveauId.setValue('');
      }
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
    reverifier(this.formulaire);
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

  /**
   * La secrétaire a écrit dans le nom. Un nom à elle ne suit plus les créneaux ; un nom vidé, ou laissé égal au nom
   * proposé, les suit de nouveau au prochain changement de jour ou d'horaire.
   */
  protected nomSaisi(): void {
    const nom = this.formulaire.controls.nom.value.trim();
    this.nomChoisi = nom !== '' && nom !== this.dernierNomPropose;
  }

  protected async creer(): Promise<void> {
    if (this.envoi() || !this.charge()) return;

    reverifier(this.formulaire);
    const corps = this.type() === 'Etude' ? this.corpsDUneEtude() : this.corpsDUneRevisionOuFormation();
    if (typeof corps === 'string') {
      this.erreur.set(corps);
      return;
    }

    this.envoi.set(true);
    this.erreur.set(null);
    try {
      const fiche = await firstValueFrom(this.http.post<FicheDuGroupe>('/api/groupes', corps));
      this.brouillon.oublier();
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
    const creneaux = lireLesCreneaux(valeurs.creneaux);
    if (!valeurs.professeurId || !valeurs.matiere.trim() || !valeurs.niveauId || creneaux === null || prix === null || !valeurs.dateDeDebut) {
      return INCOMPLET.Etude;
    }
    if (creneaux === undefined) return HEURE_ILLISIBLE;
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
      creneaux,
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
    const creneaux = parCreneaux ? lireLesCreneaux(valeurs.creneaux) : lireLesDates(valeurs.dates);
    const datesCompletes = creneaux !== null && (!parCreneaux || !!valeurs.dateDeDebut);
    const niveauManquant = type === 'Revision' && !valeurs.niveauId;
    if (!valeurs.professeurId || !valeurs.matiere.trim() || niveauManquant || prixTotal === null || nombre === null || !datesCompletes) {
      return INCOMPLET[type];
    }
    if (creneaux === undefined) return HEURE_ILLISIBLE;
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
      ...(parCreneaux ? { creneaux, dateDeDebut: valeurs.dateDeDebut } : { dates: creneaux }),
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
      const debut = lireUneHeure(premiere?.debutDeLaDate ?? '');
      const fin = lireUneHeure(premiere?.finDeLaDate ?? '');
      if (!premiere?.date || !debut || !fin) return;
      this.proposer(nomPropose({ jour: jourDeLaDate(premiere.date), debut, fin }));
      return;
    }
    const premier = this.creneaux.at(0)?.getRawValue();
    const debut = lireUneHeure(premier?.debut ?? '');
    const fin = lireUneHeure(premier?.fin ?? '');
    if (!premier || !debut || !fin) return;
    this.proposer(nomPropose({ jour: premier.jour, debut, fin }));
  }

  private proposer(nom: string): void {
    this.dernierNomPropose = nom;
    this.formulaire.controls.nom.setValue(nom);
  }
}
