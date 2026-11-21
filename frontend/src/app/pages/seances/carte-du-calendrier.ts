import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { dateDuJourLocale, FicheDuGroupe, heure, Jour, JOURS } from '../groupes/groupe';
import { ChangementDuCalendrier } from './appel';
import { MessagesDuChangement } from './messages-du-changement';

/** « 27/11 ». */
function court(date: string): string {
  return formatDate(date, 'dd/MM', FRANCAIS);
}

/** Un créneau en cours de saisie. */
interface CreneauSaisi {
  jour: Jour;
  debut: string;
  fin: string;
}

/**
 * SEA-05 et SEA-06 : les formulaires du calendrier d'un groupe, sur sa fiche, sous la bande des séances (les séances
 * annulées sont dans la bande) ; le formulaire « Ajouter une séance » (SEA-05), ouvert depuis « Cette séance » ; le nouvel horaire à partir d'une date
 * (SEA-06), ouvert depuis l'en-tête de la fiche. Après un changement, les messages WhatsApp sont prêts, et la fiche se recharge.
 */
/** Une période de la feuille d'appel : « La séance du jour », « Mois 2 (4 séances) · en cours »… */
export interface PeriodeDeLaFeuille {
  valeur: string;
  libelle: string;
  du: string;
  au: string;
}

/**
 * Les périodes proposées pour la feuille d'appel (SEA-08) : la séance du jour s'il y en a une ; pour une Étude, chaque
 * Mois, le Mois en cours d'abord ; pour une Révision ou une Formation, les 4 prochaines séances.
 */
export function periodesDeLaFeuille(fiche: FicheDuGroupe, aujourdhui = dateDuJourLocale()): PeriodeDeLaFeuille[] {
  const choix: PeriodeDeLaFeuille[] = [];
  const toutes = [...fiche.mois.flatMap((m) => m.seances), ...fiche.seances];
  if (toutes.some((s) => s.date === aujourdhui)) choix.push({ valeur: 'jour', libelle: 'La séance du jour', du: aujourdhui, au: aujourdhui });
  if (fiche.type === 'Etude') {
    const enCours = fiche.mois.filter((m) => m.commence).at(-1)?.numero;
    const mois = [...fiche.mois].sort((a, b) => (a.numero === enCours ? -1 : b.numero === enCours ? 1 : a.numero - b.numero));
    for (const m of mois.filter((m) => m.seances.length > 0)) {
      choix.push({
        valeur: `mois-${m.numero}`,
        libelle: `Mois ${m.numero} (${m.seances.length} séances)${m.numero === enCours ? ' · en cours' : ''}`,
        du: m.seances[0].date,
        au: m.seances.at(-1)!.date,
      });
    }
  } else if (fiche.seances.length > 0) {
    const aVenir = fiche.seances.filter((s) => s.date >= aujourdhui);
    const quatre = (aVenir.length > 0 ? aVenir : fiche.seances.slice(-4)).slice(0, 4);
    choix.push({ valeur: 'prochaines', libelle: `${quatre.length} séances, du ${court(quatre[0].date)}`, du: quatre[0].date, au: quatre.at(-1)!.date });
  }
  return choix;
}

