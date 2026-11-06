import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MessageWhatsApp } from '../inscriptions/inscription';
import { ChangementDuCalendrier } from './appel';

/**
 * SEA-03 à SEA-06 : le message préparé après un changement du calendrier, et pour chaque élève concerné un bouton
 * WhatsApp par destinataire (l'élève, son parent, ou les deux). Rien ne part tout seul.
 */
@Component({
  selector: 'sh-messages-du-changement',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let leChangement = changement();
    <section class="card carte">
      <h2 class="titre">{{ titre() }}</h2>
      <p class="texte-du-message">{{ leChangement.texte }}</p>
      @for (eleve of leChangement.eleves; track eleve.eleveId) {
        <div class="eleve">
          <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
          @for (message of eleve.messages; track message.lien) {
            <div class="destinataire">
              <span>{{ pourQui(message) }}</span>
              <a class="btn" [href]="message.lien" target="_blank" rel="noopener">Ouvrir WhatsApp</a>
            </div>
          } @empty {
            <p class="vide">Aucun téléphone pour le prévenir.</p>
          }
        </div>
      } @empty {
        <p class="vide">Aucun élève à prévenir.</p>
      }
    </section>
  `,
  styleUrls: ['../eleves/eleves.css', '../inscriptions/inscriptions.css'],
  styles: `
    .eleve { display: grid; gap: 6px; padding: 10px 0; border-top: 1px solid var(--line); }
  `,
})
export class MessagesDuChangement {
  readonly changement = input.required<ChangementDuCalendrier>();
  readonly titre = input('Prévenir les élèves et les parents');

  /** « Au parent · 98 401 220 », « À Adem · 22 345 678 ». */
  protected pourQui(message: MessageWhatsApp): string {
    const qui = message.destinataire === 'Parent' ? 'Au parent' : `À ${message.destinataire}`;
    return `${qui} · ${message.telephone}`;
  }
}
