# 00. Exigences communes

Ces règles valent pour **toutes** les stories. Une story n'est terminée que si elle les respecte aussi. Elles ne sont pas répétées dans chaque story.

## Accès

- **Depuis n'importe quel navigateur récent**, sur ordinateur comme sur téléphone. Chaque écran s'adapte à la largeur : sur téléphone, le menu devient des onglets en bas ([maquette](../maquette/index.html), vue téléphone).
- **Installable sur l'écran d'accueil** du téléphone (PWA), sans passer par un store.
- **Pas de fonctionnement hors connexion** : sans Internet, l'appel se fait sur la feuille d'appel (SEA-08), puis se recopie (SEA-09).
- **La date du jour** est toujours affichée (cahier des charges, § 9).

## Sécurité

- **Il faut être connecté** pour tout, sauf le formulaire public d'inscription (DEM-01), la page qui choisit le mot de passe (CPT-02) et celle du mot de passe oublié (CPT-03).
- **Un centre ne voit jamais les données d'un autre centre**, même en tapant une adresse à la main. Chaque nouvelle donnée a son test d'isolation.
- **Deux rôles** : la secrétaire ne voit ni les ressources humaines, ni les comptes, ni les paramètres du centre, ni les parts des professeurs, ni le tableau de bord, ni le journal (CPT-06). Le refus se fait sur le serveur, pas seulement en cachant un bouton.
- **Connexion chiffrée** (HTTPS) partout.

## Langue, dates et montants

- **Tout est en français** : écrans, reçus, messages, emails.
- **Dates** au format 15/01/2028, **heures** au format 8h, 10h30.
- **Montants en dinars tunisiens, au millime** : « 37,500 TND ». La plateforme n'arrondit pas.
- **Quand un prix se divise mal**, la dernière part prend la différence : une Révision à 50 TND en 3 séances donne 16,667 + 16,667 + 16,666 = 50,000 TND.
- **Espèces seulement** : ni chèque, ni virement, ni D17.

## Ce qui change en cours d'année

Une seule règle pour le prix d'un groupe (GRP-04), le montant de l'assurance (CEN-02) et la part du centre (CEN-02, PRT-04) : **ce qui a commencé, ou est déjà dû, garde l'ancienne valeur ; la nouvelle vaut pour ce qui commence après.**

| Ce qui change | Exemple | Résultat |
|---|---|---|
| Prix d'un groupe | 40 → 45 TND le 01/02 | Mois 5, commencé le 15/01 : 40 TND · Mois 6 : 45 TND |
| Montant de l'assurance | 20 → 25 TND en janvier | due depuis septembre : 20 TND · due à partir de février : 25 TND |
| Part du centre | 5 % → 10 % le 01/02 | Formation commencée en janvier : 5 % · Formation qui commence en mars : 10 % |

**Seule exception, le prix spécial** (ELV-05) : il vaut tout de suite pour tout ce qui n'est pas encore payé, Mois en cours compris.

## Rien ne disparaît

- **Un reçu n'est jamais supprimé ni modifié** : il peut seulement être annulé (CAI-07).
- **Un compte se désactive** (CPT-05), **un groupe se ferme ou se termine** (GRP-08, CEN-04), **un professeur part** (GRP-03) : leur historique reste consultable.
- **Seule une fiche sans aucun historique** (ni paiement, ni présence, ni groupe) peut être supprimée, en cas d'erreur de saisie (ELV-11).
- **Pas de suppression en masse.**

## Journal

Chaque action ci-dessous écrit une ligne au journal : **qui, quoi, quand**, avec la valeur d'avant et celle d'après. La ligne s'écrit dès la story de l'action, même si l'écran du journal (TDB-04) vient plus tard. Personne ne peut modifier ni effacer une ligne.

- encaissement, annulation de reçu, sortie de caisse, paiement noté « déjà payé avant SchoolHub » ;
- prix spécial accordé ou retiré, dispense d'assurance ;
- arrêt d'inscription, changement de groupe ;
- correction de présence ;
- annulation et déplacement de séance, fermeture d'un groupe ;
- création, désactivation et réactivation d'un compte.

## Messages

- **WhatsApp** : la plateforme prépare un lien `wa.me` avec le texte déjà écrit ; la secrétaire l'ouvre et l'envoie depuis le WhatsApp du centre. Rien ne part tout seul.
- **Les destinataires** d'un message sur un élève dépendent de son âge. La règle vaut pour tous les messages (séance annulée, déplacée ou ajoutée, changement d'horaire, absence, relance, reçu, confirmation d'inscription), par WhatsApp comme par email :

  | Élève | Destinataires | Exemple |
  |---|---|---|
  | Majeur | l'élève seul | Sami, 19 ans, Formation informatique |
  | Mineur avec un téléphone | l'élève **et** son parent : deux messages | Yassine, 16 ans, lycée ; Adem, 14 ans, collège |
  | Mineur sans téléphone | le parent seul | Lina, 9 ans, école |

  Avec des liens `wa.me` envoyés à la main, un message en double demande deux envois : prévenir 5 absents mineurs, c'est 10 messages.
- **Les textes restent neutres et courts**, sans publicité, pour pouvoir devenir plus tard des modèles de l'API WhatsApp.
- **Les emails** partent automatiquement, depuis l'adresse du produit, avec le nom du centre comme expéditeur.

## Rapidité au guichet

Un groupe peut compter 20 élèves qui paient à la suite (cahier des charges, § 9).

- **La recherche d'un élève** affiche ses résultats pendant la frappe.
- **Un écran courant s'affiche en moins de 2 secondes** sur un téléphone en 4G.
- **Encaissement groupé de 20 élèves** : après le clic « Encaisser », les 20 paiements et leurs 20 reçus sont enregistrés en moins de 5 secondes, et le PDF des 20 reçus est prêt en moins de 5 secondes.

## Listes

Chaque liste de la story qui la crée peut être filtrée, imprimée et exportée en Excel ([épopée 12](12-listes.md)).
