# 01. Programmes, inscriptions et tarification

Type: grilling
Mode: HITL
Status: claimed (Taha)
Blocked by: —

## Question

Quelles sont les règles exactes de chaque **Programme** et de chaque **Mode de tarification** (mensuel, par séance, forfait) ?

- **Étude, au mois** : le « mois » est-il le mois calendaire, ou un cycle de 4 séances qui démarre à la première séance de l'élève ? Que se passe-t-il pour un mois à 5 séances, ou à 3 séances à cause d'un jour férié, du Ramadan ou des vacances ? L'étude recommande de rendre le nombre de séances par mois paramétrable.
- **Révision** : paiement à la séance ou au mois. Qui choisit, et quand ? Faut-il des packs de séances ?
- **Formation, au forfait** : paiement en une fois ou en plusieurs tranches ? Que se passe-t-il en cas d'abandon ?
- **Cas particuliers** : paiement partiel, avance sur plusieurs mois, remises (frères et sœurs, plusieurs matières, prorata en cours de mois), remboursement.
- **Code couleur** : règle exacte du orange (2ᵉ séance non payée) et du rouge (3ᵉ séance) pour chaque mode. Les seuils sont-ils réglables par centre ? Quand le badge « groupe entièrement payé » s'affiche-t-il ?

## Contexte

