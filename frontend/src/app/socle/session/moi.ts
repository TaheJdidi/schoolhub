/** Les deux rôles, tels que l'API les écrit. */
export type Role = 'Proprietaire' | 'Secretaire';

/** La personne connectée, telle que l'API la décrit (GET /api/moi, et les réponses de connexion). */
export interface Moi {
  prenom: string;
  nom: string;
  role: Role;
  centre: string;
}
