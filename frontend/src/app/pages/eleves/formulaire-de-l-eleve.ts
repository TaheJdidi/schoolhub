import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, output, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom, merge } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderLeBrouillon } from '../../socle/brouillons';
import { requis, reverifier } from '../../socle/champs-requis';
import { messageDErreur } from '../../socle/erreurs';
import { FicheDuGroupe, ResumeDuGroupe } from '../groupes/groupe';
import { datesDuGroupe, EnregistrementDeLInscription, modesDuGroupe, premiereSeanceProposee } from '../inscriptions/inscription';
import { Niveau } from '../parametres/carte-des-niveaux';
import { nomComplet, Professeur, trier } from '../professeurs/professeur';
import { aSesPropresContacts, EnregistrementDeLEleve, FicheDeLEleve } from './eleve';

const INCOMPLET = 'Remplissez les champs obligatoires';
const GROUPE_EN_CHARGEMENT = 'Le groupe se charge encore, un instant';

/** Ce que le formulaire rend : la fiche, et l'inscription faite avec elle (ou son refus) si un groupe était choisi. */
export type EleveEnregistre = EnregistrementDeLEleve & {
  groupeId: string | null;
  inscription: EnregistrementDeLInscription | null;
  refusDeLInscription: string | null;
};

/**
 * ELV-02 : le formulaire de la fiche d'un élève, pour la créer (sans `eleve`) ou la modifier. Le téléphone et l'email
 * de l'élève deviennent obligatoires à partir de la 7ᵉ année ; le téléphone du parent, facultatif pour un élève majeur.
 * Si les niveaux n'ont pas pu se charger, rien ne part. Réussi, il rend l'enregistrement au parent. À la création, on
 * peut aussi choisir le professeur, puis l'un de ses groupes, le mode et la première séance : « Enregistrer » crée la
 * fiche, puis inscrit l'élève dans ce groupe.
 */
