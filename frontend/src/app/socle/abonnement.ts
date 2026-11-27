import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** L'état de l'abonnement d'un centre, tel que l'API l'écrit (EDI-05). */
export type EtatDeLAbonnement = 'EnAttente' | 'Actif' | 'EnRetard' | 'Suspendu';

export const ETATS_DE_L_ABONNEMENT: Record<EtatDeLAbonnement, { libelle: string; classe: string }> = {
  EnAttente: { libelle: 'En attente', classe: 'info' },
  Actif: { libelle: 'Actif', classe: 'ok' },
  EnRetard: { libelle: 'En retard', classe: 'bad' },
  Suspendu: { libelle: 'Suspendu', classe: 'muted' },
};

/** L'abonnement vu par le centre (GET /api/abonnement). */
export interface SituationDuCentre {
  etat: EtatDeLAbonnement;
  payeJusquAu: string | null;
  finDeLaPeriodeDeGrace: string | null;
  /** Suspendu : rien ne se crée ni ne se modifie (EDI-05). */
  lectureSeule: boolean;
}

/**
 * L'abonnement du centre de la personne connectée, lu par le cadre : de quoi afficher ses bandeaux.
 * Le serveur refuse de toute façon les écritures d'un centre Suspendu (403).
 */
@Injectable({ providedIn: 'root' })
export class Abonnement {
  private readonly http = inject(HttpClient);

  readonly situation = signal<SituationDuCentre | null>(null);
  readonly lectureSeule = computed(() => this.situation()?.lectureSeule ?? false);

  async charger(): Promise<void> {
    try {
      this.situation.set(await firstValueFrom(this.http.get<SituationDuCentre>('/api/abonnement')));
    } catch {
      // Sans réponse, pas de bandeau : le serveur garde la main sur ce qui est permis.
    }
  }
}
