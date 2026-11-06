# Encaissement groupé

Conception établie le 06/11/2026 pour Lassad et Taha. C'est l'étape 3 de l'épopée [Caisse et reçus](../../user-stories/07-caisse-recus.md), selon le [découpage](2026-11-06-encaisser-et-le-recu-design.md#le-découpage-de-lépopée).

## But de cette étape

Depuis un groupe, la secrétaire voit ce que chaque élève doit à ce jour dans ce groupe, coche ceux qui paient et encaisse en une seule action : un reçu par élève, numérotés à la suite. Les emails partent tout seuls ; « Imprimer tous les reçus » donne un seul fichier, un reçu A5 par page (CAI-04).

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Où vit le code ? | Dans `RecusDuCentre`, à côté de l'encaissement d'un élève (CAI-03), qui partage avec lui le reçu, le règlement, le journal et l'email. |
| Quels élèves ? | Ceux encore inscrits dans le groupe aujourd'hui (pas arrêtés), comme le badge « groupe payé » (CAI-02). Un élève en rattrapage n'est pas une inscription : il n'y est pas. |
| Ce que chacun paie ? | Tout ce qu'il doit à ce jour dans ce groupe : les sommes de son inscription dues aujourd'hui ou avant, pas réglées, de plus de 0 TND ; et son assurance de l'année si elle est due à ce jour (« assurance comprise si elle est due »). Rien d'avance : « Payer d'avance » reste sur la fiche de l'élève. |
| « Déjà à jour » ? | Les élèves du groupe qui ne doivent rien à ce jour (prix spécial à 0 TND compris), listés à part. |
| Le montant ? | Toujours celui du calcul. La page renvoie, pour chaque élève coché, les sommes qu'elle a montrées (inscription et clé) ; le serveur vérifie qu'elles sont toutes encore à payer. Sinon tout est refusé : 409 « Un élève coché a payé entre-temps : rechargez la page ». |
| Tout ou rien ? | Oui : une seule transaction. Les élèves sont verrouillés dans l'ordre de leur identifiant (pas d'interblocage avec un encaissement d'un élève), puis le centre. |
| Les numéros ? | À la suite, dans l'ordre de la page (nom, puis prénom) : 18 élèves cochés donnent les reçus n° 41 à 58. |
| L'email ? | Après l'enregistrement, chaque reçu part à ses destinataires qui ont un email, avec copie au centre, comme pour un élève seul. |
| Imprimer tous les reçus ? | Une page sans le menu (`/recus/impression?ids=…`), un reçu A5 par page ; l'impression du navigateur en fait un seul PDF. La page d'un reçu (`/recus/{id}/impression`) est la même, avec un seul reçu. |
| Le journal ? | Une ligne `Encaissement` par reçu, comme pour un élève seul. |
| Combien d'élèves ? | 100 au plus par encaissement (400 « Cochez au plus 100 élèves »). |
| Un groupe fermé ? | Il peut encore être encaissé : ce qui est dû reste dû. |

## 1. Les données

Rien de nouveau : le reçu, ses lignes et le règlement de CAI-03. Pas de migration.

## 2. Les adresses

Toutes pour une personne connectée du centre.

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/groupes/{id}/encaissement` | ce que chaque élève doit à ce jour dans le groupe | 200, `{ "groupe": { "id", "nom", "matiere", "type" }, "aPayer": [{ "eleveId", "prenom", "nom", "couleur", "sommes": [{ "inscriptionId", "cle", "libelle", "montant" }], "total" }], "aJour": [{ "eleveId", "prenom", "nom" }] }` ; 404 « Groupe introuvable » |
| `POST /api/groupes/{id}/encaissement` | `{ "eleves": [{ "eleveId", "sommes": [{ "inscriptionId", "cle" }] }] }` | 201, `{ "recus": [reçu] }` (le reçu de CAI-03) ; 400 « Cochez au moins un élève », « Cochez au plus 100 élèves » ; 404 « Groupe introuvable » ; 409 « Un élève coché a payé entre-temps : rechargez la page » |

## 3. Les pages

- **La fiche d'un groupe** : « Encaissement groupé », sous le badge.
- **L'encaissement groupé** (`/professeurs/groupes/{id}/encaissement`) : « Qui paie maintenant ? », un élève par ligne avec sa couleur, ce qu'il paie (« Mois 3 + Assurance 2027-2028 ») et son total ; « Tout cocher » / « Tout décocher » ; le nombre d'élèves et le total ; « Encaisser et créer 18 reçus ». À côté, « Déjà à jour ».
- **Après l'encaissement** : « 18 reçus créés », le total encaissé ; un reçu par ligne (numéro, élève, ce qui est payé, montant, « Email envoyé » ou « Pas d'email », « Voir ») ; « Imprimer les 18 reçus » et « Retour au groupe ».

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Ce que chacun doit (Mois dû, assurance due, élève à jour, prix spécial à 0, élève arrêté) ; encaisser deux élèves (reçus à la suite dans l'ordre de la page, lignes, règlements, à jour ensuite, emails avec copie au centre, journal) ; les refus ; tout ou rien quand un élève a payé entre-temps ; un autre centre. |
| Angular | La liste (cochés, « Tout cocher », total) ; l'envoi exact ; les reçus créés ; « Imprimer les reçus » ; le reçu à imprimer avec plusieurs reçus ; le bouton sur la fiche du groupe ; les adresses. |