@Component({
  selector: 'sh-carte-du-calendrier',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'fermerAuClavier()' },
  imports: [MessagesDuChangement],
  template: `
    <!-- SEA-05 et SEA-06 : les formulaires ouverts depuis la bande des séances (« + Ajouter ») et l'horaire (« Changer »). -->
    @if (action() !== null) {
    <div class="fond-de-fenetre" (click)="fermer()"></div>
    <section class="card carte fenetre-de-seance" role="dialog" aria-modal="true" aria-labelledby="titre-de-la-fenetre-du-calendrier">
      <div class="haut-de-fenetre">
        <h2 class="titre" id="titre-de-la-fenetre-du-calendrier">{{ titreDeLaFenetre() }}</h2>
        <button type="button" class="fermer-la-fenetre" aria-label="Fermer" (click)="fermer()">✕</button>
      </div>
      @if (modifiable()) {
        @if (action() === 'supplementaire') {
          @if (etude()) {
            <p class="aide">Elle compte dans le Mois : le Mois garde 4 séances, et les suivantes passent au Mois d'après.</p>
          } @else {
            <p class="aide">Elle est offerte : elle a son appel, mais ne compte pas dans les séances payées.</p>
          }
          <div class="champs">
            <label class="f">Date <input #date type="date" (input)="date_.set(date.value)" /></label>
            <label class="f">De <input #debut type="time" [value]="debut_()" (input)="debut_.set(debut.value)" /></label>
            <label class="f">À <input #fin type="time" [value]="fin_()" (input)="fin_.set(fin.value)" /></label>
          </div>
          <div class="actions">
            <button type="button" class="btn" [disabled]="envoi()" (click)="fermer()">Retour</button>
            <button type="button" class="btn primary" [disabled]="envoi()" (click)="ajouter()">Ajouter la séance</button>
          </div>
        } @else {
          <p class="aide">Les séances à partir de cette date se recréent sur le nouvel horaire ; celles d'avant ne changent pas.</p>
          @for (creneau of creneaux(); track $index; let rang = $index) {
            <div class="champs creneau">
              <label class="f">
                Jour
                <select class="inp" (change)="changerLeCreneau(rang, 'jour', $any($event.target).value)">
                  @for (j of jours; track j.jour) {
                    <option [value]="j.jour" [selected]="j.jour === creneau.jour">{{ j.jour }}</option>
                  }
                </select>
              </label>
              <label class="f">De <input type="time" [value]="creneau.debut" (input)="changerLeCreneau(rang, 'debut', $any($event.target).value)" /></label>
              <label class="f">À <input type="time" [value]="creneau.fin" (input)="changerLeCreneau(rang, 'fin', $any($event.target).value)" /></label>
              @if (creneaux().length > 1) {
                <button type="button" class="btn ghost" (click)="retirerLeCreneau(rang)">Retirer</button>
              }
            </div>
          }
          <div class="champs">
            <label class="f">À partir du <input #depart type="date" (input)="date_.set(depart.value)" /></label>
          </div>
          <div class="actions">
            <button type="button" class="btn ghost" (click)="ajouterUnCreneau()">Ajouter un créneau</button>
            <button type="button" class="btn" [disabled]="envoi()" (click)="fermer()">Retour</button>
            <button type="button" class="btn primary" [disabled]="envoi()" (click)="changerLHoraire()">Changer l'horaire</button>
          </div>
        }
      }
      @if (erreur(); as erreur) {
        <p class="note bad" role="alert">{{ erreur }}</p>
      }
    </section>
    }
    @if (changement(); as changement) {
      <sh-messages-du-changement [changement]="changement" />
    }
  `,
  styleUrls: ['../eleves/eleves.css', '../inscriptions/inscriptions.css'],
  styles: `
    .actions.debut { justify-content: flex-start; }
    /* Le formulaire s'ouvre dans une fenêtre, au centre, au-dessus de la page. */
    .fond-de-fenetre { position: fixed; inset: 0; z-index: 60; background: rgb(15 23 42 / 0.45); }
    .fenetre-de-seance { position: fixed; z-index: 61; top: 50%; left: 50%; transform: translate(-50%, -50%); margin: 0;
      width: min(560px, calc(100vw - 32px)); max-height: calc(100vh - 32px); overflow: auto; padding: 20px 22px; border-radius: 14px;
      box-shadow: 0 12px 40px rgb(15 23 42 / 0.25); }
    .fenetre-de-seance .haut-de-fenetre { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; margin-bottom: 12px; }
    .fenetre-de-seance .haut-de-fenetre h2 { margin: 0; }
    .fermer-la-fenetre { display: inline-flex; align-items: center; justify-content: center; width: 32px; height: 32px; padding: 0; border: 0;
      border-radius: 8px; background: none; color: var(--ink-2); font-size: 18px; cursor: pointer; }
    .fermer-la-fenetre:hover { background: var(--sunk); }
    .creneau { align-items: end; }
    .aide { margin: 0; color: var(--ink-2); }
  `,
})
export class CarteDuCalendrier {
  private readonly http = inject(HttpClient);

