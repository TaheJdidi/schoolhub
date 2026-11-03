const ACCUEIL = '/aujourdhui';

/**
 * La page où revenir après la connexion. Seule une adresse de l'application est acceptée,
 * qui commence par un seul « / » : un lien piégé (`?retour=//exemple.com`) ne peut pas envoyer vers un autre site.
 * Revenir sur la connexion n'aurait pas de sens : on va alors sur « Aujourd'hui ».
 */
export function adresseDeRetour(retour: string | null | undefined): string {
  if (!retour || !retour.startsWith('/') || retour.startsWith('//') || retour.startsWith('/\\')) return ACCUEIL;
  if (retour === '/connexion' || retour.startsWith('/connexion?')) return ACCUEIL;
  return retour;
}
