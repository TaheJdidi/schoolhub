import { formatDate } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { DELAI_DE_RECHERCHE } from '../../cadre/recherche-d-eleves';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { EleveTrouve, FicheDeLEleve } from '../eleves/eleve';
import { dateDuJourLocale, eleves, FicheDuGroupe, heure, TYPES } from '../groupes/groupe';
import {
  CAPACITE_ATTEINTE,
  EnregistrementDeLInscription,
  estComplet,
  GroupePossible,
  InscriptionDeLEleve,
  MessageWhatsApp,
  ModeDeTarification,
} from './inscription';

const CHOISIR_L_ELEVE_ET_LE_GROUPE = "Choisissez l'élève et le groupe";
const CHOISIR_LE_MODE = 'Choisissez le mode de tarification';

/** Une séance que l'élève peut choisir comme première : sa date « 2027-11-20 » et « samedi 20/11/2027 · 10h ». */
interface DateDeDebut {
  date: string;
  libelle: string;
}

/**
 * ELV-03 : inscrire un élève dans un groupe, depuis sa fiche (on choisit le groupe) ou depuis le groupe (on cherche
 * l'élève). La page propose le mode de tarification selon le type du groupe et la séance du jour, ou la prochaine, comme
 * première séance. Enregistrée, elle montre ce que l'élève paiera à cette séance et les messages WhatsApp à envoyer.
 */
