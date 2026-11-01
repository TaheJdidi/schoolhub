# 12. Listes (LST)

Le cahier des charges demande que chaque liste puisse être imprimée, exportée et importée, avec une sélection multiple (§ 8). Décidé au [ticket 13](../wayfinder/cadrage-mvp/issues/13-user-stories-mvp.md), Q7.

### LST-01 · Imprimer une liste

En tant que secrétaire, je veux imprimer une liste, afin de l'avoir sur papier.

**Critères d'acceptation**

- Ces listes s'impriment : élèves, professeurs, groupes avec leurs élèves, reçus, entrées de la bibliothèque et achats. La liste des employés s'imprime aussi, en rôle propriétaire.
- L'impression suit le filtre en cours : « élèves de 9ᵉ année de base » n'imprime qu'eux.
- La page A4 porte le nom du centre, le nom de la liste, la date et le nombre de lignes.

Maquette : pas d'écran.

### LST-02 · Exporter une liste en Excel

En tant que secrétaire, je veux exporter une liste en Excel, afin de la retravailler ou de la garder.

**Critères d'acceptation**

- Les mêmes listes qu'en LST-01 s'exportent en fichier Excel (.xlsx), en suivant le filtre en cours.
- Les colonnes reprennent celles de la liste ; les montants sont des nombres, et les dates des dates.
- La secrétaire ne peut pas exporter les employés.

Maquette : pas d'écran.

### LST-03 · Importer des élèves depuis le fichier modèle

En tant que secrétaire d'un nouveau centre, je veux importer ma liste d'élèves depuis Excel, afin de ne pas tout ressaisir le premier jour.

**Critères d'acceptation**

- « Télécharger le modèle » donne un fichier Excel avec les colonnes : prénom, nom, niveau, téléphone WhatsApp du parent, élève majeur (oui ou non), téléphone de l'élève, email de l'élève, email du parent, établissement, groupe (facultatif).
- À l'envoi du fichier, la plateforme vérifie chaque ligne avant d'importer quoi que ce soit, et montre les lignes à corriger : « ligne 14 : niveau inconnu », « ligne 27 : téléphone du parent manquant », « ligne 40 : groupe introuvable ». Elle signale aussi les doublons possibles.
- Rien n'est importé tant qu'il reste une erreur : la secrétaire corrige son fichier et l'envoie de nouveau.
- Quand tout est bon : « 120 élèves importés, dont 95 inscrits dans un groupe ». Une ligne avec un groupe inscrit l'élève à partir du Mois en cours du groupe (GRP-07).

Maquette : Élèves → Importer.

### LST-04 · Importer des professeurs depuis le fichier modèle

En tant que secrétaire d'un nouveau centre, je veux importer la liste des professeurs depuis Excel, afin de préparer vite les groupes.

**Critères d'acceptation**

- Le modèle a les colonnes : prénom, nom, téléphone, email (facultatif).
- Les mêmes vérifications qu'en LST-03 s'appliquent, et rien n'est importé tant qu'il reste une erreur.

Maquette : pas d'écran.

### LST-05 · Agir sur plusieurs élèves sélectionnés

En tant que secrétaire, je veux sélectionner plusieurs élèves d'un coup, afin de faire la même action pour tous.

**Critères d'acceptation**

- La liste des élèves a une case par ligne, et « Tout sélectionner » prend toutes les lignes du filtre en cours.
- Avec une sélection, la secrétaire peut :
  - **inscrire dans un groupe** : les règles d'ELV-03 s'appliquent à chacun, et les élèves déjà inscrits sont listés à part ;
  - **changer le niveau**, par exemple de « 9ᵉ année de base » à « 1ʳᵉ année secondaire » à la rentrée ;
  - **imprimer ou exporter** la sélection.
- Il n'y a pas de suppression en masse.

Maquette : Élèves.
