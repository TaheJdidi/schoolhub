# 02. Centre (CEN)

Les centres ne s'inscrivent pas seuls : l'équipe SchoolHub les crée ([cadrage](../cadrage.md), Q3). Le propriétaire règle ensuite son centre.

### CEN-01 · Créer un centre et son premier propriétaire par une commande

En tant que membre de l'équipe SchoolHub (Taha ou Lassad), je veux créer un centre avec une commande, afin d'ouvrir SchoolHub à un nouveau client sans écran d'administration.

**Critères d'acceptation**

- La commande prend le nom du centre, le code de son lien public (par exemple `les-jasmins`), et le prénom, le nom et l'email du premier propriétaire.
- Elle crée le centre, vide, et le compte du propriétaire, qui reçoit son invitation (CPT-02).
- Un code de lien public déjà pris, ou un email déjà utilisé, est refusé avec un message clair.
- Le nouveau centre ne voit aucune donnée d'un autre centre, et inversement.
- La commande ne règle rien d'autre : le propriétaire fait le reste (CEN-02).

Maquette : sans objet.

### CEN-02 · Régler les paramètres du centre

En tant que propriétaire, je veux régler les informations et les prix de mon centre, afin que les reçus, les séances et les calculs soient justes.

**Critères d'acceptation**

- La page « Paramètres du centre » contient :
  - le nom, le téléphone et l'email du centre ;
  - l'année scolaire en cours, avec sa date de début et sa date de fin, par exemple du 15/09/2027 au 30/06/2028 ;
  - le montant de l'assurance, par exemple 20 TND ;
  - le prix de l'entrée à la journée à la bibliothèque, et sa capacité (facultative) ;
  - la part du centre par défaut, par exemple 5 %.
- Le nom et le téléphone du centre apparaissent sur chaque reçu ; l'email du centre reçoit la copie de chaque reçu envoyé par email.
- Tant que l'année scolaire n'est pas réglée, on ne peut pas créer de groupe d'Étude.
- Un changement suit la règle des [exigences communes](00-exigences-communes.md#ce-qui-change-en-cours-dannée) : ce qui est déjà dû garde l'ancienne valeur. Si l'assurance passe de 20 à 25 TND en janvier, Adem, qui la doit depuis septembre, paie 20 TND ; un élève dont l'assurance devient due en février paie 25 TND.
- Une nouvelle part du centre par défaut vaut pour les périodes (Mois, Révision, Formation) qui commencent après le changement. Elle ne change pas la part d'un professeur qui a la sienne (PRT-04).
- Pas de logo dans le MVP.

Maquette : pas d'écran.

### CEN-03 · Compléter la liste des niveaux

En tant que propriétaire, je veux ajouter des niveaux à la liste des niveaux scolaires tunisiens, afin de créer des groupes pour des publics qui n'y sont pas.

**Critères d'acceptation**

- La liste contient d'office les niveaux scolaires tunisiens, de la 1ʳᵉ année de base à la 4ᵉ année secondaire, avec ses sections (par exemple « 4ᵉ année secondaire, section mathématiques »).
- Le propriétaire ajoute « Université, 1ʳᵉ année » : ce niveau apparaît dans les groupes, les fiches élèves et le formulaire public.
- Un niveau ajouté peut être renommé. Il ne peut être retiré que si aucun groupe ni aucun élève ne l'utilise.
- Les niveaux d'office ne se modifient pas.

Maquette : pas d'écran.

### CEN-04 · Passer à l'année scolaire suivante

En tant que propriétaire, je veux ouvrir la nouvelle année scolaire, afin que les groupes, les Mois et l'assurance repartent de zéro à la rentrée.

**Critères d'acceptation**

- En juin 2028, le propriétaire saisit l'année 2028-2029, du 15/09/2028 au 30/06/2029.
- Le 30/06/2028, fin de l'année 2027-2028, les groupes d'Étude deviennent des **groupes terminés** : plus aucune séance n'est prévue, ils sortent de la liste des groupes et gardent leur historique.
- À la rentrée, la secrétaire crée les nouveaux groupes comme au premier jour (GRP-04). Leurs Mois commencent au Mois 1.
- L'assurance est de nouveau due par chaque élève, à la première séance de sa première inscription en Étude de la nouvelle année.
- Les élèves gardent leur fiche ; la secrétaire change leur niveau en masse (LST-05).
- La numérotation des reçus continue : elle ne repart pas à 1.
- Une Révision ou une Formation à cheval sur les deux années continue jusqu'à sa dernière séance.

Maquette : pas d'écran.
