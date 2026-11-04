import { Location } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { de, EnregistrementDeLEleve, FicheDeLEleve } from './eleve';
import { FormulaireDeLEleve } from './formulaire-de-l-eleve';

/**
 * ELV-02, ELV-04 et ELV-11 : la fiche d'un élève. Ses contacts, ses inscriptions (aucune pour l'instant : elles
 * viendront avec ELV-03), « Modifier » avec le même formulaire que la création, et « Supprimer la fiche » après
 * confirmation. L'avertissement « Doublon possible » d'une création arrive par l'état de la navigation.
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

  private get id(): string {
    return this.route.snapshot.paramMap.get('id') ?? '';
  }

  async ngOnInit(): Promise<void> {
    const etat = this.location.getState() as { avertissement?: string | null } | null;
    this.avertissement.set(etat?.avertissement ?? null);
    try {
      this.fiche.set(await firstValueFrom(this.http.get<FicheDeLEleve>(`/api/eleves/${this.id}`)));
    } catch (erreur) {
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
    if (this.envoi()) return;
    this.envoi.set(true);
    try {
      await firstValueFrom(this.http.delete(`/api/eleves/${this.id}`));
      await this.router.navigate(['/eleves']);
    } catch (erreur) {
      this.aConfirmer.set(false);
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
