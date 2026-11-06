# 04. Élèves et inscriptions (ELV)

Un élève a une seule fiche, d'une année sur l'autre. Une inscription le relie à un groupe et reçoit ses paiements ([ticket 01](../wayfinder/cadrage-mvp/issues/01-programmes-inscriptions-tarification.md), [ticket 04](../wayfinder/cadrage-mvp/issues/04-parcours-inscription-eleve.md)).

### ELV-01 · Chercher un élève depuis n'importe quel écran

En tant que secrétaire au guichet, je veux retrouver un élève en tapant quelques lettres, afin d'encaisser vite.

**Critères d'acceptation**

- Le champ de recherche reste en haut de chaque écran, sur ordinateur comme sur téléphone.
- En tapant « adem », « miled » ou « 22 345 », les résultats s'affichent pendant la frappe : nom, niveau, groupes et couleur de paiement.
- La recherche ignore les accents et les majuscules : « hedi » trouve « Hédi ».
- Elle cherche aussi dans le téléphone du parent.
- Un clic ouvre la fiche de l'élève (ELV-04).

Maquette : la recherche du guichet, en haut de l'écran.

### ELV-02 · Créer ou modifier la fiche d'un élève

En tant que secrétaire, je veux enregistrer un élève avec les contacts utiles, afin de pouvoir joindre sa famille.

**Critères d'acceptation**

- Obligatoires : prénom, nom, niveau et téléphone WhatsApp du parent. À partir de la 7ᵉ année de base, aussi le téléphone et l'email de l'élève.
- La case « L'élève est majeur » rend le téléphone du parent facultatif.
- Facultatifs : téléphone et email de l'élève avant la 7ᵉ année de base, email du parent, établissement scolaire.
- La fiche ne demande jamais de carte d'identité, d'adresse, de date de naissance ni de photo.
- Si un élève a déjà le même prénom et le même nom, ou le même téléphone, la plateforme prévient « Doublon possible », sans bloquer.

Maquette : fiche élève → Modifier la fiche de l'élève.

### ELV-03 · Inscrire un élève dans un groupe, avec son mode de tarification

En tant que secrétaire, je veux inscrire un élève dans un groupe, afin qu'il soit à l'appel et que la plateforme sache ce qu'il doit.

**Critères d'acceptation**

- On inscrit depuis la fiche de l'élève (« Inscrire dans un autre groupe ») ou depuis le groupe (« Ajouter un élève »). Les groupes du niveau de l'élève sont proposés en premier.
- Le mode de tarification dépend du type : par Mois pour une Étude ; à la séance ou en totalité pour une Révision ; en une fois ou en tranches pour une Formation qui a des tranches.
- Arrivée en cours de Mois : Adem rejoint le groupe de Karim, à 40 TND le Mois, à la 2ᵉ séance du Mois 3. Il doit 30,000 TND pour le Mois 3, soit 3 séances.
- Arrivée en cours de Révision : la totalité vaut le prix des séances qui restent.
- Si c'est sa première inscription en Étude de l'année, et que l'assurance n'est ni payée ni dispensée, elle est due à sa première séance.
- Un élève déjà inscrit dans ce groupe est refusé.
- Si le groupe a atteint sa capacité, la plateforme prévient « Ce groupe a atteint sa capacité. L'inscription reste possible. »
- Après l'enregistrement, la plateforme propose un message WhatsApp de confirmation aux [destinataires](00-exigences-communes.md#messages) de l'élève : groupe, professeur, date et heure de la première séance.

Maquette : fiche élève → Inscriptions.

### ELV-04 · Voir la fiche d'un élève

En tant que secrétaire, je veux voir sur une seule page tout ce qui concerne un élève, afin de répondre au parent au guichet.

**Critères d'acceptation**

- En haut, « À payer » : tout ce qu'il doit à ce jour, inscription par inscription, avec sa couleur (CAI-01).
- Ses inscriptions : groupe, professeur, mode de tarification, prix ou prix spécial.
- L'assurance de l'année : payée, à payer, dispensée, déjà payée avant SchoolHub, ou sans objet (aucune Étude).
- Ses présences par groupe, depuis le début : P présent, A absent, ? appel à faire, et ses rattrapages.
- Ses reçus : numéro, date, montant, et la mention « annulé » s'il y a lieu.
- Ses contacts, et la date du dernier rappel de paiement (CAI-08).
- Un élève sans inscription montre « Aucune inscription : cet élève vient seulement à la bibliothèque », avec ses entrées.

Maquette : fiche élève.

### ELV-05 · Accorder un prix spécial

En tant que propriétaire, je veux accorder un prix spécial à un élève pour une inscription, afin d'aider une famille en difficulté ou un proche du centre.

**Critères d'acceptation**

