# 07. Caisse et reçus (CAI)

Espèces seulement. Pas de paiement partiel ni de remboursement. Chaque paiement donne un reçu numéroté, jamais supprimé ([ticket 01](../wayfinder/cadrage-mvp/issues/01-programmes-inscriptions-tarification.md), [ticket 10](../wayfinder/cadrage-mvp/issues/10-caisse-recus.md)).

### CAI-01 · Voir ce qu'un élève doit, et sa couleur

En tant que secrétaire, je veux voir d'un coup d'œil qui est en retard de paiement, afin de réclamer au bon moment.

**Critères d'acceptation**

- Chaque somme est due à une séance précise :
  - un Mois d'Étude, à sa première séance ;
  - une séance de Révision payée à la séance, le jour de cette séance, si l'élève est présent ;
  - la totalité d'une Révision, ou une Formation payée en une fois, à la première séance de l'élève ;
  - une tranche de Formation, à sa séance ;
  - l'assurance, à la première séance de sa première inscription en Étude de l'année.
- Une somme due à la séance k et pas payée fait passer l'élève en **orange** à la séance k+1, puis en **rouge** à la séance k+2. Exemple : le Mois 3 est dû le samedi 02/10 ; pas payé, Adem est orange le 09/10 et rouge le 16/10.
- Le jour où une somme est due, l'élève est « à payer aujourd'hui » ; sans retard, il est « à jour ».
- Sur sa fiche et dans la recherche, un élève prend la pire couleur de ses inscriptions.
- Un élève à prix spécial 0 n'a jamais de couleur.

Maquette : fiche groupe (pastilles) et fiche élève.

### CAI-02 · Voir le badge « groupe payé » et les élèves à relancer

En tant que secrétaire, je veux savoir si tout un groupe a payé, afin de repérer les groupes à relancer.

**Critères d'acceptation**

- Le badge « Groupe payé » s'affiche quand tous les élèves encore inscrits ont payé ce qui est dû à ce jour. Un élève à 0 TND compte comme payé ; un élève en rattrapage ne compte pas.
- Sinon, le badge dit « 3 à relancer » (élèves orange ou rouges), ou « 2 à payer aujourd'hui ».
- Le badge apparaît sur le cadre du groupe (GRP-02), sur sa fiche et sur l'accueil.

Maquette : Professeurs (cadres des groupes) et fiche groupe.

### CAI-03 · Encaisser un élève depuis sa fiche

En tant que secrétaire, je veux encaisser ce qu'un élève doit depuis sa fiche, afin de lui remettre son reçu tout de suite.

**Critères d'acceptation**

