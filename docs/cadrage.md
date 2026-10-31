# Cadrage du projet SchoolHub

> Document partagé de cadrage. On y trouve l'état d'avancement, les remarques sur le cahier des charges et les réponses du tour 1.
> La suite du travail (décisions à prendre une par une) se passe dans la [carte de décisions](wayfinder/cadrage-mvp/map.md).

| | |
|---|---|
| Besoin initial | [cahier-des-charges.md](cahier-des-charges.md) |
| Glossaire | [CONTEXT.md](../CONTEXT.md) |
| Carte de décisions | [wayfinder/cadrage-mvp/map.md](wayfinder/cadrage-mvp/map.md) |
| Étude de l'existant | [../research/etude-de-l-existant.md](../research/etude-de-l-existant.md) |
| Méthode | *Wayfinder* : on fixe d'abord la destination, puis on tranche les décisions une par une jusqu'à ce que le chemin soit clair |
| Dernière mise à jour | 28/10/2026 |

## Le projet en une phrase

Une application web pour aider les propriétaires de centres d'étude (professeurs, groupes d'élèves, séances hebdomadaires) à gérer leur activité : élèves, professeurs, groupes, présence, paiements, bibliothèque, achats et ressources humaines. Deux rôles principaux : le **propriétaire** (accès à tout, y compris les RH) et la **secrétaire** (accès à tout sauf les RH).

## Avancement

