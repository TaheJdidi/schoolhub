# 13. Back office de l'Éditeur et abonnements (EDI)

Un espace web à part, `/editeur`, réservé à l'équipe SchoolHub (Lassad et Taha), avec la double authentification. Il remplace peu à peu la commande `creer-centre` (CEN-01), qui reste pour dépanner. Le principe est celui du back office de Kinap : l'abonnement se paie hors ligne et se saisit à la main ; il n'y a ni essai gratuit, ni inscription en ligne.


### EDI-00 · Entrer dans le back office avec la double authentification

En tant qu'Éditeur, je veux une application d'authentification obligatoire, afin que personne n'entre dans le back office avec mon seul mot de passe.

**Critères d'acceptation**

- Le premier compte Éditeur se crée par la commande `creer-editeur --email …` : il reçoit une invitation, comme un compte de centre.
- À la première connexion, l'Éditeur scanne un QR code avec son application (Google Authenticator, Microsoft Authenticator…) et saisit le code affiché pour confirmer.
- Ensuite, chaque connexion demande le code à 6 chiffres de l'application. 5 codes faux de suite bloquent le compte 15 minutes, comme les mots de passe.
- Sans double authentification active, aucune adresse du back office ne répond.
- L'Éditeur n'appartient à aucun centre : il ne voit aucune page d'un centre, et un compte de centre ne voit rien du back office (« Accès réservé à l'Éditeur »).

### EDI-01 · Créer un centre

En tant qu'Éditeur, je veux créer un centre avec son premier propriétaire, afin de démarrer un nouveau client.

**Critères d'acceptation**

- Champs : nom du centre et code de son lien public (`les-jasmins`) ; prénom, nom, email et téléphone du propriétaire ; prix mensuel.
- Le prix mensuel proposé est celui de l'offre unique (90,000 TND, réglable dans la configuration). L'Éditeur peut le changer pour ce centre, à la création ou plus tard (ex. 90,000 → 75,000 TND, remise négociée).
- Le centre est créé **En attente**, avec ses niveaux d'office, comme par la commande (CEN-01). L'invitation ne part pas encore.
- Un code déjà pris, ou un email déjà utilisé sur SchoolUp, est refusé avec un message clair.
- La liste des centres montre, pour chacun : nom, état de l'abonnement, payé jusqu'au, prix mensuel, nombre de comptes, premier propriétaire.

### EDI-02 · Saisir un paiement d'abonnement

En tant qu'Éditeur, je veux saisir un paiement reçu, afin de prolonger l'abonnement du centre.

**Critères d'acceptation**

- Champs : date, montant, mode (virement, espèces, chèque), période couverte (du… au…), n° de facture (la facture est faite hors de SchoolUp).
- Exemple : « 270,000 TND, virement, du 05/01/2027 au 04/04/2027, facture F-2027-003 ». Le centre passe **Actif** jusqu'au 04/04/2027.
- L'historique de tous les paiements du centre reste visible sur sa fiche, avec qui les a saisis.

### EDI-03 · Envoyer l'invitation au propriétaire

En tant qu'Éditeur, je veux envoyer au propriétaire son lien par email ou par WhatsApp, afin qu'il choisisse son mot de passe.

**Critères d'acceptation**

- Les boutons ne sont actifs qu'après le premier paiement d'abonnement.
- « Envoyer par email » envoie l'email tout seul.
- « Envoyer par WhatsApp » ouvre WhatsApp avec le message et le lien déjà écrits, au numéro du propriétaire.
- On peut renvoyer l'invitation autant de fois que nécessaire. Chaque envoi est daté sur la fiche.
- L'Éditeur peut corriger le prénom, le nom, le téléphone et l'email du propriétaire avant de renvoyer. L'email ne se change plus une fois le mot de passe choisi.

### EDI-04 · Confirmer un paiement au propriétaire

En tant qu'Éditeur, je veux que le propriétaire reçoive une confirmation de chaque paiement, afin de garder une trace.

**Critères d'acceptation**

