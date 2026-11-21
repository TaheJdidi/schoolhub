# 06. Séances et appel (SEA)

Les séances se créent depuis l'horaire du groupe (GRP-04 à GRP-06). L'appel se fait sur la plateforme ou sur une feuille d'appel ; un élève est présent sauf s'il est marqué absent ([ticket 02](../wayfinder/cadrage-mvp/issues/02-seances-presence-rattrapage.md)).

### SEA-01 · Voir l'accueil « Aujourd'hui »

En tant que secrétaire, je veux voir en arrivant tout ce qui se passe aujourd'hui, afin de savoir quoi faire sans chercher.

**Critères d'acceptation**

- L'accueil montre la date du jour et les séances du jour, heure par heure : groupe, professeur, nombre d'élèves attendus, et « appel fait », « appel à faire » ou « annulée ».
- Il montre aussi :
  - les appels à recopier, c'est-à-dire les séances passées sans appel (SEA-09) ;
  - le nombre de demandes d'inscription à traiter ;
  - les élèves en rouge, avec ce qu'ils doivent ;
  - ma caisse du jour ;
  - le nombre de présents à la bibliothèque.
- Un clic sur une séance ouvre son groupe, avec l'appel du jour.

Maquette : Aujourd'hui.

### SEA-02 · Voir les séances d'un groupe

En tant que secrétaire, je veux voir les séances passées et à venir d'un groupe, afin de savoir où il en est.

**Critères d'acceptation**

- La fiche groupe liste ses séances avec leur état : à venir, appel fait, appel à faire, annulée (avec son motif), de remplacement ou supplémentaire.
- Pour une Étude, les séances sont rangées par Mois (« Mois 3 : séances 9 à 12 »), et le Mois en cours est indiqué.
- Pour une Révision ou une Formation, l'avancement s'affiche : « séance 2 sur 3 ».

Maquette : fiche groupe → Séances.

### SEA-03 · Annuler une séance

En tant que secrétaire, je veux annuler une séance qui n'aura pas lieu, afin qu'elle ne compte pas et que les élèves soient prévenus.

**Critères d'acceptation**

