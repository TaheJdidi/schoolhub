import { formatDate } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant, lireUnNombre } from '../../socle/montants';
import { InscriptionDeLEleve, modeCourt } from '../inscriptions/inscription';
import { dateDuJourLocale, eleves, etatDeLaSeance, FicheDuGroupe, heure, TYPES } from './groupe';

const PRIX_ILLISIBLE = 'Écrivez le prix du Mois, par exemple 60,000';

/**
 * Le prix selon le type : « 60,000 TND par Mois » ; « 50,000 TND les 3 séances, ou 16,667 TND la séance » ;
 * « 300,000 TND, en une fois ou en 3 tranches ».
 */
function prixDuGroupe(fiche: FicheDuGroupe): string {
  const total = ecrireUnMontant(fiche.prixTotal);
  switch (fiche.type) {
    case 'Etude':
      return `${ecrireUnMontant(fiche.prixDuMois)} TND par Mois`;
    case 'Revision': {
      const nombre = fiche.nombreDeSeances ?? 1;
      const seance = Math.round(((fiche.prixTotal ?? 0) / nombre) * 1000) / 1000;
      return `${total} TND les ${nombre} séances, ou ${ecrireUnMontant(seance)} TND la séance`;
    }
    case 'Formation': {
      const tranches = fiche.tranches.length;
      if (tranches === 0) return `${total} TND, en une fois`;
      return `${total} TND, en une fois ou en ${tranches} ${tranches > 1 ? 'tranches' : 'tranche'}`;
    }
  }
}
const CAPACITE_ILLISIBLE = 'Écrivez la capacité en nombre de places, par exemple 16';
const FERMETURE_INCOMPLETE = 'Choisissez la date et écrivez le motif';

