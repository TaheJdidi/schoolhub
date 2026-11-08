import { formatDate } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { MessageWhatsApp } from '../inscriptions/inscription';
import { AUCUN_EMAIL, RecuDecrit, totalPaye } from './recu';

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
          <nav class="fil" aria-label="Fil d'Ariane"><a routerLink="/caisse">Caisse</a> › Reçu n° {{ recu.numero }}</nav>
          <h1 class="h1">
            Reçu n° {{ recu.numero }}
            @if (recu.annule) {
              <span class="pill bad">Annulé</span>
            }
          </h1>
          <p class="sub">{{ recu.eleve.prenom }} {{ recu.eleve.nom }} · {{ date(recu.jour) }} · {{ montant(recu.total) }} TND</p>
        </div>
        <a class="btn" [routerLink]="['/eleves', recu.eleve.id]">Fiche de l'élève</a>
      </div>

      @if (recu.annule) {
        <p class="note bad" role="status">Reçu annulé : {{ recu.motifDAnnulation }}.</p>
      }

      <!-- Comme le prototype (écran 6) : le reçu avec sa souche à gauche ; le remettre et l'annuler à droite. -->
      <div class="colonnes-du-recu">
        <article class="ticket" [class.annule]="recu.annule" aria-label="Le reçu">
          <div class="souche">
            <span class="tag">Souche</span>
            <dl>
              <dt>N°</dt>
              <dd class="num">{{ recu.numero }}</dd>
              <dt>Date</dt>
              <dd class="num">{{ court(recu.jour) }}</dd>
              <dt>Élève</dt>
              <dd>{{ recu.eleve.prenom }} {{ recu.eleve.nom }}</dd>
              <dt>Montant</dt>
              <dd class="num">{{ montant(recu.total) }}</dd>
            </dl>
          </div>
          <div class="corps-du-ticket">
            <div class="haut-du-ticket">
              <div>
                <b class="centre">{{ recu.centre.nom }}</b>
                @if (recu.centre.telephone) {
                  <small>{{ recu.centre.telephone }}</small>
                }
              </div>
              <div class="numero">Reçu<br /><b class="num">n° {{ recu.numero }}</b></div>
            </div>
            <dl class="infos">
              <dt>Date</dt>
              <dd>{{ dateEtHeure(recu.creeLe) }}</dd>
              <dt>Élève</dt>
              <dd>{{ recu.eleve.prenom }} {{ recu.eleve.nom }}</dd>
            </dl>
            <div class="lignes-du-ticket">
              @for (ligne of recu.lignes; track $index) {
                <div class="ligne-du-ticket">
                  <span>{{ ligne.groupe }} · {{ ligne.libelle }}</span>
                  <span class="num">{{ montant(ligne.montant) }}</span>
                </div>
              }
            </div>
            <div class="total-du-ticket">
              <b>{{ totalPaye(recu.mode) }}</b>
              <b class="num">{{ montant(recu.total) }} TND</b>
            </div>
            <small class="mention">Reçu par {{ recu.secretaire }}. Ce reçu ne peut être ni modifié ni supprimé.</small>
          </div>
        </article>

        <div class="colonne">
          <section class="card bloc">
            <div class="entete-bloc"><h2 class="titre">Remettre le reçu</h2></div>
            <div class="corps-bloc">
              <div class="onglets" role="tablist" aria-label="Façon de remettre le reçu">
                @for (onglet of onglets; track onglet.valeur) {
                  <button type="button" role="tab" [attr.aria-selected]="ongletChoisi() === onglet.valeur" (click)="ongletChoisi.set(onglet.valeur)">
                    {{ onglet.libelle }}
                  </button>
                }
              </div>
              @switch (ongletChoisi()) {
                @case ('imprimer') {
                  <p class="aide">Un reçu A5, à remettre à l'élève ou au parent.</p>
                  <a class="btn primary" [routerLink]="['/recus', recu.id, 'impression']" target="_blank">Imprimer (A5)</a>
                }
                @case ('whatsapp') {
                  <div class="conversation">
                    <p class="texte-du-message bulle">{{ recu.texte }}</p>
                  </div>
                  @for (message of recu.messages; track message.lien) {
                    <div class="destinataire">
                      <span>{{ pourQui(message) }}</span>
                      <a class="btn primary" [href]="message.lien" target="_blank" rel="noopener">Ouvrir WhatsApp</a>
                    </div>
                  } @empty {
                    <p class="vide">Aucun téléphone connu pour cet élève.</p>
                  }
                  <p class="aide">Le message reprend tout le contenu du reçu, en texte. Il s'ouvre dans WhatsApp, prêt à envoyer.</p>
                }
                @case ('email') {
                  @if (recu.emails.length > 0) {
                    <p class="aide">Envoyé à {{ recu.emails.join(', ') }}.</p>
                    <div class="actions debut">
                      <button type="button" class="btn" [disabled]="envoi()" (click)="renvoyer()">Renvoyer l'email</button>
                    </div>
                  } @else {
                    <p class="aide">{{ aucunEmail }}</p>
                  }
                }
              }
              @if (succes(); as succes) {
                <p class="note ok" role="status">{{ succes }}</p>
              }
            </div>
          </section>

          @if (!recu.annule) {
            <section class="card bloc">
              <div class="entete-bloc"><h2 class="titre">Annuler ce reçu</h2></div>
              <div class="corps-bloc">
                @if (recu.annulable) {
                  <p class="aide">Le reçu reste visible avec la mention « annulé ». Ce qu'il payait redevient à payer.</p>
                  <label class="f">
                    Motif de l'annulation
                    <input #motif autocomplete="off" placeholder="Mauvais élève sélectionné" (input)="motifSaisi.set(motif.value)" />
                  </label>
                  <button type="button" class="btn danger large" [disabled]="envoi()" (click)="annuler()">Annuler le reçu n° {{ recu.numero }}</button>
                } @else {
                  <p class="aide">{{ recu.pourquoiPasAnnulable }}</p>
                }
              </div>
            </section>
          }
          @if (erreur(); as erreur) {
            <p class="note bad" role="alert">{{ erreur }}</p>
          }
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
    .colonnes-du-recu { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 16px; align-items: start; }
    @media (max-width: 1000px) { .colonnes-du-recu { grid-template-columns: minmax(0, 1fr); } }
    .colonne { display: grid; gap: 16px; min-width: 0; }
    /* Le reçu comme un ticket : la souche à gauche, séparée par un pointillé. */
    .ticket { display: grid; grid-template-columns: 120px minmax(0, 1fr); background: var(--surface); border: 1px solid var(--line);
      border-radius: 6px; box-shadow: 0 12px 32px rgba(16, 36, 56, 0.12); overflow: hidden; }
    .ticket.annule { opacity: 0.7; }
    .souche { display: grid; align-content: start; gap: 10px; padding: 14px; background: var(--sunk); border-right: 2px dashed var(--line-2); }
    .souche dl { display: grid; gap: 2px; margin: 0; }
    .souche dt { margin-top: 6px; color: var(--ink-3); font-size: 10.5px; letter-spacing: 0.06em; text-transform: uppercase; }
    .souche dd { margin: 0; font-size: 13px; overflow-wrap: anywhere; }
    .corps-du-ticket { display: grid; gap: 12px; padding: 16px 18px; min-width: 0; }
    .haut-du-ticket { display: flex; justify-content: space-between; gap: 12px; padding-bottom: 10px; border-bottom: 1px solid var(--line); }
    .haut-du-ticket small { display: block; color: var(--ink-3); font-size: 11.5px; }
    .centre { font-family: var(--font-display); font-size: 17px; }
    .numero { text-align: right; font-size: 13px; }
    .infos { display: grid; grid-template-columns: auto minmax(0, 1fr); gap: 4px 12px; margin: 0; font-size: 13px; }
    .infos dt { color: var(--ink-2); }
    .infos dd { margin: 0; }
    .lignes-du-ticket { display: grid; gap: 6px; padding: 10px 0; border-top: 1px solid var(--line); border-bottom: 1px solid var(--line); font-size: 13px; }
    .ligne-du-ticket { display: flex; justify-content: space-between; gap: 12px; }
    .total-du-ticket { display: flex; justify-content: space-between; gap: 12px; }
    .total-du-ticket .num { font-size: 16px; }
    .mention { color: var(--ink-3); font-size: 11.5px; }
    .bloc { display: grid; padding: 0; overflow: hidden; }
    .entete-bloc { padding: 14px 18px; border-bottom: 1px solid var(--line); }
    .corps-bloc { display: grid; gap: 12px; padding: 14px 18px; }
    .onglets { display: inline-flex; padding: 3px; border: 1px solid var(--line-2); border-radius: 10px; background: var(--sunk); justify-self: start; }
    .onglets button { padding: 6px 14px; border: 0; border-radius: 7px; background: none; color: var(--ink-2); font: inherit; font-size: 13px; cursor: pointer; }
    .onglets button[aria-selected='true'] { background: var(--surface); color: var(--ink); font-weight: 600; box-shadow: 0 1px 2px rgba(0, 0, 0, 0.08); }
    /* L'aperçu du message WhatsApp, comme dans l'application. */
    .conversation { display: grid; padding: 14px; border-radius: 10px; background: var(--wa-bg, #efeae2); }
    .bulle { justify-self: end; max-width: 85%; margin: 0; padding: 10px 12px; border-radius: 10px; background: var(--wa, #d9fdd3); color: var(--wa-ink, #111b21); white-space: pre-wrap; font-size: 13px; }
    .btn.danger { border-color: var(--bad); color: var(--bad); }
    .btn.large { width: 100%; }
  `,
})
export class PageDuRecu implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  protected readonly montant = ecrireUnMontant;
  protected readonly totalPaye = totalPaye;
  protected readonly aucunEmail = AUCUN_EMAIL;
  protected readonly recu = signal<RecuDecrit | null>(null);
  protected readonly introuvable = signal(false);
  /** « Remettre le reçu » : WhatsApp d'abord, comme le prototype. */
  protected readonly onglets = [
    { valeur: 'imprimer', libelle: 'Imprimer' },
    { valeur: 'whatsapp', libelle: 'WhatsApp' },
    { valeur: 'email', libelle: 'Email' },
  ] as const;
  protected readonly ongletChoisi = signal<'imprimer' | 'whatsapp' | 'email'>('whatsapp');
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

  /** « 27/11 », sur la souche. */
  protected court(texte: string): string {
    return formatDate(texte, 'dd/MM', FRANCAIS);
  }

  /** « 07/11/2026 à 10h05 ». */
  protected dateEtHeure(texte: string): string {
    return formatDate(texte, "dd/MM/yyyy 'à' H'h'mm", FRANCAIS);
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
