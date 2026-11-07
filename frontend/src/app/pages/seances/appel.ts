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
  /** SEA-12 : la date de la séance d'un autre groupe où l'élève rattrape celle-ci. */
  rattrapeeLe?: string | null;
}

/** SEA-12 : un élève en rattrapage à l'appel, à part : il paie dans son groupe d'origine. */
export interface EleveEnRattrapage {
  rattrapageId: string;
  inscriptionId: string;
  eleveId: string;
  prenom: string;
  nom: string;
  groupeDOrigine: string;
  /** La séance de son groupe qu'il rattrape, « 2027-11-30 ». */
  seanceRattrapee: string;
  absent: boolean;
  prevenuLe: string | null;
  messages: MessageWhatsApp[];
}

/** SEA-12 : un élève qui peut venir en rattrapage (GET /api/seances/{id}/rattrapages/possibles). */
export interface EleveRattrapable {
  inscriptionId: string;
  eleveId: string;
  prenom: string;
  nom: string;
  groupe: { id: string; nom: string };
  seances: { id: string; date: string; debut: string; fin: string }[];
}

/** SEA-08 : la feuille d'appel d'un groupe (GET /api/groupes/{id}/feuille-d-appel?du=&au=). */
export interface FeuilleDAppel {
  centre: string;
  annee: string | null;
  groupe: string;
  matiere: string;
  niveau: string | null;
  professeur: string;
  seances: { date: string; debut: string }[];
  eleves: { nom: string; prenom: string }[];
}

/** Le groupe d'une séance. */
export interface GroupeDeLAppel {
  id: string;
  nom: string;
  type: TypeDeGroupe;
  matiere: string;
  /** Le nom du niveau du groupe, pour l'accueil ; absent ou `null` sans niveau. */
  niveau?: string | null;
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
  /** SEA-03 : la séance est annulée, avec son motif. */
  annulee?: boolean;
  motifDAnnulation?: string | null;
  /** « Prevue », « Remplacement » ou « Supplementaire ». */
  nature?: NatureDeLaSeance;
  /** SEA-12 : les élèves en rattrapage à cette séance. */
  rattrapages?: EleveEnRattrapage[];
  /** SEA-07 : quand l'appel a été fait la première fois, et par qui (« Amira Jlassi »). */
  appelFaitLe?: string | null;
  appelFaitPar?: string | null;
}

/** D'où vient une séance. */
export type NatureDeLaSeance = 'Prevue' | 'Remplacement' | 'Supplementaire';

/** Un élève concerné par un changement du calendrier, et un message par destinataire. */
export interface MessagesDUnEleve {
  eleveId: string;
  prenom: string;
  nom: string;
  messages: MessageWhatsApp[];
}

/** SEA-03 à SEA-06 : le texte du message, et les messages prêts pour les élèves concernés. */
export interface ChangementDuCalendrier {
  texte: string;
  eleves: MessagesDUnEleve[];
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
