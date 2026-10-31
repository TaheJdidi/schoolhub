# 01. Programmes, inscriptions et tarification

Type: grilling
Mode: HITL
Status: resolved
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

> Grilling terminé le 29/10/2026 : toutes les questions sont tranchées, et la synthèse est dans « Réponse », en bas du ticket. [CONTEXT.md](../../../../CONTEXT.md) est à jour.

### Décisions prises (tour 1)

- **Q1. Pas de Programme.** La plateforme n'a pas de notion de programme : une **Inscription** relie un élève à un **Groupe**. Par exemple, un élève s'inscrit dans le groupe 1 du professeur A et dans le groupe 2 du professeur B ; un autre seulement chez le professeur K. *Cela annule le concept de Programme retenu au tour 1 (Q5 de [cadrage.md](../../../cadrage.md)) : à corriger dans cadrage.md quand le ticket sera résolu.*
- **Q2. Le mode de tarification n'est pas un choix : il découle du type du groupe.** Étude : paiement mensuel. Révision : paiement du montant total. Formation : paiement en tranches ou en une fois (Q5). *Corrigé au tour 2 (Q10) : une Révision se paie à la séance ou en totalité, au choix de l'élève.*
- **Q3. Remplacée par la correction ci-dessous.** La réponse « ok » portait sur un cycle de N séances propre à chaque élève ; elle a été corrigée.
- **Q4. Paiement à la première séance, saisi par la secrétaire.** L'élève paie toujours à la première séance. S'il paie plusieurs mois d'un coup (par exemple 2), la secrétaire coche à la main les mois concernés.
- **Q5. Formation : en une fois ou en tranches, au choix de l'élève.** Soit il donne le montant total au début, soit il suit un échéancier de paiement bien défini.
- **Q6. Prix spécial.** Le centre peut accorder à un élève un prix spécial, voire la gratuité, pour diverses raisons : famille en difficulté, proche du centre, etc.
- **Q7. Assurance.** C'est une somme annuelle que chaque élève paie au centre, et non l'assurance du centre lui-même.

### Correction du 29/10/2026

Le **mois** reste le mois du calendrier. Pour une Étude, le paiement est toujours basé sur un lot de 4 séances, et ce nombre n'est pas réglable : le conseil de l'étude de l'existant (le rendre paramétrable) n'est pas retenu. Le lien exact entre le mois du calendrier et le lot de 4 séances reste à trancher (Q17).

### Retour du centre pilote (29/10/2026)

Le propriétaire du centre pilote ne raisonne pas en mois du calendrier. Il numérote les mois de chaque groupe : **Mois 1, Mois 2… Mois n**. Chaque mois est un lot de **4 séances**, et chaque séance a sa date. Par exemple, la séance 3 du Mois 2 a lieu le 5 novembre 2026.

Exemple pour un groupe du jeudi qui a commencé le 24 septembre 2026 :

| Mois du groupe | Séance 1 | Séance 2 | Séance 3 | Séance 4 |
|---|---|---|---|---|
| Mois 1 | 24 sept. | 1ᵉʳ oct. | 8 oct. | 15 oct. |
| Mois 2 | 22 oct. | 29 oct. | **5 nov.** | 12 nov. |
| Mois 3 | 19 nov. | 26 nov. | 3 déc. | 10 déc. |

Le Mois 2 commence en octobre et finit en novembre : il ne correspond à aucun mois du calendrier.

**Avis (Claude)** : c'est une bonne idée, meilleure que le mois du calendrier.

- **L'élève reçoit toujours exactement 4 séances pour son prix.** Plus de problème de mois à 5 jeudis ou à 3 séances.
- **Le code couleur se lit directement.** Si le Mois 2 n'est pas payé à sa séance 2, l'élève passe en orange ; à sa séance 3, en rouge.
- **Le badge « groupe payé » et l'encaissement groupé deviennent naturels**, si tout le groupe partage la même numérotation (Q17a).
- **C'est déjà le langage du centre** : la secrétaire n'a rien à réapprendre.

