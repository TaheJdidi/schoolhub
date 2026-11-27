import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ETATS_DE_L_ABONNEMENT, EtatDeLAbonnement } from '../../socle/abonnement';
import { grouperUnTelephone } from '../../socle/champs-de-contact';
import { messageDErreur } from '../../socle/erreurs';
import { formaterMoment } from './api';

interface CentreEnAccesSupport {
  nom: string;
  code: string;
  telephone: string | null;
  email: string | null;
  etat: EtatDeLAbonnement;
  supportJusquAu: string;
  eleves: number;
  groupesOuverts: number;
  professeurs: number;
}

interface CompteEnAccesSupport {
  prenom: string;
  nom: string;
  email: string;
  role: 'Proprietaire' | 'Secretaire';
  etat: 'Invite' | 'Actif' | 'Desactive';
}

const ROLES = { Proprietaire: 'Propriétaire', Secretaire: 'Secrétaire' } as const;
const ETATS_DES_COMPTES = { Invite: 'Invité', Actif: 'Actif', Desactive: 'Désactivé' } as const;

/**
 * EDI-07 : le centre vu par l'Éditeur pendant l'accès support, en lecture seule : son accueil (identité et chiffres) et
 * ses comptes. Chaque page ouverte va au journal du centre. Sans autorisation, le serveur répond 403 et la page le dit.
 */
@Component({
  selector: 'sh-vue-support',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="pagehead">
      <div>
        <h1 class="h1">Accès support{{ centre() ? ' · ' + centre()!.nom : '' }}</h1>
        <p class="sub">Lecture seule. Chaque page consultée est notée au journal du centre.</p>
      </div>
      <a class="btn ghost" [routerLink]="['/editeur/centres', id()]">Retour à la fiche</a>
    </div>

    @if (message(); as message) {
      <p class="note bad bandeau" role="alert">{{ message }}</p>
    }

    @if (centre(); as centre) {
      <p class="note ok bandeau">Autorisé par le propriétaire jusqu'au {{ moment(centre.supportJusquAu) }}.</p>
      <section class="card bloc" aria-label="Accueil du centre">
        <h2 class="h2">Le centre</h2>
        <dl class="infos">
          <div><dt>Nom</dt><dd>{{ centre.nom }}</dd></div>
          <div><dt>État de l'abonnement</dt><dd>{{ etat(centre.etat) }}</dd></div>
          <div><dt>Lien public</dt><dd>/inscription/{{ centre.code }}</dd></div>
          <div><dt>Téléphone</dt><dd class="num">{{ telephone(centre.telephone) }}</dd></div>
          <div><dt>Email</dt><dd>{{ centre.email }}</dd></div>
          <div><dt>Élèves</dt><dd class="num">{{ centre.eleves }}</dd></div>
          <div><dt>Groupes ouverts</dt><dd class="num">{{ centre.groupesOuverts }}</dd></div>
          <div><dt>Professeurs</dt><dd class="num">{{ centre.professeurs }}</dd></div>
        </dl>
      </section>
    }

    @if (comptes(); as comptes) {
      <section class="card bloc" aria-label="Comptes du centre">
        <h2 class="h2">Comptes</h2>
        <ul class="liste">
          @for (compte of comptes; track compte.email) {
            <li>
              <b>{{ compte.prenom }} {{ compte.nom }}</b>
              <small>{{ role(compte) }} · {{ compte.email }} · {{ etatDuCompte(compte) }}</small>
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: `
    .bandeau { margin: 0 0 14px; }
    .h2 { font-family: var(--font-display); font-size: 16px; font-weight: 600; margin: 0; }
    .bloc { display: grid; gap: 12px; padding: 16px; margin-bottom: 16px; max-width: 920px; }
    .infos { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px 16px; margin: 0; }
    .infos dt { font-size: 12.5px; color: var(--ink-2); }
    .infos dd { margin: 0; }
    .liste { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
    .liste li { display: grid; gap: 2px; }
    .liste small { color: var(--ink-2); overflow-wrap: anywhere; }
    @media (max-width: 600px) { .infos { grid-template-columns: minmax(0, 1fr); } }
  `,
})
export class VueSupport implements OnInit {
  private readonly http = inject(HttpClient);

  readonly id = input.required<string>();

  protected readonly centre = signal<CentreEnAccesSupport | null>(null);
  protected readonly comptes = signal<CompteEnAccesSupport[] | null>(null);
  protected readonly message = signal<string | null>(null);

  protected readonly moment = formaterMoment;
  protected readonly etat = (etat: EtatDeLAbonnement) => ETATS_DE_L_ABONNEMENT[etat].libelle;
  protected readonly role = (compte: CompteEnAccesSupport) => ROLES[compte.role];
  protected readonly etatDuCompte = (compte: CompteEnAccesSupport) => ETATS_DES_COMPTES[compte.etat];
  protected readonly telephone = (chiffres: string | null) => (chiffres ? grouperUnTelephone(chiffres) : '');

  ngOnInit(): void {
    void this.charger();
  }

  private async charger(): Promise<void> {
    const adresse = `/api/editeur/centres/${this.id()}/support`;
    try {
      this.centre.set(await firstValueFrom(this.http.get<CentreEnAccesSupport>(`${adresse}/accueil`)));
      this.comptes.set(await firstValueFrom(this.http.get<CompteEnAccesSupport[]>(`${adresse}/comptes`)));
    } catch (erreur) {
      this.message.set(messageDErreur(erreur));
    }
  }
}
