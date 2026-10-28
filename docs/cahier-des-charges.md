# Cahier des charges initial : plateforme de gestion de centre d'étude

> Transcription Markdown du document `centre.docx` fourni par le porteur du projet (28/10/2026).
> Le fond n'a pas été modifié : seuls la mise en forme et l'orthographe ont été retouchées. Les espaces Révision et Formation répétaient mot pour mot le modèle de l'Espace Étude ; ils y renvoient au lieu de le recopier.
> Les remarques et questions de cadrage sont dans [cadrage.md](cadrage.md).

## 1. Espace Étude (annuel)

### Professeur

L'espace possède une liste de professeurs (CRUD sur les professeurs).

Modèle : `id, nom, prénom, email, numéro de téléphone, liste de groupes`.

### Groupe

Chaque groupe est une liste d'élèves avec un créneau de 2 h par semaine. Exemple : le professeur P1 a deux groupes G1 et G2 ; la séance de G1 est le samedi de 10 h à 12 h, celle de G2 le dimanche de 8 h à 10 h.

Modèle : `id, nom, date, nb élèves, liste élèves`.

### Élève

Modèle : `id, nom, prénom, email, numéro de téléphone, niveau, montant à payer, payé (bool)`.

CRUD sur chaque élève.

## 2. Espace Révision (période variable, par exemple 1 mois)

Même modèle Professeur / Groupe / Élève que l'Espace Étude.

> Remarque : pour la révision, il y a deux types de paiement : par séance ou par mois.

## 3. Espace Formation (période variable, par exemple 1 mois)

Même modèle Professeur / Groupe / Élève que l'Espace Étude.

> Remarque : pour la formation, il y a deux types de paiement : par séance ou par mois.

## 4. Espace Bibliothèque

L'espace bibliothèque sert à avoir la liste des élèves par jour. On peut charger la liste des élèves pendant une durée déterminée.

## 5. Espace Ressources humaines

Liste des ressources humaines qui travaillent dans le centre.

Modèle : `id, nom, prénom, email, numéro de téléphone`.

Rôle de la section : gestion des ressources, des jours de congé, des horaires de travail…

## 6. Espace Achats

Traçabilité des achats faits pour le centre, et calcul du total mensuel ou annuel (papier, stylos, produits de nettoyage, appareils…).

Deux catégories d'achat :

- **Matériel**
- **Service** : femme de ménage, jardinage, intervention électricité, etc.

## 7. Accès

- Un **super admin**, le propriétaire du centre, qui a tous les accès.
- La **secrétaire** a tous les accès sauf les ressources humaines.

## 8. Remarques générales

- Sélection multiple sur chaque liste (professeurs, élèves, ressources humaines, groupes) pour faire du CRUD en masse.
- La plateforme doit être sécurisée.
- La plateforme doit être accessible depuis n'importe quelle machine.
- La plateforme doit être responsive.
- Chaque liste (groupes, élèves, professeurs…) peut être imprimée, exportée et importée.
- Quand on clique sur « payer » pour passer un élève au statut Payé, un email part directement vers l'élève et vers l'email du centre, pour la traçabilité.
- Si tout le groupe a payé le mois en cours, l'icône du groupe l'indique.
- En cliquant sur le nom d'un professeur, la liste de ses groupes s'affiche, chaque groupe dans un cadre dans le corps de la page (comme les data sources dans UHMI).
- Chaque élève a une couleur : il doit payer les frais mensuels dès la première séance (en général 4 séances par mois). S'il n'a pas payé à la 2ᵉ séance, la couleur devient orange ; à la 3ᵉ séance, rouge.

## 9. Idées à améliorer

- Le reçu doit pouvoir se donner rapidement, car un groupe peut compter 20 élèves.
- **Architecture** : une seule plateforme pour tous les centres. Le centre qui achète la plateforme reçoit un accès pour créer un compte. Le propriétaire crée son compte en super admin, puis crée des comptes (email + mot de passe) pour les employés qui gèrent la bibliothèque, la formation, les achats… Le super admin a accès aux ressources humaines et à la gestion des comptes employés.
- La plateforme affiche la date du jour (jour/mois/année).
- Pour s'inscrire, l'élève envoie un email au centre avec un objet précis et, dans le corps du mail, les noms de ses amis.
- Paiement de l'assurance du centre.
- Les groupes sont nommés ainsi : `Groupe Lundi : 8h => 10h`.
- On peut lister les groupes par jour : un clic sur le professeur liste tous ses groupes, puis un filtre n'affiche par exemple que ceux du lundi.
- Un élève peut changer de séance pour assister avec un autre groupe en cas d'absence : la présence et le paiement doivent donc être indépendants du groupe.
- À chaque séance, chaque élève a un statut de présence : présent ou absent.

## 10. Étapes prévues

1. Demander une maquette pour valider le format du front-end.
2. Demander une estimation.
3. Préparer les user stories.
4. Préparer CONTEXT.md, DESIGN.md, l'architecture…
