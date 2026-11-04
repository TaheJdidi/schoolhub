# 03. Professeurs et groupes (GRP)

Un groupe a un type (Étude, Révision ou Formation), un professeur, une matière et un niveau. Son prix se fixe par élève ([ticket 01](../wayfinder/cadrage-mvp/issues/01-programmes-inscriptions-tarification.md)). Ses séances se créent depuis son horaire ([ticket 02](../wayfinder/cadrage-mvp/issues/02-seances-presence-rattrapage.md)).

### GRP-01 · Ajouter ou modifier un professeur

En tant que secrétaire, je veux enregistrer les professeurs du centre, afin de leur rattacher des groupes.

**Critères d'acceptation**

- La fiche demande le prénom, le nom et le téléphone ; l'email est facultatif.
- Si un autre professeur a déjà ce téléphone, la plateforme prévient, sans bloquer.
- Le professeur n'a pas de compte : il ne se connecte pas.
- En rôle propriétaire, la fiche montre aussi la part du centre de ce professeur (PRT-04).
- Un professeur qui n'a jamais eu de groupe peut être supprimé (erreur de saisie). Sinon, il peut seulement partir (GRP-03).

Maquette : Professeurs → Ajouter un professeur.

### GRP-02 · Voir les groupes d'un professeur, filtrés par jour

En tant que secrétaire, je veux voir tous les groupes d'un professeur d'un coup d'œil, afin de trouver vite le bon groupe.

**Critères d'acceptation**

- La liste des professeurs montre chacun avec son nombre de groupes ouverts.
- Un clic sur Karim Ben Salah affiche chacun de ses groupes ouverts dans un cadre : nom (« Groupe Samedi 10h → 12h »), matière, niveau, type, nombre d'élèves sur la capacité, et badge de paiement (CAI-02).
- Le filtre « Samedi » n'affiche que les groupes qui ont une séance le samedi.
- Ses groupes fermés et terminés sont rangés à part, dans « Anciens groupes ».

Maquette : Professeurs.

### GRP-03 · Marquer un professeur « parti »

En tant que secrétaire, je veux marquer qu'un professeur a quitté le centre, afin qu'il n'encombre plus la liste, sans perdre son historique.

**Critères d'acceptation**

- « Marquer parti » n'est possible que si le professeur n'a plus de groupe ouvert. Sinon, le message dit « Fermez d'abord ses groupes ».
- Un professeur parti sort de la liste des professeurs. Le filtre « Professeurs partis » montre sa fiche et ses anciens groupes.
- Ses parts non payées restent dans « Parts des professeurs » (PRT-01).
- « Revenir au centre » le remet dans la liste.

Maquette : pas d'écran.

### GRP-04 · Créer un groupe d'Étude

En tant que secrétaire, je veux créer un groupe d'Étude avec son horaire et son prix, afin que ses séances et ses Mois se créent tout seuls pour l'année.

**Critères d'acceptation**

