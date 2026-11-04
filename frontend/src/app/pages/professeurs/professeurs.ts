import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { EnregistrementDuProfesseur, groupesOuverts, nombreDeProfesseurs, nomComplet, Professeur, trier } from './professeur';

const ADRESSE = '/api/professeurs';

type Filtre = 'au-centre' | 'partis';

/** Ce que montre la colonne de droite : la fiche du professeur choisi, ou le formulaire. */
type Mode = 'fiche' | 'ajout' | 'modification';

/**
 * GRP-01, GRP-02 (la liste) et GRP-03 : la liste des professeurs à gauche, la fiche du professeur choisi à droite.
 * La liste se charge une fois ; après une action, elle prend le professeur renvoyé par le serveur.
 * Le formulaire d'ajout ou de modification remplace la fiche ; un téléphone déjà pris donne un avertissement orange.
 * Une seule action à la fois : pendant un envoi, les boutons sont bloqués.
 */
@Component({
  selector: 'sh-professeurs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  templateUrl: './professeurs.html',
  styleUrl: './professeurs.css',
})
export class Professeurs implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly groupesOuverts = groupesOuverts;
  protected readonly nomComplet = nomComplet;

  protected readonly professeurs = signal<Professeur[]>([]);
  /** La liste est arrivée : sans elle, rien ne s'ajoute. */
  protected readonly charge = signal(false);
  protected readonly erreurDeLaListe = signal<string | null>(null);
  protected readonly filtre = signal<Filtre>('au-centre');
  private readonly choisiId = signal<string | null>(null);
  /** La suppression du professeur choisi attend « Oui » ou « Non ». */
  protected readonly aConfirmer = signal(false);
  protected readonly mode = signal<Mode>('fiche');
  /** Le professeur dont la fiche est en train de changer. */
  private modifieId: string | null = null;
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  /** « Ce téléphone est aussi celui de … » : le professeur est enregistré, la secrétaire est prévenue. */
  protected readonly avertissement = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  protected readonly formulaire = new FormGroup({
    prenom: new FormControl('', { nonNullable: true }),
    nom: new FormControl('', { nonNullable: true }),
    telephone: new FormControl('', { nonNullable: true }),
    email: new FormControl('', { nonNullable: true }),
  });

  protected readonly visibles = computed(() => this.professeurs().filter((p) => p.parti === (this.filtre() === 'partis')));
  protected readonly sousTitre = computed(() => nombreDeProfesseurs(this.professeurs().filter((p) => !p.parti).length));
  /** Le professeur choisi, ou à défaut le premier de la liste affichée. */
  protected readonly choisi = computed(
    () => this.visibles().find((p) => p.id === this.choisiId()) ?? this.visibles()[0] ?? null,
  );

  async ngOnInit(): Promise<void> {
    try {
      this.professeurs.set(await firstValueFrom(this.http.get<Professeur[]>(ADRESSE)));
      this.charge.set(true);
    } catch (erreur) {
      this.erreurDeLaListe.set(messageDErreur(erreur));
    }
  }

  protected filtrer(filtre: Filtre): void {
    this.filtre.set(filtre);
    this.choisiId.set(null);
    this.revenirALaFiche();
  }

  protected choisir(professeur: Professeur): void {
    this.choisiId.set(professeur.id);
    this.revenirALaFiche();
  }

  protected ouvrirLAjout(): void {
    this.revenirALaFiche();
    this.formulaire.reset();
    this.modifieId = null;
    this.mode.set('ajout');
  }

  protected ouvrirLaModification(professeur: Professeur): void {
    this.revenirALaFiche();
    this.formulaire.setValue({
      prenom: professeur.prenom,
      nom: professeur.nom,
      telephone: professeur.telephone,
      email: professeur.email ?? '',
    });
    this.modifieId = professeur.id;
    this.mode.set('modification');
  }

  protected annuler(): void {
    this.revenirALaFiche();
  }

  protected enregistrer(): Promise<void> {
    const { prenom, nom, telephone, email } = this.formulaire.getRawValue();
    const fiche = { prenom: prenom.trim(), nom: nom.trim(), telephone: telephone.trim(), email: email.trim() };
    if (!fiche.prenom || !fiche.nom || !fiche.telephone) {
      this.succes.set(null);
      this.erreur.set('Remplissez le prénom, le nom et le téléphone');
      return Promise.resolve();
    }

    const modifieId = this.modifieId;
    return this.agir(async () => {
      const enregistrement = await firstValueFrom(
        modifieId
          ? this.http.put<EnregistrementDuProfesseur>(`${ADRESSE}/${modifieId}`, fiche)
          : this.http.post<EnregistrementDuProfesseur>(ADRESSE, fiche),
      );
      this.montrer(enregistrement.professeur);
      this.mode.set('fiche');
      this.avertissement.set(enregistrement.avertissement);
      return modifieId ? 'Fiche enregistrée' : 'Professeur ajouté';
    });
  }

  protected partir(professeur: Professeur): Promise<void> {
    return this.agir(async () => {
      const aJour = await firstValueFrom(this.http.post<Professeur>(`${ADRESSE}/${professeur.id}/depart`, null));
      this.montrer(aJour);
      return `${nomComplet(aJour)} a quitté le centre`;
    });
  }

  protected revenir(professeur: Professeur): Promise<void> {
    return this.agir(async () => {
      const aJour = await firstValueFrom(this.http.post<Professeur>(`${ADRESSE}/${professeur.id}/retour`, null));
      this.montrer(aJour);
      return `${nomComplet(aJour)} est de retour au centre`;
    });
  }

  protected demanderConfirmation(): void {
    this.succes.set(null);
    this.avertissement.set(null);
    this.erreur.set(null);
    this.aConfirmer.set(true);
  }

  protected annulerLaConfirmation(): void {
    this.aConfirmer.set(false);
  }

  protected supprimer(professeur: Professeur): Promise<void> {
    this.aConfirmer.set(false);
    return this.agir(async () => {
      await firstValueFrom(this.http.delete(`${ADRESSE}/${professeur.id}`));
      this.professeurs.update((liste) => liste.filter((p) => p.id !== professeur.id));
      this.choisiId.set(null);
      return `Fiche de ${nomComplet(professeur)} supprimée`;
    });
  }

  /** Le professeur renvoyé par le serveur prend sa place dans la liste, et sa fiche s'affiche dans le bon filtre. */
  private montrer(aJour: Professeur): void {
    this.professeurs.update((liste) => trier([...liste.filter((p) => p.id !== aJour.id), aJour]));
    this.filtre.set(aJour.parti ? 'partis' : 'au-centre');
    this.choisiId.set(aJour.id);
  }

  private revenirALaFiche(): void {
    this.mode.set('fiche');
    this.aConfirmer.set(false);
    this.succes.set(null);
    this.avertissement.set(null);
    this.erreur.set(null);
  }

  /** Une action vers le serveur : le message vert la confirme, ou le refus s'affiche en rouge, sous la fiche. */
  private async agir(action: () => Promise<string>): Promise<void> {
    if (this.envoi()) return;

    this.envoi.set(true);
    this.succes.set(null);
    this.avertissement.set(null);
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
