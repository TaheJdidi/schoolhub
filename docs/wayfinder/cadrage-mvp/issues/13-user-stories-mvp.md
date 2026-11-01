# 13. User stories du MVP

Type: grilling
Mode: HITL
Status: resolved
Blocked by: 01, 02, 03, 04, 05, 06, 07, 09, 10, 11

## Question

Quelles user stories composent le MVP, avec quels critères d'acceptation et dans quel ordre de priorité ?

Le MVP couvre tout le cahier des charges (tour 1, Q4), plus les ajouts décidés dans les tickets précédents.

## Avancement

> Grilling terminé le 01/11/2026 : toutes les questions sont tranchées, et la synthèse est dans « Réponse », en bas du ticket. Les stories sont dans [docs/user-stories/](../../../user-stories/README.md), et [CONTEXT.md](../../../../CONTEXT.md) est à jour.

### Décisions prises (tour 1, 01/11/2026)

- **Q1. Format** : une phrase « En tant que…, je veux…, afin de… », des critères d'acceptation en liste avec un exemple chiffré, et l'écran de la maquette concerné. Un fichier par épopée dans `docs/user-stories/`, un sommaire, et un numéro par épopée (CAI-03).
- **Q2. Douze épopées** : comptes et accès ; centre ; professeurs et groupes ; élèves et inscriptions ; demandes d'inscription ; séances et appel ; caisse et reçus ; bibliothèque ; parts des professeurs ; RH et achats ; tableau de bord et journal ; listes. Les messages WhatsApp et les emails vont dans l'épopée de l'action qui les déclenche.
- **Q4. Un centre se crée par une commande** lancée par Taha ou Lassad, avec le nom du centre et l'email du propriétaire, qui reçoit son lien d'invitation. Pas d'écran d'administration de la plateforme.
- **Q5. Paramètres du centre**, modifiables par le propriétaire seul : nom, téléphone et email du centre ; dates de l'année scolaire ; montant de l'assurance ; prix et capacité de la bibliothèque ; part du centre par défaut ; niveaux ajoutés ; lien public et QR code. Un nouveau prix ne change pas ce qui est déjà payé. Pas de logo dans le MVP.
- **Q7. Listes** : impression et export Excel des élèves, professeurs, groupes, reçus, entrées de la bibliothèque, achats et employés ; import des élèves (avec leur groupe, en option) et des professeurs depuis un fichier modèle ; sélection multiple pour inscrire dans un groupe, changer de niveau, imprimer ou exporter. Pas de suppression en masse ; seule une fiche sans aucun historique peut être supprimée.
- **Q8. Relance** : un bouton « Relancer » sur chaque élève orange ou rouge prépare un message WhatsApp au parent, et la date de la dernière relance reste visible. Pas de file d'envoi du jour.
- **Q9. Les montants gardent les millimes** : une Révision à 50 TND en 3 séances donne 16,667 TND la séance ; pas d'arrondi au dinar.
- **Q3, Q6 et Q10** n'ont pas été comprises : elles sont reformulées au tour 2.

### Décisions prises (tour 2, 01/11/2026)

- **Q3. Une seule livraison, complète, pour la rentrée scolaire 2027-2028.** Pas de livraison en plusieurs fois au centre pilote : les étiquettes P1, P2 et P3 n'ont plus lieu d'être. Reporté dans le [ticket 14](14-estimation-plan-livraison.md).
- **Q6. On ne recopie pas le passé** : un groupe peut être créé « en cours », par exemple au Mois 5, et l'assurance cochée « déjà payée ». Utile pour un pilote qui démarre quelques semaines après la rentrée, et pour tout centre qui arrive en cours d'année.
- **Q10. Une page `00-exigences-communes.md`** pour les règles valables partout ; les travaux techniques sont listés à part, pour le ticket 14.
- **Q11. La dernière séance prend la différence** : 16,667 + 16,667 + 16,666 = 50,000 TND.

### Décisions prises (tour 3, 01/11/2026)

