import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { EleveEnregistre, FormulaireDeLEleve } from './formulaire-de-l-eleve';

/**
 * ELV-02 : créer la fiche d'un élève ; réussie, la page ouvre la fiche, avec l'avertissement « Doublon possible ». Si un
 * groupe était choisi, l'élève y est inscrit en même temps (ELV-03) : la page montre la confirmation de l'inscription
 * (à payer, messages WhatsApp) ; refusée, elle rouvre l'inscription dans ce groupe avec le refus.
 */
@Component({
  selector: 'sh-nouvel-eleve',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormulaireDeLEleve, RouterLink],
  template: `
    <div class="pagehead">
      <div>
        <nav class="fil" aria-label="Fil d'Ariane"><a routerLink="/eleves">Élèves</a> ›</nav>
        <h1 class="h1">Nouvel élève</h1>
        <p class="sub">Ni carte d'identité, ni adresse, ni date de naissance : seulement les contacts utiles.</p>
      </div>
    </div>
    <sh-formulaire-de-l-eleve [proposerUnGroupe]="true" (enregistre)="ouvrirLaFiche($event)" (annule)="revenir()" />
  `,
  styleUrl: './eleves.css',
})
export class NouvelEleve {
  private readonly router = inject(Router);

  protected ouvrirLaFiche(enregistrement: EleveEnregistre): void {
    const { eleve, avertissement, groupeId, inscription, refusDeLInscription } = enregistrement;
    if (inscription) {
      void this.router.navigate(['/eleves', eleve.id, 'inscription'], { state: { avertissement, confirmation: inscription } });
    } else if (groupeId) {
      void this.router.navigate(['/eleves', eleve.id, 'inscription'], {
        queryParams: { groupe: groupeId },
        state: { avertissement, erreur: refusDeLInscription },
      });
    } else {
      void this.router.navigate(['/eleves', eleve.id], { state: { avertissement } });
    }
  }

  protected revenir(): void {
    void this.router.navigate(['/eleves']);
  }
}
