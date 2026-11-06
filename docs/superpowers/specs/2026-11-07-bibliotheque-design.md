# La bibliothèque

Conception établie le 07/11/2026 pour Lassad et Taha. Elle couvre toute l'épopée [Bibliothèque](../../user-stories/08-bibliotheque.md) : BIB-01, BIB-02 et BIB-03.

## But de cette étape

La secrétaire enregistre et encaisse une entrée à la journée en quelques secondes, pour un élève du centre ou une personne de l'extérieur ; elle voit qui est là, note les départs, et revoit la liste d'un jour passé.

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Où vit le code ? | Un module `Bibliotheque` : l'entrée (`EntreeALaBibliotheque`) et le service `BibliothequeDuCentre`. Le reçu vient de `RecusDuCentre`. |
| Le prix ? | Celui des paramètres du centre (CEN-02), pour tous. Sans prix fixé : 409 « Le prix de l'entrée n'est pas fixé : voyez les paramètres du centre ». |
| Le reçu ? | Un reçu numéroté à la suite, comme les autres, d'une ligne « Bibliothèque », « Entrée à la bibliothèque (journée) ». Il entre dans la caisse du jour de la secrétaire (CAI-09). Il ne part pas par email : il se remet seulement si la personne le demande (imprimé ou WhatsApp, CAI-05). Pas de règlement : rien n'était dû. |
| Une personne de l'extérieur ? | La page envoie sa fiche (mêmes champs obligatoires qu'ELV-02) ; la fiche et l'entrée s'enregistrent ensemble, tout ou rien. Elle reste un élève sans groupe. |
| Élève du centre ou extérieur ? | « Élève du centre » s'il a au moins une inscription, sinon « extérieur ». |
| Les avertissements ? | Sans bloquer : « La bibliothèque est pleine (30 / 30) : l'entrée est quand même notée. » ; « Adem Miled a déjà une entrée aujourd'hui. » |
| L'arrivée, le départ ? | L'arrivée est l'heure de l'enregistrement, à Tunis. « Noter le départ » met l'heure actuelle, ou celle que la secrétaire corrige (« HH:mm », pas avant l'arrivée) ; le départ reste vide s'il est oublié. |
| Un reçu annulé ? | L'entrée reste dans la liste avec « reçu annulé », mais ne compte ni dans les présents, ni dans les entrées, ni dans l'encaissé. |
| Les présents ? | Les entrées du jour, pas annulées, sans départ. |
| Un jour passé ? | Le même écran, avec un choix de date : la liste, le nombre d'entrées et le total encaissé ; « Imprimer ». L'export viendra avec LST-02. |
| Supprimer une fiche ? | Une fiche qui a une entrée a un historique : elle ne se supprime pas (ELV-11). |

## 1. Les données

**L'entrée** (`entrees_a_la_bibliotheque`, `IAppartientAuCentre`) : `eleve_id`, `jour`, `arrivee_le`, `depart` (heure, facultative), `recu_id`, `montant`. Migration `AjouterLaBibliotheque`.

## 2. Les adresses

Toutes pour une personne connectée du centre.

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/bibliotheque?jour=` | la liste d'un jour (aujourd'hui par défaut) | 200, `{ "jour", "prix", "capacite", "presents", "entrees", "encaisse", "liste": [{ "id", "eleveId", "prenom", "nom", "exterieur", "arrivee", "depart", "recuId", "numero", "annule" }] }` |
| `POST /api/bibliotheque/entrees` | `{ "eleveId" }` ou `{ "fiche": {…} }` | 201, `{ "entree", "recu", "avertissements" }` ; les refus de la fiche (ELV-02) ; 404 « Élève introuvable » ; 409 (prix pas fixé) |
| `PUT /api/bibliotheque/entrees/{id}/depart` | `{ "heure": "17:30" }`, ou rien pour maintenant | 200, l'entrée ; 400 « L'heure de départ s'écrit HH:mm, après l'arrivée » ; 404 « Entrée introuvable » |

## 3. Les pages

**La bibliothèque** (`/bibliotheque`) : « Présents en ce moment » (sur la capacité, « places libres » ou « capacité atteinte »), « Entrées aujourd'hui » et l'encaissé ; « Nouvelle entrée » (chercher un élève du centre, ou « Personne de l'extérieur » avec sa fiche) ; la liste du jour (nom, élève du centre ou extérieur, arrivée, départ ou « Noter le départ », reçu) ; le choix de date et « Imprimer ».

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Une entrée d'un élève du centre (reçu, caisse, pas d'email) ; une personne de l'extérieur (fiche et entrée ensemble ; refus de la fiche, rien d'enregistré) ; les avertissements (déjà venu, pleine) ; sans prix ; noter le départ (maintenant, corrigé, refus) ; la liste du jour et d'un jour passé (présents, entrées, encaissé, reçu annulé) ; la fiche qui ne se supprime plus ; un autre centre. |
| Angular | La page (chiffres, liste, noter le départ), la nouvelle entrée (élève du centre, extérieur, avertissements), un jour passé. |
