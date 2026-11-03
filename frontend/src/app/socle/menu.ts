import { NomDIcone } from './icones';
import { Role } from './session/moi';

/** Une entrée du menu : le menu de gauche, la page « Plus » et les adresses de l'application en sont construits. */
export interface EntreeDuMenu {
  libelle: string;
  chemin: string;
  icone: NomDIcone;
  reservee: boolean;
}

/**
 * Le menu complet, dans l'ordre de la maquette. Une page pas encore faite affiche « Cette page arrive bientôt » :
 * la story qui la construit remplace cette page à son adresse (app.routes.ts).
 */
export const MENU: readonly EntreeDuMenu[] = [
  { libelle: "Aujourd'hui", chemin: 'aujourdhui', icone: 'calendar', reservee: false },
  { libelle: 'Professeurs', chemin: 'professeurs', icone: 'graduation-cap', reservee: false },
  { libelle: 'Élèves', chemin: 'eleves', icone: 'users', reservee: false },
  { libelle: 'Caisse', chemin: 'caisse', icone: 'wallet', reservee: false },
  { libelle: 'Bibliothèque', chemin: 'bibliotheque', icone: 'book-open', reservee: false },
  { libelle: "Demandes d'inscription", chemin: 'demandes', icone: 'inbox', reservee: false },
  { libelle: 'Achats', chemin: 'achats', icone: 'shopping-cart', reservee: false },
  // Réservé au propriétaire (CPT-06) : le serveur refuse aussi ces données à la secrétaire.
  { libelle: 'Parts des professeurs', chemin: 'parts', icone: 'percent', reservee: true },
  { libelle: 'Tableau de bord', chemin: 'tableau-de-bord', icone: 'trending-up', reservee: true },
  { libelle: 'Ressources humaines', chemin: 'ressources-humaines', icone: 'contact', reservee: true },
  { libelle: 'Journal', chemin: 'journal', icone: 'history', reservee: true },
  { libelle: 'Comptes', chemin: 'comptes', icone: 'user-cog', reservee: true },
  { libelle: 'Paramètres du centre', chemin: 'parametres', icone: 'settings', reservee: true },
];

/** Les onglets du téléphone, en bas de l'écran : « Plus » liste tout le menu. */
export const ONGLETS: readonly EntreeDuMenu[] = [
  { libelle: "Aujourd'hui", chemin: 'aujourdhui', icone: 'calendar', reservee: false },
  { libelle: 'Guichet', chemin: 'guichet', icone: 'search', reservee: false },
  { libelle: 'Caisse', chemin: 'caisse', icone: 'wallet', reservee: false },
  { libelle: 'Plus', chemin: 'plus', icone: 'menu', reservee: false },
];

/** Le menu d'une personne selon son rôle : la secrétaire ne voit pas les entrées réservées. */
export function menuDe(role: Role): EntreeDuMenu[] {
  return MENU.filter((entree) => role === 'Proprietaire' || !entree.reservee);
}
