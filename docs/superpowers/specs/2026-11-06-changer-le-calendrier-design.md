# Changer le calendrier

Conception établie le 06/11/2026 pour Lassad et Taha. C'est l'étape 2 de l'épopée [Séances et appel](../../user-stories/06-seances-appel.md), selon le [découpage](2026-11-06-l-appel-design.md#le-découpage-de-lépopée).

## But de cette étape

La secrétaire annule une séance (SEA-03), la déplace (SEA-04), remplace une séance déjà annulée, ajoute une séance supplémentaire (SEA-05) et change l'horaire d'un groupe à partir d'une date (SEA-06). Les Mois, les numéros des séances et ce que doivent les élèves suivent d'eux-mêmes. Chaque action prépare un message WhatsApp pour les élèves concernés et leurs parents ; l'annulation et le déplacement vont au journal.

## Décisions

Chaque fois la recommandation, sans question.

| Question | Décision |
|---|---|
| Que garde-t-on ? | La séance gagne sa **nature** (`Prevue`, `Remplacement`, `Supplementaire`), son annulation (`AnnuleeLe`, `MotifDAnnulation`) et, pour un remplacement, la séance qu'elle remplace. Rien ne s'enregistre sur les Mois : ils se calculent. |
| Quelles séances comptent ? | Celles qui ne sont ni annulées, ni supplémentaires (`QuiComptent`). Ce filtre est appliqué partout où les séances se comptent : les Mois et les numéros de la fiche, ce que doit l'élève, l'inscription (une première séance doit compter), « terminé », les groupes proposés au formulaire public et aux demandes, les professeurs partis. |
| Une séance annulée ? | Elle reste visible avec son motif, mais ne compte plus : le Mois s'allonge et les suivants se décalent. Elle n'a pas d'appel (409 « Cette séance est annulée : elle n'a pas d'appel ») et sort des appels à recopier. Une séance qui a son appel a eu lieu : elle ne s'annule pas. |
| Le remplacement compte « à sa place » ? | Les séances qui comptent restent rangées par date : le remplacement prend la place de sa date. Déplacer le 16/10 au 19/10 garde le même nombre de séances dans le Mois. |
| Déplacer ? | En une seule action : la séance d'origine est annulée (« Déplacée au 07/12/2027 ») et une séance de remplacement est créée. Une séance supplémentaire déplacée reste supplémentaire. La nouvelle date peut être un jour férié. |
| Remplacer une séance annulée ? | Pour une Révision ou une Formation, la page propose « Remplacer cette séance » après l'annulation, pour garder le nombre de séances prévu. Une seule fois par séance ; jamais pour une séance supplémentaire. Le remplacement va au journal comme un déplacement. |
| Une nouvelle séance ? | Une date, et des heures « HH:mm » qui finissent après leur début ; elle ne chevauche aucune autre séance du groupe qui a lieu (409 « Le groupe a déjà une séance à ce moment-là »). |
| Changer l'horaire ? | Les nouveaux créneaux (mêmes règles qu'à la création) et une date de départ, demain au plus tôt. Les séances prévues de cette date et après, sans appel, sont recréées sur les nouveaux créneaux ; les séances d'avant, de remplacement et supplémentaires restent. Une Étude va jusqu'à la fin de son année scolaire ; une Révision ou une Formation garde son nombre de séances. Les créneaux du groupe changent ; son nom ne change pas. Pour revenir à l'ancien horaire (fin du Ramadan), un autre changement. |
| Un groupe fermé ou terminé ? | Ses séances ne changent plus : 409. |
| Les messages ? | Un texte pour tous, et pour chaque élève concerné un lien `wa.me` par [destinataire](../../user-stories/00-exigences-communes.md#messages). Élèves concernés : inscrits ce jour-là (pour un changement d'horaire, pas arrêtés avant la date de départ). Le motif d'une annulation ne part pas dans le message. |
| Le journal ? | `SeanceAnnulee` (le groupe, le professeur et le motif ; avant : la séance ; après : « Annulée ») et `SeanceDeplacee` (avant : l'ancienne séance ; après : la nouvelle). La séance supplémentaire et le changement d'horaire n'y vont pas (exigences communes). |

## 1. Les données

**La séance** (`seances`) gagne `nature` (les séances déjà enregistrées prennent `Prevue`), `annulee_le`, `motif_d_annulation` (200 caractères), `remplace_la_seance_id` (clé vers `seances`, mise à vide si la séance remplacée disparaît avec la fermeture du groupe). **Le journal** gagne `SeanceAnnulee` et `SeanceDeplacee`. Migration `ChangerLeCalendrier`.

## 2. Les adresses

Toutes pour une personne connectée du centre ; chacune répond 200, `{ "texte", "eleves": [{ "eleveId", "prenom", "nom", "messages" }] }`.

| Adresse | Ce qu'elle fait |
|---|---|
| `POST /api/seances/{id}/annulation` | `{ "motif" }` |
| `POST /api/seances/{id}/deplacement` | `{ "date", "debut", "fin" }` |
| `POST /api/seances/{id}/remplacement` | `{ "date", "debut", "fin" }`, pour une séance déjà annulée |
| `POST /api/groupes/{id}/seances` | `{ "date", "debut", "fin" }` : une séance supplémentaire |
| `POST /api/groupes/{id}/horaire` | `{ "creneaux", "aPartirDu" }` |

La fiche d'un groupe donne la nature de chaque séance et `horsCompte` : les séances annulées (motif, date du remplacement) et supplémentaires. L'appel d'une séance dit si elle est annulée, avec son motif.

### Les refus

| Cas | Refus |
|---|---|
| Séance ou groupe d'un autre centre, ou inconnu | 404 « Séance introuvable », « Groupe introuvable » |
| Groupe fermé, ou terminé | 409 « Ce groupe est fermé : ses séances ne changent plus », « … terminé … » |
| Annuler ou déplacer une séance annulée, ou qui a son appel | 409 « Cette séance est déjà annulée », « Cette séance a son appel : elle a eu lieu » |
| Motif vide, ou trop long | 400 « Écrivez le motif de l'annulation », « Le motif fait au plus 200 caractères » |
| Nouvelle séance | 400 « Choisissez la date », « Choisissez l'heure de début et l'heure de fin », « La séance doit finir après son début » ; 409 « Le groupe a déjà une séance à ce moment-là » |
| Remplacer | 409 « Seule une séance annulée se remplace », « Cette séance est déjà remplacée », « Une séance supplémentaire ne se remplace pas » |
| Changer l'horaire | les refus des créneaux de la création ; 400 « Choisissez la date de départ du nouvel horaire », « Le nouvel horaire part au plus tôt demain », « Cette date est après la fin de l'année scolaire » |

## 3. Les pages

**La séance** (`/professeurs/groupes/{id}/appel/{seance}`), sous son appel : la carte « Cette séance » avec « Déplacer cette séance » (date, heures proposées) et « Annuler cette séance » (motif) ; une séance annulée le dit et propose « Remplacer cette séance ». Une séance qui a son appel ne propose rien.

**La fiche d'un groupe** : une séance de remplacement est marquée « remplacement » ; la carte « Le calendrier » liste les séances annulées (motif, « remplacée le 07/12 ») et supplémentaires, et propose « Ajouter une séance supplémentaire » et « Changer l'horaire du groupe » (créneaux proposés depuis ceux du groupe, « Ajouter un créneau », « À partir du »). Rien pour un groupe fermé ou terminé.

Après chaque action, « Prévenir les élèves et les parents » montre le texte et un bouton « Ouvrir WhatsApp » par destinataire de chaque élève.

## 4. Les tests

| Côté | Ce que les tests vérifient |
|---|---|
| .NET | Annuler allonge le Mois et décale les suivants (message, élèves, journal) ; une séance annulée change ce que doit un élève arrivé en cours de Mois ; les refus de l'annulation ; une séance annulée n'a pas d'appel ; déplacer (le remplacement à sa date dans le Mois, motif, message, journal) et ses refus ; remplacer une séance annulée d'une Révision (numéro et prix), une seule fois ; une séance supplémentaire hors des Mois, avec son appel ; changer l'horaire d'une Étude jusqu'à la fin de l'année, et d'une Révision en gardant son nombre de séances ; la date de départ ; un groupe fermé ; un autre centre. |
| Angular | La séance (annuler avec motif, déplacer avec les heures proposées, remplacer après une annulation, rien après l'appel) ; la carte du calendrier (séances annulées et supplémentaires, séance supplémentaire, nouvel horaire, refus, groupe fermé) ; les messages. |
