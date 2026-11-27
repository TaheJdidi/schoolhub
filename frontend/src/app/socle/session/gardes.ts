import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { premierePage } from './moi';
import { Session } from './session';

/**
 * Les pages du cadre d'un centre : sans session, on va sur la connexion, qui ramènera ensuite à la page demandée.
 * L'Éditeur n'appartient à aucun centre : il va dans son back office (EDI-00).
 */
export const estConnecte: CanActivateFn = async (_route, etat) => {
  const session = inject(Session);
  const router = inject(Router);

  const moi = await session.charger();
  if (!moi) return router.createUrlTree(['/connexion'], { queryParams: { retour: etat.url } });
  return moi.role === 'Editeur' ? router.createUrlTree(['/editeur']) : true;
};

/** Le back office (EDI-00) : réservé à l'Éditeur. Un compte de centre retourne sur sa première page. */
export const estEditeur: CanActivateFn = async (_route, etat) => {
  const session = inject(Session);
  const router = inject(Router);

  const moi = await session.charger();
  if (!moi) return router.createUrlTree(['/connexion'], { queryParams: { retour: etat.url } });
  return moi.role === 'Editeur' ? true : router.createUrlTree(['/aujourdhui']);
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

/** La page de connexion : une personne déjà connectée va directement sur sa première page. */
export const nonConnecte: CanActivateFn = async () => {
  const session = inject(Session);
  const router = inject(Router);

  const moi = await session.charger();
  return moi ? router.createUrlTree([premierePage(moi)]) : true;
};
