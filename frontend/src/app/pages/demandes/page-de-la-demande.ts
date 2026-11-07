import { formatDate } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderLeBrouillon } from '../../socle/brouillons';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { NombreDeDemandes } from '../../socle/nombre-de-demandes';
import { aSesPropresContacts } from '../eleves/eleve';
import { eleves, heure, TYPES } from '../groupes/groupe';
import { CAPACITE_ATTEINTE, estComplet, MessageWhatsApp, ModeDeTarification } from '../inscriptions/inscription';
import { Niveau } from '../parametres/carte-des-niveaux';
import {
  DemandeEnregistree,
  DetailDeLaDemande,
  GroupeProposeALaDemande,
  libelleDuChoix,
  MatiereDeLaDemande,
  MotifDuRefus,
  MOTIFS,
  professeurEtHoraire,
} from './demande';

const INCOMPLET = 'Remplissez les champs obligatoires';
const AUCUN_GROUPE = 'Choisissez au moins un groupe';
const CHOISIR_LE_MODE = 'Choisissez le mode de tarification';

/** « Nouvelle fiche » : la fiche de la demande ; sinon l'identifiant d'une fiche existante. */
const NOUVELLE_FICHE = '';

/**
 * DEM-04 et DEM-05 : une demande d'inscription. La secrétaire vérifie la fiche pré-remplie, ou choisit la fiche d'un
 * élève qui existe déjà ; elle choisit le groupe de chaque matière (le préféré est proposé, les amis cités déjà inscrits
 * sont affichés), puis enregistre : l'élève est inscrit, et le message WhatsApp de confirmation est prêt. Elle peut aussi
 * refuser la demande, avec un motif, sans qu'aucun message ne parte.
 */
