import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, output, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom, merge } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderLeBrouillon } from '../../socle/brouillons';
import { requis, reverifier } from '../../socle/champs-requis';
import { messageDErreur } from '../../socle/erreurs';
import { ResumeDuGroupe } from '../groupes/groupe';
import { Niveau } from '../parametres/carte-des-niveaux';
import { nomComplet, Professeur, trier } from '../professeurs/professeur';
import { aSesPropresContacts, EnregistrementDeLEleve, FicheDeLEleve } from './eleve';

const INCOMPLET = 'Remplissez les champs obligatoires';

/**
 * ELV-02 : le formulaire de la fiche d'un élève, pour la créer (sans `eleve`) ou la modifier. Le téléphone et l'email
 * de l'élève deviennent obligatoires à partir de la 7ᵉ année ; le téléphone du parent, facultatif pour un élève majeur.
 * Si les niveaux n'ont pas pu se charger, rien ne part. Réussi, il rend l'enregistrement au parent. À la création, on
 * peut aussi choisir le professeur, puis l'un de ses groupes : le parent ouvre alors l'inscription dans ce groupe.
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
  /** À la création : « Professeur » puis « Groupe » (facultatifs), pour inscrire l'élève juste après. */
  readonly proposerUnGroupe = input(false);
  readonly enregistre = output<EnregistrementDeLEleve & { groupeId: string | null }>();
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

  protected readonly formulaire = new FormGroup({
    prenom: new FormControl('', { nonNullable: true, validators: requis() }),
    nom: new FormControl('', { nonNullable: true, validators: requis() }),
    niveauId: new FormControl('', { nonNullable: true, validators: requis() }),
    etablissement: new FormControl('', { nonNullable: true }),
    telephoneEleve: new FormControl('', { nonNullable: true, validators: requis(() => this.propresContacts()) }),
    emailEleve: new FormControl('', { nonNullable: true, validators: requis(() => this.propresContacts()) }),
    majeur: new FormControl(false, { nonNullable: true }),
    telephoneParent: new FormControl('', { nonNullable: true, validators: requis(() => !this.majeur()) }),
    emailParent: new FormControl('', { nonNullable: true }),
    professeurId: new FormControl('', { nonNullable: true }),
    groupeId: new FormControl('', { nonNullable: true }),
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
    // La classe et « majeur » changent les champs obligatoires.
    merge(controles.niveauId.valueChanges, controles.majeur.valueChanges)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => reverifier(this.formulaire));
    this.remplir();
    this.brouillon.reprendre();

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
      this.charge.set(true);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
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
    };
    const contactsDeLEleve = !this.propresContacts() || (fiche.telephoneEleve && fiche.emailEleve);
    const contactDuParent = fiche.majeur || fiche.telephoneParent;
    if (!fiche.prenom || !fiche.nom || !fiche.niveauId || !contactsDeLEleve || !contactDuParent) {
      this.erreur.set(INCOMPLET);
      return;
    }

    this.envoi.set(true);
    this.erreur.set(null);
    try {
      const eleve = this.eleve();
      const enregistrement = await firstValueFrom(
        eleve
          ? this.http.put<EnregistrementDeLEleve>(`/api/eleves/${eleve.id}`, fiche)
          : this.http.post<EnregistrementDeLEleve>('/api/eleves', fiche),
      );
      this.brouillon.oublier();
      this.enregistre.emit({ ...enregistrement, groupeId: eleve ? null : valeurs.groupeId || null });
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