- Le formulaire demande le professeur, la matière, le niveau, un ou plusieurs créneaux par semaine (jour, heure de début, heure de fin), le prix du Mois, la date de début et, en option, la capacité.
- La matière se choisit dans la liste des matières du centre ; une matière nouvelle s'y ajoute en la tapant.
- Le nom se propose tout seul à partir du premier créneau, « Groupe Samedi 10h → 12h » ; la secrétaire peut le changer.
- Les séances sont créées de la date de début à la fin de l'année scolaire : Karim, le samedi de 10h à 12h, à partir du 18/09/2027, donne une séance chaque samedi jusqu'au 24/06/2028.
- Les séances sont rangées en Mois de 4 : Mois 1 = séances 1 à 4, Mois 2 = séances 5 à 8, etc. Avec deux créneaux par semaine, un Mois dure deux semaines.
- Le groupe se modifie ensuite : nom, capacité, prix. Un nouveau prix vaut pour les Mois qui n'ont pas encore commencé : le prix passe de 40 à 45 TND le 01/02, le Mois 5 commencé le 15/01 reste à 40 TND, et le Mois 6 passe à 45 TND.
- Les professeurs, faits avant les groupes ([conception](../superpowers/specs/2026-11-04-professeurs-design.md#4-ce-que-grp-04-devra-ajouter-ici)), attendent cette story pour : compter les groupes ouverts de chacun ; refuser « Marquer parti » à un professeur qui a un groupe ouvert (« Fermez d'abord ses groupes ») ; refuser de supprimer un professeur qui a eu un groupe ; refuser un professeur parti dans un nouveau groupe.

Maquette : fiche groupe (« Le groupe »).

### GRP-05 · Créer un groupe de Révision

En tant que secrétaire, je veux créer une Révision de quelques séances, afin de proposer une préparation courte, par exemple avant un examen.

**Critères d'acceptation**

- Le formulaire demande le professeur, la matière, le niveau, le prix total, le nombre de séances et, en option, la capacité.
- Les dates viennent soit d'un ou plusieurs créneaux par semaine à partir d'une date de début, soit d'une liste de dates et d'heures saisies une à une.
- Le prix d'une séance est le prix total divisé par le nombre de séances, au millime : 50 TND en 3 séances donnent 16,667 + 16,667 + 16,666 TND.
- Après sa dernière séance, la Révision devient un groupe terminé.

Maquette : fiche groupe, type Révision.

### GRP-06 · Créer un groupe de Formation, avec ses tranches

En tant que secrétaire, je veux créer une Formation au prix et au nombre de séances fixés, avec ses tranches, afin que l'élève sache d'avance ce qu'il paie et quand.

**Critères d'acceptation**

- Le formulaire demande le professeur, la matière, le prix total, le nombre de séances, les créneaux ou les dates, et en option le niveau et la capacité.
- Des tranches facultatives : chacune a un montant et la séance où elle est due. Exemple : 300 TND en 3 tranches de 100 TND, dues aux séances 1, 5 et 9.
- La somme des tranches doit faire le prix total : sinon, l'enregistrement est refusé avec l'écart affiché.
- Les tranches sont les mêmes pour tous les élèves du groupe.
- Après sa dernière séance, la Formation devient un groupe terminé.

Maquette : fiche groupe, type Formation.

### GRP-07 · Créer un groupe déjà en cours

En tant que secrétaire d'un centre qui commence SchoolHub en cours d'année, je veux créer un groupe qui a déjà eu des séances, sans recopier le passé, afin de démarrer tout de suite.

**Critères d'acceptation**

- À la création, la case « Ce groupe a déjà commencé » demande où il en est :
  - pour une Étude, le Mois en cours et sa première séance dans SchoolHub, par exemple « Mois 5, à partir du samedi 15/01/2028 » ;
  - pour une Révision ou une Formation, la séance où il en est, par exemple « séance 4 sur 10, le 15/01/2028 ».
- Les séances et les Mois d'avant n'existent pas dans SchoolHub : ses élèves ne doivent rien pour eux.
- Ce qu'un élève a payé avant SchoolHub (assurance, tranche, totalité) se note sur sa fiche (ELV-07).
- Pour une Étude, les parts du professeur commencent au Mois 5. Pour une Révision ou une Formation, la part se calcule à la fin, sur tout le prix du groupe, y compris ce qui a été payé avant SchoolHub.

Maquette : pas d'écran.

### GRP-08 · Fermer un groupe

En tant que secrétaire, je veux fermer un groupe arrêté avant sa fin, afin qu'il ne prévoie plus de séances, en gardant son historique.

**Critères d'acceptation**

- La secrétaire ferme le groupe à une date, avec un motif. Les séances prévues après cette date disparaissent.
- Avant de confirmer, la plateforme liste les élèves encore inscrits et propose, pour chacun, de le changer de groupe (ELV-09). Les inscriptions qui restent sont arrêtées à la date de fermeture (ELV-08).
- Ce qui était dû avant la fermeture reste dû. Pas de remboursement.
- Le groupe sort de la liste des groupes ; il reste dans « Anciens groupes », avec ses séances, présences, paiements et reçus.
- La fermeture va au journal.

Maquette : pas d'écran.
