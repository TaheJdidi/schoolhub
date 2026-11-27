import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BoutonDeDeconnexion } from '../../cadre/bouton-de-deconnexion';
import { Icone } from '../../socle/icone';
import { initiales } from '../../socle/personnes';
import { Session } from '../../socle/session/session';

/**
 * EDI-00 : le back office de l'Éditeur (l'équipe SchoolHub), après son mot de passe et le code de son application.
 * Il n'appartient à aucun centre et ne voit pas leurs données. Ses pages : la liste des centres, la création et la fiche
 * d'un centre, et le journal de l'Éditeur. Le logo et la pastille « Back office » sont violets, pour ne jamais confondre
 * cet espace avec celui d'un centre.
 */
@Component({
  selector: 'sh-espace-editeur',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BoutonDeDeconnexion, RouterOutlet, RouterLink, RouterLinkActive, Icone],
  template: `
    <div class="shell">
      <header class="top">
        <a class="brand" routerLink="/editeur" aria-label="SchoolUp Éditeur, retour à la liste des centres">
          <span class="logo" aria-hidden="true">SU</span>
          <span class="bt"><b>SchoolUp</b><small class="espace">Éditeur · Back office</small></span>
        </a>
        <button type="button" class="ouvrir-le-menu" aria-controls="menu-editeur" [attr.aria-expanded]="menuOuvert()"
          (click)="menuOuvert.set(!menuOuvert())"><sh-icone nom="menu" /> Menu</button>
        <nav id="menu-editeur" class="menu" aria-label="Menu" [class.ouvert]="menuOuvert()" (click)="choisir($event)">
          <a class="lien" routerLink="/editeur" routerLinkActive="actif" [routerLinkActiveOptions]="{ exact: true }"><sh-icone nom="users" /><span>Centres</span></a>
          <a class="lien" routerLink="/editeur/centres/nouveau" routerLinkActive="actif"><sh-icone nom="plus" /><span>Créer un centre</span></a>
          <a class="lien" routerLink="/editeur/journal" routerLinkActive="actif"><sh-icone nom="history" /><span>Journal</span></a>
        </nav>
        <div class="moi">
          <span class="avatar" aria-hidden="true">{{ lettres() }}</span>
          <span class="un"><b>{{ personne()?.prenom }} {{ personne()?.nom }}</b><small>Éditeur</small></span>
        </div>
        <sh-bouton-de-deconnexion class="sortie" />
      </header>
      <main class="main">
        <router-outlet />
      </main>
    </div>
  `,
  styles: `
    .shell { min-height: 100dvh; display: grid; grid-template-columns: 248px minmax(0, 1fr); background: var(--bg); }
    .top { position: sticky; top: 0; height: 100dvh; overflow-y: auto; display: flex; flex-direction: column; gap: 22px;
      padding: 22px 14px 16px; background: var(--surface); border-right: 1px solid var(--line); }
    .brand { display: flex; align-items: center; gap: 10px; padding: 0 8px; color: inherit; text-decoration: none; min-width: 0; }
    .logo { width: 36px; height: 36px; border-radius: 10px; background: #5B21B6; color: #FFFFFF; display: grid; place-items: center;
      flex: none; font-weight: 700; font-size: 14px; }
    .bt { display: grid; line-height: 1.2; min-width: 0; }
    .bt b { font-weight: 700; font-size: 18px; letter-spacing: -.02em; }
    .bt .espace { justify-self: start; margin-top: 2px; padding: 1px 8px; border-radius: 999px; background: #EDE9FE; color: #5B21B6; font-size: 11px; font-weight: 600; }
    .menu { display: flex; flex-direction: column; gap: 2px; }
    .lien { display: flex; align-items: center; gap: 12px; min-height: 44px; padding: 0 12px; border-radius: var(--r);
      color: var(--ink-2); font-size: 14px; font-weight: 500; text-decoration: none; white-space: nowrap; }
    .lien span { flex: 1; }
    .lien:hover { background: var(--sunk); color: var(--ink); }
    .lien.actif { background: var(--accent-soft); color: var(--accent-ink); font-weight: 600; }
    .moi { margin-top: auto; display: flex; align-items: center; gap: 10px; padding: 12px; border-radius: 12px; background: var(--sunk); min-width: 0; }
    .avatar { width: 36px; height: 36px; flex: none; border-radius: 50%; background: #EDE9FE; color: #5B21B6;
      display: grid; place-items: center; font-weight: 600; font-size: 13px; }
    .un { display: grid; min-width: 0; line-height: 1.25; }
    .un b { font-size: 13px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .un small { font-size: 12px; color: var(--ink-3); }
    .sortie { flex: none; margin-top: -14px; }
    .main { padding: 28px 32px 40px; min-width: 0; }
    .ouvrir-le-menu { display: none; margin-left: auto; align-items: center; gap: 8px; min-height: 44px; padding: 0 14px;
      border-radius: 12px; border: 1px solid var(--line); background: var(--surface); color: var(--ink); font-weight: 600; cursor: pointer; }
    .ouvrir-le-menu[aria-expanded="true"] { background: var(--accent-soft); color: var(--accent-ink); border-color: transparent; }
    @media (prefers-color-scheme: dark) {
      .bt .espace, .avatar { background: #2A1F4D; color: #DDD6FE; }
    }
    @media (max-width: 899px) {
      .shell { grid-template-columns: minmax(0, 1fr); grid-template-rows: auto 1fr; }
      .top { z-index: 20; height: auto; overflow: visible; flex-direction: row; flex-wrap: wrap; align-items: center;
        gap: 10px; padding: 10px 14px; border-right: 0; border-bottom: 1px solid var(--line); }
      .brand { padding: 0; }
      .ouvrir-le-menu { display: inline-flex; }
      .menu { display: none; order: 10; flex-basis: 100%; padding-top: 4px; }
      .menu.ouvert { display: flex; }
      .moi { display: none; }
      .sortie { order: 11; flex-basis: 100%; margin-top: 0; display: none; }
      .menu.ouvert ~ .sortie { display: grid; }
      .main { padding: 18px 16px 32px; }
    }
  `,
})
export class EspaceEditeur {
  protected readonly personne = inject(Session).personne;
  protected readonly menuOuvert = signal(false);

  protected readonly lettres = computed(() => {
    const moi = this.personne();
    return moi ? initiales(moi.prenom, moi.nom) : '';
  });

  protected choisir(evenement: Event): void {
    if ((evenement.target as Element).closest('a')) this.menuOuvert.set(false);
  }
}
