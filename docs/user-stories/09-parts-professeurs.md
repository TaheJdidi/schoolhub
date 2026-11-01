# 09. Parts des professeurs (PRT)

Tout ce qui suit est réservé au propriétaire. Le centre garde sa part sur le **montant du groupe**, que les élèves aient payé ou non ; le professeur reçoit le reste, moins ses avances ([ticket 05](../wayfinder/cadrage-mvp/issues/05-paiement-professeurs.md)). Les parts se paient hors caisse.

### PRT-01 · Voir les parts à payer

En tant que propriétaire, je veux savoir combien je dois à chaque professeur, et pour quoi, afin de le payer juste.

**Critères d'acceptation**

- Une ligne par groupe et par période, créée quand la période est finie : après chaque Mois d'une Étude (ses 4 séances faites), après la dernière séance d'une Révision ou d'une Formation.
- Chaque ligne montre : groupe, période, montant du groupe, part du centre, avance déduite, montant à verser, et « payée » ou « non payée ».
- Le montant du groupe compte ce que chaque élève doit pour la période, prix spéciaux et prorata compris, qu'il ait payé ou non. Un élève à 0 TND compte pour 0 ; l'assurance n'en fait pas partie.
- Exemple : 10 élèves à 200 TND pour le Mois 2, dont un n'a pas payé. Le montant du groupe est 2 000,000 TND, la part du centre (5 %) 100,000 TND, et le professeur reçoit 1 900,000 TND.
- Les millimes sont gardés : 5 % de 1 937,500 TND font 96,875 TND.
- Par professeur, un résumé montre le reste à verser, le déjà versé et les avances pas encore déduites.
- Une séance annulée allonge le Mois, et retarde donc sa ligne.

Maquette : Parts des professeurs.

### PRT-02 · Marquer une part payée

En tant que propriétaire, je veux marquer une part comme payée, afin de savoir ce qui reste à verser.

**Critères d'acceptation**

- Sur une ligne, « Payée » enregistre la date ; « Non payée » la remet à verser, en cas d'erreur.
- Le professeur n'a pas de compte : le propriétaire lui communique le montant lui-même.
- Une part payée n'entre pas dans la caisse d'une secrétaire.

Maquette : Parts des professeurs → Payée / Non payée.

### PRT-03 · Enregistrer une avance

En tant que propriétaire, je veux noter l'argent avancé à un professeur, afin qu'il soit déduit de sa part suivante.

**Critères d'acceptation**

- « Nouvelle avance » demande le professeur, le montant et la date.
- L'avance est déduite de la prochaine part de ce professeur. Si elle est plus grande que la part, le reste est déduit des parts suivantes.
- Exemple : Karim reçoit une avance de 300 TND le 05/11 ; sa part du Mois 3, de 1 900 TND, devient 1 600 TND à verser.

Maquette : Parts des professeurs → Nouvelle avance.

### PRT-04 · Changer la part du centre pour un professeur

En tant que propriétaire, je veux fixer une part du centre différente pour un professeur, afin de respecter ce que nous avons convenu ensemble.

**Critères d'acceptation**

- Sur la fiche du professeur, la part du centre vaut d'abord celle du centre (CEN-02), par exemple 5 %. Le propriétaire la change pour Karim, par exemple à 10 %.
- La nouvelle part vaut pour les périodes qui commencent après le changement ; une période déjà commencée garde l'ancienne part. Une Formation commencée en janvier reste à 5 % ; celle qui commence en mars passe à 10 %.
- La secrétaire ne voit pas cette part.

Maquette : pas d'écran.
