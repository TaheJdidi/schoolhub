/** Les matières proposées à tout centre, même neuf ; celles du centre s'y ajoutent. */
export const MATIERES_PROPOSEES = [
  'Allemand',
  'Anglais',
  'Arabe',
  'Chimie',
  'Économie',
  'Éducation islamique',
  'Espagnol',
  'Éveil scientifique',
  'Français',
  'Gestion',
  'Histoire-Géographie',
  'Informatique',
  'Italien',
  'Mathématiques',
  'Philosophie',
  'Physique',
  'Sciences de la vie et de la terre',
  'Sciences expérimentales',
  'Sciences physiques',
  'Technologie',
];

/** L'option « Autre matière… » de la liste. */
export const AUTRE_MATIERE = 'autre';

/** « mathématiques » et « Mathématiques » sont la même matière, comme au serveur. */
export const normaliser = (nom: string) => nom.trim().normalize('NFKC').toLowerCase();

/** Les matières proposées et celles du centre, une seule fois chacune, dans l'ordre alphabétique. */
export function listeDesMatieres(duCentre: (string | null)[]): string[] {
  const parNom = new Map(MATIERES_PROPOSEES.map((nom) => [normaliser(nom), nom]));
  for (const nom of duCentre) if (nom) parNom.set(normaliser(nom), nom);
  return [...parNom.values()].sort((a, b) => a.localeCompare(b, 'fr'));
}
