# 01. Comptes et accès (CPT)

Deux rôles fixes, Propriétaire et Secrétaire. Un compte appartient à un seul centre. Les professeurs, les parents et les élèves n'ont pas de compte ([ticket 03](../wayfinder/cadrage-mvp/issues/03-roles-comptes-acces.md)).

### CPT-01 · Se connecter et se déconnecter

En tant que secrétaire, je veux me connecter avec mon email et mon mot de passe, afin de travailler sur mon centre depuis n'importe quel ordinateur ou téléphone.

**Critères d'acceptation**

- Amira saisit son email et son mot de passe : elle arrive sur « Aujourd'hui » du Centre Les Jasmins.
- Avec un mauvais mot de passe, le message dit « Email ou mot de passe incorrect », sans préciser lequel des deux est faux.
- Après 5 essais faux de suite, le compte est bloqué 15 minutes.
- Un compte désactivé ne peut pas se connecter.
- Sans activité pendant 12 heures, il faut se reconnecter.
- « Se déconnecter » ramène à la page de connexion ; le bouton « retour » du navigateur ne montre plus aucune donnée.

Maquette : pas d'écran de connexion (à dessiner pendant le développement).

### CPT-02 · Choisir son mot de passe depuis le lien d'invitation

En tant que nouvelle secrétaire, je veux choisir mon mot de passe depuis le lien reçu par email, afin que personne d'autre ne le connaisse.

**Critères d'acceptation**

- Amira reçoit l'email « Rejoindre le Centre Les Jasmins sur SchoolHub », avec un lien.
- Le lien ouvre une page où elle choisit son mot de passe, d'au moins 8 caractères, puis elle est connectée.
- Le lien ne sert qu'une fois et expire au bout de 7 jours. Un lien expiré affiche « Ce lien a expiré : demandez au propriétaire une nouvelle invitation ».

Maquette : pas d'écran.

### CPT-03 · Retrouver l'accès par « mot de passe oublié »

En tant que secrétaire, je veux recevoir un lien pour changer mon mot de passe, afin de retrouver l'accès sans déranger le propriétaire.

**Critères d'acceptation**

- Sur la page de connexion, « Mot de passe oublié » demande l'email.
- La page répond toujours « Si un compte existe pour cet email, un lien vient de partir », pour ne pas révéler qui a un compte.
- Le lien est valable 1 heure et ne sert qu'une fois.
- Un compte désactivé ne reçoit pas de lien.

Maquette : pas d'écran.

### CPT-04 · Inviter une secrétaire ou un autre propriétaire

En tant que propriétaire, je veux créer le compte d'une secrétaire ou d'un associé, afin qu'il travaille sur SchoolHub avec ses propres accès.

**Critères d'acceptation**

- Le propriétaire saisit le prénom, le nom, l'email et le rôle (Secrétaire ou Propriétaire) : l'invitation part (CPT-02).
- Un email déjà utilisé sur SchoolHub, dans ce centre ou dans un autre, est refusé : une personne qui travaille dans deux centres a deux comptes, avec deux emails.
- La page « Comptes » montre chaque personne, son rôle et son état : invité, actif ou désactivé.
- « Renvoyer l'invitation » envoie un nouveau lien à un compte encore invité ; l'ancien lien ne marche plus.
- La création du compte va au journal.

Maquette : pas d'écran.

### CPT-05 · Désactiver un compte

En tant que propriétaire, je veux désactiver le compte d'une personne qui quitte le centre, afin qu'elle ne puisse plus se connecter, sans perdre ce qu'elle a fait.

**Critères d'acceptation**

- Le propriétaire désactive le compte de Sonia : elle ne peut plus se connecter, et si elle est connectée, elle est déconnectée à sa prochaine action.
- Son nom reste sur les reçus, les paiements, les appels et les clôtures qu'elle a faits.
- Il reste toujours au moins un propriétaire actif : désactiver le dernier est refusé.
- « Réactiver » rend l'accès à un compte désactivé, avec le même email.
- La désactivation et la réactivation vont au journal.

Maquette : pas d'écran.

### CPT-06 · Cacher à la secrétaire ce qui est réservé au propriétaire

En tant que propriétaire, je veux que la secrétaire ne voie pas les parties réservées, afin que les salaires, la rémunération des professeurs et la rentabilité restent confidentiels.

**Critères d'acceptation**

- Connectée en secrétaire, Amira ne voit dans le menu ni « Parts des professeurs », ni « Tableau de bord », ni « Ressources humaines », ni « Journal », ni « Comptes », ni « Paramètres du centre ».
- Si elle tape l'adresse d'une de ces pages, elle lit « Accès réservé au propriétaire », et le serveur ne lui envoie aucune donnée.
- Sur la fiche d'un élève, elle voit un prix spécial déjà accordé, mais ne peut ni en accorder un, ni dispenser de l'assurance (ELV-05, ELV-06).
- Sur la fiche d'un professeur, elle ne voit ni ses parts, ni la part du centre.

Maquette : le menu en rôle secrétaire et en rôle propriétaire.