@Component({
  selector: 'sh-page-d-inscription',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  templateUrl: './page-d-inscription.html',
  styleUrls: ['../eleves/eleves.css', './inscriptions.css'],
})
export class PageDInscription implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly delai = inject(DELAI_DE_RECHERCHE);

  protected readonly types = TYPES;
  protected readonly montant = ecrireUnMontant;
  protected readonly nombreDEleves = eleves;
  protected readonly estComplet = estComplet;

  /**
   * D'où vient la secrétaire : la fiche de l'élève (`eleve`), celle du groupe (`groupe`), ou une inscription de l'élève
   * qu'elle change de groupe (`changement`, ELV-09).
   */
  protected readonly depuis: 'eleve' | 'groupe' | 'changement' =
    this.route.snapshot.data['depuis'] === 'groupe' ? 'groupe' : this.route.snapshot.data['depuis'] === 'changement' ? 'changement' : 'eleve';
  private readonly id = this.route.snapshot.paramMap.get('id') ?? '';
  private readonly inscriptionDeDepart = this.route.snapshot.paramMap.get('inscription') ?? '';
  /** L'inscription qu'on change de groupe. */
  protected readonly ancienne = signal<InscriptionDeLEleve | null>(null);

  protected readonly ficheDeLEleve = signal<FicheDeLEleve | null>(null);
  protected readonly possibles = signal<GroupePossible[] | null>(null);
  /** « Groupes de son niveau », puis « Autres groupes » : seulement celles qui ont des groupes. */
  protected readonly sections = computed(() => {
    const possibles = this.possibles() ?? [];
    return [
      { titre: 'Groupes de son niveau', groupes: possibles.filter((g) => g.duNiveauDeLEleve) },
      { titre: 'Autres groupes', groupes: possibles.filter((g) => !g.duNiveauDeLEleve) },
    ].filter((section) => section.groupes.length > 0);
  });

  /** Depuis le groupe : la recherche, ses résultats et les élèves déjà inscrits. */
  protected readonly recherche = signal('');
  protected readonly trouves = signal<EleveTrouve[]>([]);
  protected readonly dejaInscrits = signal<ReadonlySet<string>>(new Set());

  protected readonly eleveChoisi = signal<{ id: string; prenom: string; nom: string } | null>(null);
  protected readonly groupeChoisi = signal<string | null>(null);
  protected readonly groupe = signal<FicheDuGroupe | null>(null);
  protected readonly mode = signal<ModeDeTarification | null>(null);
  protected readonly debutLe = signal<string | null>(null);

  protected readonly introuvable = signal<string | null>(null);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);
  protected readonly confirmation = signal<EnregistrementDeLInscription | null>(null);

  private minuterie: ReturnType<typeof setTimeout> | undefined;
  private derniereRecherche = 0;
  private dernierGroupe = 0;

  protected readonly titre = computed(() => {
    if (this.depuis === 'groupe') return 'Ajouter un élève';
    const fiche = this.ficheDeLEleve();
    if (this.depuis === 'changement') return fiche ? `Changer ${fiche.prenom} ${fiche.nom} de groupe` : 'Changer de groupe';
    return fiche ? `Inscrire ${fiche.prenom} ${fiche.nom}` : 'Inscrire un élève';
  });

  protected readonly sousTitre = computed(() => {
    const groupe = this.groupe();
    const ancienne = this.ancienne();
    if (this.depuis === 'changement') return ancienne ? `Depuis ${ancienne.groupe.nom} · ${ancienne.groupe.matiere}` : '';
    return this.depuis === 'groupe' && groupe ? `${groupe.nom} · ${groupe.matiere}` : '';
  });

  /** Un groupe fermé ou terminé n'accepte plus d'élève : le serveur le refuserait. */
  protected readonly ferme = computed(() => {
    const groupe = this.groupe();
    if (!groupe) return null;
    if (groupe.fermeLe) return "Ce groupe est fermé : on n'y inscrit plus";
    return groupe.termine ? "Ce groupe est terminé : on n'y inscrit plus" : null;
  });

  protected readonly complet = computed(() => {
    const groupe = this.groupe();
    return groupe !== null && estComplet(groupe);
  });

  protected readonly capaciteAtteinte = CAPACITE_ATTEINTE;

  /** Les modes à choisir : aucun pour une Étude, ni pour une Formation sans tranches. */
  protected readonly modes = computed<{ mode: ModeDeTarification; libelle: string }[]>(() => {
    const groupe = this.groupe();
    if (groupe?.type === 'Revision') {
      return [
        { mode: 'ALaSeance', libelle: 'À la séance' },
        { mode: 'EnTotalite', libelle: 'En totalité' },
      ];
    }
    if (groupe?.type === 'Formation' && groupe.tranches.length > 0) {
      return [
        { mode: 'EnUneFois', libelle: 'En une fois' },
        { mode: 'EnTranches', libelle: `En ${groupe.tranches.length} tranches` },
      ];
    }
    return [];
  });

  /** Les séances du groupe, une par jour, dans l'ordre. */
  protected readonly dates = computed<DateDeDebut[]>(() => {
    const groupe = this.groupe();
    if (!groupe) return [];
    const seances = groupe.type === 'Etude' ? groupe.mois.flatMap((mois) => mois.seances) : groupe.seances;
    const vues = new Set<string>();
    return seances
      .filter((seance) => !vues.has(seance.date) && vues.add(seance.date))
      .map((seance) => ({ date: seance.date, libelle: `${formatDate(seance.date, 'EEEE dd/MM/yyyy', FRANCAIS)} · ${heure(seance.debut)}` }));
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.minuterie));
  }

  async ngOnInit(): Promise<void> {
    try {
      if (this.depuis !== 'groupe') {
        const changement = this.depuis === 'changement';
        // Un changement ne propose que les groupes du même type, sans celui de départ.
        const params: Record<string, string> = changement ? { eleve: this.id, depuis: this.inscriptionDeDepart } : { eleve: this.id };
        const [fiche, possibles, inscriptions] = await Promise.all([
          firstValueFrom(this.http.get<FicheDeLEleve>(`/api/eleves/${this.id}`)),
          firstValueFrom(this.http.get<GroupePossible[]>('/api/inscriptions/groupes-possibles', { params })),
          changement
            ? firstValueFrom(this.http.get<InscriptionDeLEleve[]>('/api/inscriptions', { params: { eleve: this.id } }))
            : Promise.resolve([] as InscriptionDeLEleve[]),
        ]);
        this.ancienne.set(inscriptions.find((inscription) => inscription.id === this.inscriptionDeDepart) ?? null);
        this.ficheDeLEleve.set(fiche);
        this.eleveChoisi.set({ id: fiche.id, prenom: fiche.prenom, nom: fiche.nom });
        this.possibles.set(possibles);
      } else {
        const [fiche, inscriptions] = await Promise.all([
          firstValueFrom(this.http.get<FicheDuGroupe>(`/api/groupes/${this.id}`)),
          firstValueFrom(this.http.get<InscriptionDeLEleve[]>('/api/inscriptions', { params: { groupe: this.id } })),
        ]);
        this.groupeChoisi.set(fiche.id);
        this.montrerLeGroupe(fiche);
        // Un élève arrêté peut se réinscrire (ELV-08) : seules les inscriptions pas arrêtées comptent.
        this.dejaInscrits.set(new Set(inscriptions.filter((inscription) => inscription.arreteLe === null).map((inscription) => inscription.eleve.id)));
      }
    } catch (erreur) {
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) {
        this.introuvable.set(this.depuis === 'groupe' ? "Ce groupe n'existe pas." : "Cet élève n'existe pas.");
      } else {
        this.erreur.set(messageDErreur(erreur));
      }
    }
  }

  // ----- Depuis le groupe : chercher l'élève -----

  /** Une nouvelle recherche oublie l'élève choisi : on n'inscrit jamais un élève qu'on ne voit plus dans la liste. */
  protected chercher(evenement: Event): void {
    const texte = (evenement.target as HTMLInputElement).value.trim();
    this.recherche.set(texte);
    this.eleveChoisi.set(null);
    clearTimeout(this.minuterie);
    if (!texte) {
      this.derniereRecherche++;
      this.trouves.set([]);
      return;
    }
    this.minuterie = setTimeout(() => void this.lancerLaRecherche(texte), this.delai);
  }

  private async lancerLaRecherche(recherche: string): Promise<void> {
    const numero = ++this.derniereRecherche;
    try {
      const trouves = await firstValueFrom(this.http.get<EleveTrouve[]>('/api/eleves', { params: { recherche } }));
      if (numero === this.derniereRecherche) this.trouves.set(trouves);
    } catch (erreur) {
      if (numero === this.derniereRecherche) this.erreur.set(messageDErreur(erreur));
    }
  }

  protected choisirLEleve(eleve: EleveTrouve): void {
    this.eleveChoisi.set({ id: eleve.id, prenom: eleve.prenom, nom: eleve.nom });
    this.erreur.set(null);
  }

  // ----- Depuis l'élève : choisir le groupe -----

  /** Le groupe choisi : sa fiche donne ses séances, son type et ses tranches. Seule la dernière réponse compte. */
  protected async choisirLeGroupe(possible: GroupePossible): Promise<void> {
    const numero = ++this.dernierGroupe;
    this.groupeChoisi.set(possible.id);
    this.groupe.set(null);
    this.erreur.set(null);
    try {
      const fiche = await firstValueFrom(this.http.get<FicheDuGroupe>(`/api/groupes/${possible.id}`));
      if (numero === this.dernierGroupe) this.montrerLeGroupe(fiche);
    } catch (erreur) {
      if (numero === this.dernierGroupe) this.erreur.set(messageDErreur(erreur));
    }
  }

  /** Le mode se choisit de nouveau ; la première séance proposée est celle du jour, ou la suivante, ou la dernière. */
  private montrerLeGroupe(fiche: FicheDuGroupe): void {
    this.groupe.set(fiche);
    this.mode.set(null);
    const aujourdhui = dateDuJourLocale();
    const dates = this.dates();
    this.debutLe.set((dates.find((d) => d.date >= aujourdhui) ?? dates.at(-1))?.date ?? null);
  }

  protected choisirLeMode(mode: ModeDeTarification): void {
    this.mode.set(mode);
    this.erreur.set(null);
  }

  protected choisirLaDate(evenement: Event): void {
    this.debutLe.set((evenement.target as HTMLSelectElement).value);
  }

  // ----- Inscrire -----

  protected async inscrire(): Promise<void> {
    if (this.envoi()) return;
    const eleve = this.eleveChoisi();
    const groupe = this.groupe();
    if (!eleve || !groupe) {
      this.erreur.set(CHOISIR_L_ELEVE_ET_LE_GROUPE);
      return;
    }
    if (this.modes().length > 0 && !this.mode()) {
      this.erreur.set(CHOISIR_LE_MODE);
      return;
    }

    this.envoi.set(true);
    this.erreur.set(null);
    try {
      const confirmation =
        this.depuis === 'changement'
          ? this.http.post<EnregistrementDeLInscription>(`/api/inscriptions/${this.inscriptionDeDepart}/changement`, {
              groupeId: groupe.id,
              mode: this.mode(),
              debutLe: this.debutLe(),
            })
          : this.http.post<EnregistrementDeLInscription>('/api/inscriptions', {
              eleveId: eleve.id,
              groupeId: groupe.id,
              mode: this.mode(),
              debutLe: this.debutLe(),
            });
      this.confirmation.set(await firstValueFrom(confirmation));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }

  // ----- La confirmation -----

  /** « samedi 20/11/2027 ». */
  protected jour(date: string): string {
    return formatDate(date, 'EEEE dd/MM/yyyy', FRANCAIS);
  }

  /** « Au parent · 98 401 220 », « À Adem · 22 345 678 ». */
  protected pourQui(message: MessageWhatsApp): string {
    const qui = message.destinataire === 'Parent' ? 'Au parent' : `À ${message.destinataire}`;
    return `${qui} · ${message.telephone}`;
  }
}
