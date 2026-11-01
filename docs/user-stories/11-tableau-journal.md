# 11. Tableau de bord et journal (TDB)

Tout ce qui suit est réservé au propriétaire ([ticket 03](../wayfinder/cadrage-mvp/issues/03-roles-comptes-acces.md), [ticket 07](../wayfinder/cadrage-mvp/issues/07-rh-achats-tableau-de-bord.md)).

### TDB-01 · Voir le tableau de bord d'un mois ou d'une année

En tant que propriétaire, je veux voir ce que mon centre a encaissé, dépensé et gagné, afin de savoir s'il est rentable.

**Critères d'acceptation**

- Le propriétaire choisit un mois du calendrier (« Octobre 2027 ») ou l'année scolaire (« Année 2027-2028 »).
- Le tableau montre :
  - **Encaissé** : les espèces reçues dans la période, sans les reçus annulés, détaillées en Étude, Révision, Formation, bibliothèque et assurance ;
  - **Impayés** : ce qui était dû dans la période et n'est toujours pas payé ;
  - **Dépenses** : parts des professeurs, salaires et achats ;
  - **Bénéfice** : l'encaissé moins les dépenses, en rouge s'il est négatif.
- Chaque somme compte dans le mois de sa date : date du paiement, fin du Mois pour une part de professeur, mois du salaire, date de l'achat.
- Ce qui a été payé avant SchoolHub (ELV-07) n'est pas compté dans l'encaissé.

Maquette : Tableau de bord.

### TDB-02 · Voir le détail par groupe et par professeur

En tant que propriétaire, je veux voir ce que rapporte chaque groupe, afin de savoir lesquels marchent.

**Critères d'acceptation**

- Sous le tableau de bord, une ligne par groupe : montant du groupe, encaissé, impayés, part du professeur et part du centre, pour la période choisie.
- « Regrouper par professeur » fait le total par professeur.
- Une ligne de total termine le tableau.

Maquette : Tableau de bord → Par groupe.

### TDB-03 · Voir la liste des prix spéciaux et des dispenses

En tant que propriétaire, je veux voir tous les prix spéciaux et toutes les dispenses d'assurance, afin de savoir ce qu'ils coûtent au centre.

**Critères d'acceptation**

- Une ligne par prix spécial en cours : élève, groupe, prix normal, prix spécial, raison, date et auteur.
- Une ligne par dispense d'assurance de l'année : élève, raison, date et auteur.
- La liste s'imprime et s'exporte (LST-01, LST-02).

Maquette : pas d'écran.

### TDB-04 · Consulter le journal

En tant que propriétaire, je veux voir qui a fait quoi, et quand, sur l'argent et la présence, afin de comprendre une erreur ou un écart.

**Critères d'acceptation**

- Le journal liste les actions de la page [Exigences communes](00-exigences-communes.md#journal), les plus récentes en premier : quand, qui, action et détail (avant, après).
- Il se filtre par personne, par type d'action et par période.
- Exemple : « 12/10/2027 16h42 · Amira Jlassi · Reçu n° 0142 annulé · motif : mauvais élève sélectionné ».
- Personne ne peut modifier ni effacer une ligne.

Maquette : Journal.
