# Les parts des professeurs

Conception établie le 07/11/2026 pour Lassad et Taha. Elle couvre toute l'épopée [Parts des professeurs](../../user-stories/09-parts-professeurs.md) : PRT-01 à PRT-04, réservées au propriétaire. Les règles viennent du [ticket 05](../../wayfinder/cadrage-mvp/issues/05-paiement-professeurs.md).

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Où vit le code ? | Le calcul des périodes et de leur montant dans `InscriptionsDuCentre` (il a l'échéancier) ; un module `Parts` pour les parts payées, les avances et la part du centre d'un professeur. |
| Les périodes ? | Étude : chaque Mois (« Mois 3 »), ses séances qui comptent. Révision, Formation : le groupe entier (« Toute la Révision »). |
| Quand une ligne naît ? | Quand la période est finie : la dernière séance de la période a eu lieu avant aujourd'hui. Un Mois incomplet (fin d'année, groupe fermé) est fini avec la dernière séance du groupe, si le groupe est fermé ou si plus aucune séance ne vient. Une séance annulée allonge le Mois : sa ligne attend. |
| Le montant du groupe ? | Tout ce que les inscriptions du groupe doivent pour la période, payé ou non : les sommes de l'échéancier (prix spéciaux, prorata, séances manquées d'une Révision payée à la séance), réduction du report comprise (ELV-09). L'assurance n'en fait pas partie. |
| La part du centre ? | Le pourcentage du professeur s'il en a un, sinon celui du centre (CEN-02), sinon 0 %. Un changement vaut pour les périodes qui commencent après le jour du changement : chaque changement est gardé avec sa date. Montant arrondi au millime (5 % de 1 937,500 = 96,875). |
| Les avances ? | Notées avec le professeur, le montant et la date. Elles se déduisent des parts pas encore payées, de la plus ancienne à la plus récente, jusqu'à épuisement. |
| « Payée » ? | Fige la ligne : montant du groupe, pourcentage, part du centre, avance déduite, à verser, et la date. « Non payée » la défige : elle se recalcule. Une part payée n'entre dans aucune caisse. |
| Le résumé ? | Par professeur et pour tout le centre : reste à verser, déjà versé, avances pas encore déduites. |
| La secrétaire ? | Rien : toutes les adresses sont réservées au propriétaire (403), et la page aussi. |

## 1. Les données

- **La part payée** (`parts_payees`) : `groupe_id`, `cle`, `professeur_id`, `montant_du_groupe`, `pourcentage`, `part_du_centre`, `avance_deduite`, `a_verser`, `payee_le`. Une seule par groupe et clé.
- **L'avance** (`avances`) : `professeur_id`, `montant`, `le`.
- **La part d'un professeur** (`parts_des_professeurs`) : `professeur_id`, `pourcentage` (vide : retour à celle du centre), `changee_le`.

Migration `AjouterLesParts`.

## 2. Les adresses

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/parts` | les parts, par professeur | 200, `{ "resteAVerser", "dejaVerse", "avancesRestantes", "professeurs": [{ "id", "nom", "pourcentage", "resteAVerser", "dejaVerse", "avancesRestantes", "lignes": [{ "groupeId", "groupe", "matiere", "cle", "periode", "debut", "fin", "montantDuGroupe", "pourcentage", "partDuCentre", "avanceDeduite", "aVerser", "payee", "payeeLe" }] }] }` |
| `POST /api/parts/paiement` | `{ "groupeId", "cle", "payee" }` | 200, les parts ; 404 « Part introuvable » (période pas finie, ou inconnue) |
| `POST /api/parts/avances` | `{ "professeurId", "montant", "le" }` | 201, les parts ; 400 « Choisissez le professeur », « Le montant de l'avance est positif, au millime près », « Choisissez la date » ; 404 « Professeur introuvable » |
| `GET /api/professeurs/{id}/part-du-centre` | sa part | 200, `{ "pourcentage", "parDefaut", "changeeLe" }` |
| `PUT /api/professeurs/{id}/part-du-centre` | `{ "pourcentage" }` (vide : celle du centre) | 200, sa part ; 400 « La part du centre va de 0 à 100 %, au centième près » |

## 3. Les pages

- **Parts des professeurs** (`/parts`, propriétaire) : les trois chiffres ; « Enregistrer une avance » (professeur, montant, date) ; le tableau, un bloc par professeur (« Karim Ben Salah · part du centre 5 % ») : groupe, période, montant du groupe, part du centre, avance déduite, à verser, « Payée » à cocher.
- **La fiche d'un professeur**, pour le propriétaire : « Part du centre : 5 % (celle du centre) », « Changer ».

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Le Mois fini (montant payé ou non, prix spécial, part, millimes), le Mois pas fini ou allongé par une annulation ; une Révision finie ; payée puis non payée ; une avance déduite, et plus grande que la part ; la part d'un professeur pour les périodes suivantes ; réservé au propriétaire ; un autre centre. |
| Angular | La page (chiffres, tableau, payée, avance), la part du centre sur la fiche du professeur. |
