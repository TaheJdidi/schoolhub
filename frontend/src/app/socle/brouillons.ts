import { DestroyRef, inject, Injectable, Signal, signal } from '@angular/core';
import { AbstractControl, FormArray, FormGroup } from '@angular/forms';

/**
 * Les formulaires commencés puis laissés pour une autre page : on les retrouve en revenant. Gardés en mémoire seulement,
 * comme la session (jamais dans le stockage du navigateur) : ils disparaissent en rechargeant la page et à la
 * déconnexion, pour que la personne suivante, sur le même ordinateur de l'accueil, ne les voie pas.
 */
@Injectable({ providedIn: 'root' })
export class Brouillons {
  private readonly gardes = new Map<string, unknown>();

  garder<T>(cle: string, brouillon: T): void {
    this.gardes.set(cle, structuredClone(brouillon));
  }

  /** Le brouillon gardé sous cette clé, ou `null`. */
  reprendre<T>(cle: string): T | null {
    const brouillon = this.gardes.get(cle);
    return brouillon === undefined ? null : (structuredClone(brouillon) as T);
  }

  oublier(cle: string): void {
    this.gardes.delete(cle);
  }

  /** À la déconnexion : plus aucun brouillon. */
  toutOublier(): void {
    this.gardes.clear();
  }
}

/** Ce qu'un formulaire garde en plus de ses champs : le type choisi, une case hors du formulaire… */
export interface EtatDuBrouillon<E> {
  lire: () => E;
  ecrire: (etat: E) => void;
}

export interface OptionsDuBrouillon<E> {
  /** Les listes du formulaire (`FormArray`), par nom, et comment créer une ligne vide. */
  lignes?: Record<string, () => AbstractControl>;
  /** Ce qui est gardé hors des champs du formulaire. */
  etat?: EtatDuBrouillon<E>;
  /** Un formulaire qui s'ouvre et se ferme sur la page : gardé seulement s'il est ouvert en partant. */
  aGarder?: () => boolean;
}

/** Le brouillon d'un formulaire, tel que la page le manie. */
export interface BrouillonDUnFormulaire {
  /** Le formulaire vient d'être repris d'une visite précédente. */
  readonly repris: Signal<boolean>;
  /** Reprend le brouillon gardé, s'il y en a un (à appeler une fois les listes de la page chargées). */
  reprendre(): boolean;
  /** Après un envoi réussi : plus de brouillon, et rien n'est gardé en quittant la page. */
  oublier(): void;
  /** « Recommencer » : le formulaire vide, comme à la première visite, et plus de brouillon. */
  recommencer(): void;
}

type Brouillon<E> = { valeurs: unknown; etat?: E };

/**
 * Le brouillon d'une page sans formulaire Angular (des cases cochées, des choix gardés dans des signaux) : ce que
 * `lire` rend est gardé en quittant la page si `aGarder` le dit (la saisie a changé), et rendu à `ecrire` en revenant.
 */
export function garderUnBrouillon<E>(cle: string | (() => string), etat: EtatDuBrouillon<E> & { aGarder: () => boolean }): BrouillonDUnFormulaire {
  const brouillons = inject(Brouillons);
  const laCle = typeof cle === 'string' ? () => cle : cle;
  const repris = signal(false);
  let etatDuDebut: { etat: E } | null = null;

  inject(DestroyRef).onDestroy(() => {
    if (etat.aGarder()) brouillons.garder(laCle(), etat.lire());
    else if (repris()) brouillons.oublier(laCle());
  });

  return {
    repris,
    reprendre() {
      const brouillon = brouillons.reprendre<E>(laCle());
      if (brouillon === null) return false;
      etatDuDebut ??= { etat: etat.lire() };
      etat.ecrire(brouillon);
      repris.set(true);
      return true;
    },
    oublier() {
      brouillons.oublier(laCle());
      repris.set(false);
    },
    recommencer() {
      brouillons.oublier(laCle());
      if (etatDuDebut) etat.ecrire(etatDuDebut.etat);
      repris.set(false);
    },
  };
}

/**
 * Garde ce qui a été saisi dans le formulaire quand on quitte la page sans l'envoyer, et le reprend en revenant. À
 * appeler pendant la construction de la page. Un formulaire laissé tel quel n'est pas gardé. La clé peut dépendre de la
 * page (« eleve-<id> ») : elle est relue en quittant et en reprenant.
 */
export function garderLeBrouillon<E = never>(
  cle: string | (() => string),
  formulaire: FormGroup,
  options: OptionsDuBrouillon<E> = {},
): BrouillonDUnFormulaire {
  const brouillons = inject(Brouillons);
  const laCle = typeof cle === 'string' ? () => cle : cle;
  const repris = signal(false);
  const lignes = Object.entries(options.lignes ?? {});
  // Le formulaire de la première visite, pour « Recommencer ».
  const longueursDuDebut = new Map(lignes.map(([nom]) => [nom, (formulaire.get(nom) as FormArray).length]));
  let etatDuDebut: { etat: E } | null = null;

  const ajuster = (nom: string, longueur: number, nouvelle: () => AbstractControl) => {
    const liste = formulaire.get(nom) as FormArray;
    while (liste.length > longueur) liste.removeAt(liste.length - 1, { emitEvent: false });
    while (liste.length < longueur) liste.push(nouvelle(), { emitEvent: false });
  };

  inject(DestroyRef).onDestroy(() => {
    if (!formulaire.dirty || options.aGarder?.() === false) {
      // Fermé ou laissé tel quel : l'ancien brouillon n'a plus lieu d'être.
      if (repris()) brouillons.oublier(laCle());
      return;
    }
    brouillons.garder<Brouillon<E>>(laCle(), { valeurs: formulaire.getRawValue(), etat: options.etat?.lire() });
  });

  return {
    repris,
    reprendre() {
      const brouillon = brouillons.reprendre<Brouillon<E>>(laCle());
      if (!brouillon) return false;
      if (options.etat) {
        etatDuDebut ??= { etat: options.etat.lire() };
        if (brouillon.etat !== undefined) options.etat.ecrire(brouillon.etat);
      }
      const valeurs = brouillon.valeurs as Record<string, unknown>;
      for (const [nom, nouvelle] of lignes) {
        const gardees = valeurs[nom];
        if (Array.isArray(gardees)) ajuster(nom, gardees.length, nouvelle);
      }
      formulaire.patchValue(valeurs);
      formulaire.markAsDirty();
      repris.set(true);
      return true;
    },
    oublier() {
      brouillons.oublier(laCle());
      formulaire.markAsPristine();
      repris.set(false);
    },
    recommencer() {
      brouillons.oublier(laCle());
      if (options.etat && etatDuDebut) options.etat.ecrire(etatDuDebut.etat);
      for (const [nom, nouvelle] of lignes) ajuster(nom, longueursDuDebut.get(nom)!, nouvelle);
      formulaire.reset();
      repris.set(false);
    },
  };
}