- Sur une inscription, le propriétaire saisit le prix spécial (du Mois pour une Étude, total pour une Révision ou une Formation) et une raison obligatoire. Il peut valoir 0.
- Le prix spécial vaut pour tout ce qui n'est pas encore payé, Mois en cours compris ; ce qui est payé ne change pas et n'est pas remboursé. Adem a payé les Mois 1 à 3 à 40 TND ; le 01/12, le propriétaire lui accorde 20 TND : les Mois 1 à 3 restent à 40 TND, le Mois 4 en cours et les suivants passent à 20 TND.
- Pour une Formation en tranches, les tranches qui restent se partagent le nouveau prix : 300 TND en 3 × 100 TND avec un prix spécial de 150 TND donnent 3 × 50 TND.
- Un élève à 0 TND compte comme payé et n'a jamais de couleur.
- En rôle secrétaire, le bouton est remplacé par « Seul le propriétaire accorde un prix spécial. Inscrivez au prix normal et prévenez-le. »
- Le propriétaire peut retirer le prix spécial : le prix normal revient pour ce qui n'est pas payé.
- Accorder et retirer un prix spécial vont au journal.

Maquette : fiche élève → Prix spécial.

### ELV-06 · Dispenser un élève de l'assurance

En tant que propriétaire, je veux dispenser un élève de l'assurance de l'année, avec une raison, afin de ne pas la lui réclamer.

**Critères d'acceptation**

- Sur la fiche, pour l'année scolaire en cours, le propriétaire choisit « Dispenser » et écrit la raison.
- L'assurance n'est plus due ; la fiche montre « dispensé ». L'année suivante, elle redevient due.
- La secrétaire ne peut pas dispenser.
- La dispense va au journal.

Maquette : fiche élève → Assurance.

### ELV-07 · Noter un paiement fait avant SchoolHub

En tant que secrétaire d'un centre qui démarre en cours d'année, je veux noter ce qu'un élève a déjà payé dans le carnet de reçus, afin qu'on ne le lui réclame pas deux fois.

**Critères d'acceptation**

- Sur la fiche, « Déjà payé avant SchoolHub » est possible pour l'assurance de l'année en cours, et pour une somme d'une Révision ou d'une Formation déjà en cours (GRP-07) due avant le premier jour du groupe dans SchoolHub.
- La somme devient payée, sans reçu. Elle n'entre ni dans la caisse ni dans l'encaissé du tableau de bord.
- La fiche la montre avec la mention « payé avant SchoolHub ».
- Cette action va au journal.

Maquette : pas d'écran.

### ELV-08 · Arrêter une inscription

En tant que secrétaire, je veux arrêter l'inscription d'un élève qui ne vient plus, afin qu'il ne doive plus rien ensuite.

**Critères d'acceptation**

- L'arrêt se fait à une date, aujourd'hui par défaut : rien n'est dû après.
- Ce qui était dû avant reste dû. Adem arrête le 25/01, et le Mois 5 a commencé le samedi 15/01 : le Mois 5 reste dû en entier, car un Mois se paie à sa première séance.
- Pas de remboursement, ni de pause : s'il revient, il est réinscrit (ELV-03) et paie au prorata.
- Il n'apparaît plus à l'appel des séances suivantes.
- L'arrêt va au journal.

Maquette : fiche élève → Arrêter l'inscription.

### ELV-09 · Changer un élève de groupe

En tant que secrétaire, je veux passer un élève dans un autre groupe du même type, afin qu'il garde ce qu'il a déjà payé.

**Critères d'acceptation**

- Le nouveau groupe est du même type, même avec un autre professeur.
- Le changement se fait à une date : l'ancienne inscription s'arrête, la nouvelle commence.
- La valeur des séances payées et pas suivies dans l'ancien groupe est déduite de ce qu'il doit dans le nouveau, et le surplus est reporté sur ses paiements suivants. Pas de remboursement.
- Exemple : Adem a payé le Mois 5 (40 TND) et change de groupe après la séance 1. Il lui reste 3 séances, soit 30 TND. Il arrive à la 2ᵉ séance d'un Mois à 48 TND : il doit 36 TND, moins 30 TND, soit 6 TND.
- Le changement va au journal.

Maquette : fiche élève → Changer de groupe.

### ELV-10 · Passer une Formation au paiement total

En tant que secrétaire, je veux qu'un élève qui paie une Formation en tranches puisse tout payer d'un coup, afin de solder sa Formation.

**Critères d'acceptation**

- « Payer tout le reste » regroupe les tranches à venir en une seule somme, due aujourd'hui.
- Exemple : 3 tranches de 100 TND, la première payée : il doit 200 TND aujourd'hui.
- Pas de réduction automatique.

Maquette : fiche élève → À payer.

### ELV-11 · Supprimer une fiche sans historique

En tant que secrétaire, je veux supprimer une fiche créée par erreur, afin de garder une liste propre.

**Critères d'acceptation**

- « Supprimer » n'apparaît que si l'élève n'a ni inscription, ni paiement, ni reçu, ni présence, ni entrée à la bibliothèque.
- La plateforme demande de confirmer.
- Une fiche avec un historique ne se supprime pas : elle reste, même sans inscription.

Maquette : pas d'écran.
