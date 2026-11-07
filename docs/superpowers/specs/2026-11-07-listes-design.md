# Les listes

Conception établie le 07/11/2026 pour Lassad et Taha. Elle couvre toute l'épopée [Listes](../../user-stories/12-listes.md) : LST-01 à LST-05. Les fichiers Excel viennent de ClosedXML, choisi au [ticket 12](../../wayfinder/cadrage-mvp/issues/12-architecture-technique.md) (Q8).

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Imprimer (LST-01) ? | L'impression du navigateur, sur la page même : le cadre (en-tête, menu, onglets), les boutons et les champs ne s'impriment pas ; un en-tête d'impression porte le centre, le nom de la liste (« Élèves de 9ᵉ année de base »), la date et le nombre de lignes. Page A4. |
| Quelles listes ? | Élèves (filtre par recherche et, nouveau, par niveau), professeurs (au centre ou partis), les élèves d'un groupe, les reçus (recherche de la caisse), les entrées de la bibliothèque (un jour), les achats (mois ou année, catégorie), les employés (propriétaire). |
| Exporter (LST-02) ? | Le serveur prépare le fichier (`GET /api/exports/{liste}`) avec les mêmes filtres que la liste ; une ligne de titres en gras et figée ; montants en nombres « 0,000 », dates en dates ; nom « eleves-2027-11-27.xlsx ». Les employés : 403 pour la secrétaire. |
| Le modèle (LST-03, LST-04) ? | `GET /api/imports/{eleves|professeurs}/modele` : les colonnes du ticket, une ligne d'exemple ; pour les élèves, une 2ᵉ feuille avec les niveaux du centre. |
| Importer ? | `POST /api/imports/{eleves|professeurs}` (multipart, champ « fichier », 10 Mo et 2 000 lignes au plus). Chaque ligne passe par les règles de la fiche (ELV-02, GRP-01) : « ligne 14 : niveau inconnu « Terminale » », « ligne 27 : le téléphone du parent est obligatoire », « ligne 40 : groupe introuvable ». Une erreur : 400 et rien n'est importé. Les doublons possibles (dans le centre, ou deux lignes du fichier) sont signalés sans bloquer. Tout ou rien, dans une seule transaction. |
| Le groupe d'une ligne ? | « Matière · Nom du groupe » (le nom seul s'il est unique parmi les groupes ouverts) ; l'élève est inscrit à partir du Mois en cours du groupe (la 1ʳᵉ séance du Mois de la dernière séance passée, GRP-07), ou à la prochaine séance d'une Révision ou d'une Formation. |
| La sélection (LST-05) ? | Une case par élève et « Tout sélectionner » (les lignes du filtre en cours). Avec une sélection : inscrire dans un groupe ouvert (le mode d'une Révision ou d'une Formation se choisit), changer le niveau, imprimer (seules les lignes sélectionnées) ou exporter la sélection. Chaque élève passe ou est refusé seul : les élèves déjà inscrits sont listés à part, les refus avec leur raison. 200 élèves au plus. Pas de suppression en masse. |

## Les adresses

| Adresse | Ce qu'elle fait |
|---|---|
| `GET /api/exports/eleves?recherche=&niveau=&ids=`, `/professeurs?partis=`, `/groupes/{id}`, `/recus?jour=&numero=&eleve=`, `/bibliotheque?jour=`, `/achats?mois=&annee=&categorie=`, `/employes` | le fichier Excel |
| `GET /api/imports/eleves/modele`, `/professeurs/modele` | le modèle |
| `POST /api/imports/eleves`, `/professeurs` | 200 `{ "importe", "importes", "inscrits", "erreurs", "doublons" }` ; 400 avec les erreurs |
| `POST /api/selection/inscription` | `{ "eleveIds", "groupeId", "mode", "debutLe" }` → `{ "reussis", "dejaInscrits", "refuses" }` |
| `POST /api/selection/niveau` | `{ "eleveIds", "niveauId" }` → le même résultat |