@Component({
  selector: 'sh-page-de-la-demande',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, BrouillonRepris],
  templateUrl: './page-de-la-demande.html',
  styleUrls: ['../eleves/eleves.css', '../inscriptions/inscriptions.css'],
  styles: `
    .matiere { display: grid; gap: 8px; border: 0; margin: 0; padding: 0; min-width: 0; }
    .prefere { color: var(--ink-3); font-weight: 400; }
    .amis { color: var(--ok) !important; }
    .lien-fiche { color: var(--accent); font-size: 13px; text-decoration: none; }
    .motif { width: auto; max-width: 100%; }
    .refuser { color: var(--bad); border-color: var(--bad); }
    .somme small { color: var(--ink-2); white-space: nowrap; }
  `,
})
export class PageDeLaDemande implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly nombre = inject(NombreDeDemandes);
  private readonly destroyRef = inject(DestroyRef);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  protected readonly types = TYPES;
  protected readonly motifs = MOTIFS;
  protected readonly nombreDEleves = eleves;
  protected readonly estComplet = estComplet;
  protected readonly professeurEtHoraire = professeurEtHoraire;
  protected readonly libelleDuChoix = libelleDuChoix;
  protected readonly capaciteAtteinte = CAPACITE_ATTEINTE;
  protected readonly nouvelleFiche = NOUVELLE_FICHE;

  protected readonly demande = signal<DetailDeLaDemande | null>(null);
  protected readonly introuvable = signal(false);
  protected readonly niveaux = signal<Niveau[]>([]);
  protected readonly erreur = signal<string | null>(null);
  protected readonly envoi = signal(false);
  protected readonly confirmation = signal<DemandeEnregistree | null>(null);

  /** La fiche choisie : `NOUVELLE_FICHE`, ou l'identifiant d'une fiche existante. */
  protected readonly fiche = signal(NOUVELLE_FICHE);
  /** Le groupe choisi pour chaque matière ; `''` : ne pas l'inscrire dans cette matière. */
  protected readonly groupes = signal<Readonly<Record<string, string>>>({});
  /** Le mode choisi pour chaque groupe qui offre un choix. */
  protected readonly modes = signal<Readonly<Record<string, ModeDeTarification>>>({});

  /** DEM-05 : la question du refus est ouverte, avec son motif. */
  protected readonly refus = signal(false);
  protected readonly motif = signal<MotifDuRefus | ''>('');

  private readonly niveauChoisi = signal('');
  protected readonly majeur = signal(false);
  protected readonly propresContacts = computed(() => aSesPropresContacts(this.niveaux(), this.niveauChoisi()));

  protected readonly formulaire = new FormGroup({
    prenom: new FormControl('', { nonNullable: true }),
    nom: new FormControl('', { nonNullable: true }),
    niveauId: new FormControl('', { nonNullable: true }),
    etablissement: new FormControl('', { nonNullable: true }),
    telephoneEleve: new FormControl('', { nonNullable: true }),
    emailEleve: new FormControl('', { nonNullable: true }),
    majeur: new FormControl(false, { nonNullable: true }),
    telephoneParent: new FormControl('', { nonNullable: true }),
    emailParent: new FormControl('', { nonNullable: true }),
  });

  /** Une demande à moitié traitée, puis laissée pour une autre page, se retrouve en revenant : la fiche et les choix. */
  protected readonly brouillon = garderLeBrouillon(() => `demande-${this.id}`, this.formulaire, {
    etat: {
      lire: () => ({ fiche: this.fiche(), groupes: this.groupes(), modes: this.modes() }),
      ecrire: (etat) => {
        this.fiche.set(etat.fiche);
        this.groupes.set(etat.groupes);
        this.modes.set(etat.modes);
      },
    },
  });

  /** « Doublon possible » : une fiche d'élève ou une autre demande à traiter lui ressemble. */
  protected readonly doublon = computed(() => {
    const demande = this.demande();
    return demande !== null && (demande.fichesSemblables.length > 0 || demande.autreDemandeSemblable);
  });

  async ngOnInit(): Promise<void> {
    const controles = this.formulaire.controls;
    controles.niveauId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((id) => this.niveauChoisi.set(id));
    controles.majeur.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((majeur) => this.majeur.set(majeur));

    try {
      const [demande, niveaux] = await Promise.all([
        firstValueFrom(this.http.get<DetailDeLaDemande>(`/api/demandes/${this.id}`)),
        firstValueFrom(this.http.get<Niveau[]>('/api/niveaux')),
      ]);
      this.niveaux.set(niveaux);
      this.montrer(demande);
      if (!demande.traiteeLe) this.brouillon.reprendre();
    } catch (erreur) {
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) {
        this.introuvable.set(true);
      } else {
        this.erreur.set(messageDErreur(erreur));
      }
    }
  }

  /** La fiche telle qu'envoyée, et pour chaque matière le groupe préféré s'il est encore proposé. */
  private montrer(demande: DetailDeLaDemande): void {
    this.demande.set(demande);
    this.formulaire.setValue({
      prenom: demande.prenom,
      nom: demande.nom,
      niveauId: demande.niveau.id,
      etablissement: demande.etablissement ?? '',
      telephoneEleve: demande.telephoneEleve ?? '',
      emailEleve: demande.emailEleve ?? '',
      majeur: demande.majeur,
      telephoneParent: demande.telephoneParent ?? '',
      emailParent: demande.emailParent ?? '',
    });
    this.groupes.set(
      Object.fromEntries(
        demande.matieres.map((m) => [m.matiere, m.groupes.some((g) => g.id === m.groupePrefere) ? m.groupePrefere : '']),
      ),
    );
  }

  /** « Recommencer » : la fiche telle que le parent l'a envoyée. */
  protected remettreLaDemande(): void {
    const demande = this.demande();
    if (demande) this.montrer(demande);
    this.formulaire.markAsPristine();
  }

  // ----- Les choix -----

  protected choisirLaFiche(id: string): void {
    this.fiche.set(id);
    this.formulaire.markAsDirty();
    this.erreur.set(null);
  }

  protected choisirLeGroupe(matiere: MatiereDeLaDemande, groupeId: string): void {
    this.groupes.update((groupes) => ({ ...groupes, [matiere.matiere]: groupeId }));
    this.formulaire.markAsDirty();
    this.erreur.set(null);
  }

  protected choisirLeMode(groupe: GroupeProposeALaDemande, mode: ModeDeTarification): void {
    this.modes.update((modes) => ({ ...modes, [groupe.id]: mode }));
    this.formulaire.markAsDirty();
    this.erreur.set(null);
  }

  /** Le groupe choisi pour cette matière, s'il y en a un. */
  protected groupeChoisi(matiere: MatiereDeLaDemande): GroupeProposeALaDemande | null {
    const id = this.groupes()[matiere.matiere];
    return matiere.groupes.find((g) => g.id === id) ?? null;
  }

  /** « samedi 27/11/2027 à 10h ». */
  protected premiereSeance(groupe: GroupeProposeALaDemande): string {
    return `${formatDate(groupe.premiereSeance.date, 'EEEE dd/MM/yyyy', FRANCAIS)} à ${heure(groupe.premiereSeance.debut)}`;
  }

  /** « Reçue le 27/11/2027 à 10:00 ». */
  protected recue(demande: DetailDeLaDemande): string {
    return `Reçue le ${formatDate(demande.recueLe, "dd/MM/yyyy 'à' HH:mm", FRANCAIS)}`;
  }

  /** « Inscrite le 28/11/2027 », « Refusée le 28/11/2027 · Doublon ». */
  protected traitee(demande: DetailDeLaDemande): string {
    const date = demande.traiteeLe ? ` le ${formatDate(demande.traiteeLe, 'dd/MM/yyyy', FRANCAIS)}` : '';
    if (demande.etat === 'Inscrite') return `Demande enregistrée${date} : l'élève est inscrit.`;
    return `Demande refusée${date} · ${demande.motif ? MOTIFS[demande.motif] : ''}`;
  }

  // ----- Enregistrer (DEM-04) -----

  protected async enregistrer(): Promise<void> {
    const demande = this.demande();
    if (this.envoi() || !demande) return;

    const choisis = demande.matieres.map((m) => this.groupeChoisi(m)).filter((g): g is GroupeProposeALaDemande => g !== null);
    if (choisis.length === 0) {
      this.erreur.set(AUCUN_GROUPE);
      return;
    }
    if (choisis.some((g) => g.modes.length > 0 && !this.modes()[g.id])) {
      this.erreur.set(CHOISIR_LE_MODE);
      return;
    }

    const eleveId = this.fiche() || null;
    const fiche = eleveId ? null : this.ficheSaisie();
    if (!eleveId && !fiche) {
      this.erreur.set(INCOMPLET);
      return;
    }

    this.envoi.set(true);
    this.erreur.set(null);
    try {
      const confirmation = await firstValueFrom(
        this.http.post<DemandeEnregistree>(`/api/demandes/${demande.id}/inscription`, {
          eleveId,
          fiche,
          groupes: choisis.map((g) => ({ groupeId: g.id, mode: this.modes()[g.id] ?? null })),
        }),
      );
      this.brouillon.oublier();
      this.confirmation.set(confirmation);
      void this.nombre.actualiser();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** La fiche du formulaire, sans les espaces autour ; `null` si un champ obligatoire manque. */
  private ficheSaisie() {
    const valeurs = this.formulaire.getRawValue();
    const fiche = {
      prenom: valeurs.prenom.trim(),
      nom: valeurs.nom.trim(),
      niveauId: valeurs.niveauId,
      majeur: valeurs.majeur,
      telephoneEleve: valeurs.telephoneEleve.trim(),
      emailEleve: valeurs.emailEleve.trim(),
      telephoneParent: valeurs.telephoneParent.trim(),
      emailParent: valeurs.emailParent.trim(),
      etablissement: valeurs.etablissement.trim(),
    };
    const contactsDeLEleve = !this.propresContacts() || (fiche.telephoneEleve && fiche.emailEleve);
    const contactDuParent = fiche.majeur || fiche.telephoneParent;
    return fiche.prenom && fiche.nom && fiche.niveauId && contactsDeLEleve && contactDuParent ? fiche : null;
  }

  /** « Au parent · 98 401 220 », « À Adem · 22 345 678 ». */
  protected pourQui(message: MessageWhatsApp): string {
    const qui = message.destinataire === 'Parent' ? 'Au parent' : `À ${message.destinataire}`;
    return `${qui} · ${message.telephone}`;
  }

  /** « samedi 27/11/2027 ». */
  protected jour(date: string): string {
    return formatDate(date, 'EEEE dd/MM/yyyy', FRANCAIS);
  }

  // ----- Refuser (DEM-05) -----

  protected demanderLeRefus(): void {
    this.refus.set(true);
    this.motif.set('');
    this.erreur.set(null);
  }

  protected annulerLeRefus(): void {
    this.refus.set(false);
    this.erreur.set(null);
  }

  protected choisirLeMotif(evenement: Event): void {
    this.motif.set((evenement.target as HTMLSelectElement).value as MotifDuRefus | '');
  }

  protected async refuser(): Promise<void> {
    const demande = this.demande();
    if (this.envoi() || !demande) return;
    if (!this.motif()) {
      this.erreur.set('Choisissez le motif du refus');
      return;
    }

    this.envoi.set(true);
    this.erreur.set(null);
    try {
      await firstValueFrom(this.http.post(`/api/demandes/${demande.id}/refus`, { motif: this.motif() }));
      this.brouillon.oublier();
      void this.router.navigate(['/demandes']);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
