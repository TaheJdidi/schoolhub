# 10. RH et achats (RHA)

Les ressources humaines sont réservées au propriétaire et ne concernent pas les professeurs. La secrétaire saisit et voit les achats ([ticket 07](../wayfinder/cadrage-mvp/issues/07-rh-achats-tableau-de-bord.md)).

### RHA-01 · Créer ou modifier la fiche d'un employé

En tant que propriétaire, je veux une fiche pour chaque employé, afin d'avoir son poste, ses horaires et son salaire au même endroit.

**Critères d'acceptation**

- La fiche contient : prénom, nom, email, téléphone, poste, horaires de travail de la semaine et salaire mensuel.
- Une fiche RH n'est pas un compte : Amira, secrétaire, a les deux ; l'agent de ménage n'a qu'une fiche.
- Un employé qui quitte le centre est marqué « parti » : il sort de la liste, et sa fiche, ses congés et ses salaires restent consultables.

Maquette : Ressources humaines (écran à détailler).

### RHA-02 · Noter les jours de congé

En tant que propriétaire, je veux noter les congés de chaque employé, afin de savoir combien de jours il a pris dans l'année.

**Critères d'acceptation**

- « Ajouter un congé » demande les dates et, en option, une note.
- La fiche montre le total de jours pris dans l'année du calendrier.
- Pas de solde de congés ni de demande à valider.

Maquette : Ressources humaines (écran à détailler).

### RHA-03 · Marquer un salaire payé

En tant que propriétaire, je veux marquer chaque mois les salaires payés, afin de ne pas en oublier.

**Critères d'acceptation**

- Pour un mois, la liste montre chaque employé avec son salaire, et « payé » ou « à payer ».
- « Payé » enregistre la date. Le salaire d'octobre compte dans les dépenses d'octobre (TDB-01), même payé le 02/11.
- Un salaire se paie hors caisse.

Maquette : Ressources humaines (écran à détailler).

### RHA-04 · Saisir un achat, payé ou non depuis sa caisse

En tant que secrétaire, je veux enregistrer chaque achat du centre, afin que les dépenses soient suivies et que ma caisse reste juste.

**Critères d'acceptation**

- L'achat demande la date, le libellé, la catégorie (Matériel ou Service) et le montant ; le fournisseur et la photo de la facture sont facultatifs.
- La case « Payé depuis ma caisse » en fait une **sortie de caisse** : les espèces attendues de sa caisse du jour baissent d'autant (CAI-09), et la sortie va au journal.
- Exemple : « Ramettes de papier, Matériel, 45,000 TND, payé depuis ma caisse ».
- L'auteur peut modifier ou supprimer un achat le jour même, avant la clôture de sa caisse ; ensuite, seul le propriétaire le peut.
- L'assurance que le centre paie pour lui-même est un achat de catégorie Service.

Maquette : Achats (écran à détailler).

### RHA-05 · Refaire un achat

En tant que secrétaire, je veux refaire un achat qui revient chaque mois, afin de ne pas tout ressaisir.

**Critères d'acceptation**

- « Refaire cet achat » crée un nouvel achat avec le même libellé, la même catégorie, le même montant et le même fournisseur, à la date du jour, modifiables avant d'enregistrer.
- Rien ne se répète tout seul.

Maquette : Achats (écran à détailler).

### RHA-06 · Voir les totaux des achats

En tant que secrétaire, je veux voir le total des achats par mois, par année et par catégorie, afin de savoir ce que coûte le centre.

**Critères d'acceptation**

- La liste des achats se filtre par mois, par année et par catégorie, avec le total en bas.
- Exemple : octobre 2027, Matériel 320,000 TND, Service 450,000 TND, total 770,000 TND.

Maquette : Achats (écran à détailler).
