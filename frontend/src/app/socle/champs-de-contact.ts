import { AfterContentInit, DestroyRef, Directive, ElementRef, inject, Injector, OnInit, Renderer2 } from '@angular/core';
import { AbstractControl, NG_VALIDATORS, NgControl, ValidationErrors, Validator } from '@angular/forms';

/**
 * Les messages sous un champ qui ne se lit pas : courts, pour tenir sur une ligne sous le champ, sans pousser les champs
 * voisins. Les champs restent vides tant que rien n'est écrit (pas d'exemple, qu'on prendrait pour un vrai numéro).
 */
export const TELEPHONE_PAS_VALABLE = 'Il faut 8 chiffres';
export const EMAIL_PAS_VALABLE = "Cet email n'est pas valable";

/** La taille la plus grande d'un email, celle du serveur (AdresseEmail.LongueurMaximale). */
const LONGUEUR_MAXIMALE_D_UN_EMAIL = 256;

/**
 * Les 8 chiffres d'un numéro tunisien, comme le serveur les lit (TelephoneTunisien) : sans les espaces, tirets et points,
 * ni l'indicatif « +216 » ou « 00216 ». `null` si rien n'est écrit, `undefined` si ce n'est pas un numéro de 8 chiffres.
 */
export function lireUnTelephone(saisie: string): string | null | undefined {
  let texte = saisie.replace(/[\s\-.‎‏‪-‮⁦-⁩]/g, '');
  if (texte === '') return null;
  if (texte.startsWith('+216')) texte = texte.slice(4);
  else if (texte.startsWith('00216')) texte = texte.slice(5);
  return /^\d{8}$/.test(texte) ? texte : undefined;
}

/** Les chiffres groupés comme on les lit, même incomplets : « 22 », « 22 345 », « 22 345 678 ». */
export function grouperUnTelephone(chiffres: string): string {
  return [chiffres.slice(0, 2), chiffres.slice(2, 5), chiffres.slice(5, 8)].filter((partie) => partie !== '').join(' ');
}

/** Un email écrit tel quel, sans espace, avec un domaine qui a un point, comme le serveur l'accepte (AdresseEmail). */
export function estUnEmail(saisie: string): boolean {
  const email = saisie.trim();
  return email.length <= LONGUEUR_MAXIMALE_D_UN_EMAIL && /^[^\s@<>(),;:"]+@[^\s@<>(),;:"]+\.[^\s@<>(),;:".]+$/.test(email);
}

/**
 * Ce que les deux champs ont en commun : un message sous le champ quand ce qui est écrit ne se lit pas, dès qu'on sort du champ (ou que le formulaire est envoyé), jusqu'à ce que ce soit corrigé.
 * Un champ vide n'est pas refusé ici : c'est `requis` qui dit s'il est obligatoire.
 */
@Directive()
abstract class ChampDeContact implements Validator, OnInit, AfterContentInit {
  protected readonly element = inject<ElementRef<HTMLInputElement>>(ElementRef).nativeElement;
  private readonly rendu = inject(Renderer2);
  private readonly injecteur = inject(Injector);
  // Lu dans ngOnInit : le demander ici tournerait en rond (le contrôle du formulaire demande ce validateur).
  private champ: NgControl | null = null;
  private message: HTMLElement | null = null;
  private quitte = false;

  protected abstract readonly pasValable: string;
  protected abstract readonly cle: string;
  protected abstract valable(texte: string): boolean;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.message?.remove());
  }

  ngOnInit(): void {
    this.champ = this.injecteur.get(NgControl, null, { self: true, optional: true });
    // Le formulaire envoyé montre aussi le message, sans attendre qu'on passe dans le champ.
    this.element.form?.addEventListener('submit', () => {
      this.quitte = true;
      this.afficher();
    });
  }

  /**
   * Une valeur remise par la page (un brouillon repris, la fiche à modifier) met le message à jour. Ici et pas dans
   * ngOnInit : `formControlName` n'a son contrôle qu'après, quand il passe avant cette directive.
   */
  ngAfterContentInit(): void {
    this.champ?.statusChanges?.subscribe(() => this.afficher());
  }

  validate(controle: AbstractControl): ValidationErrors | null {
    const texte = `${controle.value ?? ''}`;
    return texte.trim() === '' || this.valable(texte) ? null : { [this.cle]: true };
  }

  /** En sortant du champ : la saisie est remise au propre, puis le message s'affiche si elle ne se lit toujours pas. */
  protected sortir(): void {
    this.quitte = true;
    this.afficher();
  }

  /** Change ce qui est écrit dans le champ, et dans le formulaire quand il y en a un. */
  protected ecrire(texte: string): void {
    if (texte === this.element.value) return;
    const curseurALaFin = this.element.selectionStart === this.element.value.length;
    this.element.value = texte;
    if (curseurALaFin) this.element.setSelectionRange?.(texte.length, texte.length);
    this.champ?.control?.setValue(texte);
  }

  /** Le message sous le champ, une fois qu'on en est sorti, tant que ce qui est écrit ne se lit pas. */
  protected afficher(): void {
    const texte = this.element.value;
    const refuse = this.quitte && texte.trim() !== '' && !this.valable(texte);
    this.rendu.setAttribute(this.element, 'aria-invalid', String(refuse));
    if (!refuse) {
      this.message?.remove();
      this.message = null;
      return;
    }
    if (!this.message) {
      this.message = this.rendu.createElement('small');
      this.rendu.addClass(this.message, 'erreur-du-champ');
      this.rendu.setAttribute(this.message, 'aria-live', 'polite');
      this.message!.textContent = this.pasValable;
      this.element.insertAdjacentElement('afterend', this.message!);
    }
  }
}

