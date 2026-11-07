import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { BrouillonRepris } from '../../socle/brouillon-repris';
import { garderLeBrouillon } from '../../socle/brouillons';
import { messageDErreur } from '../../socle/erreurs';
import { Niveau } from '../parametres/carte-des-niveaux';
import { aSesPropresContacts, EnregistrementDeLEleve, FicheDeLEleve } from './eleve';

const INCOMPLET = 'Remplissez les champs obligatoires';

/**
 * ELV-02 : le formulaire de la fiche d'un élève, pour la créer (sans `eleve`) ou la modifier. Le téléphone et l'email
 * de l'élève deviennent obligatoires à partir de la 7ᵉ année ; le téléphone du parent, facultatif pour un élève majeur.
 * Si les niveaux n'ont pas pu se charger, rien ne part. Réussi, il rend l'enregistrement au parent.
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
  readonly enregistre = output<EnregistrementDeLEleve>();
  readonly annule = output<void>();

  protected readonly niveaux = signal<Niveau[]>([]);
  protected readonly charge = signal(false);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);
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

  /** Laissée pour une autre page, la fiche reprend où elle en était en revenant (une par élève modifié). */
  protected readonly brouillon = garderLeBrouillon(() => {
    const eleve = this.eleve();
    return eleve ? `eleve-${eleve.id}` : this.brouillonDeLaCreation();
  }, this.formulaire);

  async ngOnInit(): Promise<void> {
    const controles = this.formulaire.controls;
    controles.niveauId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((id) => this.niveauChoisi.set(id));
    controles.majeur.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((majeur) => this.majeur.set(majeur));
    this.remplir();
    this.brouillon.reprendre();

    try {
      this.niveaux.set(await firstValueFrom(this.http.get<Niveau[]>('/api/niveaux')));
      this.charge.set(true);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
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
      });
      this.formulaire.markAsPristine();
    }
  }

  protected async enregistrer(): Promise<void> {
    if (this.envoi() || !this.charge()) return;

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
      this.enregistre.emit(enregistrement);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
