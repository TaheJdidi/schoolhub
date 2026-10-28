# Étude de l'existant : SaaS de gestion pour centres d'étude et de cours de soutien

> **Date** : 28 octobre 2026
> **Marché cible** : **Tunisie**. Un centre pilote est disponible. Le Maroc, l'Algérie, la France et les acteurs internationaux servent de contexte secondaire.
> **Méthode** : sources primaires d'abord (sites éditeurs, pages tarifs, centres d'aide officiels, textes de loi, presse nationale pour les données de marché). Chaque affirmation est suivie de son URL.
> **Niveau de confiance** : fonctionnalités et prix viennent presque tous **des pages des éditeurs**. Ce sont des déclarations commerciales : je ne les ai ni testées, ni recoupées avec des avis indépendants. Capterra et G2 ont renvoyé HTTP 403. Les mentions **[NON VÉRIFIÉ]** signalent ce que je n'ai pas pu confirmer à la source.
> **Conversions** : ordres de grandeur, non vérifiés à la date du jour (environ 1 € ≈ 3,4 TND ; 1 $ ≈ 3,0 TND ; 1 MAD ≈ 0,31 TND ; 1 000 DZD ≈ 23 TND).

---

## 1. Résumé exécutif

### Verdict sur la saturation

| Marché | Verdict | Justification en une ligne |
|---|---|---|
| **Tunisie (cible)** | **Pas saturé. Concurrence directe faible mais réelle, statu quo dominant.** | Je n'ai trouvé **aucun SaaS tunisien dédié aux centres d'étude ou de soutien en groupe** qui affiche un prix public et reprenne le modèle Étude, Révision, Formation et salle d'étude. Les acteurs locaux sont des **ERP pour écoles privées** (Educanet, Acurschool, TSI-ECOLE, Olvo School, Minyar) et des outils pour **centres de formation** (EduGestion, Classimum, ce dernier non vérifié). Le vrai concurrent est **Excel, le cahier, le carnet à souche et WhatsApp**. |
| Maroc (contexte) | **Concurrentiel.** | Au moins 8 à 10 SaaS dédiés aux centres de soutien, à 127-499 MAD/mois (≈ 40-155 TND), avec WhatsApp, paie des profs et paiement à la séance. C'est **la preuve que ce modèle se vend au Maghreb**, et une **menace d'entrée** en Tunisie. |
| Algérie (contexte) | **Émergent.** | Quelques acteurs : Dzair Edu, AlgeriaSchool (4 000 DA/mois ≈ 90 TND), Algeriasm. |
| International et France | **Saturé, mais hors sujet.** | Des dizaines d'outils matures (Teachworks, TutorBird, ClassCard, Jackrabbit…), en USD ou EUR, pensés pour la carte bancaire, sans arabe ni modèle maghrébin. |

**En une phrase** : l'idée n'est **pas consommée en Tunisie**, mais elle n'est **pas originale** au Maghreb. La différenciation doit venir de l'**adéquation au fonctionnement réel d'un centre tunisien** :
- présence et paiement à la séance, découplés du groupe ;
- escalade des impayés ;
- caisse en espèces et reçus en masse ;
- WhatsApp ;
- paie des enseignants au pourcentage ;
- salle d'étude.

Le simple CRUD élèves, groupes et paiements ne suffit pas.

### Les 3 plus grands trous en Tunisie
1. **Aucun outil tunisien ne gère le modèle « séance »**. La présence et le paiement doivent être attachés à l'élève et à la séance, pas au groupe (rattrapage dans un autre groupe). Il faut aussi l'escalade de l'impayé au fil des séances (orange, puis rouge) et le badge « groupe à jour ». À l'international, seuls Jackrabbit et ClassCard font des rattrapages propres.
2. **Pas d'outil « tout le centre »**. Il faudrait réunir : Étude annuelle, Révision et Formation à la séance ou au mois, **salle d'étude (abonnement à la journée ou à durée libre)**, **dépenses** (matériel, services, assurance) et **RH (congés, horaires)**. Personne ne couvre la salle d'étude, nulle part.
3. **Pas d'outil « cash-first + WhatsApp + paie au pourcentage » en TND**. Ce trio est la norme chez les concurrents marocains, mais je ne l'ai vu chez aucun acteur tunisien.

---

## 2. Rappel de l'idée analysée

Il s'agit d'un SaaS web multi-tenant pour centres d'étude.

