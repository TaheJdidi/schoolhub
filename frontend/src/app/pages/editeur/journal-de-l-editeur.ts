import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { ACTIONS, formaterMoment, LigneDuJournal } from './api';

/** Le journal de l'Éditeur : ce qu'il a fait sur chaque centre (EDI-05). */
@Component({
  selector: 'sh-journal-de-l-editeur',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Journal de l'Éditeur</h1>
        <p class="sub">
          Les actions du back office, des plus récentes aux plus anciennes. Il ne se modifie ni ne
          s'efface.
        </p>
      </div>
    </div>
    @if (message(); as message) {
      <p class="note bad" role="alert">{{ message }}</p>
    }
    @if (lignes(); as lignes) {
      @if (lignes.length === 0) {
        <p class="sub">Rien pour l'instant.</p>
      } @else {
        <div class="card table">
          <table>
            <thead>
              <tr>
                <th>Quand</th>
                <th>Qui</th>
                <th>Action</th>
                <th>Centre</th>
                <th>Détail</th>
              </tr>
            </thead>
            <tbody>
              @for (ligne of lignes; track $index) {
                <tr>
                  <td class="num">{{ moment(ligne.quand) }}</td>
                  <td>{{ ligne.auteur }}</td>
                  <td>{{ action(ligne) }}</td>
                  <td>{{ ligne.centre }}</td>
                  <td class="detail">{{ ligne.detail }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    }
  `,
  styles: `
    .table {
      overflow-x: auto;
    }
    table {
      width: 100%;
      border-collapse: collapse;
    }
    th,
    td {
      text-align: left;
      padding: 8px 12px;
      border-top: 1px solid var(--line);
      vertical-align: top;
    }
    thead th {
      border-top: 0;
      color: var(--ink-3);
      font-weight: 600;
      font-size: 12px;
      text-transform: uppercase;
      letter-spacing: .05em;
    }
    th,
    td {
      padding: 12px 14px;
    }
    tbody tr:hover {
      background: var(--sunk);
    }
    td {
      white-space: nowrap;
    }
    .detail {
      white-space: normal;
      min-width: 240px;
    }
  `,
})
export class JournalDeLEditeur {
  private readonly http = inject(HttpClient);

  protected readonly lignes = signal<LigneDuJournal[] | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly moment = formaterMoment;
  protected readonly action = (ligne: LigneDuJournal) => ACTIONS[ligne.action] ?? ligne.action;

  constructor() {
    void this.charger();
  }

  private async charger(): Promise<void> {
    try {
      this.lignes.set(
        await firstValueFrom(this.http.get<LigneDuJournal[]>('/api/editeur/journal')),
      );
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    }
  }
}
