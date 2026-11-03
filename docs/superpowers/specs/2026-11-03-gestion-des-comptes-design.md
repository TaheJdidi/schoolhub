# Gestion des comptes et journal

Conception établie le 03/11/2026 avec Lassad et Taha. Elle termine l'épopée [Comptes et accès](../../user-stories/01-comptes-acces.md), et pose le [journal](../../user-stories/00-exigences-communes.md#journal), dont toutes les stories suivantes se serviront.

## But

Le propriétaire gère lui-même les personnes qui travaillent sur SchoolHub pour son centre : il invite une secrétaire ou un associé, il désactive le compte de quelqu'un qui part, et il le réactive s'il revient. Chacune de ces actions laisse une ligne au journal, que personne ne peut modifier ni effacer.

## Périmètre

**Dans cette étape :**

- le **journal** : sa table, son écriture, et l'interdiction de le modifier ou de l'effacer ;
- [CPT-04](../../user-stories/01-comptes-acces.md#cpt-04--inviter-une-secrétaire-ou-un-autre-propriétaire) : inviter une secrétaire ou un autre propriétaire, la page « Comptes », et « Renvoyer l'invitation » ;
- [CPT-05](../../user-stories/01-comptes-acces.md#cpt-05--désactiver-un-compte) : désactiver et réactiver un compte ;
- [CEN-01](../../user-stories/02-centre.md#cen-01--créer-un-centre-et-son-premier-propriétaire-par-une-commande), déjà faite : la commande écrit désormais la création du premier propriétaire au journal.

**Plus tard :**

- l'écran du journal, ses filtres et son entrée du menu : [TDB-04](../../user-stories/11-tableau-journal.md#tdb-04--consulter-le-journal) ;
- les autres actions du journal (encaissement, reçu annulé, prix spécial…) : chacune avec sa story.

**Pas prévu**, car aucune story ne le demande : modifier le nom, l'email ou le rôle d'un compte, et supprimer un compte.

## Décisions

| Question | Décision |
|---|---|
| Le propriétaire se trompe d'email en invitant : que fait-il ? | Il désactive le compte invité (le lien ne marche plus), puis il invite la personne avec le bon email. Rien n'est effacé : la mauvaise adresse reste dans la liste, désactivée. |
| Un propriétaire peut-il désactiver son propre compte ? | Non. C'est un autre propriétaire qui le fait. Personne ne s'enferme dehors par erreur, et il reste toujours un propriétaire actif : celui qui clique. |
| Comment se reconnecte une personne réactivée ? | Avec son ancien mot de passe. Si elle l'a oublié, elle passe par « Mot de passe oublié » (CPT-03). |
| Comment s'écrit le journal ? | Explicitement : chaque action appelle le journal avec une phrase en français, et la ligne s'enregistre dans la même transaction que l'action. Pas d'écriture automatique des modifications en base : elle serait illisible pour un propriétaire et ne connaîtrait pas l'intention (le motif d'une annulation, par exemple). |
| Comment garantir que personne ne modifie le journal ? | Aucune adresse d'API ne le modifie, et un trigger PostgreSQL refuse toute modification, suppression ou vidage de la table, même en cas d'erreur dans notre code. |
| La commande CEN-01 écrit-elle au journal ? | Oui : la création du premier propriétaire est une création de compte. L'auteur est « SchoolHub », car personne n'est connecté. |
| « Renvoyer l'invitation » va-t-il au journal ? | Non : la liste des [exigences communes](../../user-stories/00-exigences-communes.md#journal) ne le cite pas. |

## 1. Le journal

### Une ligne du journal

`LigneDuJournal`, dans la table `journal`, dans `Socle/Journal/` :

| Champ | Exemple | Contenu |
|---|---|---|
| `Id` | | un `Guid` version 7, comme les autres données |
| `CentreId` | | le centre de la ligne : elle implémente `IAppartientAuCentre` |
| `Quand` | 03/11/2026 18h25 | l'heure de l'horloge de l'application (`TimeProvider`), que les tests remplacent |
| `AuteurId` | | le compte qui a fait l'action, pour le filtre « par personne » de TDB-04. Vide pour la commande CEN-01. |
| `Auteur` | Hedi Ben Salah | le prénom et le nom, **figés** au moment de l'action ; « SchoolHub » pour la commande CEN-01 |
| `Action` | `CompteDesactive` | le type d'action, enregistré en texte : un code stable pour le filtre « par type d'action » de TDB-04 |
| `Detail` | Sonia Mabrouk · Secrétaire | une phrase en français, écrite par l'action |
| `Avant`, `Apres` | Actif, Désactivé | la valeur d'avant et celle d'après, vides quand elles n'ont pas de sens |

`AuteurId` a une clé étrangère vers `comptes`, qui interdit de supprimer un compte qui a écrit au journal (de toute façon, un compte ne se supprime pas).

**Les types d'action** (`ActionDuJournal`), pour l'instant : `CompteCree`, `CompteDesactive` et `CompteReactive`. Chaque story suivante ajoute les siens.

**Ce que le journal contient après cette étape :**

```
01/11 15h50 · SchoolHub      · Compte créé       · Hedi Ben Salah · Propriétaire · hedi@exemple.tn
03/11 18h20 · Hedi Ben Salah · Compte créé       · Sonia Mabrouk · Secrétaire · sonia.mabrouk@exemple.tn
03/11 18h25 · Hedi Ben Salah · Compte désactivé  · Sonia Mabrouk · Secrétaire       Actif → Désactivé
03/11 18h40 · Hedi Ben Salah · Compte réactivé   · Sonia Mabrouk · Secrétaire       Désactivé → Actif
```

### L'écriture

Le service `JournalDuCentre` (enregistré pour la durée de la requête) a une seule méthode. Il ne s'appelle pas `Journal`, qui est déjà le nom de son espace de noms (`SchoolHub.Socle.Journal`) : C# confondrait les deux.

```csharp
journal.Noter(auteur, ActionDuJournal.CompteDesactive, "Sonia Mabrouk · Secrétaire", avant: "Actif", apres: "Désactivé");
```

- **Elle ajoute la ligne au contexte, sans l'enregistrer** : la ligne part avec le `SaveChangesAsync` de l'action, dans la même transaction. Si l'action échoue, la ligne n'existe pas ; si la ligne ne peut pas s'écrire, l'action non plus.
- **L'auteur est passé par l'action** : le compte connecté, ou `null` pour la commande CEN-01, ce qui donne « SchoolHub ».
- **Le centre de la ligne est le centre courant**, comme pour toute donnée d'un centre. La commande CEN-01 n'a pas de compte connecté : elle définit elle-même le centre courant, celui qu'elle vient de créer, avant d'écrire la ligne.

### Personne ne modifie ni n'efface une ligne

- **Aucune adresse d'API** ne modifie ni ne supprime une ligne : il n'y a que `Noter`.
- **Un trigger PostgreSQL** refuse toute modification (`UPDATE`), suppression (`DELETE`) et vidage (`TRUNCATE`) de la table `journal`, avec le message « Le journal ne se modifie pas et ne s'efface pas ».
- **Le SQL du trigger est écrit à un seul endroit** (une constante du journal). La migration l'exécute. La base des tests, créée à partir du modèle par `EnsureCreated` (qui ignore les migrations), l'exécute aussi juste après sa création : sans cela, le trigger n'existerait pas dans les tests.

### La séparation des centres

La ligne implémente `IAppartientAuCentre` : le filtre automatique la cache aux autres centres, et `ModeleTests` vérifie qu'elle ne l'oublie pas. Un test d'isolation vérifie qu'un centre ne lit jamais le journal d'un autre.

## 2. Les comptes du centre (CPT-04, CPT-05)

### Les adresses

Toutes sont **réservées au propriétaire** (politique `Proprietaire`) : une secrétaire reçoit 403 « Accès réservé au propriétaire », et une personne non connectée reçoit 401.

| Adresse | Ce qu'elle fait | Réponse |
|---|---|---|
| `GET /api/comptes` | la liste des comptes du centre | 200, la liste |
| `POST /api/comptes` | inviter une personne : `{ prenom, nom, email, role }` | 201, le compte créé |
| `POST /api/comptes/{id}/invitation` | renvoyer l'invitation | 200, le compte |
| `POST /api/comptes/{id}/desactivation` | désactiver le compte | 200, le compte |
| `POST /api/comptes/{id}/reactivation` | réactiver le compte | 200, le compte |

**Un compte de la liste** : `{ id, prenom, nom, email, role, etat, moi }`, où `moi` dit si c'est le compte de la personne connectée. La liste montre d'abord les comptes actifs et invités, puis les désactivés, chaque groupe trié par prénom puis par nom.

**Les refus** répondent `{ "message": "…" }`, comme le reste de l'API :

| Cas | Statut | Message |
|---|---|---|
| Prénom, nom ou rôle manquant, ou rôle inconnu | 400 | « Le prénom, le nom et le rôle sont obligatoires » |
| Prénom ou nom de plus de 100 caractères (la taille des colonnes) | 400 | « Le prénom et le nom font au plus 100 caractères » |
| Email manquant ou pas valable | 400 | « Cet email n'est pas valable » |
| Email déjà utilisé, dans ce centre ou dans un autre | 409 | « Cet email est déjà utilisé sur SchoolHub » |
| L'email d'invitation n'est pas parti | 503 | « L'invitation n'est pas partie : réessayez dans un moment » |
| Compte inconnu, ou d'un autre centre | 404 | « Compte introuvable » |
| Renvoyer l'invitation d'un compte qui n'est pas invité | 409 | « Ce compte n'attend plus d'invitation » |
| Désactiver son propre compte | 400 | « Vous ne pouvez pas désactiver votre propre compte » |
| Désactiver un compte déjà désactivé | 409 | « Ce compte est déjà désactivé » |
| Réactiver un compte qui n'est pas désactivé | 409 | « Ce compte n'est pas désactivé » |
| Désactiver le dernier propriétaire actif | 409 | « Il doit rester au moins un propriétaire actif » |

### La liste filtre elle-même sur le centre

Les comptes ne passent pas par le filtre automatique : c'est une donnée commune, lue à la connexion avant de connaître le centre ([conception des comptes](2026-11-02-comptes-et-connexion-design.md#le-compte-est-une-donnée-commune)). La liste et chaque action filtrent donc **explicitement** sur le centre courant : un compte d'un autre centre répond « Compte introuvable », exactement comme un compte qui n'existe pas. Un test d'isolation le vérifie pour chaque adresse.

### Inviter

1. Le prénom, le nom, un email valable et le rôle sont obligatoires. Les espaces autour sont retirés. L'email valable suit la même règle que la commande CEN-01 : la vérification devient commune aux deux.
2. Un email déjà utilisé sur SchoolHub, quel que soit le centre, est refusé. Identity compare les emails sans tenir compte des majuscules. Si deux invitations du même email arrivent à la même seconde, l'index unique de la base refuse la seconde, avec le même message.
3. **Tout ou rien**, comme CEN-01 : le compte (invité), la ligne « Compte créé » et l'envoi de l'email forment une seule transaction, validée seulement une fois l'email parti. Si l'email ne part pas, rien n'est créé.

L'email est celui de CPT-02 : « Vous êtes invité(e) à rejoindre le Centre Les Jasmins sur SchoolHub », avec le lien valable 7 jours.

### Renvoyer l'invitation

Seulement pour un compte **invité**. Le tampon de sécurité du compte change, ce qui rend l'ancien lien caduc, puis un nouveau lien part, valable 7 jours. Si l'email ne part pas, rien ne change : l'ancien lien marche encore. Ce n'est pas noté au journal.

### Désactiver

1. Pour un compte **actif ou invité**, et jamais le sien.
2. Le compte passe à « désactivé », et son tampon de sécurité change : ses liens d'invitation et de mot de passe oublié ne marchent plus. Sa session tombe à sa prochaine action (c'est déjà le cas : le serveur relit le compte à chaque requête).
3. La ligne « Compte désactivé » s'écrit, avec l'état d'avant (actif ou invité) et « Désactivé ».
4. Son nom reste sur tout ce qu'il a fait : rien n'est effacé.

**Il reste toujours un propriétaire actif.** Puisque personne ne se désactive soi-même, la personne qui clique en est un, et elle le reste. Le seul cas qui ferait exception : Hedi et Mehdi, les deux propriétaires, se désactivent l'un l'autre à la même seconde. Pour l'éviter, la désactivation se fait dans une transaction qui **verrouille la ligne du centre** (`SELECT … FOR UPDATE`) : les désactivations d'un même centre passent l'une après l'autre. Une fois le verrou pris, le serveur relit le compte de celui qui clique, et refuse s'il n'est plus actif. Il vérifie enfin qu'il restera au moins un propriétaire actif.

### Réactiver

1. Seulement pour un compte **désactivé**.
2. Un compte qui avait choisi son mot de passe redevient **actif**, avec ce mot de passe.
3. Un compte qui ne l'avait jamais choisi (l'email mal tapé, par exemple) redevient **invité**, sans email. Le propriétaire peut ensuite renvoyer l'invitation.
4. La ligne « Compte réactivé » s'écrit, avec « Désactivé » et l'état d'après (actif ou invité).