- « À payer » liste ce qui est dû à ce jour, avec une case par somme ; « Payer d'avance » ajoute les Mois suivants, ou les tranches suivantes.
- Adem est inscrit en mathématiques à 40 TND : sa fiche montre « Mois 3 à payer ». La secrétaire coche le Mois 3 et le Mois 4 : le total affiché est 80,000 TND.
- Chaque somme se paie en entier : pas de paiement partiel.
- « Encaisser en espèces » crée **un seul reçu** pour cet élève, avec un numéro qui suit le précédent, même s'il paie dans plusieurs groupes.
- Sa couleur se met à jour tout de suite.
- Le paiement entre dans la caisse du jour de la secrétaire.
- Le reçu part tout seul par email à ceux de ses [destinataires](00-exigences-communes.md#messages) qui ont un email, avec copie au centre.
- L'encaissement va au journal ; la remise du reçu suit (CAI-05).

Maquette : fiche élève → À payer.

### CAI-04 · Faire un encaissement groupé

En tant que secrétaire, je veux encaisser plusieurs élèves d'un groupe en une seule action, afin de ne pas faire 20 encaissements à la suite.

**Critères d'acceptation**

- Depuis un groupe, « Encaissement groupé » liste les élèves avec ce que chacun doit dans ce groupe à ce jour, assurance comprise si elle est due.
- La secrétaire coche les élèves qui paient (« Tout cocher » existe). Chacun paie tout ce qu'il doit dans ce groupe.
- 18 élèves cochés donnent 18 reçus, un par élève, numérotés à la suite.
- « Imprimer tous les reçus » donne un seul fichier, avec un reçu A5 par page.
- Les emails partent tout seuls pour les élèves qui en ont un.
- Les élèves déjà à jour sont listés à part, dans « Déjà à jour ».

Maquette : Encaissement groupé.

### CAI-05 · Remettre le reçu : imprimé, WhatsApp ou email

En tant que secrétaire, je veux remettre le reçu de la façon qui convient à la famille, afin d'éviter le papier quand ce n'est pas utile.

**Critères d'acceptation**

- Le reçu contient : nom et téléphone du centre, numéro, date, élève, groupe, ce qui est payé (« Mois 3 », « tranche 2 », « 2 séances », « assurance », « entrée à la bibliothèque »), montant et nom de la secrétaire.
- Trois choix :
  - **Imprimer** : un PDF au format A5 ;
  - **WhatsApp** : un message qui reprend tout le contenu du reçu en texte, prêt à envoyer à ses [destinataires](00-exigences-communes.md#messages) ;
  - **Email** : déjà parti s'il y avait un email ; « Renvoyer l'email » sinon.
- Sans email connu, l'écran dit « Aucun email n'est connu pour cet élève. Ajoutez-en un sur sa fiche, ou utilisez WhatsApp. »
- Un reçu peut être remis de nouveau plus tard, depuis la liste des reçus (CAI-06).
- Un reçu annulé porte la mention « annulé » partout, PDF compris.

Maquette : Reçu → Remettre le reçu.

### CAI-06 · Retrouver un reçu

En tant que secrétaire, je veux retrouver un reçu, afin de le remettre de nouveau ou de répondre à un parent.

**Critères d'acceptation**

- La caisse montre les reçus du jour. La liste des reçus se cherche par numéro, par élève et par date.
- La secrétaire voit tous les reçus du centre.
- Ouvrir un reçu montre son aperçu, permet de le remettre (CAI-05) ou de l'annuler (CAI-07).

Maquette : Caisse → Reçus du jour.

### CAI-07 · Annuler un reçu

En tant que secrétaire, je veux annuler un reçu fait par erreur, afin de corriger sans rien effacer.

**Critères d'acceptation**

- La secrétaire peut annuler un de ses reçus du jour, tant que sa caisse n'est pas clôturée. Le motif est obligatoire, par exemple « Mauvais élève sélectionné ».
- Dans les autres cas, seul le propriétaire peut annuler, et l'écran dit pourquoi : « Ce reçu date d'avant aujourd'hui », « Votre caisse est clôturée » ou « Ce reçu a été fait par une autre secrétaire ».
- Le reçu reste visible avec la mention « annulé » et son motif. Ce qui était payé redevient à payer, et les couleurs se recalculent.
- Annulé le jour même, le montant sort de la caisse du jour. Annulé par le propriétaire après la clôture, il ne change pas la clôture déjà faite.
- L'annulation va au journal.

Maquette : Reçu → Annuler ce reçu.

### CAI-08 · Relancer un élève

En tant que secrétaire, je veux relancer un élève en retard et, s'il est mineur, son parent, sans réécrire le message, afin de récupérer les impayés.

**Critères d'acceptation**

- « Relancer » apparaît sur chaque élève orange ou rouge : sur sa fiche, dans la liste du groupe et dans « Élèves en rouge » de l'accueil.
- « Relancer » prépare un message pour chacun de ses [destinataires](00-exigences-communes.md#messages), déjà écrit : « Bonjour, il reste à payer pour Adem Miled : Mois 3 de mathématiques, 40,000 TND. Centre Les Jasmins. »
- La date de la dernière relance s'affiche, « relancé le 12/10 », pour ne pas relancer deux fois.

Maquette : fiche groupe (« à relancer ») et Aujourd'hui (« Élèves en rouge »).

### CAI-09 · Clôturer sa caisse le soir

En tant que secrétaire, je veux compter mes espèces le soir et les comparer à ce que la plateforme attend, afin de repérer tout de suite une erreur.

**Critères d'acceptation**

- « Ma caisse » montre l'encaissé du jour (les reçus), les sorties de caisse (les achats payés en espèces, RHA-04) et les espèces attendues : l'encaissé moins les sorties.
- La secrétaire saisit le montant compté : la plateforme enregistre l'écart (compté moins attendu) et l'heure, et la caisse passe à « Clôturée ».
- Exemple : attendu 845,000 TND, compté 840,000 TND : écart de −5,000 TND.
- Chaque secrétaire a sa caisse ; le propriétaire qui encaisse a aussi la sienne.
- Un encaissement fait après la clôture ouvre une deuxième caisse du jour, à clôturer aussi.
- Une caisse oubliée est signalée le lendemain, à la secrétaire et au propriétaire, et se clôture alors avec le montant compté.

Maquette : Caisse → Clôture du soir.

### CAI-10 · Contrôler les clôtures et marquer « argent reçu »

En tant que propriétaire, je veux voir les clôtures de toutes les secrétaires, afin de récupérer les espèces et de repérer les écarts.

**Critères d'acceptation**

- Le tableau « Contrôle des clôtures » montre pour chaque caisse : jour, secrétaire, attendu, compté et écart. Un écart négatif est en rouge.
- Le propriétaire coche « argent reçu » quand il récupère les espèces ; les caisses pas encore récupérées sont « à récupérer ».
- Le tableau se filtre par période et par secrétaire.

Maquette : Caisse → Contrôle des clôtures (rôle propriétaire).
