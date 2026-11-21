import { formatDate } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, signal, untracked, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { messageDErreur } from '../../socle/erreurs';
import { FRANCAIS } from '../../socle/francais';
import { ecrireUnMontant } from '../../socle/montants';
import { InscriptionDeLEleve } from '../inscriptions/inscription';
import { CarteDuCalendrier } from '../seances/carte-du-calendrier';
import { Icone } from '../../socle/icone';
import { NomDIcone } from '../../socle/icones';
import { PageDeLAppel } from '../seances/page-de-l-appel';
import { EncaissementDuGroupe } from '../caisse/recu';
import { BadgeDuGroupeComponent } from '../caisse/paiement';
import { dateDuJourLocale, eleves, etatDeLaSeance, FicheDuGroupe, heure, TYPES } from './groupe';


/**
 * Le prix selon le type : « 60,000 TND par Mois » ; « 50,000 TND les 3 séances, ou 16,667 TND la séance » ;
 * « 300,000 TND, en une fois ou en 3 tranches ».
 */
function prixDuGroupe(fiche: FicheDuGroupe): string {
  const total = ecrireUnMontant(fiche.prixTotal);
  switch (fiche.type) {
    case 'Etude':
      return `${ecrireUnMontant(fiche.prixDuMois)} TND par Mois`;
    case 'Revision': {
      const nombre = fiche.nombreDeSeances ?? 1;
      const seance = Math.round(((fiche.prixTotal ?? 0) / nombre) * 1000) / 1000;
      return `${total} TND les ${nombre} séances, ou ${ecrireUnMontant(seance)} TND la séance`;
    }
    case 'Formation': {
      const tranches = fiche.tranches.length;
      if (tranches === 0) return `${total} TND, en une fois`;
      return `${total} TND, en une fois ou en ${tranches} ${tranches > 1 ? 'tranches' : 'tranche'}`;
    }
  }
}

/**
 * GRP-04 à GRP-07 : la fiche d'un groupe. Une Étude montre ses Mois ; une Révision ou une Formation, ses séances
 * numérotées (avec leur prix pour une Révision) et les tranches d'une Formation. « Modifier » et « Fermer le groupe »
 * (GRP-08) se font depuis la carte du groupe, sur la page du professeur (FenetreDuGroupe).
 */