- [x] Lecture du cahier des charges
- [x] Tour 1 : destination fixée (voir [Décisions prises](#décisions-prises))
- [x] Carte de décisions et tickets créés
- [x] Étude de l'existant : l'idée est-elle déjà « consommée » ? (voir [ci-dessous](#étude-de-lexistant--lidée-est-elle-consommée-))
- [ ] Résolution des tickets de la carte, un par un

## Étude de l'existant : l'idée est-elle consommée ?

Résumé de l'[étude complète](../research/etude-de-l-existant.md) (28/10/2026, sources citées ; les points non vérifiés y sont signalés).

**Verdict : l'idée n'est pas consommée en Tunisie, mais elle n'est pas originale au Maghreb.**

| Marché | Verdict |
|---|---|
| **Tunisie** | **Pas saturé.** Aucun SaaS tunisien dédié aux centres d'étude avec un prix public n'a été trouvé. Les acteurs locaux visent les écoles privées (Educanet, Acurschool, TSI-ECOLE, Olvo) ou les centres de formation (EduGestion, Classimum). Le vrai concurrent est Excel, le cahier, le carnet de reçus et WhatsApp. |
| Maroc | **Concurrentiel** : 8 à 10 SaaS dédiés, de 127 à 499 MAD/mois (≈ 40 à 155 TND). Le modèle se vend, et un acteur marocain pourrait entrer en Tunisie. |
| Algérie | **Émergent.** |
| International | **Saturé mais inadapté** : prix en USD ou EUR, paiement par carte, pas d'arabe. |

**Les trois trous du marché tunisien**, que SchoolHub peut occuper :

1. Le modèle « séance » : présence et paiement attachés à l'élève et à la séance (rattrapage dans un autre groupe), code couleur des impayés, badge « groupe payé ».
2. Un outil pour tout le centre : Étude, Révision, Formation, bibliothèque (aucun concurrent ne la gère), dépenses, RH.
3. Le trio caisse en espèces, WhatsApp et paie des professeurs au pourcentage, en TND.

**Améliorations retenues dans la carte** : WhatsApp vers les parents, paiement des professeurs, caisse et reçus numérotés, inscription en ligne avec liste d'attente, portail parent par lien, tableau de bord de rentabilité, règle des 4 séances par mois rendue paramétrable. D'autres pistes (hors connexion, reçus bilingues, planning des salles, paiement en ligne) sont dans « Pas encore spécifié » de la carte.

**Risques principaux** :

- **Prix** : culture du gratuit ou de la licence achetée une fois ; repère de 40 à 160 TND/mois.
- **Juridique et réglementaire** (données des mineurs, hébergement, réglementation des cours de soutien) : sorti du cadrage par décision du 28/10/2026. Les constats de l'étude restent consultables dans ses sections 3.2, 3.3 et 9. L'idée d'alias d'enseignants pour masquer leur identité n'est pas retenue.
- **Saisonnalité** : achats à la rentrée et avant le bac, résiliations l'été.

## Remarques sur le cahier des charges

Points relevés à la lecture, indépendamment de l'étude de marché. Chacun est repris dans un ticket de la carte.

### Modèle de données

- **« payé (bool) » sur l'élève ne suffit pas.** Ce champ ne garde aucun historique. Il ne gère pas non plus le paiement d'une partie seulement, les remises entre frères et sœurs, ni un élève inscrit à deux matières. Il faut enregistrer des paiements par inscription et par période (mois ou séance).
- **Le champ « date » du groupe doit devenir un créneau qui se répète chaque semaine, et générer des séances datées.** Sans séances datées, impossible de faire la présence, le rattrapage dans un autre groupe ou la couleur orange/rouge, qui compte les séances.
- **Les trois espaces Étude, Révision et Formation ont le même modèle.** Mieux vaut un seul concept paramétré qu'un code triplé (voir Q5).

### Fonctionnement au quotidien

- **L'inscription par email avec un objet précis est fragile.** Je propose un formulaire d'inscription public, avec un lien ou un QR code. Il crée une demande que la secrétaire valide.
- **Les reçus par email ne suffiront pas.** Beaucoup d'élèves et de parents ne lisent pas leurs emails. Mieux vaut un reçu PDF ou imprimable, un envoi par WhatsApp ou SMS, et un encaissement groupé (marquer 20 élèves payés en une seule action).

### Fonctions manquantes

- **Le paiement des professeurs.** En général c'est un pourcentage ou un montant fixe par élève ou par séance. C'est souvent ce qui fait le plus mal au propriétaire.
- **Le contact des parents.** Les élèves sont souvent mineurs.
- **Un tableau de bord pour le propriétaire.** Il afficherait les encaissements du mois, les impayés, les dépenses (le module achats) et le bénéfice.
- **Une clôture de caisse quotidienne pour la secrétaire.** On y ajoute un historique qui garde la trace de qui a marqué quel élève comme payé.

## Questions, tour 1 : la destination

Les questions sans réponse explicite prennent la recommandation (➡️), comme convenu le 28/10/2026.

### Q1. Destination de la carte

À quoi ressemble la fin de cette phase de cadrage ? (a) un dossier prêt à développer ; (b) aller jusqu'au MVP codé ; (c) seulement une décision « on y va / on n'y va pas ».

➡️ (a) : le glossaire (CONTEXT.md), les user stories du MVP par ordre de priorité, une maquette des écrans clés validée, l'architecture avec ses décisions techniques documentées (ADR), et une estimation.

**Réponse :** recommandation retenue.

### Q2. Marché et centre pilote

Dans quel pays et quelle ville lance-t-on en premier ? A-t-on un vrai centre pour tester ? Quelles langues ? Quelle monnaie ?

➡️ Partir d'un ou deux centres pilotes réels qu'on connaît. Le français d'abord, mais l'application prévue dès le départ pour l'arabe (écriture de droite à gauche).

**Réponse :** Tunisie. Nous avons un centre pilote pour tester. Monnaie : dinar tunisien (TND). Langues : recommandation retenue (français d'abord, arabe prévu). *Ville du centre pilote : non précisée.*

### Q3. Modèle produit

Une seule plateforme pour tous les centres dès le départ, ou un outil pour un seul centre d'abord ?

➡️ Une architecture qui sépare les données de chaque centre dès le premier jour. En revanche, pas d'inscription en libre-service ni de facturation de l'abonnement dans le MVP : les comptes des centres sont créés à la main.

**Réponse :** recommandation retenue.

### Q4. Périmètre du MVP

Qu'est-ce qui entre dans la première version ?

➡️ MVP : élèves, professeurs, groupes, séances, présence, paiements avec reçus, rôles propriétaire et secrétaire. V2 : bibliothèque. V3 : achats et RH.

**Réponse :** on fait **tout** le cahier des charges dans le MVP. Un professeur peut faire des séances d'étude sur toute l'année, une révision de quelques séances seulement (par exemple 2), ou une formation (par exemple en informatique) dont le prix et le nombre de séances sont fixés à l'avance.

*Conséquence :* l'estimation sera plus grande. Le ticket [Estimation et plan de livraison](wayfinder/cadrage-mvp/issues/14-estimation-plan-livraison.md) proposera un ordre de livraison par lots pour que le centre pilote utilise l'application au plus tôt.

### Q5. Les trois « espaces »

Étude, Révision et Formation ont le même modèle dans le cahier des charges ; seules la durée et le mode de paiement changent. Un élève en Étude maths qui prend aussi une Révision bac maths, est-ce deux inscriptions et deux paiements séparés ?

➡️ Oui. Un seul concept, **Programme**, avec un type (Étude / Révision / Formation), une période et un mode de tarification. Les groupes appartiennent à un programme. Un élève peut avoir plusieurs **Inscriptions**, et les paiements sont liés à l'inscription, pas à l'élève.

**Réponse :** recommandation retenue, complétée par la réponse à Q4 : il existe trois modes de tarification, **mensuel**, **par séance** et **forfait** (prix et nombre de séances fixés à l'avance, cas typique de la Formation).

### Q6. Où ranger la carte de décisions

(a) des fichiers Markdown dans le dépôt ; (b) les issues GitHub du dépôt privé `TaheJdidi/schoolhub`.

➡️ (a) : tout reste en Markdown, au même endroit que ce document.

**Réponse :** recommandation retenue. La carte est dans [docs/wayfinder/cadrage-mvp/](wayfinder/cadrage-mvp/map.md).

### Q7. Contraintes

Qui développe ? Une technologie préférée ? Une échéance ?

**Réponse :** front-end **Angular 22**, back-end **.NET 10**. *Qui développe et l'échéance : non précisés ; ils sont repris dans le ticket d'estimation.*

## Décisions prises

- **Destination (Q1)** : un dossier prêt à développer : CONTEXT.md, user stories priorisées, maquette validée, architecture + ADR, estimation. *(28/10/2026)*
- **Marché (Q2)** : lancement en Tunisie, avec un centre pilote déjà disponible. Monnaie : TND. Français d'abord, arabe (droite à gauche) prévu dès la conception. *(28/10/2026)* **Corrigé le 31/10/2026** ([ticket 12](wayfinder/cadrage-mvp/issues/12-architecture-technique.md), Q6) : français seulement, sans préparation pour l'arabe.
- **Modèle produit (Q3)** : plateforme multi-centres dès le départ, données de chaque centre séparées ; pas d'inscription en libre-service ni de facturation de l'abonnement dans le MVP. *(28/10/2026)*
- **Périmètre (Q4)** : le MVP couvre tout le cahier des charges (Étude, Révision, Formation, bibliothèque, RH, achats). *(28/10/2026)*
- **Modèle du domaine (Q5)** : un concept **Programme** (types Étude, Révision, Formation) ; un élève a plusieurs **Inscriptions** ; les paiements sont liés à l'inscription ; trois modes de tarification : mensuel, par séance, forfait. *(28/10/2026)* **Remplacé le 29/10/2026** par le ticket [Programmes, inscriptions et tarification](wayfinder/cadrage-mvp/issues/01-programmes-inscriptions-tarification.md) : il n'y a pas de Programme ; une inscription relie un élève à un groupe de type Étude, Révision ou Formation, et l'Étude se paie par Mois de 4 séances.
- **Outil de suivi (Q6)** : carte et tickets en Markdown dans `docs/wayfinder/`. *(28/10/2026)*
- **Technologies (Q7)** : Angular 22 et .NET 10. *(28/10/2026)*
- **Juridique** : pas d'étude juridique ou réglementaire, le projet avance dans tous les cas. Le ticket [Cadre légal tunisien](wayfinder/cadrage-mvp/issues/08-cadre-legal-tunisien.md) est hors périmètre. *(28/10/2026)*
