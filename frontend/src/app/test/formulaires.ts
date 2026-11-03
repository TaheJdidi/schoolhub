/** Saisit une valeur dans un champ, comme au clavier. */
export function saisir(page: HTMLElement, selecteur: string, valeur: string): void {
  const champ = page.querySelector(selecteur) as HTMLInputElement | null;
  if (!champ) throw new Error(`Aucun champ « ${selecteur} » dans la page`);
  champ.value = valeur;
  champ.dispatchEvent(new Event('input'));
}

/** Envoie le formulaire de la page, comme avec la touche Entrée. */
export function envoyer(page: HTMLElement): void {
  const formulaire = page.querySelector('form');
  if (!formulaire) throw new Error('Aucun formulaire dans la page');
  formulaire.dispatchEvent(new Event('submit', { cancelable: true }));
}
