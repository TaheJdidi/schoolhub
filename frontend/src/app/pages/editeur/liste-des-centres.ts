import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ETATS_DE_L_ABONNEMENT } from '../../socle/abonnement';
import { messageDErreur } from '../../socle/erreurs';
import { formaterDate, formaterMontant, LigneDeCentre } from './api';

/** La liste des centres du back office, avec l'état de leur abonnement (EDI-01, EDI-05) et les chiffres de l'activité. */
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
      </section>
    }

    @if (message(); as message) {
      <p class="note bad bandeau" role="alert">{{ message }}</p>
    }

    @if (centres(); as centres) {
      @if (centres.length === 0) {
        <p class="sub vide">Aucun centre pour l'instant : créez le premier.</p>
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
  `,
})
export class ListeDesCentres {
  private readonly http = inject(HttpClient);

  protected readonly centres = signal<LigneDeCentre[] | null>(null);
  protected readonly message = signal<string | null>(null);

  /** Le revenu mensuel compte les centres Actifs et En retard, qui fonctionnent encore. */
  protected readonly chiffres = computed(() => {
    const centres = this.centres();
    if (!centres || centres.length === 0) return null;
    const payants = centres.filter((c) => c.etat === 'Actif' || c.etat === 'EnRetard');
    return {
      total: centres.length,
      actifs: centres.filter((c) => c.etat === 'Actif').length,
      enRetard: centres.filter((c) => c.etat === 'EnRetard').length,
      enAttente: centres.filter((c) => c.etat === 'EnAttente').length,
      suspendus: centres.filter((c) => c.etat === 'Suspendu').length,
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

  private async charger(): Promise<void> {
    try {
      this.centres.set(await firstValueFrom(this.http.get<LigneDeCentre[]>('/api/editeur/centres')));
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    }
  }
}
