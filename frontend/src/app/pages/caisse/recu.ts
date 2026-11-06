import { MessageWhatsApp } from '../inscriptions/inscription';

/** Une somme que l'élève peut payer (GET /api/eleves/{id}/encaissable). */
export interface SommeAEncaisser {
  /** `null` pour l'assurance. */
  inscriptionId: string | null;
  cle: string;
  groupe: string;
  libelle: string;
  montant: number;
  /** « 2027-11-13 » ; `null` pour une assurance pas encore due. */
  dueLe: string | null;
  /** Due aujourd'hui ou avant : cochée d'office. */
  due: boolean;
}

/** CAI-03 : ce que l'élève peut payer. */
export interface Encaissable {
  eleve: { id: string; prenom: string; nom: string };
  sommes: SommeAEncaisser[];
  aUnEmail: boolean;
}

/** Un reçu (GET /api/recus/{id}). */
export interface RecuDecrit {
  id: string;
  numero: number;
  /** « 2027-11-27 ». */
  jour: string;
  creeLe: string;
  centre: { nom: string; telephone: string | null };
  eleve: { id: string; prenom: string; nom: string };
  lignes: { groupe: string; libelle: string; montant: number }[];
  total: number;
  secretaire: string;
  annule: boolean;
  motifDAnnulation: string | null;
  annuleLe: string | null;
  /** Tout le contenu du reçu en texte, pour WhatsApp et l'email. */
  texte: string;
  messages: MessageWhatsApp[];
  emails: string[];
  /** CAI-07 : la personne connectée peut l'annuler ; sinon, pourquoi. */
  annulable: boolean;
  pourquoiPasAnnulable: string | null;
}

/** Un reçu dans la liste (GET /api/recus). */
export interface RecuDeLaListe {
  id: string;
  numero: number;
  jour: string;
  creeLe: string;
  eleve: string;
  total: number;
  secretaire: string;
  annule: boolean;
}

/** CAI-05 : le texte affiché quand l'élève n'a aucun email. */
export const AUCUN_EMAIL = 'Aucun email n’est connu pour cet élève. Ajoutez-en un sur sa fiche, ou utilisez WhatsApp.';
