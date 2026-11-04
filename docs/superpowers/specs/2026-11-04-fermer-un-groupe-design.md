# Fermer un groupe

Conception établie le 04/11/2026 pour Lassad et Taha. Elle couvre la dernière story de l'épopée [Professeurs et groupes](../../user-stories/03-professeurs-groupes.md) : [GRP-08](../../user-stories/03-professeurs-groupes.md#grp-08--fermer-un-groupe), après [Révision, Formation et groupe déjà en cours](2026-11-04-revision-formation-design.md).

## But

La secrétaire ferme un groupe arrêté avant sa fin (trop peu d'élèves, professeur qui part) : ses séances à venir disparaissent, il sort de la liste des groupes ouverts et rejoint « Anciens groupes » avec son historique. La fermeture va au journal.

## Périmètre

**Dans cette étape :** fermer un groupe à une date, avec un motif ; supprimer ses séances après cette date ; le ranger dans « Anciens groupes » ; noter la fermeture au journal ; un professeur dont tous les groupes sont fermés ou terminés peut partir.

**Plus tard :**

- la liste des élèves encore inscrits, le changement de groupe proposé pour chacun (ELV-09) et l'arrêt des inscriptions qui restent (ELV-08) : il n'y a pas encore d'élèves ;
- « ce qui était dû reste dû, sans remboursement » : la caisse (CAI) ;
- les présences et les reçus gardés avec le groupe : les séances (SEA) et la caisse.

## Décisions

Lassad et Taha ont demandé de travailler sans questions et de prendre chaque fois la recommandation. Les voici.

| Question | Décision |
|---|---|
| Quelle date de fermeture ? | Aujourd'hui ou avant (date de Tunis) : on ferme un groupe « arrêté ». Pas de limite vers le passé : un groupe qui n'a pas encore commencé se ferme aussi (toutes ses séances disparaissent). La page propose la date du jour. |
| Quelles séances disparaissent ? | Celles **après** la date de fermeture. La séance du jour même reste : elle a eu lieu. |
| Le motif ? | Obligatoire, 200 caractères au plus. |
| Comment s'enregistre la fermeture ? | Deux champs du groupe : `FermeLe` (date) et `MotifDeFermeture`. Fermé ne veut pas dire terminé : une Révision fermée est « Fermé ». |
| Un groupe fermé peut-il changer ? | Non : il ne se modifie plus et ne se ferme pas deux fois (409). Pas de réouverture : la story n'en parle pas. |
| Le journal ? | Une ligne « GroupeFerme » : le détail donne le groupe, son professeur et le motif ; avant « Ouvert », après « Fermé le 15/03/2028 ». |
| Qui ferme ? | Toute personne connectée du centre : la story est celle de la secrétaire. |

## 1. Les données et l'adresse

Le groupe gagne `FermeLe` (`date`, vide tant qu'il est ouvert) et `MotifDeFermeture` (200 caractères au plus).

`POST /api/groupes/{id}/fermeture` avec `{ "date": "2028-03-15", "motif": "Trop peu d'élèves" }` : dans la même transaction, le groupe est fermé, ses séances après la date sont supprimées et la ligne du journal est écrite. Réponse 200, la fiche du groupe.

| Cas | Statut | Message |
|---|---|---|
| Date absente | 400 | « La date de fermeture est obligatoire » |
| Date après aujourd'hui | 400 | « La date de fermeture ne peut pas être après aujourd'hui » |
| Motif vide | 400 | « Écrivez le motif de la fermeture » |
| Motif de plus de 200 caractères | 400 | « Le motif fait au plus 200 caractères » |
| Groupe inconnu ou d'un autre centre | 404 | « Groupe introuvable » |
| Groupe déjà fermé | 409 | « Ce groupe est déjà fermé » |
| `PUT` d'un groupe fermé | 409 | « Ce groupe est fermé : il ne se modifie plus » |

**La fiche** gagne `fermeLe` et `motifDeFermeture` (vides pour un groupe ouvert). **Le résumé** gagne `ferme`.

**Les professeurs** : un groupe fermé n'est plus ouvert ; un professeur dont les groupes sont tous fermés ou terminés peut partir. Supprimer reste refusé dès qu'il a eu un groupe.

## 2. Les pages

**La fiche du groupe** :

- « Fermer le groupe », à côté de « Modifier », ouvre dans la carte « Le groupe » : la date de fermeture (aujourd'hui d'office) et le motif, avec « Les 12 séances prévues après le 15/03/2028 seront supprimées. » (ou « Aucune séance prévue après le … ») selon la date choisie, puis « Annuler » et « Fermer le groupe ».
- Une date vide ou un motif vide ne part pas : « Choisissez la date et écrivez le motif ».
- Réussie : « Groupe fermé » sous la carte ; la pastille « Fermé » à côté du titre ; la carte dit « Fermé le 15/03/2028 : Trop peu d'élèves » ; « Modifier » et « Fermer le groupe » disparaissent.
- Le bouton se bloque pendant l'envoi ; un refus s'affiche sous la carte.

**La fiche du professeur** : « Anciens groupes » montre aussi les groupes fermés, avec la pastille « Fermé ».

## 3. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Fermer : séances après la date supprimées, celle du jour gardée ; fiche et résumé fermés ; ligne du journal (auteur, action, détail, avant, après) ; chaque refus du tableau ; un groupe d'un autre centre introuvable ; un groupe pas encore commencé se ferme et perd toutes ses séances ; PUT refusé ; professeur qui part après la fermeture de son groupe. |
| Angular | Le formulaire de fermeture (date du jour d'office, compte des séances supprimées selon la date), l'envoi exact, « Groupe fermé » et l'affichage fermé, le refus sous la carte, la page qui ne part pas sans motif ; « Anciens groupes » avec « Fermé ». |
