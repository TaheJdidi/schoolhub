# 14. Estimation et plan de livraison

Type: grilling
Mode: HITL
Status: resolved
Blocked by: 12, 13

## Question

Combien de temps faut-il pour livrer le MVP, et dans quel ordre ?

- **Équipe et échéance** : qui développe (seul ou en équipe, à temps plein ou partiel) ? Quelle échéance (rentrée, saison du bac) ? Ces points sont restés sans réponse au tour 1 (Q7).
- **Lots** : le MVP couvrant tout le cahier des charges, dans quel ordre livrer au centre pilote pour qu'il l'utilise au plus tôt ? Par exemple : élèves, groupes, présence et paiements d'abord.
- **Saisonnalité** : l'étude note des achats à la rentrée et avant le bac, et des résiliations l'été.

## Déjà tranché ailleurs

- **Lots** ([ticket 13](13-user-stories-mvp.md), Q3, 01/11/2026) : une seule livraison, complète. Pas de livraison en plusieurs fois au centre pilote. L'échéance « rentrée 2027-2028 » est avancée par la Q2 ci-dessous.
- **Ce qu'il faut estimer** ([ticket 13](13-user-stories-mvp.md), Q10) : les 78 [user stories](../../../user-stories/README.md), rangées dans l'ordre de construction, plus les [travaux techniques](../../../user-stories/travaux-techniques.md).

## Avancement

> Grilling terminé le 01/11/2026 avec Lassad et Taha : toutes les questions sont tranchées, et la synthèse est dans « Réponse », en bas du ticket. Le plan est dans [docs/plan-de-livraison.md](../../../plan-de-livraison.md).

### Décisions prises (tour 1, 01/11/2026)

- **Q1. Deux développeurs, Taha et Lassad**, avec un nombre d'heures par semaine qui varie selon leurs disponibilités.
- **Q2. Le plus tôt possible, et avant juin 2027.** Pour un centre, la saison commence dès juin : Révisions, Formations d'été et bibliothèque ouverte. Cela avance l'échéance « rentrée 2027-2028 » du ticket 13 ; le centre pilote démarrera donc en cours d'année (GRP-07, ELV-07).
- **Q3. Estimation en jours de travail par story**, avec une fourchette, plus les travaux techniques et 20 % de marge. Une story n'est terminée que si ses critères passent, si ses calculs d'argent ont des tests automatiques, si elle marche sur téléphone et si elle est en production.
- **Q4. Pas de démonstration en cours de route** : l'équipe finit tout, puis présente le travail terminé aux centres.
- **Q5. Les contacts du centre pilote sont Lassad et Taha** ; le reste de la recommandation est retenu : groupe WhatsApp avec réponse le jour même, aide à préparer les fichiers d'import, formation de 2 heures sur place avant la mise en service.

### Estimation proposée (01/11/2026)

[docs/estimation.md](../../../estimation.md) : **113 à 187 jours de travail, soit environ 136 à 225 jours avec la marge de 20 %**, ou 950 à 1 570 heures. Pour être prêts le 01/06/2027, il faut en moyenne 31 à 52 heures par semaine à deux pendant 30 semaines.

### Décisions prises (tour 2, 01/11/2026)

- **Q6 à Q9. Pas d'estimation ni de date pour le moment** : Taha et Lassad n'ont pas d'heures de travail fixes, et ils codent avec Claude pour avancer plus vite. Les chiffres de [docs/estimation.md](../../../estimation.md) restent un ordre de grandeur, sans engagement. Pas de point de contrôle ; la livraison se fait quand tout est fini et testé, en visant avant juin 2027.
- **Q10. Pas de période d'essai chez le centre pilote.** Le principe : **bien tester la plateforme avant de livrer**. Après la livraison, Taha et Lassad corrigent les erreurs signalées. La liste des tests et la façon de corriger sont précisées au tour 3.
- **Q11.** Pas encore de réponse : la question est reposée au tour 3.

