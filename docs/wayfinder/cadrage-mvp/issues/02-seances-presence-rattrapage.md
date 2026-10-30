# 02. Séances, présence et rattrapage

Type: grilling
Mode: HITL
Status: claimed (Taha)
Blocked by: 01

## Question

Comment les **Séances** sont-elles créées, et comment la **Présence** et le **Rattrapage** sont-ils suivis ?

- **Création des séances** : générées automatiquement à partir de l'horaire hebdomadaire du groupe ? Comment gérer une séance ajoutée, annulée ou reportée, les jours fériés et les vacances scolaires tunisiennes ?
- **Présence** : qui la saisit (secrétaire, professeur) et à quel moment ? Appel manuel sur une liste, ou pointage par QR code ? Peut-on la corriger après coup ?
- **Rattrapage** : quand un élève assiste à une séance d'un autre groupe, à quelle inscription la séance compte-t-elle, pour le paiement et pour le code couleur ? Faut-il une limite ? Le groupe d'accueil doit-il être du même programme ou du même professeur ?
- **Absence** : une séance manquée reste-t-elle due ? Donne-t-elle droit à un rattrapage ?
- **Changement de groupe** définitif en cours d'année.

## Contexte

- Le cahier des charges impose que la présence et le paiement ne soient pas liés au groupe.
- [Étude de l'existant](../../../../research/etude-de-l-existant.md) : aucun acteur maghrébin ne gère ce modèle « séance » ; c'est le premier trou du marché.
- [Programmes, inscriptions et tarification](01-programmes-inscriptions-tarification.md), Q17c à Q17f : une séance annulée ne compte pas dans le Mois, une séance de remplacement compte à sa place. Ce ticket décide comment on annule, remplace ou ajoute une séance, et comment le centre déclare ses jours de fermeture.

## Avancement

> Grilling commencé le 30/10/2026. [CONTEXT.md](../../../../CONTEXT.md) est à jour avec les décisions prises.

### Décisions prises (tour 1, 30/10/2026)

- **Q1. Séances créées automatiquement** à partir du ou des créneaux hebdomadaires du groupe. Étude : de la date de début du groupe jusqu'à la fin de l'année scolaire fixée par le centre. Révision et Formation : jusqu'au nombre de séances prévu. Pour une Révision aux dates irrégulières, la secrétaire saisit la liste des dates en créant le groupe.
- **Q2. Jours fériés et vacances : pas traités pour le moment.** Il n'y a pas de calendrier de fermeture du centre ; les séances concernées s'annulent à la main (voir Q10).
- **Q3. Pas de règle propre au Ramadan.** L'horaire d'un groupe peut changer à tout moment, à partir d'une date. Cette souplesse couvre le Ramadan : on change l'horaire au début, puis on revient à l'ancien à la fin.
- **Q4. Quatre actions sur les séances** : annuler (avec un motif), déplacer (une annulation et une séance de remplacement en une seule action), ajouter une séance supplémentaire, changer l'horaire du groupe à partir d'une date. À chaque annulation ou déplacement, la plateforme prépare un message WhatsApp (lien `wa.me`) pour les parents du groupe. Qui peut faire quoi : ticket 03.
- **Q5. Appel sur la plateforme ou sur papier.** Sur la plateforme, depuis un téléphone ou un ordinateur ; ou sur une **feuille d'appel** imprimée depuis la plateforme, par exemple celle du groupe 1 pour le Mois 2. Tous les élèves sont présents sauf ceux marqués absents, pour faire le moins de clics possible. Pas de pointage par QR code ni par badge. *Retenu faute d'objection : une séance non annulée compte à sa date, même sans appel ; après l'appel, la plateforme propose un message WhatsApp aux parents des absents.*
- **Q6. La présence se corrige à tout moment.** Chaque changement garde qui l'a fait, quand, et l'ancienne valeur ; le propriétaire peut les consulter.
- **Q7. Rattrapage dans un groupe du même professeur et du même niveau.** Le groupe a donc un **niveau** (voir Q12). *Retenu faute d'objection : chaque rattrapage compense une seule séance précise de son groupe, déjà manquée ou à venir ; pas de délai.*
- **Q8. Le paiement se fait toujours dans le groupe d'origine.** *Retenu faute d'objection : l'élève en rattrapage n'entre ni dans le badge « groupe payé » ni dans le code couleur du groupe d'accueil, et il apparaît à part dans son appel.* La rémunération du professeur qui l'accueille se décide au ticket 05.
- **Q9. Changement de groupe définitif** : arrêt dans l'ancien groupe, inscription dans le nouveau au prorata, et la valeur des séances payées et non suivies est déduite de ce que l'élève doit dans le nouveau groupe. Uniquement entre groupes du même type.

### Questions ouvertes (tour 2)

La recommandation est donnée après ➡️.

- **Q10. Jours fériés en pratique** : sans calendrier, un jour férié avec 15 groupes demande 15 annulations. ➡️ Une annulation groupée : on choisit une date ou une période et les groupes concernés (tous par défaut), et toutes leurs séances sont annulées en une fois, avec le motif. Le calendrier automatique reste pour plus tard.
- **Q11. Feuille d'appel et saisie** : qui recopie la feuille papier dans la plateforme, et quand ? Que contient la feuille ? Comment savoir qu'un appel n'a pas encore été saisi, puisque tout le monde est présent par défaut ? ➡️ Chaque séance est « appel fait » ou « appel à faire », et la secrétaire voit la liste des appels à faire. Elle recopie les absences dès que la feuille revient, idéalement le jour même. La feuille contient les noms, une colonne par séance datée, et des lignes vides pour les élèves en rattrapage ; ni téléphone ni information de paiement, puisqu'elle circule dans la salle.
- **Q12. Niveau et matière du groupe** : liste fixe des niveaux scolaires tunisiens, complétable par le centre, ou texte libre ? Obligatoire pour quels types de groupe ? Un groupe peut-il mélanger deux niveaux ? Un professeur peut-il enseigner deux matières au même niveau, ce qui exigerait une **matière** sur le groupe pour le rattrapage ? ➡️ Liste fixe complétable ; obligatoire pour Étude et Révision, facultatif pour Formation ; un seul niveau par groupe. Ajouter une matière sur le groupe, et exiger même professeur, même niveau et même matière pour un rattrapage.
- **Q13. Changement de groupe : crédit restant** : Ali garde 45 TND de séances non suivies, et le nouveau groupe coûte 40 TND le Mois. Que deviennent les 5 TND en trop ? Le nouveau groupe doit-il avoir le même professeur ? ➡️ Le reste est reporté sur ses paiements suivants dans le nouveau groupe, sans remboursement. N'importe quel groupe du même type, même avec un autre professeur.
- **Q14. Professeur remplaçant** : le professeur est absent, mais un collègue fait la séance à sa place. (a) Impossible : la séance est annulée, puis éventuellement remplacée. (b) Possible : la séance a lieu, compte normalement dans le Mois, et la plateforme garde qui l'a faite. ➡️ (b). Qui est payé pour cette séance se décide au ticket 05.