- **Q12. Pas d'étiquette de priorité** : le sommaire range les épopées dans l'ordre où on les construit. Le ticket 14 vérifie que tout tient avant la rentrée.
- **Q13. À la fin de l'année scolaire, les groupes d'Étude deviennent des groupes terminés**, comme une Révision ou une Formation après sa dernière séance. À la rentrée, la secrétaire crée les nouveaux groupes comme au premier jour : pas de bouton « Reconduire » (GRP-09 retirée).
- **Q14. Un professeur sans groupe ouvert peut être marqué « parti »** : il sort de la liste, son historique et ses parts restent consultables.
- **Q15. Liste validée**, sans GRP-09 : 78 stories.

### Stories écrites (01/11/2026)

Les 78 stories sont écrites dans [docs/user-stories/](../../../user-stories/README.md), avec les [exigences communes](../../../user-stories/00-exigences-communes.md) et les [travaux techniques](../../../user-stories/travaux-techniques.md).

### Décisions prises (tour 4, 01/11/2026)

- **Validées** : H1, H2, H6, H8, H9, H10, H11, H12, H13 et H14. H15 n'a pas reçu de réponse : elle prend la recommandation.
- **H7. Les messages d'une séance supplémentaire ou d'un changement d'horaire vont aux élèves.** Ils vont aux parents seulement pour les élèves de l'école ou du collège (1ʳᵉ à 9ᵉ année de base), pas pour ceux du lycée.
- **H16. Deux minutes, c'est trop** pour un encaissement groupé de 20 élèves : la cible est reformulée au tour 5.
- **H3, H4 et H5** n'ont pas été comprises : elles sont reformulées au tour 5.

### Décisions prises (tour 5, 01/11/2026)

- **H3. Pour les prix d'un groupe, ce qui a commencé garde l'ancien prix** : le Mois 5, commencé le 15/01, reste à 40 TND ; le Mois 6 passe à 45 TND. La part du centre et l'assurance sont reprises au tour 6.
- **H4. Un prix spécial vaut pour tout ce qui n'est pas encore payé**, Mois en cours compris, sans remboursement de ce qui est payé. Les tranches qui restent d'une Formation se partagent le nouveau prix.
- **H5. Pour une Formation déjà commencée au démarrage, la part du professeur se calcule sur tout le prix**, y compris ce qui a été payé avant SchoolHub.
- **H7 bis. Un élève majeur reçoit tous les messages ; pour un élève mineur, chaque message part en double, à l'élève et à son parent.** Le lien avec la règle d'H7 (école, collège, lycée) est repris au tour 6.
- **H16 bis. Vitesse** : après le clic « Encaisser », les 20 paiements et leurs 20 reçus sont enregistrés en moins de 5 secondes, et le PDF des 20 reçus est prêt en moins de 5 secondes.

### Décisions prises (tour 6, 01/11/2026)

- **H3 ter. Une seule règle pour ce qui change en cours d'année** : prix d'un groupe, montant de l'assurance et part du centre. Ce qui a commencé, ou est déjà dû, garde l'ancienne valeur. Seule exception, le prix spécial (H4).
- **H7 ter. Une seule règle pour tous les messages**, qui remplace celle d'H7 : un élève majeur reçoit seul ses messages ; un mineur les reçoit avec son parent ; un mineur sans téléphone, par son parent seul.

### Questions du tour 6 (répondues le 01/11/2026)

- **H3 ter. Une seule règle pour tout ce qui change en cours d'année ?** (a) Prix d'un groupe, montant de l'assurance et part du centre suivent la même règle : ce qui a commencé, ou est déjà dû, garde l'ancienne valeur. Une assurance due depuis septembre reste à 20 TND si le centre passe à 25 TND en janvier ; une Formation commencée garde la part du centre de son début. (b) La règle vaut pour les prix des groupes seulement : la nouvelle part du centre vaut pour tout ce qui n'est pas fini, et l'assurance pas encore payée passe au nouveau montant. ➡️ (a).
- **H7 ter. La règle majeur et mineur remplace-t-elle celle de l'école, du collège et du lycée ?** (a) Oui, pour tous les messages : majeur, l'élève seul ; mineur, l'élève et son parent ; un mineur sans téléphone (souvent à l'école), le parent seul. (b) Non : les messages sur les séances suivent H7, et les autres messages suivent la règle majeur et mineur. ➡️ (a), plus simple à retenir. Avec des liens `wa.me` envoyés à la main, un message en double demande deux envois à la secrétaire.

