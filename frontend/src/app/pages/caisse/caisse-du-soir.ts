import { formatDate } from '@angular/common';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';

/** CAI-09 : une caisse pas encore clôturée (GET /api/caisse). */
export interface CaisseOuverte {
  /** « 2027-11-27 ». */
  jour: string;
  /** 1 pour la première caisse du jour, 2 pour celle d'après la clôture. */
  rang: number;
  encaisse: number;
  sorties: number;
  attendu: number;
  /** Le nombre de reçus qui ne sont pas annulés. */
  recus: number;
  /** D'un jour passé : oubliée. */
  oubliee: boolean;
  /** Le nombre de reçus annulés : une caisse qui n'a que ceux-là se clôture aussi. */
  annules: number;
}

/** Une clôture de caisse. */
export interface Cloture {
  id: string;
  jour: string;
  rang: number;
  compteId: string;
  secretaire: string;
  encaisse: number;
  sorties: number;
  attendu: number;
  compte: number;
  ecart: number;
  clotureeLe: string;
  argentRecu: boolean;
  argentRecuLe: string | null;
}

/** CAI-09 : « Ma caisse ». */
export interface MaCaisse {
  ouvertes: CaisseOuverte[];
  clotures: Cloture[];
}

/** CAI-10 : le contrôle des clôtures (GET /api/caisse/clotures), pour le propriétaire. */
export interface ControleDesClotures {
  clotures: Cloture[];
  aRecuperer: number;
  nonCloturees: { compteId: string; secretaire: string; jour: string; attendu: number; recus: number }[];
  comptes: { id: string; nom: string }[];
}

/** « −5,000 », « +2,500 », « 0,000 ». */
export function ecrireUnEcart(ecart: number): string {
  return (ecart > 0 ? '+' : ecart < 0 ? '−' : '') + ecrireUnMontant(Math.abs(ecart));
}

/** « 27/11 ». */
export function jourCourt(jour: string): string {
  return formatDate(jour, 'dd/MM', FRANCAIS);
}
