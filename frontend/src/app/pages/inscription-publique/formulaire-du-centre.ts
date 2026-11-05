import { CreneauDuGroupe, TypeDeGroupe } from '../groupes/groupe';
import { Niveau } from '../parametres/carte-des-niveaux';

/** Un groupe proposé par le formulaire public : jamais d'élève, ni de nombre d'inscrits. */
export interface GroupeDuFormulaire {
  id: string;
  matiere: string;
  /** `null` : une Formation ouverte à tous les niveaux. */
  niveauId: string | null;
  type: TypeDeGroupe;
  professeur: string;
  creneaux: CreneauDuGroupe[];
}

/** Le formulaire public d'un centre (GET /api/public/inscription/{code}). */
export interface FormulaireDuCentre {
  centre: string;
  /** « 2027-2028 », ou `null` si le centre n'a pas encore d'année scolaire. */
  annee: string | null;
  niveaux: Niveau[];
  groupes: GroupeDuFormulaire[];
}
