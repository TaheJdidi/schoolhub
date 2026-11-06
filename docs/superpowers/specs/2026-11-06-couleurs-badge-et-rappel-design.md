# Couleurs, badge et rappel de paiement

Conception établie le 06/11/2026 pour Lassad et Taha. C'est l'étape 2 de l'épopée [Caisse et reçus](../../user-stories/07-caisse-recus.md), selon le [découpage](2026-11-06-encaisser-et-le-recu-design.md#le-découpage-de-lépopée).

## But de cette étape

La secrétaire voit d'un coup d'œil qui est en retard : chaque élève a sa couleur (CAI-01), chaque groupe son badge (CAI-02). Pour un élève orange ou rouge, « Envoyer un rappel » prépare le message WhatsApp de ce qu'il doit, pour lui et son parent (CAI-08) ; la date du dernier rappel reste affichée. L'accueil liste les élèves en rouge.

## Le mot « rappel de paiement »

Le 06/11/2026, Lassad a remplacé « relance » par **« rappel de paiement »** : le bouton « Envoyer un rappel », la mention « rappel envoyé le 12/10 », le badge « 3 rappels à envoyer ». [CONTEXT.md](../../../CONTEXT.md) et les user stories sont à jour ; « relance » rejoint les mots à éviter.

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Comment la couleur se calcule ? | Comme ce que doit l'élève : rien ne s'enregistre. Chaque somme pas payée et de plus de 0 TND est due à une séance k (celle de l'échéancier) : le jour même et jusqu'à la séance suivante, « à payer » ; à la séance k+1, **orange** ; à la séance k+2, **rouge**. Les séances sont celles qui comptent (ni annulées, ni supplémentaires). Sans séance suivante, l'élève reste « à payer ». La règle est une fonction pure (`CouleurDesPaiements`). |
| L'assurance ? | Due à la première séance de la première inscription en Étude de l'année ; elle prend la couleur que lui donnent les séances suivantes de ce groupe. |
| La couleur d'un élève ? | La pire de ses inscriptions et de son assurance. Une inscription à prix spécial 0 TND n'a jamais de couleur. Une Révision payée à la séance ne doit pas une séance manquée (SEA-07) : elle ne colore pas. |
| Où la couleur s'affiche ? | Sur la fiche de l'élève (« À payer »), dans la liste des élèves, dans la recherche du haut de l'écran, et pour chaque élève de la fiche d'un groupe. |
| Le badge d'un groupe ? | D'après ses élèves encore inscrits aujourd'hui (pas arrêtés) : « N rappels à envoyer » s'il y a des élèves orange ou rouges ; sinon « N à payer » ; sinon « Groupe payé ». Un élève à 0 TND compte comme payé ; un élève en rattrapage n'est pas une inscription, il ne compte pas. L'assurance n'entre pas dans le badge : elle n'est pas d'un groupe. Pas de badge pour un groupe sans élève. Sur les cadres de la page Professeurs et sur la fiche du groupe ; l'accueil le montrera avec les séances du jour (SEA-01). |
| Le rappel ? | Ce que l'élève doit à ce jour, en un message pour chacun de ses [destinataires](../../user-stories/00-exigences-communes.md#messages) : « Bonjour, il reste à payer pour Adem Miled : Mois 3 de mathématiques, 40,000 TND ; assurance 2027-2028, 20,000 TND. Total : 60,000 TND. Centre Les Jasmins. » Le total n'apparaît qu'avec plusieurs sommes. Rien à payer : 409 « Cet élève n'a rien à payer aujourd'hui ». |
| « Rappel envoyé le » ? | Ouvrir WhatsApp note le moment sur l'élève (`DernierRappelLe`) : la date s'affiche sur sa fiche, à côté du bouton, et dans « Élèves en rouge ». |
| Où le bouton apparaît ? | Sur chaque élève orange ou rouge : sa fiche, la liste du groupe, et « Élèves en rouge » de l'accueil. |
| Le journal ? | Rien : un rappel ne touche ni à l'argent ni à la présence. |

## 1. Les données

**L'élève** gagne `dernier_rappel_le`. Migration `AjouterLesRappels`.

## 2. Les adresses

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/eleves` | la liste et la recherche | chaque élève gagne `couleur` |
| `GET /api/eleves/{id}/a-payer` | ce que l'élève doit à ce jour | gagne `couleur` et `dernierRappelLe` |
| `GET /api/inscriptions?…` | les inscriptions | chacune gagne `couleur` (vide à 0 TND) |
| `GET /api/groupes`, `GET /api/groupes/{id}` | les groupes | chacun gagne `badge` : `{ "etat": "Paye" \| "Rappels" \| "APayer", "nombre" }` |
| `GET /api/eleves/{id}/rappel` | le rappel de paiement | 200, `{ "couleur", "texte", "messages", "dernierRappelLe" }` ; 404 ; 409 |
| `POST /api/eleves/{id}/rappel/envoye` | le rappel est ouvert | 204 ; 404 |
| `GET /api/eleves/en-rouge` | les élèves en rouge | 200, `[{ "id", "prenom", "nom", "dues", "total", "dernierRappelLe" }]` |

## 3. Les pages

- **La pastille** : « À jour », « À payer », « Orange », « Rouge », dans leur teinte ; rien à 0 TND.
- **Le badge** : « Groupe payé », « 3 rappels à envoyer », « 2 à payer ».
- **Le bouton de rappel** : « Envoyer un rappel » ouvre le texte et un « Ouvrir WhatsApp » par destinataire ; ensuite « rappel envoyé le 12/10 ».
- **L'accueil** : « Élèves en rouge · N », avec ce que chacun doit et son bouton de rappel.

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | La règle des couleurs (le jour même, à la séance suivante, à celle d'après, sans séance suivante) ; la couleur de chaque élève et de chaque inscription ; payer remet à jour ; un prix spécial à 0 TND sans couleur ; le badge (rappels, à payer, groupe payé) ; le texte exact du rappel, sa date une fois ouvert ; un élève à jour sans rappel ; les élèves en rouge ; un autre centre. |
| Angular | La pastille, le badge, le bouton de rappel (date du dernier rappel, texte, message ouvert noté, refus) ; les élèves en rouge sur l'accueil. |
