# Traiter les demandes

Conception établie le 06/11/2026 pour Lassad et Taha. Elle termine l'épopée [Demandes d'inscription](../../user-stories/05-demandes.md) : c'est l'étape 2 du [découpage](2026-11-05-formulaire-public-design.md#le-découpage-de-lépopée).

## But de cette étape

La secrétaire voit les demandes à traiter (leur nombre est dans le menu et sur l'accueil), les filtre par niveau et par matière, et ouvre une demande. Elle vérifie la fiche pré-remplie, ou choisit la fiche d'un élève qui existe déjà, choisit le groupe de chaque matière, et enregistre : l'élève est inscrit et le message WhatsApp de confirmation est prêt. Ou elle refuse la demande, avec un motif. Les demandes traitées restent consultables.

## Décisions

Comme pour l'étape 1, chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Où vit le code ? | Dans le module `Demandes` : un service `DemandesDuCentre`, à côté de `FormulairePublic`. |
| Enregistrer, en une ou plusieurs actions ? | **Une seule**, tout ou rien, comme le bouton « Enregistrer l'élève » de la maquette : la fiche (nouvelle ou existante) et une inscription par groupe choisi, dans une transaction. Le premier refus annule tout : ni fiche, ni inscription, et la demande reste à traiter. |
| Les règles de l'inscription ? | Celles d'ELV-03, par le même code (`InscriptionsDuCentre.InscrireAsync`) : pas de double inscription, groupe fermé ou terminé refusé, avertissement de capacité. `InscrireAsync` rejoint la transaction de la demande quand il y en a une. |
| La première séance ? | La prochaine séance du groupe, aujourd'hui compris : la page la montre, la secrétaire n'a pas à la choisir. Pour une autre date, elle inscrit depuis la fiche de l'élève (ELV-03). Un groupe sans séance à venir n'est pas proposé ; à l'envoi : 409 « Ce groupe n'a plus de séance à venir ». |
| Le mode de tarification ? | Demandé seulement quand le groupe offre un choix (Révision, Formation en tranches), comme sur la page d'inscription. |
| Les groupes proposés ? | Pour chaque matière demandée : les groupes ouverts, pas terminés, avec une séance à venir, du niveau de la demande ou sans niveau ; le préféré en tête et coché s'il est encore proposé. La secrétaire peut ne pas inscrire une matière, mais il faut au moins un groupe. |
| Les amis cités ? | Le texte est découpé aux virgules, points-virgules, retours à la ligne et « et ». Un ami dont le prénom et le nom (sans accents ni majuscules) sont ceux d'un élève inscrit dans le groupe s'affiche : « Ami dans ce groupe : Sami Chaabane ». |
| « Doublon possible » ? | Un élève du centre, ou une autre demande à traiter, au même prénom et au même nom (sans accents ni majuscules), ou avec un téléphone en commun (élève ou parent, dans un sens ou dans l'autre). Les mêmes règles que l'avertissement de la fiche élève. |
| Une fiche qui existe déjà ? | La demande liste les fiches semblables ; la secrétaire peut en choisir une au lieu de créer une fiche. La fiche choisie n'est pas modifiée. |
| Le message de confirmation ? | Une seule matière : le message d'ELV-03. Plusieurs : un seul message, une ligne par groupe (« • Mathématiques avec Karim Ben Salah, Groupe Samedi 10h → 12h : première séance le samedi 27/11/2027 à 10h. »), pour les [destinataires](../../user-stories/00-exigences-communes.md#messages) de l'élève. |
| Le refus ? | Le motif « Doublon » ou « Fausse demande », puis une confirmation. Aucun message ne part. |
| Deux secrétaires sur la même demande ? | La ligne de la demande est verrouillée pendant l'enregistrement ou le refus : la seconde reçoit 409 « Cette demande est déjà traitée ». |
| Le nombre dans le menu ? | Relu par le cadre à chaque changement de page, sans minuteur (une session ouverte ne se renouvelle pas toute seule). |
| Les filtres ? | Dans la page, sur les demandes chargées : les niveaux et les matières présents dans l'onglet. |
| Le journal ? | Rien : ni l'inscription (ELV-03 n'y va pas), ni le refus. |

## 1. Les données

**La demande** (`demandes_d_inscription`) gagne : `Etat` `Inscrite` et `Refusee` ; `motif` (`Doublon`, `FausseDemande`) ; `eleve_id` (l'élève inscrit, clé étrangère `Restrict`) ; `traitee_le`. Migration `TraiterLesDemandes`.

## 2. Les adresses

Toutes pour une personne connectée du centre.

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/demandes` | les demandes à traiter, les plus anciennes en premier | 200, `[{ "id", "prenom", "nom", "niveau", "matieres": [{ "matiere", "professeur", "creneaux" }], "amis", "recueLe", "doublonPossible", "etat", "motif", "traiteeLe" }]` |
| `GET /api/demandes?traitees=true` | les demandes traitées, les plus récemment traitées en premier | 200, la même forme |
| `GET /api/demandes/nombre` | le nombre de demandes à traiter | 200, `{ "aTraiter" }` |
| `GET /api/demandes/{id}` | une demande, ses fiches semblables et ses matières | 200 ; 404 « Demande introuvable » |
| `POST /api/demandes/{id}/inscription` | `{ "eleveId" \| "fiche", "groupes": [{ "groupeId", "mode" }] }` | 201, `{ "eleve", "inscriptions", "avertissement", "texte", "messages" }` |
| `POST /api/demandes/{id}/refus` | `{ "motif" }` | 204 |

### Les refus

| Cas | Refus |
|---|---|
| Demande d'un autre centre ou inconnue | 404 « Demande introuvable » |
| Demande déjà inscrite ou refusée | 409 « Cette demande est déjà traitée » |
| Aucun groupe | 400 « Choisissez au moins un groupe » |
| Fiche existante inconnue | 404 « Élève introuvable » |
| Nouvelle fiche | les refus d'ELV-02, dans leur ordre |
| Un groupe | 404 « Groupe introuvable », puis les refus d'ELV-03 ; 409 « Ce groupe n'a plus de séance à venir » |
| Motif absent ou inconnu | 400 « Choisissez le motif du refus » |

## 3. Les pages

**« Demandes d'inscription »** (`/demandes`) : à gauche les onglets « À traiter » et « Traitées », les filtres « Tous les niveaux » et « Toutes les matières », et la liste (nom, « Doublon possible », niveau et matières avec l'horaire du groupe préféré, amis, « Reçue le … » ; pour une demande traitée, « Inscrit » ou « Refusée · motif » et « Traitée le … »). À droite, la carte du lien public (étape 1). Sur un téléphone, une colonne.

**Une demande** (`/demandes/{id}`) : l'avertissement de doublon ; « La fiche de l'élève » (créer une nouvelle fiche, ou utiliser une fiche semblable) ; la fiche pré-remplie, comme sur la page Élèves ; « Les groupes » : pour chaque matière, ses groupes (nom, « · préféré », professeur, horaire, type, élèves et capacité, première séance, amis, « Complet ») et « Ne pas l'inscrire dans cette matière », puis le mode s'il y a un choix ; « Refuser » et « Enregistrer l'élève ». Enregistrée : « Élève enregistré », ses groupes, l'avertissement, le message et « Ouvrir WhatsApp » par destinataire. Refusée : retour à la liste. Une demande déjà traitée le dit.

**Le menu** montre le nombre de demandes à traiter à côté de « Demandes d'inscription », et **l'accueil** « 2 demandes d'inscription à traiter », avec un lien.

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | La liste (ordre, matières, nombre) ; « Doublon possible » avec un élève et avec une autre demande ; le détail (groupe préféré, première séance, amis, groupe fermé retiré, fiches semblables) ; enregistrer avec une nouvelle fiche (inscriptions, message à plusieurs lignes, état, nombre) ou une fiche existante ; un seul groupe garde le message d'ELV-03 ; la capacité ; tout ou rien ; chaque refus ; une demande déjà traitée ; refuser, et le motif obligatoire ; un autre centre ; les personnes non connectées. |
| Angular | La liste (lignes, filtres, onglet « Traitées », vide) et le lien ; la demande (pré-remplissage, enregistrement exact, le mode d'une Révision, les refus de la page et du serveur, une fiche existante, le refus avec motif, une demande traitée, une demande inconnue) ; le nombre dans le menu, relu à chaque page ; l'accueil ; les adresses. |
