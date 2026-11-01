# Plan de livraison du MVP

Décidé le 01/11/2026 par Lassad et Taha, au [ticket 14](wayfinder/cadrage-mvp/issues/14-estimation-plan-livraison.md).

## L'équipe

- **Lassad et Taha** développent le MVP, avec un nombre d'heures par semaine qui varie selon leurs disponibilités.
- **Ils codent avec Claude**, pour avancer plus vite.

## Quand

- **Le plus tôt possible, et avant juin 2027.** Pour un centre, la saison commence dès juin : Révisions, Formations d'été et bibliothèque ouverte.
- **Pas d'estimation ni de date fixée pour le moment.** L'[estimation](estimation.md) reste un ordre de grandeur de l'effort, sans engagement.
- **Une seule livraison, complète** : les 78 [user stories](user-stories/README.md), construites dans l'ordre des épopées.
- **Pas de démonstration en cours de route** : l'équipe finit tout, puis présente le travail terminé aux centres.
- **Le centre pilote démarrera en cours d'année** : ses groupes se créent « en cours » (GRP-07), et ce qu'il a déjà encaissé se note « payé avant SchoolHub » (ELV-07).

## Avant de livrer : tout tester

On ne livre que quand tout est coché.

- [ ] **Les 78 stories sont finies** : leurs critères passent, leurs calculs d'argent ont des tests automatiques, elles marchent sur téléphone et sur ordinateur.
- [ ] **Tous les tests automatiques passent**, dont ceux qui vérifient qu'un centre ne voit jamais les données d'un autre.
- [ ] **Lassad et Taha rejouent tous les critères des stories** sur un « Centre de test » : l'un sur ordinateur, l'autre sur téléphone.
- [ ] **Une semaine type est jouée du début à la fin** : demandes d'inscription, appel, encaissement groupé de 20 élèves, reçus imprimé, par WhatsApp et par email, clôture du soir, fin d'un Mois avec les parts des professeurs, tableau de bord.
- [ ] **Les vrais fichiers Excel du centre pilote** (élèves et professeurs) s'importent sans erreur.
- [ ] **Une restauration de sauvegarde** a été essayée, et elle marche.
- [ ] **Les préparatifs techniques sont faits** : nom de domaine, SPF et DKIM pour les emails, abonnement Azure ([travaux techniques](user-stories/travaux-techniques.md#à-faire-avant-le-pilote)).

## Mise en service au centre pilote

- **Contacts** : Lassad et Taha.
- **Un groupe WhatsApp** réunit le propriétaire, la secrétaire, Lassad et Taha, avec une réponse le jour même.
- **Avant la mise en service** :
  - l'équipe aide le centre à préparer ses fichiers Excel d'élèves et de professeurs ;
  - elle crée le centre (CEN-01), puis l'aide à importer ses fichiers et à créer ses groupes en cours ;
  - elle donne une formation de 2 heures sur place.
- **Pas de période d'essai** : le centre passe directement à SchoolHub.

## Après la livraison : corriger les erreurs

Le centre signale l'erreur dans le groupe WhatsApp, et Lassad et Taha la corrigent.

| Erreur | Exemple | Délai |
|---|---|---|
| **Bloquante** : l'argent ou le travail s'arrête | impossible d'encaisser, reçu faux, appel ou connexion impossible | **corrigée le jour même**, et mise en ligne dès qu'elle est prête. La secrétaire est prévenue et reprend son carnet en attendant. |
| **Gênante** | une liste mal triée, un texte mal écrit | regroupée avec d'autres, et mise en ligne le soir après la clôture de caisse, **dans la semaine** |
