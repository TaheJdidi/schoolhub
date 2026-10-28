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
Personne qui suit un ou plusieurs programmes au centre.
_Avoid_ : étudiant, apprenant

### L'offre d'enseignement

**Programme** :
Offre d'enseignement proposée par le centre, d'un type donné (Étude, Révision ou Formation), sur une période donnée et avec un mode de tarification.
_Avoid_ : espace, offre, cours

**Étude** :
Programme qui dure toute l'année scolaire.
_Avoid_ : espace étude, cours annuel

**Révision** :
Programme court de quelques séances, par exemple avant un examen.
_Avoid_ : espace révision, stage

**Formation** :
Programme dont le prix et le nombre de séances sont fixés à l'avance, par exemple une formation en informatique.
_Avoid_ : espace formation, atelier

**Mode de tarification** :
Manière dont une inscription est facturée : mensuel, par séance ou forfait.
_Avoid_ : type de paiement, formule

**Groupe** :
Ensemble d'élèves d'un programme qui suivent les séances d'un même professeur sur un horaire hebdomadaire fixe, par exemple « Groupe Lundi 8h→10h ».
_Avoid_ : classe, section

**Séance** :
Occurrence datée d'un cours, pour un groupe.
_Avoid_ : cours, leçon, session

### Le suivi des élèves

**Inscription** :
Rattachement d'un élève à un programme. Elle porte le mode de tarification et reçoit les paiements. Un élève peut avoir plusieurs inscriptions.
_Avoid_ : abonnement, adhésion

**Paiement** :
Somme encaissée par le centre au titre d'une inscription.
_Avoid_ : versement, règlement

**Présence** :
Statut d'un élève à une séance : présent ou absent.
_Avoid_ : pointage, assiduité

**Rattrapage** :
Participation d'un élève à une séance d'un autre groupe que le sien, en général après une absence.
_Avoid_ : remplacement, changement de séance
