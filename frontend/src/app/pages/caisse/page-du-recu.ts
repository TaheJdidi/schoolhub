import { formatDate } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { MessageWhatsApp } from '../inscriptions/inscription';
import { AUCUN_EMAIL, RecuDecrit } from './recu';

/**
 * CAI-05, CAI-06 et CAI-07 : un reçu, son aperçu, et les trois façons de le remettre : l'imprimer (A5), l'envoyer par
 * WhatsApp (le texte reprend tout le reçu), ou par email (déjà parti s'il y a un email ; « Renvoyer l'email »). Puis
 * « Annuler ce reçu », avec un motif, si la personne connectée le peut ; sinon, pourquoi.
 */
@Component({
  selector: 'sh-page-du-recu',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    @if (introuvable()) {
      <div class="pagehead">
        <div>
          <nav class="fil" aria-label="Fil d'Ariane"><a routerLink="/caisse">Caisse</a> ›</nav>
          <h1 class="h1">Reçu introuvable</h1>
          <p class="sub">Ce reçu n'existe pas.</p>
        </div>
      </div>
    } @else if (recu(); as recu) {
      <div class="pagehead">
        <div>
          <nav class="fil" aria-label="Fil d'Ariane"><a routerLink="/caisse">Caisse</a> ›</nav>
          <h1 class="h1">
            Reçu n° {{ recu.numero }}
            @if (recu.annule) {
              <span class="pill bad">Annulé</span>
            }
          </h1>
          <p class="sub">{{ recu.eleve.prenom }} {{ recu.eleve.nom }} · {{ montant(recu.total) }} TND · le {{ date(recu.jour) }}</p>
        </div>
      </div>

      <div class="cartes">
        @if (recu.annule) {
          <p class="note bad" role="status">Reçu annulé : {{ recu.motifDAnnulation }}.</p>
        }
        <section class="card carte">
          <h2 class="titre">Le reçu</h2>
          <p class="texte-du-message">{{ recu.texte }}</p>
        </section>

        <section class="card carte">
          <h2 class="titre">Remettre le reçu</h2>
          <div class="actions debut">
            <a class="btn" [routerLink]="['/recus', recu.id, 'impression']" target="_blank">Imprimer (A5)</a>
          </div>
          <h3 class="tag">WhatsApp</h3>
          @for (message of recu.messages; track message.lien) {
            <div class="destinataire">
              <span>{{ pourQui(message) }}</span>
              <a class="btn" [href]="message.lien" target="_blank" rel="noopener">Ouvrir WhatsApp</a>
            </div>
          } @empty {
            <p class="vide">Aucun téléphone connu pour cet élève.</p>
          }
          <h3 class="tag">Email</h3>
          @if (recu.emails.length > 0) {
            <p class="aide">Envoyé à {{ recu.emails.join(', ') }}.</p>
            <div class="actions debut">
              <button type="button" class="btn" [disabled]="envoi()" (click)="renvoyer()">Renvoyer l'email</button>
            </div>
          } @else {
            <p class="aide">{{ aucunEmail }}</p>
          }
          @if (succes(); as succes) {
            <p class="note ok" role="status">{{ succes }}</p>
          }
        </section>

        @if (!recu.annule) {
          <section class="card carte">
            <h2 class="titre">Annuler ce reçu</h2>
            @if (recu.annulable) {
              @if (!enAnnulation()) {
                <div class="actions debut">
                  <button type="button" class="btn" (click)="enAnnulation.set(true)">Annuler ce reçu</button>
                </div>
              } @else {
                <label class="f">
                  Motif
                  <input #motif autocomplete="off" placeholder="Mauvais élève sélectionné" (input)="motifSaisi.set(motif.value)" />
                </label>
                <p class="aide">Le reçu reste visible avec « annulé » ; ce qu'il payait redevient à payer.</p>
                <div class="actions">
                  <button type="button" class="btn" [disabled]="envoi()" (click)="enAnnulation.set(false)">Retour</button>
                  <button type="button" class="btn primary" [disabled]="envoi()" (click)="annuler()">Confirmer l'annulation</button>
                </div>
              }
            } @else {
              <p class="aide">{{ recu.pourquoiPasAnnulable }}</p>
            }
          </section>
        }
        @if (erreur(); as erreur) {
          <p class="note bad" role="alert">{{ erreur }}</p>
        }
        <div class="actions">
          <a class="btn" [routerLink]="['/eleves', recu.eleve.id]">Voir la fiche de l'élève</a>
        </div>
      </div>
    } @else if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css', '../inscriptions/inscriptions.css'],
  styles: `
    .actions.debut { justify-content: flex-start; }
    .pill.bad { background: var(--bad-soft); color: var(--bad); }
  `,
})
export class PageDuRecu implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  protected readonly montant = ecrireUnMontant;
  protected readonly aucunEmail = AUCUN_EMAIL;
  protected readonly recu = signal<RecuDecrit | null>(null);
  protected readonly introuvable = signal(false);
  protected readonly enAnnulation = signal(false);
  protected readonly motifSaisi = signal('');
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.recu.set(await firstValueFrom(this.http.get<RecuDecrit>(`/api/recus/${this.id}`)));
    } catch (erreur) {
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) this.introuvable.set(true);
      else this.erreur.set(messageDErreur(erreur));
    }
  }

  protected async renvoyer(): Promise<void> {
    await this.agir(() => firstValueFrom(this.http.post<RecuDecrit>(`/api/recus/${this.id}/email`, null)), 'Email renvoyé');
  }

  protected async annuler(): Promise<void> {
    if (!this.motifSaisi().trim()) {
      this.erreur.set("Écrivez le motif de l'annulation");
      return;
    }
    await this.agir(() => firstValueFrom(this.http.post<RecuDecrit>(`/api/recus/${this.id}/annulation`, { motif: this.motifSaisi().trim() })), null);
    this.enAnnulation.set(false);
  }

  private async agir(action: () => Promise<RecuDecrit>, succes: string | null): Promise<void> {
    if (this.envoi()) return;
    this.envoi.set(true);
    this.erreur.set(null);
    this.succes.set(null);
    try {
      this.recu.set(await action());
      this.succes.set(succes);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** « 27/11/2027 ». */
  protected date(texte: string): string {
    return formatDate(texte, 'dd/MM/yyyy', FRANCAIS);
  }

  /** « Au parent · 98 401 220 », « À Adem · 22 345 678 ». */
  protected pourQui(message: MessageWhatsApp): string {
    const qui = message.destinataire === 'Parent' ? 'Au parent' : `À ${message.destinataire}`;
    return `${qui} · ${message.telephone}`;
  }
}
