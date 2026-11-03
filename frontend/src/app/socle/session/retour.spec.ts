import { adresseDeRetour } from './retour';

describe('adresseDeRetour', () => {
  it.each([
    [undefined, '/aujourdhui'],
    [null, '/aujourdhui'],
    ['', '/aujourdhui'],
    ['/caisse', '/caisse'],
    ['/eleves?groupe=3', '/eleves?groupe=3'],
    ['//exemple.com', '/aujourdhui'],
    ['/\\exemple.com', '/aujourdhui'],
    ['https://exemple.com', '/aujourdhui'],
    ['caisse', '/aujourdhui'],
    ['/connexion?retour=%2Fcaisse', '/aujourdhui'],
  ])('adresseDeRetour(%s) → %s', (retour, attendu) => {
    expect(adresseDeRetour(retour)).toBe(attendu);
  });
});