### Questions du tour 5 (répondues le 01/11/2026)

- **H3. Un prix ou une part qui change en cours d'année.** Le prix du groupe de Karim passe de 40 à 45 TND le 01/02, alors que le Mois 5 a commencé le 15/01. (a) Ce qui a déjà commencé garde l'ancienne règle : le Mois 5 reste à 40 TND, le Mois 6 passe à 45 TND. La même règle vaut pour la part du centre d'un professeur : une Révision ou une Formation déjà commencée garde l'ancienne part. (b) Le nouveau prix vaut aussi pour les Mois commencés et pas encore payés. ➡️ (a).
- **H4. Un prix spécial accordé en cours de route.** Adem a payé les Mois 1 à 3 à 40 TND ; le 01/12, le propriétaire lui accorde 20 TND. (a) Les Mois payés restent à 40 TND, sans remboursement, et tout ce qui n'est pas encore payé passe à 20 TND, Mois en cours compris. Pour une Formation en tranches, les tranches qui restent se partagent ce qui reste à payer : 300 TND en 3 × 100 TND avec un prix spécial de 150 TND donnent 3 × 50 TND. (b) Le prix spécial ne vaut qu'à partir du Mois suivant. ➡️ (a).
- **H5. Une Formation déjà commencée au démarrage de SchoolHub.** Formation de 10 séances à 300 TND, à la séance 4 au démarrage ; la tranche 1 (100 TND) a été payée dans le carnet. (a) La part du professeur se calcule à la fin sur tout le prix, 300 TND par élève, car il n'a encore rien reçu pour cette Formation ; (b) seulement sur ce qui est dû dans SchoolHub, 200 TND. ➡️ (a).
- **H7 bis. La règle d'H7 vaut-elle pour les autres messages ?** (a) Elle vaut pour tous les messages sur les séances (annulation, déplacement, séance supplémentaire, changement d'horaire) ; les messages d'absence, de relance, de reçu et de confirmation d'inscription vont toujours au parent, qui surveille et qui paie. (b) Elle vaut pour tous les messages. ➡️ (a). Un élève majeur sans parent reçoit tout ; un niveau ajouté par le centre (CEN-03) compte comme le lycée.
- **H16 bis. Quelle vitesse pour l'encaissement groupé ?** Les deux minutes comptaient aussi les clics de la secrétaire. ➡️ Après le clic « Encaisser », les 20 paiements et leurs 20 reçus sont enregistrés en moins de 5 secondes, et le PDF des 20 reçus est prêt en moins de 5 secondes.

### Hypothèses à valider (tour 4, répondu le 01/11/2026)

En écrivant les critères, ces points n'étaient tranchés par aucun ticket. Les stories appliquent la règle ci-dessous ; une hypothèse refusée se corrige dans la story citée.

- **H1.** Après 5 essais de mot de passe faux, le compte est bloqué 15 minutes ; sans activité pendant 12 heures, il faut se reconnecter (CPT-01).
- **H2.** Un compte désactivé peut être réactivé (CPT-05).
- **H3.** Un nouveau prix de groupe vaut pour les Mois pas encore commencés (GRP-04) ; une nouvelle part du centre, pour les périodes pas encore finies (CEN-02, PRT-04).
- **H4.** Un prix spécial vaut pour ce qui n'est pas encore payé, et les tranches d'une Formation baissent dans la même proportion (ELV-05).
- **H5.** Pour une Révision ou une Formation déjà en cours au démarrage, la part du professeur se calcule sur tout le prix, y compris ce qui a été payé avant SchoolHub (GRP-07).
- **H6.** Dans une Révision ou une Formation, une séance annulée doit être remplacée pour garder le nombre de séances prévu (SEA-03).
- **H7.** Une séance supplémentaire et un changement d'horaire préparent aussi un message WhatsApp aux parents (SEA-05, SEA-06).
- **H8.** L'encaissement groupé comprend l'assurance quand elle est due (CAI-04).
- **H9.** Le propriétaire qui encaisse a sa propre caisse ; un encaissement après la clôture ouvre une deuxième caisse du jour ; une caisse oubliée se clôture le lendemain (CAI-09).
- **H10.** Un reçu annulé par le propriétaire après la clôture ne change pas la clôture déjà faite (CAI-07).
- **H11.** Un achat se modifie ou se supprime par son auteur le jour même, avant la clôture de sa caisse ; ensuite, par le propriétaire seul (RHA-04).
- **H12.** Un employé qui quitte le centre est marqué « parti », comme un professeur (RHA-01).
- **H13.** Les congés se comptent par année du calendrier ; un salaire compte dans le mois qu'il paie (RHA-02, RHA-03).
- **H14.** Rien n'est importé tant que le fichier Excel a une erreur (LST-03, LST-04).
- **H15.** L'année du tableau de bord est l'année scolaire, comme dans la maquette (TDB-01).
- **H16.** Un écran courant s'affiche en moins de 2 secondes en 4G, et un encaissement groupé de 20 élèves prend moins de 2 minutes ([exigences communes](../../../user-stories/00-exigences-communes.md#rapidité-au-guichet)).