Cela tranche Q17 en faveur d'un lot de 4 séances, et non du mois du calendrier. La phrase « le mois reste le mois du calendrier » de la correction ci-dessus ne vaut plus. Le mois du calendrier ne sert plus qu'aux bilans (encaissements d'octobre, par exemple). Les questions Q17a à Q17f, plus bas, sont tranchées.

### Décisions prises (tour 2, 29/10/2026)

- **Q8. Type de groupe.** Étude, Révision et Formation sont des **types de groupe**.
- **Q9. Le prix se fixe sur le groupe**, le même pour tous ses élèves sauf prix spécial. Chaque professeur a ses propres tarifs : à nombre d'élèves égal, le groupe d'un professeur peut coûter 2 000 TND et celui d'un autre 1 500 TND. *Le sens exact de ces montants est précisé en Q18.*
- **Q10. Révision : à la séance ou en totalité, au choix de l'élève.** Cela corrige la décision Q2 du tour 1.
- **Q11. Pas de paiement partiel.** L'élève paie toujours le montant entier de ce qui est dû (Mois, totalité, séance ou tranche).
- **Q12. Tranches identiques pour tous les élèves d'une Formation**, fixées sur le groupe. Le reste de la recommandation est considéré comme retenu, faute d'objection : chaque tranche est due à une séance donnée, l'élève peut passer au paiement total à tout moment, et il n'y a pas de réduction automatique.
- **Q13. Prix spécial sur chaque inscription.** Un élève peut avoir un prix spécial dans le groupe 1 et le prix normal dans le groupe 2. La raison obligatoire et la liste visible par le propriétaire sont retenues, faute d'objection.
- **Q14. Pas de remboursement.** Le centre ne rend jamais d'argent.
- **Q15. Assurance réservée aux groupes d'Étude.** Le montant est annuel, et chaque centre fixe le sien. *Détails en Q20.*
- **Q16. Une séance manquée par l'élève compte.** Elle est perdue, sauf rattrapage dans un autre groupe.
- **Q17 et Q17a à Q17f** : voir « Retour du centre pilote » et les réponses dans la liste ci-dessous.

### Questions du tour 2 (répondues le 29/10/2026)

La recommandation est donnée après ➡️.

- **Q8. Nom des trois catégories** (Étude, Révision, Formation) : « section », « espace » ou « type de groupe » ? En Tunisie, « section » désigne aussi la filière du bac. ➡️ Type de groupe.
- **Q9. Où se fixe le prix** : sur le Groupe (prix du Mois pour une Étude, montant total pour une Révision, montant total et tranches pour une Formation), le même pour tous les élèves du groupe sauf prix spécial ? ➡️ Oui, sur le Groupe. *La seconde moitié de la question (nombre de séances réglable) est tranchée : toujours 4.*
- **Q10. Fin du paiement à la séance, et quand se paie une Révision** : le paiement à la séance du cahier des charges sort-il du MVP ? À quel moment se paie le montant total d'une Révision, et que paie un élève arrivé en cours de route ? ➡️ Il sort du MVP. Montant total à la première séance de l'élève ; un élève arrivé en retard paie aussi le total, sauf prix spécial.
- **Q11. Paiement partiel** (40 TND donnés sur 60) : impossible, ou enregistré avec un reste dû qui compte comme impayé pour la couleur ? ➡️ Possible, et le reste dû compte comme impayé.
- **Q12. Échéancier d'une Formation** : tranches identiques pour tous ou négociées élève par élève ? Dues à une séance donnée ou à une date ? L'élève peut-il changer de choix ? Le paiement en une fois donne-t-il une réduction ? ➡️ Tranches fixées sur le groupe, identiques pour tous, chacune due à une séance donnée. Passage des tranches au paiement total possible à tout moment. Pas de réduction automatique ; sinon, prix spécial.
- **Q13. Prix spécial sur chaque inscription, ou statut « particulier » de l'élève** (un pourcentage sur tous ses groupes) ? La raison est-elle enregistrée ? ➡️ Sur chaque inscription : un montant qui remplace le prix normal (0 = gratuit), avec une raison obligatoire, et une liste de tous les prix spéciaux visible par le propriétaire. Qui peut en accorder : ticket 03.
- **Q14. Remboursements** : le centre rend-il parfois de l'argent (Formation payée puis abandonnée, mois payé d'avance puis départ) ? ➡️ Oui, rarement : une écriture à part, avec sa raison ; un paiement n'est jamais modifié ni supprimé.
- **Q15. Détails de l'assurance** : un seul montant par centre et par année, ou un montant par type de groupe ? Obligatoire pour tous, même pour une Révision de 2 séances ? Payée quand ? Dispense possible ? D'autres frais annuels, comme des frais d'inscription ? ➡️ Un seul montant par centre et par année, obligatoire pour tous, dû à la première séance de la première inscription de l'année. Dispense possible, avec une raison. Pas d'autres frais, sauf si le centre pilote en a.
- **Q16. Absences** : un élève d'Étude qui manque une séance d'un mois payé la perd-il (sauf rattrapage), ou sa période de paiement s'allonge-t-elle ? ➡️ La séance manquée compte quand même ; l'absence est perdue, sauf rattrapage (ticket 02). Avec les Mois de 4 séances (Q17) : le Mois ne s'allonge pas pour l'élève absent, contrairement à une séance annulée par le professeur (Q17c).
- **Q17. Mois du calendrier et lot de 4 séances.** Un groupe d'Étude « Samedi 10h→12h » coûte 60 TND. Octobre 2026 compte 5 samedis : le 3, le 10, le 17, le 24 et le 31. Ali commence le 3 octobre, Sami le 17.
  - (a) **Lot personnel** de 4 séances à partir de la première séance de l'élève, sans lien avec le calendrier : Ali paie du 3 au 24 octobre, Sami du 17 octobre au 7 novembre.
  - (b) **Lot commun au groupe**, compté 4 par 4 depuis la rentrée : tous paient à la même séance, et un élève arrivé au milieu d'un lot paie les séances restantes.
  - (c) **Mois du calendrier au prix de 4 séances** : Ali paie 60 TND pour octobre (5ᵉ séance comprise) et 60 TND pour novembre, même si un jour férié n'y laisse que 3 séances. Sami paie 3 × 15 = 45 TND pour octobre.

  ➡️ (c), sauf si « toujours 4 séances » veut dire que l'élève reçoit exactement 4 séances pour son prix : ce serait alors (a) ou (b). À préciser aussi : Sami paie-t-il octobre en entier plutôt que 45 TND ?

  **Réponse de principe (centre pilote, 29/10/2026)** : un lot de 4 séances numéroté Mois 1, Mois 2… (voir « Retour du centre pilote »). Ce n'est donc pas (c). Le choix entre (a) et (b) se fait en Q17a.