## 3. La commande CEN-01

La commande crée le centre, puis définit ce centre comme **centre courant**. Elle crée ensuite le premier propriétaire et note « Compte créé » au journal, avec « SchoolHub » comme auteur, avant d'envoyer l'invitation. C'est toujours tout ou rien : si l'email ne part pas, il n'y a ni centre, ni compte, ni ligne au journal.

Les centres qui existent déjà sur les postes de développement n'ont pas cette ligne : ce sont des données de test.

## 4. La page « Comptes »

À l'adresse `/comptes`, dans le menu du propriétaire, elle remplace la page « bientôt ». La secrétaire voit toujours « Accès réservé au propriétaire ».

```
Comptes                                                   [ + Inviter une personne ]
Les personnes qui se connectent à SchoolHub pour le Centre Les Jasmins.

┌───────────────────────────────────────────────────────────────────────────────┐
│ Prénom [        ]  Nom [        ]  Email [                ]  Rôle [Secrétaire ▾] │
│                                         [ Annuler ]  [ Envoyer l'invitation ]   │
└───────────────────────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────────────────────┐
│ Hedi Ben Salah (vous)        Propriétaire   Actif                              │
│ hedi@exemple.tn                                                                │
├───────────────────────────────────────────────────────────────────────────────┤
│ Mehdi Trabelsi               Propriétaire   Actif                [ Désactiver ] │
├───────────────────────────────────────────────────────────────────────────────┤
│ Sonia Mabrouk                Secrétaire     Invité  [ Renvoyer l'invitation ]   │
│ sonia.mabrouk@exemple.tn                                         [ Désactiver ] │
├───────────────────────────────────────────────────────────────────────────────┤
│ Amira Jlassi                 Secrétaire     Désactivé            [ Réactiver ] │
└───────────────────────────────────────────────────────────────────────────────┘
```