### Questions du tour 3 (répondues le 01/11/2026)

- **Q12. Ordre dans le sommaire.** Avec une seule livraison : (a) pas d'étiquette de priorité ; le sommaire range les épopées dans l'ordre où on les construit, chacune s'appuyant sur les précédentes, et le ticket 14 vérifie que tout tient avant la rentrée ; (b) marquer quand même les stories qui pourraient attendre en cas de retard. ➡️ (a).
- **Q13. Fin d'année et rentrée suivante.** (a) À la date de fin de l'année scolaire, les groupes d'Étude se terminent seuls : ils sortent de la liste et gardent leur historique, comme une Révision ou une Formation après sa dernière séance. À la rentrée, « Reconduire » recopie un groupe de l'an passé (professeur, matière, horaire, prix) sans ses élèves ; les niveaux changent en masse ; l'assurance redevient due. (b) Les groupes continuent d'une année sur l'autre avec leurs élèves. ➡️ (a), avec « groupe terminé » (arrivé au bout) distinct de « groupe fermé » (arrêté avant).
- **Q14. Professeur qui quitte le centre.** (a) Quand il n'a plus de groupe ouvert, on le marque « parti » : il sort de la liste des professeurs, et ses parts et son historique restent consultables ; (b) il reste dans la liste. ➡️ (a).
- **Q15. Liste des stories.** Manque-t-il une story, ou faut-il en retirer une ? ➡️ La liste ci-dessous.

### Liste des stories proposée (tour 3)

