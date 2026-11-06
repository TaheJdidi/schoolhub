import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * DEM-03 : le nombre de demandes d'inscription à traiter, pour le menu et l'accueil. Le cadre le relit à chaque
 * changement de page : une demande enregistrée ou refusée, ou une nouvelle arrivée, se voit à la page suivante.
 * Sans minuteur : une session laissée ouverte ne se renouvelle pas toute seule.
 */
@Injectable({ providedIn: 'root' })
export class NombreDeDemandes {
  private readonly http = inject(HttpClient);
  private derniere = 0;

  /** `null` tant qu'il n'est pas connu, ou si la lecture a échoué. */
  readonly aTraiter = signal<number | null>(null);

  async actualiser(): Promise<void> {
    const numero = ++this.derniere;
    try {
      const reponse = await firstValueFrom(this.http.get<{ aTraiter: number }>('/api/demandes/nombre'));
      if (numero === this.derniere) this.aTraiter.set(reponse.aTraiter);
    } catch {
      // Le menu reste lisible sans le nombre : rien à montrer à la personne.
      if (numero === this.derniere) this.aTraiter.set(null);
    }
  }
}
