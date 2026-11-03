import { ecrireUnMontant, lireUnNombre } from './montants';

describe('les montants', () => {
  it('lit les montants écrits à la tunisienne', () => {
    expect(lireUnNombre('1 000,500')).toBe(1000.5);
    expect(lireUnNombre('2.5')).toBe(2.5);
    expect(lireUnNombre('2,5')).toBe(2.5);
    expect(lireUnNombre(' 40 ')).toBe(40);
    expect(lireUnNombre('  ')).toBeNull();
    expect(lireUnNombre('2,5 TND')).toBeUndefined();
    expect(lireUnNombre('deux')).toBeUndefined();
    expect(lireUnNombre('1,2,3')).toBeUndefined();
  });

  it('écrit les montants au millime', () => {
    expect(ecrireUnMontant(20)).toBe('20,000');
    expect(ecrireUnMontant(2.5)).toBe('2,500');
    expect(ecrireUnMontant(null)).toBe('');
  });
});
