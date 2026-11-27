/** Les rôles, tels que l'API les écrit. L'Éditeur est l'équipe SchoolHub, dans le back office (EDI-00). */
export type Role = 'Proprietaire' | 'Secretaire' | 'Editeur';

/** La personne connectée, telle que l'API la décrit (GET /api/moi, et les réponses de connexion). */
export interface Moi {
  prenom: string;
  nom: string;
  role: Role;
  /** Le nom de son centre ; `null` pour l'Éditeur, qui n'en a pas. */
  centre: string | null;
}

/** La réponse 202 de la connexion : l'Éditeur doit encore saisir le code de son application (EDI-00). */
export interface EtapeSuivante {
  etape: 'activation-2fa' | 'code-2fa';
  message: string;
}

/** La première page d'une personne qui vient de se connecter : le back office pour l'Éditeur, sinon « Aujourd'hui ». */
export function premierePage(moi: Moi): string {
  return moi.role === 'Editeur' ? '/editeur' : '/aujourdhui';
}
