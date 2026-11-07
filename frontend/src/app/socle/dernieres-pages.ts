import { Injectable, signal } from '@angular/core';

/** Où les dernières pages sont rangées dans l'onglet. */
const STOCKAGE = 'schoolhub.dernieres-pages';

/**
 * La dernière page ouverte dans chaque section du menu (« /professeurs/groupes/nouveau » pour « professeurs »), comme
 * les onglets d'une application de téléphone : revenir à une section rouvre la page qu'on y avait laissée. Rangées dans
 * l'onglet (`sessionStorage`) : elles survivent à un rechargement, et partent à la déconnexion.
 */
@Injectable({ providedIn: 'root' })
export class DernieresPages {
  private readonly pages = signal<Readonly<Record<string, string>>>(lireLeStockage());

  /** La page vient de s'ouvrir : elle devient la dernière de sa section. */
  noter(adresse: string): void {
    const section = sectionDe(adresse);
    if (!section || this.pages()[section] === adresse) return;
    this.pages.update((pages) => ({ ...pages, [section]: adresse }));
    this.ecrire();
  }

  /** La dernière page ouverte dans cette section, ou la section elle-même. */
  derniere(section: string): string {
    return this.pages()[section] ?? `/${section}`;
  }

  /** À la déconnexion. */
  toutOublier(): void {
    this.pages.set({});
    this.ecrire();
  }

  private ecrire(): void {
    try {
      const pages = this.pages();
      if (Object.keys(pages).length === 0) sessionStorage.removeItem(STOCKAGE);
      else sessionStorage.setItem(STOCKAGE, JSON.stringify(pages));
    } catch {
      // Stockage refusé : elles restent en mémoire.
    }
  }
}

/** « professeurs » pour « /professeurs/groupes/nouveau?professeur=k ». */
export function sectionDe(adresse: string): string {
  return adresse.split(/[/?#]/)[1] ?? '';
}

function lireLeStockage(): Record<string, string> {
  try {
    const lu: unknown = JSON.parse(sessionStorage.getItem(STOCKAGE) ?? 'null');
    if (!lu || typeof lu !== 'object') return {};
    return Object.fromEntries(Object.entries(lu).filter(([, a]) => typeof a === 'string' && a.startsWith('/'))) as Record<string, string>;
  } catch {
    return {};
  }
}
