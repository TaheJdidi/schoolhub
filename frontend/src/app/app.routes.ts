import { Routes } from '@angular/router';
import { Cadre } from './cadre/cadre';
import { AccesReserve } from './pages/acces-reserve/acces-reserve';
import { Aujourdhui } from './pages/aujourdhui/aujourdhui';
import { Bientot } from './pages/bientot/bientot';
import { Introuvable } from './pages/introuvable/introuvable';
import { EntreeDuMenu, MENU } from './socle/menu';
import { estConnecte, estProprietaire } from './socle/session/gardes';

const titre = (page: string) => `${page} · SchoolHub`;

/**
 * Les adresses d'une entrée du menu pas encore construite. Une entrée réservée en a deux, à la même adresse :
 * la page pour le propriétaire, puis « Accès réservé au propriétaire » pour la secrétaire.
 */
function adressesDe(entree: EntreeDuMenu): Routes {
  const page = { path: entree.chemin, component: Bientot, data: { titre: entree.libelle }, title: titre(entree.libelle) };
  if (!entree.reservee) return [page];
  return [
    { ...page, canMatch: [estProprietaire] },
    { path: entree.chemin, component: AccesReserve, title: titre('Accès réservé') },
  ];
}

export const routes: Routes = [
  // Les pages sans connexion viendront ici, avant le cadre.
  {
    path: '',
    component: Cadre,
    canActivate: [estConnecte],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'aujourdhui' },
      { path: 'aujourdhui', component: Aujourdhui, title: titre("Aujourd'hui") },
      ...MENU.filter((entree) => entree.chemin !== 'aujourdhui').flatMap(adressesDe),
      { path: 'guichet', component: Bientot, data: { titre: 'Guichet' }, title: titre('Guichet') },
      { path: '**', component: Introuvable, title: titre('Page introuvable') },
    ],
  },
];
