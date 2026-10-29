# Carte : cadrage du MVP SchoolHub

## Destination

Un dossier prêt à développer pour le MVP de SchoolHub, qui couvre tout le [cahier des charges](../../cahier-des-charges.md) : glossaire ([CONTEXT.md](../../../CONTEXT.md)), user stories par ordre de priorité, maquette des écrans clés validée, architecture avec ADR, estimation et plan de livraison.

## Notes

- **Domaine** : gestion de centres d'étude en Tunisie, en SaaS multi-centres. Un centre pilote est disponible. Monnaie : TND. Français d'abord, arabe (droite à gauche) prévu dès la conception.
- **Technologies imposées** : Angular 22 (front-end), .NET 10 (back-end).
- **Décisions de départ** : voir [cadrage.md, Décisions prises](../../cadrage.md#décisions-prises).
- **Étude de marché** : [etude-de-l-existant.md](../../../research/etude-de-l-existant.md). À relire avant chaque ticket : elle donne les pratiques des concurrents et les contraintes tunisiennes.
- **Skills** : pour chaque ticket de grilling, utiliser *grilling* et *domain-modeling*, et mettre CONTEXT.md à jour dès qu'un terme est tranché.
- **Centre pilote** : apporter à chaque session ses documents réels (listes d'élèves, registres de présence, carnet de reçus, tarifs, emploi du temps, rémunération des professeurs).
- **Langue** : tout est écrit en français, dans le dépôt.
- **Lire et prendre un ticket** : les tickets sont dans [issues/](issues/). Chaque ticket a un `Type` (grilling, research, prototype, task), un `Mode` (HITL : avec un humain, AFK : agent seul), un `Status` (open, claimed, resolved, ou closed s'il est hors périmètre) et une ligne `Blocked by`. Un ticket est **prenable** s'il est `open` et que tous ses bloquants sont `resolved` ; le plus petit numéro passe en premier. Pour le prendre, passer `Status: claimed (prénom)` avant tout travail. Pour le résoudre, écrire la réponse sous `## Réponse`, passer `Status: resolved`, puis ajouter une ligne dans « Décisions prises » ci-dessous.

## Décisions prises

<!-- une ligne par ticket résolu : [titre](lien) : résumé de la réponse en une ligne -->

- [Programmes, inscriptions et tarification](issues/01-programmes-inscriptions-tarification.md) : pas de Programme, une inscription relie un élève à un groupe de type Étude, Révision ou Formation ; Étude payée par Mois de 4 séances communs au groupe ; Révision à la séance (total ÷ séances) ou en totalité ; Formation en une fois ou en tranches ; ni paiement partiel ni remboursement ; orange à k+1, rouge à k+2.
- [Canaux de notification en Tunisie](issues/09-canaux-notification.md) : MVP en liens `wa.me` manuels plus email (0 TND), API WhatsApp (≈ 8-17 TND/mois) et SMS local en V2 ; WhatsApp ou Messenger reste à confirmer avec le pilote.

## Pas encore spécifié

- **Fonctionnement hors connexion** : appel saisi hors ligne puis synchronisé (PWA). Coût élevé selon l'étude ; à revoir une fois l'architecture choisie.
- **Application mobile ou web responsive seulement** : pour la secrétaire, les professeurs et les parents. Dépend des rôles et de l'architecture.
- **Reprise des données du centre pilote** : import Excel guidé et accompagnement au démarrage.
- **Arabe** : faut-il des reçus et messages bilingues FR/AR dès le MVP, avant l'interface en arabe ? L'étude classe ce point 5ᵉ sur 15 améliorations.
- **Planning des salles** et détection des conflits d'horaire entre groupes, salles et professeurs.
- **Paiement en ligne des familles** (Konnect, Flouci, D17) : l'étude le juge utile plus tard, l'espèce dominant.
- **Exploitation** : sauvegardes, supervision, support du centre pilote.

## Hors périmètre

- **Inscription des centres en libre-service et facturation de l'abonnement SaaS** : écartées du MVP au tour 1 (Q3) ; les comptes des centres sont créés à la main.
- **Grille tarifaire, stratégie commerciale et e-facture TTN des abonnements** : ils concernent la société qui vend SchoolHub, pas le dossier de développement. Les pistes de prix sont dans l'[étude de l'existant](../../../research/etude-de-l-existant.md), section 7.
- **[Cadre légal tunisien](issues/08-cadre-legal-tunisien.md)** (données des mineurs, hébergement, reçus et facture électronique, licence des centres) : pas d'étude juridique ou réglementaire, le projet avance dans tous les cas (décision du 28/10/2026).
- **Alias d'enseignants pour masquer leur identité** : proposés par l'étude à cause du décret 2015-1619. Non retenus, car ils serviraient à contourner une réglementation. La question de ne pas demander certains identifiants (CIN, matricule) reste dans [Parcours d'inscription d'un élève](issues/04-parcours-inscription-eleve.md).
