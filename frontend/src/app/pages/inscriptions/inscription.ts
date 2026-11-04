import { ecrireUnMontant } from '../../socle/montants';
import { TypeDeGroupe } from '../groupes/groupe';

/** La façon dont l'élève paie son inscription, telle que l'API l'écrit. */
export type ModeDeTarification = 'ParMois' | 'ALaSeance' | 'EnTotalite' | 'EnUneFois' | 'EnTranches';

/** Une inscription (GET /api/inscriptions?eleve= ou ?groupe=). */
export interface InscriptionDeLEleve {
  id: string;
  eleve: { id: string; prenom: string; nom: string };
  groupe: { id: string; nom: string; type: TypeDeGroupe; matiere: string };
  professeur: string;
  mode: ModeDeTarification;
  /** Le prix du Mois à venir pour une Étude, le prix total sinon. */
  prixNormal: number;
  nombreDeTranches: number;
  /** « 2027-11-20 » et « 10:00:00 » ; `null` si le groupe n'a plus de séance à sa date ou après. */
  premiereSeance: { date: string; debut: string } | null;
  /** ELV-05 : le prix qui remplace le prix normal pour ce qui n'est pas payé ; 0 : gratuit. */
  prixSpecial: number | null;
  raisonDuPrixSpecial: string | null;
  /** Les sommes déjà réglées : « Tranche 1 », 100, « AvantSchoolHub ». */
  reglees: { libelle: string; montant: number; origine: string }[];
  /** ELV-07 : les sommes d'avant SchoolHub qu'on peut noter payées. */
  payablesAvantSchoolHub: { cle: string; libelle: string; montant: number }[];
  /** ELV-10 : une Formation en tranches qui a encore une tranche à venir. */
  peutRegrouperLeReste: boolean;
}

/** Une somme due à la première séance de l'élève. */
export interface SommeALArrivee {
  libelle: string;
  montant: number;
}

/** Un message de confirmation prêt à ouvrir : « Parent » ou le prénom de l'élève. */
export interface MessageWhatsApp {
  destinataire: string;
  telephone: string;
  lien: string;
}

/** La réponse d'une inscription (POST /api/inscriptions). */
export interface EnregistrementDeLInscription {
  inscription: InscriptionDeLEleve;
  aPayerALArrivee: SommeALArrivee[];
  avertissement: string | null;
  texte: string;
  messages: MessageWhatsApp[];
}

/** Un groupe proposé à l'élève (GET /api/inscriptions/groupes-possibles). */
export interface GroupePossible {
  id: string;
  nom: string;
  type: TypeDeGroupe;
  matiere: string;
  professeur: string;
  niveau: string | null;
  duNiveauDeLEleve: boolean;
  eleves: number;
  capacite: number | null;
  dejaInscrit: boolean;
}

/** L'assurance de l'année (GET /api/eleves/{id}/assurance) ; 204 quand elle est sans objet. */
export interface AssuranceDeLEleve {
  annee: string;
  montant: number;
  dueLe: string | null;
  etat: 'APayer' | 'PayeeAvantSchoolHub' | 'Dispensee';
  raisonDeLaDispense: string | null;
}

/** Une ligne de « À payer » (GET /api/eleves/{id}/a-payer) : `inscriptionId` vaut `null` pour l'assurance. */
export interface LigneAPayer {
  inscriptionId: string | null;
  groupe: string;
  libelle: string;
  cle: string;
  montant: number;
  dueLe: string;
}

/** Ce que l'élève doit à ce jour, et le total. */
export interface APayer {
  lignes: LigneAPayer[];
  total: number;
}

/** Le texte de la capacité atteinte, sur la page comme dans la réponse du serveur. */
export const CAPACITE_ATTEINTE = "Ce groupe a atteint sa capacité. L'inscription reste possible.";

/** Un groupe qui compte déjà autant d'élèves que sa capacité. */
export function estComplet(groupe: { eleves: number; capacite: number | null }): boolean {
  return groupe.capacite !== null && groupe.eleves >= groupe.capacite;
}

/** « Étude, 40,000 TND par Mois », « Révision en totalité, 100,000 TND », « Formation en 3 tranches, 300,000 TND ». */
export function libelleDuMode(inscription: Pick<InscriptionDeLEleve, 'mode' | 'prixNormal' | 'nombreDeTranches'>): string {
  const prix = `${ecrireUnMontant(inscription.prixNormal)} TND`;
  switch (inscription.mode) {
    case 'ParMois':
      return `Étude, ${prix} par Mois`;
    case 'EnTotalite':
      return `Révision en totalité, ${prix}`;
    case 'ALaSeance':
      return `Révision à la séance, ${prix}`;
    case 'EnUneFois':
      return `Formation en une fois, ${prix}`;
    case 'EnTranches':
      return `Formation en ${inscription.nombreDeTranches} tranches, ${prix}`;
  }
}

/** « Par Mois », « En totalité », « En 3 tranches » : le mode seul, pour la liste des élèves d'un groupe. */
export function modeCourt(inscription: Pick<InscriptionDeLEleve, 'mode' | 'nombreDeTranches'>): string {
  switch (inscription.mode) {
    case 'ParMois':
      return 'Par Mois';
    case 'ALaSeance':
      return 'À la séance';
    case 'EnTotalite':
      return 'En totalité';
    case 'EnUneFois':
      return 'En une fois';
    case 'EnTranches':
      return `En ${inscription.nombreDeTranches} tranches`;
  }
}
