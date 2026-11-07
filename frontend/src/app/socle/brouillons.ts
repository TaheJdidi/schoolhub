import { DestroyRef, inject, Injectable, Signal, signal } from '@angular/core';
import { AbstractControl, FormArray, FormGroup } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

/** Où les brouillons sont rangés dans l'onglet. */
const STOCKAGE = 'schoolhub.brouillons';

/** Un brouillon, et l'adresse de la page où il a été commencé (« /professeurs/groupes/nouveau »). */
interface Garde {
  brouillon: unknown;
  adresse: string | null;
}

/**
 * Les formulaires commencés puis laissés pour une autre page, ou interrompus par un rechargement : on les retrouve en
 * revenant. Ils sont rangés dans le stockage de l'onglet (`sessionStorage`) : ils survivent à un rechargement, mais pas
 * à la fermeture de l'onglet. Ils partent à la déconnexion, pour que la personne suivante, sur le même ordinateur de
 * l'accueil, ne les voie pas. Si le navigateur refuse ce stockage (navigation privée…), ils restent en mémoire.
 */
@Injectable({ providedIn: 'root' })
export class Brouillons {
  /** Les brouillons, du plus ancien au plus récent. */
  private readonly gardes = new Map<string, Garde>(Object.entries(lireLeStockage()));
  /** Change à chaque brouillon gardé ou oublié : le menu relit alors ses liens. */
  private readonly version = signal(0);
  /** Les pages ouvertes qui ont un brouillon : chacune sait le garder si l'onglet se recharge ou se ferme. */
  private readonly ouverts = new Set<() => void>();

  constructor() {
    // Un rechargement ne détruit pas les pages : chacune garde son brouillon avant que l'onglet parte.
    globalThis.addEventListener?.('pagehide', () => this.ouverts.forEach((garder) => garder()));
  }

  garder<T>(cle: string, brouillon: T, adresse: string | null = null): void {
    // Le plus récent va à la fin.
    this.gardes.delete(cle);
    this.gardes.set(cle, { brouillon: JSON.parse(JSON.stringify(brouillon)), adresse });
    this.ecrire();
  }

  /** Le brouillon gardé sous cette clé, ou `null`. */
  reprendre<T>(cle: string): T | null {
    const garde = this.gardes.get(cle);
    return garde === undefined ? null : (JSON.parse(JSON.stringify(garde.brouillon)) as T);
  }

  /**
   * La page d'une section (« professeurs ») où un formulaire attend, la plus récente : le menu y ramène, au lieu de
   * la page d'accueil de la section. `null` s'il n'y en a pas.
   */
  pageEnCours(section: string): string | null {
    this.version();
    const adresses = [...this.gardes.values()].map((g) => g.adresse).reverse();
    return adresses.find((a) => a?.startsWith(`/${section}/`)) ?? null;
  }

  oublier(cle: string): void {
    if (this.gardes.delete(cle)) this.ecrire();
  }

  /** À la déconnexion : plus aucun brouillon. */
  toutOublier(): void {
    this.gardes.clear();
    this.ecrire();
  }

  /** Une page ouverte confie comment garder son brouillon ; ce qui est rendu la retire. */
  suivre(garder: () => void): () => void {
    this.ouverts.add(garder);
    return () => this.ouverts.delete(garder);
  }

  private ecrire(): void {
    this.version.update((v) => v + 1);
    try {
      if (this.gardes.size === 0) sessionStorage.removeItem(STOCKAGE);
      else sessionStorage.setItem(STOCKAGE, JSON.stringify(Object.fromEntries(this.gardes)));
    } catch {
      // Stockage refusé ou plein : les brouillons restent en mémoire.
    }
  }
}

function lireLeStockage(): Record<string, Garde> {
  try {
    const texte = sessionStorage.getItem(STOCKAGE);
    const lu: unknown = texte ? JSON.parse(texte) : null;
    if (!lu || typeof lu !== 'object') return {};
    // Seulement les brouillons bien formés (un ancien format est ignoré).
    return Object.fromEntries(Object.entries(lu).filter(([, g]) => g && typeof g === 'object' && 'brouillon' in g)) as Record<string, Garde>;
  } catch {
    return {};
  }
}

/** L'adresse de la page en cours de construction, sans ses paramètres : « /professeurs/groupes/nouveau ». */
function adresseDeLaPage(): () => string | null {
  const route = inject(ActivatedRoute, { optional: true });
  return () => (route ? '/' + route.snapshot.pathFromRoot.flatMap((r) => r.url.map((s) => s.path)).join('/') : null);
}

/** Garde le brouillon en quittant la page, et aussi si l'onglet se recharge pendant qu'elle est ouverte. */
function garderEnPartant(brouillons: Brouillons, garder: () => void): void {
  const nePlusSuivre = brouillons.suivre(garder);
  inject(DestroyRef).onDestroy(() => {
    nePlusSuivre();
    garder();
  });
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
  const adresse = adresseDeLaPage();
  const repris = signal(false);
  let etatDuDebut: { etat: E } | null = null;

  garderEnPartant(brouillons, () => {
    if (etat.aGarder()) brouillons.garder(laCle(), etat.lire(), adresse());
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
  const adresse = adresseDeLaPage();
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

  garderEnPartant(brouillons, () => {
    if (!formulaire.dirty || options.aGarder?.() === false) {
      // Fermé ou laissé tel quel : l'ancien brouillon n'a plus lieu d'être.
      if (repris()) brouillons.oublier(laCle());
      return;
    }
    brouillons.garder<Brouillon<E>>(laCle(), { valeurs: formulaire.getRawValue(), etat: options.etat?.lire() }, adresse());
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