- **Rôles** : le propriétaire est super-admin (y compris RH). La secrétaire est admin (tout sauf RH).
- **Espaces** : trois espaces sur le même modèle enseignants → groupes → élèves. L'Étude est annuelle. La Révision et la Formation ont une période variable, avec paiement à la séance ou au mois.
- **Présence par séance** : rattrapage possible dans un autre groupe. La présence et le paiement ne sont donc **pas liés au groupe**.
- **Code couleur des impayés** : orange à la 2ᵉ séance, rouge à la 3ᵉ. Badge « groupe payé ». Reçus envoyés par e-mail, en masse (20 d'un coup).
- **Autres espaces** : bibliothèque ou salle de révision (liste du jour, abonnements), RH (personnel, congés, horaires), achats (matériel et services, assurance, totaux).
- **Transverse** : CRUD en masse, impression, export et import de toutes les listes, responsive.

---

## 3. Contexte tunisien

### 3.1 Taille du marché et prix pratiqués
- **Dépense nationale en cours particuliers** : environ **1,468 milliard de TND par an** (FTDES, 2023). Selon la même source, 67 % des élèves du primaire, 61 % des collégiens et 80 % des lycéens y ont recours. Le coût moyen est de **94 TND/mois** pour un élève du primaire, **74 TND** pour un collégien et **120 TND** pour un lycéen ([webdo.tn](https://www.webdo.tn/fr/actualite/national/tunisie-le-cout-annuel-des-cours-particuliers-s-eleve-a-1-468-milliard-selon-le-ftdes/208720/)). L'Association des parents d'élèves estime ce marché à 1,5 à 3 milliards TND par an. C'est une déclaration, pas une enquête ([allafrica](https://fr.allafrica.com/stories/202605060281.html)).
- **Tarifs de groupe** : **15 à 25 TND la séance de 2 h** en primaire, **20 à 30 TND** au collège ([allafrica, oct. 2024](https://fr.allafrica.com/stories/202410010319.html)). Pendant les révisions du bac, une famille peut dépenser **1 000 à 2 000 TND** selon l'OTIC ([lapresse.tn, mai 2025](https://www.lapresse.tn/2025/05/10/jusqua-2-000-dinars-pour-reussir-le-bac-les-cours-particuliers-hors-de-controle-en-tunisie/)).
- **Enseignement privé** : **1 252 établissements privés** (primaire et secondaire, statistiques 2023), environ 10 % des élèves. Les frais démarrent vers **50 TND/mois** dans certaines régions de l'intérieur ([businessnews.com.tn, sept. 2026](https://businessnews.com.tn/2026/09/15/enseignement-prive-de-cinquante-dinars-par-mois-a-60-000-dinars-par-an-le-grand-ecart/1418480/)).
- **Nombre de centres d'étude ou de soutien en Tunisie** : **aucune statistique trouvée. [NON VÉRIFIÉ]** C'est à demander au pilote et aux directions régionales.
- **Ordre de grandeur pour un centre** : *estimation de ma part*. Un centre de 150 élèves, avec 1,5 matière par élève à environ 70 TND/mois, encaisse **environ 15 000 TND/mois**. Un SaaS à 50-150 TND/mois représente **0,3 à 1 % du chiffre d'affaires**. Le frein est la **valeur perçue** et l'**informalité**, pas le montant.

### 3.2 Cadre réglementaire de l'activité (point critique)
- **Décret n° 2015-1619 du 30 octobre 2015** : il organise les cours de soutien et les cours particuliers dans les établissements publics, et **interdit aux enseignants du public d'en donner hors des établissements publics**. Les sanctions vont jusqu'à la révocation. Le ministère l'a **rappelé le 12 novembre 2024** ([tuniscope.com](https://www.tuniscope.com/article/394587/etude/vie/le-ministere-de-l-education-met-en-garde-434813) ; [africanmanager.com](https://africanmanager.com/les-cours-particuliers-interdits-en-dehors-des-etablissements-scolaires/)). Je n'ai pas lu le texte au JORT. **[NON VÉRIFIÉ à la source primaire]**
- **Centres de soutien privés** : selon des propos rapportés (une enseignante sur Express FM, repris par Tuniscope le 13 octobre 2026), ils doivent détenir une **licence de la direction régionale compétente**. Leurs enseignants doivent être **qualifiés**, et l'activité est **soumise au contrôle** administratif ([tuniscope.com](https://www.tuniscope.com/article/442289/actu-arabe/arabe/231-53461621)). Ce n'est pas un texte officiel : je n'ai pas identifié l'arrêté ou le cahier des charges applicable. **[NON VÉRIFIÉ]**
- **Proposition de loi sur l'enseignement privé** : elle était en discussion au Parlement en février 2025 ([allafrica](https://fr.allafrica.com/stories/202502200515.html), lu via résumé). Son adoption n'est pas vérifiée. **[NON VÉRIFIÉ]**
- **Concurrence publique gratuite** : le ministère a lancé en mars 2026 la plateforme *edusoutien.education.tn* ([managers.tn](https://managers.tn/2026/03/30/une-premiere-nationale-la-tunisie-lance-sa-plateforme-officielle-de-soutien-scolaire-gratuite-du-primaire-jusquau-baccalaureat/)). C'est du contenu pédagogique, pas de la gestion de centre. Ce n'est pas un concurrent du SaaS, mais c'est un signal politique.
- **Conséquences produit** :
  - Beaucoup de centres emploient des enseignants du public. Un outil cloud qui **nomme les enseignants et calcule leurs revenus** peut faire fuir. Il faut prévoir des **alias ou noms d'affichage d'enseignant**, ne pas rendre obligatoires les champs identifiants (CIN, matricule), et donner des garanties de confidentialité claires.
  - **Inversement**, un centre en règle (licence, enseignants qualifiés) peut vouloir **prouver sa conformité** : registres de présence, listes d'enseignants, reçus numérotés. C'est un argument de vente pour les centres structurés.

### 3.3 Facturation électronique (impacte le fondateur et certains centres)
- La **loi de finances 2026 (art. 53)** étend l'obligation de **facture électronique via TTN** aux **prestations de services**, à partir du 1ᵉʳ janvier 2026. Sanction : 100 à 500 TND par facture papier, plafond de 50 000 TND ([tunisienumerique.com](https://www.tunisienumerique.com/tunisie-extension-de-la-facturation-electronique-aux-prestations-de-services/) ; [circulaire KPMG](https://assets.kpmg.com/content/dam/kpmgsites/tn/pdf/circulaires-fiscales/Circulaire%20fiscale%20f%C3%A9vrier%202026%20final.pdf.coredownload.inline.pdf) ; [efacturetn.com](https://efacturetn.com/fr/facture-electronique-ecoles-privees)).
- **Qui est exclu** : les forfaitaires, les auto-entrepreneurs et les activités non assujetties à la TVA ne sont pas concernés pour l'instant ([tuniscope, déc. 2025](https://www.tuniscope.com/article/421017/business/economie/facturation-electronique-en-tunisie-qui-est-concerne-et-que-change-la-loi-de-finances-2026-543610)). Les sources divergent sur le périmètre exact.
- **Pour le fondateur** : la société qui vend le SaaS est un prestataire de services, probablement assujetti à la TVA. Elle devra **facturer ses abonnements en e-facture TTN**. À vérifier avec un expert-comptable.
- **Pour les centres** : les **reçus** de l'outil ne doivent **pas se présenter comme des factures**. Un centre assujetti à la TVA qui facture (par exemple l'Espace Formation pour des entreprises) peut devoir passer par TTN. Le régime TVA des activités d'enseignement et de soutien en Tunisie **n'a pas été vérifié. [NON VÉRIFIÉ]**

### 3.4 Paiement en Tunisie
- **Les familles paient surtout en espèces** au guichet. C'est un constat commun aux concurrents maghrébins (espèces, chèque, virement chez MyCentreKey et SoutienPro). Je n'ai pas de statistique tunisienne. **[NON VÉRIFIÉ]**
- **Paiement en ligne local** : **Konnect** est le premier facilitateur de paiement agréé par la BCT ([managers.tn](https://managers.tn/2023/12/07/konnect-lagrement-de-la-banque-centrale-en-poche)), avec un [SDK officiel](https://packagist.org/packages/konnect/sdk). **Flouci** est aussi facilitateur agréé, avec plugins Shopify et WooCommerce ([managers.tn, juin 2026](https://managers.tn/2026/06/08/une-premiere-en-tunisie-flouci-permet-lintegration-des-paiements-sur-shopify/)). **D17** (wallet de La Poste) est déjà accepté par Tadris.tn ([tadris.tn](https://www.tadris.tn/soutien-scolaire-en-ligne.html)). Ces solutions sont utiles **pour encaisser l'abonnement SaaS** auprès des centres, moins pour les familles au départ.

---

## 4. Concurrents en Tunisie (priorité)

### 4.1 Solutions tunisiennes identifiées

| Solution | Type | Cible | Prix public | Ce qui recoupe la spec | Ce qui manque par rapport à la spec |
|---|---|---|---|---|---|
| **EduGestion** ([edu-gestion.com](https://edu-gestion.com/)) | SaaS web, « Made in Tunisia » (+216, références à Tunis, Sousse, Sfax), FR seulement | Centres de formation et centres éducatifs | **Aucun prix**, « Commencer gratuitement » | Classes, **présences avec séances générées automatiquement**, taux et tendances, **alertes élèves à risque**, relevés de paiement par élève, **reçus**, relances, messagerie par classe, rôles, portails admin, prof et élève | Rien sur : paiement à la séance ou au mois, rattrapage, code couleur, WhatsApp ou SMS, paie des profs, dépenses, RH, salle d'étude, arabe |
| **Classimum** (classimum.com) | SaaS « conçu en Tunisie » | Centres de formation | **« À partir de 159 DT HT/mois », essai 7 jours**, d'après un extrait de moteur de recherche | Inscriptions, échéanciers, planning, présences, **pointage**, **paie des formateurs**, certificats avec QR code, encaissement en dinars, reçu conforme TVA et timbre (extrait) | **[NON VÉRIFIÉ]** : domaine injoignable lors de mes visites (ENOTFOUND). Orienté formation professionnelle plutôt que soutien scolaire. **C'est le concurrent tunisien le plus proche à surveiller.** |
| **Educanet** ([educanet.pro](https://educanet.pro/)) | SaaS + apps iOS et Android | **Écoles privées** | Sur demande de démo | Frais en ligne, factures, comptabilité, **RH (présence, paie, congés)**, messagerie | Modèle « école » (notes, bulletins, discipline). Pas de groupes à la séance. |
| **Acurschool** ([acurschool.com](https://acurschool.com/)) | Web, mobile, bureau | Écoles et établissements | Sur devis | **Pointage enseignants et élèves**, absences, **alertes e-mail et SMS aux parents pour retard de paiement ou absence**, RH (extrait de recherche) | Site injoignable lors de ma visite. **[PARTIELLEMENT VÉRIFIÉ]** |
| **TSI-ECOLE** ([tsi.com.tn](https://www.tsi.com.tn/erp-ecole-tsi/)) | Web (.NET et Angular) | Écoles, collèges, universités | Sur devis | Paiements **par prestation et période**, « reste à payer », **reçus**, **dépenses**, droits par groupe d'utilisateurs, historique, export xls et pdf, certificats AR/FR | Paie dans un produit séparé (TSI-PAIE). Pas de séance ni de groupe de soutien. ERP lourd. |
| **Olvo School** ([olvotechnology.com](https://olvotechnology.com/logiciel-de-gestion-scolaire-en-tunisie/)) | Logiciel + app parents | Écoles privées et centres de formation | Démo, aucun prix | Frais par niveau, **reçus et factures, suivi des retards et relances automatiques**, app parents (absences, notes), **pointage RFID ou biométrique** (OLVO Access) | Modèle « école » (moyennes, trimestres, bulletins). |
| **Minyar, ABS Computer (Sfax)** ([abscomputer.co](https://abscomputer.co/nos-logiciels/gestion-scolarite-minyar/)) | Logiciel (bureau ou web, non précisé) | Instituts privés | Devis | Inscription, affectation, **impression de reçus**, suivi quotidien des paiements, situation financière par élève | Pas de présence décrite, pas de séance. |
| **JeridSchool** ([jeridschool.com](https://www.jeridschool.com/)) | SaaS | Établissements et centres de formation | — | Groupes, formateurs, salles, paiements (extrait de recherche) | Seul le titre de la page est lisible. **[NON VÉRIFIÉ]** |
| **EduSolution, Bees Solution** ([bees-solution.com](https://bees-solution.com/logiciel-gestion-ecole-tunisie)) | Logiciel en ligne | Écoles primaires, collèges, lycées | Pas de prix (extrait) | — | Site injoignable. **[NON VÉRIFIÉ]** |
| **EdTrust** ([dealroom](https://app.dealroom.co/companies/edtrust)) | Logiciel de gestion scolaire | Écoles | — | — | Fiche Dealroom seulement. **[NON VÉRIFIÉ]** |

**Acteurs adjacents qui ne sont pas des concurrents directs** :
- **Tadris.tn** ([tadris.tn](https://www.tadris.tn/soutien-scolaire-en-ligne.html)) : cours en ligne à la séance ou au mois. Il concurrence les centres, pas le SaaS.
- **edusoutien.education.tn** : plateforme publique gratuite de contenu.
- **eFactureTN** ([efacturetn.com](https://efacturetn.com/fr/facture-electronique-ecoles-privees)) : e-facture TTN pour écoles privées. C'est un partenaire ou une intégration possible.
- **Permis.tn** ([permis.tn](https://www.permis.tn/)) : logiciel pour auto-écoles avec séances, paiement par tranches et solde visible en ligne. C'est un **modèle analogue** qui prouve qu'un SaaS vertical « à la séance » se vend en TND.

**Lecture** :
- **L'offre tunisienne existe pour les écoles et la formation professionnelle, pas pour le centre de soutien en groupe.**
- Le seul acteur qui recoupe une partie sensible de la spec (pointage, échéanciers, paie des formateurs, TND) est **Classimum**, que je n'ai pas pu vérifier. Si son prix de 159 DT HT/mois est exact, il **fixe une borne haute** de prix sur le marché.
- **Limite importante de ma recherche** : en Tunisie, une partie de l'offre se vend **par Facebook, Instagram et démarchage**, sans site indexé. Je n'ai pas pu explorer ce canal. **Il faut demander au centre pilote ce qu'il a déjà vu ou essayé.**

### 4.2 Le statu quo tunisien : le vrai concurrent

| Outil actuel | Ce qu'il fait bien | Ce qui coince (où le SaaS gagne) |
|---|---|---|
| **Cahier d'appel papier** par groupe | Gratuit, immédiat, aucune panne | Rattrapage dans un autre groupe illisible. Impossible de savoir qui a assisté à combien de séances depuis son dernier paiement. Aucun historique consolidé. |
| **Carnet de reçus à souche** | Le parent repart avec un papier, culture du « wasl » | Pas de total automatique. Fraude ou oubli de caisse indétectable. Pas de relance. |
| **Excel ou Google Sheets** (un onglet par groupe) | La secrétaire maîtrise, souple | Formules cassées, doublons d'élèves, pas de droits d'accès (la secrétaire voit tout), pas de reçu, mauvais sur mobile |
| **Groupes WhatsApp ou Facebook** (par groupe, ou parents-centre) | Canal déjà adopté par 100 % des parents | Pas de lien avec les données. Relances manuelles une par une. Mélange vie privée et pro. |
| **Logiciel de bureau sur mesure** (freelance local, Access, projet de fin d'études) | Payé une fois, « c'est à moi » | Pas de mise à jour. Données sur un seul PC (perte, vol). Pas d'accès à distance pour le propriétaire. |

Ce statu quo est **implicite** dans l'argumentaire des concurrents marocains et algériens, qui visent tous « Excel et papier » et proposent l'**import Excel** (Centre Soutien, MyCentreKey, Dzair Edu, OmniDaam). Je n'ai trouvé **aucune donnée chiffrée** sur l'équipement des centres tunisiens. **[NON VÉRIFIÉ]** C'est la première chose à mesurer avec le pilote.

---

## 5. Contexte secondaire : Maroc, Algérie, France, international

### 5.1 Maroc : la référence produit et prix (et une menace d'entrée)

| Solution | Type | Prix public (≈ TND) | Fonctionnalités clés (selon l'éditeur) |
|---|---|---|---|
| **OmniDaam** ([omnidaam.com](https://omnidaam.com/)) | SaaS web, FR/AR | **159, 249 ou 499 MAD/mois** (≈ 49, 77, 155 TND). Offre de rentrée 127, 199 ou 399 MAD. Annuel de 106 à 333 MAD/mois. Essai 14 j. | **Échéances mensuelles automatiques, paiement à la séance, forfaits, remises, prorata**. Appel sur téléphone. **Relance des impayés en un clic**. **Reçus PDF numérotés**. **WhatsApp depuis le numéro du centre (QR), sans coût par message**. **Paie des profs en 5 modes** (heure, séance, élève, %, forfait) avec avances. Multi-centres. Import Excel. |
| **SoutienGo** ([soutiengo.com](https://soutiengo.com/)) | SaaS web | 159, 249 ou 499 MAD/mois d'après un extrait de recherche (site en erreur 502) | Mêmes prix qu'OmniDaam : probablement le même éditeur ou une marque blanche. **[NON VÉRIFIÉ]** |
| **Moujtahid** ([moujtahide.ma](https://moujtahide.ma/)) | SaaS + app parents iOS et Android | 189 et 289 MAD/mois (≈ 59 et 90 TND). La FAQ annonce le plan Débutant « gratuit à vie », ce qui contredit la grille. | **Planning avec détection des conflits de salle et de prof**. Appel en un clic **avec SMS automatique aux parents**. Liens de paiement. **Score de risque de décrochage**. Multi-sites. |
| **MyCentreKey** ([mycentrekey.com](https://mycentrekey.com/en)) | SaaS web | 199 et 399 MAD/mois (≈ 62 et 124 TND). 1ᵉʳ mois gratuit. | **Liste d'attente par niveau et matière, alerte à 7 demandes**. **Liste quotidienne des parents en retard avec message WhatsApp pré-rédigé**. **Délai de grâce paramétrable**. Espèces, chèque, virement. **Dépenses**. Paie mensuelle des profs. |
| **Skoolly** ([skoolly.ma](https://skoolly.ma/)) | SaaS + apps | 4 à 6 MAD par élève et par mois (≈ 1,2 à 1,9 TND) | Plutôt écoles. Relances e-mail et WhatsApp, reçus, FR/AR, IA en darija. |
| **Tayssir Académie** ([academie.tayssir.school](https://academie.tayssir.school/)) | SaaS + apps (plan Pro) | De 500 à 5 000 MAD/an (≈ 13 à 130 TND/mois) | Soutien, langues, formation. **Gains des profs à la séance ou à la commission**. **Dépenses, salaires, avances**. **RH** (retards, paie, remplacements). Salles. |
| **Centre Soutien** ([centresoutien.com](https://centresoutien.com/fr)) | **Bureau, 100 % hors ligne** | Licence à vie (montant non affiché) | Facturation mensuelle, **relances WhatsApp**, paie horaire des profs, conflits de salle et de prof, **FR/AR avec RTL**. Argument « loi 09-08 ». |
| **SoutienPro** ([soutien-pro.dahbimoad.com](https://soutien-pro.dahbimoad.com/)) | Bureau, hors ligne | Licence à vie sur devis | Paie des profs au % ou par élève. Badges PDF. Espèces, chèque, virement. **Rôles Admin et Secrétaire**. Journal d'audit. |
| **EduSchoolConnect** ([eduschoolconnect.com](https://eduschoolconnect.com/use-cases/centres-formation)) | SaaS (Marrakech, présent aussi en Belgique) | Prix sur une page séparée, non consultée | Inscription **à la séance, par pack d'heures ou au mois**. Paie des formateurs selon les heures. |
| **Class Manager** ([classmanager.academy](https://classmanager.academy/organisme-de-formation)) | SaaS | Non affiché | Groupes par niveau, présences, relances, **dépenses et charges**, SMS. |

**À retenir pour la Tunisie** : ces produits sont en **français** et sans dépendance locale forte. Un éditeur marocain peut ajouter le TND et attaquer le marché tunisien rapidement. Leur grille (≈ 40-155 TND/mois) est **l'ancrage de prix le plus pertinent**.

### 5.2 Algérie

| Solution | Prix (≈ TND) | Points clés |
|---|---|---|
| **Dzair Edu** ([dzairedu.com](https://dzairedu.com/)) | Devis | Cible les **écoles de soutien** : groupes de sessions courtes, **liste d'attente**, import Excel ou papier, échéances, reçus avec solde, **WhatsApp pré-rédigé**, 7 profils, journal d'activité, **AR/FR/EN**. Pas de paie ni de RH. |
| **AlgeriaSchool** ([algeriaschool.com](https://algeriaschool.com/)) | **4 000, 8 000 ou 16 000 DA/mois** (≈ 90, 180, 360 TND) | Présences, factures, **commission des profs**, **dépenses**. FR/AR/EN. |
| **Algeriasm** ([algeriasm.net](https://algeriasm.net/gestion-des-formations/)) | Devis | **Module « cours de soutien »** : groupes par matière, **remboursement des absences justifiées**, **soldes par groupe, mois et élève**, **paie des profs au %, à l'heure ou au forfait**. |
| AymnEDU, Innova, annonces Ouedkniss ([ifta-computer](https://www.ouedkniss.com/store/16056/ifta-computer/annonce/25665835)) | Licences de bureau de 30 000 à 39 000 DA | Pages partiellement ou pas du tout accessibles. **[NON VÉRIFIÉ]** |

### 5.3 France et Europe francophone
- **Aucun SaaS français grand public dédié aux centres de soutien en groupe** n'est apparu. Les comparatifs couvrent les écoles privées et les organismes de formation soumis à Qualiopi ([lejournaldelatech.fr](https://lejournaldelatech.fr/saas-logiciels/meilleur-logiciel-gestion-ecole-centre-formation-2026)) :
  - **MyScol** : 504 €/an pour 50 élèves.
  - **École Futée** : 4 à 16 €/élève/an.
  - **Digiforma** : 49 à 839 €/mois.
  - **Dendreo** : 225 à 992 €/mois.
  - **RosarioSIS** et **Gibbon** : gratuits.
- **TutorBird** a une interface en français ([tutorbird.com/fr](https://www.tutorbird.com/fr/)). **Teach 'n Go** supporte le français et l'arabe ([teachngo.com/pricing](https://www.teachngo.com/pricing)).
- Hoplum, Acadomia et Complétude (outils internes) n'ont pas été vérifiés. **[NON VÉRIFIÉ]**
- **Hors cible** à court terme (RGPD, facturation conforme, concurrence).

### 5.4 International

| Solution | Prix (≈ TND) | Intérêt comme référence |
|---|---|---|
| **Teachworks** ([pricing](https://teachworks.com/pricing)) | 15 à 175 $/mois + 0,05 à 0,25 $ par élève-leçon (≈ 45 à 525 TND + variable) | **Paie des profs** de référence : paliers, paiement en masse ([add-on](https://www.teachworks.com/addons/bulk-wage-payments)) |
| **TutorBird** ([pricing](https://www.tutorbird.com/pricing/)) | 16,95 $ + 4,95 $ par tuteur (≈ 50 + 15 TND) | Frais de retard automatiques, SMS inclus, FR |
| **Jackrabbit Class** ([pricing](https://www.jackrabbitclass.com/pricing/)) | À partir de 49 $/mois (≈ 150 TND) | **Rattrapage dans une autre classe** ([aide](https://help.jackrabbitclass.com/help/schedule-makeup-class-record)), **listes d'attente** ([aide](https://help.jackrabbitclass.com/help/waitlist-enrollments)), pointeuse du personnel |
| **ClassCard** ([pricing](https://www.classcardapp.com/pricing)) | 99 à 349 $/mois (≈ 300 à 1 050 TND) | **Crédits de rattrapage** liés au package ([aide](https://help.classcardapp.com/en/article/make-up-classes-9x5cp5/)) |
| **Oases Online** ([pricing](https://www.oasesonline.com/pricing)) | 99 à 699 $/mois | Paie des tuteurs, multi-sites |
| **Teach 'n Go** ([pricing](https://www.teachngo.com/pricing)) | 69 à 539 €/mois | 24 langues dont FR et AR |
| **Classe365** ([pricing](https://www.classe365.com/pricing)) | 100 $ + 75 $ par module | SMS d'absence |
| **Wise** ([pricing](https://www.wise.live/pricing)) | 0,69 par séance ou par siège | Paiement à la séance, **WhatsApp Business**, paiement des tuteurs |
| **OpenEduCat** ([pricing](https://openeducat.org/pricing)) | Gratuit (LGPL), modules à partir de 79 $/an | Base Odoo, cible les centres de langues au Maroc |
| **Daftra** ([page éducation](https://www.daftra.com/%D8%A8%D8%B1%D9%86%D8%A7%D9%85%D8%AC-%D8%A5%D8%AF%D8%A7%D8%B1%D8%A9-%D8%A7%D9%84%D9%85%D8%B1%D8%A7%D9%83%D8%B2-%D8%A7%D9%84%D8%AA%D8%B9%D9%84%D9%8A%D9%85%D9%8A%D8%A9/)) | Pas de prix affiché | Arabe natif, **présence par code-barres**, abonnements mensuels avec rappels, dépenses |
| Teachmint ([teachmint.com](https://www.teachmint.com/)) | — | A pivoté vers le matériel de classe : plus un concurrent |
| Classplus, Fedena | — | Pages illisibles ou en 404. **[NON VÉRIFIÉ]** |

---

## 6. Tableau comparatif des besoins de la spec

Légende : ✅ annoncé · ◐ partiel ou implicite · ❌ non mentionné sur la page consultée (pas forcément absent) · ? non vérifiable

| Besoin de la spec | **EduGestion (TN)** | **Classimum (TN)** ? | **Educanet (TN)** | **TSI-ECOLE (TN)** | **Olvo (TN)** | **Statu quo Excel + papier + WhatsApp** | OmniDaam (MA) | MyCentreKey (MA) | Dzair Edu (DZ) | Jackrabbit (US) |
|---|---|---|---|---|---|---|---|---|---|---|
| Prix d'entrée (≈ TND/mois) | gratuit au départ ? | 159 HT ? | devis | devis | devis | 0 | ≈ 39 à 49 | ≈ 62 | devis | ≈ 150 |
| Interface arabe | ❌ | ? | ❌ | ◐ (certificats AR) | ? | ✅ | ✅ | ◐ (en cours) | ✅ | ❌ |
| Groupes à créneau hebdo fixe | ✅ | ✅ | ◐ | ◐ | ◐ | ✅ (manuel) | ✅ | ✅ | ✅ | ✅ |
| Paiement **au mois et à la séance** | ❌ | ◐ échéanciers | ◐ | ◐ par période | ◐ | ✅ (manuel) | ✅ | ◐ | ◐ | ◐ |
| **Présence découplée du groupe** (rattrapage) | ❌ | ❌ | ❌ | ❌ | ❌ | ◐ (illisible) | ◐ | ❌ | ❌ | ✅ |
| **Impayé escaladé par séance** (orange, rouge) | ◐ relances | ? | ❌ | ◐ reste à payer | ◐ relances | ❌ | ◐ | ◐ délai de grâce | ◐ | ❌ |
| **Badge « groupe payé »** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Reçu instantané, en masse | ✅ reçus | ✅ ? | ? | ✅ reçus | ✅ | ◐ carnet | ✅ PDF numérotés | ◐ | ✅ | ◐ |
| WhatsApp ou SMS aux parents | ❌ | ? | ❌ | ❌ | ◐ app | ✅ (manuel) | ✅ WhatsApp | ✅ WhatsApp | ✅ WhatsApp | ◐ |
| Paie et commission des profs | ❌ | ✅ ? | ✅ | ◐ (TSI-PAIE) | ❌ | ◐ (calcul manuel) | ✅ 5 modes | ✅ | ❌ | ✅ |
| Dépenses du centre | ❌ | ? | ◐ | ✅ | ❌ | ◐ | ❌ | ✅ | ◐ | ❌ |
| RH : congés, horaires | ❌ | ◐ pointage | ✅ | ❌ | ◐ pointage RFID | ❌ | ❌ | ❌ | ❌ | ◐ pointeuse |
| **Salle d'étude à la journée** | ❌ | ❌ | ❌ | ❌ | ❌ | ◐ (cahier) | ❌ | ❌ | ❌ | ❌ |
| Rôles propriétaire et secrétaire | ✅ | ? | ✅ | ✅ | ? | ❌ | ✅ | ? | ✅ | ✅ |
| Import, export, impression | ? | ? | ? | ✅ | ✅ migration | ✅ | ✅ | ◐ | ✅ | ✅ |

---

## 7. Prix : repères en TND et hypothèse de grille

| Repère | Prix mensuel (≈ TND) |
|---|---|
| Statu quo (Excel, papier, WhatsApp) | 0 |
| Skoolly (Maroc), par élève | 1,2 à 1,9 par élève |
| Tayssir Académie (Maroc), formule d'entrée annuelle | ≈ 13 |
| OmniDaam (Maroc) | 39 à 155 |
| Moujtahid (Maroc) | 59 à 90 |
| MyCentreKey (Maroc) | 62 à 124 |
| TutorBird (international) | ≈ 50 + 15 par tuteur |
| AlgeriaSchool (Algérie) | 90 à 360 |
| **Classimum (Tunisie)** | **159 HT ? [NON VÉRIFIÉ]** |
| Jackrabbit, ClassCard (international) | 150 à 1 050 |

**Hypothèse de grille à tester avec le pilote** (*c'est ma déduction à partir des repères, pas une donnée*) :
- **Essentiel** : environ 49-59 TND/mois, jusqu'à 100 élèves. Élèves, groupes, présences, paiements, reçus, liens WhatsApp.
- **Centre** : environ 89-99 TND/mois, jusqu'à 300 élèves. Ajoute paie des profs, dépenses, salle d'étude, RH.
- **Multi-centres** : environ 149-179 TND/mois. Ajoute plusieurs centres, WhatsApp API, support prioritaire.
- **Conditions** : essai 30 jours, **paiement annuel avec 2 mois offerts** (cela neutralise le churn de l'été), offre de rentrée en septembre.
- **Facturation de l'abonnement** : en TND, par e-facture TTN, paiement par virement, Konnect, Flouci, D17 ou espèces via un revendeur. Prévoir la TVA tunisienne. Taux et régime à valider avec un comptable.

---

## 8. Améliorations à ajouter (absentes de la spec), classées pour un centre tunisien

| Rang | Idée | Pourquoi (preuve) | Effort |
|---|---|---|---|
| **1** | **WhatsApp aux parents** : reçu, absence, relance d'impayé, changement d'horaire | C'est le canal réel, et le différenciateur n°1 des concurrents maghrébins (OmniDaam, MyCentreKey, Dzair Edu, Centre Soutien). **Commencer par des liens `wa.me` pré-remplis**, gratuits et sans API. L'API Meta est payante par message. **Les messages « utility » sont facturés depuis le 1ᵉʳ octobre 2026**, et la Tunisie est dans la zone « Rest of Africa » ([Meta](https://developers.facebook.com/docs/whatsapp/pricing)). Les parents n'ont souvent pas d'e-mail : **l'e-mail seul (spec actuelle) ne suffit pas**. | Faible (liens), moyen (API) |
| **2** | **Paie et commission des enseignants** : % du chiffre du groupe, prix par séance, par élève présent, forfait ; avances ; fiche mensuelle | Le partage du chiffre (par exemple 80/20) est la norme du soutien en Tunisie : la presse décrit le partage 80 % / 20 % du système encadré ([babnet](https://www.babnet.net/festivaldetail-74911.asp)). OmniDaam, Algeriasm, SoutienPro, Tayssir et Classimum (?) le font. C'est **la 2ᵉ question du propriétaire** après « qui n'a pas payé ? ». | Moyen |
| **3** | **Caisse « cash-first »** : journal de caisse, clôture par utilisateur, reçus **numérotés et non supprimables** (annulation tracée), impression thermique ou A5, reçu PDF par WhatsApp | Les familles paient surtout en espèces. Le propriétaire doit **contrôler la caisse tenue par la secrétaire**. SoutienPro et Dzair Edu ont un journal d'audit. | Moyen |
| **4** | **Onboarding par import Excel et saisie de masse guidée** | Le concurrent est Excel. Dzair Edu et OmniDaam vendent la reprise de données. **Avec le centre pilote, faire l'import pour lui.** | Faible |
| **5** | **Documents et messages bilingues FR/AR** (interface RTL ensuite) | En Tunisie, l'administration du centre est souvent en français, mais **les parents lisent l'arabe**. Centre Soutien, OmniDaam et Dzair Edu proposent FR/AR. | Moyen (à prévoir dès l'architecture) |
| **6** | **Pointage par QR code ou badge** : élèves (groupes, rattrapages, salle d'étude) et personnel (horaires RH) | Rend le rattrapage dans un autre groupe et la **liste du jour de la salle d'étude** triviaux à saisir. Daftra (code-barres), Olvo (RFID), Classimum (QR) le font. | Moyen |
| **7** | **Inscription en ligne et liste d'attente par niveau et matière**, avec capacité du groupe | Remplace l'inscription par e-mail. Pics de rentrée et de révision du bac. MyCentreKey alerte à 7 demandes, Dzair Edu et Jackrabbit ont des listes d'attente. | Moyen |
| **8** | **Portail parent par lien sécurisé**, sans application : solde, présences, reçus | Réduit les appels « est-ce que mon fils est venu ? ». Moujtahid, Educanet et Olvo ont des apps : un lien web suffit au départ. | Moyen |
| 9 | Tableau de bord de rentabilité : chiffre par groupe, prof et espace ; marge après paie et dépenses | Relie les modules Achats, RH et Paiements de la spec. MyCentreKey affiche facturé, encaissé, dépenses et net. | Faible |
| 10 | Remises (fratrie, prorata), packs de séances, crédits de rattrapage | OmniDaam (remises, prorata), ClassCard (crédits). Indispensable pour l'Espace Révision. | Moyen |
| 11 | Planning des salles et des profs avec détection des conflits | Moujtahid, Centre Soutien | Moyen |
| 12 | Résilience réseau : PWA, appel saisi hors ligne puis synchronisé | Les concurrents de bureau vendent « 100 % hors ligne ». | Élevé |
| 13 | Alias d'enseignant et champs identifiants facultatifs | Contexte du décret 2015-1619 (voir 3.2) | Faible |
| 14 | Paiement en ligne Konnect, Flouci ou D17 pour les familles | Utile plus tard. Le cash domine. | Moyen |
| 15 | Multi-centres | OmniDaam Plus, MyCentreKey Growth | Moyen |

**À corriger dans la spec** : la règle « mois de 4 séances » doit être **paramétrable**. Il y a des mois à 5 séances, des jours fériés, le Ramadan et les vacances. Les seuils orange et rouge doivent aussi être réglables par centre.

---

## 9. Protection des données personnelles en Tunisie : loi organique n° 2004-63 et INPDP

Texte consulté : **traduction anglaise non officielle** de la loi organique n° 2004-63 du 27 juillet 2004 ([ictpolicyafrica.org](https://ictpolicyafrica.org/en/document/1bmfv348zkf?page=2)). Il faut se référer au texte arabe ou français du JORT pour toute décision.

| Point | Ce que dit la loi (ou la source) | Impact pour le SaaS et pour les centres |
|---|---|---|
| **Déclaration préalable (art. 7)** | Tout traitement doit être **déclaré à l'INPDP** par le responsable du traitement. L'Instance a un mois pour répondre, faute de quoi la déclaration est réputée acceptée ([art. 7](https://ictpolicyafrica.org/en/document/1bmfv348zkf?page=2)). Procédure fixée par le **décret n° 2007-3004** ([texte](https://legislation-securite.tn/wp-content/uploads/sites/5/2023/11/Decret-n°-2007-3004-du-27-Novembre-2007-Fr.pdf)). En juillet 2023, l'Instance a transmis une trentaine de dossiers au parquet pour traitement sans déclaration ([bkassocies.tn](https://bkassocies.tn/en/practical-guide-application-of-tunisian-legislation-on-personal-data-protection/)). | **Chaque centre** est responsable du traitement de ses élèves et doit déclarer. **Le SaaS est sous-traitant** pour ces données, et responsable de son propre traitement (comptes clients, facturation). **Fournir au centre un modèle de déclaration** est un vrai plus commercial. |
| **Données d'enfants (art. 28)** | « The processing of personal data related to a child cannot be carried out without the consent of his tutor and after authorization of the juvenile and family court judge » ([art. 28](https://ictpolicyafrica.org/en/document/1bmfv348zkf?page=6)). Le juge peut aussi autoriser sans le consentement du tuteur si l'intérêt de l'enfant l'exige ([page 7](https://ictpolicyafrica.org/en/document/1bmfv348zkf?page=7)). | **Point dur** : l'Espace Étude concerne surtout des mineurs. La lettre du texte exige **consentement du tuteur + autorisation du juge de la famille**. Je n'ai trouvé aucune doctrine sur son application aux écoles ou centres. **[À faire valider par un juriste tunisien]** Minimum : formulaire de consentement parental à l'inscription (champ et horodatage dans l'outil), minimisation (pas d'e-mail ou téléphone de l'élève mineur si le contact parent suffit). |
| **Sécurité et confidentialité (art. 18, 19, 23)** | Le responsable et le sous-traitant doivent empêcher la modification, l'altération et l'accès non autorisés, y compris pendant la transmission. Obligation de confidentialité, même après la fin du traitement ([bkassocies.tn](https://bkassocies.tn/en/practical-guide-application-of-tunisian-legislation-on-personal-data-protection/)). | HTTPS partout, isolation stricte des tenants, droits par rôle (la secrétaire ne voit pas la RH), journal d'accès, sauvegardes chiffrées. Contrat de sous-traitance avec chaque centre. |
| **Communication à des tiers (art. 47)** | Consentement exprès et écrit de la personne ([bkassocies.tn](https://bkassocies.tn/en/practical-guide-application-of-tunisian-legislation-on-personal-data-protection/)) | Attention aux intégrations : WhatsApp API (Meta), envoi d'e-mails, analytics. Les mentionner dans le consentement. |
| **Transfert à l'étranger (art. 50 à 52)** | Interdit s'il menace la sécurité publique. Autorisé seulement vers un pays au niveau de protection adéquat, et **toujours soumis à l'autorisation de l'INPDP** (réponse sous un mois), même vers les pays « adéquats ». S'il s'agit de données d'enfants, la demande va **au juge de la famille** ([bkassocies.tn](https://bkassocies.tn/en/practical-guide-application-of-tunisian-legislation-on-personal-data-protection/) ; décret 2007-3004, art. 11). | **Décision d'architecture** : héberger **en Tunisie** (datacenter local ou cloud tunisien) évite le régime de transfert. Héberger en UE (AWS, OVH, Hetzner…) suppose **une autorisation INPDP**, et pour des mineurs, le juge. **Recommandation : hébergement en Tunisie dès le pilote**, ou autorisation demandée formellement. Les e-mails transactionnels et WhatsApp transitent aussi à l'étranger, à documenter. |
| **Sanctions** | **Transfert à l'étranger sans autorisation : 1 an de prison et 5 000 TND d'amende (art. 90)**. Responsabilité civile (art. 82 et 83) ([bkassocies.tn](https://bkassocies.tn/en/practical-guide-application-of-tunisian-legislation-on-personal-data-protection/)). Plus de 200 dossiers transmis au parquet depuis 2016 selon une source commerciale ([dpo-consulting](https://www.dpo-consulting.com/fr-fr/blog/loi-tunisienne-sur-la-protection-des-donnees)). | Risque pénal réel, même si l'application est irrégulière. |
| **État de l'INPDP** | En octobre 2026, la société civile (Forum Chawki Gaddes) appelle à la « **réactivation** » de l'INPDP et à son autonomie ([allafrica](https://fr.allafrica.com/stories/202610040003.html)). Le site *inpdp.tn* ne répondait pas lors de ma visite. La Tunisie a ratifié la **Convention 108** du Conseil de l'Europe en 2017 ([coe.int](https://rm.coe.int/sante-projet-final-post-com-tech-juillet-2021-pour-soumission-19-10-20/pdf/1680a457b0)). Un projet de réforme (garanties pour le cloud et l'hébergement à l'étranger) est évoqué, sans statut confirmé ([globallawexperts](https://globallawexperts.com/?p=1510855)). | Contrôle faible aujourd'hui, mais **le cadre peut se durcir** (réforme alignée sur le RGPD). Concevoir dès maintenant « RGPD-ready » : export, suppression, registre, consentement. |

**Plan de conformité minimal pour le pilote** (*recommandation, pas un avis juridique*) :
1. Héberger en Tunisie, ou obtenir l'autorisation INPDP de transfert.
2. Signer un contrat de sous-traitance avec le centre.
3. Fournir au centre un modèle de déclaration INPDP et un formulaire de consentement parental (FR/AR).
4. Minimiser les données (le téléphone du parent plutôt que l'e-mail de l'élève, pas de CIN).
5. Tenir un journal d'accès, permettre l'export et la suppression par centre.
6. Faire valider l'art. 28 (juge de la famille) par un avocat tunisien avant la commercialisation large.

Pour mémoire, hors Tunisie : Maroc, loi 09-08 et CNDP ([cms.law](https://cms.law/fr/mar/legal-updates/flash-info-maroc-etat-des-lieux-de-la-protection-des-donnees-a-caractere-personnel-au-maroc-loi-n-09-08)). Algérie, loi 18-07 modifiée par la loi 25-11 de 2025, avec **DPO obligatoire** ([tsa-algerie.com](https://www.tsa-algerie.com/protection-de-donnees-personnelles-nouvelle-obligation-en-algerie/)). France et UE, RGPD.

---

## 10. Risques, Tunisie d'abord

1. **Disposition à payer et culture de la licence** : beaucoup de centres paient 0 aujourd'hui, ou ont payé une fois un freelance. L'abonnement doit **prouver sa valeur dès le premier mois** : impayés récupérés, caisse contrôlée, temps gagné sur les reçus. Ancrage de prix : ≈ 40-160 TND/mois (section 7).
2. **Informalité et réglementation** : décret 2015-1619, licence de la direction régionale (non vérifiée), peur fiscale de la traçabilité. Il faut **vendre le contrôle interne et la tranquillité**, pas la transparence. Il faut aussi des alias d'enseignant.
3. **Données des mineurs** : art. 28 (tuteur + juge de la famille), transfert à l'étranger soumis à autorisation, sanction pénale (art. 90). Cela pousse à **héberger en Tunisie**.
4. **Facturation électronique** : la société du fondateur doit probablement émettre des e-factures TTN pour ses abonnements (LF 2026). Les reçus des centres ne sont pas des factures.
5. **Saisonnalité** : achats à la rentrée (septembre-octobre) et avant le bac (avril-juin), **résiliations l'été**. Il faut favoriser l'annuel.
6. **Entrée d'un concurrent marocain ou algérien** : produits déjà en français, prix bas, WhatsApp. La défense passe par le modèle tunisien (Étude, Révision, Formation, salle d'étude), la présence terrain, le support en darija tunisienne et l'hébergement local.
7. **Ajout d'un module par un éditeur tunisien d'ERP scolaire** : Educanet, Acurschool, TSI ou Olvo peuvent ajouter un module « centre de soutien ». Classimum est à surveiller.
8. **Canal de vente** : marché relationnel (bouche-à-oreille entre propriétaires, groupes Facebook, démarchage). **Le centre pilote doit devenir une vitrine** : témoignage, chiffres avant et après, parrainage.
9. **Coûts variables** : l'API WhatsApp est désormais payante sur les messages « utility ». Commencer par les liens `wa.me`.

### Ce qu'il faut mesurer avec le centre pilote
- Outils actuels exacts (cahier, Excel, WhatsApp), temps passé par mois sur l'appel, les reçus et les relances.
- Taux d'impayés et retards moyens avant et après.
- Modèle de rémunération des enseignants (%, séance, fixe).
- Nombre de rattrapages hors groupe par mois.
- Usage réel de la salle d'étude.
- Montant que le propriétaire serait prêt à payer après 1 mois et après 3 mois.
- Logiciels concurrents qu'on lui a déjà proposés (Facebook, démarcheurs).

---

## 11. Limites de cette étude
- Les fonctionnalités viennent des **sites éditeurs**, je n'ai fait **aucun essai produit**. Les avis Capterra et G2 n'ont pas été accessibles (403).
- **Sites injoignables** lors de mes visites : Classimum, Acurschool, Bees Solution, SoutienGo (502), Innova, inpdp.tn, Hoplum, Fedena. Leurs informations viennent d'extraits de moteur de recherche et sont marquées **[NON VÉRIFIÉ]**.
- **L'offre tunisienne vendue uniquement via Facebook ou Instagram** n'est pas indexée et a pu m'échapper.
- **Aucune donnée quantitative** sur le nombre de centres d'étude en Tunisie ni sur leur équipement logiciel.
- Pour la loi 2004-63, j'ai lu une **traduction anglaise non officielle**. Pour le décret 2015-1619 et la LF 2026, j'ai lu des **articles de presse ou de cabinets**, pas le JORT.
