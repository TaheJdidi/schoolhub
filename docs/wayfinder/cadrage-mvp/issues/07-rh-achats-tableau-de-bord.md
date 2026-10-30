# 07. RH, achats et tableau de bord

Type: grilling
Mode: HITL
Status: resolved
Blocked by: —

## Question

Jusqu'où vont les modules Ressources humaines et Achats dans le MVP, et que montre le tableau de bord du propriétaire ?

- **RH** : fiche employé, jours de congé (solde, demande, validation), horaires de travail. Les salaires en font-ils partie ? Les professeurs apparaissent-ils dans ce module ?
- **Achats** : catégories Matériel et Service, fournisseur, pièce justificative (photo de la facture), achats récurrents (ménage, jardinage), totaux mensuels et annuels.
- **Assurance du centre** : un achat de type Service, ou un suivi à part avec échéances ?
- **Tableau de bord** : encaissements, impayés, dépenses et bénéfice du mois ; rentabilité par groupe, professeur et programme ?

## Contexte

- [Étude de l'existant](../../../../research/etude-de-l-existant.md), section 8, rang 9 (tableau de bord de rentabilité).
- [Rôles, comptes et accès](03-roles-comptes-acces.md), Q8 : la secrétaire voit les achats, mais ni les ressources humaines ni le tableau de bord de rentabilité.
- [Paiement des professeurs](05-paiement-professeurs.md), Q7 : le professeur reçoit sa part sur le montant du groupe, payé ou non. Les impayés sont une perte pour le centre seul, à faire apparaître dans la rentabilité.
- [Bibliothèque](06-bibliotheque.md) : les entrées à la journée reviennent entièrement au centre.

## Avancement

> Grilling fait le 30/10/2026 en un seul tour : toutes les questions sont tranchées, et la synthèse est dans « Réponse », en bas du ticket. [CONTEXT.md](../../../../CONTEXT.md) est à jour.

### Décisions prises (30/10/2026)

- **Assurance du centre : sans objet.** L'assurance est une somme payée par les élèves ([ticket 01](01-programmes-inscriptions-tarification.md), Q7). Si le centre paie aussi sa propre assurance, c'est un achat de type Service.
- **Q1. RH simples** : une fiche par employé (nom, prénom, email, téléphone, poste), ses horaires de travail de la semaine, et ses jours de congé, notés par le propriétaire avec le total de jours pris dans l'année. Pas de solde, de demande ni de pointage.
- **Q2. Salaires** : le salaire mensuel est noté sur la fiche, et chaque mois le propriétaire le marque « payé ». Le tableau de bord le compte dans les dépenses.
- **Q3. Les professeurs ne sont pas dans les RH** : ils ont leur propre liste et sont payés par leur part ([ticket 05](05-paiement-professeurs.md)). *Retenu faute d'objection : une fiche RH n'est pas un compte ; une secrétaire a les deux, un agent de ménage n'a qu'une fiche.*
- **Q4. Achats** : date, libellé, catégorie (Matériel ou Service) et montant ; fournisseur et photo de la facture facultatifs. Un bouton « refaire cet achat » pour les achats qui reviennent chaque mois, sans répétition automatique. Totaux par mois, par année et par catégorie.
- **Q5. Tableau de bord** du propriétaire, pour un mois du calendrier ou pour l'année : encaissé (détaillé en Étude, Révision, Formation, bibliothèque et assurance), impayés, dépenses (parts des professeurs, salaires, achats) et bénéfice, avec un tableau par groupe (montant du groupe, encaissé, impayés, part du professeur, part du centre) que l'on peut regrouper par professeur. Chaque somme compte dans le mois de sa date : date du paiement, fin du Mois pour une part de professeur, date de l'achat.

### Questions posées (répondues le 30/10/2026)

- **Q1. Jusqu'où vont les RH ?** (a) une fiche, des horaires et des congés notés par le propriétaire ; (b) un solde de congés, des demandes validées par le propriétaire et un pointage. ➡️ (a).
- **Q2. Les salaires font-ils partie du MVP ?** ➡️ Oui : salaire mensuel sur la fiche, marqué « payé » chaque mois.
- **Q3. Les professeurs sont-ils dans les RH ?** ➡️ Non ; une fiche RH n'est pas un compte.
- **Q4. Que note-t-on pour un achat ?** ➡️ Date, libellé, catégorie, montant ; fournisseur et photo facultatifs ; « refaire cet achat » ; totaux.
- **Q5. Que montre le tableau de bord ?** ➡️ Encaissé, impayés, dépenses et bénéfice par mois ou par année, avec le détail par groupe.

## Réponse

Résolu le 30/10/2026, par grilling. Le vocabulaire est dans [CONTEXT.md](../../../../CONTEXT.md).

### Ressources humaines

- **Une fiche par employé** : nom, prénom, email, téléphone, poste, horaires de travail de la semaine et salaire mensuel. Les professeurs n'en font pas partie.
- **Congés** : le propriétaire note les jours de congé, et la fiche montre le total de jours pris dans l'année.
- **Salaires** : chaque mois, le propriétaire marque le salaire « payé ».
- **Une fiche RH n'est pas un compte** : une secrétaire a les deux, un agent de ménage n'a qu'une fiche.
- Seul le propriétaire voit les RH ([ticket 03](03-roles-comptes-acces.md)).

### Achats

- **Date, libellé, catégorie (Matériel ou Service) et montant**, avec le fournisseur et la photo de la facture en option.
- **« Refaire cet achat »** pour ce qui revient chaque mois, sans répétition automatique.
- **Totaux** par mois, par année et par catégorie.
- La secrétaire saisit et voit les achats.
- L'assurance que le centre paie pour lui-même, s'il en a une, est un achat de type Service.

### Tableau de bord

- **Réservé au propriétaire**, pour un mois du calendrier ou pour l'année.
- **Encaissé**, détaillé en Étude, Révision, Formation, bibliothèque et assurance.
- **Impayés** : ce qui était dû et n'a pas été payé. C'est une perte pour le centre seul.
- **Dépenses** : parts des professeurs, salaires et achats.
- **Bénéfice** : l'encaissé moins les dépenses.
- **Tableau par groupe**, regroupable par professeur : montant du groupe, encaissé, impayés, part du professeur et part du centre.
- **Chaque somme compte dans le mois de sa date** : date du paiement, fin du Mois pour une part de professeur, date de l'achat.

### Renvoyé à d'autres tickets

- **[Caisse et reçus](10-caisse-recus.md)** : un achat ou un salaire sort-il de la caisse de la secrétaire ? Les achats qu'elle saisit vont-ils au journal ?
- **[Maquette des écrans clés](11-maquette-ecrans-cles.md)** : le tableau de bord du propriétaire.
- **[Architecture technique](12-architecture-technique.md)** : stockage des photos de factures.

### Plus tard

- Solde de congés, demandes de congé validées par le propriétaire, pointage des heures du personnel.
- Achats qui se répètent automatiquement.
