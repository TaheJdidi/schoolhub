# Ce que l'élève doit

Conception établie le 04/11/2026 pour Lassad et Taha. C'est l'étape 3 de l'épopée [Élèves et inscriptions](../../user-stories/04-eleves-inscriptions.md), après [les inscriptions](2026-11-04-inscriptions-design.md).

## But de cette étape

La fiche de l'élève dit ce qu'il doit à ce jour, inscription par inscription, assurance comprise. Le propriétaire accorde ou retire un prix spécial (ELV-05) et dispense de l'assurance (ELV-06). La secrétaire note ce qui a été payé avant SchoolHub (ELV-07) et regroupe les tranches à venir d'une Formation en une somme due aujourd'hui (ELV-10).

La couleur (CAI-01), l'encaissement et ses reçus (CAI-03), et « Payer d'avance » viennent avec l'épopée Caisse : elle s'appuiera sur les règlements créés ici.

## Décisions

Lassad et Taha ont demandé de travailler sans questions et de prendre chaque fois la recommandation. Les voici.

| Question | Décision |
|---|---|
| Comment savoir qu'une somme est payée ? | Par un **règlement** : la somme (son inscription et sa clé, « Mois 3 », « Tranche 2 », « Totalité »…, ou l'assurance de l'année), le montant réglé, son origine et son auteur. Ici, la seule origine est « payé avant SchoolHub » ; la caisse ajoutera « reçu ». Un règlement garde son montant : un prix spécial ne le change pas. |
| Le prix spécial ? | Sur l'inscription : un montant (au millime, 0 permis) et une raison obligatoire. Il remplace le prix normal pour chaque somme pas encore réglée, en retard ou à venir, Mois en cours compris (exigences communes). Étude : le prix du Mois ; Révision et Formation : le prix total. |
| Le prix spécial d'une Formation en tranches ? | Les tranches pas réglées se partagent ce qui reste du prix spécial une fois retirés les règlements, la dernière prenant la différence ; jamais moins de 0. 300 TND en 3 × 100 TND, prix spécial 150 TND : 3 × 50 TND. |
| Le prix spécial d'une Révision ? | À la séance : le prix spécial divisé par le nombre de séances. En totalité : la part des séances qui restent, comme pour le prix normal. |
| Qui accorde, retire, dispense ? | Le propriétaire seul (403 « Accès réservé au propriétaire »). La secrétaire lit « Seul le propriétaire accorde un prix spécial. Inscrivez au prix normal et prévenez-le. » et « Seul le propriétaire dispense de l'assurance. ». |
| « Payé avant SchoolHub » : pour quoi ? | L'assurance de l'année du centre, tant qu'elle n'est ni réglée ni dispensée. Et, pour une Révision ou une Formation déjà en cours (GRP-07, première séance dans SchoolHub après la 1), une somme pas réglée, de plus de 0 TND, qui était due avant SchoolHub : la totalité ou le paiement en une fois d'un élève dont la première séance est celle du groupe dans SchoolHub, ou une tranche dont la séance vient avant. Une séance « à la séance », ou une tranche due dans SchoolHub, se paie à la caisse. |
| Une séance de Révision « si l'élève est présent » (CAI-01) ? | L'appel n'existe pas encore (SEA) : chaque séance est due à sa date. L'appel la rendra due seulement si l'élève est présent. |
| « Payer tout le reste » (ELV-10) ? | Pour une Formation en tranches : les tranches pas réglées dont la séance vient après aujourd'hui, sauf celle due à la première séance de l'élève (ce qui était dû à son arrivée, ou avant SchoolHub), deviennent une seule somme, « Reste de la Formation », due aujourd'hui. Les autres restent à part. Il faut qu'elles valent plus de 0 TND. Pas de réduction. Toute personne connectée du centre. Avec un prix spécial, un reste payé compte comme ses tranches payées. |
| Le journal ? | Prix spécial accordé ou retiré, dispense d'assurance, payé avant SchoolHub (exigences communes). Le regroupement du reste n'y va pas. |
| « À payer » ? | Ce qui est dû à ce jour (sa séance a eu lieu ou a lieu aujourd'hui), pas réglé, et de plus de 0 TND, groupe par groupe, puis l'assurance, avec le total. Une somme à 0 TND (prix spécial 0) compte comme payée ; un centre dont l'assurance vaut 0 TND n'en réclame pas. |
| Deux onglets ? | Comme à l'étape 2 : la ligne de l'élève est verrouillée pendant chaque action ; une somme ne se règle qu'une fois (index unique). |

## 1. Les données

- **Inscription** : `PrixSpecial` (au millime, facultatif), `RaisonDuPrixSpecial` (200 caractères au plus), `ResteRegroupeLe` (la date de « Payer tout le reste »).
- **Assurance de l'année** : `DispenseeLe` et `RaisonDeLaDispense` (200 caractères au plus).
- **Règlement** (`Reglement`, table `reglements`, `IAppartientAuCentre`) : `InscriptionId` ou `AssuranceId` (l'un des deux), `Cle` (« Mois 3 », « Séance 4 », « Totalité », « Une fois », « Tranche 2 », « Reste », ou « Assurance »), `Montant`, `Origine` (`AvantSchoolHub`), `RegleLe`, `AuteurId`. Un seul règlement par somme : index uniques (`InscriptionId`, `Cle`) et (`AssuranceId`).

## 2. L'échéancier

`Echeancier.Calculer` reçoit en plus le prix spécial, les règlements de l'inscription (par clé) et la date du regroupement du reste. Chaque somme due porte sa clé et dit si elle est réglée ; une somme réglée vaut son règlement. Les libellés ne changent pas, sauf « Reste de la Formation ».

## 3. Les adresses

Toute personne connectée du centre, sauf mention. Un élève, une inscription ou une somme d'un autre centre est introuvable (404).

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/eleves/{id}/a-payer` | ce qu'il doit à ce jour | 200, `{ "lignes": [{ "inscriptionId", "groupe", "libelle", "cle", "montant", "dueLe" }], "total" }` |
| `PUT /api/inscriptions/{id}/prix-special` | **propriétaire** : accorder ou changer, `{ "montant", "raison" }` | 200, l'inscription |
| `DELETE /api/inscriptions/{id}/prix-special` | **propriétaire** : retirer | 200, l'inscription |
| `POST /api/inscriptions/{id}/reste` | « Payer tout le reste » | 200, l'inscription |
| `POST /api/inscriptions/{id}/paye-avant-schoolhub` | `{ "cle" }` | 200, l'inscription |
| `POST /api/eleves/{id}/assurance/dispense` | **propriétaire** : `{ "raison" }` | 200, l'assurance |
| `POST /api/eleves/{id}/assurance/payee-avant-schoolhub` | | 200, l'assurance |

**L'inscription, en JSON**, gagne `prixSpecial`, `raisonDuPrixSpecial`, `reglees` (`[{ "libelle", "montant", "origine": "AvantSchoolHub" }]`), `payablesAvantSchoolHub` (`[{ "cle", "libelle", "montant" }]`) et `peutRegrouperLeReste`. **L'assurance** gagne `etat` (`APayer`, `PayeeAvantSchoolHub`, `Dispensee`) et `raisonDeLaDispense`.

### Les refus

| Cas | Refus |
|---|---|
| Inscription inconnue | 404 « Inscription introuvable » |
| Prix spécial : montant absent, négatif ou au-delà du millime ; raison vide ; raison trop longue | 400 « Le prix spécial doit être positif ou nul, au millime près » ; « Écrivez la raison du prix spécial » ; « La raison fait au plus 200 caractères » |
| Retirer sans prix spécial | 409 « Cette inscription n'a pas de prix spécial » |
| Reste : pas une Formation en tranches ; déjà regroupé ; aucune tranche à venir | 409 « Seule une Formation payée en tranches regroupe son reste » ; « Le reste est déjà regroupé » ; « Il ne reste aucune tranche à venir » |
| Payé avant SchoolHub : clé inconnue ; déjà réglée ; pas permise | 404 « Somme introuvable » ; 409 « Cette somme est déjà payée » ; 409 « Cette somme ne date pas d'avant SchoolHub » |
| Assurance : aucune cette année ; déjà payée ; déjà dispensée ; raison vide ou trop longue | 404 « Aucune assurance cette année » ; 409 « L'assurance est déjà payée » ; 409 « L'assurance est déjà dispensée » ; 400 « Écrivez la raison de la dispense » ; « La raison fait au plus 200 caractères » |

### Le journal

| Action | Détail | Avant → après |
|---|---|---|
| `PrixSpecialAccorde` | « Adem Miled · Groupe Samedi 10h → 12h (Mathématiques) · Famille en difficulté » | « 40,000 TND par Mois » → « 20,000 TND par Mois » (Révision, Formation : « 100,000 TND ») |
| `PrixSpecialRetire` | « Adem Miled · Groupe Samedi 10h → 12h (Mathématiques) » | « 20,000 TND par Mois » → « 40,000 TND par Mois » |
| `AssuranceDispensee` | « Adem Miled · Assurance 2027-2028 · Orphelin » | « À payer » → « Dispensée » |
| `PayeAvantSchoolHub` | « Adem Miled · Groupe … (Physique) · Totalité, séances 4 à 10 », ou « Adem Miled · Assurance 2027-2028 » | « À payer » → « Payé avant SchoolHub, 70,000 TND » |

## 4. La fiche de l'élève

- **« À payer »**, en haut : les lignes du jour (« Mathématiques · Groupe Samedi 10h → 12h », « Mois 3 (3 séances) », « 30,000 TND », « dû le 20/11/2027 »), puis « Assurance 2027-2028 », et le total. Sinon : « Rien à payer aujourd'hui. ».
- **Chaque inscription** : avec un prix spécial, « Prix spécial 20,000 TND au lieu de 40,000 TND (Famille en difficulté) ». Le propriétaire a « Prix spécial » (le formulaire « Prix spécial (TND) », « Raison », « Enregistrer le prix spécial », « Annuler »), et « Retirer le prix spécial » ; la secrétaire lit la phrase du propriétaire, sous la carte. Les sommes réglées : « Tranche 1 · 100,000 TND · payé avant SchoolHub ». Les sommes permises : « Totalité, séances 4 à 10 · 70,000 TND » et « Déjà payé avant SchoolHub », avec confirmation (« Noter « Totalité, séances 4 à 10 » comme payé avant SchoolHub ? Oui · Non »). Une Formation en tranches qui le permet : « Payer tout le reste », avec confirmation (« Regrouper les tranches à venir en une somme due aujourd'hui ? Oui · Non »).
- **L'assurance** : « Année 2027-2028 : 20,000 TND, due le 20/11/2027 » et « à payer », « payée avant SchoolHub » ou « dispensée (Orphelin) ». À payer : « Déjà payée avant SchoolHub » (avec confirmation), et pour le propriétaire « Dispenser » (« Raison », « Dispenser l'assurance », « Annuler ») ; la secrétaire lit « Seul le propriétaire dispense de l'assurance. ».
- Chaque action relit « À payer », l'inscription ou l'assurance ; son refus s'affiche sous sa carte ; ses boutons se bloquent pendant l'envoi.

## 5. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET, l'échéancier | L'exemple d'ELV-05 (Mois 1 à 3 réglés à 40, prix spécial 20 : Mois 4 et suivants à 20) ; prix spécial 0 ; Révision à la séance et en totalité ; Formation 3 × 100 → 3 × 50, et avec la tranche 1 réglée ; le reste regroupé (3 × 100, la 1 réglée → 200 dus aujourd'hui) ; une somme réglée garde son montant. |
| .NET, l'API | « À payer » (dû à ce jour, pas réglé, plus de 0, assurance comprise) ; prix spécial accordé, changé, retiré, refus, réservé au propriétaire, journal ; dispense, refus, propriétaire, journal ; payé avant SchoolHub (assurance, somme permise, refus), journal ; le reste, refus ; isolation entre centres ; deux règlements de la même somme. |
| Angular | « À payer » et « Rien à payer aujourd'hui. » ; le prix spécial (propriétaire : formulaire, envoi exact, retrait ; secrétaire : la phrase) ; payé avant SchoolHub (confirmation, envoi, la somme passe en réglée) ; « Payer tout le reste » ; l'assurance (états, dispense du propriétaire, phrase de la secrétaire) ; refus sous la carte, boutons bloqués. |
