import { formatDate } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant, lireUnNombre } from '../../socle/montants';
import { dateDuJourLocale } from '../groupes/groupe';

/** Un employé (GET /api/employes). */
export interface Employe {
  id: string;
  prenom: string;
  nom: string;
  email: string | null;
  telephone: string | null;
  poste: string;
  horaires: string | null;
  salaire: number;
  partiLe: string | null;
  joursDeConge: number;
}

export interface Conge {
  id: string;
  du: string;
  au: string;
  note: string | null;
  jours: number;
}

export interface SalairesDuMois {
  mois: string;
  salaires: { employeId: string; nom: string; poste: string; montant: number; paye: boolean; payeLe: string | null }[];
  totalPaye: number;
  totalAPayer: number;
}

/**
 * RHA-01 à RHA-03 : les ressources humaines, pour le propriétaire. Les employés (poste, salaire, congés de l'année), leur
 * fiche et leurs congés ; « Partis » ; les salaires d'un mois, « Payé » ou « À payer ».
 */
@Component({
  selector: 'sh-ressources-humaines',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Ressources humaines</h1>
        <p class="sub">Les employés du centre, leurs congés et leurs salaires. Les professeurs n'en font pas partie.</p>
      </div>
      @if (!formulaireOuvert()) {
        <button type="button" class="btn primary" (click)="nouveau()">Ajouter un employé</button>
      }
    </div>

    @if (formulaireOuvert()) {
      <form class="card carte" [formGroup]="formulaire" (ngSubmit)="enregistrer()" novalidate>
        <h2 class="titre">{{ enModification() ? 'Modifier la fiche' : 'Nouvel employé' }}</h2>
        <div class="champs">
          <label class="f">Prénom <input formControlName="prenom" autocomplete="off" /></label>
          <label class="f">Nom <input formControlName="nom" autocomplete="off" /></label>
          <label class="f">Poste <input formControlName="poste" autocomplete="off" placeholder="Agent de ménage" /></label>
          <label class="f">Salaire mensuel (TND) <input formControlName="salaire" inputmode="decimal" autocomplete="off" /></label>
          <label class="f">Téléphone (facultatif) <input formControlName="telephone" inputmode="tel" autocomplete="off" /></label>
          <label class="f">Email (facultatif) <input formControlName="email" type="email" autocomplete="off" /></label>
        </div>
        <label class="f">Horaires de la semaine (facultatif) <input formControlName="horaires" autocomplete="off" placeholder="Lundi à samedi, 8h → 16h" /></label>
        <div class="actions">
          <button type="button" class="btn" [disabled]="envoi()" (click)="formulaireOuvert.set(false)">Annuler</button>
          <button type="submit" class="btn primary" [disabled]="envoi()">Enregistrer</button>
        </div>
      </form>
    }

    <section class="card carte">
      <div class="entete">
        <h2 class="titre">{{ partis() ? 'Employés partis' : 'Employés' }}</h2>
        <button type="button" class="btn sm" (click)="partis.set(!partis())">{{ partis() ? 'Au centre' : 'Partis' }}</button>
      </div>
      @for (employe of visibles(); track employe.id) {
        <div class="employe">
          <button type="button" class="nom" (click)="choisir(employe)">
            <b>{{ employe.prenom }} {{ employe.nom }}</b>
            <small>{{ employe.poste }} · {{ montant(employe.salaire) }} TND par mois · {{ employe.joursDeConge }} jour{{ employe.joursDeConge > 1 ? 's' : '' }} de congé cette année</small>
          </button>
        </div>
        @if (choisi()?.id === employe.id) {
          <div class="fiche">
            <p>
              {{ employe.telephone ?? 'Pas de téléphone' }}{{ employe.email ? ' · ' + employe.email : '' }}
              @if (employe.horaires) {
                <br />{{ employe.horaires }}
              }
              @if (employe.partiLe) {
                <br />Parti le {{ date(employe.partiLe) }}
              }
            </p>
            <div class="actions debut">
              <button type="button" class="btn sm" (click)="modifier(employe)">Modifier</button>
              <button type="button" class="btn sm" [disabled]="envoi()" (click)="changerLaPresence(employe)">{{ employe.partiLe ? 'Revenir au centre' : 'Marquer parti' }}</button>
            </div>
            <h3 class="sous-titre">Congés</h3>
            @for (conge of conges(); track conge.id) {
              <div class="conge">
                <span>Du {{ date(conge.du) }} au {{ date(conge.au) }} · {{ conge.jours }} jour{{ conge.jours > 1 ? 's' : '' }}{{ conge.note ? ' · ' + conge.note : '' }}</span>
                <button type="button" class="btn sm" [disabled]="envoi()" (click)="retirerLeConge(conge)">Retirer</button>
              </div>
            } @empty {
              <p class="aide">Aucun congé noté.</p>
            }
            <div class="nouveau-conge">
              <label class="f">Du <input #du type="date" /></label>
              <label class="f">Au <input #au type="date" /></label>
              <label class="f">Note (facultative) <input #note autocomplete="off" /></label>
              <button type="button" class="btn" [disabled]="envoi()" (click)="ajouterUnConge(employe, du.value, au.value, note.value)">Ajouter un congé</button>
            </div>
          </div>
        }
      } @empty {
        <p class="vide">{{ partis() ? 'Aucun employé parti.' : 'Aucun employé pour le moment.' }}</p>
      }
    </section>

    <section class="card carte">
      <div class="entete">
        <h2 class="titre">Salaires du mois</h2>
        <label class="f mois">Mois <input #mois type="month" [value]="moisChoisi()" (change)="changerDeMois(mois.value)" /></label>
      </div>
      @if (salaires(); as salaires) {
        @for (salaire of salaires.salaires; track salaire.employeId) {
          <div class="salaire">
            <span class="deux"><b>{{ salaire.nom }}</b><small>{{ salaire.poste }}</small></span>
            <b class="num">{{ montant(salaire.montant) }} TND</b>
            <label class="coche">
              <input type="checkbox" [checked]="salaire.paye" [disabled]="envoi()" (change)="marquer(salaire.employeId, !salaire.paye)" />
              {{ salaire.paye ? 'Payé le ' + date(salaire.payeLe!) : 'À payer' }}
            </label>
          </div>
        } @empty {
          <p class="vide">Aucun employé ce mois-là.</p>
        }
        <p class="total">Payé {{ montant(salaires.totalPaye) }} TND · à payer <b>{{ montant(salaires.totalAPayer) }} TND</b></p>
      }
    </section>

    @if (succes(); as succes) {
      <p class="note ok" role="status">{{ succes }}</p>
    }
    @if (erreur(); as erreur) {
      <p class="note bad" role="alert">{{ erreur }}</p>
    }
  `,
  styleUrls: ['../eleves/eleves.css'],
  styles: `
    :host { display: grid; gap: 16px; }
    .pagehead { margin-bottom: 0; }
    .champs { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 10px; }
    .actions { display: flex; gap: 8px; justify-content: flex-end; flex-wrap: wrap; }
    .actions.debut { justify-content: flex-start; }
    .entete { display: flex; justify-content: space-between; align-items: end; gap: 10px; flex-wrap: wrap; }
    .employe { border-top: 1px solid var(--line); }
    .nom { display: grid; gap: 2px; width: 100%; padding: 10px 4px; background: none; border: 0; text-align: left; cursor: pointer; color: var(--ink); }
    .nom small, .salaire small { color: var(--ink-2); font-size: 12.5px; }
    .fiche { display: grid; gap: 8px; padding: 4px 4px 14px; }
    .fiche p { margin: 0; color: var(--ink-2); font-size: 13.5px; }
    .sous-titre { margin: 6px 0 0; font-size: 13px; color: var(--ink-2); text-transform: uppercase; letter-spacing: 0.04em; }
    .conge { display: flex; justify-content: space-between; gap: 8px; align-items: center; font-size: 13.5px; }
    .nouveau-conge { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 8px; align-items: end; }
    .salaire { display: flex; align-items: center; gap: 12px; padding: 9px 0; border-top: 1px solid var(--line); flex-wrap: wrap; }
    .deux { display: grid; flex: 1; min-width: 0; }
    .coche { display: inline-flex; align-items: center; gap: 6px; cursor: pointer; }
    .total { margin: 0; text-align: right; }
    .mois { min-width: 160px; }
  `,
})
export class RessourcesHumaines implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly montant = ecrireUnMontant;
  protected readonly employes = signal<Employe[]>([]);
  protected readonly choisi = signal<Employe | null>(null);
  protected readonly conges = signal<Conge[]>([]);
  protected readonly salaires = signal<SalairesDuMois | null>(null);
  protected readonly moisChoisi = signal(dateDuJourLocale().slice(0, 7));
  protected readonly partis = signal(false);
  protected readonly formulaireOuvert = signal(false);
  protected readonly enModification = signal<string | null>(null);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  protected readonly visibles = computed(() => this.employes().filter((e) => !!e.partiLe === this.partis()));

  protected readonly formulaire = new FormGroup({
    prenom: new FormControl('', { nonNullable: true }),
    nom: new FormControl('', { nonNullable: true }),
    poste: new FormControl('', { nonNullable: true }),
    salaire: new FormControl('', { nonNullable: true }),
    telephone: new FormControl('', { nonNullable: true }),
    email: new FormControl('', { nonNullable: true }),
    horaires: new FormControl('', { nonNullable: true }),
  });

  async ngOnInit(): Promise<void> {
    await Promise.all([this.chargerLesEmployes(), this.chargerLesSalaires()]);
  }

  protected nouveau(): void {
    this.formulaire.reset();
    this.enModification.set(null);
    this.formulaireOuvert.set(true);
  }

  protected modifier(employe: Employe): void {
    this.formulaire.reset({
      prenom: employe.prenom,
      nom: employe.nom,
      poste: employe.poste,
      salaire: ecrireUnMontant(employe.salaire),
      telephone: employe.telephone ?? '',
      email: employe.email ?? '',
      horaires: employe.horaires ?? '',
    });
    this.enModification.set(employe.id);
    this.formulaireOuvert.set(true);
  }

  protected async enregistrer(): Promise<void> {
    const valeurs = this.formulaire.getRawValue();
    const salaire = lireUnNombre(valeurs.salaire);
    const saisie = { ...valeurs, salaire: typeof salaire === 'number' ? salaire : null };
    const id = this.enModification();
    await this.envoyer(async () => {
      const employe = await firstValueFrom(id ? this.http.put<Employe>(`/api/employes/${id}`, saisie) : this.http.post<Employe>('/api/employes', saisie));
      this.formulaireOuvert.set(false);
      this.succes.set(`La fiche de ${employe.prenom} ${employe.nom} est enregistrée.`);
      await Promise.all([this.chargerLesEmployes(), this.chargerLesSalaires()]);
    });
  }

  protected async choisir(employe: Employe): Promise<void> {
    if (this.choisi()?.id === employe.id) {
      this.choisi.set(null);
      return;
    }
    this.choisi.set(employe);
    this.conges.set([]);
    await this.envoyer(async () => this.conges.set(await firstValueFrom(this.http.get<Conge[]>(`/api/employes/${employe.id}/conges`))));
  }

  protected async changerLaPresence(employe: Employe): Promise<void> {
    await this.envoyer(async () => {
      await firstValueFrom(this.http.post<Employe>(`/api/employes/${employe.id}/${employe.partiLe ? 'retour' : 'depart'}`, null));
      this.choisi.set(null);
      await Promise.all([this.chargerLesEmployes(), this.chargerLesSalaires()]);
    });
  }

  protected async ajouterUnConge(employe: Employe, du: string, au: string, note: string): Promise<void> {
    await this.envoyer(async () => {
      await firstValueFrom(this.http.post<Conge>(`/api/employes/${employe.id}/conges`, { du: du || null, au: au || null, note }));
      this.conges.set(await firstValueFrom(this.http.get<Conge[]>(`/api/employes/${employe.id}/conges`)));
      await this.chargerLesEmployes();
    });
  }

  protected async retirerLeConge(conge: Conge): Promise<void> {
    const employe = this.choisi();
    await this.envoyer(async () => {
      await firstValueFrom(this.http.delete(`/api/conges/${conge.id}`));
      this.conges.update((conges) => conges.filter((c) => c.id !== conge.id));
      if (employe) await this.chargerLesEmployes();
    });
  }

  protected async changerDeMois(mois: string): Promise<void> {
    if (!mois) return;
    this.moisChoisi.set(mois);
    await this.chargerLesSalaires();
  }

  protected async marquer(employeId: string, paye: boolean): Promise<void> {
    await this.envoyer(async () =>
      this.salaires.set(await firstValueFrom(this.http.post<SalairesDuMois>('/api/salaires', { employeId, mois: this.moisChoisi(), paye }))),
    );
  }

  /** « 27/11/2027 ». */
  protected date(texte: string): string {
    return formatDate(texte, 'dd/MM/yyyy', FRANCAIS);
  }

  private async envoyer(action: () => Promise<void>): Promise<void> {
    this.envoi.set(true);
    this.erreur.set(null);
    this.succes.set(null);
    try {
      await action();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  private async chargerLesEmployes(): Promise<void> {
    try {
      const employes = await firstValueFrom(this.http.get<Employe[]>('/api/employes'));
      this.employes.set(employes);
      const choisi = this.choisi();
      if (choisi) this.choisi.set(employes.find((e) => e.id === choisi.id) ?? null);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  private async chargerLesSalaires(): Promise<void> {
    try {
      this.salaires.set(await firstValueFrom(this.http.get<SalairesDuMois>('/api/salaires', { params: { mois: this.moisChoisi() } })));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }
}