- L'annulation demande un motif, par exemple « professeur absent ».
- Une séance annulée ne compte pas : le Mois s'allonge. Le Mois 3 prévu les 02/10, 09/10, 16/10 et 23/10 perd le 16/10 : il devient 02/10, 09/10, 23/10 et 30/10, et les Mois suivants se décalent.
- Pour une Révision ou une Formation, la plateforme propose de remplacer la séance (SEA-04), pour garder le nombre de séances prévu.
- La plateforme prépare un message WhatsApp pour les [destinataires](00-exigences-communes.md#messages) de chaque élève du groupe.
- L'annulation va au journal.

Maquette : fiche groupe → Annuler cette séance.

### SEA-04 · Déplacer une séance

En tant que secrétaire, je veux déplacer une séance à une autre date, en une seule action, afin de ne pas faire une annulation puis un ajout.

**Critères d'acceptation**

- La secrétaire choisit la nouvelle date et l'heure : la séance d'origine est annulée, et une **séance de remplacement** est créée. Elle compte à sa place dans le Mois.
- La nouvelle date peut tomber un jour férié.
- La plateforme prépare un message WhatsApp pour les [destinataires](00-exigences-communes.md#messages) de chaque élève du groupe, avec l'ancienne et la nouvelle date.
- Le déplacement va au journal.

Maquette : fiche groupe → Déplacer cette séance.

### SEA-05 · Ajouter une séance

En tant que secrétaire, je veux ajouter une séance en plus de l'horaire, afin qu'elle ait son appel et trouve sa place dans le calendrier.

**Critères d'acceptation**

- La secrétaire choisit la date et l'heure. La séance apparaît dans la bande des séances, à sa date.
- Dans une Étude, elle compte dans le Mois : le Mois garde 4 séances, et les suivantes passent au Mois d'après. Le Mois 3 des 14/11, 21/11, 28/11 et 05/12 reçoit une séance le dimanche 22/11 : il devient 14/11, 21/11, 22/11 et 28/11, et le 05/12 ouvre le Mois 4.
- Dans une Révision ou une Formation, la séance supplémentaire est offerte, par exemple avant un examen : elle ne compte pas, et rien de plus n'est dû.
- Elle a son appel, comme les autres séances.
- La plateforme prépare un message WhatsApp pour les [destinataires](00-exigences-communes.md#messages) de chaque élève du groupe.

Maquette : fiche groupe → Ajouter une séance.

### SEA-06 · Changer l'horaire d'un groupe à partir d'une date

En tant que secrétaire, je veux changer l'horaire d'un groupe à partir d'une date, par exemple pendant le Ramadan, afin que les séances suivantes se recalculent.

**Critères d'acceptation**

- La secrétaire saisit le ou les nouveaux créneaux et la date de départ, par exemple « le samedi de 14h à 16h, à partir du 29/01/2028 » pour le Ramadan.
- Les séances à venir après cette date sont recréées sur le nouvel horaire ; les séances d'avant ne changent pas.
- Pour revenir à l'ancien horaire après le Ramadan, elle fait un autre changement, par exemple à partir du 26/02/2028.
- La plateforme prépare un message WhatsApp pour les [destinataires](00-exigences-communes.md#messages) de chaque élève du groupe.

Maquette : fiche groupe → Changer l'horaire du groupe.

### SEA-07 · Faire l'appel sur la plateforme

En tant que secrétaire, je veux faire l'appel en ne touchant que les absents, afin d'aller vite.

**Critères d'acceptation**

- À partir du jour de la séance, l'appel s'ouvre avec tous les élèves présents. Un toucher marque un élève absent ; un autre le remet présent.
- « Enregistrer l'appel » fait passer la séance à « appel fait ».
- Les élèves venus d'un autre groupe apparaissent à part, avec « Paie dans son groupe d'origine ».
- Un absent en Étude doit quand même son Mois. Un absent d'une Révision payée à la séance ne doit pas cette séance.
- L'appel se fait aussi bien sur téléphone que sur ordinateur.

Maquette : fiche groupe → Appel.

### SEA-08 · Imprimer une feuille d'appel

En tant que secrétaire, je veux imprimer une feuille d'appel, afin que le professeur fasse l'appel sur papier, même sans Internet.

**Critères d'acceptation**

- La période se choisit : la séance du jour, un Mois (par exemple le Mois 2) ou des dates.
- La feuille A4 porte le nom du groupe, le professeur et l'année scolaire, puis, pour chaque élève, son nom, son prénom, une case par séance et une case de signature.
- Des lignes vides servent aux élèves venus d'un autre groupe.
- Elle ne montre ni téléphone ni paiement.
- La consigne est écrite : « Écrivez A dans la case d'un élève absent. Les autres sont présents. »

Maquette : Feuille d'appel.

### SEA-09 · Recopier une feuille d'appel

En tant que secrétaire, je veux voir les appels à recopier, afin de saisir les feuilles dès leur retour.

**Critères d'acceptation**

- La liste « Appels à recopier » montre les séances passées, non annulées, encore « appel à faire ».
- « Recopier » ouvre l'appel de cette séance, comme en SEA-07.
- Une séance non annulée compte à sa date, même si l'appel n'est jamais fait.

Maquette : Aujourd'hui → Appels à recopier.

### SEA-10 · Corriger une présence

En tant que secrétaire, je veux corriger une erreur d'appel, afin que la présence soit juste.

**Critères d'acceptation**

- « Corriger l'appel » est possible à tout moment sur une séance « appel fait ».
- Chaque correction garde qui l'a faite, quand, et l'ancienne valeur ; elle va au journal.
- Si la correction change ce qui est dû (un absent d'une Révision payée à la séance devient présent), ce qui est dû se met à jour.

Maquette : fiche groupe → Corriger l'appel.

### SEA-11 · Prévenir les absents et leurs parents

En tant que secrétaire, je veux prévenir les absents juste après l'appel et, pour un mineur, son parent, afin que la famille sache que l'élève n'est pas venu.

**Critères d'acceptation**

- Après l'enregistrement de l'appel, « Prévenir les parents des absents » liste les absents, avec un bouton WhatsApp pour chacun de leurs [destinataires](00-exigences-communes.md#messages).
- Le message est déjà écrit : « Bonjour, Adem Miled était absent à la séance de mathématiques du samedi 15/01/2028, de 10h à 12h. Centre Les Jasmins. »
- Un message ouvert est marqué comme tel, pour ne pas l'envoyer deux fois.

Maquette : fiche groupe → Prévenir les parents des absents.

### SEA-12 · Venus d'un autre groupe

En tant que secrétaire, je veux ajouter en quelques touchers les élèves d'un autre groupe du même professeur venus à cette séance à la place de la leur, afin qu'ils ne perdent pas leur cours.

**Critères d'acceptation**

- Depuis une séance, « Venus d'un autre groupe » ne propose que les autres groupes du même professeur, de la même matière et du même niveau. S'il n'y en a qu'un, il est déjà choisi.
- La secrétaire touche le groupe, puis coche les élèves venus ; « Ajouter 5 élèves » les ajoute tous d'un coup, ou aucun si l'un ne peut pas venir.
- Ils remplacent la prochaine séance de leur groupe, à partir du jour de cette séance ; « changer » permet d'en choisir une autre, déjà passée ou à venir.
- Dans le groupe d'accueil, ils apparaissent à part à l'appel, et n'entrent ni dans le badge « groupe payé » ni dans les couleurs.
- Le paiement reste dans leur groupe d'origine ; la séance remplacée y montre l'étiquette « Venu le 21/11 avec un autre groupe ». La secrétaire y fait l'appel comme pour les autres élèves.

Maquette : fiche groupe → + Venus d'un autre groupe.
