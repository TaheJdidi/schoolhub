import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, fromEvent, map, merge } from 'rxjs';
import { dateDuJour } from '../socle/dates';
import { Icone } from '../socle/icone';
import { EntreeDuMenu, menuDe, ONGLETS } from '../socle/menu';
import { initiales, libelleDuRole } from '../socle/personnes';
import { Session } from '../socle/session/session';
import { BoutonDeDeconnexion } from './bouton-de-deconnexion';
import { RechercheDEleves } from './recherche-d-eleves';

/** Les onglets du téléphone qui ont leur propre page : toute autre page du menu allume « Plus ». */
const ONGLETS_DIRECTS = ['aujourdhui', 'guichet', 'caisse'];

/**
 * Le cadre de l'application, autour des pages d'une personne connectée, comme dans la maquette :
 * l'en-tête, le menu à gauche sur ordinateur, et les onglets en bas sur téléphone.
 */
@Component({
  selector: 'sh-cadre',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icone, BoutonDeDeconnexion, RechercheDEleves],
  templateUrl: './cadre.html',
  styleUrl: './cadre.css',
})
export class Cadre {
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  private readonly document = inject(DOCUMENT);

  /**
   * La date du jour. Le cadre reste ouvert d'une page à l'autre, parfois toute la nuit à l'accueil :
   * elle se recalcule à chaque navigation et quand on revient sur l'onglet. Sans minuteur, et sans
   * appel au serveur, qui renouvellerait la session.
   */
  private readonly maintenant = toSignal(
    merge(
      this.router.events.pipe(filter((evenement) => evenement instanceof NavigationEnd)),
      fromEvent(this.document, 'visibilitychange').pipe(filter(() => this.document.visibilityState === 'visible')),
    ).pipe(map(() => new Date())),
    { initialValue: new Date() },
  );
  protected readonly date = computed(() => dateDuJour(this.maintenant()));

  protected readonly personne = this.session.personne;
  protected readonly initiales = computed(() => {
    const moi = this.personne();
    return moi ? initiales(moi.prenom, moi.nom) : '';
  });
  protected readonly role = computed(() => {
    const moi = this.personne();
    return moi ? libelleDuRole(moi.role) : '';
  });

  protected readonly menu = computed(() => menuDe(this.personne()?.role ?? 'Secretaire'));
  /** Le titre « Propriétaire » se place avant la première entrée réservée (-1 : aucune). */
  protected readonly premiereReservee = computed(() => this.menu().findIndex((entree) => entree.reservee));
  protected readonly onglets = ONGLETS;

  /** Le premier segment de l'adresse en cours : « caisse » pour /caisse. */
  private readonly page = toSignal(
    this.router.events.pipe(
      filter((evenement) => evenement instanceof NavigationEnd),
      map(() => this.premierSegment()),
    ),
    { initialValue: this.premierSegment() },
  );

  protected ongletActif(onglet: EntreeDuMenu): boolean {
    const page = this.page();
    return onglet.chemin === 'plus' ? !ONGLETS_DIRECTS.includes(page) : onglet.chemin === page;
  }

  private premierSegment(): string {
    return this.router.url.split(/[/?#]/)[1] ?? '';
  }
}
