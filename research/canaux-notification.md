# Canaux de notification en Tunisie : WhatsApp, SMS, email, Messenger

> **Date** : 28 octobre 2026
> **Ticket** : [09. Canaux de notification en Tunisie](../docs/wayfinder/cadrage-mvp/issues/09-canaux-notification.md)
> **Question** : par quel canal envoyer les reçus, les rappels d'impayés et les avis d'absence aux parents et aux élèves en Tunisie, et pour quel coût ?
> **Méthode** : sources primaires d'abord. Pour WhatsApp, j'ai lu la documentation développeurs de Meta, le centre d'aide WhatsApp et **la grille tarifaire officielle de Meta**, en vigueur au 1ᵉʳ octobre 2026. Pour le reste, j'ai consulté les pages tarifs des fournisseurs. Chaque affirmation est suivie de son URL.
> **Niveau de confiance** : élevé sur les tarifs WhatsApp, lus dans le fichier de Meta et recoupés avec le fichier de Twilio. Moyen sur les SMS locaux : ce sont des pages commerciales, parfois anciennes, et les opérateurs ne publient pas leurs prix. Faible sur les parts d'usage des messageries : il n'existe pas d'enquête tunisienne récente qui soit publique. Les mentions **[NON VÉRIFIÉ]** signalent ce que je n'ai pas pu confirmer à la source.
> **Hors périmètre** (décision du fondateur) : protection des données, consentement, réglementation télécom. Seules les règles techniques sur le Sender ID SMS sont traitées.
> **Conversions** : mêmes ordres de grandeur que l'étude de l'existant, non vérifiés à la date du jour : **1 $ ≈ 3,0 TND ; 1 € ≈ 3,4 TND**.

---

## 1. Résumé

| | Recommandation |
|---|---|
| **MVP** | **Liens `wa.me` pré-remplis**, envoyés à la main depuis le WhatsApp du centre. Chaque message contient **un lien vers le reçu PDF** hébergé par SchoolHub. Coût : **0 TND**, rien à faire valider chez Meta. **L'email part en parallèle** quand le parent en a un (Brevo gratuit ou Amazon SES : **≈ 0 TND**), avec copie au centre pour la traçabilité. |
| **V2** | **API WhatsApp Cloud** avec des modèles « utility ». Coût : **≈ 8 TND par mois** pour 300 élèves en direct chez Meta, **≈ 17 TND** via Twilio. On peut garder le numéro actuel du centre grâce à la « coexistence ». **SMS de secours** par un agrégateur tunisien (**≈ 30 à 60 TND HT par mois**) pour les parents sans WhatsApp. |
| **À éviter** | Les agrégateurs SMS internationaux vers la Tunisie (Twilio, Vonage) : **≈ 0,46 à 0,54 $ le SMS**, soit environ 900 à 1 000 TND par mois. Les revendeurs WhatsApp tunisiens à abonnement : à partir de 1 200 DT par mois. |
| **À valider avec le centre pilote** | Quelle messagerie les parents utilisent vraiment. Une mesure de Sensor Tower de 2024 donne **Messenger loin devant WhatsApp en Tunisie** en utilisateurs actifs (≈ 6,3 M contre ≈ 2,3 M). Voir la section 6. |

---

## 2. Hypothèses de volume (centre de 300 élèves)

Ce sont les hypothèses par défaut du ticket. **Un message = un destinataire**, en général le parent.

| Message | Hypothèse | Volume par mois |
|---|---|---|
| Reçu | 1 par élève et par mois | **300** |
| Rappel d'impayé | 25 % des élèves (75) × 2 rappels | **150** |
| Avis d'absence | 15 % des élèves (45) × 1 par semaine × 4,33 semaines | **≈ 195** |
| **Total** | | **≈ 645 messages par mois** |

- **Si l'on écrit aussi à l'élève** (parent + élève), il faut **doubler** : ≈ 1 290 messages par mois. Les coûts variables de la section 5 doublent aussi.
- **Pics** : les reçus se concentrent en début de mois. Si le centre encaisse 300 mensualités sur 2 ou 3 jours, il faut envoyer 100 à 150 messages par jour. C'est important pour les plafonds journaliers (sections 3.2 et 4.3).

---

## 3. WhatsApp

### 3.1 Liens `wa.me` pré-remplis (clic pour discuter)

