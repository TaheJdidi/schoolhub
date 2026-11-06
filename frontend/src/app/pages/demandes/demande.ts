import { CreneauDuGroupe, heure, TypeDeGroupe } from '../groupes/groupe';
import { EnregistrementDeLInscription, MessageWhatsApp, ModeDeTarification } from '../inscriptions/inscription';
import { EleveTrouve } from '../eleves/eleve';

/** Où en est une demande, tel que l'API l'écrit. */
export type EtatDeLaDemande = 'ATraiter' | 'Inscrite' | 'Refusee';

/** DEM-05 : le motif d'un refus. */
export type MotifDuRefus = 'Doublon' | 'FausseDemande';

export const MOTIFS: Record<MotifDuRefus, string> = { Doublon: 'Doublon', FausseDemande: 'Fausse demande' };

/** Une matière demandée, avec le professeur et l'horaire du groupe préféré. */
export interface MatiereDemandee {
  matiere: string;
  professeur: string;
  creneaux: CreneauDuGroupe[];
}

/** Une demande dans la liste (GET /api/demandes, ou ?traitees=true). */
export interface DemandeDeLaListe {
  id: string;
  prenom: string;
  nom: string;
  niveau: { id: string; nom: string };
  matieres: MatiereDemandee[];
  amis: string | null;
  /** « 2027-11-27T09:00:00+00:00 ». */
  recueLe: string;
  doublonPossible: boolean;
  etat: EtatDeLaDemande;
  motif: MotifDuRefus | null;
  traiteeLe: string | null;
}

/** Un groupe où inscrire l'élève pour une matière (DEM-04). */
export interface GroupeProposeALaDemande {
  id: string;
  nom: string;
  type: TypeDeGroupe;
  professeur: string;
  creneaux: CreneauDuGroupe[];
  eleves: number;
  capacite: number | null;
  /** Les modes à choisir ; vide quand le groupe n'offre pas de choix. */
  modes: ModeDeTarification[];
  nombreDeTranches: number;
  /** La prochaine séance du groupe : la première de l'élève. « 2027-11-27 » et « 10:00:00 ». */
  premiereSeance: { date: string; debut: string };
  /** Les amis cités déjà inscrits dans ce groupe. */
  amis: string[];
}

/** Une matière demandée : le groupe préféré, et les groupes où l'inscrire. */
export interface MatiereDeLaDemande {
  matiere: string;
  groupePrefere: string;
  groupes: GroupeProposeALaDemande[];
}

/** Une demande ouverte (GET /api/demandes/{id}). */
export interface DetailDeLaDemande {
  id: string;
  prenom: string;
  nom: string;
  niveau: { id: string; nom: string };
  majeur: boolean;
  telephoneParent: string | null;
  emailParent: string | null;
  telephoneEleve: string | null;
  emailEleve: string | null;
  etablissement: string | null;
  amis: string | null;
  recueLe: string;
  etat: EtatDeLaDemande;
  motif: MotifDuRefus | null;
  traiteeLe: string | null;
  eleveId: string | null;
  fichesSemblables: EleveTrouve[];
  autreDemandeSemblable: boolean;
  matieres: MatiereDeLaDemande[];
}

/** La réponse d'un enregistrement (POST /api/demandes/{id}/inscription). */
export interface DemandeEnregistree {
  eleve: { id: string; prenom: string; nom: string };
  inscriptions: EnregistrementDeLInscription[];
  avertissement: string | null;
  texte: string;
  messages: MessageWhatsApp[];
}

/** « Mathématiques (samedi 10h) », « Informatique (lundi 17h, jeudi 17h) ». */
export function matiereEtHoraire(matiere: MatiereDemandee): string {
  const horaire = matiere.creneaux.map((c) => `${c.jour.toLowerCase()} ${heure(c.debut)}`).join(', ');
  return horaire ? `${matiere.matiere} (${horaire})` : matiere.matiere;
}

/** « Karim Ben Salah · Samedi 10h → 12h ». */
export function professeurEtHoraire(groupe: { professeur: string; creneaux: CreneauDuGroupe[] }): string {
  const horaire = groupe.creneaux.map((c) => `${c.jour} ${heure(c.debut)} → ${heure(c.fin)}`).join(', ');
  return horaire ? `${groupe.professeur} · ${horaire}` : groupe.professeur;
}

/** Le libellé d'un mode à choisir : « À la séance », « En 3 tranches »… */
export function libelleDuChoix(mode: ModeDeTarification, nombreDeTranches: number): string {
  switch (mode) {
    case 'ParMois':
      return 'Par Mois';
    case 'ALaSeance':
      return 'À la séance';
    case 'EnTotalite':
      return 'En totalité';
    case 'EnUneFois':
      return 'En une fois';
    case 'EnTranches':
      return `En ${nombreDeTranches} tranches`;
  }
}
