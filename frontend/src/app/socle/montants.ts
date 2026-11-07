/**
 * Lit un nombre écrit à la tunisienne : virgule ou point, espaces permis (« 1 000,500 »).
 * Rend `null` pour une saisie vide, et `undefined` pour une saisie qui n'est pas un nombre (« 2,5 TND »).
 */
export function lireUnNombre(saisie: string): number | null | undefined {
  const nettoyee = saisie.replace(/\s/g, '');
  if (nettoyee === '') return null;
  if (!/^-?\d+([.,]\d+)?$/.test(nettoyee)) return undefined;
  return Number(nettoyee.replace(',', '.'));
}

/** Un montant en dinars, au millime, avec une virgule : « 2,500 », « −35,000 ». Vide s'il n'y a pas de montant. */
export function ecrireUnMontant(montant: number | null): string {
  if (montant === null) return '';
  const ecrit = Math.abs(montant).toFixed(3).replace('.', ',');
  return montant < 0 && ecrit !== '0,000' ? `−${ecrit}` : ecrit;
}
