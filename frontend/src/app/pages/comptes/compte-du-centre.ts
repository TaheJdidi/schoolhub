import { Role } from '../../socle/session/moi';

/** Où en est un compte, tel que l'API l'écrit. */
export type EtatDuCompte = 'Invite' | 'Actif' | 'Desactive';

/** Un compte du centre, tel que l'API le décrit (GET /api/comptes, et la réponse de chaque action). */
export interface CompteDuCentre {
  id: string;
  prenom: string;
  nom: string;
  email: string;
  role: Role;
  etat: EtatDuCompte;
  /** C'est le compte de la personne connectée. */
  moi: boolean;
}

/** L'état tel qu'il s'affiche, et la couleur de sa pastille dans la maquette. */
export const ETATS: Record<EtatDuCompte, { libelle: string; pastille: 'ok' | 'info' | 'muted' }> = {
  Actif: { libelle: 'Actif', pastille: 'ok' },
  Invite: { libelle: 'Invité', pastille: 'info' },
  Desactive: { libelle: 'Désactivé', pastille: 'muted' },
};
