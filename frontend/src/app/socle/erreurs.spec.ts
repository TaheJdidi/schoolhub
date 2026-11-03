import { HttpErrorResponse } from '@angular/common/http';
import { ERREUR_INCONNUE, messageDErreur, PAS_D_INTERNET } from './erreurs';

describe('messageDErreur', () => {
  it("dit qu'il n'y a pas d'Internet quand le serveur ne répond pas", () => {
    expect(messageDErreur(new HttpErrorResponse({ status: 0 }))).toBe(PAS_D_INTERNET);
  });

  it('reprend le message du serveur tel quel', () => {
    const refus = new HttpErrorResponse({ status: 401, error: { message: 'Email ou mot de passe incorrect' } });

    expect(messageDErreur(refus)).toBe('Email ou mot de passe incorrect');
  });

  it.each([
    ['un 500 vide', new HttpErrorResponse({ status: 500, error: null })],
    ['un 500 en texte', new HttpErrorResponse({ status: 500, error: 'Internal Server Error' })],
    ['une autre erreur', new Error('panne')],
  ])('donne le message général pour %s', (_cas, erreur) => {
    expect(messageDErreur(erreur)).toBe(ERREUR_INCONNUE);
  });

  it('a les textes exacts', () => {
    expect(PAS_D_INTERNET).toBe('Pas de connexion à Internet : réessayez.');
    expect(ERREUR_INCONNUE).toBe('Une erreur est survenue : réessayez.');
  });
});