- **00. Exigences communes** : pas des stories.
- **01. Comptes et accès (CPT)** : CPT-01 se connecter et se déconnecter ; CPT-02 choisir son mot de passe depuis le lien d'invitation ; CPT-03 mot de passe oublié ; CPT-04 inviter une secrétaire ou un autre propriétaire ; CPT-05 désactiver un compte ; CPT-06 cacher à la secrétaire ce qui est réservé au propriétaire.
- **02. Centre (CEN)** : CEN-01 créer un centre et son premier propriétaire par une commande ; CEN-02 régler les paramètres du centre ; CEN-03 compléter la liste des niveaux ; CEN-04 passer à l'année scolaire suivante (selon Q13).
- **03. Professeurs et groupes (GRP)** : GRP-01 ajouter ou modifier un professeur ; GRP-02 voir les groupes d'un professeur, en cadres, filtrés par jour ; GRP-03 marquer un professeur « parti » (selon Q14) ; GRP-04 créer un groupe d'Étude ; GRP-05 créer un groupe de Révision ; GRP-06 créer un groupe de Formation avec ses tranches ; GRP-07 créer un groupe déjà en cours ; GRP-08 fermer un groupe ; GRP-09 reconduire un groupe de l'an passé (selon Q13).
- **04. Élèves et inscriptions (ELV)** : ELV-01 chercher un élève depuis n'importe quel écran ; ELV-02 créer ou modifier la fiche d'un élève ; ELV-03 inscrire un élève dans un groupe, avec son mode de tarification ; ELV-04 voir la fiche d'un élève ; ELV-05 accorder un prix spécial ; ELV-06 dispenser de l'assurance ; ELV-07 noter « assurance déjà payée » ; ELV-08 arrêter une inscription ; ELV-09 changer un élève de groupe ; ELV-10 passer une Formation au paiement total ; ELV-11 supprimer une fiche sans historique.
- **05. Demandes d'inscription (DEM)** : DEM-01 remplir le formulaire public ; DEM-02 afficher et imprimer le lien public et son QR code ; DEM-03 voir les demandes, filtrées par niveau et par matière ; DEM-04 enregistrer une demande comme élève inscrit ; DEM-05 refuser une demande.
- **06. Séances et appel (SEA)** : SEA-01 accueil « Aujourd'hui » ; SEA-02 voir les séances d'un groupe ; SEA-03 annuler une séance ; SEA-04 déplacer une séance ; SEA-05 ajouter une séance supplémentaire ; SEA-06 changer l'horaire d'un groupe ; SEA-07 faire l'appel ; SEA-08 imprimer une feuille d'appel ; SEA-09 recopier une feuille d'appel ; SEA-10 corriger une présence ; SEA-11 prévenir les parents des absents ; SEA-12 inscrire un élève en rattrapage.
- **07. Caisse et reçus (CAI)** : CAI-01 voir ce qu'un élève doit, et sa couleur ; CAI-02 badge « groupe payé » et élèves à relancer ; CAI-03 encaisser un élève ; CAI-04 encaissement groupé ; CAI-05 remettre le reçu ; CAI-06 retrouver un reçu ; CAI-07 annuler un reçu ; CAI-08 relancer un élève ; CAI-09 clôturer sa caisse ; CAI-10 contrôler les clôtures.
- **08. Bibliothèque (BIB)** : BIB-01 enregistrer une entrée à la journée ; BIB-02 liste du jour et départs ; BIB-03 liste d'un jour passé.
- **09. Parts des professeurs (PRT)** : PRT-01 voir les parts à payer ; PRT-02 marquer une part payée ; PRT-03 enregistrer une avance ; PRT-04 changer la part du centre pour un professeur.
- **10. RH et achats (RHA)** : RHA-01 fiche d'un employé ; RHA-02 jours de congé ; RHA-03 salaire payé ; RHA-04 saisir un achat, payé ou non depuis sa caisse ; RHA-05 refaire un achat ; RHA-06 totaux des achats.
- **11. Tableau de bord et journal (TDB)** : TDB-01 tableau de bord d'un mois ou d'une année ; TDB-02 détail par groupe et par professeur ; TDB-03 liste des prix spéciaux et des dispenses ; TDB-04 consulter le journal.
- **12. Listes (LST)** : LST-01 imprimer une liste ; LST-02 exporter en Excel ; LST-03 importer des élèves ; LST-04 importer des professeurs ; LST-05 agir sur plusieurs élèves sélectionnés.

### Questions du tour 2 (répondues le 01/11/2026)

- **Q3. Ordre de livraison.** Tout le cahier des charges est dans le MVP, mais il ne peut pas être livré le même jour. Pour que le centre pilote commence avant la fin du développement, chaque story reçoit une étiquette : P1, le centre ne peut pas démarrer sans elle ; P2, il en a besoin pendant ses 4 premières semaines ; P3, le reste. ➡️ Oui ; je propose l'étiquette de chaque story au tour suivant.
- **Q6. Ce qui s'est passé avant le premier jour.** Le centre pilote commencera en cours d'année : ses groupes auront déjà eu des séances et ses élèves auront déjà payé dans le carnet. (a) On ne recopie pas le passé : on indique « ce groupe en est au Mois 5 », et on coche « assurance déjà payée » ; (b) on recopie séances, présences et paiements depuis septembre ; (c) on attend septembre 2027. ➡️ (a).
- **Q10. Règles valables partout.** (a) Les règles qui valent pour toute l'application (téléphone et ordinateur, séparation des centres, TND, français…) sont écrites une seule fois, dans `00-exigences-communes.md`, et les travaux techniques (serveur Azure, sauvegardes, pipeline) sont listés à part pour le ticket 14 ; (b) on les répète dans chaque story. ➡️ (a).
- **Q11. Le millime en trop.** Avec les millimes, 3 séances à 16,667 TND font 50,001 TND au lieu de 50. (a) La dernière séance prend la différence : 16,667 + 16,667 + 16,666 = 50,000 ; (b) on laisse 50,001. ➡️ (a).

