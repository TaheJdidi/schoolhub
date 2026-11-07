import { lireUneHeure } from './champ-heure';

describe('lireUneHeure', () => {
  it('met les minutes à 00 quand seule l’heure est écrite', () => {
    expect(lireUneHeure('10')).toBe('10:00');
    expect(lireUneHeure('8')).toBe('08:00');
    expect(lireUneHeure('8h')).toBe('08:00');
    expect(lireUneHeure(' 14 h ')).toBe('14:00');
  });

  it('lit les heures avec leurs minutes', () => {
    expect(lireUneHeure('8h30')).toBe('08:30');
    expect(lireUneHeure('08:30')).toBe('08:30');
    expect(lireUneHeure('8.30')).toBe('08:30');
    expect(lireUneHeure('830')).toBe('08:30');
    expect(lireUneHeure('1415')).toBe('14:15');
  });

  it('rend null pour un champ vide, undefined pour une heure illisible', () => {
    expect(lireUneHeure('')).toBeNull();
    expect(lireUneHeure('   ')).toBeNull();
    expect(lireUneHeure('25')).toBeUndefined();
    expect(lireUneHeure('8h75')).toBeUndefined();
    expect(lireUneHeure('8h7')).toBeUndefined();
    expect(lireUneHeure('midi')).toBeUndefined();
  });
});
