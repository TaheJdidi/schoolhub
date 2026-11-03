import { formatDate } from '@angular/common';
import { FRANCAIS } from './francais';

/** La date du jour affichée dans l'en-tête : « Mardi 3 novembre 2026 ». */
export function dateDuJour(maintenant: Date): string {
  const date = formatDate(maintenant, 'EEEE d MMMM y', FRANCAIS);
  return date.charAt(0).toUpperCase() + date.slice(1);
}
