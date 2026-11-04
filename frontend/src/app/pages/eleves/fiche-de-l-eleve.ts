import { formatDate, Location } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom, Observable } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant, lireUnNombre } from '../../socle/montants';
import { Session } from '../../socle/session/session';
import { APayer, AssuranceDeLEleve, InscriptionDeLEleve, libelleDuMode } from '../inscriptions/inscription';
import { de, EnregistrementDeLEleve, FicheDeLEleve } from './eleve';
import { FormulaireDeLEleve } from './formulaire-de-l-eleve';

/** L'action ouverte sur la fiche : un formulaire ou une question « Oui · Non ». */
type Action =
  | { genre: 'prix'; inscription: string }
  | { genre: 'payeAvant'; inscription: string; cle: string }
  | { genre: 'reste'; inscription: string }
  | { genre: 'assurancePayee' }
  | { genre: 'dispense' };

/**
 * ELV-02 à ELV-07, ELV-10 et ELV-11 : la fiche d'un élève. « À payer » d'abord ; ses contacts, ses inscriptions (prix
 * spécial du propriétaire, sommes payées avant SchoolHub, « Payer tout le reste ») et l'assurance de l'année (payée avant
 * SchoolHub, dispense du propriétaire),
 * « Modifier » avec le même formulaire que la création, et « Supprimer la fiche » après confirmation, tant qu'il n'a
 * aucune inscription. L'avertissement « Doublon possible » d'une création arrive par l'état de la navigation. La page suit
 * l'adresse : la recherche de l'en-tête mène d'une fiche à une autre sans recréer la page.
 */