/**
 * `<input shTelephone formControlName="telephone">` : un numéro tunisien de 8 chiffres. On ne peut y écrire que des
 * chiffres (et l'indicatif « +216 » d'un numéro collé, retiré) ; les chiffres se groupent pendant qu'on écrit
 * (« 22 345 678 »), et pas plus de 8. Marche aussi sans formulaire Angular (le formulaire public).
 */
@Directive({
  selector: 'input[shTelephone]',
  host: { inputmode: 'tel', '(input)': 'taper()', '(blur)': 'sortir()' },
  providers: [{ provide: NG_VALIDATORS, useExisting: ChampTelephone, multi: true }],
})
export class ChampTelephone extends ChampDeContact {
  protected readonly pasValable = TELEPHONE_PAS_VALABLE;
  protected readonly cle = 'telephone';

  protected valable(texte: string): boolean {
    return lireUnTelephone(texte) !== undefined;
  }

  protected taper(): void {
    const texte = this.element.value;
    // Au milieu du numéro, on enlève seulement les lettres, pour ne pas déplacer le curseur.
    if (this.element.selectionStart !== texte.length) {
      this.ecrire(texte.replace(/[^\d\s+\-.]/g, ''));
      this.afficher();
      return;
    }
    const sansIndicatif = texte.trim().replace(/^(\+216|00216)/, '');
    this.ecrire(grouperUnTelephone(sansIndicatif.replace(/\D/g, '').slice(0, 8)));
    this.afficher();
  }

  protected override sortir(): void {
    const chiffres = lireUnTelephone(this.element.value);
    if (chiffres) this.ecrire(grouperUnTelephone(chiffres));
    super.sortir();
  }
}

/**
 * `<input shEmail formControlName="email">` : un email. Les espaces n'y entrent pas, et le message dit quand l'adresse
 * ne se lit pas (« prenom@exemple » sans point, deux @…), avant même d'envoyer le formulaire.
 */
@Directive({
  selector: 'input[shEmail]',
  host: { type: 'email', inputmode: 'email', '(input)': 'taper()', '(blur)': 'sortir()' },
  providers: [{ provide: NG_VALIDATORS, useExisting: ChampEmail, multi: true }],
})
export class ChampEmail extends ChampDeContact {
  protected readonly pasValable = EMAIL_PAS_VALABLE;
  protected readonly cle = 'email';

  protected valable(texte: string): boolean {
    return estUnEmail(texte);
  }

  protected taper(): void {
    this.ecrire(this.element.value.replace(/\s/g, ''));
    this.afficher();
  }
}
