# Les professeurs

Conception établie le 04/11/2026 pour Lassad et Taha. Elle couvre la première étape de l'épopée [Professeurs et groupes](../../user-stories/03-professeurs-groupes.md) : les professeurs, avant les groupes.

## But

La secrétaire enregistre les professeurs du centre, corrige leur fiche, et marque ceux qui partent. Les groupes d'Étude (GRP-04) pourront ensuite se rattacher à un professeur de cette liste.

## Périmètre

**Dans cette étape :**

- [GRP-01](../../user-stories/03-professeurs-groupes.md#grp-01--ajouter-ou-modifier-un-professeur) : ajouter, modifier et supprimer un professeur ; un téléphone déjà pris par un autre professeur prévient sans bloquer ;
- [GRP-03](../../user-stories/03-professeurs-groupes.md#grp-03--marquer-un-professeur--parti-) : marquer un professeur « parti », le filtre « Professeurs partis », et « Revenir au centre » ;
- [GRP-02](../../user-stories/03-professeurs-groupes.md#grp-02--voir-les-groupes-dun-professeur-filtrés-par-jour), la liste seulement : chaque professeur avec son nombre de groupes ouverts (0 pour l'instant).

**Plus tard :**

- la part du centre du professeur, visible du propriétaire sur la fiche : [PRT-04](../../user-stories/09-parts-professeurs.md#prt-04--changer-la-part-du-centre-pour-un-professeur) ;
- les cadres des groupes d'un professeur, le filtre par jour, « Anciens groupes » et le badge de paiement : GRP-04 et la caisse ;
- **ce que GRP-04 devra ajouter ici**, quand un groupe pointera vers un professeur (section 4).

## Décisions

Lassad et Taha ont demandé de travailler sans questions et de prendre chaque fois la recommandation. Les voici.

| Question | Décision |
|---|---|
| Où vit le code ? | Dans un module `Professeurs` (`backend/src/SchoolHub/Modules/Professeurs/`, comme le prévoit le README du backend), l'un des modules de l'[ADR 0003](../../adr/0003-un-seul-deploiement-decoupe-en-modules.md). Le professeur appartient au centre (`IAppartientAuCentre`). |
| Comment prévenir d'un téléphone en double sans bloquer ? | Le professeur s'enregistre, et la réponse porte un `avertissement` : « Ce téléphone est aussi celui de Karim Ben Salah ». La page l'affiche sous la fiche. Une seule requête, pas de confirmation. |
| Quels professeurs comptent pour le doublon ? | Tous les autres professeurs du centre, partis compris : un professeur parti qui revient doit se retrouver plutôt que se recréer. Le professeur lui-même ne compte pas. |
| Le doublon prévient-il à chaque modification ? | Oui, tant que le doublon existe. C'est plus simple et toujours vrai. |
| Comment s'écrit le départ ? | Un simple drapeau `Parti`. Pas de date : aucune story ne la demande. |
| Faut-il confirmer « Marquer parti » ? | Non : il se défait avec « Revenir au centre ». La suppression, elle, demande confirmation, car elle ne se défait pas. |
| Une seule liste ou deux appels ? | `GET /api/professeurs` rend tous les professeurs, partis compris, avec leur état. La page les sépare. Un centre en a quelques dizaines au plus. |
| Le nombre de groupes ouverts ? | Le champ `groupesOuverts` existe déjà dans l'API et vaut toujours 0. GRP-04 le comptera. |
| Le journal ? | Les professeurs n'y vont pas : la liste des [exigences communes](../../user-stories/00-exigences-communes.md#journal) ne les cite pas. |
| Qui y a accès ? | Toute personne connectée du centre : la secrétaire et le propriétaire. |
| Un téléphone collé depuis WhatsApp ? | Accepté : `TelephoneTunisien.Normaliser` ignore désormais tout espace (`char.IsWhiteSpace`, les insécables compris) et les caractères invisibles de mise en forme (les marques de direction que WhatsApp ajoute autour d'un numéro copié). Les espaces insécables étaient la remarque n° 4 reportée des paramètres ; elle touche directement la fiche du professeur. |
| L'ordre de la liste ? | Par prénom, puis par nom : au centre, on dit « Karim » ou « Mme Leila ». |
| Les messages de départ et de retour ? | Sans genre, car la fiche ne dit pas si c'est un professeur ou une professeure : « Karim Ben Salah a quitté le centre », « Karim Ben Salah est de retour au centre », « Fiche de Karim Ben Salah supprimée ». Les étiquettes « Parti » et « Professeurs partis » restent celles de la story. |
| Un double-clic sur « Marquer parti » ? | Le second clic ne compte pas : « Revenir au centre » prend la place du bouton dès la réponse, et le second clic déferait le départ. De même dans l'autre sens. |

## 1. Les données

**Le professeur** (`Professeur`, table `professeurs`) appartient au centre (`IAppartientAuCentre`) :

| Champ | Contenu |
|---|---|
| `Prenom`, `Nom` | 100 caractères au plus chacun |
| `Telephone` | 8 chiffres, sans espaces ni indicatif (`TelephoneTunisien`) |
| `Email` | facultatif, 256 caractères au plus |
| `Parti` | vrai quand le professeur a quitté le centre (GRP-03) |

Pas d'index unique sur le téléphone : deux professeurs peuvent l'avoir (un frère qui donne le numéro de la famille, une erreur), la plateforme prévient seulement. Un index simple sur le centre suffit, comme pour les niveaux (il vient de la clé étrangère).

Le professeur n'a pas de compte : aucun lien avec `Compte`.

## 2. Les adresses

Toute personne connectée du centre. Le filtre automatique ne montre que les professeurs du centre courant : un professeur d'un autre centre est « introuvable ».

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/professeurs` | la liste, partis compris, par prénom puis nom | 200, les professeurs |
| `POST /api/professeurs` | ajouter | 201, l'enregistrement |
| `PUT /api/professeurs/{id}` | modifier toute la fiche | 200, l'enregistrement |
| `DELETE /api/professeurs/{id}` | supprimer (erreur de saisie) | 204 |
| `POST /api/professeurs/{id}/depart` | marquer parti | 200, le professeur |
| `POST /api/professeurs/{id}/retour` | revenir au centre | 200, le professeur |

**Un professeur, en JSON** :

```json
{
  "id": "…",
  "prenom": "Karim",
  "nom": "Ben Salah",
  "telephone": "98 401 220",
  "email": null,
  "parti": false,
  "groupesOuverts": 0
}
```

**Ce que la page envoie** pour ajouter ou modifier : `{ "prenom", "nom", "telephone", "email" }`.

**L'enregistrement** (réponse de l'ajout et de la modification) : `{ "professeur": {…}, "avertissement": "Ce téléphone est aussi celui de Karim Ben Salah" }`, ou `"avertissement": null`. Avec plusieurs professeurs : « … celui de Karim Ben Salah et Leila Khelifi » (« , » entre les premiers, « et » avant le dernier).

Marquer parti un professeur déjà parti, ou faire revenir un professeur qui est au centre, ne change rien et répond 200 : la page n'a pas de bouton pour cela, et deux clics rapides ne doivent pas donner d'erreur.

### Les règles de saisie

Les espaces autour des textes sont retirés. Le premier champ refusé donne la réponse, et rien n'est enregistré.

| Champ | Règle | Refus (400) |
|---|---|---|
| Prénom | obligatoire, 100 caractères au plus | « Le prénom est obligatoire » ; « Le prénom fait au plus 100 caractères » |
| Nom | obligatoire, 100 caractères au plus | « Le nom est obligatoire » ; « Le nom fait au plus 100 caractères » |
| Téléphone | obligatoire : 8 chiffres, espaces permis, « +216 » ou « 00216 » permis devant | « Le téléphone est obligatoire » ; « Le téléphone doit avoir 8 chiffres » |
| Email | facultatif : la règle `AdresseEmail.EstValable` des comptes | « Cet email n'est pas valable » |

| Autre cas | Statut | Message |
|---|---|---|
| Professeur inconnu ou d'un autre centre | 404 | « Professeur introuvable » |

## 3. La page « Professeurs »

À l'adresse `/professeurs`, pour la secrétaire et le propriétaire, elle remplace la page « bientôt ». Elle suit l'écran « Professeurs » de la maquette : la liste à gauche, la fiche du professeur choisi à droite. Sur un téléphone, la fiche passe sous la liste.

```
Professeurs                                              [ + Ajouter un professeur ]
3 professeurs

[ Au centre ] [ Professeurs partis ]
┌──────────────────────┐   ┌─ Karim Ben Salah ─────────────────────────────────┐
│ Karim Ben Salah      │   │ 98 401 220 · karim@exemple.tn                      │
│ Aucun groupe ouvert  │   │ Ses groupes : aucun groupe ouvert.                 │
│ Leila Khelifi        │   │                [ Modifier ] [ Marquer parti ]      │
│ Aucun groupe ouvert  │   │                [ Supprimer ]                       │
└──────────────────────┘   │ ✓ Professeur ajouté                               │
                           │ ⚠ Ce téléphone est aussi celui de Leila Khelifi   │
                           └───────────────────────────────────────────────────┘
```

**La liste**

- Deux filtres, en haut : « Au centre » (choisi d'office) et « Professeurs partis ». Le sous-titre compte les professeurs au centre : « 3 professeurs », « 1 professeur », « Aucun professeur ».
- Chaque professeur : son prénom et son nom, puis « Aucun groupe ouvert », « 1 groupe ouvert » ou « 4 groupes ouverts ». Un clic choisit le professeur (bouton `aria-pressed`).
- Liste vide : « Aucun professeur pour l'instant. Ajoutez le premier. » ; sans professeur parti : « Aucun professeur parti. »
- À l'ouverture, le premier professeur de la liste est choisi. Changer de filtre choisit le premier de l'autre liste.
- Pendant le chargement, rien n'est cliquable ; si le chargement échoue, le refus s'affiche en rouge sous la liste, et « Ajouter un professeur » reste bloqué.

**La fiche**

- Le nom en titre, le téléphone groupé et l'email s'il existe. « Ses groupes » : « Aucun groupe ouvert » (les cadres viendront avec GRP-04).
- Au centre : « Modifier », « Marquer parti », « Supprimer ». Parti : une pastille « Parti », « Modifier », « Revenir au centre », « Supprimer ».
- « Supprimer » demande confirmation dans la fiche : « Supprimer Karim Ben Salah ? Seulement en cas d'erreur de saisie. Oui · Non ».
- « Marquer parti » range le professeur dans « Professeurs partis » ; la page reste sur sa fiche (dans le filtre des partis) avec « Karim Ben Salah a quitté le centre ». « Revenir au centre » fait l'inverse : « Karim Ben Salah est de retour au centre ». Le second clic d'un double-clic sur l'un de ces boutons ne compte pas.
- Après une suppression : « Fiche de Karim Ben Salah supprimée », et le premier professeur de la liste est choisi.

**Le formulaire** (ajouter, ou modifier)

- « Ajouter un professeur » ouvre le formulaire vide à la place de la fiche ; « Modifier » l'ouvre rempli. Prénom, nom, téléphone, et « Email (facultatif) ». « Annuler » revient à la fiche.
- Les champs vides obligatoires ne partent pas : « Remplissez le prénom, le nom et le téléphone ».
- Après l'enregistrement, la fiche du professeur s'affiche, avec « Professeur ajouté » ou « Fiche enregistrée », et l'avertissement du doublon s'il y en a un, dans une note orange. Le professeur ajouté entre dans la liste à sa place, sans recharger la liste.

**Partout** : les boutons se bloquent pendant un envoi ; les messages s'affichent sous la carte qui les concerne (la fiche ou le formulaire), dans la colonne de droite, même quand la fiche a disparu (dernier professeur supprimé) ; un refus du serveur s'affiche en rouge.

## 4. Ce que GRP-04 devra ajouter ici

Quand un groupe pointera vers un professeur :

1. **compter `groupesOuverts`** dans la liste ;
2. **refuser « Marquer parti »** d'un professeur qui a un groupe ouvert : 409 « Fermez d'abord ses groupes » ;
3. **refuser « Supprimer »** d'un professeur qui a eu un groupe, même fermé : 409 « Ce professeur a eu des groupes : marquez-le parti » ; sans ce refus, la clé étrangère donnerait une erreur 500 ;
4. **refuser un professeur parti** dans le formulaire d'un nouveau groupe.

Le point 1 est commenté dans `ProfesseurDuCentre.De`, les points 2 et 3 dans `ProfesseursDuCentre`, le point 4 sur `Professeur.Parti` ; les quatre sont rappelés dans la story GRP-04.

## 5. L'organisation du code

```
backend/src/SchoolHub/Modules/Professeurs/
├── Professeur.cs                   le professeur et sa configuration
├── ProfesseursDuCentre.cs          lister, ajouter, modifier, supprimer, départ, retour ; les règles de saisie
└── ProfesseursEndpoints.cs         les 6 adresses, et les formes JSON

backend/src/SchoolHub/Socle/Centres/TelephoneTunisien.cs   + tout espace et toute marque de direction sont ignorés

frontend/src/app/pages/professeurs/ la page, la fiche, le formulaire, et leurs tests
```

## 6. Les tests

### Côté .NET (`dotnet test`)

| Sujet | Ce que les tests vérifient |
|---|---|
| Ajouter | La fiche complète, l'email vide en `null`, le téléphone « +216 98 401 220 » enregistré en 8 chiffres et renvoyé groupé. Chaque refus du tableau, sans rien enregistrer. |
| Doublon | Un autre professeur (au centre ou parti) avec ce téléphone donne l'avertissement, et le professeur est bien enregistré. Deux doublons : « X et Y ». Pas d'avertissement pour soi-même à la modification. Un professeur d'un autre centre ne compte pas. |
| Modifier, supprimer | La modification, le refus d'une saisie, la suppression, 404 pour un professeur inconnu. |
| Départ, retour | Parti puis revenu ; un second départ ne change rien. La liste garde les partis avec `parti: true`. |
| Liste | L'ordre par prénom puis nom ; `groupesOuverts` vaut 0. |
| Droits | La secrétaire peut tout faire ; une personne non connectée reçoit 401. Un centre ne voit, ne modifie, ne supprime ni ne fait partir les professeurs d'un autre (404). |
| Téléphone | Les espaces insécables (U+00A0, U+202F) et les marques de direction (U+202A à U+202C, U+200E, U+200F, U+2066 à U+2069) sont ignorés. |

### Côté Angular (`npm test`, Vitest)

| Sujet | Ce que les tests vérifient |
|---|---|
| Liste | Les professeurs au centre, le compte du sous-titre, « Aucun groupe ouvert », la liste vide, le filtre des partis, le premier choisi d'office, le refus du chargement (et « Ajouter » bloqué). |
| Fiche | Le téléphone et l'email, les boutons selon l'état, « Marquer parti » et « Revenir au centre » avec leur message, « Supprimer » avec « Oui » et « Non ». |
| Formulaire | Ajouter (champs vides refusés, la fiche du nouveau professeur, « Professeur ajouté »), l'avertissement orange, modifier (rempli, « Fiche enregistrée »), annuler, le refus du serveur sous le formulaire, le bouton bloqué pendant l'envoi. |
| Adresse | `/professeurs` montre la page à la secrétaire. |

### À la main, dans le navigateur

Avec le vrai serveur et PostgreSQL : ajouter deux professeurs avec le même téléphone, modifier, marquer parti, revenir, supprimer ; sur une largeur de téléphone (375 pixels) et d'ordinateur.

## Points d'attention

- **Le filtre du centre** fait tout le travail d'isolation : chaque lecture passe par `contexte.Professeurs`, jamais par une requête qui l'ignore.
- **Le doublon** se cherche sur les 8 chiffres enregistrés : « 98 401 220 » et « +216 98401220 » sont le même téléphone.
- **Aucune règle de groupe n'est simulée** : `groupesOuverts` vaut 0 parce qu'aucun groupe n'existe, et la section 4 dit ce qui changera.