- Tour 1, Q4 et Q5 de [cadrage.md](../../../cadrage.md) : un élève a plusieurs inscriptions, les paiements sont liés à l'inscription.
- Remarque « payé (bool) ne suffit pas » dans [cadrage.md](../../../cadrage.md).
- [Étude de l'existant](../../../../research/etude-de-l-existant.md), section 8 (remises, packs, crédits de rattrapage ; règle des 4 séances à rendre paramétrable).

## Avancement

> Grilling commencé le 29/10/2026, en pause. Pour reprendre : répondre aux questions ouvertes Q8 à Q17 ci-dessous, puis passer aux sujets du tour suivant. [CONTEXT.md](../../../../CONTEXT.md) est déjà à jour avec les décisions prises.

### Décisions prises (tour 1)

- **Q1. Pas de Programme.** La plateforme n'a pas de notion de programme : une **Inscription** relie un élève à un **Groupe**. Par exemple, un élève s'inscrit dans le groupe 1 du professeur A et dans le groupe 2 du professeur B ; un autre seulement chez le professeur K. *Cela annule le concept de Programme retenu au tour 1 (Q5 de [cadrage.md](../../../cadrage.md)) : à corriger dans cadrage.md quand le ticket sera résolu.*
- **Q2. Le mode de tarification n'est pas un choix : il découle du type du groupe.** Étude : paiement mensuel. Révision : paiement du montant total. Formation : paiement en tranches ou en une fois (Q5).
- **Q3. Remplacée par la correction ci-dessous.** La réponse « ok » portait sur un cycle de N séances propre à chaque élève ; elle a été corrigée.
- **Q4. Paiement à la première séance, saisi par la secrétaire.** L'élève paie toujours à la première séance. S'il paie plusieurs mois d'un coup (par exemple 2), la secrétaire coche à la main les mois concernés.
- **Q5. Formation : en une fois ou en tranches, au choix de l'élève.** Soit il donne le montant total au début, soit il suit un échéancier de paiement bien défini.
- **Q6. Prix spécial.** Le centre peut accorder à un élève un prix spécial, voire la gratuité, pour diverses raisons : famille en difficulté, proche du centre, etc.
- **Q7. Assurance.** C'est une somme annuelle que chaque élève paie au centre, et non l'assurance du centre lui-même.

### Correction du 29/10/2026

Le **mois** reste le mois du calendrier. Pour une Étude, le paiement est toujours basé sur un lot de 4 séances, et ce nombre n'est pas réglable : le conseil de l'étude de l'existant (le rendre paramétrable) n'est pas retenu. Le lien exact entre le mois du calendrier et le lot de 4 séances reste à trancher (Q17).

### Questions ouvertes (tour 2)

La recommandation est donnée après ➡️.

- **Q8. Nom des trois catégories** (Étude, Révision, Formation) : « section », « espace » ou « type de groupe » ? En Tunisie, « section » désigne aussi la filière du bac. ➡️ Type de groupe.
- **Q9. Où se fixe le prix** : sur le Groupe (prix du mois pour une Étude, montant total pour une Révision, montant total et tranches pour une Formation), le même pour tous les élèves du groupe sauf prix spécial ? ➡️ Oui, sur le Groupe. *La seconde moitié de la question (nombre de séances réglable) est tranchée : toujours 4.*
- **Q10. Fin du paiement à la séance, et quand se paie une Révision** : le paiement à la séance du cahier des charges sort-il du MVP ? À quel moment se paie le montant total d'une Révision, et que paie un élève arrivé en cours de route ? ➡️ Il sort du MVP. Montant total à la première séance de l'élève ; un élève arrivé en retard paie aussi le total, sauf prix spécial.
- **Q11. Paiement partiel** (40 TND donnés sur 60) : impossible, ou enregistré avec un reste dû qui compte comme impayé pour la couleur ? ➡️ Possible, et le reste dû compte comme impayé.
- **Q12. Échéancier d'une Formation** : tranches identiques pour tous ou négociées élève par élève ? Dues à une séance donnée ou à une date ? L'élève peut-il changer de choix ? Le paiement en une fois donne-t-il une réduction ? ➡️ Tranches fixées sur le groupe, identiques pour tous, chacune due à une séance donnée. Passage des tranches au paiement total possible à tout moment. Pas de réduction automatique ; sinon, prix spécial.
- **Q13. Prix spécial sur chaque inscription, ou statut « particulier » de l'élève** (un pourcentage sur tous ses groupes) ? La raison est-elle enregistrée ? ➡️ Sur chaque inscription : un montant qui remplace le prix normal (0 = gratuit), avec une raison obligatoire, et une liste de tous les prix spéciaux visible par le propriétaire. Qui peut en accorder : ticket 03.
- **Q14. Remboursements** : le centre rend-il parfois de l'argent (Formation payée puis abandonnée, mois payé d'avance puis départ) ? ➡️ Oui, rarement : une écriture à part, avec sa raison ; un paiement n'est jamais modifié ni supprimé.
- **Q15. Détails de l'assurance** : un seul montant par centre et par année, ou un montant par type de groupe ? Obligatoire pour tous, même pour une Révision de 2 séances ? Payée quand ? Dispense possible ? D'autres frais annuels, comme des frais d'inscription ? ➡️ Un seul montant par centre et par année, obligatoire pour tous, dû à la première séance de la première inscription de l'année. Dispense possible, avec une raison. Pas d'autres frais, sauf si le centre pilote en a.
- **Q16. Absences** : un élève d'Étude qui manque une séance d'un mois payé la perd-il (sauf rattrapage), ou sa période de paiement s'allonge-t-elle ? ➡️ La séance manquée compte quand même ; l'absence est perdue, sauf rattrapage (ticket 02). Formulation à adapter selon la réponse à Q17.
- **Q17. Mois du calendrier et lot de 4 séances.** Un groupe d'Étude « Samedi 10h→12h » coûte 60 TND. Octobre 2026 compte 5 samedis : le 3, le 10, le 17, le 24 et le 31. Ali commence le 3 octobre, Sami le 17.
  - (a) **Lot personnel** de 4 séances à partir de la première séance de l'élève, sans lien avec le calendrier : Ali paie du 3 au 24 octobre, Sami du 17 octobre au 7 novembre.
  - (b) **Lot commun au groupe**, compté 4 par 4 depuis la rentrée : tous paient à la même séance, et un élève arrivé au milieu d'un lot paie les séances restantes.
  - (c) **Mois du calendrier au prix de 4 séances** : Ali paie 60 TND pour octobre (5ᵉ séance comprise) et 60 TND pour novembre, même si un jour férié n'y laisse que 3 séances. Sami paie 3 × 15 = 45 TND pour octobre.

  ➡️ (c), sauf si « toujours 4 séances » veut dire que l'élève reçoit exactement 4 séances pour son prix : ce serait alors (a) ou (b). À préciser aussi : Sami paie-t-il octobre en entier plutôt que 45 TND ?

### Tour suivant

Ces sujets dépendent des réponses du tour 2 : arrêt ou pause d'une inscription, règle orange/rouge pour chaque type de groupe, moment où s'affiche le badge « groupe payé », seuils réglables ou non par centre.
