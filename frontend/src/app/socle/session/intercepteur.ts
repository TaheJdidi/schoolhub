import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { Session } from './session';

/**
 * Les adresses où un 401 est un refus normal, traité par la page elle-même :
 * la connexion et les mots de passe, la déconnexion, et /api/moi, que les gardes traitent sans message.
 */
const SANS_REGLE = [
  '/api/connexion',
  '/api/deconnexion',
  '/api/moi',
  '/api/invitation',
  '/api/mot-de-passe-oublie',
  '/api/nouveau-mot-de-passe',
];

/**
 * La session est tombée pendant le travail (12 heures sans activité, compte désactivé, mot de passe
 * changé ailleurs) : le serveur répond 401 à la prochaine action. On oublie la personne et on revient
 * à la connexion, qui dira « Votre session a expiré » puis ramènera à la page en cours.
 */
export const intercepteurDeSession: HttpInterceptorFn = (requete, suite) => {
  const session = inject(Session);
  const router = inject(Router);

  return suite(requete).pipe(
    catchError((erreur: unknown) => {
      const chemin = requete.url.split('?')[0];
      if (erreur instanceof HttpErrorResponse && erreur.status === 401 && !SANS_REGLE.includes(chemin)) {
        session.oublier();
        router.navigate(['/connexion'], { queryParams: { retour: router.url, expiree: 1 } });
      }
      return throwError(() => erreur);
    }),
  );
};
