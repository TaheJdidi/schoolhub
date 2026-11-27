import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ETATS_DE_L_ABONNEMENT } from '../../socle/abonnement';
import { messageDErreur } from '../../socle/erreurs';
import { formaterDate, formaterMontant, LienWhatsApp, LigneDeCentre, ouvrirWhatsApp } from './api';

type Filtre = 'tous' | 'a-relancer' | 'a-supprimer';

const FILTRES: { valeur: Filtre; libelle: string }[] = [
  { valeur: 'tous', libelle: 'Tous' },
  { valeur: 'a-relancer', libelle: 'À relancer' },
  { valeur: 'a-supprimer', libelle: 'À supprimer' },
];

/**
 * La liste des centres du back office, avec l'état de leur abonnement (EDI-01, EDI-05) et les chiffres de l'activité.
 * « À relancer » : la fin est dans 7 jours ou moins, ou le centre est En retard, avec le bouton WhatsApp de la relance (EDI-06).
 * « À supprimer » : Suspendus depuis 12 mois (EDI-08).
 */
@Component({
  selector: 'sh-liste-des-centres',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Centres</h1>
        <p class="sub">Les centres abonnés à SchoolUp et l'état de leur abonnement.</p>
      </div>
      <a class="btn primary" routerLink="/editeur/centres/nouveau">Créer un centre</a>
    </div>

    @if (chiffres(); as c) {
      <section class="chiffres" aria-label="Résumé des centres">
        <div class="chiffre accent"><span>Revenu mensuel</span><b class="num">{{ montant(c.revenu) }}</b>
          <small>{{ c.payants }} centre{{ c.payants > 1 ? 's' : '' }} payant{{ c.payants > 1 ? 's' : '' }}</small></div>
        <div class="chiffre"><span>Actifs</span><b>{{ c.actifs }}</b><small>sur {{ c.total }} centre{{ c.total > 1 ? 's' : '' }}</small></div>
        <div class="chiffre" [class.alerte]="c.enRetard > 0"><span>En retard</span><b>{{ c.enRetard }}</b><small>en période de grâce</small></div>
        <div class="chiffre"><span>En attente</span><b>{{ c.enAttente }}</b><small>sans premier paiement</small></div>
        <div class="chiffre"><span>Suspendus</span><b>{{ c.suspendus }}</b><small>en lecture seule</small></div>
        <div class="chiffre"><span>À relancer</span><b>{{ c.aRelancer }}</b><small>fin dans 7 jours ou moins</small></div>
      </section>
    }

    <div class="filtres" role="tablist" aria-label="Filtrer les centres">
      @for (f of filtres; track f.valeur) {
        <button type="button" role="tab" class="btn filtre" [class.primary]="filtre() === f.valeur"
          [attr.aria-selected]="filtre() === f.valeur" (click)="filtrer(f.valeur)">{{ f.libelle }}</button>
      }
    </div>

    @if (supprime(); as supprime) {
      <p class="note ok bandeau" role="status">Le {{ supprime }} et toutes ses données sont supprimés.</p>
    }
    @if (message(); as message) {
      <p class="note bad bandeau" role="alert">{{ message }}</p>
    }

    @if (centres(); as centres) {
      @if (centres.length === 0) {
        <p class="sub vide">{{ vide() }}</p>
      } @else {
        <div class="card table">
          <table>
            <thead>
              <tr>
                <th>Centre</th>
                <th>État</th>
                <th>Payé jusqu'au</th>
                <th class="nb">Prix mensuel</th>
                <th class="nb">Comptes</th>
                <th>Propriétaire</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (centre of centres; track centre.id) {
                <tr [attr.data-centre]="centre.id">
                  <td>
                    <a class="lnk" [routerLink]="['/editeur/centres', centre.id]">{{ centre.nom }}</a>
                    <small class="code">/inscription/{{ centre.code }}</small>
                  </td>
                  <td><span class="pill {{ etat(centre).classe }}">{{ etat(centre).libelle }}</span></td>
                  <td class="num">
                    {{ date(centre.payeJusquAu) }}
                    @if (centre.reactiveJusquAu) {
                      <small class="reactive">Réactivé jusqu'au {{ date(centre.reactiveJusquAu) }}</small>
                    }
                  </td>
                  <td class="num nb">{{ montant(centre.prixMensuel) }}</td>
                  <td class="num nb">{{ centre.nombreDeComptes }}</td>
                  <td>{{ centre.proprietaire }}</td>
                  <td class="actions">
                    @if (centre.aRelancer) {
                      <button type="button" class="btn" [disabled]="envoi() || !centre.telephoneConnu" (click)="relancer(centre)">
                        Relancer par WhatsApp
                      </button>
                    }
                    @if (centre.aSupprimer) {
                      <a class="btn" [routerLink]="['/editeur/centres', centre.id]">Supprimer…</a>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    } @else if (!message()) {
      <p class="sub">Chargement des centres…</p>
    }
  `,
  styles: `
    .chiffres { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 16px; margin-bottom: 22px; }
    @media (max-width: 480px) { .chiffres { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; } .chiffre { padding: 14px; }
      .chiffre b { font-size: 21px; } .chiffre.accent { grid-column: 1 / -1; } }
    .chiffre { display: grid; gap: 4px; align-content: start; padding: 18px 20px; border-radius: 12px;
      background: var(--surface); border: 1px solid var(--line); }
    .chiffre span { font-size: 13px; color: var(--ink-3); }
    .chiffre b { font-size: 26px; font-weight: 700; letter-spacing: -.02em; }
    .chiffre small { font-size: 12px; color: var(--ink-3); }
    .chiffre.accent { background: var(--accent-soft); border-color: transparent; }
    .chiffre.accent span, .chiffre.accent b, .chiffre.accent small { color: var(--accent-ink); }
    .chiffre.alerte { background: var(--bad-soft); border-color: transparent; }
    .chiffre.alerte span, .chiffre.alerte b, .chiffre.alerte small { color: var(--bad); }
    .bandeau { margin: 0 0 14px; }
    .vide { margin-top: 8px; }
    .table { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: left; padding: 12px 14px; border-top: 1px solid var(--line); white-space: nowrap; }
    thead th { border-top: 0; font-size: 12px; text-transform: uppercase; letter-spacing: .05em; color: var(--ink-3); font-weight: 600; }
    tbody tr:hover { background: var(--sunk); }
    .code { display: block; color: var(--ink-3); font-size: 12px; }
    .reactive { display: block; color: var(--warn); }
    .nb { text-align: right; }
    .actions { text-align: right; }
    .filtres { display: inline-flex; gap: 4px; padding: 4px; border-radius: 12px; background: var(--sunk); margin-bottom: 16px; }
    .filtres .btn { min-height: 36px; padding: 0 14px; border: 0; border-radius: 9px; background: transparent; color: var(--ink-2); font-size: 13px; }
    .filtres .btn.primary { background: var(--surface); color: var(--ink); filter: none; }
  `,
})
export class ListeDesCentres {
  private readonly http = inject(HttpClient);

  /** Le nom du centre qui vient d'être supprimé (?supprime=…), depuis sa fiche (EDI-08). */
  readonly supprime = input<string>();

  protected readonly filtres = FILTRES;
  protected readonly filtre = signal<Filtre>('tous');
  protected readonly centres = signal<LigneDeCentre[] | null>(null);
  /** Tous les centres, pour les chiffres du haut, gardés pendant les filtres. */
  private readonly tous = signal<LigneDeCentre[] | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly envoi = signal(false);

  /** Le revenu mensuel compte les centres Actifs et En retard, qui fonctionnent encore. */
  protected readonly chiffres = computed(() => {
    const centres = this.tous();
    if (!centres || centres.length === 0) return null;
    const payants = centres.filter((c) => c.etat === 'Actif' || c.etat === 'EnRetard');
    return {
      total: centres.length,
      actifs: centres.filter((c) => c.etat === 'Actif').length,
      enRetard: centres.filter((c) => c.etat === 'EnRetard').length,
      enAttente: centres.filter((c) => c.etat === 'EnAttente').length,
      suspendus: centres.filter((c) => c.etat === 'Suspendu').length,
      aRelancer: centres.filter((c) => c.aRelancer).length,
      payants: payants.length,
      revenu: payants.reduce((somme, c) => somme + c.prixMensuel, 0),
    };
  });

  protected readonly etat = (centre: LigneDeCentre) => ETATS_DE_L_ABONNEMENT[centre.etat];
  protected readonly date = formaterDate;
  protected readonly montant = formaterMontant;

  constructor() {
    void this.charger();
  }

  protected vide(): string {
    switch (this.filtre()) {
      case 'a-relancer':
        return 'Aucun centre à relancer.';
      case 'a-supprimer':
        return 'Aucun centre à supprimer.';
      default:
        return "Aucun centre pour l'instant : créez le premier.";
    }
  }

  protected filtrer(filtre: Filtre): void {
    if (filtre === this.filtre()) return;
    this.filtre.set(filtre);
    this.centres.set(null);
    void this.charger();
  }

  private async charger(): Promise<void> {
    this.message.set(null);
    const filtre = this.filtre();
    const adresse = filtre === 'tous' ? '/api/editeur/centres' : `/api/editeur/centres?filtre=${filtre}`;
    try {
      const centres = await firstValueFrom(this.http.get<LigneDeCentre[]>(adresse));
      this.centres.set(centres);
      if (filtre === 'tous') this.tous.set(centres);
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    }
  }

  protected async relancer(centre: LigneDeCentre): Promise<void> {
    if (this.envoi()) return;
    this.envoi.set(true);
    this.message.set(null);
    try {
      await ouvrirWhatsApp(() => firstValueFrom(this.http.post<LienWhatsApp>(`/api/editeur/centres/${centre.id}/relance/whatsapp`, null)));
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
