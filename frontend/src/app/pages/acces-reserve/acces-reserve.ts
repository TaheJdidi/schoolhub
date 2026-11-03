import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Ce que lit une secrétaire à l'adresse d'une page réservée au propriétaire (CPT-06).
 * La vraie page n'est jamais créée pour elle : elle ne demande donc rien au serveur, qui refuserait de toute façon.
 */
@Component({
  selector: 'sh-acces-reserve',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pagehead">
      <div><h1 class="h1">Accès réservé au propriétaire</h1></div>
    </div>
  `,
})
export class AccesReserve {}