- **Le formulaire d'invitation** s'ouvre avec « Inviter une personne ». Le rôle est « Secrétaire » par défaut. Les champs vides sont signalés avant l'envoi. Après l'envoi, le formulaire se ferme et le message « Invitation envoyée à sonia.mabrouk@exemple.tn » s'affiche.
- **Chaque ligne** montre le prénom et le nom, l'email, le rôle, et l'état dans une pastille de la maquette : actif en vert (`pill ok`), invité en bleu (`pill info`), désactivé en gris (`pill muted`). La ligne de la personne connectée porte « (vous) » et n'a pas de bouton « Désactiver ».
- **Les boutons dépendent de l'état** : « Renvoyer l'invitation » pour un compte invité, « Désactiver » pour un compte actif ou invité, « Réactiver » pour un compte désactivé.
- **« Désactiver » demande une confirmation dans la ligne même** (« Désactiver le compte de Sonia Mabrouk ? Oui · Non »), pas dans une fenêtre du navigateur.
- **Après chaque action**, la ligne se met à jour avec le compte renvoyé par le serveur, et un message vert le confirme : « Nouvelle invitation envoyée à … », « Compte de Sonia Mabrouk désactivé », « Compte de Sonia Mabrouk réactivé ». Un refus du serveur s'affiche en rouge, avec son message. Pendant l'envoi, le bouton se bloque.
- **Une liste plutôt qu'un tableau** : sur un téléphone, le contenu de chaque ligne passe à la ligne, sans défilement de côté.
- Les pastilles (`.pill`) passent de la maquette à `styles.css`, pour servir aux pages suivantes.

