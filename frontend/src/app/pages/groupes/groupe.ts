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

/** Le type d'un groupe ; Révision et Formation viendront (GRP-05, GRP-06). */
export type TypeDeGroupe = 'Etude';

export const TYPES: Record<TypeDeGroupe, string> = { Etude: 'Étude' };

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

/** La fiche d'un groupe (GET /api/groupes/{id}). `prixDuMois` est le prix du premier Mois qui n'a pas commencé. */
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
  prixDuMois: number;
  mois: MoisDuGroupe[];
}

/** « 10:00 » donne « 10h », « 17:30 » donne « 17h30 », « 08:00 » donne « 8h ». */
export function heure(texte: string): string {
  const [heures, minutes] = texte.split(':');
  return `${Number(heures)}h${minutes === '00' ? '' : minutes}`;
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
