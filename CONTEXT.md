# SchoolHub

Plateforme de gestion pour les centres d'étude tunisiens : un centre y gère ses élèves, ses professeurs, ses groupes, les séances, la présence et les paiements, ainsi que sa bibliothèque, ses achats et son personnel.

## Language

### Le centre et ses utilisateurs

**Centre** :
Établissement client qui utilise SchoolHub pour gérer son activité. Ses données sont séparées de celles des autres centres.
_Avoid_ : école, établissement

**Propriétaire** :
Personne qui possède le centre, seule ou avec des associés : un centre peut avoir plusieurs propriétaires, tous avec les mêmes droits, et un propriétaire ne possède qu'un centre. Il a accès à tout, y compris aux ressources humaines, aux comptes des employés et au journal.
_Avoid_ : super admin, gérant, directeur

**Secrétaire** :
Employé qui gère le quotidien du centre ; tout employé qui se connecte à SchoolHub est secrétaire. Il n'a accès ni aux ressources humaines, ni aux comptes des employés, ni à la rémunération des professeurs, ni au tableau de bord de rentabilité, ni au journal.
_Avoid_ : admin, réceptionniste, bibliothécaire

**Professeur** :
Personne qui anime un ou plusieurs groupes au centre. Il n'a pas de compte sur SchoolHub.
_Avoid_ : enseignant, prof, formateur

**Employé** :
Personne qui travaille pour le centre, hors professeurs : secrétaire, agent de ménage, gardien… Elle a une fiche dans les ressources humaines, et un compte seulement si elle est secrétaire.
_Avoid_ : salarié, personnel

**Compte désactivé** :
Compte d'une personne qui a quitté le centre. Il ne permet plus de se connecter, mais son nom reste sur les paiements, les reçus et les appels qu'elle a faits.
_Avoid_ : compte supprimé

**Journal** :
Historique des actions qui touchent à l'argent ou à la présence : qui a fait quoi, et quand. Seul le propriétaire le consulte.
_Avoid_ : log, audit, historique

**Élève** :
Personne inscrite dans un ou plusieurs groupes du centre, ou qui vient seulement à la bibliothèque.
_Avoid_ : étudiant, apprenant

**Parent** :
Personne responsable d'un élève, que le centre contacte par WhatsApp. Chaque élève a un parent enregistré, sauf un élève majeur, pour qui il est facultatif.
_Avoid_ : tuteur, responsable légal, famille

### L'offre d'enseignement

**Groupe** :
Ensemble d'élèves qui suivent les séances d'un même professeur, en général sur un horaire hebdomadaire, par exemple « Groupe Lundi 8h→10h ». Chaque groupe a un type (Étude, Révision ou Formation), une matière et un niveau, facultatif pour une Formation.
_Avoid_ : classe, section, programme

**Groupe fermé** :
Groupe arrêté en cours d'année. Il n'apparaît plus dans la liste des groupes, mais son historique (séances, présences, paiements, reçus) reste consultable.
_Avoid_ : groupe supprimé, groupe effacé

**Type de groupe** :
Catégorie d'un groupe : Étude, Révision ou Formation. Elle détermine comment ses élèves peuvent payer.
_Avoid_ : section, espace, catégorie

**Étude** :
Type de groupe qui dure toute l'année scolaire.
_Avoid_ : espace étude, cours annuel

**Révision** :
Type de groupe court, de quelques séances, par exemple avant un examen.
_Avoid_ : espace révision, stage

**Formation** :
Type de groupe dont le prix et le nombre de séances sont fixés à l'avance, par exemple une formation en informatique.
_Avoid_ : espace formation, atelier

**Matière** :
Discipline enseignée dans un groupe, par exemple les mathématiques, la physique ou l'informatique.
_Avoid_ : sujet, cours

**Niveau** :
Classe scolaire que vise un groupe, par exemple 9ᵉ année de base ou 4ᵉ année secondaire, section mathématiques.
_Avoid_ : classe, année

**Mode de tarification** :
Manière dont un élève paie son inscription. Le type de groupe fixe les choix possibles : chaque Mois pour une Étude ; à la séance ou en une fois pour une Révision ; en une fois ou en tranches pour une Formation.
_Avoid_ : type de paiement, formule

**Séance** :
Occurrence datée d'un cours, pour un groupe.
_Avoid_ : cours, leçon, session

**Mois** :
Lot de 4 séances d'un groupe d'Étude, numéroté depuis la rentrée (Mois 1, Mois 2…) et commun à tous les élèves du groupe. C'est l'unité de paiement d'une Étude ; il ne correspond pas au mois du calendrier.
_Avoid_ : cycle, période, mois calendaire

**Séance annulée** :
Séance prévue qui n'a pas lieu (professeur absent, jour férié, vacances). Elle ne compte pas dans le Mois.
_Avoid_ : séance supprimée, séance reportée

**Séance de remplacement** :
Séance ajoutée à une autre date pour remplacer une séance annulée. Elle compte à sa place dans le Mois.
_Avoid_ : séance de rattrapage

**Séance supplémentaire** :
Séance ajoutée en plus des séances prévues, par exemple avant un examen. Elle est offerte et ne compte pas dans le Mois.
_Avoid_ : séance bonus, séance de rattrapage

