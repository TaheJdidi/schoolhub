# 03. Rôles, comptes et accès

Type: grilling
Mode: HITL
Status: claimed (Taha)
Blocked by: —

## Question

Qui se connecte à SchoolHub, et que peut voir et faire chacun ?

- **Propriétaire et Secrétaire** sont acquis. Le cahier des charges parle aussi d'employés qui gèrent une section précise (bibliothèque, formation, achats) : faut-il des droits par section, ou deux rôles fixes suffisent-ils ?
- **Professeurs** : ont-ils un accès (leurs groupes, saisie de la présence, leur rémunération) ?
- **Parents et élèves** : un portail par lien sécurisé, sans application (solde, présences, reçus) ? Ou aucun accès dans le MVP ?
- Un centre peut-il avoir plusieurs propriétaires ? Que se passe-t-il quand une secrétaire quitte le centre ?
- Quelles actions doivent être tracées (qui a encaissé, qui a annulé un reçu, qui a supprimé un élève) ?

## Contexte

- Tour 1, Q3 de [cadrage.md](../../../cadrage.md).
- [Étude de l'existant](../../../../research/etude-de-l-existant.md), section 8 (portail parent par lien, journal d'audit).
- [Séances, présence et rattrapage](02-seances-presence-rattrapage.md) : décider qui peut annuler, déplacer ou ajouter une séance, changer l'horaire d'un groupe, faire l'appel (le professeur a-t-il un compte ?) et corriger une présence.

## Avancement

> Grilling commencé le 30/10/2026. [CONTEXT.md](../../../../CONTEXT.md) est à jour avec les décisions prises.

### Décisions prises (tour 1, 30/10/2026)

- **Q1. Deux rôles fixes : Propriétaire et Secrétaire.** Tout employé qui se connecte à SchoolHub est secrétaire ; il n'y a pas de droits par partie du centre (bibliothèque, achats…).
- **Q2. Pas de compte pour les professeurs, pour le moment.** Conséquence pour le [ticket 02](02-seances-presence-rattrapage.md) : l'appel se fait sur la plateforme par la secrétaire, ou sur la feuille d'appel par le professeur, que la secrétaire recopie ensuite.
- **Q3. Pas d'accès pour les parents ni pour les élèves dans le MVP**, à part le lien vers chaque reçu ([ticket 09](09-canaux-notification.md)). Le portail par lien sécurisé reste pour plus tard.
- **Q4. Un centre peut avoir plusieurs propriétaires**, tous avec les mêmes droits. Il en reste toujours au moins un.
- **Q5. Le propriétaire crée les comptes** avec l'email de la personne, qui choisit son mot de passe grâce à un lien reçu par email. Quand quelqu'un quitte le centre, son compte est désactivé, jamais supprimé : son nom reste sur les paiements, les reçus et les appels qu'il a faits.
- **Q6. Le journal garde qui a fait quoi, et quand**, pour les actions qui touchent à l'argent ou à la présence : encaissements et annulations de reçu, prix spéciaux et dispenses d'assurance, arrêts d'inscription et changements de groupe, corrections de présence, annulations et déplacements de séance, fermeture d'un groupe, création et désactivation des comptes. Seul le propriétaire le voit.
- **Q7. Seul le propriétaire accorde un prix spécial ou une dispense d'assurance.** La secrétaire fait tout le reste du quotidien (séances, appel, corrections de présence), et chaque action est tracée.
- **Q8. La secrétaire ne voit pas** les ressources humaines, les comptes des employés, la rémunération des professeurs, le tableau de bord de rentabilité ni le journal. Elle garde les élèves, les groupes, les séances, les paiements et la caisse, la bibliothèque et les achats.

### Questions ouvertes (tour 2)

- **Q9. Une même personne dans plusieurs centres** (un propriétaire qui a deux centres, une secrétaire qui travaille dans deux centres) : (a) un compte par centre, avec un email différent pour chacun ; (b) un seul compte rattaché à plusieurs centres, qui choisit son centre après la connexion. ➡️ (b), avec des droits propres à chaque centre. Ce choix touche la façon dont les données de chaque centre sont séparées : il sera difficile à changer plus tard (ticket 12).
- **Q10. Prix spécial demandé par la secrétaire** : seul le propriétaire en accorde (Q7). Que fait la secrétaire qui inscrit une famille en difficulté en son absence ? (a) Elle inscrit l'élève au prix normal et prévient le propriétaire, qui pose le prix spécial plus tard. (b) Elle enregistre une demande de prix spécial avec la raison, que le propriétaire accepte ou refuse dans la plateforme. ➡️ (a) pour le MVP, sans circuit de demande. Le propriétaire pose le prix avant la séance 2 du Mois, pour que l'élève ne passe pas en orange.
