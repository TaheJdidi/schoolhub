import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { Session } from './session';

/** Les pages du cadre : sans session, on va sur la connexion, qui ramènera ensuite à la page demandée. */
export const estConnecte: CanActivateFn = async (_route, etat) => {
  const session = inject(Session);
  const router = inject(Router);

  const moi = await session.charger();
  return moi ? true : router.createUrlTree(['/connexion'], { queryParams: { retour: etat.url } });
};

/**
 * Les pages réservées au propriétaire. Une secrétaire tombe sur l'adresse suivante, qui affiche
 * « Accès réservé au propriétaire ». Cette garde passe pendant la recherche de l'adresse, donc AVANT
 * celle du cadre (`estConnecte`) : elle charge elle-même la session, sinon un propriétaire qui ouvre
 * directement /journal serait pris pour une secrétaire.
 */
export const estProprietaire: CanMatchFn = async () => {
  const moi = await inject(Session).charger();
  return moi?.role === 'Proprietaire';
};

/** La page de connexion : une personne déjà connectée va directement sur « Aujourd'hui ». */
export const nonConnecte: CanActivateFn = async () => {
  const session = inject(Session);
  const router = inject(Router);

  const moi = await session.charger();
  return moi ? router.createUrlTree(['/aujourdhui']) : true;
};
