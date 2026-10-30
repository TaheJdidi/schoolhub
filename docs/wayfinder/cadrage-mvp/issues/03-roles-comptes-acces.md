# 03. Rôles, comptes et accès

Type: grilling
Mode: HITL
Status: resolved
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

> Grilling terminé le 30/10/2026 : toutes les questions sont tranchées, et la synthèse est dans « Réponse », en bas du ticket. [CONTEXT.md](../../../../CONTEXT.md) est à jour.

### Décisions prises (tour 1, 30/10/2026)

- **Q1. Deux rôles fixes : Propriétaire et Secrétaire.** Tout employé qui se connecte à SchoolHub est secrétaire ; il n'y a pas de droits par partie du centre (bibliothèque, achats…).
- **Q2. Pas de compte pour les professeurs, pour le moment.** Conséquence pour le [ticket 02](02-seances-presence-rattrapage.md) : l'appel se fait sur la plateforme par la secrétaire, ou sur la feuille d'appel par le professeur, que la secrétaire recopie ensuite.
- **Q3. Pas d'accès pour les parents ni pour les élèves dans le MVP**, à part le lien vers chaque reçu ([ticket 09](09-canaux-notification.md)). Le portail par lien sécurisé reste pour plus tard.
- **Q4. Un centre peut avoir plusieurs propriétaires**, tous avec les mêmes droits. Il en reste toujours au moins un.
- **Q5. Le propriétaire crée les comptes** avec l'email de la personne, qui choisit son mot de passe grâce à un lien reçu par email. Quand quelqu'un quitte le centre, son compte est désactivé, jamais supprimé : son nom reste sur les paiements, les reçus et les appels qu'il a faits.
- **Q6. Le journal garde qui a fait quoi, et quand**, pour les actions qui touchent à l'argent ou à la présence : encaissements et annulations de reçu, prix spéciaux et dispenses d'assurance, arrêts d'inscription et changements de groupe, corrections de présence, annulations et déplacements de séance, fermeture d'un groupe, création et désactivation des comptes. Seul le propriétaire le voit.
- **Q7. Seul le propriétaire accorde un prix spécial ou une dispense d'assurance.** La secrétaire fait tout le reste du quotidien (séances, appel, corrections de présence), et chaque action est tracée.
- **Q8. La secrétaire ne voit pas** les ressources humaines, les comptes des employés, la rémunération des professeurs, le tableau de bord de rentabilité ni le journal. Elle garde les élèves, les groupes, les séances, les paiements et la caisse, la bibliothèque et les achats.

### Décisions prises (tour 2, 30/10/2026)

- **Q9. Un compte par centre.** Chaque propriétaire ne possède qu'un seul centre. Une personne qui travaillerait dans deux centres aurait un compte dans chacun, avec un email différent ; pas de choix du centre après la connexion.
- **Q10. Pas de circuit de demande de prix spécial.** La secrétaire inscrit l'élève au prix normal et prévient le propriétaire, qui pose le prix spécial plus tard, de préférence avant la séance 2 du Mois.

### Questions du tour 2 (répondues le 30/10/2026)

- **Q9. Une même personne dans plusieurs centres** (un propriétaire qui a deux centres, une secrétaire qui travaille dans deux centres) : (a) un compte par centre, avec un email différent pour chacun ; (b) un seul compte rattaché à plusieurs centres, qui choisit son centre après la connexion. ➡️ (b), avec des droits propres à chaque centre. Ce choix touche la façon dont les données de chaque centre sont séparées : il sera difficile à changer plus tard (ticket 12).
- **Q10. Prix spécial demandé par la secrétaire** : seul le propriétaire en accorde (Q7). Que fait la secrétaire qui inscrit une famille en difficulté en son absence ? (a) Elle inscrit l'élève au prix normal et prévient le propriétaire, qui pose le prix spécial plus tard. (b) Elle enregistre une demande de prix spécial avec la raison, que le propriétaire accepte ou refuse dans la plateforme. ➡️ (a) pour le MVP, sans circuit de demande. Le propriétaire pose le prix avant la séance 2 du Mois, pour que l'élève ne passe pas en orange.

## Réponse

Résolu le 30/10/2026, par grilling. Le détail des échanges est dans « Avancement » ci-dessus ; le vocabulaire est dans [CONTEXT.md](../../../../CONTEXT.md).

### Qui se connecte

- **Deux rôles fixes, Propriétaire et Secrétaire.** Tout employé qui se connecte est secrétaire ; pas de droits par partie du centre.
- **Plusieurs propriétaires possibles** dans un centre, tous avec les mêmes droits ; il en reste toujours au moins un. **Un propriétaire ne possède qu'un centre.**
- **Un compte appartient à un seul centre.** Une personne qui travaille dans deux centres a deux comptes, avec deux emails.
- **Pas de compte pour les professeurs**, pour le moment : l'appel se fait sur la plateforme par la secrétaire, ou sur la feuille d'appel par le professeur.
- **Pas d'accès pour les parents ni pour les élèves** dans le MVP, à part le lien vers chaque reçu.

### Comptes

- **Le propriétaire crée les comptes** avec l'email de la personne, qui choisit son mot de passe grâce à un lien reçu par email.
- **Quand une personne quitte le centre, son compte est désactivé**, jamais supprimé : son nom reste sur ce qu'elle a fait.

### Droits

- **Le propriétaire a accès à tout.** Lui seul accorde un prix spécial ou une dispense d'assurance. Il n'y a pas de circuit de demande : la secrétaire inscrit l'élève au prix normal et prévient le propriétaire.
- **La secrétaire fait tout le quotidien** : élèves, groupes, séances, appel et corrections de présence, paiements et caisse, bibliothèque, achats. Elle ne voit pas les ressources humaines, les comptes des employés, la rémunération des professeurs, le tableau de bord de rentabilité ni le journal.

### Journal

- **Il garde qui a fait quoi, et quand**, pour les encaissements et les annulations de reçu, les prix spéciaux et les dispenses d'assurance, les arrêts d'inscription et les changements de groupe, les corrections de présence, les annulations et les déplacements de séance, la fermeture d'un groupe, la création et la désactivation des comptes.
- **Seul le propriétaire le consulte.**

### Renvoyé à d'autres tickets

- **[Paiement des professeurs](05-paiement-professeurs.md)** : sans compte, comment le professeur reçoit-il sa fiche de rémunération ?
- **[Caisse et reçus](10-caisse-recus.md)** : qui peut annuler un reçu.
- **[Architecture technique](12-architecture-technique.md)** : un compte par centre, deux rôles fixes, journal.

### Plus tard

- Compte professeur (appel sur son téléphone, consultation de sa rémunération).
- Portail parent par lien sécurisé (solde, présences, reçus).
- Droits par partie du centre pour les employés.
- Demande de prix spécial par la secrétaire, validée par le propriétaire.
