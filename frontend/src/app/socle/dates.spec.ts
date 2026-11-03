import { dateDuJour } from './dates';

describe('dateDuJour', () => {
  it('écrit la date du jour en toutes lettres, avec une majuscule', () => {
    expect(dateDuJour(new Date(2026, 10, 3))).toBe('Mardi 3 novembre 2026');
    expect(dateDuJour(new Date(2028, 0, 15))).toBe('Samedi 15 janvier 2028');
  });
});
