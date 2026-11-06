# Les achats

Conception établie le 07/11/2026 pour Lassad et Taha. Elle couvre la partie achats de l'épopée [RH et achats](../../user-stories/10-rh-achats.md) : RHA-04, RHA-05 et RHA-06. Les RH (RHA-01 à RHA-03) font l'étape suivante.

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Où vit le code ? | Un module `Achats` : l'achat, sa facture et le service `AchatsDuCentre`. La caisse (`CaissesDuCentre`) compte les sorties. |
| Un achat ? | La date, le libellé (200 caractères), la catégorie (`Materiel` ou `Service`), le montant (positif, au millime), le fournisseur (facultatif, 200 caractères), « Payé depuis ma caisse », l'auteur. |
| Une sortie de caisse ? | Un achat « payé depuis ma caisse », du jour de l'auteur : il entre dans sa caisse ouverte comme les reçus, et la clôture le range (`cloture_id`). Les espèces attendues baissent d'autant. Une caisse qui n'a que des sorties se clôture aussi. Une sortie se note seulement pour aujourd'hui. |
| La facture ? | Facultative : une photo (JPEG, PNG, WebP) ou un PDF, 5 Mo au plus, gardée en base à part de l'achat ; elle se remplace ou se retire. |
| Modifier, supprimer ? | L'auteur, le jour même, tant que sa caisse n'est pas clôturée depuis l'achat ; sinon, seul le propriétaire (403 « Cet achat date d'avant aujourd'hui… », « Votre caisse est clôturée… », « Cet achat a été saisi par une autre personne… »). Une sortie déjà rangée dans une clôture ne change plus de caisse : son montant et sa case « payé depuis ma caisse » restent. |
| Refaire un achat ? | La page reprend libellé, catégorie, montant et fournisseur dans un nouvel achat, à la date du jour, modifiable avant d'enregistrer. Rien ne se répète tout seul. |
| Les totaux ? | Par mois (par défaut le mois en cours) ou par année, et par catégorie : la liste, le total Matériel, le total Service, le total. |
| Le journal ? | `SortieDeCaisse` : à la création d'une sortie (« Ramettes de papier · Matériel » ; après : « Sortie, 45,000 TND »), à sa modification (avant, après) et à sa suppression (après : « Supprimée »). Un achat hors caisse ne va pas au journal (exigences communes). |
| Qui ? | La secrétaire et le propriétaire voient et saisissent les achats. |

## 1. Les données

- **L'achat** (`achats`, `IAppartientAuCentre`) : `jour`, `libelle`, `categorie`, `montant`, `fournisseur`, `paye_depuis_la_caisse`, `auteur_id`, `auteur`, `cree_le`, `cloture_id`.
- **La facture** (`factures_d_achat`) : `achat_id` (unique), `nom`, `type`, `contenu`.
- **Le journal** gagne `SortieDeCaisse`.

Migration `AjouterLesAchats`.

## 2. Les adresses

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/achats?mois=2027-10` ou `?annee=2027`, `&categorie=` | la liste, les plus récents en premier, et les totaux | 200, `{ "achats": [achat], "totalMateriel", "totalService", "total" }` |
| `POST /api/achats` | `{ "jour", "libelle", "categorie", "montant", "fournisseur", "payeDepuisLaCaisse" }` | 201, l'achat ; 400 « Choisissez la date », « Écrivez le libellé », « Le libellé fait au plus 200 caractères », « Choisissez la catégorie », « Le montant est positif, au millime près », « Le fournisseur fait au plus 200 caractères », « Une sortie de caisse se note le jour même » |
| `PUT /api/achats/{id}` | les mêmes champs | 200 ; 403 (pourquoi) ; 404 « Achat introuvable » |
| `DELETE /api/achats/{id}` | | 204 ; 403 ; 404 |
| `PUT /api/achats/{id}/facture` | un fichier (multipart, `facture`) | 200, l'achat ; 400 « La facture est une photo (JPEG, PNG, WebP) ou un PDF de 5 Mo au plus » |
| `GET /api/achats/{id}/facture` | le fichier | 200 ; 404 |
| `DELETE /api/achats/{id}/facture` | | 200, l'achat |

Un achat : `{ "id", "jour", "libelle", "categorie", "montant", "fournisseur", "payeDepuisLaCaisse", "auteur", "aUneFacture", "modifiable", "pourquoiPasModifiable" }`.

## 3. Les pages

**Achats** (`/achats`) : les filtres (mois ou année, catégorie) ; la liste (date, libellé et fournisseur, catégorie, « sortie de caisse », montant, facture, « Modifier », « Supprimer », « Refaire ») avec les totaux en bas ; « Nouvel achat » (formulaire, et la facture à joindre). **Ma caisse** montre ses sorties de caisse.

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Saisir un achat ; une sortie de caisse (caisse, attendu, journal, clôture avec seulement une sortie) ; les refus ; modifier, supprimer (auteur le jour même, après la clôture, autre personne, propriétaire) ; la facture (joindre, lire, refus, retirer) ; les totaux par mois, année, catégorie ; un autre centre. |
| Angular | La liste et les totaux, les filtres, le nouvel achat, refaire, modifier, supprimer. |
