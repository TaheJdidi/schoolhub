import { Niveau } from '../parametres/carte-des-niveaux';

/** La fiche d'un élève (GET /api/eleves/{id}) : les téléphones groupés, les champs vides à `null`. */
export interface FicheDeLEleve {
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
}

/** La réponse d'une création ou d'une modification : la fiche, et « Doublon possible : … » s'il y a lieu. */
export interface EnregistrementDeLEleve {
  eleve: FicheDeLEleve;
  avertissement: string | null;
}

/** Un élève dans la liste ou la recherche (GET /api/eleves). */
export interface EleveTrouve {
  id: string;
  prenom: string;
  nom: string;
  niveau: string;
  /** CAI-01 : sa couleur, la pire de ses inscriptions. */
  couleur?: string | null;
}

/** Le rang du premier niveau d'office où l'élève a son propre téléphone et son email. */
const RANG_DE_LA_7E_ANNEE = 7;

/**
 * À partir de la 7ᵉ année de base, et pour un niveau ajouté (université, adultes), l'élève a ses propres contacts.
 * Le rang d'un niveau d'office est sa place parmi les niveaux d'office : le serveur les envoie dans l'ordre scolaire.
 */
export function aSesPropresContacts(niveaux: Niveau[], niveauId: string): boolean {
  const niveau = niveaux.find((n) => n.id === niveauId);
  if (!niveau) return false;
  if (!niveau.dOffice) return true;
  return niveaux.filter((n) => n.dOffice).indexOf(niveau) + 1 >= RANG_DE_LA_7E_ANNEE;
}

/** « Aucun élève », « 1 élève », « 3 élèves ». */
export function nombreDEleves(nombre: number): string {
  if (nombre === 0) return 'Aucun élève';
  return nombre === 1 ? '1 élève' : `${nombre} élèves`;
}

/** « d'Adem Miled », « de Sami Chaabane » : l'élision devant une voyelle ou un h, pas devant un y (« de Yassine »). */
export function de(nom: string): string {
  return /^[aeiouhàâéèêëîïôûü]/i.test(nom) ? `d'${nom}` : `de ${nom}`;
}
