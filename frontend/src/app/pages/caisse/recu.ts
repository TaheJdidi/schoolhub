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
  /** « Especes » ou « Cheque » ; absent d'un vieux reçu : espèces. */
  mode?: ModeDePaiement;
}

/** Comment l'élève paie : en espèces (dans la caisse) ou par chèque (compté à part). */
export type ModeDePaiement = 'Especes' | 'Cheque';

/** « Total payé en espèces » ou « Total payé par chèque ». */
export function totalPaye(mode: ModeDePaiement | undefined): string {
  return mode === 'Cheque' ? 'Total payé par chèque' : 'Total payé en espèces';
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

/** CAI-04 : une somme due à ce jour dans le groupe ; `inscriptionId` vaut `null` pour l'assurance. */
export interface SommeDuGroupe {
  inscriptionId: string | null;
  cle: string;
  libelle: string;
  montant: number;
}

/** CAI-04 : un élève qui doit quelque chose à ce jour dans le groupe. */
export interface EleveAEncaisser {
  eleveId: string;
  prenom: string;
  nom: string;
  /** CAI-01 : « APayer », « Orange » ou « Rouge ». */
  couleur: string;
  sommes: SommeDuGroupe[];
  total: number;
}

/** CAI-04 : GET /api/groupes/{id}/encaissement. */
export interface EncaissementDuGroupe {
  groupe: { id: string; nom: string; matiere: string; type: string };
  aPayer: EleveAEncaisser[];
  aJour: { id: string; prenom: string; nom: string }[];
}

/** CAI-04 : la réponse de POST /api/groupes/{id}/encaissement, un reçu par élève. */
export interface RecusDuGroupe {
  recus: RecuDecrit[];
}

/** CAI-05 : le texte affiché quand l'élève n'a aucun email. */
export const AUCUN_EMAIL = 'Aucun email n’est connu pour cet élève. Ajoutez-en un sur sa fiche, ou utilisez WhatsApp.';