@Component({
  selector: 'sh-page-de-l-eleve',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormulaireDeLEleve, RouterLink],
  templateUrl: './fiche-de-l-eleve.html',
  styleUrl: './eleves.css',
})
export class PageDeLEleve implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly destroyRef = inject(DestroyRef);
  private readonly session = inject(Session);

  protected readonly de = de;
  protected readonly libelleDuMode = libelleDuMode;
  protected readonly montant = ecrireUnMontant;
  protected readonly fiche = signal<FicheDeLEleve | null>(null);
  protected readonly inscriptions = signal<InscriptionDeLEleve[]>([]);
  /** L'assurance de l'année ; `null` quand elle est sans objet. */
  protected readonly assurance = signal<AssuranceDeLEleve | null>(null);
  protected readonly aPayer = signal<APayer | null>(null);
  /** Le propriétaire accorde un prix spécial et dispense de l'assurance ; la secrétaire lit qu'elle ne le peut pas. */
  protected readonly proprietaire = computed(() => this.session.personne()?.role === 'Proprietaire');
  protected readonly action = signal<Action | null>(null);
  protected readonly agit = signal(false);
  protected readonly erreurDesInscriptions = signal<string | null>(null);
  protected readonly erreurDeLAssurance = signal<string | null>(null);
  protected readonly introuvable = signal(false);
  protected readonly enModification = signal(false);
  protected readonly aConfirmer = signal(false);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly avertissement = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  /** « samedi 20/11/2027 ». */
  protected jour(date: string): string {
    return formatDate(date, 'EEEE dd/MM/yyyy', FRANCAIS);
  }

  /** « 20/11/2027 ». */
  protected date(date: string): string {
    return formatDate(date, 'dd/MM/yyyy', FRANCAIS);
  }

  /** « 22 345 678 · adem@exemple.tn », ou « — » s'il n'y a rien. */
  protected joindre(...valeurs: (string | null)[]): string {
    return valeurs.filter((valeur) => !!valeur).join(' · ') || '—';
  }

  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((parametres) => void this.charger(parametres.get('id') ?? ''));
  }

  /** La fiche de l'élève de l'adresse : tout ce que montrait la fiche précédente est oublié. */
  private async charger(id: string): Promise<void> {
    this.fiche.set(null);
    this.inscriptions.set([]);
    this.assurance.set(null);
    this.aPayer.set(null);
    this.action.set(null);
    this.erreurDesInscriptions.set(null);
    this.erreurDeLAssurance.set(null);
    this.introuvable.set(false);
    this.enModification.set(false);
    this.aConfirmer.set(false);
    this.succes.set(null);
    this.erreur.set(null);
    const etat = this.location.getState() as { avertissement?: string | null } | null;
    this.avertissement.set(etat?.avertissement ?? null);
    // Lu une fois : sans cela, il reviendrait après un rechargement ou un retour en arrière, même corrigé.
    if (etat?.avertissement !== undefined) {
      const { avertissement: _lu, ...reste } = etat;
      this.location.replaceState(this.location.path(), '', reste);
    }
    try {
      // La fiche, ses inscriptions, son assurance (204 : sans objet) et « À payer » arrivent ensemble.
      const [fiche, [inscriptions, assurance, aPayer]] = await Promise.all([
        firstValueFrom(this.http.get<FicheDeLEleve>(`/api/eleves/${id}`)),
        this.lireCeQuIlDoit(id),
      ]);
      if (this.route.snapshot.paramMap.get('id') !== id) return;
      this.fiche.set(fiche);
      this.montrerCeQuIlDoit(inscriptions, assurance, aPayer);
    } catch (erreur) {
      if (this.route.snapshot.paramMap.get('id') !== id) return;
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) this.introuvable.set(true);
      else this.erreur.set(messageDErreur(erreur));
    }
  }

  /** Les inscriptions, l'assurance et « À payer » : ce qu'une action peut changer. */
  private lireCeQuIlDoit(id: string): Promise<[InscriptionDeLEleve[], AssuranceDeLEleve | null, APayer]> {
    return Promise.all([
      firstValueFrom(this.http.get<InscriptionDeLEleve[]>('/api/inscriptions', { params: { eleve: id } })),
      firstValueFrom(this.http.get<AssuranceDeLEleve | null>(`/api/eleves/${id}/assurance`)),
      firstValueFrom(this.http.get<APayer>(`/api/eleves/${id}/a-payer`)),
    ]);
  }

  private montrerCeQuIlDoit(inscriptions: InscriptionDeLEleve[], assurance: AssuranceDeLEleve | null, aPayer: APayer): void {
    this.inscriptions.set(inscriptions);
    this.assurance.set(assurance);
    this.aPayer.set(aPayer);
  }

  // ----- Les actions sur les inscriptions et l'assurance -----

  protected ouvrir(action: Action): void {
    this.action.set(action);
    this.erreurDesInscriptions.set(null);
    this.erreurDeLAssurance.set(null);
  }

  protected fermer(): void {
    this.action.set(null);
  }

  protected prixOuvertPour(inscription: string): boolean {
    const action = this.action();
    return action?.genre === 'prix' && action.inscription === inscription;
  }

  protected payeAvantOuvertPour(inscription: string, cle: string): boolean {
    const action = this.action();
    return action?.genre === 'payeAvant' && action.inscription === inscription && action.cle === cle;
  }

  protected resteOuvertPour(inscription: string): boolean {
    const action = this.action();
    return action?.genre === 'reste' && action.inscription === inscription;
  }

  /** « à payer », « payée avant SchoolHub », « dispensée (Orphelin) ». */
  protected etatDeLAssurance(assurance: AssuranceDeLEleve): string {
    if (assurance.etat === 'Dispensee') return `dispensée (${assurance.raisonDeLaDispense})`;
    return assurance.etat === 'PayeeAvantSchoolHub' ? 'payée avant SchoolHub' : 'à payer';
  }

  protected accorderUnPrixSpecial(inscription: string, montant: string, raison: string): void {
    // Un montant illisible part vide : le serveur répond « Le prix spécial doit être positif ou nul, au millime près ».
    void this.agir('inscriptions', () =>
      this.http.put(`/api/inscriptions/${inscription}/prix-special`, { montant: lireUnNombre(montant) ?? null, raison }),
    );
  }

  protected retirerLePrixSpecial(inscription: string): void {
    void this.agir('inscriptions', () => this.http.delete(`/api/inscriptions/${inscription}/prix-special`));
  }

  protected payerAvantSchoolHub(inscription: string, cle: string): void {
    void this.agir('inscriptions', () => this.http.post(`/api/inscriptions/${inscription}/paye-avant-schoolhub`, { cle }));
  }

  protected regrouperLeReste(inscription: string): void {
    void this.agir('inscriptions', () => this.http.post(`/api/inscriptions/${inscription}/reste`, null));
  }

  protected assurancePayeeAvantSchoolHub(id: string): void {
    void this.agir('assurance', () => this.http.post(`/api/eleves/${id}/assurance/payee-avant-schoolhub`, null));
  }

  protected dispenser(id: string, raison: string): void {
    void this.agir('assurance', () => this.http.post(`/api/eleves/${id}/assurance/dispense`, { raison }));
  }

  /**
   * Envoie l'action, puis relit ce que l'élève doit. Les boutons restent bloqués pendant l'envoi ; un refus s'affiche
   * sous la carte de l'action, qui reste ouverte.
   */
  private async agir(carte: 'inscriptions' | 'assurance', requete: () => Observable<unknown>): Promise<void> {
    const fiche = this.fiche();
    if (this.agit() || !fiche) return;
    const erreur = carte === 'inscriptions' ? this.erreurDesInscriptions : this.erreurDeLAssurance;
    this.agit.set(true);
    erreur.set(null);
    try {
      await firstValueFrom(requete());
      this.action.set(null);
      const [inscriptions, assurance, aPayer] = await this.lireCeQuIlDoit(fiche.id);
      if (this.route.snapshot.paramMap.get('id') === fiche.id) this.montrerCeQuIlDoit(inscriptions, assurance, aPayer);
    } catch (refus) {
      erreur.set(messageDErreur(refus));
    } finally {
      this.agit.set(false);
    }
  }

  protected modifier(): void {
    this.succes.set(null);
    this.avertissement.set(null);
    this.erreur.set(null);
    this.aConfirmer.set(false);
    this.enModification.set(true);
  }

  protected enregistre(enregistrement: EnregistrementDeLEleve): void {
    this.fiche.set(enregistrement.eleve);
    this.enModification.set(false);
    this.succes.set('Fiche enregistrée');
    this.avertissement.set(enregistrement.avertissement);
  }

  protected demanderConfirmation(): void {
    this.succes.set(null);
    this.avertissement.set(null);
    this.erreur.set(null);
    this.aConfirmer.set(true);
  }

  protected async supprimer(): Promise<void> {
    const fiche = this.fiche();
    if (this.envoi() || !fiche) return;
    this.envoi.set(true);
    try {
      // L'élève affiché, et non celui de l'adresse : ils ne font qu'un, mais c'est lui que la secrétaire a confirmé.
      await firstValueFrom(this.http.delete(`/api/eleves/${fiche.id}`));
      await this.router.navigate(['/eleves']);
    } catch (erreur) {
      this.aConfirmer.set(false);
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
