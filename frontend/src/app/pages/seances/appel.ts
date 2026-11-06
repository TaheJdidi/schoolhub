import { MessageWhatsApp } from '../inscriptions/inscription';
import { TypeDeGroupe } from '../groupes/groupe';

/** Un élève à l'appel ; un absent a le message prêt pour chacun de ses destinataires (SEA-11). */
export interface EleveALAppel {
  inscriptionId: string;
  eleveId: string;
  prenom: string;
  nom: string;
  absent: boolean;
  /** Le moment où le message a été ouvert ; `null` tant qu'il ne l'est pas. */
  prevenuLe: string | null;
  messages: MessageWhatsApp[];
}

/** Le groupe d'une séance. */
export interface GroupeDeLAppel {
  id: string;
  nom: string;
  type: TypeDeGroupe;
  matiere: string;
}

/** L'appel d'une séance (GET et PUT /api/seances/{id}/appel). */
export interface AppelDeLaSeance {
  seanceId: string;
  /** « 2027-11-27 » ; les heures « 10:00 ». */
  date: string;
  debut: string;
  fin: string;
  groupe: GroupeDeLAppel;
  professeur: string;
  /** À partir du jour de la séance. */
  ouvert: boolean;
  appelFait: boolean;
  eleves: EleveALAppel[];
}

/** SEA-09 : une séance passée encore « appel à faire » (GET /api/seances/a-recopier). */
export interface AppelARecopier {
  seanceId: string;
  date: string;
  debut: string;
  fin: string;
  groupe: GroupeDeLAppel;
  professeur: string;
}