- **Q17a. Numérotation commune au groupe ou propre à chaque élève ?** Le Mois 2 est-il le même pour tous les élèves du groupe (du 22 octobre au 12 novembre), ou chaque élève a-t-il son propre Mois 1 à partir de sa première séance ? ➡️ Commune au groupe. C'est ce que suggère l'exemple du propriétaire (une séance datée appartient au Mois 2 du groupe). C'est aussi la seule façon d'avoir un badge « groupe payé pour le Mois 2 » et un encaissement groupé. **Retenu le 29/10/2026.**
- **Q17b. Élève qui arrive au milieu d'un mois.** Sami arrive le 5 novembre, à la séance 3 du Mois 2 (60 TND le mois). Paie-t-il 2 séances × 15 = 30 TND, puis le prix plein à partir du Mois 3 ? Ou le Mois 2 entier ? ➡️ 30 TND pour les 2 séances restantes, puis prix plein. À vérifier avec la pratique actuelle du centre. **Retenu le 29/10/2026.**
- **Q17c. Séance annulée** (professeur absent, jour férié, vacances). Elle ne compte pas dans le mois, qui se termine alors une semaine plus tard ? Et une séance remplacée à une autre date compte-t-elle ? ➡️ Une séance annulée ne compte pas ; une séance de remplacement compte à sa place. L'élève reçoit ainsi toujours 4 séances. **Retenu le 29/10/2026.**
- **Q17d. Fin de l'année scolaire.** La numérotation repart-elle à Mois 1 à chaque rentrée ? Que se passe-t-il si le dernier mois de l'année n'a que 2 séances ? ➡️ Elle repart à Mois 1 à chaque rentrée. Un dernier mois incomplet se paie au prorata des séances, comme en Q17b. **Retenu le 29/10/2026.**

  **Exemples d'application de Q17c** (groupe du jeudi, tableau du « Retour du centre pilote ») :
  - **Professeur absent le 5 novembre, séance non remplacée** : la séance est annulée et ne compte pas, et il n'y a pas d'appel ce jour-là. Le Mois 2 devient 22 oct., 29 oct., 12 nov. et 19 nov. ; le Mois 3 commence le 26 novembre. Toutes les dates suivantes se décalent d'une semaine.
  - **Professeur absent le 5 novembre, remplacé le samedi 7 novembre** : la séance du 7 devient la séance 3 du Mois 2. Le Mois 2 finit toujours le 12 novembre et rien ne se décale.
  - **Élève absent le 5 novembre**, la séance ayant bien eu lieu : elle compte pour lui (Q16). Il peut la rattraper dans un autre groupe (ticket 02).
  - **Jour férié ou vacances** : même règle qu'une séance annulée, pour tous les groupes concernés. La façon dont le centre déclare ses jours de fermeture se décide au ticket 02.
