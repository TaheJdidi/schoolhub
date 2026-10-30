# SchoolHub

Plateforme de gestion pour les centres d'étude tunisiens : un centre y gère ses élèves, ses professeurs, ses groupes, les séances, la présence et les paiements, ainsi que sa bibliothèque, ses achats et son personnel.

## Language

### Le centre et ses utilisateurs

**Centre** :
Établissement client qui utilise SchoolHub pour gérer son activité. Ses données sont séparées de celles des autres centres.
_Avoid_ : école, établissement

**Propriétaire** :
Personne qui possède le centre. Il a accès à tout, y compris aux ressources humaines et aux comptes des employés.
_Avoid_ : super admin, gérant, directeur

**Secrétaire** :
Employé qui gère le quotidien du centre. Il a accès à tout sauf aux ressources humaines.
_Avoid_ : admin, réceptionniste

**Professeur** :
Personne qui anime un ou plusieurs groupes au centre.
_Avoid_ : enseignant, prof, formateur

**Élève** :
Personne inscrite dans un ou plusieurs groupes du centre.
_Avoid_ : étudiant, apprenant

### L'offre d'enseignement

**Groupe** :
Ensemble d'élèves qui suivent les séances d'un même professeur, en général sur un horaire hebdomadaire, par exemple « Groupe Lundi 8h→10h ». Chaque groupe a un type (Étude, Révision ou Formation), une matière et un niveau, facultatif pour une Formation.
_Avoid_ : classe, section, programme

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

### Le suivi des élèves

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
Somme encaissée par le centre auprès d'un élève, pour une inscription ou pour l'assurance.
_Avoid_ : versement, règlement

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