**Bibliothèque** :
Salle où les élèves viennent réviser seuls, en payant une entrée à la journée. Elle est ouverte aux élèves des groupes comme à des personnes de l'extérieur.
_Avoid_ : salle de révision, salle d'étude

**Entrée à la journée** :
Droit de venir à la bibliothèque pour une journée, payé en arrivant, au même prix pour tous. La secrétaire note l'heure d'arrivée, et l'heure de départ si elle la connaît.
_Avoid_ : ticket, abonnement, séance

### Le suivi des élèves

**Demande d'inscription** :
Formulaire envoyé depuis le lien public du centre par un élève qui veut s'inscrire. Ce n'est pas encore une inscription : la secrétaire enregistre ensuite l'élève à la main.
_Avoid_ : pré-inscription, candidature

**Inscription** :
Rattachement d'un élève à un groupe. Elle reçoit les paiements. Un élève peut avoir plusieurs inscriptions, dans des groupes de professeurs différents.
_Avoid_ : abonnement, adhésion

**Tranche** :
Partie du prix d'une Formation, due à un moment fixé à l'avance, quand l'élève ne paie pas tout au début.
_Avoid_ : échéance, acompte

**Prix spécial** :
Prix accordé à un élève pour une inscription précise, à la place du prix normal du groupe, pour une raison particulière (famille en difficulté, proche du centre…). Il peut être nul : l'élève ne paie alors rien.
_Avoid_ : remise, réduction, geste

**Assurance** :
Somme annuelle payée au centre par les élèves des groupes d'Étude. Chaque centre fixe son montant.
_Avoid_ : cotisation

**Paiement** :
Somme en espèces encaissée par le centre auprès d'un élève, pour une inscription, pour l'assurance ou pour une entrée à la bibliothèque.
_Avoid_ : versement, règlement

**Impayé** :
Somme qu'un élève devait payer et n'a pas encore payée. Elle reste une perte pour le centre seul : le professeur reçoit sa part quand même.
_Avoid_ : dette, retard

**Présence** :
Statut d'un élève à une séance : présent ou absent.
_Avoid_ : pointage, assiduité

**Appel** :
Relevé de la présence des élèves à une séance, fait sur la plateforme ou sur une feuille d'appel. Un élève est présent sauf s'il est marqué absent.
_Avoid_ : pointage, contrôle

**Feuille d'appel** :
Liste imprimée depuis la plateforme des élèves d'un groupe et des séances d'une période, par exemple les 4 séances du Mois 2, pour faire l'appel sur papier.
_Avoid_ : cahier d'appel, liste de présence

**Rattrapage** :
Participation d'un élève à une séance d'un autre groupe du même professeur, de la même matière et du même niveau, à la place d'une séance de son groupe qu'il manque. L'élève continue de payer dans son propre groupe.
_Avoid_ : remplacement, changement de séance

**Changement de groupe** :
Passage définitif d'un élève d'un groupe à un autre du même type. Les séances déjà payées et non suivies dans l'ancien groupe sont déduites de ce qu'il doit dans le nouveau.
_Avoid_ : transfert, mutation

### La caisse

**Encaissement groupé** :
Enregistrement en une seule action des paiements de plusieurs élèves d'un groupe pour le Mois en cours, avec un reçu par élève.
_Avoid_ : paiement de masse

**Reçu** :
Preuve d'un paiement, numérotée à la suite pour tout le centre, jamais supprimée ni modifiée. Il se remet imprimé, par un message WhatsApp qui en reprend le contenu, ou par email.
_Avoid_ : facture, ticket, bon

**Reçu annulé** :
Reçu qui reste visible avec la mention « annulé » et un motif. La secrétaire peut annuler le jour même, avant sa clôture ; ensuite, seul le propriétaire le peut.
_Avoid_ : reçu supprimé

**Caisse** :
Espèces encaissées par une secrétaire dans la journée, moins les achats qu'elle a payés en espèces (sorties de caisse).
_Avoid_ : tiroir, trésorerie

**Clôture de caisse** :
Comptage des espèces que fait chaque secrétaire le soir. La plateforme compare le montant compté aux espèces attendues et garde l'écart ; le propriétaire marque « argent reçu » quand il récupère les espèces.
_Avoid_ : fermeture, arrêté de caisse

### La rémunération des professeurs

**Montant du groupe** :
Somme de ce que les élèves d'un groupe doivent pour un Mois, ou pour toute une Révision ou Formation, prix spéciaux compris, qu'ils aient payé ou non. Le professeur reçoit ce montant moins la part du centre.
_Avoid_ : chiffre, recette, encaissé

**Part du centre** :
Pourcentage que le centre garde sur le montant du groupe, par exemple 5 %. Le reste revient au professeur.
_Avoid_ : commission, marge

**Avance** :
Somme versée à un professeur avant qu'il ait droit à sa part. Elle est déduite de son paiement suivant.
_Avoid_ : prêt, acompte

### La gestion du centre

**Achat** :
Dépense du centre, de catégorie Matériel (papier, produits, appareils) ou Service (ménage, jardinage, électricité).
_Avoid_ : frais, charge

**Tableau de bord** :
Vue du propriétaire sur un mois du calendrier ou une année : encaissé, impayés, dépenses (parts des professeurs, salaires, achats) et bénéfice, avec le détail par groupe.
_Avoid_ : statistiques, rapport, bilan