## 5. L'organisation du code

```
backend/src/SchoolHub/Socle/
├── Journal/
│   ├── LigneDuJournal.cs          la ligne, ActionDuJournal, et sa configuration EF Core
│   ├── JournalDuCentre.cs         le service Noter, et le SQL du trigger
├── Comptes/
│   ├── ComptesEndpoints.cs        les 5 adresses de la page « Comptes »
│   ├── GestionDesComptes.cs       inviter, renvoyer, désactiver, réactiver : les règles et le journal
│   ├── Compte.cs                  + Reactiver()
│   └── …
├── Centres/CreationDeCentre.cs    + la ligne du journal
└── Donnees/Migrations/            + la table journal et son trigger

frontend/src/app/
├── pages/comptes/                 la page, son modèle et ses tests
└── app.routes.ts                  « comptes » montre la page au lieu de « bientôt »
```

- `ComptesEndpoints` lit la requête et répond ; `GestionDesComptes` porte les règles, et c'est lui que les tests de règles appellent à travers l'API.
- La règle « email valable » quitte `CreationDeCentre` pour une petite classe commune aux deux.
- Les README du backend et du frontend gagnent une section « Le journal » (comment une story y écrit) et la page « Comptes ».

## 6. Les tests

### Côté .NET (`dotnet test`)

