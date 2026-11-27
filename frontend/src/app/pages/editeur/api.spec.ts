import { formaterDate, formaterMontant, lendemain, lireUnPrix } from './api';
import { codePropose } from './nouveau-centre';

describe('les outils du back office', () => {
  it('lit un prix en dinars, au millime près', () => {
    expect(lireUnPrix('90')).toBe(90);
    expect(lireUnPrix('75,500')).toBe(75.5);
    expect(lireUnPrix('1 250.250')).toBe(1250.25);
    expect(lireUnPrix('0')).toBeUndefined();
    expect(lireUnPrix('12,3456')).toBeUndefined();
    expect(lireUnPrix('90 TND')).toBeUndefined();
    expect(lireUnPrix('')).toBeUndefined();
  });

  it('écrit les montants et les dates comme on les lit', () => {
    expect(formaterMontant(270)).toBe('270,000 TND');
    expect(formaterDate('2027-04-04')).toBe('04/04/2027');
    expect(formaterDate(null)).toBe('');
    expect(lendemain('2027-02-28')).toBe('2027-03-01');
  });

  it('propose le code du lien public depuis le nom du centre', () => {
    expect(codePropose('Centre Les Jasmins')).toBe('les-jasmins');
    expect(codePropose("École d'été  2027 !")).toBe('ecole-d-ete-2027');
  });
});
