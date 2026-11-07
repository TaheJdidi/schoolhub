import { formatDate } from '@angular/common';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { dateDuJourLocale, FicheDuGroupe, heure, TypeDeGroupe } from '../groupes/groupe';

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
  /** ELV-08 : le dernier jour compté, « 2028-01-25 » ; `null` tant qu'elle n'est pas arrêtée. */
  arreteLe: string | null;
  /** ELV-09 : la valeur des séances payées et pas suivies dans l'ancien groupe, déduite ici. */
  report: number;
  /** CAI-01 : « AJour », « APayer », « Orange » ou « Rouge » ; vide pour un prix spécial à 0 TND. */
  couleur?: string | null;
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
  /** « Payee » : encaissée avec un reçu (CAI-03). */
  etat: 'APayer' | 'Payee' | 'PayeeAvantSchoolHub' | 'Dispensee';
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
  /** CAI-01 : la pire couleur de l'élève. */
  couleur?: string;
  /** CAI-08 : la date de son dernier rappel de paiement. */
  dernierRappelLe?: string | null;
}

/** Le texte de la capacité atteinte, sur la page comme dans la réponse du serveur. */
export const CAPACITE_ATTEINTE = "Ce groupe a atteint sa capacité. L'inscription reste possible.";

/** Un groupe qui compte déjà autant d'élèves que sa capacité. */
/** Une séance que l'élève peut choisir comme première : sa date « 2027-11-20 » et « samedi 20/11/2027 · 10h ». */
export interface DateDeDebut {
  date: string;
  libelle: string;
}

/** Les modes à choisir : aucun pour une Étude, ni pour une Formation sans tranches. */
export function modesDuGroupe(groupe: FicheDuGroupe | null): { mode: ModeDeTarification; libelle: string }[] {
  if (groupe?.type === 'Revision') {
    return [
      { mode: 'ALaSeance', libelle: 'À la séance' },
      { mode: 'EnTotalite', libelle: 'En totalité' },
    ];
  }
  if (groupe?.type === 'Formation' && groupe.tranches.length > 0) {
    return [
      { mode: 'EnUneFois', libelle: 'En une fois' },
      { mode: 'EnTranches', libelle: `En ${groupe.tranches.length} tranches` },
    ];
  }
  return [];
}

/** Les séances du groupe, une par jour, dans l'ordre. */
export function datesDuGroupe(groupe: FicheDuGroupe | null): DateDeDebut[] {
  if (!groupe) return [];
  const seances = groupe.type === 'Etude' ? groupe.mois.flatMap((mois) => mois.seances) : groupe.seances;
  const vues = new Set<string>();
  return seances
    .filter((seance) => !vues.has(seance.date) && vues.add(seance.date))
    .map((seance) => ({ date: seance.date, libelle: `${formatDate(seance.date, 'EEEE dd/MM/yyyy', FRANCAIS)} · ${heure(seance.debut)}` }));
}

/** La première séance proposée : celle du jour, ou la suivante, ou la dernière. */
export function premiereSeanceProposee(dates: DateDeDebut[]): string | null {
  const aujourdhui = dateDuJourLocale();
  return (dates.find((d) => d.date >= aujourdhui) ?? dates.at(-1))?.date ?? null;
}

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
