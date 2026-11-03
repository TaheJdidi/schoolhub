import { Role } from './session/moi';

/** Les initiales affichées dans la pastille de l'en-tête : « AJ » pour Amira Jlassi. */
export function initiales(prenom: string, nom: string): string {
  return (prenom.charAt(0) + nom.charAt(0)).toUpperCase();
}

/** Le rôle tel qu'il s'affiche, avec ses accents. */
export function libelleDuRole(role: Role): string {
  return role === 'Proprietaire' ? 'Propriétaire' : 'Secrétaire';
}