Sur le PostgreSQL jetable, avec de vraies requêtes HTTP, comme les tests existants :

| Sujet | Ce que les tests vérifient |
|---|---|
| Inviter | Le compte est créé « invité » dans le centre de la personne connectée, l'email part avec un lien qui marche (CPT-02), et la ligne « Compte créé » s'écrit. Un champ manquant ou un email pas valable est refusé. Un email déjà pris, dans ce centre ou dans un autre (en majuscules aussi), est refusé. Si l'email ne part pas, il n'y a ni compte ni ligne au journal. |
| Liste | Elle montre les comptes du centre, avec `moi` sur celui de la personne connectée, dans l'ordre prévu, et jamais ceux d'un autre centre. |
| Renvoyer | Le nouveau lien marche et l'ancien ne marche plus. Refusé pour un compte actif ou désactivé. Si l'email ne part pas, l'ancien lien marche encore. |
| Désactiver | La session ouverte de Sonia tombe à sa requête suivante, et elle ne peut plus se connecter. Le lien d'invitation d'un compte invité désactivé ne marche plus. Désactiver son propre compte est refusé. La ligne « Compte désactivé » porte l'état d'avant. Deux propriétaires qui se désactivent l'un l'autre en même temps : l'un des deux est refusé, et il reste un propriétaire actif. |
| Réactiver | Sonia se reconnecte avec son ancien mot de passe. Un compte qui n'avait jamais choisi son mot de passe redevient invité. Refusé pour un compte qui n'est pas désactivé. La ligne « Compte réactivé » s'écrit. |
| Sécurité | Chaque adresse refuse une secrétaire (403) et une personne non connectée (401). Chaque action sur un compte d'un autre centre répond « Compte introuvable ». |
| Journal | La ligne a la bonne heure, le bon auteur, le bon centre. Un centre ne lit jamais le journal d'un autre. Modifier, supprimer ou vider la table est refusé par la base. |
| CEN-01 | La commande écrit « Compte créé » avec « SchoolHub » comme auteur. Si l'email ne part pas, il n'y a pas de ligne. |

### Côté Angular (`npm test`, Vitest)

Les tests simulent les réponses du serveur (`HttpTestingController`) :

| Sujet | Ce que les tests vérifient |
|---|---|
| Liste | Les comptes s'affichent avec leur rôle et leur état. La ligne « (vous) » n'a pas de « Désactiver ». Les boutons suivent l'état. |
| Inviter | Les champs vides ne partent pas. L'envoi poste les bons champs, ferme le formulaire, ajoute le compte et affiche le message. Un refus affiche le message du serveur. |
| Actions | « Désactiver » demande confirmation ; « Non » n'envoie rien ; « Oui » envoie et met la ligne à jour. « Renvoyer » et « Réactiver » envoient et affichent leur message. Le bouton se bloque pendant l'envoi. |
| Adresses | `/comptes` montre la page au propriétaire, et « Accès réservé au propriétaire » à la secrétaire. |

### À la main, dans le navigateur

Avec le vrai serveur et PostgreSQL dans Docker :

1. en propriétaire, inviter une secrétaire, puis ouvrir le lien écrit dans la console et choisir son mot de passe ;
2. renvoyer l'invitation d'un autre compte invité : l'ancien lien affiche « Ce lien a expiré » ;
3. connecté en secrétaire dans une autre fenêtre, se faire désactiver : la page suivante ramène à la connexion, et la connexion affiche « Ce compte est désactivé » ;
4. réactiver la secrétaire : elle se reconnecte avec son ancien mot de passe ;
5. vérifier les lignes du journal dans la base ;
6. tout cela sur une largeur de téléphone (375 pixels) et d'ordinateur, en mode clair et en mode sombre.

## Points d'attention

- **La base des tests ne passe pas par les migrations** : sans l'exécution du SQL du trigger après `EnsureCreated`, le test du trigger échouerait, et le trigger pourrait manquer sans qu'on le voie. Le SQL est donc partagé entre la migration et la création de la base des tests.
- **Le verrou de la désactivation** (`SELECT … FOR UPDATE` sur la ligne du centre) doit être pris dans la même transaction que la modification du compte, avant de relire celui qui clique.
- **Le message de l'email déjà pris** dit qu'un compte existe avec cet email, quel que soit le centre. C'est voulu par CPT-04, et seul un propriétaire connecté peut le lire.
