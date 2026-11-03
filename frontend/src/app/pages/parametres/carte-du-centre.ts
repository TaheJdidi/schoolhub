import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { ecrireUnMontant, lireUnNombre } from '../../socle/montants';

/** Les réglages du centre, tels que l'API les décrit (GET et PUT /api/centre/parametres). Un réglage vide vaut `null`. */
export interface Reglages {
  nom: string;
  telephone: string | null;
  email: string | null;
  anneeScolaire: { debut: string | null; fin: string | null } | null;
  montantDeLAssurance: number | null;
  prixDeLEntreeALaBibliotheque: number | null;
  capaciteDeLaBibliotheque: number | null;
  partDuCentreParDefaut: number | null;
}

const ADRESSE = '/api/centre/parametres';

/**
 * CEN-02 : les coordonnées, l'année scolaire et les prix du centre, enregistrés d'un coup.
 * La carte lit les nombres écrits avec une virgule ; les autres règles sont celles du serveur, qui répond.
 */
@Component({
  selector: 'sh-carte-du-centre',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  templateUrl: './carte-du-centre.html',
  styleUrl: './parametres.css',
})
export class CarteDuCentre implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly formulaire = new FormGroup({
    nom: new FormControl('', { nonNullable: true }),
    telephone: new FormControl('', { nonNullable: true }),
    email: new FormControl('', { nonNullable: true }),
    debut: new FormControl('', { nonNullable: true }),
    fin: new FormControl('', { nonNullable: true }),
    assurance: new FormControl('', { nonNullable: true }),
    entree: new FormControl('', { nonNullable: true }),
    capacite: new FormControl('', { nonNullable: true }),
    part: new FormControl('', { nonNullable: true }),
  });

  /**
   * Les réglages du serveur sont dans le formulaire. Avant, rien ne s'enregistre : le formulaire est enregistré
   * d'un coup, et un formulaire vide effacerait l'assurance, les prix et la part du centre.
   */
  protected readonly charges = signal(false);
  /** Le serveur n'a pas d'année scolaire pour ce centre. */
  protected readonly sansAnnee = signal(false);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  ngOnInit(): Promise<void> {
    return this.charger();
  }

  protected async charger(): Promise<void> {
    this.erreur.set(null);
    try {
      this.remplir(await firstValueFrom(this.http.get<Reglages>(ADRESSE)));
      this.charges.set(true);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected async enregistrer(): Promise<void> {
    if (!this.charges() || this.envoi()) return;

    const saisie = this.lire();
    this.succes.set(null);
    if (typeof saisie === 'string') {
      this.erreur.set(saisie);
      return;
    }

    this.envoi.set(true);
    this.erreur.set(null);
    try {
      this.remplir(await firstValueFrom(this.http.put<Reglages>(ADRESSE, saisie)));
      this.succes.set('Paramètres enregistrés');
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** Le formulaire prend les réglages du serveur : téléphone groupé, montants au millime. */
  private remplir(reglages: Reglages): void {
    this.formulaire.setValue({
      nom: reglages.nom,
      telephone: reglages.telephone ?? '',
      email: reglages.email ?? '',
      debut: reglages.anneeScolaire?.debut ?? '',
      fin: reglages.anneeScolaire?.fin ?? '',
      assurance: ecrireUnMontant(reglages.montantDeLAssurance),
      entree: ecrireUnMontant(reglages.prixDeLEntreeALaBibliotheque),
      capacite: reglages.capaciteDeLaBibliotheque?.toString() ?? '',
      part: reglages.partDuCentreParDefaut?.toString().replace('.', ',') ?? '',
    });
    this.sansAnnee.set(reglages.anneeScolaire === null);
  }

  /** Les réglages à envoyer, ou le message d'un nombre illisible. Un champ vide part en `null`. */
  private lire(): Reglages | string {
    const valeurs = this.formulaire.getRawValue();

    const assurance = lireUnNombre(valeurs.assurance);
    const entree = lireUnNombre(valeurs.entree);
    if (assurance === undefined || entree === undefined) return 'Écrivez un montant, par exemple 2,500';

    const capacite = lireUnNombre(valeurs.capacite);
    if (capacite === undefined || (capacite !== null && !Number.isInteger(capacite)))
      return 'Écrivez un nombre de places, par exemple 40';

    const part = lireUnNombre(valeurs.part);
    if (part === undefined) return 'Écrivez un pourcentage, par exemple 5';

    const texte = (saisie: string) => saisie.trim() || null;
    const debut = valeurs.debut || null;
    const fin = valeurs.fin || null;
    return {
      nom: valeurs.nom.trim(),
      telephone: texte(valeurs.telephone),
      email: texte(valeurs.email),
      anneeScolaire: debut || fin ? { debut, fin } : null,
      montantDeLAssurance: assurance,
      prixDeLEntreeALaBibliotheque: entree,
      capaciteDeLaBibliotheque: capacite,
      partDuCentreParDefaut: part,
    };
  }
}