### Questions du tour 1 (répondues le 01/11/2026)

La recommandation est donnée après ➡️.

- **Q1. Format et rangement.** (a) Une phrase « En tant que…, je veux…, afin de… », des critères d'acceptation en liste avec un exemple chiffré, et l'écran de la maquette concerné ; (b) des critères au format « Étant donné / Quand / Alors ». ➡️ (a), avec un fichier par épopée dans `docs/user-stories/`, un sommaire qui donne l'ordre de priorité, et un numéro par épopée (par exemple CAI-03) pour ne jamais renuméroter.
- **Q2. Découpage en épopées.** Comptes et accès ; centre ; professeurs et groupes ; élèves et inscriptions ; demandes d'inscription ; séances et appel ; caisse et reçus ; bibliothèque ; parts des professeurs ; RH et achats ; tableau de bord et journal ; listes (impression, export, import). ➡️ Cette liste. Les messages WhatsApp et les emails vont dans l'épopée de l'action qui les déclenche.
- **Q3. Échelle de priorité.** Le MVP couvre tout : la priorité dit donc dans quel ordre le centre pilote en a besoin. (a) P1, indispensable le premier jour du pilote ; P2, nécessaire avant la fin de son premier Mois ; P3, le reste ; (b) MoSCoW ; (c) un classement complet. ➡️ (a). Le classement de chaque story vient au tour 2 ; le [ticket 14](14-estimation-plan-livraison.md) en fera les lots.
- **Q4. Création d'un centre.** Sans inscription en libre-service (cadrage, Q3) : (a) une commande lancée par Taha ou Lassad, avec le nom du centre et l'email du propriétaire, qui reçoit son lien d'invitation ; (b) un écran d'administration de la plateforme ; (c) à la main dans la base. ➡️ (a).
- **Q5. Paramètres du centre.** Nom, téléphone et email du centre ; dates de l'année scolaire ; montant de l'assurance ; prix et capacité de la bibliothèque ; part du centre par défaut ; niveaux ajoutés ; lien public et QR code. ➡️ Cette liste, modifiable par le propriétaire seul ; un nouveau prix ne change pas ce qui est déjà payé ; pas de logo dans le MVP.
- **Q6. Démarrage en cours d'année.** Un groupe en est déjà au Mois 4 et Ali a payé les Mois 1 à 3 et l'assurance dans le carnet. (a) La secrétaire crée le groupe « en cours, Mois 4, à partir du 12/01/2027 » et coche « assurance déjà payée » pour chaque élève ; rien d'autre n'est recopié ; (b) elle recopie séances et paiements depuis la rentrée ; (c) le pilote attend septembre 2027. ➡️ (a), pour le pilote comme pour tout centre qui arrive en cours d'année.
- **Q7. Listes.** Imprimer et exporter en Excel les élèves, professeurs, groupes, reçus, entrées de la bibliothèque, achats et employés ; importer les élèves (avec leur groupe, en option) et les professeurs depuis un fichier modèle ; sélection multiple pour inscrire dans un groupe, changer de niveau à la rentrée, imprimer ou exporter. Pas de suppression en masse ; une fiche sans aucun historique peut être supprimée, sinon elle reste. ➡️ Cette proposition.
- **Q8. Relances des impayés.** (a) Un bouton « Relancer » sur chaque élève orange ou rouge, qui prépare un message WhatsApp au parent et garde la date de la dernière relance ; (b) une file d'envoi du jour pour les reçus, les absences et les relances ([ticket 09](09-canaux-notification.md)) ; (c) pas de relance préparée. ➡️ (a).
- **Q9. Arrondis.** Une Révision à 50 TND en 3 séances donne 16,667 TND la séance ; 3 séances d'un Mois à 50 TND donnent 37,500 TND. (a) Arrondir au 0,500 TND ; (b) au dinar entier le plus proche ; (c) garder le millime. ➡️ (b) pour tout ce que la plateforme calcule (prix d'une séance, prorata, part du centre) ; les prix saisis par le centre restent tels quels.
- **Q10. Exigences communes.** (a) Une page « Exigences communes », valable pour toutes les stories (téléphone et ordinateur, séparation des centres, journal, TND, français, rapidité au guichet), et les tâches techniques listées à part pour le ticket 14 ; (b) ces règles répétées dans chaque story. ➡️ (a).

