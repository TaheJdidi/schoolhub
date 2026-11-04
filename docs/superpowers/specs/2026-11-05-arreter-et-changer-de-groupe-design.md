# Arrêter une inscription et changer de groupe

Conception établie le 05/11/2026 pour Lassad et Taha. C'est l'étape 4, la dernière, de l'épopée [Élèves et inscriptions](../../user-stories/04-eleves-inscriptions.md), après [ce que l'élève doit](2026-11-04-ce-que-l-eleve-doit-design.md).

## But de cette étape

La secrétaire arrête l'inscription d'un élève qui ne vient plus (ELV-08) : rien n'est dû après la date de l'arrêt. Elle passe un élève dans un autre groupe du même type (ELV-09) : l'ancienne inscription s'arrête, la nouvelle commence, et la valeur des séances payées et pas suivies est déduite de ce qu'il doit dans le nouveau groupe.

## Décisions

Lassad et Taha ont demandé de travailler sans questions et de prendre chaque fois la recommandation. Les voici.

| Question | Décision |
|---|---|
| Que veut dire « arrêté le 25/01 » ? | Le 25/01 est le dernier jour compté : une somme due ce jour-là ou avant reste due, une somme due après ne l'est plus. Une somme déjà payée reste payée, même après l'arrêt : pas de remboursement. L'exemple d'ELV-08 tient : le Mois 5, dû le 15/01, reste dû en entier. |
| Quelle date ? | Aujourd'hui par défaut ; une autre date est permise. Avant la première séance de l'élève, plus rien n'est dû : c'est ainsi qu'on défait une inscription faite par erreur. |
| Réinscrire un élève arrêté ? | Oui (ELV-08) : une nouvelle inscription, au prorata. Un élève n'a qu'une inscription **pas arrêtée** par groupe (l'index unique ne compte plus que celles-là). |
| Qui compte comme inscrit ? | Pour le nombre d'élèves et la capacité : une inscription pas arrêtée, ou arrêtée à une date qui n'est pas encore passée. Pour « Déjà inscrit » : une inscription pas arrêtée, comme l'index unique. |
| Le changement de groupe : à quelle date ? | À la première séance de l'élève dans le nouveau groupe, choisie comme pour une inscription. L'ancienne inscription s'arrête la veille. |
| Le même type ? | Le nouveau groupe est du même type que l'ancien (ELV-09), ouvert, pas terminé, et l'élève n'y est pas déjà inscrit. Le mode se choisit comme pour une inscription. |
| La valeur des séances payées et pas suivies ? | Pour chaque somme réglée de l'ancienne inscription, la part de ses séances qui tombent à la date du changement ou après : son montant réglé × ces séances ÷ ses séances, au millime. Une Formation (en une fois, en tranches, ou son reste) compte ensemble tout ce qui est réglé, sur toutes les séances de l'élève. L'exemple d'ELV-09 tient : le Mois 5 payé 40 TND, le changement après la séance 1 : 30 TND. |
| Comment la déduire ? | La nouvelle inscription garde ce **report**. Il réduit ses sommes une à une, de la première à la dernière, jusqu'à s'épuiser : « le surplus est reporté sur ses paiements suivants ». L'exemple d'ELV-09 : la 2ᵉ séance d'un Mois à 48 TND, 36 TND moins 30 TND, soit 6 TND. Une somme réglée garde sa part du report. |
| Le journal ? | L'arrêt et le changement de groupe (exigences communes). |
| Le message WhatsApp d'un changement ? | Le même que pour une inscription : le nouveau groupe et sa première séance. |
| Un groupe fermé ? | Rien de plus pour l'instant : ses séances après la fermeture disparaissent déjà, donc plus rien n'y est dû. La liste des élèves avant de fermer, et le changement proposé, restent à faire avec la fermeture (mémoire « Fermer un groupe »). |

## 1. Les données

- **Inscription** : `ArreteLe` (date, facultative) et `Report` (au millime, 0 par défaut). L'index unique (`EleveId`, `GroupeId`) ne compte plus que les inscriptions dont `ArreteLe` est vide.

## 2. L'échéancier

- `ConditionsDeLInscription` gagne `ArreteLe` et `Report`. Une somme pas réglée dont la date vient après `ArreteLe` disparaît. Le report réduit les sommes, dans l'ordre des dates, chacune de sa part ; une somme réglée garde la sienne.
- `Echeancier.ValeurNonSuivie(groupe, conditions, depuisLe)` : la valeur des séances réglées et pas suivies à partir de cette date.

## 3. Les adresses

Toute personne connectée du centre. Ce qui est d'un autre centre est introuvable (404).

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `POST /api/inscriptions/{id}/arret` | arrêter, `{ "date" }` | 200, l'inscription |
| `POST /api/inscriptions/{id}/changement` | changer de groupe, `{ "groupeId", "mode", "debutLe" }` | 201, `{ "inscription", "aPayerALArrivee", "avertissement", "texte", "messages", "report" }` |

L'inscription, en JSON, gagne `arreteLe` et `report`. `groupes-possibles` gagne le paramètre `depuis` (une inscription) : seulement les groupes du même type, sans le groupe de départ.

### Les refus

| Cas | Refus |
|---|---|
| Inscription inconnue ; déjà arrêtée | 404 « Inscription introuvable » ; 409 « Cette inscription est déjà arrêtée » |
| Arrêt sans date | 400 « Choisissez la date de l'arrêt » |
| Changement : pas de groupe ; groupe inconnu ; le même ; un autre type | 400 « Choisissez le nouveau groupe » ; 404 « Groupe introuvable » ; 409 « L'élève est déjà dans ce groupe » ; 409 « Le nouveau groupe doit être du même type » |
| Changement : groupe fermé, terminé, déjà inscrit, mode, première séance | les refus de l'inscription (étape 2) |
| Changement avant sa première séance dans l'ancien groupe | 400 « Le changement vient avant sa première séance dans l'ancien groupe » |

### Le journal

| Action | Détail | Avant → après |
|---|---|---|
| `InscriptionArretee` | « Adem Miled · Groupe Samedi 10h → 12h (Mathématiques) » | « Depuis le 20/11/2027 » → « Arrêtée le 25/01/2028 » (l'inscription : sans accord sur l'élève) |
| `ChangementDeGroupe` | « Adem Miled · Groupe Samedi 10h → 12h (Mathématiques) → Groupe Mardi 17h → 19h (Mathématiques) · report de 30,000 TND » | « Groupe Samedi 10h → 12h » → « Groupe Mardi 17h → 19h, depuis le 18/01/2028 » (coupés à 100 caractères) |

## 4. Les pages

- **La fiche de l'élève**, sur chaque inscription pas arrêtée : « Arrêter l'inscription » (« Date de l'arrêt », aujourd'hui d'office, et « Rien ne sera dû après cette date ; ce qui était dû avant reste dû. Pas de remboursement. », puis « Arrêter l'inscription » et « Annuler ») et « Changer de groupe » (vers `/eleves/{id}/changement/{inscription}`). Arrêtée : « Inscription arrêtée le 25/01/2028 », sans action. Avec un report : « Report de 30,000 TND depuis l'ancien groupe ».
- **Le changement de groupe** (`/eleves/{id}/changement/{inscription}`) : la page d'inscription, titre « Changer Adem Miled de groupe », sous-titre « Depuis Groupe Samedi 10h → 12h · Mathématiques », les groupes du même type ; « Changer de groupe » ; la confirmation dit « Changement enregistré », « Report de 30,000 TND depuis l'ancien groupe » s'il y en a un, puis comme une inscription.
- **Le groupe**, carte « Les élèves » : une inscription arrêtée garde sa ligne, avec « arrêtée le 25/01/2028 » ; le nombre d'élèves ne la compte plus une fois la date passée.

## 5. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET, l'échéancier | L'exemple d'ELV-08 (Mois 5 dû en entier) ; une somme payée après l'arrêt reste réglée ; un arrêt avant la première séance ; le report (l'exemple d'ELV-09 : 6 TND, puis le surplus sur la somme suivante ; une somme réglée garde sa part) ; la valeur non suivie (Mois, séance, totalité, Formation). |
| .NET, l'API | Arrêter (journal, refus, « À payer » suit) ; réinscrire après un arrêt ; le nombre d'élèves et « Déjà inscrit » ; changer de groupe (l'exemple d'ELV-09, journal, chaque refus) ; groupes possibles du même type ; isolation entre centres. |
| Angular | Arrêter (date d'office, envoi exact, confirmation, relecture) ; l'inscription arrêtée sans action ; le report ; la page de changement (titre, groupes du même type, envoi exact, confirmation) ; la carte « Les élèves » du groupe. |