- **Q17e. Séance en plus.** Le professeur ajoute une séance sans en avoir annulé, par exemple avant un examen. Compte-t-elle dans le Mois (le Mois finit plus tôt et le paiement suivant arrive plus tôt) ? Est-elle offerte, hors du compte des 4 ? Ou se paie-t-elle à part ? ➡️ En ajoutant la séance, la secrétaire indique si elle remplace une séance annulée (elle compte alors à sa place) ou si elle s'ajoute (elle est offerte et ne compte pas dans le Mois). **Retenu le 29/10/2026.**
- **Q17f. Vocabulaire.** Dans le glossaire, « Rattrapage » désigne un élève qui assiste à la séance d'un autre groupe. Une séance ajoutée par le professeur pour tout son groupe est autre chose. ➡️ « Séance de remplacement » quand elle remplace une séance annulée, « Séance supplémentaire » quand elle s'ajoute. « Rattrapage » reste réservé à l'élève. **Retenu le 29/10/2026.**

### Décisions prises (tour 3, 29/10/2026)

- **Q18. Prix par élève**, fixé sur chaque groupe. Un même professeur peut donc avoir plusieurs tarifs (point non contesté).
- **Q19. Le prix d'une séance de Révision est le prix total divisé par le nombre de séances** (100 TND pour 4 séances, donc 25 TND). Retenu faute d'objection : chaque séance se paie le jour même. Comme le prix est le même, passer de la séance à la totalité revient à payer d'avance les séances restantes ; rien ne l'empêche.
- **Q20. Assurance payée une seule fois** par élève et par année scolaire, même avec plusieurs groupes d'Étude. Retenu faute d'objection : elle est due à la première séance de sa première inscription en Étude de l'année, et une dispense est possible avec une raison.
- **Q21. Pas de remboursement** à l'arrêt. Retenu faute d'objection : la secrétaire marque l'arrêt à une date, plus rien n'est dû ensuite, et il n'y a pas de pause dans le MVP.
- **Q22. Une seule règle orange/rouge, avec des seuils fixes.** Une somme due à la séance k fait passer l'élève en orange à la séance k+1 et en rouge à la séance k+2. Par conséquent, la règle s'applique aussi à l'assurance.
- **Q23. Badge « groupe payé »** : il s'affiche quand tous les élèves encore inscrits ont payé ce qui est dû à ce jour. Un élève à prix spécial 0 compte comme payé.

### Questions du tour 3 (répondues le 29/10/2026)

La recommandation est donnée après ➡️.

- **Q18. Sens du prix d'un groupe.** « Un groupe de 10 élèves qui coûte 2 000 TND » : est-ce **200 TND par élève et par Mois** (le total de 2 000 TND n'étant qu'une conséquence) ? Ou un **prix total du groupe partagé** entre les élèves, chacun payant moins si un 11ᵉ élève arrive ? Un même professeur peut-il avoir des prix différents selon ses groupes (par exemple bac et collège) ? ➡️ Un prix **par élève** (par Mois pour une Étude), fixé sur chaque groupe ; un même professeur peut donc avoir plusieurs tarifs.
- **Q19. Révision payée à la séance.** Le prix d'une séance est-il le total divisé par le nombre de séances (100 TND pour 4 séances, donc 25 TND la séance) ? Ou un prix à part, souvent un peu plus cher (30 TND) ? La séance se paie-t-elle le jour même ? L'élève peut-il passer de la séance à la totalité en cours de route ? ➡️ Deux prix fixés sur le groupe : le prix total et le prix d'une séance. Chaque séance se paie le jour même. Le choix se fait à l'inscription, sans changement en cours de route dans le MVP.
- **Q20. Détails de l'assurance.** Un élève inscrit dans deux groupes d'Étude paie-t-il l'assurance une seule fois dans l'année ? À quel moment est-elle due ? Peut-on en dispenser un élève ? ➡️ Une fois par élève et par année scolaire, due à la première séance de sa première inscription en Étude de l'année. Dispense possible, avec une raison.
- **Q21. Arrêt d'une inscription.** Un élève quitte son groupe d'Étude après la séance 2 du Mois 3. Le Mois 3, déjà payé, est perdu (pas de remboursement, Q14) et rien n'est plus dû après ? Faut-il aussi une « pause » (par exemple un mois de maladie) ? ➡️ La secrétaire marque l'arrêt à une date : plus rien n'est dû ensuite et l'élève sort de la liste d'appel. Pas de pause dans le MVP : l'élève qui revient est réinscrit et paie au prorata (Q17b).
- **Q22. Règle orange/rouge pour tous les types de groupe.** Proposition d'une seule règle : une somme due à la séance k (Mois d'Étude, totalité ou séance de Révision, tranche de Formation) fait passer l'élève en **orange à la séance k+1** et en **rouge à la séance k+2** si elle n'est pas payée. Pour une Étude, cela redonne la règle du cahier des charges : Mois dû à la séance 1, orange à la 2, rouge à la 3. Les seuils sont-ils fixes, ou réglables par centre ? ➡️ Une seule règle pour tous les types de groupe, avec des seuils fixes dans le MVP, comme le nombre de 4 séances par Mois.
- **Q23. Badge « groupe payé ».** Il s'affiche quand tous les élèves actifs du groupe ont payé ce qui est dû à ce jour (le Mois en cours pour une Étude) ? Un élève à prix spécial 0 compte-t-il comme payé ? ➡️ Oui aux deux.

