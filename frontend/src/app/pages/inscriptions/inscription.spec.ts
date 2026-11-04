import { estComplet, libelleDuMode } from './inscription';

describe("le mode d'une inscription", () => {
  it('se lit avec son prix', () => {
    expect(libelleDuMode({ mode: 'ParMois', prixNormal: 40, nombreDeTranches: 0 })).toBe('Étude, 40,000 TND par Mois');
    expect(libelleDuMode({ mode: 'EnTotalite', prixNormal: 100, nombreDeTranches: 0 })).toBe('Révision en totalité, 100,000 TND');
    expect(libelleDuMode({ mode: 'ALaSeance', prixNormal: 100, nombreDeTranches: 0 })).toBe('Révision à la séance, 100,000 TND');
    expect(libelleDuMode({ mode: 'EnUneFois', prixNormal: 300, nombreDeTranches: 3 })).toBe('Formation en une fois, 300,000 TND');
    expect(libelleDuMode({ mode: 'EnTranches', prixNormal: 300, nombreDeTranches: 3 })).toBe('Formation en 3 tranches, 300,000 TND');
  });

  it('dit si un groupe est complet', () => {
    expect(estComplet({ eleves: 15, capacite: 15 })).toBe(true);
    expect(estComplet({ eleves: 14, capacite: 15 })).toBe(false);
    expect(estComplet({ eleves: 40, capacite: null })).toBe(false);
  });
});
