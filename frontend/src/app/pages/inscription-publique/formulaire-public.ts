import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal, WritableSignal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { aSesPropresContacts } from '../eleves/eleve';
import { heure } from '../groupes/groupe';
import { FormulaireDuCentre, GroupeDuFormulaire } from './formulaire-du-centre';

/** Une matière proposée pour le niveau choisi, avec ses groupes. */
interface MatiereProposee {
  matiere: string;
  groupes: GroupeDuFormulaire[];
}

/**
 * DEM-01 : le formulaire public d'inscription d'un centre, ouvert sans compte depuis son lien ou son QR code. La fiche
 * de l'élève suit les règles d'ELV-02 (le serveur les vérifie) ; pour chaque matière cochée, l'élève choisit son groupe
 * préféré parmi ceux de son niveau et les Formations ouvertes à tous. Un champ piège, invisible, arrête les robots.
 */
@Component({
  selector: 'sh-formulaire-public',
  changeDetection: ChangeDetectionStrategy.OnPush,
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
  /** Le prénom de la demande envoyée : la page dit merci. */
  protected readonly merci = signal<string | null>(null);

  protected readonly prenom = signal('');
  protected readonly nom = signal('');
  protected readonly niveauId = signal('');
  protected readonly etablissement = signal('');
  protected readonly telephoneEleve = signal('');
  protected readonly emailEleve = signal('');
  protected readonly majeur = signal(false);
  protected readonly telephoneParent = signal('');
  protected readonly emailParent = signal('');
  protected readonly amis = signal('');
  protected readonly siteWeb = signal('');
  /** Le groupe préféré de chaque matière cochée. */
  protected readonly choix = signal<Readonly<Record<string, string>>>({});

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

  protected basculerMajeur(evenement: Event): void {
    this.majeur.set((evenement.target as HTMLInputElement).checked);
  }

  /** Un autre niveau : les matières qui n'y sont plus proposées sont décochées. */
  protected choisirLeNiveau(evenement: Event): void {
    this.niveauId.set((evenement.target as HTMLSelectElement).value);
    const proposees = new Set(this.matieres().map((m) => m.matiere));
    this.choix.update((choix) => Object.fromEntries(Object.entries(choix).filter(([matiere]) => proposees.has(matiere))));
  }

  /** Cochée, une matière propose son premier groupe ; décochée, elle sort de la demande. */
  protected cocher(matiere: MatiereProposee, evenement: Event): void {
    const cochee = (evenement.target as HTMLInputElement).checked;
    this.choix.update((choix) => {
      const { [matiere.matiere]: _retiree, ...autres } = choix;
      return cochee ? { ...autres, [matiere.matiere]: matiere.groupes[0].id } : autres;
    });
  }

  protected choisirLeGroupe(matiere: string, evenement: Event): void {
    const groupe = (evenement.target as HTMLSelectElement).value;
    this.choix.update((choix) => ({ ...choix, [matiere]: groupe }));
  }

  /** « Karim Ben Salah · Samedi 10h → 12h · Révision ». */
  protected libelle(groupe: GroupeDuFormulaire): string {
    const horaire = groupe.creneaux.map((c) => `${c.jour} ${heure(c.debut)} → ${heure(c.fin)}`).join(', ');
    const type = groupe.type === 'Revision' ? ' · Révision' : groupe.type === 'Formation' ? ' · Formation' : '';
    return `${groupe.professeur} · ${horaire}${type}`;
  }

  protected async envoyer(evenement: Event): Promise<void> {
    evenement.preventDefault();
    if (this.envoi()) return;
    const ouNull = (valeur: string) => (valeur.trim() === '' ? null : valeur.trim());
    const choix = this.choix();
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
      // Dans l'ordre des matières de la page.
      groupes: this.matieres().filter((m) => choix[m.matiere]).map((m) => choix[m.matiere]),
      amis: ouNull(this.amis()),
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
