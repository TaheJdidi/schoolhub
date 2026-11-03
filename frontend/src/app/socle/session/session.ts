import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Moi } from './moi';

/** La personne n'est pas encore connue : il faudra demander au serveur. */
const INCONNUE = Symbol('inconnue');

/**
 * La personne connectée, gardée en mémoire seulement (jamais dans le stockage du navigateur).
 * Le serveur est la seule source : on lui demande qui est connecté quand on ne le sait pas.
 * Rien ne l'interroge en tâche de fond : chaque appel renouvellerait la session de 12 heures.
 */
@Injectable({ providedIn: 'root' })
export class Session {
  private readonly http = inject(HttpClient);
  private readonly etat = signal<Moi | null | typeof INCONNUE>(INCONNUE);
  private enCours: Promise<Moi | null> | null = null;

  /** La personne connectée, ou `null` tant qu'elle n'est pas connue. */
  readonly personne = computed(() => {
    const etat = this.etat();
    return etat === INCONNUE ? null : etat;
  });

  /** Après une connexion ou un mot de passe choisi : le serveur a déjà dit qui est connecté. */
  connaitre(moi: Moi): void {
    this.etat.set(moi);
  }

  /** La personne redevient inconnue : le prochain `charger()` redemandera au serveur. */
  oublier(): void {
    this.etat.set(INCONNUE);
  }

  /**
   * Qui est connecté, en demandant au serveur seulement si on ne le sait pas encore.
   * Un 401, ou toute autre erreur, veut dire « personne », retenu jusqu'à la prochaine connexion
   * ou au prochain `oublier()`. Deux appels simultanés partagent la même requête.
   */
  charger(): Promise<Moi | null> {
    const etat = this.etat();
    if (etat !== INCONNUE) return Promise.resolve(etat);

    this.enCours ??= firstValueFrom(this.http.get<Moi>('/api/moi'))
      .catch(() => null)
      .then((moi) => {
        this.etat.set(moi);
        this.enCours = null;
        return moi;
      });
    return this.enCours;
  }

  /**
   * Ferme la session sur le serveur, puis oublie la personne. Un 401 veut dire que la session
   * était déjà finie : le résultat est le même. Toute autre erreur (plus d'Internet) est relancée,
   * et la personne reste connectée.
   */
  async seDeconnecter(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/deconnexion', null));
    } catch (erreur) {
      if (!(erreur instanceof HttpErrorResponse && erreur.status === 401)) throw erreur;
    }
    this.oublier();
  }
}