@Component({
  selector: 'sh-formulaire-de-l-eleve',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, BrouillonRepris],
  templateUrl: './formulaire-de-l-eleve.html',
  styleUrl: './eleves.css',
})
export class FormulaireDeLEleve implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  /** La fiche à modifier ; sans elle, le formulaire crée un élève. */
  readonly eleve = input<FicheDeLEleve | null>(null);
  /** La clé du brouillon d'une nouvelle fiche : chaque endroit qui crée un élève garde le sien. */
  readonly brouillonDeLaCreation = input('eleve-nouveau');
  /** À la création : « Professeur » puis « Groupe » (facultatifs), pour inscrire l'élève en même temps. */
  readonly proposerUnGroupe = input(false);
  /** Créé depuis un groupe qui a un niveau : l'élève prend ce niveau, qui ne se choisit pas. */
  readonly niveauDuGroupe = input<{ id: string; nom: string } | null>(null);
  /** Une personne de l'extérieur, à la bibliothèque : pas de section « Le parent », son téléphone est obligatoire. */
  readonly sansParent = input(false);
  readonly enregistre = output<EleveEnregistre>();
  readonly annule = output<void>();

  protected readonly niveaux = signal<Niveau[]>([]);
  protected readonly charge = signal(false);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);
  private readonly niveauChoisi = signal('');
  protected readonly majeur = signal(false);
  protected readonly propresContacts = computed(() => aSesPropresContacts(this.niveaux(), this.niveauChoisi()));

  /** Les groupes ouverts du centre, pour « Groupe (facultatif) ». */
  private readonly groupes = signal<ResumeDuGroupe[]>([]);
  private readonly ouverts = computed(() => this.groupes().filter((g) => !g.ferme && !g.termine));
  private readonly tousLesProfesseurs = signal<Professeur[]>([]);
  /** Les professeurs au centre qui ont un groupe ouvert : deux professeurs peuvent enseigner la même matière à la même heure. */
  protected readonly professeurs = computed(() => {
    const avecUnGroupe = new Set(this.ouverts().map((g) => g.professeurId));
    return trier(this.tousLesProfesseurs().filter((p) => !p.parti && avecUnGroupe.has(p.id)));
  });
  protected readonly nomComplet = nomComplet;
  /** Les groupes du professeur choisi : ceux du niveau de l'élève d'abord, puis les autres ; chaque partie par nom. */
  protected readonly sectionsDeGroupes = computed(() => {
    const niveau = this.niveaux().find((n) => n.id === this.niveauChoisi())?.nom ?? null;
    const professeur = this.professeurChoisi();
    const ouverts = this.ouverts()
      .filter((g) => g.professeurId === professeur)
      .sort((a, b) => a.nom.localeCompare(b.nom, 'fr'));
    return [
      { titre: 'Groupes de son niveau', groupes: ouverts.filter((g) => niveau !== null && g.niveau === niveau) },
      { titre: niveau ? 'Autres groupes' : 'Groupes', groupes: ouverts.filter((g) => niveau === null || g.niveau !== niveau) },
    ].filter((section) => section.groupes.length > 0);
  });

  /** La fiche du groupe choisi : ses modes et ses séances. */
  protected readonly ficheDuGroupe = signal<FicheDuGroupe | null>(null);
  protected readonly modes = computed(() => modesDuGroupe(this.ficheDuGroupe()));
  protected readonly dates = computed(() => datesDuGroupe(this.ficheDuGroupe()));
  private dernierGroupe = 0;

  protected readonly formulaire = new FormGroup({
    prenom: new FormControl('', { nonNullable: true, validators: requis() }),
    nom: new FormControl('', { nonNullable: true, validators: requis() }),
    niveauId: new FormControl('', { nonNullable: true, validators: requis() }),
    etablissement: new FormControl('', { nonNullable: true }),
    telephoneEleve: new FormControl('', { nonNullable: true, validators: requis(() => this.propresContacts() || this.sansParent()) }),
    emailEleve: new FormControl('', { nonNullable: true, validators: requis(() => this.propresContacts()) }),
    majeur: new FormControl(false, { nonNullable: true }),
    telephoneParent: new FormControl('', { nonNullable: true, validators: requis(() => !this.majeur() && !this.sansParent()) }),
    emailParent: new FormControl('', { nonNullable: true }),
    professeurId: new FormControl('', { nonNullable: true }),
    groupeId: new FormControl('', { nonNullable: true }),
    // Le mode, pour une Révision ou une Formation à tranches ; la première séance, dès qu'un groupe est choisi.
    mode: new FormControl('', { nonNullable: true, validators: requis(() => this.modes().length > 0) }),
    debutLe: new FormControl('', { nonNullable: true, validators: requis(() => this.ficheDuGroupe() !== null) }),
  });

  protected readonly professeurChoisi = toSignal(this.formulaire.controls.professeurId.valueChanges, { initialValue: '' });


  /** Laissée pour une autre page, la fiche reprend où elle en était en revenant (une par élève modifié). */
  protected readonly brouillon = garderLeBrouillon(() => {
    const eleve = this.eleve();
    return eleve ? `eleve-${eleve.id}` : this.brouillonDeLaCreation();
  }, this.formulaire);

  async ngOnInit(): Promise<void> {
    const controles = this.formulaire.controls;
    controles.niveauId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((id) => this.niveauChoisi.set(id));
    controles.majeur.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((majeur) => this.majeur.set(majeur));
    // Un autre professeur : le groupe se choisit de nouveau parmi les siens.
    controles.professeurId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.oublierUnGroupeQuiNEstPlusPropose());
    // Un autre groupe : son mode se choisit de nouveau, et sa première séance est proposée.
    controles.groupeId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((id) => {
      controles.mode.setValue('');
      void this.chargerLeGroupe(id);
    });
    // La classe et « majeur » changent les champs obligatoires.
    merge(controles.niveauId.valueChanges, controles.majeur.valueChanges)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => reverifier(this.formulaire));
    this.remplir();
    this.brouillon.reprendre();
    const niveauDuGroupe = this.niveauDuGroupe();
    if (niveauDuGroupe && !this.eleve()) controles.niveauId.setValue(niveauDuGroupe.id);

    try {
      const avecGroupe = this.proposerUnGroupe();
      const [niveaux, groupes, professeurs] = await Promise.all([
        firstValueFrom(this.http.get<Niveau[]>('/api/niveaux')),
        avecGroupe ? firstValueFrom(this.http.get<ResumeDuGroupe[]>('/api/groupes')) : Promise.resolve([]),
        avecGroupe ? firstValueFrom(this.http.get<Professeur[]>('/api/professeurs')) : Promise.resolve([]),
      ]);
      this.niveaux.set(niveaux);
      this.groupes.set(groupes);
      this.tousLesProfesseurs.set(professeurs);
      // Un professeur parti, ou un groupe fermé, entre-temps : il ne se propose plus.
      const professeurId = this.formulaire.controls.professeurId;
      if (!this.professeurs().some((p) => p.id === professeurId.value)) professeurId.setValue('');
      this.oublierUnGroupeQuiNEstPlusPropose();
      // Un brouillon repris garde son groupe, son mode et sa première séance s'ils valent encore.
      if (this.formulaire.controls.groupeId.value) await this.chargerLeGroupe(this.formulaire.controls.groupeId.value);
      this.charge.set(true);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** La fiche du groupe choisi ; seule la dernière réponse compte. */
  private async chargerLeGroupe(id: string): Promise<void> {
    const numero = ++this.dernierGroupe;
    this.ficheDuGroupe.set(null);
    const { mode, debutLe } = this.formulaire.controls;
    if (!id) {
      debutLe.setValue('');
      return;
    }
    try {
      const fiche = await firstValueFrom(this.http.get<FicheDuGroupe>(`/api/groupes/${id}`));
      if (numero !== this.dernierGroupe) return;
      this.ficheDuGroupe.set(fiche);
      if (!this.modes().some((m) => m.mode === mode.value)) mode.setValue('');
      if (!this.dates().some((d) => d.date === debutLe.value)) debutLe.setValue(premiereSeanceProposee(this.dates()) ?? '');
    } catch (erreur) {
      if (numero === this.dernierGroupe) this.erreur.set(messageDErreur(erreur));
    }
  }

  private oublierUnGroupeQuiNEstPlusPropose(): void {
    const groupeId = this.formulaire.controls.groupeId;
    if (!this.sectionsDeGroupes().some((s) => s.groupes.some((g) => g.id === groupeId.value))) groupeId.setValue('');
  }

  /** « Groupe Samedi 10h → 12h · Mathématiques · 9ᵉ année », et « complet » s'il n'a plus de place. */
  protected libelleDuGroupe(groupe: ResumeDuGroupe): string {
    const parties = [groupe.nom, groupe.matiere, groupe.niveau].filter(Boolean).join(' · ');
    return groupe.capacite !== null && groupe.eleves >= groupe.capacite ? `${parties} (complet)` : parties;
  }

  /** « Annuler » : le brouillon part aussi. */
  protected annuler(): void {
    this.brouillon.oublier();
    this.annule.emit();
  }

  /** La fiche à modifier, telle qu'elle est enregistrée ; rien pour une nouvelle fiche. */
  protected remplir(): void {
    const eleve = this.eleve();
    if (eleve) {
      this.formulaire.setValue({
        prenom: eleve.prenom,
        nom: eleve.nom,
        niveauId: eleve.niveau.id,
        etablissement: eleve.etablissement ?? '',
        telephoneEleve: eleve.telephoneEleve ?? '',
        emailEleve: eleve.emailEleve ?? '',
        majeur: eleve.majeur,
        telephoneParent: eleve.telephoneParent ?? '',
        emailParent: eleve.emailParent ?? '',
        professeurId: '',
        groupeId: '',
        mode: '',
        debutLe: '',
      });
      this.formulaire.markAsPristine();
    }
  }

  protected async enregistrer(): Promise<void> {
    if (this.envoi() || !this.charge()) return;

    reverifier(this.formulaire);
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
      ...(this.sansParent() ? { sansParent: true, telephoneParent: '', emailParent: '' } : {}),
    };
    const contactsDeLEleve =
      (!this.propresContacts() || (fiche.telephoneEleve && fiche.emailEleve)) && (!this.sansParent() || fiche.telephoneEleve);
    const contactDuParent = this.sansParent() || fiche.majeur || fiche.telephoneParent;
    const eleve = this.eleve();
    const groupe = eleve ? null : this.ficheDuGroupe();
    if (!eleve && valeurs.groupeId && !groupe) {
      this.erreur.set(GROUPE_EN_CHARGEMENT);
      return;
    }
    const inscriptionIncomplete = groupe !== null && ((this.modes().length > 0 && !valeurs.mode) || !valeurs.debutLe);
    if (!fiche.prenom || !fiche.nom || !fiche.niveauId || !contactsDeLEleve || !contactDuParent || inscriptionIncomplete) {
      this.erreur.set(INCOMPLET);
      return;
    }

    this.envoi.set(true);
    this.erreur.set(null);
    try {
      const enregistrement = await firstValueFrom(
        eleve
          ? this.http.put<EnregistrementDeLEleve>(`/api/eleves/${eleve.id}`, fiche)
          : this.http.post<EnregistrementDeLEleve>('/api/eleves', fiche),
      );
      this.brouillon.oublier();
      // La fiche est créée : l'inscription part ensuite. Refusée (groupe fermé entre-temps…), la fiche reste créée.
      let inscription: EnregistrementDeLInscription | null = null;
      let refusDeLInscription: string | null = null;
      if (groupe) {
        try {
          inscription = await firstValueFrom(
            this.http.post<EnregistrementDeLInscription>('/api/inscriptions', {
              eleveId: enregistrement.eleve.id,
              groupeId: groupe.id,
              mode: valeurs.mode || null,
              debutLe: valeurs.debutLe,
            }),
          );
        } catch (erreur) {
          refusDeLInscription = messageDErreur(erreur);
        }
      }
      this.enregistre.emit({ ...enregistrement, groupeId: groupe?.id ?? null, inscription, refusDeLInscription });
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
