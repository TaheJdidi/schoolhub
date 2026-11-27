import { EtatDeLAbonnement } from '../../socle/abonnement';
import { ecrireUnMontant, lireUnNombre } from '../../socle/montants';

// Ce que l'API du back office donne (/api/editeur/…). Montants en dinars, dates en « 2027-04-04 ».

export type ModeDePaiement = 'Virement' | 'Especes' | 'Cheque';

export const MODES: Record<ModeDePaiement, string> = {
  Virement: 'Virement',
  Especes: 'Espèces',
  Cheque: 'Chèque',
};

export interface LigneDeCentre {
  id: string;
  nom: string;
  code: string;
  etat: EtatDeLAbonnement;
  payeJusquAu: string | null;
  prixMensuel: number;
  nombreDeComptes: number;
  proprietaire: string | null;
  reactiveJusquAu: string | null;
  /** La fin est dans 7 jours ou moins, ou le centre est En retard (EDI-06). */
  aRelancer: boolean;
  /** Suspendu depuis 12 mois ou plus (EDI-08). */
  aSupprimer: boolean;
  suppressionPossibleLe: string | null;
  telephoneConnu: boolean;
}

export interface Envoi {
  objet: 'Invitation' | 'ConfirmationDePaiement' | 'RelanceAvantLaFin' | 'RelanceDeLaFin' | 'Relance';
  canal: 'Email' | 'WhatsApp';
  quand: string;
  destinataire: string;
  echeance: string | null;
}

export interface Paiement {
  id: string;
  date: string;
  montant: number;
  mode: ModeDePaiement;
  du: string;
  au: string;
  numeroDeFacture: string;
  saisiLe: string;
  saisiPar: string;
  confirmations: Envoi[];
}

export interface ProprietaireDuCentre {
  prenom: string;
  nom: string;
  email: string;
  telephone: string | null;
  etat: 'Invite' | 'Actif' | 'Desactive';
}

export interface FicheDuCentre {
  id: string;
  nom: string;
  code: string;
  etat: EtatDeLAbonnement;
  payeJusquAu: string | null;
  finDeLaPeriodeDeGrace: string | null;
  suspenduLe: string | null;
  forcage: 'Suspendu' | 'Actif' | null;
  motifDuForcage: string | null;
  forceLe: string | null;
  prixMensuel: number;
  nombreDeComptes: number;
  proprietaire: ProprietaireDuCentre | null;
  paiements: Paiement[];
  invitations: Envoi[];
  relances: Envoi[];
  /** Le dernier jour (inclus) d'une réactivation à la main sans paiement : ensuite, le centre repasse Suspendu (EDI-05). */
  reactiveJusquAu: string | null;
  aRelancer: boolean;
  aSupprimer: boolean;
  suppressionPossibleLe: string | null;
  /** La fin de l'accès support donné par le propriétaire, ou `null` (EDI-07). */
  supportJusquAu: string | null;
}

export interface LienWhatsApp {
  lien: string;
  fiche: FicheDuCentre;
}

export interface PaiementEnregistre {
  fiche: FicheDuCentre;
  confirmationEnvoyee: boolean;
  message: string;
}

export interface LigneDuJournal {
  quand: string;
  auteur: string;
  action: string;
  centre: string;
  detail: string;
}

export const ACTIONS: Record<string, string> = {
  CentreCree: 'Centre créé',
  PrixChange: 'Prix changé',
  PaiementSaisi: 'Paiement saisi',
  ProprietaireCorrige: 'Propriétaire corrigé',
  CentreSuspendu: 'Centre suspendu',
  CentreReactive: 'Centre réactivé',
  CentreSupprime: 'Centre supprimé',
};

export const CANAUX: Record<Envoi['canal'], string> = { Email: 'Email', WhatsApp: 'WhatsApp' };

export const OBJETS: Record<Envoi['objet'], string> = {
  Invitation: 'Invitation',
  ConfirmationDePaiement: 'Confirmation',
  RelanceAvantLaFin: 'Relance automatique (J-7)',
  RelanceDeLaFin: 'Relance automatique (jour de la fin)',
  Relance: 'Relance',
};

/** « 270,000 TND ». */
export function formaterMontant(montant: number): string {
  return `${ecrireUnMontant(montant)} TND`;
}

/** Un montant saisi en dinars, supérieur à 0, au millime près : « 90 », « 75,500 ». `undefined` sinon. */
export function lireUnPrix(saisie: string): number | undefined {
  const nombre = lireUnNombre(saisie);
  if (nombre === null || nombre === undefined || nombre <= 0) return undefined;
  const decimales = saisie.replace(/\s/g, '').split(/[.,]/)[1] ?? '';
  return decimales.length <= 3 ? nombre : undefined;
}

/** « 2027-04-04 » → « 04/04/2027 ». */
export function formaterDate(iso: string | null | undefined): string {
  if (!iso) return '';
  const [annee, mois, jour] = iso.slice(0, 10).split('-');
  return `${jour}/${mois}/${annee}`;
}

/** Un moment de l'API (« 2027-01-05T09:00:00+00:00 ») à Tunis : « 05/01/2027 à 10h00 ». */
export function formaterMoment(iso: string): string {
  const morceaux = new Intl.DateTimeFormat('fr-FR', {
    timeZone: 'Africa/Tunis',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date(iso));
  const partie = (type: string) => morceaux.find((p) => p.type === type)?.value ?? '';
  return `${partie('day')}/${partie('month')}/${partie('year')} à ${partie('hour')}h${partie('minute')}`;
}

/** Le jour d'après une date « 2027-04-04 » → « 2027-04-05 ». */
export function lendemain(iso: string): string {
  const [annee, mois, jour] = iso.split('-').map(Number);
  return new Date(Date.UTC(annee, mois - 1, jour + 1)).toISOString().slice(0, 10);
}

/** Aujourd'hui à Tunis, « 2027-01-05 ». */
export function aujourdhui(maintenant: Date = new Date()): string {
  const morceaux = new Intl.DateTimeFormat('fr-FR', {
    timeZone: 'Africa/Tunis',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).formatToParts(maintenant);
  const partie = (type: string) => morceaux.find((p) => p.type === type)?.value ?? '';
  return `${partie('year')}-${partie('month')}-${partie('day')}`;
}

/**
 * Ouvre WhatsApp avec le message déjà écrit (`https://wa.me/216…?text=…`). L'onglet s'ouvre tout de suite, pendant le clic
 * (un navigateur bloque un onglet ouvert plus tard), puis reçoit le lien du serveur.
 */
export async function ouvrirWhatsApp<T extends { lien: string }>(demander: () => Promise<T>): Promise<T> {
  const onglet = window.open('', '_blank');
  try {
    const reponse = await demander();
    if (onglet) onglet.location.href = reponse.lien;
    return reponse;
  } catch (erreur) {
    onglet?.close();
    throw erreur;
  }
}