### Décisions prises (tour 3, 01/11/2026)

- **Q12. Il faut tout tester** : la liste à cocher est retenue en entier. On ne livre que quand tout est coché.
- **Q13. Corrections après la livraison** : recommandation retenue. Une erreur bloquante est corrigée le jour même ; une erreur gênante, dans la semaine, le soir après la clôture de caisse.
- **Q11 bis. Lassad et Taha** répondent ensemble à ce grilling, comme au ticket 13.

### Questions du tour 3 (répondues le 01/11/2026)

- **Q12. Ce qu'il faut avant de livrer.** ➡️ (1) Les 78 stories sont terminées : leurs critères passent, leurs calculs d'argent ont des tests automatiques, elles marchent sur téléphone et sur ordinateur. (2) Tous les tests automatiques passent, dont ceux de la séparation des centres. (3) Taha et Lassad rejouent tous les critères d'acceptation sur un « Centre de test », l'un sur ordinateur, l'autre sur téléphone. (4) Une semaine type, jouée de bout en bout : demandes d'inscription, appel, encaissement groupé de 20 élèves, reçus imprimés, par WhatsApp et par email, clôture du soir, fin d'un Mois et parts des professeurs, tableau de bord. (5) Les vrais fichiers d'élèves et de professeurs du centre pilote s'importent sans erreur. (6) Une restauration de sauvegarde a été essayée.
- **Q13. Corriger après la livraison.** ➡️ Le centre signale l'erreur dans le groupe WhatsApp. Une erreur qui bloque l'argent ou le travail (connexion, encaissement, reçus, caisse, appel) est corrigée le jour même et mise en ligne dès qu'elle est prête, en prévenant la secrétaire, qui reprend son carnet en attendant. Les autres erreurs sont regroupées et mises en ligne le soir, après la clôture de caisse, dans la semaine.
- **Q11 bis. Prénom.** Le poste s'appelle « Lassad », et la réponse à Q10 dit « moi et Taha » : est-ce Lassad qui répond ? Les tickets 13 et 14 seraient alors à son nom.

### Questions du tour 2 (répondues le 01/11/2026)

- **Q6. Les chiffres.** Une épopée vous paraît-elle sous-estimée ou surestimée ? ➡️ Garder ces chiffres, et les recalculer au point de contrôle (Q8).
- **Q7. Le rythme.** Finir avant juin demande 31 à 52 heures par semaine à deux, soit 16 à 26 heures chacun en moyenne. Est-ce possible, même si les heures varient d'une semaine à l'autre ? C'est un fait à fournir.
- **Q8. Un point de contrôle.** (a) À la fin de l'épopée 04, ou le 31/01/2027 au plus tard, on compare le temps passé à l'estimation et on recalcule la date de fin ; (b) pas de point de contrôle. ➡️ (a).
- **Q9. Si la date de fin dépasse juin.** (a) On garde tout le MVP, et la mise en service se fait dès que tout est fini, même en été : le démarrage en cours d'année le permet (GRP-07) ; (b) on attend la rentrée de septembre 2027 ; (c) on retire des épopées pour tenir juin, ce qui contredit la livraison unique. ➡️ (a).
- **Q10. Deux semaines d'essai avant l'usage réel.** (a) Une fois tout fini, la secrétaire du centre pilote utilise SchoolHub pendant deux semaines en même temps que son carnet de reçus, puis passe à SchoolHub seul ; le développement doit alors finir vers le 15/05/2027 pour une mise en service le 01/06 ; (b) passage direct à SchoolHub. ➡️ (a).
- **Q11. Prénom.** La réponse à Q1 dit « Taha et moi » : les tickets 13 et 14 sont au nom de Taha. Qui répond à ce grilling ?

### Questions du tour 1 (répondues le 01/11/2026)

La recommandation est donnée après ➡️.

