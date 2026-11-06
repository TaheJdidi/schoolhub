# Encaisser et le reçu

Conception établie le 06/11/2026 pour Lassad et Taha. Elle ouvre l'épopée [Caisse et reçus](../../user-stories/07-caisse-recus.md).

## Le découpage de l'épopée

1. **Encaisser et le reçu** (cette spec) : CAI-03 encaisser depuis la fiche, CAI-05 remettre le reçu, CAI-06 retrouver un reçu, CAI-07 annuler.
2. **Couleurs, badge et relance** : CAI-01 la couleur d'un élève, CAI-02 le badge « groupe payé », CAI-08 relancer.
3. **Encaissement groupé** : CAI-04.
4. **La caisse du soir** : CAI-09 clôturer sa caisse, CAI-10 le contrôle des clôtures. La clôture ajoutera son refus à l'annulation d'un reçu (« Votre caisse est clôturée »).

## But de cette étape

Depuis la fiche d'un élève, la secrétaire coche ce qu'il paie (dû aujourd'hui, ou d'avance) et encaisse en espèces : un seul reçu, numéroté à la suite pour tout le centre. Le reçu part par email s'il y a un email ; elle le remet aussi imprimé ou par WhatsApp. Elle retrouve les reçus dans la caisse, et annule un reçu fait par erreur.

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Où vit le code ? | Un module `Caisse` : le reçu et le service `RecusDuCentre`. Les sommes à payer viennent du calcul des inscriptions (`InscriptionsDuCentre.SommesEncaissablesAsync`). |
| Que peut-on payer ? | Chaque somme pas réglée et de plus de 0 TND des inscriptions de l'élève, et l'assurance de l'année si elle est à payer. Celles dues aujourd'hui ou avant sont cochées d'office ; « Payer d'avance » montre les autres (Mois, tranches, séances à venir). |
| Le montant ? | Toujours celui du calcul, jamais celui de la page : la page envoie seulement l'inscription et la clé de chaque somme (« Mois 3 », ou « Assurance »). Chaque somme se paie en entier. Une somme qui n'est plus à payer (payée entre-temps dans un autre onglet) refuse tout : 409 « Une somme cochée n'est plus à payer : rechargez la page ». |
| Ce que l'encaissement enregistre ? | Le **reçu** (`Recu`, table `recus`) : numéro, élève, secrétaire (et son nom, figé), moment, jour à Tunis, total, et ses **lignes** figées (groupe, libellé, montant, la somme réglée). Et un **règlement** par somme (`OrigineDuReglement.Encaissement`, avec le reçu), qui la rend réglée : ce que l'élève doit se recalcule de lui-même. La part du report (ELV-09) est figée dans le règlement, comme pour « payé avant SchoolHub ». |
| Le numéro ? | À la suite pour tout le centre, sans trou : la ligne du centre est verrouillée pendant l'encaissement, et l'index (centre, numéro) est unique. Un reçu annulé garde son numéro. |
| Deux encaissements du même élève en même temps ? | La ligne de l'élève est verrouillée : le second voit les sommes déjà réglées et refuse. |
| L'email ? | Après l'enregistrement, le reçu part en texte à ceux de ses [destinataires](../../user-stories/00-exigences-communes.md#messages) qui ont un email (un majeur, ou son parent à défaut ; un mineur, son parent et lui), avec copie au centre s'il a un email. Un envoi qui échoue ne défait pas l'encaissement : « Renvoyer l'email » existe. **Le PDF joint à l'email viendra plus tard** : l'email reprend tout le contenu du reçu en texte. |
| Imprimer ? | Une page A5 sans le menu (`/recus/{id}/impression`) ; l'impression du navigateur en fait un PDF. « ANNULÉ » s'il l'est. |
| WhatsApp ? | Un message par destinataire, qui reprend tout le reçu en texte : « Reçu n° 12 · Centre Les Jasmins, 71 234 567 », la date, l'élève, une ligne par somme, « Total payé en espèces : 60,000 TND », « Reçu par Amira Jlassi » ; « REÇU ANNULÉ : motif » en tête s'il l'est. |
| Retrouver ? | La caisse montre les reçus du jour, avec leur total (les reçus annulés n'y comptent pas) ; la recherche par numéro, par élève (chaque mot, sans accents) et par date, parmi tous les reçus du centre (200 au plus). |
| Annuler ? | Avec un motif. La secrétaire annule ses reçus du jour ; sinon, seul le propriétaire, et la page dit pourquoi : « Ce reçu date d'avant aujourd'hui… », « Ce reçu a été fait par une autre secrétaire… ». Le reçu reste, avec « annulé » et son motif ; ses règlements partent, et ce qu'il payait redevient à payer. |
| L'assurance encaissée ? | Elle est « payée » (nouvel état `Payee`), et non « payée avant SchoolHub ». |
| Le journal ? | `Encaissement` (« Reçu n° 1 · Adem Miled · Mois 3, Mois 4 » ; après : « Encaissé, 80,000 TND ») et `RecuAnnule` (avant : « Encaissé, … » ; après : « Annulé »). |

## 1. Les données

- **Le reçu** (`recus`, `IAppartientAuCentre`) : `numero` (unique par centre), `eleve_id`, `auteur_id`, `auteur`, `cree_le`, `jour`, `total`, `annule_le`, `motif_d_annulation`, `annule_par_id` ; **ses lignes** (`lignes_des_recus`) : `groupe`, `libelle`, `montant`, `inscription_id`, `assurance_id`, `cle`.
- **Le règlement** gagne `recu_id` et l'origine `Encaissement`.
- **Le journal** gagne `Encaissement` et `RecuAnnule`.

Migration `AjouterLesRecus`.

## 2. Les adresses

Toutes pour une personne connectée du centre.

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/eleves/{id}/encaissable` | ce que l'élève peut payer | 200, `{ "eleve", "sommes": [{ "inscriptionId", "cle", "groupe", "libelle", "montant", "dueLe", "due" }], "aUnEmail" }` ; 404 |
| `POST /api/eleves/{id}/encaissement` | `{ "sommes": [{ "inscriptionId", "cle" }] }` | 201, le reçu ; 400 « Cochez au moins une somme » ; 409 « Une somme cochée n'est plus à payer : rechargez la page » ; 404 |
| `GET /api/recus?jour=&numero=&eleve=` | les reçus, les plus récents en premier | 200, `[{ "id", "numero", "jour", "creeLe", "eleve", "total", "secretaire", "annule" }]` |
| `GET /api/recus/{id}` | un reçu | 200, `{ "id", "numero", "jour", "creeLe", "centre", "eleve", "lignes", "total", "secretaire", "annule", "motifDAnnulation", "annuleLe", "texte", "messages", "emails", "annulable", "pourquoiPasAnnulable" }` ; 404 « Reçu introuvable » |
| `POST /api/recus/{id}/email` | renvoyer l'email | 200, le reçu ; 400 « Aucun email n'est connu pour cet élève. Ajoutez-en un sur sa fiche, ou utilisez WhatsApp. » |
| `POST /api/recus/{id}/annulation` | `{ "motif" }` | 200, le reçu ; 400 « Écrivez le motif de l'annulation », « Le motif fait au plus 200 caractères » ; 403 (le pourquoi) ; 409 « Ce reçu est déjà annulé » |

## 3. Les pages

- **La fiche d'un élève** : « Encaisser » sous « À payer ». L'assurance encaissée dit « payée ».
- **L'encaissement** (`/eleves/{id}/encaissement`) : « À payer », coché d'office ; « Payer d'avance » ; le total ; « Encaisser en espèces », qui ouvre le reçu.
- **Le reçu** (`/caisse/recus/{id}`) : son texte ; « Remettre le reçu » : « Imprimer (A5) », WhatsApp par destinataire, l'email (« Envoyé à … », « Renvoyer l'email », ou « Aucun email n'est connu… ») ; « Annuler ce reçu » avec motif, ou pourquoi pas.
- **Le reçu à imprimer** (`/recus/{id}/impression`, sans le menu) : la page A5.
- **La caisse** (`/caisse`) : « Reçus du jour », leur nombre et leur total ; la recherche par numéro, élève et date.

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Ce que l'élève peut payer ; encaisser le Mois 3 et le Mois 4 d'avance (numéro, texte exact, messages, ce qui reste à payer, emails avec copie au centre, journal) ; l'assurance encaissée « payée » ; la numérotation à la suite, et à part pour un autre centre ; les refus ; retrouver (jour, numéro, élève) ; annuler son reçu du jour (texte, à payer de nouveau, journal, nouvel encaissement) ; seul le propriétaire pour le reçu d'une autre secrétaire ou d'hier ; les refus de l'annulation ; renvoyer l'email ; sans email ; un email qui échoue ; un autre centre. |
| Angular | L'encaissement (cochées d'office, total, d'avance, envoi exact, ouverture du reçu, refus) ; le reçu (remettre, renvoyer l'email, sans email, annuler avec motif, pourquoi pas) ; le reçu à imprimer ; la caisse (reçus du jour, total sans les annulés, recherche) ; les adresses. |
