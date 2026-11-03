import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';

/** Un niveau, tel que l'API le décrit (GET /api/niveaux). Un niveau d'office ne se modifie pas. */
export interface Niveau {
  id: string;
  nom: string;
  dOffice: boolean;
}

const ADRESSE = '/api/niveaux';
const NOM_VIDE = 'Saisissez le nom du niveau';

/**
 * CEN-03 : les niveaux ajoutés par le centre (ajouter, renommer, retirer), et les 29 niveaux d'office, repliés.
 * Une seule action à la fois : pendant un envoi, tous les boutons de la carte sont bloqués.
 */
@Component({
  selector: 'sh-carte-des-niveaux',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  templateUrl: './carte-des-niveaux.html',
  styleUrl: './parametres.css',
})
export class CarteDesNiveaux implements OnInit {
  private readonly http = inject(HttpClient);

  private readonly niveaux = signal<Niveau[]>([]);
  protected readonly ajoutes = computed(() => this.niveaux().filter((n) => !n.dOffice));
  protected readonly dOffice = computed(() => this.niveaux().filter((n) => n.dOffice));

  protected readonly ajout = new FormGroup({ nouveau: new FormControl('', { nonNullable: true }) });
  protected readonly nouveauNom = new FormControl('', { nonNullable: true });
  /** Le niveau dont le nom est en train de changer. */
  protected readonly enRenommage = signal<string | null>(null);
  /** Le niveau dont le retrait attend « Oui » ou « Non ». */
  protected readonly aConfirmer = signal<string | null>(null);
  protected readonly envoi = signal(false);
  protected readonly succes = signal<string | null>(null);
  protected readonly erreur = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      await this.charger();
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  protected ajouter(): Promise<void> {
    const nom = this.ajout.controls.nouveau.value.trim();
    if (!nom) return this.refuser(NOM_VIDE);

    return this.agir(async () => {
      await firstValueFrom(this.http.post<Niveau>(ADRESSE, { nom }));
      this.ajout.reset();
      await this.charger();
      return 'Niveau ajouté';
    });
  }

  protected commencerLeRenommage(niveau: Niveau): void {
    this.aConfirmer.set(null);
    this.nouveauNom.setValue(niveau.nom);
    this.enRenommage.set(niveau.id);
  }

  protected annulerLeRenommage(): void {
    this.enRenommage.set(null);
  }

  protected renommer(niveau: Niveau): Promise<void> {
    const nom = this.nouveauNom.value.trim();
    if (!nom) return this.refuser(NOM_VIDE);

    return this.agir(async () => {
      const aJour = await firstValueFrom(this.http.put<Niveau>(`${ADRESSE}/${niveau.id}`, { nom }));
      this.niveaux.update((liste) => liste.map((n) => (n.id === aJour.id ? aJour : n)));
      this.enRenommage.set(null);
      return 'Niveau renommé';
    });
  }

  protected demanderConfirmation(niveau: Niveau): void {
    this.enRenommage.set(null);
    this.aConfirmer.set(niveau.id);
  }

  protected annulerLaConfirmation(): void {
    this.aConfirmer.set(null);
  }

  protected retirer(niveau: Niveau): Promise<void> {
    this.aConfirmer.set(null);
    return this.agir(async () => {
      await firstValueFrom(this.http.delete(`${ADRESSE}/${niveau.id}`));
      this.niveaux.update((liste) => liste.filter((n) => n.id !== niveau.id));
      return 'Niveau retiré';
    });
  }

  private async charger(): Promise<void> {
    this.niveaux.set(await firstValueFrom(this.http.get<Niveau[]>(ADRESSE)));
  }

  /** Une action vers le serveur : le message vert la confirme, ou le refus s'affiche en rouge. */
  private async agir(action: () => Promise<string>): Promise<void> {
    if (this.envoi()) return;

    this.envoi.set(true);
    this.succes.set(null);
    this.erreur.set(null);
    try {
      this.succes.set(await action());
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  private async refuser(message: string): Promise<void> {
    this.succes.set(null);
    this.erreur.set(message);
  }
}
