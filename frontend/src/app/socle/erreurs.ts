import { HttpErrorResponse } from '@angular/common/http';

export const PAS_D_INTERNET = 'Pas de connexion à Internet : réessayez.';
export const ERREUR_INCONNUE = 'Une erreur est survenue : réessayez.';

/**
 * Le message à afficher pour une erreur d'appel à l'API. Les refus du serveur sont déjà écrits
 * en français (`{ "message": "…" }`) : ils s'affichent tels quels.
 */
export function messageDErreur(erreur: unknown): string {
  if (!(erreur instanceof HttpErrorResponse)) return ERREUR_INCONNUE;
  if (erreur.status === 0) return PAS_D_INTERNET;

  const message: unknown = erreur.error?.message;
  return typeof message === 'string' ? message : ERREUR_INCONNUE;
}
