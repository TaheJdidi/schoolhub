/** Un jour de la semaine, tel que l'API l'écrit. */
export type Jour = 'Lundi' | 'Mardi' | 'Mercredi' | 'Jeudi' | 'Vendredi' | 'Samedi' | 'Dimanche';

/** Les jours dans l'ordre de la semaine, avec leur nom court pour les filtres (« Sam »). */
export const JOURS: readonly { jour: Jour; court: string }[] = [
  { jour: 'Lundi', court: 'Lun' },
  { jour: 'Mardi', court: 'Mar' },
  { jour: 'Mercredi', court: 'Mer' },
  { jour: 'Jeudi', court: 'Jeu' },
  { jour: 'Vendredi', court: 'Ven' },
  { jour: 'Samedi', court: 'Sam' },
  { jour: 'Dimanche', court: 'Dim' },
];

/** Le type d'un groupe, tel que l'API l'écrit. */
export type TypeDeGroupe = 'Etude' | 'Revision' | 'Formation';

export const TYPES: Record<TypeDeGroupe, string> = { Etude: 'Étude', Revision: 'Révision', Formation: 'Formation' };

/** Un créneau de la semaine, avec ses heures « HH:mm ». */
export interface CreneauDuGroupe {
  jour: Jour;
  debut: string;
  fin: string;
}

/** Un groupe dans la liste des groupes du centre (GET /api/groupes), pour les cadres. */
export interface ResumeDuGroupe {
  id: string;
  nom: string;
  type: TypeDeGroupe;
  professeurId: string;
  matiere: string;
  /** Le nom du niveau. */
  niveau: string | null;
  creneaux: CreneauDuGroupe[];
  capacite: number | null;
  /** 0 jusqu'aux inscriptions (ELV). */
  eleves: number;
  /** Une Révision ou une Formation dont la dernière séance est passée : elle va dans « Anciens groupes ». */
  termine: boolean;
}

export interface SeanceDuGroupe {
  id: string;
  /** « 2027-09-18 ». */
  date: string;
  debut: string;
  fin: string;
}

/** Un Mois de 4 séances : il a commencé quand sa séance 1 a eu lieu. */
export interface MoisDuGroupe {
  numero: number;
  prix: number;
  commence: boolean;
  seances: SeanceDuGroupe[];
}

/** Une séance de Révision ou de Formation, numérotée ; une séance de Révision a son prix. */
export interface SeanceNumerotee {
  id: string;
  numero: number;
  date: string;
  debut: string;
  fin: string;
  prix: number | null;
}

/** Une tranche d'une Formation : son montant, dû à une séance. */
export interface TrancheDuGroupe {
  montant: number;
  seance: number;
}

/**
 * La fiche d'un groupe (GET /api/groupes/{id}). Étude : `mois`, et `prixDuMois`, le prix du premier Mois qui n'a pas
 * commencé. Révision et Formation : `seances` numérotées, `prixTotal`, `nombreDeSeances`, et les `tranches` d'une Formation.
 */
export interface FicheDuGroupe {
  id: string;
  nom: string;
  type: TypeDeGroupe;
  professeur: { id: string; prenom: string; nom: string };
  matiere: string;
  niveau: { id: string; nom: string } | null;
  creneaux: CreneauDuGroupe[];
  capacite: number | null;
  eleves: number;
  dateDeDebut: string;
  prixDuMois: number | null;
  mois: MoisDuGroupe[];
  termine: boolean;
  /** Le numéro de la première séance dans SchoolHub : plus que 1 pour un groupe déjà en cours. */
  premiereSeance: number;
  prixTotal: number | null;
  nombreDeSeances: number | null;
  seances: SeanceNumerotee[];
  tranches: TrancheDuGroupe[];
}

/** « 10:00 » donne « 10h », « 17:30 » donne « 17h30 », « 08:00 » donne « 8h ». */
export function heure(texte: string): string {
  const [heures, minutes] = texte.split(':');
  return `${Number(heures)}h${minutes === '00' ? '' : minutes}`;
}

/** Le jour de la semaine d'une date « 2028-05-20 » (lue comme une date locale, sans fuseau). */
export function jourDeLaDate(date: string): Jour {
  const [annee, mois, jour] = date.split('-').map(Number);
  const rang = new Date(annee, mois - 1, jour).getDay();
  return JOURS[(rang + 6) % 7].jour;
}

/** « Groupe Samedi 10h → 12h » : le nom proposé depuis le premier créneau, comme le serveur le fait. */
export function nomPropose(creneau: CreneauDuGroupe): string {
  return `Groupe ${creneau.jour} ${heure(creneau.debut)} → ${heure(creneau.fin)}`;
}

/** « 0 élève », « 2 élèves sur 16 ». */
export function eleves(groupe: { eleves: number; capacite: number | null }): string {
  const nombre = `${groupe.eleves} ${groupe.eleves > 1 ? 'élèves' : 'élève'}`;
  return groupe.capacite === null ? nombre : `${nombre} sur ${groupe.capacite}`;
}

/** Les groupes rangés par leur premier créneau : du lundi au dimanche, puis par heure. */
export function parCreneau<T extends { creneaux: CreneauDuGroupe[] }>(groupes: T[]): T[] {
  const rang = (groupe: T) => {
    const premier = groupe.creneaux[0];
    return `${JOURS.findIndex((j) => j.jour === premier.jour)} ${premier.debut}`;
  };
  return [...groupes].sort((a, b) => rang(a).localeCompare(rang(b)));
}