/**
 * GRP-04 à GRP-07 : la fiche d'un groupe. Une Étude montre ses Mois ; une Révision ou une Formation, ses séances
 * numérotées (avec leur prix pour une Révision) et les tranches d'une Formation. « Modifier » change le nom, la capacité
 * et, pour une Étude seulement, le prix du Mois ; le nouveau prix vaut pour les Mois qui n'ont pas encore commencé, et
 * c'est le serveur qui le décide. « Fermer le groupe » (GRP-08) le ferme à une date, avec un motif : ses séances après
 * cette date disparaissent, et il ne se modifie plus.
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
  private readonly destroyRef = inject(DestroyRef);

  protected readonly eleves = eleves;
  protected readonly modeCourt = modeCourt;
  /** Les élèves inscrits, par nom puis prénom (ELV-03). */
  protected readonly inscriptions = signal<InscriptionDeLEleve[]>([]);
  protected readonly heure = heure;
  protected readonly montant = ecrireUnMontant;

  protected readonly fiche = signal<FicheDuGroupe | null>(null);
  protected readonly introuvable = signal(false);
  protected readonly enModification = signal(false);
  protected readonly enFermeture = signal(false);
  /** La date de fermeture choisie, pour compter les séances qui disparaîtront. */
  private readonly dateDeFermeture = signal('');
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  protected readonly formulaire = new FormGroup({
    nom: new FormControl('', { nonNullable: true }),
    capacite: new FormControl('', { nonNullable: true }),
    prix: new FormControl('', { nonNullable: true }),
  });

  protected readonly fermeture = new FormGroup({
    dateDeFermeture: new FormControl('', { nonNullable: true }),
    motif: new FormControl('', { nonNullable: true }),
  });

  /** « Les 3 séances prévues après le 02/10/2027 seront supprimées. » */
  protected readonly avertissementDeFermeture = computed(() => {
    const fiche = this.fiche();
    const date = this.dateDeFermeture();
    if (!fiche || !date) return '';
    const dates = [...fiche.mois.flatMap((m) => m.seances), ...fiche.seances].map((s) => s.date);
    const supprimees = dates.filter((d) => d > date).length;
    const le = this.date(date);
    if (supprimees === 0) return `Aucune séance prévue après le ${le}.`;
    if (supprimees === 1) return `La séance prévue après le ${le} sera supprimée.`;
    return `Les ${supprimees} séances prévues après le ${le} seront supprimées.`;
  });

  /** « Mathématiques · 9ᵉ année de base · Karim Ben Salah · Étude, 60,000 TND par Mois ». */
  protected readonly sousTitre = computed(() => {
    const fiche = this.fiche();
    if (!fiche) return '';
    const parties = [fiche.matiere, fiche.niveau?.nom, `${fiche.professeur.prenom} ${fiche.professeur.nom}`].filter(Boolean);
    return `${parties.join(' · ')} · ${TYPES[fiche.type]}, ${prixDuGroupe(fiche)}`;
  });

  /** « Samedi de 10h à 12h, Mardi de 17h à 19h ». */
  protected readonly horaire = computed(
    () => this.fiche()?.creneaux.map((c) => `${c.jour} de ${heure(c.debut)} à ${heure(c.fin)}`).join(', ') ?? '',
  );

  /** SEA-02 : le jour, pour l'état des séances. */
  protected readonly aujourdhui = dateDuJourLocale();

  /** SEA-02 : « à venir », « appel fait » ou « appel à faire ». */
  protected readonly etat = (seance: { date: string; appelFait?: boolean }) => etatDeLaSeance(seance, this.aujourdhui);

  /** SEA-02 : le Mois en cours d'une Étude, le dernier qui a commencé. */
  protected readonly moisEnCours = computed(() => this.fiche()?.mois.filter((m) => m.commence).at(-1)?.numero ?? null);

  /** SEA-02 : « Séance 2 sur 3 » pour une Révision ou une Formation, d'après les séances déjà passées ou du jour. */
  protected readonly avancement = computed(() => {
    const fiche = this.fiche();
    if (!fiche || fiche.type === 'Etude' || fiche.seances.length === 0) return '';
    const passees = fiche.seances.filter((s) => s.date <= this.aujourdhui);
    const total = fiche.nombreDeSeances ?? fiche.seances.at(-1)!.numero;
    return passees.length === 0 ? `Pas encore commencé · ${total} séances` : `Séance ${passees.at(-1)!.numero} sur ${total}`;
  });

  /** « séances 9 à 12 » : les numéros des séances d'un Mois. */
  protected numerosDuMois(numero: number, nombre: number): string {
    const premiere = (numero - 1) * 4 + 1;
    return nombre === 1 ? `séance ${premiere}` : `séances ${premiere} à ${premiere + nombre - 1}`;
  }

  private get id(): string {
    return this.route.snapshot.paramMap.get('id') ?? '';
  }

  async ngOnInit(): Promise<void> {
    this.fermeture.controls.dateDeFermeture.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((date) => this.dateDeFermeture.set(date));
    try {
      const [fiche, inscriptions] = await Promise.all([
        firstValueFrom(this.http.get<FicheDuGroupe>(`/api/groupes/${this.id}`)),
        firstValueFrom(this.http.get<InscriptionDeLEleve[]>('/api/inscriptions', { params: { groupe: this.id } })),
      ]);
      this.fiche.set(fiche);
      this.inscriptions.set(inscriptions);
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
      prix: fiche.prixDuMois === null ? '' : ecrireUnMontant(fiche.prixDuMois),
    });
    this.succes.set(null);
    this.erreur.set(null);
    this.enModification.set(true);
  }

  protected annuler(): void {
    this.enModification.set(false);
    this.erreur.set(null);
  }

  protected ouvrirLaFermeture(): void {
    this.fermeture.setValue({ dateDeFermeture: dateDuJourLocale(), motif: '' });
    this.succes.set(null);
    this.erreur.set(null);
    this.enFermeture.set(true);
  }

  protected annulerLaFermeture(): void {
    this.enFermeture.set(false);
    this.erreur.set(null);
  }

  protected async fermer(): Promise<void> {
    if (this.envoi()) return;

    const { dateDeFermeture, motif } = this.fermeture.getRawValue();
    if (!dateDeFermeture || !motif.trim()) return this.refuser(FERMETURE_INCOMPLETE);

    this.envoi.set(true);
    this.succes.set(null);
    this.erreur.set(null);
    try {
      const fiche = await firstValueFrom(
        this.http.post<FicheDuGroupe>(`/api/groupes/${this.id}/fermeture`, { date: dateDeFermeture, motif: motif.trim() }),
      );
      this.fiche.set(fiche);
      this.enFermeture.set(false);
      this.succes.set('Groupe fermé');
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  protected async enregistrer(): Promise<void> {
    if (this.envoi()) return;

    const valeurs = this.formulaire.getRawValue();
    // Le prix d'une Révision ou d'une Formation est fixé à la création : seul celui d'une Étude se change.
    const etude = this.fiche()?.type === 'Etude';
    const prix = etude ? lireUnNombre(valeurs.prix) : null;
    const capacite = lireUnNombre(valeurs.capacite);
    if (etude && (prix === null || prix === undefined)) return this.refuser(PRIX_ILLISIBLE);
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