## Réponse

Résolu le 29/10/2026, par grilling avec Taha et grâce au retour du propriétaire du centre pilote. Le détail des échanges est dans « Avancement » ci-dessus ; le vocabulaire est dans [CONTEXT.md](../../../../CONTEXT.md).

### Modèle

- **Pas de notion de Programme.** Une **Inscription** relie un élève à un **Groupe**. Chaque groupe a un **type de groupe** : Étude, Révision ou Formation.
- **Le prix se fixe sur le groupe, par élève.** Chaque professeur a ses tarifs, et un même professeur peut avoir des prix différents selon ses groupes.
- **Prix spécial** : sur une inscription précise, il remplace le prix normal et peut valoir 0. La raison est obligatoire, et le propriétaire voit la liste de tous les prix spéciaux.

### Étude : paiement par Mois

- **Mois 1, Mois 2…** sont numérotés depuis la rentrée et communs à tout le groupe. Chaque Mois compte exactement 4 séances (nombre fixe).
- **Le Mois se paie à sa séance 1.** L'élève peut payer plusieurs Mois d'avance : la secrétaire coche les Mois concernés.
- **Arrivée en cours de Mois** : l'élève paie les séances restantes au prorata. Un dernier Mois incomplet en fin d'année se paie aussi au prorata. La numérotation repart à Mois 1 à chaque rentrée.
- **Séances** :
  - une séance annulée ne compte pas, et le Mois s'allonge ;
  - une séance de remplacement compte à sa place ;
  - une séance supplémentaire est offerte et ne compte pas ;
  - une séance manquée par l'élève compte (sauf rattrapage dans un autre groupe).
- **Assurance** : montant annuel fixé par chaque centre. Elle se paie une fois par élève et par année, à la première séance de sa première inscription en Étude. Dispense possible, avec une raison.

### Révision

- **À la séance ou en totalité, au choix de l'élève.**
- **Prix d'une séance = prix total ÷ nombre de séances.** Chaque séance se paie le jour même ; la totalité se paie à la première séance de l'élève.

### Formation

- **En une fois ou en tranches, au choix de l'élève.**
- **Les tranches sont fixées sur le groupe** et identiques pour tous, chacune due à une séance donnée.
- L'élève peut passer au paiement total à tout moment. Pas de réduction automatique.

### Règles communes

- **Pas de paiement partiel** : l'élève paie toujours le montant entier de ce qui est dû.
- **Pas de remboursement.**
- **Arrêt d'une inscription** à une date : plus rien n'est dû ensuite. Pas de pause : un élève qui revient est réinscrit et paie au prorata.
- **Code couleur** : une somme due à la séance k (Mois, séance, totalité, tranche ou assurance) fait passer l'élève en orange à la séance k+1, puis en rouge à la séance k+2, si elle n'est pas payée. Les seuils sont fixes.
- **Badge « groupe payé »** : tous les élèves encore inscrits ont payé ce qui est dû à ce jour. Un prix spécial 0 compte comme payé.

### Écarté

- **Nombre de séances par Mois paramétrable** (conseil de l'étude de l'existant).
- **Paiement à la séance** pour l'Étude et la Formation.
- **Concept de Programme** (tour 1 du cadrage).