**Fonctionnement** (source officielle : [centre d'aide WhatsApp, « How to use click to chat »](https://faq.whatsapp.com/5913398998672934/)) :
- **Format** : `https://wa.me/<numéro>?text=<texte encodé en URL>`.
- **Numéro** : il doit être au format international, sans zéros initiaux, sans parenthèses ni tirets. Pour la Tunisie, cela donne `https://wa.me/216XXXXXXXX`.
- **Message** : le texte pré-rempli « apparaît automatiquement dans le champ de saisie ». **L'envoi n'est donc pas automatique** : la secrétaire clique sur le lien, vérifie, puis appuie sur Envoyer.
- **Appareils** : le lien fonctionne sur téléphone et sur WhatsApp Web.

**Limites** :
- **Aucune pièce jointe via le lien.** Le format n'accepte qu'un paramètre `text` ([même source](https://faq.whatsapp.com/5913398998672934/)).
- **En revanche, une URL dans le texte fonctionne** : c'est du texte comme un autre. On peut donc inclure un lien vers le reçu PDF, par exemple `https://app.schoolhub.tn/r/9fK2…`, avec un jeton signé non devinable. Il faut l'encoder dans `text=` comme le reste du message. L'affichage d'un aperçu du lien chez le destinataire n'est pas garanti **[NON VÉRIFIÉ]**.
- **Un clic et un envoi par destinataire.** On ne peut pas envoyer en masse. Mon estimation, non mesurée : environ 20 secondes par message. Pour ≈ 645 messages, cela fait **≈ 3,5 heures de secrétariat par mois**. Une partie de ce temps existe déjà : un reçu se donne au moment de l'encaissement, et la secrétaire envoie déjà des messages WhatsApp aujourd'hui.
- **Risque anti-spam.** En octobre 2025, WhatsApp a annoncé tester un **plafond mensuel de messages sans réponse**, pour les particuliers comme pour les entreprises. Un message qui reçoit une réponse ne compte plus ([TechCrunch, 17/10/2025](https://techcrunch.com/2025/10/17/whatsapp-will-curb-the-number-of-messages-people-and-businesses-can-send-without-a-response)). Le seuil et les pays concernés ne sont pas publiés. **Application à la Tunisie [NON VÉRIFIÉ]**. Les messages envoyés à la main entrent a priori dans ce décompte.

**Pour SchoolHub, c'est trivial à développer** : il suffit de construire l'URL côté Angular ou .NET. Il n'y a ni compte Meta, ni modèle à faire valider, ni coût. Le bon écran est une **file d'envoi** : liste des messages à envoyer (reçus du jour, absences, relances), bouton « Ouvrir WhatsApp », puis case « envoyé ». Ce mécanisme existe déjà chez des concurrents marocains, par exemple MyCentreKey et sa « liste quotidienne des parents en retard avec message WhatsApp pré-rédigé » (voir l'[étude de l'existant](etude-de-l-existant.md)).

### 3.2 WhatsApp Business Platform (API Cloud)

#### Tarifs pour la Tunisie (en vigueur au 1ᵉʳ octobre 2026)

**La Tunisie est dans la région tarifaire « Rest of Africa »**, qui regroupe 40 pays et applique un tarif unique à toute la région. Le changement du 1ᵉʳ octobre 2026 a sorti le Maroc de cette région, mais pas la Tunisie ([Meta, Pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing)).

**Prix Meta par message livré, en USD** : grille officielle « Cost per message in USD on the WhatsApp Business Platform, effective October 1, 2026 », téléchargée depuis la [section Rate cards de Meta](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing#rate-cards-and-volume-tiers). Les mêmes valeurs figurent dans le [fichier CSV de Twilio](https://www.twilio.com/content/dam/twilio-com/pricing-data/en/WhatsAppPricing-pricing-details.csv), à la ligne « TN, Tunisia, Rest of Africa », dans un fichier modifié le 01/10/2026.

| Catégorie | Rest of Africa (dont la Tunisie) | ≈ TND | Pour comparer : Maroc | Pour comparer : France |
|---|---|---|---|---|
| **Marketing** | **0,0225 $** | 0,068 | 0,0414 $ | 0,0859 $ |
| **Utility** (reçu, rappel, absence) | **0,004 $** | **0,012** | 0,023 $ | 0,030 $ |
| **Authentication** | 0,004 $ | 0,012 | 0,023 $ | 0,030 $ |
| Authentication-international | n/a | – | 0,0811 $ | n/a |
| **Service** (réponse libre dans la fenêtre de 24 h) | 0,004 $, **les 1 000 premiers par mois et par numéro étant gratuits** | 0,012 | 0,023 $ | 0,030 $ |

- **Paliers de volume** : ils ne concernent que les messages utility et authentication. Pour la région Rest of Africa, le tarif de liste s'applique **de 0 à 100 000 messages utility par mois** ([fichier des paliers de Meta](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing#volume-tiers-for-utility-and-authentication-messages)). Les paliers sont calculés par portefeuille business (business portfolio). Ils ne changent donc rien pour un centre, et ne joueraient que si un seul portefeuille regroupait plus de 150 centres comme celui-ci.
- **Autre gratuité** : une fenêtre gratuite de 7 jours s'ouvre quand un utilisateur écrit au centre depuis une publicité « Click to WhatsApp » ([Meta, Pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing)). Ce cas ne concerne pas les notifications.

#### Le changement du 1ᵉʳ octobre 2026

D'après le journal des modifications de [Meta, Pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing) :
- **Les messages utility envoyés dans une fenêtre de service client ouverte deviennent payants.** Ils étaient gratuits depuis le 1ᵉʳ juillet 2025. Dans l'API, leur `pricing.type` passe de `free_customer_service` à `regular`.
- **Les messages de service redeviennent payants** (ils étaient gratuits depuis le 1ᵉʳ novembre 2024), avec **1 000 messages gratuits par mois et par numéro**. Ce quota se remet à zéro chaque mois et ne se reporte pas.
- Une entreprise **sans moyen de paiement enregistré au 30/09/2026** ne peut plus envoyer de messages de service.
- 40 tarifs ont changé et 9 pays sont sortis de leur région « Rest of », dont le Maroc. Aucune ligne du journal ne cite la Tunisie ni la région Rest of Africa.

**Conséquence pour SchoolHub** : **tout reçu, rappel ou avis d'absence coûte 0,004 $**, que le parent ait écrit au centre dans les dernières 24 heures ou non. Le calcul est donc simple : nombre de messages × 0,004 $.

#### Modèles de message, fenêtre de 24 heures, catégories

- **Fenêtre de service client de 24 heures** : elle s'ouvre quand l'utilisateur écrit ou appelle, et chaque nouveau message la relance. Fenêtre ouverte, on peut envoyer du texte libre. **Fenêtre fermée, on ne peut envoyer que des modèles approuvés** ([Meta, Pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing)). Un reçu, une relance ou une absence partira presque toujours **hors fenêtre**, donc sous forme de **modèle**.
- **Validation des modèles** : elle est automatique, à la création ou à la modification, et peut prendre **jusqu'à 24 heures**. Seul un modèle `APPROVED` peut être envoyé. Un portefeuille non vérifié est limité à **250 modèles**, un portefeuille vérifié à 6 000. Chaque langue compte comme un modèle à part : FR et AR font 2 modèles ([Meta, Message templates](https://developers.facebook.com/docs/whatsapp/business-management-api/message-templates)).
- **Catégorie utility** : le message doit être **non promotionnel** et **spécifique à l'utilisateur** (sa commande, son compte, ses services, ses transactions). Meta donne en exemple un rappel de paiement mensuel. **Un contenu mixte, par exemple un rappel avec une promo, est classé marketing**, de même qu'un contenu ambigu ([Meta, Template categorization](https://developers.facebook.com/docs/whatsapp/updates-to-pricing/new-template-guidelines)). Un reçu, une relance d'impayé ou un avis d'absence rédigés de façon neutre entrent donc dans la catégorie utility. **Le risque est financier** : un modèle reclassé marketing coûte 0,0225 $, soit 5,6 fois plus.
- **PDF dans un modèle** : l'en-tête d'un modèle peut être un **document de type PDF** (format `DOCUMENT`). Un **bouton URL** peut aussi porter une variable en suffixe, par exemple `https://app.schoolhub.tn/r/{{1}}`. Le corps est limité à **1 024 caractères** ([Meta, Template components](https://developers.facebook.com/docs/whatsapp/business-management-api/message-templates/components)). On peut donc **envoyer le reçu PDF en pièce jointe**, ou un bouton « Voir mon reçu ».

#### Vérification de l'entreprise et plafonds d'envoi

D'après [Meta, Messaging limits](https://developers.facebook.com/docs/whatsapp/messaging-limits) :
- **Plafond de départ** : un portefeuille business qui vient d'être créé peut écrire à **250 destinataires uniques** par période glissante de 24 heures, hors fenêtre de service. **Un centre qui envoie 300 reçus le même jour dépasse ce plafond.**
- **Passage à 2 000** : soit **vérifier l'entreprise**, soit la faire vérifier par le partenaire qui l'a intégrée, soit envoyer 2 000 messages de qualité en 30 jours. Ensuite, le plafond monte tout seul jusqu'à 10 000, 100 000 puis illimité.
- **Portée du plafond** : il est calculé **par portefeuille business**, tous numéros confondus.
- **Documents acceptés** : la liste des documents que Meta accepte pour vérifier une entreprise tunisienne (registre de commerce, matricule fiscal…) n'a pas été consultée **[NON VÉRIFIÉ]**.

#### Architecture multi-tenant : qui possède le compte WhatsApp ?

Trois options :

1. **SchoolHub devient « Tech Provider » chez Meta** ([Meta, Get started for Tech Providers](https://developers.facebook.com/docs/whatsapp/solution-providers/get-started-for-tech-providers)).
   - **Prérequis** : SchoolHub vérifie son entreprise, puis passe l'**App Review** (2 vidéos à fournir) avec un accès avancé à `whatsapp_business_messaging` et à `whatsapp_business_management`.
   - **Intégration des centres** : chaque centre est raccordé par **Embedded Signup**. Attention, les versions 2 et 3 d'Embedded Signup sont abandonnées le 15/10/2026 : il faut utiliser la **v4** ([Meta](https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/onboarding-business-app-users)).
   - **Paiement** : « Your onboarded clients must add a credit card ». **Chaque centre doit donc enregistrer une carte bancaire chez Meta.** Pour un centre tunisien, payer en devises avec une carte locale est un frein pratique probable **[NON VÉRIFIÉ]**.
   - **Coût Meta seul** : 0,004 $ par message.
2. **Passer par un BSP** (Business Solution Provider, revendeur agréé par Meta) qui facture SchoolHub, et SchoolHub refacture les centres. Le centre n'a alors besoin d'aucune carte.
   - **[Twilio](https://www.twilio.com/en-us/whatsapp/pricing)** : prix Meta + **0,005 $ par message**, entrant comme sortant. Il existe un SDK .NET officiel (NuGet `Twilio`).
   - **[360dialog](https://360dialog.com/pricing)** : **49 € par mois et par numéro**, sans marge sur les frais Meta. L'offre partenaires démarre à 250 € par mois pour 5 canaux.
   - **Prestataires tunisiens** : un revendeur comme [Visibility.tn](https://visibility.tn/services/whatsapp-business-api) affiche **1 200 à 5 500 DT par mois**, **2 000 à 5 000 DT de mise en place** et **0,25 DT par message modèle**. Ces prix sont hors de proportion pour un centre. Je n'ai pas trouvé de BSP tunisien à prix public raisonnable, ni consulté l'annuaire des partenaires Meta **[NON VÉRIFIÉ]**.
3. **SchoolHub possède un seul compte et un seul numéro pour tous les centres.** C'est le plus simple techniquement, mais les parents reçoivent un message de « SchoolHub » et non du centre. Les plafonds et la note de qualité deviennent aussi communs à tous les centres. **Je le déconseille.**

**La coexistence**, c'est-à-dire garder le numéro WhatsApp Business actuel du centre :
- **Principe** : un centre qui utilise déjà l'application WhatsApp Business peut être raccordé à l'API Cloud **avec le même numéro**. Il continue à répondre depuis l'application, et l'historique est synchronisé ([Meta, Onboarding WhatsApp Business app users](https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/onboarding-business-app-users)).
- **Coût** : les messages envoyés depuis l'application restent gratuits, ceux envoyés par l'API suivent les tarifs de l'API.
- **Limites** : les listes de diffusion passent en lecture seule, le débit est limité à 20 messages par seconde et les groupes ne sont pas synchronisés.
- **Prérequis** : il faut être **Tech Provider ou Solution Partner**.
- **Pays exclus** : la page ne cite aucune restriction par pays. La disponibilité réelle en Tunisie n'a pas été testée **[NON VÉRIFIÉ]**.

**C'est la meilleure transition depuis le MVP `wa.me`** : les parents continuent de recevoir les messages du numéro qu'ils connaissent déjà.

#### Effort de mise en place côté .NET 10

Estimation de ma part, non mesurée :
- **Envoi** : un simple appel REST. Un `POST` sur l'API Graph `/{phone-number-id}/messages` avec un jeton bearer, le nom du modèle, la langue et les paramètres, plus l'en-tête document avec l'URL du PDF. Je n'ai pas vérifié s'il existe un SDK .NET officiel de Meta **[NON VÉRIFIÉ]**. Un `HttpClient` typé suffit.
- **Webhooks** : un endpoint ASP.NET Core qui gère la vérification `hub.challenge`, contrôle la signature `X-Hub-Signature-256` et suit les statuts envoyé, livré, lu et échoué par message.
- **Gestion des modèles** : création via l'API ou dans WhatsApp Manager, en FR et en AR, avec suivi du statut d'approbation.
- **Charge de travail** : **5 à 10 jours de développement** pour l'envoi, les webhooks, les modèles et le suivi, **plus 2 à 6 semaines de démarches Meta** (vérification de l'entreprise, App Review, Embedded Signup) **[NON VÉRIFIÉ]** pour ces délais. Via Twilio, les démarches Meta sont en partie prises en charge par le BSP.

### 3.3 Application WhatsApp Business (gratuite) : option intermédiaire

- **Listes de diffusion** : jusqu'à **256 contacts par liste**, en nombre de listes illimité. **Seuls les contacts qui ont enregistré le numéro du centre dans leur carnet reçoivent le message**, et l'expéditeur ne voit pas d'erreur pour les autres. Les listes ne sont **pas disponibles sur Windows, Mac ni Web** ([centre d'aide WhatsApp, broadcast lists](https://faq.whatsapp.com/861663048350950/)).
- **Plafond de diffusion** : dans certains pays, « up to 35 free broadcast messages per month », avec un déploiement progressif ([centre d'aide WhatsApp, broadcast limits](https://faq.whatsapp.com/1176325363861991/)). Le calcul exact (35 envois de liste ou 35 messages) et l'application à la Tunisie et à l'application Business ne sont pas clairs **[NON VÉRIFIÉ]**.
- **Usage possible** : les messages de diffusion sont **identiques pour tous**. Ils ne conviennent donc **pas aux reçus ni aux relances personnalisées**. Ils restent utiles pour une annonce collective (« centre fermé demain », « changement d'horaire du groupe du lundi »).
- **Catalogue** : il sert à présenter des produits ou des services, ici des programmes et des tarifs. Ce n'est pas un outil de notification, et c'est hors sujet ici.
- **Avec SchoolHub** : l'application Business est **le bon support des liens `wa.me`**. La secrétaire travaille dans WhatsApp Business, sur téléphone ou en Web pour les liens. Le centre aura aussi un profil professionnel et des réponses rapides, et pourra passer ensuite à l'API par coexistence.

---

## 4. SMS

### 4.1 Fournisseurs et prix vers les mobiles tunisiens

| Fournisseur | Type | Prix par SMS | Minimum, validité | API | Source |
|---|---|---|---|---|---|
| **WinSMS** | Agrégateur tunisien | Packs HT : 1 500 SMS à 74,850 DT (**0,050**), 5 000 à 230 DT (0,046), 10 000 à 440 DT (**0,044**), 15 000 à 600 DT (0,040)… jusqu'à 200 000 à 5 780 DT (0,029) | Pack minimum à 74,850 DT HT. Validité de 6 mois (petits packs) à 2 ans | « API SMS Tunisie disponible » dans tous les packs, avec exemples de code dont **C#** | [winsms.tn/shop](https://www.winsms.tn/shop/), [options et tarifs](https://www.winsms.tn/sms-tunisie-options-tarifs/) |
| **eSMS Africa** | Agrégateur panafricain | **0,10 TND** (Basic), 0,08 (Business), 0,06 TND (Enterprise) | Aucun minimum affiché | REST (SDK Python, Node, PHP, Go) et SMPP. Couvre **Ooredoo, Tunisie Telecom et Orange**. Accusés de réception en temps réel | [esmsafrica.io](https://esmsafrica.io/fr/sms/tunisia) |
| **BipBip** | Agrégateur tunisien | 0,060 TND (pack de 1 500) à 0,040 TND (pack de 250 000) | ? | REST (SDK Node, Python, PHP) et SMPP | [bip-bip.tn](https://bip-bip.tn/), site injoignable lors de ma visite (ECONNREFUSED). Chiffres tirés d'un extrait de recherche **[NON VÉRIFIÉ]** |
| **Tunisie Telecom, Ooredoo, Orange Tunisie** | Opérateurs | Aucun tarif public d'envoi en masse pour les entreprises. Offres sur devis via les services entreprises | ? | ? | **[NON VÉRIFIÉ]** : aucune page officielle trouvée |
| **Twilio** | International | **0,5379 $** (≈ 1,61 TND) par segment | Paiement à l'usage | REST, SDK .NET officiel | [twilio.com/sms/pricing/tn](https://www.twilio.com/en-us/sms/pricing/tn) |
| **Vonage** | International | **0,46379 $** (≈ 1,39 TND) par message (Tunisie sélectionnée sur la page) | Paiement à l'usage | REST | [vonage.com/…/sms/pricing](https://www.vonage.com/communications-apis/sms/pricing/) |
| **Infobip** | International | Sur devis | ? | REST | **[NON VÉRIFIÉ]** |

**Lecture** :
- **Les agrégateurs tunisiens coûtent 10 à 40 fois moins cher que Twilio et Vonage.** Les agrégateurs internationaux paient probablement des frais de terminaison internationaux vers la Tunisie **[NON VÉRIFIÉ]** pour l'explication. Leur prix les exclut pour un centre.
- **Prix des pages locales** : la page WinSMS porte un « © 2016 » et un article d'actualité de mars 2025. Ses prix sont à confirmer par devis.

### 4.2 Sender ID alphanumérique (règles techniques)

- **Chez Twilio** ([Twilio SMS Guidelines, Tunisie](https://www.twilio.com/en-us/guidelines/tn/sms)) :
  - le Sender ID **dynamique** est accepté par les opérateurs et par Twilio ;
  - le Sender ID **pré-enregistré « domestique »** est **requis par les opérateurs pour le trafic domestique**, avec **18 jours** de mise en place ;
  - l'UCS-2 (arabe) est pris en charge.
- **Fonctions absentes** : pas de numéro long domestique, pas de short code, **pas de SMS bidirectionnel** (le parent ne peut pas répondre) et pas de SMS concaténés chez Twilio. Un numéro long international est **remplacé par un Sender ID alphanumérique générique**.
- **Sender ID chez les agrégateurs locaux** :
  - **WinSMS** : 1 entête inclus dans les petits packs, entête supplémentaire à **99 DT HT** ;
  - **eSMS Africa** : **100 $ une seule fois**, prélevés seulement si le Sender ID est approuvé ;
  - **BipBip** : Sender ID personnalisé « après validation réglementaire » **[NON VÉRIFIÉ]**.
- **Longueur et segments** :
  - **Texte latin** : 160 caractères par SMS, et chaque tranche de 160 caractères compte comme un SMS ([WinSMS](https://www.winsms.tn/sms-tunisie-options-tarifs/)) ;
  - **Arabe (UCS-2)** : la règle standard GSM est de 70 caractères par segment. Les pages consultées ne détaillent pas le décompte **[NON VÉRIFIÉ]**.
  - **Conséquence** : un avis en arabe ou un reçu avec un lien coûte souvent **2 SMS**.

### 4.3 Points forts et limites du SMS

- **Points forts** : le SMS fonctionne sans internet ni smartphone et sans dépendre d'une application. Les parents sans WhatsApp, ou avec un vieux téléphone, le reçoivent quand même.
- **Limites** :
  - **pas de pièce jointe** : seul un lien vers le PDF est possible ;
  - pas de réponse possible avec un Sender ID alphanumérique ;
  - coût réel à chaque message ;
  - une procédure de Sender ID, sinon l'expéditeur affiché est générique.

---

## 5. Email

### 5.1 Fournisseurs transactionnels

| Fournisseur | Gratuit | Prix au-delà | SDK .NET | Source |
|---|---|---|---|---|
| **Amazon SES** | Pas de quota gratuit fixe. Crédits AWS jusqu'à 200 $ pour un nouveau compte | **0,10 $ pour 1 000 emails**, plus 0,12 $ par Go de pièces jointes | Oui, AWS SDK for .NET (`AWSSDK.SimpleEmailV2`) | [aws.amazon.com/ses/pricing](https://aws.amazon.com/ses/pricing/) |
| **Brevo** | Offre gratuite : **300 emails par jour** | Starter à partir de 9 $ par mois (8,08 $ en annuel) pour 5 000 emails par mois | Oui, `brevo_csharp`, SDK maintenu par Brevo | [brevo.com/pricing](https://www.brevo.com/pricing/), [github.com/getbrevo/brevo-csharp](https://github.com/getbrevo/brevo-csharp) |
| **Resend** | **3 000 emails par mois, plafonnés à 100 par jour** | Pro à 20 $ par mois pour 50 000 emails, puis 0,90 $ pour 1 000 | Oui, NuGet `Resend` | [resend.com/pricing](https://resend.com/pricing), [docs .NET](https://resend.com/docs/send-with-dotnet) |
| **SendGrid** (Twilio) | Essai seulement : 100 emails par jour pendant **60 jours** | Essentials à partir de 19,95 $ par mois | Oui, NuGet `SendGrid` (non revérifié ici) | [twilio.com/…/email-api/pricing](https://www.twilio.com/en-us/products/email-api/pricing) |
| **Mailgun** | **100 emails par jour** | Basic à 15 $ par mois pour 10 000 emails, puis à partir de 1,80 $ pour 1 000 | Pas de SDK officiel connu **[NON VÉRIFIÉ]**. SMTP ou REST via `HttpClient` | [mailgun.com/pricing](https://www.mailgun.com/pricing/) |

- **Pour SchoolHub, qui envoie pour tous les centres** : **Amazon SES** est le moins cher dès qu'on dépasse les offres gratuites. 20 centres × 645 emails, soit environ 12 900 emails par mois, reviennent à **≈ 1,3 $ par mois**. **Brevo gratuit** suffit pour le pilote seul, mais **300 emails par jour ne suffisent pas** pour un lot de 300 reçus avec en plus les absences du jour. Resend gratuit (100 par jour) est trop juste.
- **Pièce jointe** : l'email est le seul canal gratuit qui transporte le PDF en pièce jointe.

### 5.2 Les parents tunisiens utilisent-ils l'email ?

- **Aucune source publique trouvée** sur l'usage de l'email par les parents ou les ménages tunisiens **[NON VÉRIFIÉ]**. Le rapport [DataReportal, Digital 2026 : Tunisie](https://datareportal.com/reports/digital-2026-tunisia) donne 10,4 M d'internautes (84,3 %) et 7,83 M d'identités sur les réseaux sociaux (63,3 %), mais ne mesure pas l'email.
- **Indice de terrain** : le [cadrage](../docs/cadrage.md) note que « beaucoup d'élèves et de parents ne lisent pas leurs emails ». C'est une observation du fondateur, pas une mesure.
- **Conclusion** : l'email est un **canal d'archive** (reçu PDF en pièce jointe, copie au centre pour la traçabilité exigée par le [cahier des charges](../docs/cahier-des-charges.md)). Ce n'est **pas un canal d'alerte**. Il faut demander au centre pilote quelle part de ses parents a une adresse email valide.

---

## 6. Messenger, Viber, Telegram

| Messagerie | Usage en Tunisie | Réalisme pour SchoolHub |
|---|---|---|
| **Messenger** | Portée publicitaire de **5,85 M (47,3 % de la population)** fin 2025, soit 56,2 % des internautes ([DataReportal 2026](https://datareportal.com/reports/digital-2026-tunisia)). Selon Sensor Tower au 2ᵉ trimestre 2024, **≈ 6,3 M d'utilisateurs actifs pour Messenger contre ≈ 2,3 M pour WhatsApp** ([Sensor Tower](https://sensortower.com/blog/2024-q2-unified-top-5-communication%20apps-units-tn-6070aae1241bc16eb81f5bab)). La définition exacte d'« utilisateur actif » n'est pas donnée. | **Probablement la messagerie la plus utilisée en Tunisie**, mais elle est **mal adaptée aux notifications**. La plateforme Messenger écrit à un identifiant propre à la Page, pas à un numéro de téléphone. En dehors de la fenêtre de 24 heures, une Page ne peut écrire qu'à un utilisateur qui l'a contactée, et seulement avec des tags de message limités **[NON VÉRIFIÉ]** : la page Meta sur les tags n'a pas pu être lue. Il faudrait que chaque parent écrive d'abord à la Page du centre. **Pas pour le MVP.** |
| **Telegram** | Dans le top 5 des applications de communication, avec **≈ 483 000 utilisateurs actifs** au 2ᵉ trimestre 2024 ([Sensor Tower](https://sensortower.com/blog/2024-q2-unified-top-5-communication%20apps-units-tn-6070aae1241bc16eb81f5bab)) | Usage marginal. Un bot ne peut écrire qu'à quelqu'un qui l'a démarré **[NON VÉRIFIÉ]**. **Non.** |
| **Viber** | Absent du top 5 de Sensor Tower. Aucune donnée trouvée **[NON VÉRIFIÉ]** | **Non.** |

**Mise en garde importante.** L'[étude de l'existant](etude-de-l-existant.md) pose WhatsApp comme « le canal réel ». **La seule mesure quantitative trouvée (Sensor Tower, 2024) place Messenger nettement devant WhatsApp en Tunisie.** Deux nuances :
- les parents d'élèves, les adultes de 35 à 55 ans, peuvent avoir un usage différent de la moyenne nationale ;
- l'usage pour parler à un commerce ou à un centre n'est pas mesuré.

**À vérifier en priorité avec le centre pilote** : par quel canal parle-t-il aujourd'hui aux parents, et quelle part d'entre eux a WhatsApp ? Si la réponse est « Messenger », le lien `wa.me` du MVP perd de son intérêt. Il existe un équivalent `m.me/<page>` pour ouvrir une conversation, mais sans texte pré-rempli **[NON VÉRIFIÉ]**.

---

## 7. Tableau comparatif

| Canal | Coût par message | Effort de mise en place | Pièce jointe ou PDF | Fiabilité de remise | Adéquation à un centre tunisien |
|---|---|---|---|---|---|
| **WhatsApp `wa.me`** (manuel) | **0** | **Très faible** : une URL à construire, aucun compte | Non. **Lien vers le PDF dans le texte** | Bonne si le parent a WhatsApp. Dépend de la secrétaire (oubli, temps). Risque de plafond anti-spam **[NON VÉRIFIÉ]** | **Excellente pour le MVP** : reprend ce que la secrétaire fait déjà. Environ 3,5 h par mois pour 300 élèves |
| **WhatsApp Business app, diffusion** | 0 (35 diffusions gratuites par mois dans certains pays) | Nul | Oui, manuellement | Seulement aux contacts qui ont enregistré le numéro. Listes de 256 contacts | Annonces collectives seulement. Pas de reçu personnalisé |
| **API WhatsApp Cloud, direct Meta** | **0,004 $ ≈ 0,012 TND** (utility) | **Élevé** : Tech Provider, App Review, Embedded Signup v4, vérification de l'entreprise, modèles, webhooks. 5 à 10 jours de dev et plusieurs semaines de démarches | **Oui, PDF en en-tête du modèle**, ou bouton URL | Très bonne, avec statuts livré et lu. Plafond de 250 destinataires par jour avant vérification | Très bonne **si le centre peut payer Meta par carte** **[NON VÉRIFIÉ]** |
| **API WhatsApp via Twilio** | 0,004 + 0,005 = **0,009 $ ≈ 0,027 TND** | Moyen : SDK .NET, Meta en partie géré par Twilio | Oui | Très bonne | Bonne : SchoolHub paie et refacture, le centre n'a pas besoin de carte |
| **API WhatsApp via revendeur tunisien** | 0,25 DT par modèle **et** 1 200 DT par mois minimum | Faible | Oui | Très bonne | **Hors budget** |
| **SMS, agrégateur tunisien** | **0,029 à 0,10 TND HT** par segment (arabe ou lien : souvent 2 segments) | Faible : API HTTP, achat d'un pack, demande de Sender ID | Non, lien seulement | Très bonne, même sans internet. Pas de réponse possible | **Bon canal de secours**, pour les impayés urgents et les parents sans WhatsApp |
| **SMS, Twilio ou Vonage** | **0,46 à 0,54 $ ≈ 1,4 à 1,6 TND** | Faible | Non | Bonne | **Trop cher** |
| **Email (SES, Brevo)** | **≈ 0** (0,0001 $ chez SES) | Faible : SDK .NET, réglages SPF, DKIM et DMARC du domaine | **Oui, pièce jointe** | Bonne techniquement, **mais lecture faible** **[NON VÉRIFIÉ]** | Archive et traçabilité. Pas un canal d'alerte |
| **Messenger** | 0 **[NON VÉRIFIÉ]** | Élevé : Page, App Review, le parent doit écrire en premier | Oui | Bonne une fois la conversation ouverte | Très utilisé, mais impossible d'écrire à un numéro. Pas pour le MVP |

---

## 8. Coût mensuel estimé : centre de 300 élèves (≈ 645 messages par mois)

Hypothèses de la section 2 : un message par destinataire, envoyé au parent seul. Taux : 1 $ ≈ 3,0 TND, 1 € ≈ 3,4 TND.

| Scénario | Calcul | **Par mois** | Si parent + élève (×2) |
|---|---|---|---|
| **A. `wa.me` manuel** (MVP) | 645 × 0 | **0 TND** (+ ≈ 3,5 h de secrétariat, estimation) | 0 TND (+ ≈ 7 h) |
| **B. Email seul** (Brevo gratuit ou SES) | 645 × 0,0001 $ | **≈ 0 TND** (0,2 TND sur SES) | ≈ 0 TND |
| **C. API WhatsApp, direct Meta** (tout en utility) | 645 × 0,004 $ = 2,58 $ | **≈ 8 TND** | ≈ 15 TND |
| C bis. Même chose si les modèles sont **classés marketing** | 645 × 0,0225 $ = 14,5 $ | ≈ 44 TND | ≈ 87 TND |
| **D. API WhatsApp via Twilio** | 645 × 0,009 $ = 5,81 $ (+ 0,005 $ par réponse reçue) | **≈ 17 TND** | ≈ 35 TND |
| E. API WhatsApp via 360dialog | 49 € + 2,58 $ | ≈ 175 TND | ≈ 183 TND |
| F. API WhatsApp via revendeur tunisien (Visibility.tn, Starter) | 1 200 DT + 645 × 0,25 DT | ≈ 1 360 TND (+ 2 000 à 5 000 DT de mise en place) | ≈ 1 520 TND |
| **G. SMS via WinSMS** (pack Premium à 0,044 DT HT) | 645 à 1 290 segments × 0,044 | **≈ 28 à 57 TND HT** (achat d'avance : pack de 440 DT HT valable 1 an) | ≈ 57 à 114 TND HT |
| G bis. SMS via eSMS Africa (Basic à 0,10 TND) | 645 à 1 290 × 0,10 | ≈ 65 à 129 TND (+ 100 $ une fois pour le Sender ID) | ≈ 129 à 258 TND |
| H. SMS via Twilio ou Vonage | 645 × 0,46 à 0,54 $ | ≈ 900 à 1 040 TND | ≈ 1 800 à 2 080 TND |
| **I. Mix V2 recommandé** : API WhatsApp (C ou D) pour tous, email en copie, SMS pour les 10 % sans WhatsApp | 8 à 17 + 0 + (65 × 0,044 à 0,10) | **≈ 11 à 24 TND** | ≈ 22 à 48 TND |

**Ordre de grandeur pour le prix de SchoolHub** : l'étude de l'existant vise environ 49 à 59 TND par mois (Essentiel) et environ 149 à 179 TND par mois (Multi-centres). **L'API WhatsApp en direct ou via Twilio (8 à 17 TND par mois) peut être incluse dans un plan payant** ou refacturée au réel. Le SMS doit rester une option facturée à part (packs).

---

## 9. Recommandation

### MVP

1. **Reçu PDF à URL signée.** Chaque reçu numéroté est servi sur une URL publique avec un jeton non devinable, par exemple `/r/{token}`. Le même PDF sert à l'impression thermique ou A5 (ticket 10 : caisse et reçus).
2. **Boutons « Envoyer par WhatsApp » en `wa.me`** sur le reçu, sur la liste des impayés et sur la feuille d'appel (absents). SchoolHub construit `https://wa.me/216XXXXXXXX?text=…` avec un texte en FR ou en AR et le lien du reçu.
   - **File d'envoi du jour** avec une case « envoyé », pour tracer les envois et ne pas les oublier.
   - **Coût : 0.** Délai de mise en route : immédiat, sans démarche Meta.
3. **Email automatique en parallèle** quand l'adresse existe : reçu en pièce jointe au parent, copie au centre. Utiliser **Amazon SES** (≈ 0,10 $ pour 1 000) ou **Brevo gratuit** le temps du pilote.
4. **Abstraction côté .NET**, par exemple un port `INotificationSender` avec ses adaptateurs `WaMeLink`, `Email`, puis plus tard `WhatsAppCloud` et `Sms`. Les modèles de texte doivent être **neutres** (sans promotion), pour pouvoir devenir des modèles « utility » sans réécriture.
5. **Question au centre pilote** : WhatsApp ou Messenger ? Quelle part des parents a un email ? Le centre a-t-il déjà un numéro WhatsApp Business ?

### Plus tard (V2)

1. **API WhatsApp Cloud avec des modèles utility** : reçu avec le PDF en en-tête, relance d'impayé, avis d'absence. **≈ 0,012 TND par message, ≈ 8 TND par mois pour 300 élèves.**
   - **Si les centres peuvent payer Meta par carte** : SchoolHub devient Tech Provider, avec Embedded Signup v4 et **coexistence** pour garder le numéro du centre.
   - **Sinon** : **Twilio** (≈ 17 TND par mois). SchoolHub paie et refacture, et le centre n'a pas besoin de carte.
   - **Dans les deux cas**, faire **vérifier l'entreprise** de chaque centre pour dépasser 250 destinataires par jour.
2. **SMS de secours** par un agrégateur tunisien (WinSMS, eSMS Africa ou BipBip, à comparer sur devis) pour les parents sans WhatsApp et pour les relances urgentes. Sender ID au nom du centre. **Jamais Twilio ou Vonage pour le SMS vers la Tunisie.**
3. **Messenger** seulement si le pilote montre qu'il domine chez les parents, et avec un parcours où le parent écrit d'abord à la Page du centre.

---

## 10. Principales réserves

1. **WhatsApp ou Messenger ?** Le choix de WhatsApp repose sur l'observation terrain et sur les concurrents marocains. **La seule mesure quantitative (Sensor Tower, 2024) montre Messenger devant WhatsApp en Tunisie.** Il faut valider avec le pilote avant d'investir dans l'API.
2. **Paiement à Meta depuis la Tunisie.** Le modèle Tech Provider exige une carte bancaire par centre. La faisabilité avec une carte tunisienne n'a pas été vérifiée **[NON VÉRIFIÉ]**. Un BSP comme Twilio contourne ce problème, pour 0,005 $ de plus par message.
3. **Plafond de 250 destinataires par jour** tant que l'entreprise n'est pas vérifiée. Les documents acceptés pour une entreprise tunisienne n'ont pas été consultés **[NON VÉRIFIÉ]**.
4. **Requalification en marketing.** Un modèle jugé promotionnel coûte 5,6 fois plus (0,0225 $). Les textes doivent rester strictement factuels.
5. **Plafonds anti-spam en test** sur les messages sans réponse, ce qui touche aussi le mode manuel `wa.me`. Seuil et pays inconnus **[NON VÉRIFIÉ]**.
6. **Tarifs Meta révisés chaque trimestre** (1ᵉʳ janvier, avril, juillet, octobre). Prochaine révision possible le 1ᵉʳ janvier 2027.
7. **Prix SMS locaux** : pages commerciales anciennes ou injoignables (BipBip). Les opérateurs ne publient rien. **Il faut demander des devis.**
8. **Temps de secrétariat en mode `wa.me`** (≈ 3,5 h par mois) : c'est une estimation personnelle, à mesurer avec le pilote.