@Component({
  selector: 'sh-page-du-groupe',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, CarteDuCalendrier, BadgeDuGroupeComponent, PageDeLAppel, Icone],
  templateUrl: './page-du-groupe.html',
  styleUrl: './groupes.css',
})
export class PageDuGroupe implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  protected readonly eleves = eleves;
  protected readonly types = TYPES;
  /** Les élèves inscrits, par nom puis prénom (ELV-03). */
  protected readonly inscriptions = signal<InscriptionDeLEleve[]>([]);
  protected readonly heure = heure;
  protected readonly montant = ecrireUnMontant;

  protected readonly fiche = signal<FicheDuGroupe | null>(null);
  /** La carte du calendrier, sous la bande : « + Ajouter » et le nouvel horaire y ouvrent leur formulaire. */
  protected readonly calendrier = viewChild(CarteDuCalendrier);
  /** L'appel de la séance choisie : les boutons de sa puce y ouvrent Déplacer, Annuler ou Remplacer. */
  protected readonly appelChoisi = viewChild(PageDeLAppel);
  /** Un groupe fermé ou terminé ne change plus. */
  protected readonly modifiable = computed(() => !!this.fiche() && !this.fiche()!.fermeLe && !this.fiche()!.termine);

  /**
   * SEA-03 et SEA-04 : les boutons de la puce choisie. Une séance qui a son appel ne change plus ; une séance annulée se
   * remplace (sauf une supplémentaire, ou une déjà remplacée) ; sinon, elle se déplace ou s'annule.
   */
  protected actionsDe(seance: { appelFait?: boolean; annulee?: boolean; nature?: string; remplaceeLe?: string | null }): ('deplacer' | 'annuler' | 'remplacer')[] | null {
    if (seance.annulee) return seance.nature === 'Supplementaire' || seance.remplaceeLe ? null : ['remplacer'];
    if (seance.appelFait) return null;
    return ['deplacer', 'annuler'];
  }
  protected readonly introuvable = signal(false);
  protected readonly erreur = signal<string | null>(null);

  /** Les pastilles du sous-titre : la matière, le niveau, le professeur, puis « Étude, 60,000 TND par Mois ». */
  protected readonly pastilles = computed((): { icone: NomDIcone; texte: string }[] => {
    const fiche = this.fiche();
    if (!fiche) return [];
    return [
      { icone: 'book-open' as const, texte: fiche.matiere },
      ...(fiche.niveau ? [{ icone: 'graduation-cap' as const, texte: fiche.niveau.nom }] : []),
      { icone: 'contact' as const, texte: `${fiche.professeur.prenom} ${fiche.professeur.nom}` },
      { icone: 'wallet' as const, texte: `${TYPES[fiche.type]}, ${prixDuGroupe(fiche)}` },
    ];
  });

  /** « Samedi 10h → 12h », ou « Lundi 17h → 19h, Jeudi 17h → 19h ». */
  protected readonly horaire = computed(
    () => this.fiche()?.creneaux.map((c) => `${c.jour} ${heure(c.debut)} → ${heure(c.fin)}`).join(', ') ?? '',
  );

  /** SEA-02 : le jour, pour l'état des séances. */
  protected readonly aujourdhui = dateDuJourLocale();

  /** SEA-02 : « à venir », « appel fait » ou « appel à faire ». */
  protected readonly etat = (seance: { date: string; appelFait?: boolean }) => etatDeLaSeance(seance, this.aujourdhui);

  /** SEA-02 : le Mois en cours d'une Étude, le dernier qui a commencé. */
  protected readonly moisEnCours = computed(() => this.fiche()?.mois.filter((m) => m.commence).at(-1)?.numero ?? null);

  /** SEA-02 : « Séance 2 sur 3 » pour une Révision ou une Formation, d'après les séances déjà passées ou du jour. */
  protected readonly avancement = computed(() => {
    const fiche = this.fiche();
    if (!fiche || fiche.type === 'Etude' || fiche.seances.length === 0) return '';
    const passees = fiche.seances.filter((s) => s.date <= this.aujourdhui);
    const total = fiche.nombreDeSeances ?? fiche.seances.at(-1)!.numero;
    return passees.length === 0 ? `Pas encore commencé · ${total} séances` : `Séance ${passees.at(-1)!.numero} sur ${total}`;
  });

  /** « séances 9 à 12 » : les numéros des séances d'un Mois. */
  // ----- La bande des séances et l'appel de la séance choisie (prototype, écran 3) -----

  /**
   * Toutes les séances du groupe, avec leur Mois pour une Étude. Les séances supplémentaires (SEA-05) s'y glissent à leur
   * date : pour une Étude, dans le Mois en cours à cette date, même si elles n'y comptent pas.
   */
  private readonly toutesLesSeances = computed(() => {
    const fiche = this.fiche();
    if (!fiche) return [];
    const prevues =
      fiche.type === 'Etude'
        ? fiche.mois.flatMap((mois) => mois.seances.map((seance) => ({ ...seance, mois: mois.numero as number | null })))
        : fiche.seances.map((seance) => ({ ...seance, mois: null as number | null }));
    const moisAvecSeances = fiche.mois.filter((m) => m.seances.length > 0);
    const moisDe = (date: string): number | null =>
      fiche.type !== 'Etude' ? null : (moisAvecSeances.filter((m) => m.seances[0].date <= date).at(-1) ?? moisAvecSeances[0])?.numero ?? null;
    // Les séances supplémentaires (SEA-05) et les séances annulées (SEA-03), barrées, à leur date.
    const horsCompte = (fiche.horsCompte ?? [])
      .filter((s) => s.annulee || s.nature === 'Supplementaire')
      .map((s) => ({ id: s.id, date: s.date, debut: s.debut, fin: s.fin, appelFait: s.appelFait, nature: s.nature, mois: moisDe(s.date),
        annulee: s.annulee, remplaceeLe: s.remplaceeLe }));
    if (horsCompte.length === 0) return prevues.map((s) => ({ ...s, annulee: false, remplaceeLe: null as string | null }));
    return [...prevues.map((s) => ({ ...s, annulee: false, remplaceeLe: null as string | null })), ...horsCompte]
      .sort((a, b) => (a.date + a.debut).localeCompare(b.date + b.debut));
  });
  /** `?seance=…` (depuis Aujourd'hui) ouvre l'appel de cette séance dans la fiche du groupe. */
  private readonly seanceDemandee = signal<string | null>(this.route.snapshot.queryParamMap.get('seance'));
  /** La séance choisie : celle qu'on a touchée, sinon celle du jour, sinon la dernière passée, sinon la première. */
  protected readonly seanceChoisie = computed(() => {
    const seances = this.toutesLesSeances();
    const demandee = this.seanceDemandee();
    if (demandee && seances.some((s) => s.id === demandee)) return demandee;
    const ontLieu = seances.filter((s) => !s.annulee);
    return (
      ontLieu.find((s) => s.date === this.aujourdhui) ??
      ontLieu.filter((s) => s.date < this.aujourdhui).at(-1) ??
      ontLieu[0] ??
      seances[0]
    )?.id ?? null;
  });
  protected readonly seanceChoisieEnListe = computed(() => {
    const id = this.seanceChoisie();
    return id ? [id] : [];
  });
  /** Le Mois montré dans la bande : celui de la séance choisie (une Étude) ; `null` montre toutes les séances. */
  protected readonly moisAffiche = computed(() => this.toutesLesSeances().find((s) => s.id === this.seanceChoisie())?.mois ?? null);
  protected readonly seancesAffichees = computed(() => {
    const mois = this.moisAffiche();
    return mois === null ? this.toutesLesSeances() : this.toutesLesSeances().filter((s) => s.mois === mois);
  });
  protected readonly moisPrecedent = computed(() => {
    const mois = this.moisAffiche();
    return mois !== null && this.fiche()?.mois.some((m) => m.numero === mois - 1) ? mois - 1 : null;
  });
  protected readonly moisSuivant = computed(() => {
    const mois = this.moisAffiche();
    return mois !== null && this.fiche()?.mois.some((m) => m.numero === mois + 1) ? mois + 1 : null;
  });

  protected choisirLaSeance(id: string): void {
    this.seanceDemandee.set(id);
  }

  protected choisirLeMois(numero: number): void {
    const premiere = this.fiche()?.mois.find((m) => m.numero === numero)?.seances[0];
    if (premiere) this.seanceDemandee.set(premiere.id);
  }

  /** « appel fait », « aujourd'hui », « à venir » ou « appel à faire ». */
  protected libelleDeLaPuce(seance: { date: string; appelFait?: boolean; nature?: string; annulee?: boolean; remplaceeLe?: string | null }): string {
    if (seance.annulee) return seance.remplaceeLe ? `annulée · remplacée le ${this.jourDeSeance(seance.remplaceeLe).split(' ')[1]}` : 'annulée';
    const etat = this.etat(seance);
    const libelle = seance.date === this.aujourdhui && etat !== 'appel fait' ? "aujourd'hui" : etat;
    if (seance.nature === 'Supplementaire') return `supplémentaire · ${libelle}`;
    if (seance.nature === 'Ajoutee') return `ajoutée · ${libelle}`;
    return seance.nature === 'Remplacement' ? `remplacement · ${libelle}` : libelle;
  }

  /** « séances 1 à 4 » pour la puce du Mois d'avant. */
  protected numerosDuMoisAffiche(numero: number): string {
    return this.numerosDuMois(numero, this.fiche()?.mois.find((m) => m.numero === numero)?.seances.length ?? 4);
  }

  /** « à venir » ou « en cours » pour la puce du Mois d'après. */
  protected etatDuMois(numero: number): string {
    return this.fiche()?.mois.find((m) => m.numero === numero)?.commence ? 'commencé' : 'à venir';
  }

  /** Le nombre d'élèves de cette couleur (CAI-01), les inscriptions arrêtées à part. */
  protected compte(couleur: string): number {
    return this.inscriptions().filter((i) => !i.arreteLe && i.couleur === couleur).length;
  }

  protected numerosDuMois(numero: number, nombre: number): string {
    const premiere = (numero - 1) * 4 + 1;
    return nombre === 1 ? `séance ${premiere}` : `séances ${premiere} à ${premiere + nombre - 1}`;
  }

  private get id(): string {
    return this.route.snapshot.paramMap.get('id') ?? '';
  }

  async ngOnInit(): Promise<void> {
    try {
      const [fiche, inscriptions] = await Promise.all([
        firstValueFrom(this.http.get<FicheDuGroupe>(`/api/groupes/${this.id}`)),
        firstValueFrom(this.http.get<InscriptionDeLEleve[]>('/api/inscriptions', { params: { groupe: this.id } })),
      ]);
      this.fiche.set(fiche);
      this.inscriptions.set(inscriptions);
      // Ce que chacun doit se charge dès que la fiche est là (l'effet du constructeur), puis à chaque séance choisie.
    } catch (erreur) {
      if (erreur instanceof HttpErrorResponse && erreur.status === 404) this.introuvable.set(true);
      else this.erreur.set(messageDErreur(erreur));
    }
  }

  /** Ce que chaque élève doit dans le groupe, pour l'appel (prototype, écran 3) ; sans lui, l'appel reste utile. */
  private readonly encaissement = signal<EncaissementDuGroupe | null>(null);

  /**
   * La date de la séance choisie si elle est à venir : la colonne « Paiement » montre ce qui sera dû ce jour-là (le Mois 3
   * à sa première séance), pas seulement ce qui est dû aujourd'hui. `null` : aujourd'hui.
   */
  protected readonly paiementAu = computed(() => {
    const date = this.toutesLesSeances().find((s) => s.id === this.seanceChoisie())?.date ?? null;
    return date && date > this.aujourdhui ? date : null;
  });
  /** Seule la dernière réponse compte : on change de séance plus vite que le serveur ne répond. */
  private chargement = 0;

  constructor() {
    effect(() => {
      const au = this.paiementAu();
      if (this.fiche()) untracked(() => void this.chargerLesPaiements(au));
    });
  }

  private async chargerLesPaiements(au = this.paiementAu()): Promise<void> {
    const numero = ++this.chargement;
    try {
      const encaissement = await firstValueFrom(
        this.http.get<EncaissementDuGroupe>(`/api/groupes/${this.id}/encaissement`, au ? { params: { au } } : {}),
      );
      if (numero === this.chargement) this.encaissement.set(encaissement);
    } catch {
      if (numero === this.chargement) this.encaissement.set(null);
    }
  }

  /** Ceux qui doivent quelque chose à ce jour : la colonne « Paiement » de l'appel. */
  protected readonly aPayer = computed(() => this.encaissement()?.aPayer ?? []);

  /** Après « Payer les cochés » : ce que chacun doit, et les badges, se recalculent. */
  protected apresLePaiement(): void {
    void this.chargerLesPaiements();
  }

  /** Par élève : sa couleur et « À payer : Mois 1, Mois 2 · 110,000 TND », « À payer : reste du Mois 3 · 20,000 TND (50,000 déjà payés) », ou « Payé ». */
  protected readonly paiements = computed(() => {
    const paiements: Record<string, { couleur: string | null; texte: string }> = {};
    const encaissement = this.encaissement();
    for (const inscription of this.inscriptions()) {
      paiements[inscription.eleve.id] = { couleur: inscription.couleur ?? null, texte: inscription.couleur === 'AJour' ? 'Payé' : '' };
    }
    for (const eleve of encaissement?.aJour ?? []) paiements[eleve.id] = { couleur: 'AJour', texte: 'Payé' };
    for (const eleve of encaissement?.aPayer ?? []) {
      const sommes = eleve.sommes
        .map((s) => {
          if (!s.inscriptionId) return 'assurance';
          const libelle = s.libelle.replace(/\s*\(.*\)$/, '');
          return s.avance ? `reste du ${libelle}` : libelle;
        })
        .join(', ');
      const avance = eleve.sommes.reduce((total, s) => total + (s.avance ?? 0), 0);
      paiements[eleve.eleveId] = {
        couleur: eleve.couleur,
        texte: `À payer : ${sommes} · ${ecrireUnMontant(eleve.total)} TND${avance > 0 ? ` (${ecrireUnMontant(avance)} déjà payés)` : ''}`,
      };
    }
    return paiements;
  });

  /** « séance 2 du Mois 2 » (une Étude) ou « séance 3 sur 10 ». */
  protected readonly libelleDeLaSeance = computed(() => {
    const fiche = this.fiche();
    const id = this.seanceChoisie();
    if (!fiche || !id) return null;
    if (fiche.horsCompte?.some((s) => s.id === id && s.annulee)) return 'séance annulée';
    if (fiche.horsCompte?.some((s) => s.id === id && s.nature === 'Supplementaire')) return 'séance supplémentaire, offerte, hors des Mois';
    if (fiche.type === 'Etude') {
      const mois = fiche.mois.find((m) => m.seances.some((s) => s.id === id));
      return mois ? `séance ${mois.seances.findIndex((s) => s.id === id) + 1} du Mois ${mois.numero}` : null;
    }
    const seance = fiche.seances.find((s) => s.id === id);
    return seance ? `séance ${seance.numero} sur ${fiche.nombreDeSeances ?? fiche.seances.length}` : null;
  });

  /** SEA-03 à SEA-06 : après un changement du calendrier, les Mois et les séances se recalculent. */
  protected async recharger(): Promise<void> {
    try {
      this.fiche.set(await firstValueFrom(this.http.get<FicheDuGroupe>(`/api/groupes/${this.id}`)));
    } catch (erreur) {
      this.erreur.set(messageDErreur(erreur));
    }
  }

  /** « 18/09/2027 ». */
  protected date(texte: string): string {
    return formatDate(texte, 'dd/MM/yyyy', FRANCAIS);
  }

  /** « sam. 18/09 ». */
  protected jourDeSeance(texte: string): string {
    return formatDate(texte, 'EEE dd/MM', FRANCAIS);
  }

}
