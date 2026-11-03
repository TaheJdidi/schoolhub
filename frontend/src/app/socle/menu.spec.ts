import { menuDe, ONGLETS } from './menu';

describe('le menu', () => {
  it('montre à la secrétaire les pages de tous les jours', () => {
    expect(menuDe('Secretaire').map((entree) => entree.libelle)).toEqual([
      "Aujourd'hui",
      'Professeurs',
      'Élèves',
      'Caisse',
      'Bibliothèque',
      "Demandes d'inscription",
      'Achats',
    ]);
  });

  it('ajoute au propriétaire les pages qui lui sont réservées', () => {
    const menu = menuDe('Proprietaire');

    expect(menu).toHaveLength(13);
    expect(menu.slice(7).map((entree) => entree.libelle)).toEqual([
      'Parts des professeurs',
      'Tableau de bord',
      'Ressources humaines',
      'Journal',
      'Comptes',
      'Paramètres du centre',
    ]);
    expect(menu.slice(7).every((entree) => entree.reservee)).toBe(true);
  });

  it('donne à chaque entrée son adresse', () => {
    expect(menuDe('Proprietaire').map((entree) => entree.chemin)).toEqual([
      'aujourdhui',
      'professeurs',
      'eleves',
      'caisse',
      'bibliotheque',
      'demandes',
      'achats',
      'parts',
      'tableau-de-bord',
      'ressources-humaines',
      'journal',
      'comptes',
      'parametres',
    ]);
  });

  it('a les quatre onglets du téléphone', () => {
    expect(ONGLETS.map((onglet) => [onglet.libelle, onglet.chemin])).toEqual([
      ["Aujourd'hui", 'aujourdhui'],
      ['Guichet', 'guichet'],
      ['Caisse', 'caisse'],
      ['Plus', 'plus'],
    ]);
  });
});
