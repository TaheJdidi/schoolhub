import { formatDate } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { heure } from '../groupes/groupe';
import { MessageWhatsApp } from '../inscriptions/inscription';
import { AppelDeLaSeance, EleveALAppel } from './appel';

/**
 * SEA-07, SEA-10 et SEA-11 : l'appel d'une séance. Tous les élèves sont présents ; un toucher marque un absent, un autre
 * le remet présent. « Enregistrer l'appel » fait passer la séance à « appel fait » ; ensuite, « Corriger l'appel » reste
 * possible à tout moment. Après l'enregistrement, « Prévenir les parents des absents » liste les messages WhatsApp prêts ;
 * un message ouvert est marqué, pour ne pas l'envoyer deux fois.
 */
@Component({
  selector: 'sh-page-de-l-appel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    @if (introuvable()) {
      <div class="pagehead">
        <div>
          <nav class="fil" aria-label="Fil d'Ariane"><a routerLink="/professeurs">Professeurs</a> ›</nav>
          <h1 class="h1">Séance introuvable</h1>
          <p class="sub">Cette séance n'existe pas.</p>
        </div>
      </div>
    } @else if (appel(); as appel) {
      <div class="pagehead">
        <div>
          <nav class="fil" aria-label="Fil d'Ariane">
            <a routerLink="/professeurs">Professeurs</a> ›
            <a [routerLink]="['/professeurs/groupes', appel.groupe.id]">{{ appel.groupe.nom }}</a> ›
          </nav>
          <h1 class="h1">Appel du {{ jour(appel.date) }}</h1>
          <p class="sub">{{ appel.groupe.matiere }} · {{ appel.professeur }} · {{ heure(appel.debut) }} → {{ heure(appel.fin) }}</p>
        </div>
      </div>

      <div class="cartes">
        <section class="card carte">
          <div class="entete">
            <h2 class="titre">{{ appel.appelFait ? 'Appel fait' : 'Appel à faire' }}</h2>
            @if (appel.eleves.length > 0) {
              <span class="compte">{{ presents() }} présents · {{ absents().length }} {{ absents().length > 1 ? 'absents' : 'absent' }}</span>
            }
          </div>
          @if (!appel.ouvert) {
            <p class="note" role="status">L'appel s'ouvre le jour de la séance.</p>
          } @else {
            <p class="aide">Touchez un élève pour le marquer absent ; touchez-le de nouveau pour le remettre présent.</p>
          }
          <div class="eleves">
            @for (eleve of appel.eleves; track eleve.inscriptionId) {
              <button
                type="button"
                class="eleve"
                [class.absent]="absents().includes(eleve.inscriptionId)"
                [attr.aria-pressed]="absents().includes(eleve.inscriptionId)"
                [disabled]="!appel.ouvert || envoi()"
                (click)="basculer(eleve)"
              >
                <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                <small>{{ absents().includes(eleve.inscriptionId) ? 'Absent' : 'Présent' }}</small>
              </button>
            } @empty {
              <p class="vide">Aucun élève attendu à cette séance.</p>
            }
          </div>
          @if (appel.ouvert) {
            <div class="actions">
              <button type="button" class="btn primary" [disabled]="envoi()" (click)="enregistrer()">
                {{ appel.appelFait ? "Corriger l'appel" : "Enregistrer l'appel" }}
              </button>
            </div>
          }
          @if (succes(); as succes) {
            <p class="note ok" role="status">{{ succes }}</p>
          }
          @if (erreur(); as erreur) {
            <p class="note bad" role="alert">{{ erreur }}</p>
          }
        </section>

        @if (appel.appelFait && absentsEnregistres().length > 0) {
          <section class="card carte">
            <h2 class="titre">Prévenir les parents des absents</h2>
            @for (eleve of absentsEnregistres(); track eleve.inscriptionId) {
              <div class="absent-a-prevenir">
                <b>{{ eleve.prenom }} {{ eleve.nom }}</b>
                @if (eleve.prevenuLe) {
                  <span class="pill ok">Message ouvert</span>
                }
                @for (message of eleve.messages; track message.lien) {
                  <div class="destinataire">
                    <span>{{ pourQui(message) }}</span>
                    <a class="btn" [href]="message.lien" target="_blank" rel="noopener" (click)="prevenir(eleve)">Ouvrir WhatsApp</a>
                  </div>
                } @empty {
                  <p class="vide">Aucun téléphone pour le prévenir.</p>
                }
              </div>
            }
          </section>
        }
      </div>
    } @else if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css', '../inscriptions/inscriptions.css'],
  styles: `
    .compte { color: var(--ink-2); font-size: 13px; }
    .eleves { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: 8px; }
    .eleve { display: grid; gap: 2px; text-align: left; padding: 10px 12px; border: 1px solid var(--line); border-radius: 10px;
      background: var(--surface); color: var(--ink); font: inherit; cursor: pointer; min-height: 52px; }
    .eleve small { color: var(--ok); font-size: 12.5px; }
    .eleve.absent { border-color: var(--bad); background: var(--bad-soft); }
    .eleve.absent small { color: var(--bad); font-weight: 600; }
    .eleve:disabled { cursor: default; }
    .absent-a-prevenir { display: grid; gap: 6px; padding: 10px 0; border-top: 1px solid var(--line); }
    .absent-a-prevenir:first-of-type { border-top: 0; }
    .absent-a-prevenir .pill { justify-self: start; }
  `,
})
export class PageDeLAppel implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly seanceId = inject(ActivatedRoute).snapshot.paramMap.get('seance') ?? '';

  protected readonly heure = heure;
  protected readonly appel = signal<AppelDeLaSeance | null>(null);
  protected readonly introuvable = signal(false);
  /** Les inscriptions marquées absentes sur la page, enregistrées ou pas. */
  protected readonly absents = signal<string[]>([]);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  protected readonly presents = computed(() => (this.appel()?.eleves.length ?? 0) - this.absents().length);
  /** Les absents tels qu'enregistrés : ceux à prévenir. */
  protected readonly absentsEnregistres = computed(() => this.appel()?.eleves.filter((e) => e.absent) ?? []);

  async ngOnInit(): Promise<void> {
    try {
      this.montrer(await firstValueFrom(this.http.get<AppelDeLaSeance>(`/api/seances/${this.seanceId}/appel`)));
    } catch (erreur) {
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) this.introuvable.set(true);
      else this.erreur.set(messageDErreur(erreur));
    }
  }

  private montrer(appel: AppelDeLaSeance): void {
    this.appel.set(appel);
    this.absents.set(appel.eleves.filter((e) => e.absent).map((e) => e.inscriptionId));
  }

  protected basculer(eleve: EleveALAppel): void {
    this.succes.set(null);
    this.absents.update((absents) =>
      absents.includes(eleve.inscriptionId) ? absents.filter((id) => id !== eleve.inscriptionId) : [...absents, eleve.inscriptionId],
    );
  }

  protected async enregistrer(): Promise<void> {
    const appel = this.appel();
    if (this.envoi() || !appel) return;
    this.envoi.set(true);
    this.erreur.set(null);
    this.succes.set(null);
    try {
      const corrige = appel.appelFait;
      this.montrer(await firstValueFrom(this.http.put<AppelDeLaSeance>(`/api/seances/${appel.seanceId}/appel`, { absents: this.absents() })));
      this.succes.set(corrige ? 'Appel corrigé' : 'Appel enregistré');
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** Le message s'ouvre dans WhatsApp ; la page le marque, sans attendre. */
  protected async prevenir(eleve: EleveALAppel): Promise<void> {
    const appel = this.appel();
    if (!appel || eleve.prevenuLe) return;
    try {
      await firstValueFrom(this.http.post(`/api/seances/${appel.seanceId}/absences/${eleve.inscriptionId}/prevenu`, null));
      const maintenant = new Date().toISOString();
      this.appel.set({
        ...appel,
        eleves: appel.eleves.map((e) => (e.inscriptionId === eleve.inscriptionId ? { ...e, prevenuLe: maintenant } : e)),
      });
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** « samedi 27/11/2027 ». */
  protected jour(date: string): string {
    return formatDate(date, 'EEEE dd/MM/yyyy', FRANCAIS);
  }

  /** « Au parent · 98 401 220 », « À Adem · 22 345 678 ». */
  protected pourQui(message: MessageWhatsApp): string {
    const qui = message.destinataire === 'Parent' ? 'Au parent' : `À ${message.destinataire}`;
    return `${qui} · ${message.telephone}`;
  }
}
