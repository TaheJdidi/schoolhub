# 13. Back office de l'Éditeur et abonnements (EDI)

Un espace web à part, `/editeur`, réservé à l'équipe SchoolHub (Lassad et Taha), avec la double authentification. Il remplace peu à peu la commande `creer-centre` (CEN-01), qui reste pour dépanner. Le principe est celui du back office de Kinap : l'abonnement se paie hors ligne et se saisit à la main ; il n'y a ni essai gratuit, ni inscription en ligne.

Les stories EDI-06 à EDI-08 de Kinap (relances, accès support 24 h, export puis suppression d'un centre parti) viendront plus tard.

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
- Chaque action de l'Éditeur (création, prix, paiement, correction du propriétaire, suspension, réactivation) va au journal de l'Éditeur, que personne ne peut modifier ni effacer.

Maquette : sans objet, les écrans reprennent ceux de Kinap.
