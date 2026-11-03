import { initiales, libelleDuRole } from './personnes';

describe('personnes', () => {
  it('prend les initiales du prénom et du nom', () => {
    expect(initiales('Amira', 'Jlassi')).toBe('AJ');
    expect(initiales('Hedi', 'Ben Salah')).toBe('HB');
  });

  it('écrit le rôle avec ses accents', () => {
    expect(libelleDuRole('Proprietaire')).toBe('Propriétaire');
    expect(libelleDuRole('Secretaire')).toBe('Secrétaire');
  });
});
