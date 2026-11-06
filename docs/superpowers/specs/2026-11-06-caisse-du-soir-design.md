# La caisse du soir

Conception établie le 06/11/2026 pour Lassad et Taha. C'est l'étape 4, la dernière, de l'épopée [Caisse et reçus](../../user-stories/07-caisse-recus.md), selon le [découpage](2026-11-06-encaisser-et-le-recu-design.md#le-découpage-de-lépopée).

## But de cette étape

Le soir, chaque secrétaire compte ses espèces et clôture sa caisse : la plateforme garde l'attendu, le compté et l'écart (CAI-09). Le propriétaire contrôle les clôtures de toutes les secrétaires et coche « argent reçu » quand il récupère les espèces (CAI-10).

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Qu'est-ce qu'une caisse ? | Les reçus d'un compte (secrétaire, ou propriétaire qui encaisse), d'un jour, pas encore clôturés. Chaque reçu garde la clôture qui l'a compté (`cloture_id`) : un reçu fait pendant ou après une clôture reste sans clôture, et ouvre la caisse suivante du jour (« deuxième caisse »). |
| L'encaissé ? | Le total des reçus de la caisse qui ne sont pas annulés. Un reçu annulé avant la clôture n'y compte pas ; il est quand même rangé dans la clôture. |
| Les sorties de caisse ? | Les achats payés en espèces (RHA-04) ne sont pas encore construits : les sorties valent 0 TND, et la clôture garde une colonne `sorties` pour eux. |
| L'attendu, l'écart ? | Attendu = encaissé − sorties ; écart = compté − attendu. Figés à la clôture : une annulation faite ensuite par le propriétaire ne les change pas. |
| Le montant compté ? | 0 ou plus, au millime près (400 « Saisissez le montant compté, au millime près »). |
| Une caisse vide ? | Rien à clôturer : 409 « Rien à clôturer ce jour-là ». |
| Une caisse oubliée ? | Une caisse d'un jour passé, pas clôturée. « Ma caisse » la montre en tête (« Caisse du 05/11 pas clôturée »), et la secrétaire la clôture avec le montant compté, pour ce jour-là. Le propriétaire la voit dans « Caisses pas clôturées ». |
| Après la clôture ? | La secrétaire n'annule plus un reçu de cette caisse : 403 « Votre caisse est clôturée : seul le propriétaire peut annuler ce reçu ». |
| Deux clôtures en même temps ? | La ligne du compte est verrouillée : la seconde ne trouve plus rien à clôturer. |
| « Argent reçu » ? | Le propriétaire le coche, ou le décoche s'il s'est trompé ; la date et l'auteur sont gardés. Une clôture pas encore reçue est « à récupérer ». |
| Le contrôle ? | Réservé au propriétaire : les clôtures de toutes les caisses, les plus récentes en premier, filtrées par période (par défaut les 30 derniers jours) et par compte ; le total « à récupérer » ; les caisses pas clôturées. |
| Le journal ? | `CaisseCloturee` (« Caisse d'Amira Jlassi du 06/11/2026 » ; avant : « Attendu 845,000 TND » ; après : « Compté 840,000 TND, écart −5,000 TND »). |

## 1. Les données

- **La clôture** (`clotures_de_caisse`, `IAppartientAuCentre`) : `compte_id`, `secretaire` (prénom et nom, figés), `jour`, `rang` (1, 2… dans le jour), `encaisse`, `sorties`, `attendu`, `compte`, `ecart`, `cloturee_le`, `argent_recu_le`, `argent_recu_par_id`.
- **Le reçu** gagne `cloture_id`.
- **Le journal** gagne `CaisseCloturee`.

Migration `AjouterLesClotures`.

## 2. Les adresses

| Adresse | Pour | Ce qu'elle fait | Réponse |
|---|---|---|---|
| `GET /api/caisse` | toute personne connectée | sa caisse : les caisses ouvertes (oubliées d'abord, puis celle du jour) et ses clôtures du jour | 200, `{ "ouvertes": [{ "jour", "rang", "encaisse", "sorties", "attendu", "recus", "oubliee" }], "clotures": [clôture] }` |
| `POST /api/caisse/cloture` | toute personne connectée | `{ "jour", "compte" }` | 201, la clôture ; 400 « Choisissez le jour », « Ce jour est à venir », « Saisissez le montant compté, au millime près » ; 409 « Rien à clôturer ce jour-là » |
| `GET /api/caisse/clotures?du=&au=&compteId=` | propriétaire | le contrôle | 200, `{ "clotures": [clôture], "aRecuperer", "nonCloturees": [{ "compteId", "secretaire", "jour", "attendu", "recus" }], "comptes": [{ "id", "nom" }] }` |
| `PUT /api/caisse/clotures/{id}/argent-recu` | propriétaire | `{ "recu": true }` | 200, la clôture ; 404 « Clôture introuvable » |

Une clôture : `{ "id", "jour", "rang", "compteId", "secretaire", "encaisse", "sorties", "attendu", "compte", "ecart", "clotureeLe", "argentRecu", "argentRecuLe" }`.

## 3. Les pages

**La caisse** (`/caisse`) commence par « Ma caisse » :
- Une caisse oubliée en tête, en orange, avec son jour.
- Les chiffres : encaissé (le nombre de reçus), sorties de caisse, espèces attendues.
- « Clôture du soir » : le montant compté, l'écart en direct (« Pas d'écart », « Écart : −5,000 TND »), « Clôturer ma caisse » ; ensuite « Clôturée » avec l'attendu, le compté, l'écart et l'heure, et « Le propriétaire voit votre clôture… ».

Pour le propriétaire, ensuite, « Contrôle des clôtures » : « À récupérer » (total), les caisses pas clôturées, le tableau (jour, secrétaire, attendu, compté, écart en rouge s'il est négatif, « Argent reçu » à cocher), les filtres « Du », « Au », « Secrétaire ».

Les reçus du jour et la recherche restent dessous.

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Ma caisse (encaissé sans les annulés, attendu) ; clôturer (écart, journal, « Clôturée ») ; une deuxième caisse après la clôture ; chacun sa caisse ; une caisse oubliée, clôturée le lendemain ; les refus ; plus d'annulation par la secrétaire après la clôture, toujours par le propriétaire ; le contrôle (filtres, à récupérer, pas clôturées) et « argent reçu » ; réservé au propriétaire ; un autre centre. |
| Angular | Ma caisse (chiffres, écart en direct, clôturer, clôturée, caisse oubliée) ; le contrôle (tableau, écart en rouge, argent reçu, filtres) ; rien pour la secrétaire. |
