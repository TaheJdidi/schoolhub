import { de } from './eleve';

describe("l'élision devant un prénom", () => {
  it('élide devant une voyelle ou un h', () => {
    expect(de('Adem Miled')).toBe("d'Adem Miled");
    expect(de('Émna Trabelsi')).toBe("d'Émna Trabelsi");
    expect(de('Hédi Ben Salah')).toBe("d'Hédi Ben Salah");
  });

  it("n'élide pas devant une consonne, ni devant un y", () => {
    expect(de('Sami Chaabane')).toBe('de Sami Chaabane');
    expect(de('Yassine Gharbi')).toBe('de Yassine Gharbi');
    expect(de('Youssef Mejri')).toBe('de Youssef Mejri');
  });
});
