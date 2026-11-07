import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { EnregistrementDeLEleve } from './eleve';
import { FormulaireDeLEleve } from './formulaire-de-l-eleve';

/**
 * ELV-02 : créer la fiche d'un élève ; réussie, la page ouvre la fiche, avec l'avertissement « Doublon possible ». Si un
 * groupe est choisi, elle ouvre plutôt l'inscription dans ce groupe (ELV-03), avec le même avertissement.
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

  protected ouvrirLaFiche(enregistrement: EnregistrementDeLEleve & { groupeId: string | null }): void {
    const state = { avertissement: enregistrement.avertissement };
    if (enregistrement.groupeId) {
      void this.router.navigate(['/eleves', enregistrement.eleve.id, 'inscription'], { queryParams: { groupe: enregistrement.groupeId }, state });
      return;
    }
    void this.router.navigate(['/eleves', enregistrement.eleve.id], { state });
  }

  protected revenir(): void {
    void this.router.navigate(['/eleves']);
  }
}