  readonly fiche = input.required<FicheDuGroupe>();
  /** Un groupe fermé ou terminé ne change plus. */
  readonly modifiable = input(true);
  /** Le calendrier a changé : la fiche se recharge. */
  readonly modifie = output<void>();

  protected readonly heure = heure;
  protected readonly jours = JOURS;
  /** « Ajouter une séance » ou « Changer l'horaire du groupe ». */
  protected titreDeLaFenetre(): string {
    return this.action() === 'horaire' ? "Changer l'horaire du groupe" : this.etude() ? 'Ajouter une séance' : 'Ajouter une séance supplémentaire';
  }

  /** Échap referme la fenêtre. */
  protected fermerAuClavier(): void {
    if (this.action() !== null && !this.envoi()) this.fermer();
  }

  /** SEA-05 : dans une Étude, la séance ajoutée compte dans le Mois ; ailleurs, elle est offerte. */
  protected readonly etude = computed(() => this.fiche().type === 'Etude');
  protected readonly action = signal<'supplementaire' | 'horaire' | null>(null);


  protected readonly date_ = signal('');
  protected readonly debut_ = signal('');
  protected readonly fin_ = signal('');
  protected readonly creneaux = signal<CreneauSaisi[]>([]);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);
  protected readonly changement = signal<ChangementDuCalendrier | null>(null);

  /** SEA-05 : « + Ajouter », dans la bande des séances : le formulaire s'ouvre dans une fenêtre. */
  ajouterUneSeance(): void {
    this.ouvrir('supplementaire');
  }

  /** SEA-06 : « Changer », dans la pastille de l'horaire : le formulaire s'ouvre dans une fenêtre. */
  ouvrirLeNouvelHoraire(): void {
    this.ouvrir('horaire');
  }

  /** Les heures et les créneaux proposés sont ceux du groupe. */
  protected ouvrir(action: 'supplementaire' | 'horaire'): void {
    const creneaux = this.fiche().creneaux;
    this.action.set(action);
    this.date_.set('');
    this.debut_.set(creneaux[0]?.debut ?? '');
    this.fin_.set(creneaux[0]?.fin ?? '');
    this.creneaux.set(creneaux.length > 0 ? creneaux.map((c) => ({ ...c })) : [{ jour: 'Samedi', debut: '', fin: '' }]);
    this.erreur.set(null);
  }

  protected fermer(): void {
    this.action.set(null);
    this.erreur.set(null);
  }

  protected changerLeCreneau(rang: number, champ: keyof CreneauSaisi, valeur: string): void {
    this.creneaux.update((creneaux) => creneaux.map((c, i) => (i === rang ? { ...c, [champ]: valeur } : c)));
  }

  protected ajouterUnCreneau(): void {
    this.creneaux.update((creneaux) => [...creneaux, { jour: 'Samedi', debut: '', fin: '' }]);
  }

  protected retirerLeCreneau(rang: number): void {
    this.creneaux.update((creneaux) => creneaux.filter((_, i) => i !== rang));
  }

  protected async ajouter(): Promise<void> {
    if (!this.date_() || !this.debut_() || !this.fin_()) {
      this.erreur.set('Choisissez la date et les heures');
      return;
    }
    await this.envoyer(`/api/groupes/${this.fiche().id}/seances`, { date: this.date_(), debut: this.debut_(), fin: this.fin_() });
  }

  protected async changerLHoraire(): Promise<void> {
    if (!this.date_()) {
      this.erreur.set('Choisissez la date de départ du nouvel horaire');
      return;
    }
    await this.envoyer(`/api/groupes/${this.fiche().id}/horaire`, { creneaux: this.creneaux(), aPartirDu: this.date_() });
  }

  private async envoyer(adresse: string, corps: unknown): Promise<void> {
    if (this.envoi()) return;
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      this.changement.set(await firstValueFrom(this.http.post<ChangementDuCalendrier>(adresse, corps)));
      this.action.set(null);
      this.modifie.emit();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  /** « samedi 04/12/2027 ». */
  protected jour(date: string): string {
    return formatDate(date, 'EEEE dd/MM/yyyy', FRANCAIS);
  }

  /** « 07/12 ». */
  protected jourCourt(date: string): string {
    return formatDate(date, 'dd/MM', FRANCAIS);
  }
}
