import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal, WritableSignal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { aSesPropresContacts } from '../eleves/eleve';
import { heure } from '../groupes/groupe';
import { FormulaireDuCentre, GroupeDuFormulaire } from './formulaire-du-centre';
import { ChampEmail, ChampTelephone } from '../../socle/champs-de-contact';

/** Une matière proposée pour le niveau choisi, avec ses groupes. */
interface MatiereProposee {
  matiere: string;
  groupes: GroupeDuFormulaire[];
}

/**
 * DEM-01 : le formulaire public d'inscription d'un centre, ouvert sans compte depuis son lien ou son QR code. La fiche
 * de l'élève suit les règles d'ELV-02 (le serveur les vérifie), avec l'établissement en plus. Chaque élève s'inscrit
 * seul : il choisit une matière, puis son professeur, parmi les groupes de son niveau et les Formations ouvertes à tous
 * (et l'horaire, si ce professeur en a plusieurs). Un champ piège, invisible, arrête les robots.
 */
/** La valeur de « Autre… » dans la liste des établissements, qu'aucun vrai nom n'a. */
const AUTRE_ETABLISSEMENT = '__autre__';

@Component({
  selector: 'sh-formulaire-public',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ChampEmail, ChampTelephone],
  templateUrl: './formulaire-public.html',
  styleUrl: './formulaire-public.css',
})
export class FormulairePublic implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly code = inject(ActivatedRoute).snapshot.paramMap.get('code') ?? '';

  protected readonly formulaire = signal<FormulaireDuCentre | null>(null);
  protected readonly introuvable = signal(false);
  protected readonly envoi = signal(false);
  protected readonly erreur = signal<string | null>(null);
  /** Après « Envoyer ma demande », les champs obligatoires encore vides sont entourés de rouge. */
  protected readonly champsVerifies = signal(false);
  /** Le prénom de la demande envoyée : la page dit merci. */
  protected readonly merci = signal<string | null>(null);

  protected readonly prenom = signal('');
  protected readonly nom = signal('');
  protected readonly niveauId = signal('');
  /** Ce qui part au serveur : le nom choisi dans la liste, ou celui écrit après « Autre… ». */
  protected readonly etablissement = signal('');
  /** Ce que la liste montre : un nom de la liste, « Autre… », ou rien encore. */
  protected readonly choixDeLEtablissement = signal('');
  protected readonly AUTRE = AUTRE_ETABLISSEMENT;
  protected readonly telephoneEleve = signal('');
  protected readonly emailEleve = signal('');
  protected readonly majeur = signal(false);
  protected readonly telephoneParent = signal('');
  protected readonly emailParent = signal('');
  protected readonly siteWeb = signal('');
  /** La matière souhaitée, le professeur souhaité dans cette matière, et le groupe qui en découle. */
  protected readonly matiere = signal('');
  protected readonly professeur = signal('');
  protected readonly groupeId = signal('');

  protected readonly titre = computed(() => {
    const annee = this.formulaire()?.annee;
    return annee ? `Demande d'inscription ${annee}` : "Demande d'inscription";
  });

  /** À partir de la 7ᵉ année de base, le téléphone et l'email de l'élève sont obligatoires (ELV-02). */
  protected readonly contactsFacultatifs = computed(() => {
    const niveau = this.niveauId();
    return niveau !== '' && !aSesPropresContacts(this.formulaire()?.niveaux ?? [], niveau);
  });

  /** Les matières des groupes du niveau choisi et des Formations ouvertes à tous, dans l'ordre du serveur. */
  protected readonly matieres = computed<MatiereProposee[]>(() => {
    const niveau = this.niveauId();
    if (!niveau) return [];
    const parMatiere = new Map<string, GroupeDuFormulaire[]>();
    for (const groupe of this.formulaire()?.groupes ?? []) {
      if (groupe.niveauId !== null && groupe.niveauId !== niveau) continue;
      parMatiere.set(groupe.matiere, [...(parMatiere.get(groupe.matiere) ?? []), groupe]);
    }
    return [...parMatiere].map(([matiere, groupes]) => ({ matiere, groupes }));
  });

  async ngOnInit(): Promise<void> {
    try {
      this.formulaire.set(await firstValueFrom(this.http.get<FormulaireDuCentre>(`/api/public/inscription/${this.code}`)));
    } catch (erreur) {
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) this.introuvable.set(true);
      else this.erreur.set(messageDErreur(erreur));
    }
  }

  protected saisir(champ: WritableSignal<string>, evenement: Event): void {
    champ.set((evenement.target as HTMLInputElement).value);
  }

  /** Un nom de la liste part tel quel ; « Autre… » ouvre un champ, vide, pour écrire le nom. */
  protected choisirLEtablissement(evenement: Event): void {
    const choix = (evenement.target as HTMLSelectElement).value;
    this.choixDeLEtablissement.set(choix);
    this.etablissement.set(choix === AUTRE_ETABLISSEMENT ? '' : choix);
  }

  protected basculerMajeur(evenement: Event): void {
    this.majeur.set((evenement.target as HTMLInputElement).checked);
  }

  /** Les professeurs de la matière choisie, au niveau de l'élève, sans doublon, dans l'ordre du serveur. */
  protected readonly professeurs = computed(() => {
    const proposee = this.matieres().find((m) => m.matiere === this.matiere());
    return [...new Set((proposee?.groupes ?? []).map((g) => g.professeur))];
  });

  /** Les groupes du professeur choisi dans cette matière : un seul le plus souvent, sinon on choisit l'horaire. */
  protected readonly horaires = computed(() => {
    const proposee = this.matieres().find((m) => m.matiere === this.matiere());
    return (proposee?.groupes ?? []).filter((g) => g.professeur === this.professeur());
  });

  /** Un autre niveau : la matière et le professeur restent s'ils y sont encore proposés, sinon on les rechoisit. */
  protected choisirLeNiveau(evenement: Event): void {
    this.niveauId.set((evenement.target as HTMLSelectElement).value);
    if (!this.matieres().some((m) => m.matiere === this.matiere())) this.matiere.set('');
    if (!this.professeurs().includes(this.professeur())) this.professeur.set('');
    this.garderLeGroupe();
  }

  protected choisirLaMatiere(evenement: Event): void {
    this.matiere.set((evenement.target as HTMLSelectElement).value);
    // Un seul professeur dans cette matière : il est choisi d'office.
    const professeurs = this.professeurs();
    this.professeur.set(professeurs.length === 1 ? professeurs[0] : professeurs.includes(this.professeur()) ? this.professeur() : '');
    this.garderLeGroupe();
  }

  protected choisirLeProfesseur(evenement: Event): void {
    this.professeur.set((evenement.target as HTMLSelectElement).value);
    this.garderLeGroupe();
  }

  protected choisirLeGroupe(evenement: Event): void {
    this.groupeId.set((evenement.target as HTMLSelectElement).value);
  }

  /** Le groupe reste s'il est encore parmi les horaires du professeur ; sinon c'est le premier, celui que la liste montre. */
  private garderLeGroupe(): void {
    const horaires = this.horaires();
    this.groupeId.set(horaires.some((g) => g.id === this.groupeId()) ? this.groupeId() : (horaires[0]?.id ?? ''));
  }

  /** « Samedi 10h → 12h · Révision ». */
  protected horaire(groupe: GroupeDuFormulaire): string {
    const horaire = groupe.creneaux.map((c) => `${c.jour} ${heure(c.debut)} → ${heure(c.fin)}`).join(', ');
    const type = groupe.type === 'Revision' ? ' · Révision' : groupe.type === 'Formation' ? ' · Formation' : '';
    return `${horaire}${type}`;
  }

  protected async envoyer(evenement: Event): Promise<void> {
    evenement.preventDefault();
    if (this.envoi()) return;
    this.champsVerifies.set(true);
    // Le serveur le dit aussi ; ici, la personne le sait sans attendre.
    if (this.niveauId() !== '' && this.matieres().length > 0 && !this.groupeId()) {
      this.erreur.set(this.matiere() === '' ? 'Choisissez la matière souhaitée' : 'Choisissez le professeur souhaité');
      return;
    }
    const ouNull = (valeur: string) => (valeur.trim() === '' ? null : valeur.trim());
    const demande = {
      prenom: ouNull(this.prenom()),
      nom: ouNull(this.nom()),
      niveauId: ouNull(this.niveauId()),
      majeur: this.majeur(),
      telephoneParent: ouNull(this.telephoneParent()),
      emailParent: ouNull(this.emailParent()),
      telephoneEleve: ouNull(this.telephoneEleve()),
      emailEleve: ouNull(this.emailEleve()),
      etablissement: ouNull(this.etablissement()),
      groupes: this.groupeId() ? [this.groupeId()] : [],
      siteWeb: ouNull(this.siteWeb()),
    };
    this.envoi.set(true);
    this.erreur.set(null);
    try {
      const recue = await firstValueFrom(this.http.post<{ prenom: string }>(`/api/public/inscription/${this.code}`, demande));
      this.merci.set(recue.prenom);
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    } finally {
      this.envoi.set(false);
    }
  }
}