## Réponse

Résolu le 01/11/2026, par grilling avec Lassad et Taha. Le détail des échanges est dans « Avancement » ci-dessus ; le vocabulaire est dans [CONTEXT.md](../../../../CONTEXT.md).

### Les stories

- **78 user stories** dans [docs/user-stories/](../../../user-stories/README.md), en **12 épopées** : comptes et accès, centre, professeurs et groupes, élèves et inscriptions, demandes d'inscription, séances et appel, caisse et reçus, bibliothèque, parts des professeurs, RH et achats, tableau de bord et journal, listes.
- **Format** : « En tant que…, je veux…, afin de… », des critères d'acceptation avec un exemple chiffré, et l'écran de la maquette. Le numéro reprend le code de l'épopée (CAI-03) et ne change jamais.
- **Une page d'[exigences communes](../../../user-stories/00-exigences-communes.md)** pour les règles valables partout, et une liste de [travaux techniques](../../../user-stories/travaux-techniques.md) à part, pour l'estimation.

### Livraison

- **Une seule livraison, complète.** *Échéance corrigée le 01/11/2026 ([ticket 14](14-estimation-plan-livraison.md), Q2) : le plus tôt possible, et avant juin 2027, au lieu de la rentrée 2027-2028.* Pas d'étiquette de priorité : le sommaire range les épopées dans l'ordre où on les construit.

### Règles ajoutées par ce ticket

- **Un centre se crée par une commande** de Taha ou Lassad ; le propriétaire règle ensuite ses paramètres (nom, contacts, année scolaire, assurance, bibliothèque, part du centre, niveaux).
- **Démarrage en cours d'année** : on ne recopie pas le passé. Un groupe se crée « en cours » (par exemple au Mois 5), et ce qui a été payé dans le carnet se note « payé avant SchoolHub », sans reçu.
- **Fin d'année** : les groupes d'Étude deviennent des **groupes terminés** ; à la rentrée, les nouveaux groupes se créent comme au premier jour.
- **Un professeur ou un employé qui quitte le centre est marqué « parti »**, avec son historique gardé.
- **Listes** : impression et export Excel ; import des élèves et des professeurs depuis un fichier modèle, refusé tant qu'il a une erreur ; sélection multiple pour inscrire, changer de niveau, imprimer ou exporter ; pas de suppression en masse.
- **Relance** : un bouton prépare le message WhatsApp d'un élève orange ou rouge, et garde la date de la dernière relance.
- **Montants au millime**, sans arrondi ; la dernière part prend la différence (16,667 + 16,667 + 16,666 = 50,000 TND).
- **Ce qui change en cours d'année** (prix d'un groupe, assurance, part du centre) : ce qui a commencé, ou est déjà dû, garde l'ancienne valeur. Exception : le prix spécial vaut pour tout ce qui n'est pas encore payé.
- **Destinataires des messages** : un élève majeur reçoit seul ses messages ; un mineur les reçoit avec son parent ; un mineur sans téléphone, par son parent seul.
- **Caisse** : le propriétaire qui encaisse a sa caisse ; un encaissement après la clôture ouvre une deuxième caisse ; une caisse oubliée se clôture le lendemain.
- **Vitesse** : un écran en moins de 2 secondes en 4G ; 20 paiements et leurs reçus enregistrés en moins de 5 secondes.

### Renvoyé à d'autres tickets

- **[Estimation et plan de livraison](14-estimation-plan-livraison.md)** : estimer les 78 stories et les travaux techniques, vérifier que tout tient avant la rentrée 2027, et décider quoi faire sinon.

### Écarté

- Étiquettes de priorité et livraison en plusieurs fois.
- Bouton « Reconduire » un groupe de l'an passé.
- File d'envoi du jour ([ticket 09](09-canaux-notification.md)).
- Arrondi des montants au dinar.
- Écran d'administration de la plateforme, et logo du centre sur le reçu.
