# 08. Bibliothèque (BIB)

La bibliothèque est la salle où les élèves viennent réviser seuls. Seule formule : l'**entrée à la journée**, payée en arrivant, au même prix pour tous ([ticket 06](../wayfinder/cadrage-mvp/issues/06-bibliotheque.md)).

### BIB-01 · Enregistrer une entrée à la journée

En tant que secrétaire, je veux enregistrer et encaisser une entrée à la bibliothèque en quelques secondes, afin de ne pas faire attendre l'élève.

**Critères d'acceptation**

- « Nouvelle entrée » cherche un élève du centre par son nom. Pour une personne de l'extérieur, « Personne de l'extérieur » crée une fiche d'élève sans groupe, avec les mêmes informations obligatoires (ELV-02).
- Le prix est celui des paramètres du centre (CEN-02), le même pour tous : pas de prix spécial.
- L'entrée est payée tout de suite, en espèces : elle entre dans la caisse du jour, et l'heure d'arrivée se note toute seule.
- Un reçu numéroté est créé ; il se remet seulement si la personne le demande (CAI-05).
- Si la capacité est atteinte, la plateforme prévient sans bloquer.
- Si la personne a déjà une entrée aujourd'hui, la plateforme prévient « Adem a déjà une entrée aujourd'hui ».

Maquette : Bibliothèque → Nouvelle entrée.

### BIB-02 · Voir la liste du jour et noter les départs

En tant que secrétaire, je veux voir qui est à la bibliothèque, afin de savoir combien de places il reste.

**Critères d'acceptation**

- La liste du jour montre chaque entrée : nom, « élève du centre » ou « extérieur », heure d'arrivée, heure de départ et reçu.
- « Noter le départ » met l'heure actuelle, que la secrétaire peut corriger. L'heure de départ est facultative : oubliée, elle reste vide.
- En haut : les présents en ce moment (arrivés et pas encore partis) et le nombre d'entrées du jour.

Maquette : Bibliothèque → Liste du jour.

### BIB-03 · Consulter la liste d'un jour passé

En tant que secrétaire, je veux revoir la liste d'un jour passé, afin de savoir qui est venu.

**Critères d'acceptation**

- Un choix de date affiche la liste de ce jour, avec le nombre d'entrées et le total encaissé.
- Elle s'imprime et s'exporte (LST-01, LST-02).

Maquette : pas d'écran.
