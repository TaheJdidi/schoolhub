import { Location } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { de, EnregistrementDeLEleve, FicheDeLEleve } from './eleve';
import { FormulaireDeLEleve } from './formulaire-de-l-eleve';

/**
 * ELV-02, ELV-04 et ELV-11 : la fiche d'un élève. Ses contacts, ses inscriptions (aucune pour l'instant : elles
 * viendront avec ELV-03), « Modifier » avec le même formulaire que la création, et « Supprimer la fiche » après
 * confirmation. L'avertissement « Doublon possible » d'une création arrive par l'état de la navigation. La page suit
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

  protected readonly de = de;
  protected readonly fiche = signal<FicheDeLEleve | null>(null);
  protected readonly introuvable = signal(false);
  protected readonly enModification = signal(false);
  protected readonly aConfirmer = signal(false);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly avertissement = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

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
    this.introuvable.set(false);
    this.enModification.set(false);
    this.aConfirmer.set(false);
    this.succes.set(null);
    this.erreur.set(null);
    const etat = this.location.getState() as { avertissement?: string | null } | null;
    this.avertissement.set(etat?.avertissement ?? null);
    try {
      const fiche = await firstValueFrom(this.http.get<FicheDeLEleve>(`/api/eleves/${id}`));
      if (this.route.snapshot.paramMap.get('id') === id) this.fiche.set(fiche);
    } catch (erreur) {
      if (this.route.snapshot.paramMap.get('id') !== id) return;
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) this.introuvable.set(true);
      else this.erreur.set(messageDErreur(erreur));
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
