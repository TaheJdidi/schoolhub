import { Location } from '@angular/common';
import { inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

/** Le lien reçu par email (invitation ou mot de passe oublié) : le compte et son jeton. */
export interface LienDeLEmail {
  compte: string;
  jeton: string;
}

/**
 * Lit le lien de l'email, à appeler dans le constructeur d'une page.
 * Le lien quitte aussitôt la barre d'adresse, et donc l'historique du navigateur : il reste dans l'état
 * de navigation, que le navigateur garde quand on recharge la page. Sans lien, le compte et le jeton
 * sont vides, et le serveur répondra « Ce lien a expiré ».
 */
export function lireLeLienDeLEmail(): LienDeLEmail {
  const route = inject(ActivatedRoute);
  const router = inject(Router);
  const location = inject(Location);

  const parametres = route.snapshot.queryParamMap;
  const compte = parametres.get('compte');
  const jeton = parametres.get('jeton');
  if (compte !== null || jeton !== null) {
    const lien = { compte: compte ?? '', jeton: jeton ?? '' };
    router.navigate([], { relativeTo: route, queryParams: {}, replaceUrl: true, state: lien });
    return lien;
  }

  // Après un rechargement : le lien est dans l'état de navigation.
  const etat = location.getState() as Partial<Record<keyof LienDeLEmail, unknown>> | null;
  return {
    compte: typeof etat?.compte === 'string' ? etat.compte : '',
    jeton: typeof etat?.jeton === 'string' ? etat.jeton : '',
  };
}