- **Q1. Qui développe, et combien de temps ?** Pour chaque personne (Taha, Lassad, d'autres) : heures par semaine, de quand à quand, ce qu'elle connaît déjà (Angular, .NET, PostgreSQL, Azure), et si elle code avec un assistant comme Claude Code. C'est un fait à fournir, pas un choix : il fixe la capacité de l'équipe.
- **Q2. Calendrier cible.** Le centre pilote prend ses inscriptions avant la rentrée (mi-septembre 2027). (a) Développement fini le 30/06/2027 ; juillet et août : essais avec le centre pilote sur ses vraies données, et corrections ; fin août : import des données et formation ; ouverture des inscriptions vers le 23/08 ; rentrée vers le 15/09. (b) Développement jusqu'à fin août, mise en service à la rentrée. ➡️ (a), si le centre pilote confirme la date où il ouvre ses inscriptions.
- **Q3. Méthode d'estimation.** (a) Chaque story est estimée en jours de travail, avec une fourchette (bas, haut), additionnée par épopée, plus les travaux techniques et 20 % de marge ; une story n'est terminée que si ses critères passent, si ses calculs d'argent ont des tests automatiques, si elle marche sur téléphone et si elle est en production ; (b) des points de story et une vélocité, qu'on ne peut pas encore mesurer ; (c) une estimation globale. ➡️ (a) : je propose les chiffres, vous les corrigez.
- **Q4. Démonstrations au centre pilote.** (a) Une démonstration toutes les 6 semaines, sur un « Centre démo » en production avec des données fictives, sans que le pilote s'en serve pour de vrai ; (b) rien avant les essais de juillet. ➡️ (a) : les malentendus se voient tôt, et la livraison reste unique.
- **Q5. Accompagnement et support du centre pilote** (« Pas encore spécifié » de la carte). ➡️ Taha est le contact du centre ; un groupe WhatsApp réunit le propriétaire, la secrétaire, Taha et Lassad, avec une réponse le jour même ; l'équipe aide à préparer les fichiers d'import en août ; une formation de 2 heures sur place avant l'ouverture des inscriptions.

## Réponse

Résolu le 01/11/2026, par grilling avec Lassad et Taha. Le détail des échanges est dans « Avancement » ci-dessus ; le plan complet est dans [docs/plan-de-livraison.md](../../../plan-de-livraison.md).

### Équipe et échéance

- **Lassad et Taha développent**, avec des heures qui varient selon leurs disponibilités, et ils codent avec Claude.
- **Le plus tôt possible, et avant juin 2027** : la saison des centres commence dès juin (Révisions, Formations d'été, bibliothèque). Cela avance l'échéance « rentrée 2027-2028 » du [ticket 13](13-user-stories-mvp.md). Le centre pilote démarrera en cours d'année (GRP-07, ELV-07).
- **Pas d'estimation ni de date fixée pour le moment.** L'[estimation](../../../estimation.md) proposée (environ 136 à 225 jours de travail, marge comprise) reste un ordre de grandeur, sans engagement.

### Livraison

- **Une seule livraison, complète**, sans démonstration en cours de route : l'équipe présente le travail terminé aux centres.
- **Tout tester avant de livrer** : les 78 stories finies, tous les tests automatiques, tous les critères rejoués sur ordinateur et sur téléphone, une semaine type jouée de bout en bout, l'import des vrais fichiers du pilote, une restauration de sauvegarde essayée.
- **Pas de période d'essai** chez le centre pilote.

### Mise en service et support

- **Contacts** : Lassad et Taha, dans un groupe WhatsApp avec le propriétaire et la secrétaire, avec une réponse le jour même.
- **Avant la mise en service** : aide à préparer les fichiers d'import, création du centre, formation de 2 heures sur place.
- **Après la livraison** : une erreur bloquante est corrigée le jour même ; une erreur gênante, dans la semaine.

### Écarté

- Point de contrôle et date de repli, faute d'estimation.
- Démonstrations au centre pilote en cours de développement.
- Deux semaines d'essai en double avec le carnet de reçus.
