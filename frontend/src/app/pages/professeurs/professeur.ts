/** Un professeur du centre, tel que l'API le décrit (GET /api/professeurs). Le téléphone arrive groupé. */
export interface Professeur {
  id: string;
  prenom: string;
  nom: string;
  telephone: string;
  email: string | null;
  /** Le professeur a quitté le centre (GRP-03). */
  parti: boolean;
  /** Toujours 0 en attendant les groupes (GRP-04). */
  groupesOuverts: number;
}

/** La réponse d'un ajout ou d'une modification : un téléphone déjà pris donne un avertissement, sans bloquer. */
export interface EnregistrementDuProfesseur {
  professeur: Professeur;
  avertissement: string | null;
}

/** « Aucun groupe ouvert », « 1 groupe ouvert », « 4 groupes ouverts ». */
export function groupesOuverts(nombre: number): string {
  if (nombre === 0) return 'Aucun groupe ouvert';
  return nombre === 1 ? '1 groupe ouvert' : `${nombre} groupes ouverts`;
}

/** « Aucun professeur », « 1 professeur », « 3 professeurs ». */
export function nombreDeProfesseurs(nombre: number): string {
  if (nombre === 0) return 'Aucun professeur';
  return nombre === 1 ? '1 professeur' : `${nombre} professeurs`;
}

/** Les professeurs par prénom, puis par nom, comme le serveur les envoie. */
export function trier(liste: Professeur[]): Professeur[] {
  return [...liste].sort((a, b) => a.prenom.localeCompare(b.prenom, 'fr') || a.nom.localeCompare(b.nom, 'fr'));
}

export function nomComplet(professeur: Professeur): string {
  return `${professeur.prenom} ${professeur.nom}`;
}