- À la saisie d'un paiement, un email part tout seul : « Paiement reçu : 270,000 TND, virement, du 05/01/2027 au 04/04/2027, facture F-2027-003 ».
- Si l'email ne part pas, le paiement reste enregistré, et l'Éditeur renvoie la confirmation depuis l'historique.
- Un bouton WhatsApp envoie le même message, pré-écrit.
- La date de chaque envoi est notée dans le paiement.

### EDI-05 · Faire vivre l'état de l'abonnement

En tant qu'Éditeur, je veux que l'état de chaque centre change tout seul, afin de ne rien oublier.

**Critères d'acceptation**

- Payé jusqu'au 04/04 : le centre est **Actif** jusqu'au 04/04 inclus, puis **En retard** du 05/04 au 19/04 (période de grâce de 15 jours, bandeau rouge chez le centre), puis **Suspendu** le 20/04.
- **Suspendu** : le centre est en lecture seule. Il consulte ses élèves, ses groupes, ses reçus, mais ne crée et ne modifie plus rien. Un bandeau l'explique.
- L'Éditeur peut aussi suspendre ou réactiver un centre à la main, avec un motif. Un centre impayé réactivé à la main reste Actif 15 jours, puis repasse Suspendu sans paiement.
- Un nouveau paiement rend le centre Actif tout de suite.
- Un centre ouvert avant le back office (par la commande) est **En attente** : il fonctionne normalement tant que l'Éditeur ne saisit rien.
- Chaque action de l'Éditeur (création, prix, paiement, correction du propriétaire, suspension, réactivation, suppression) va au journal de l'Éditeur, que personne ne peut modifier ni effacer.
- Le formulaire public d'inscription d'un centre Suspendu est fermé : « Les inscriptions en ligne de ce centre sont fermées pour le moment ».

### EDI-06 · Relancer les centres

En tant qu'Éditeur, je veux voir qui relancer, afin d'être payé à temps.

**Critères d'acceptation**

- 7 jours avant la fin, le centre entre dans la liste « à relancer », et un bandeau s'affiche chez son propriétaire : « Votre abonnement se termine le 04/04/2027 ». Un centre En retard y reste.
- Un email automatique part au premier propriétaire à J-7, puis le jour de la fin. Chacun part une seule fois ; s'il ne part pas (panne), il repart au passage suivant (toutes les heures).
- Dans la liste, un bouton WhatsApp ouvre le message de relance pré-écrit, avec le prix mensuel et le jour où SchoolUp passera en lecture seule. Chaque relance est datée sur la fiche.

### EDI-07 · Voir un centre en accès support

En tant qu'Éditeur, je veux voir un centre seulement quand son propriétaire l'autorise, afin d'aider sans accéder aux données des élèves le reste du temps.

**Critères d'acceptation**

- Sans autorisation, l'Éditeur ne voit que : nom du centre, état de l'abonnement, paiements, nombre de comptes.
- Dans sa page « Abonnement », le propriétaire clique sur « Autoriser le support 24 h ». Pendant 24 h, l'Éditeur voit l'accueil du centre (identité, nombre d'élèves, de groupes ouverts et de professeurs) et ses comptes, en lecture seule. Un centre Suspendu peut aussi le faire.
- Chaque page consultée par l'Éditeur va au journal du centre.
- Le propriétaire peut retirer l'autorisation avant la fin des 24 h. Une secrétaire ne peut ni la donner ni la retirer.

### EDI-08 · Exporter puis supprimer les données d'un centre parti

En tant qu'Éditeur, je veux rendre ses données à un centre qui part puis les supprimer, afin de respecter la loi.

**Critères d'acceptation**

- Pendant 12 mois après la suspension, le propriétaire télécharge depuis sa page « Abonnement » un fichier Excel : élèves, professeurs (au centre et partis), reçus, employés, achats et comptes. L'export est noté au journal du centre.
- Après 12 mois, le centre apparaît dans la liste « à supprimer ». L'Éditeur confirme en retapant exactement le nom du centre. Rien n'est supprimé automatiquement.
- La suppression efface le centre, ses comptes, son abonnement, ses paiements, son journal et toutes ses données. Elle est notée au journal de l'Éditeur, qui garde le nom du centre.

Maquette : sans objet, les écrans reprennent ceux de Kinap.
