# Les ressources humaines

Conception établie le 07/11/2026 pour Lassad et Taha. Elle couvre la partie RH de l'épopée [RH et achats](../../user-stories/10-rh-achats.md) : RHA-01, RHA-02 et RHA-03, réservées au propriétaire. Les professeurs n'en font pas partie.

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Où vit le code ? | Un module `Rh` : l'employé, ses congés, ses salaires payés, et le service `RessourcesHumaines`. |
| La fiche ? | Prénom, nom (100 caractères chacun), email et téléphone (facultatifs, mêmes règles que pour un professeur), poste (100 caractères), horaires de la semaine (texte libre, 300 caractères : « Lundi à vendredi, 8h → 16h »), salaire mensuel (0 ou plus, au millime). Une fiche RH n'est pas un compte. |
| Un employé parti ? | « Parti » avec la date : il sort de la liste (un filtre « Partis » le retrouve) ; sa fiche, ses congés et ses salaires restent. « Revenir au centre » existe. |
| Un congé ? | Du, au (au ≥ du), une note facultative (200 caractères). Les jours comptés sont les jours du calendrier, du premier au dernier inclus ; un congé à cheval sur deux années compte dans chacune pour sa part. Un congé noté par erreur se retire. |
| Les salaires d'un mois ? | Chaque employé présent ce mois-là (pas parti avant le 1er), avec son salaire et « payé » ou « à payer ». « Payé » fige le salaire et la date ; « À payer » le défige. Hors caisse. Le salaire d'octobre compte en octobre (TDB-01), même payé le 02/11. |
| Le journal ? | Rien : ces actions ne sont pas dans la liste des exigences communes. |

## 1. Les données

- **L'employé** (`employes`) : `prenom`, `nom`, `email`, `telephone`, `poste`, `horaires`, `salaire`, `parti_le`.
- **Le congé** (`conges`) : `employe_id`, `du`, `au`, `note`.
- **Le salaire payé** (`salaires_payes`) : `employe_id`, `mois` (le 1er du mois), `montant`, `paye_le` ; unique par employé et mois.

Migration `AjouterLesRessourcesHumaines`.

## 2. Les adresses

Toutes réservées au propriétaire.

| Adresse | Ce qu'elle fait |
|---|---|
| `GET /api/employes?annee=` | les employés (présents et partis), avec les jours de congé pris dans l'année (par défaut l'année en cours) |
| `POST /api/employes`, `PUT /api/employes/{id}` | la fiche ; 400 « Le prénom est obligatoire »… « Le salaire est 0 ou plus, au millime près » |
| `POST /api/employes/{id}/depart`, `/retour` | parti, ou de retour |
| `GET /api/employes/{id}/conges`, `POST …/conges`, `DELETE /api/conges/{id}` | ses congés ; 400 « Choisissez les dates du congé », « Le congé finit après son début » |
| `GET /api/salaires?mois=2027-10` | les salaires du mois et leur total, payé et à payer |
| `POST /api/salaires` | `{ "employeId", "mois", "paye" }` |

## 3. Les pages

**Ressources humaines** (`/ressources-humaines`, propriétaire) : la liste (nom, poste, salaire, congés de l'année) avec « Partis » ; la fiche (modifier, parti, congés à ajouter et retirer) ; « Salaires du mois » (un mois au choix, chaque employé « Payé » ou « À payer », le total).

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Créer, modifier une fiche et ses refus ; parti et retour ; les congés et leurs jours dans l'année (à cheval) ; les salaires d'un mois (payé, figé, à payer, employé parti avant) ; réservé au propriétaire ; un autre centre. |
| Angular | La liste, la fiche, un congé, les salaires du mois. |
