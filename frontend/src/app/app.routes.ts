import { Type } from '@angular/core';
import { Routes } from '@angular/router';
import { Cadre } from './cadre/cadre';
import { Plus } from './cadre/plus';
import { AccesReserve } from './pages/acces-reserve/acces-reserve';
import { Aujourdhui } from './pages/aujourdhui/aujourdhui';
import { Bientot } from './pages/bientot/bientot';
import { Comptes } from './pages/comptes/comptes';
import { Connexion } from './pages/connexion/connexion';
import { Invitation } from './pages/invitation/invitation';
import { Introuvable } from './pages/introuvable/introuvable';
import { MotDePasseOublie } from './pages/mot-de-passe-oublie/mot-de-passe-oublie';
import { NouveauMotDePasse } from './pages/nouveau-mot-de-passe/nouveau-mot-de-passe';
import { EntreeDuMenu, MENU } from './socle/menu';
import { estConnecte, estProprietaire, nonConnecte } from './socle/session/gardes';

const titre = (page: string) => `${page} · SchoolHub`;

/** Les pages du menu déjà construites, par adresse : les autres affichent « Cette page arrive bientôt ». */
const PAGES: Partial<Record<string, Type<unknown>>> = {
  comptes: Comptes,
};

/**
 * Les adresses d'une entrée du menu. Une entrée réservée en a deux, à la même adresse :
 * la page pour le propriétaire, puis « Accès réservé au propriétaire » pour la secrétaire.
 */
function adressesDe(entree: EntreeDuMenu): Routes {
  const page = {
    path: entree.chemin,
    component: PAGES[entree.chemin] ?? Bientot,
    data: { titre: entree.libelle },
    title: titre(entree.libelle),
  };
  if (!entree.reservee) return [page];
  return [
    { ...page, canMatch: [estProprietaire] },
    { path: entree.chemin, component: AccesReserve, title: titre('Accès réservé') },
  ];
}

export const routes: Routes = [
  // Les pages sans connexion, avant le cadre.
  { path: 'connexion', component: Connexion, canActivate: [nonConnecte], title: titre('Connexion') },
  // Les pages des liens des emails restent ouvertes à une personne connectée : elles connectent la personne du lien.
  { path: 'invitation', component: Invitation, title: titre('Choisir son mot de passe') },
  { path: 'mot-de-passe-oublie', component: MotDePasseOublie, title: titre('Mot de passe oublié') },
  { path: 'nouveau-mot-de-passe', component: NouveauMotDePasse, title: titre('Nouveau mot de passe') },
  {
    path: '',
    component: Cadre,
    canActivate: [estConnecte],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'aujourdhui' },
      { path: 'aujourdhui', component: Aujourdhui, title: titre("Aujourd'hui") },
      ...MENU.filter((entree) => entree.chemin !== 'aujourdhui').flatMap(adressesDe),
      { path: 'guichet', component: Bientot, data: { titre: 'Guichet' }, title: titre('Guichet') },
      { path: 'plus', component: Plus, title: titre('Plus') },
      { path: '**', component: Introuvable, title: titre('Page introuvable') },
    ],
  },
];
