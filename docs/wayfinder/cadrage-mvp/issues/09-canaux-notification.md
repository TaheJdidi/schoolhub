# 09. Canaux de notification en Tunisie

Type: research
Mode: AFK
Status: resolved
Blocked by: —

## Question

Par quel canal envoyer reçus, rappels d'impayés et absences aux parents et aux élèves en Tunisie, et pour quel coût ?

- **WhatsApp** : liens `wa.me` pré-remplis (gratuits, envoi manuel) ou WhatsApp Business Platform (prix des messages « utility » en Tunisie depuis le 1ᵉʳ octobre 2026, modèles de messages, conditions).
- **SMS** : fournisseurs (opérateurs, agrégateurs), prix par message, API disponibles.
- **Email** : usage réel chez les parents.
- **Recommandation pour le MVP**, avec un coût mensuel estimé pour un centre de 300 élèves.

Les résultats vont dans `research/`.

## Contexte

- [Étude de l'existant](../../../../research/etude-de-l-existant.md), section 8, rang 1 : WhatsApp est le canal réel ; l'email seul ne suffit pas.

## Réponse

Résolu le 28/10/2026 par un agent de recherche. Rapport complet, avec ses sources : [research/canaux-notification.md](../../../../research/canaux-notification.md).

**Recommandation pour le MVP** : liens `wa.me` pré-remplis, envoyés à la main depuis le WhatsApp du centre, plus un email automatique quand le parent en a un.

- Chaque message contient un **lien vers le reçu PDF** (URL avec jeton non devinable), car un lien `wa.me` ne peut pas porter de pièce jointe.
- Une **file d'envoi du jour** avec une case « envoyé » pour les reçus, les absences et les relances.
- L'**email**, via Amazon SES ou l'offre gratuite de Brevo, porte le PDF en pièce jointe, avec copie au centre.
- Coût : **0 TND**, plus environ 3,5 h de secrétariat par mois pour 300 élèves (estimation à mesurer).
- Côté .NET, prévoir une abstraction d'envoi (wa.me, email, puis API WhatsApp et SMS). Les textes doivent rester neutres pour devenir plus tard des modèles WhatsApp « utility ».

**Plus tard (V2)** :

- **API WhatsApp Cloud**, avec des modèles « utility » : environ 8 TND par mois pour 300 élèves en direct chez Meta, ou environ 17 TND via Twilio si les centres ne peuvent pas payer Meta par carte.
- **SMS de secours** par un agrégateur tunisien (WinSMS, eSMS Africa) : environ 28 à 129 TND HT par mois. Jamais Twilio ni Vonage pour le SMS, à environ 900-1 000 TND par mois.

**Faits clés** :

- La Tunisie est dans la zone tarifaire Meta « Rest of Africa » : 0,004 $ par message utility et 0,0225 $ par message marketing (grille du 1ᵉʳ octobre 2026).
- Sans vérification de l'entreprise, un compte ne peut écrire qu'à 250 destinataires par jour.

**Réserve principale** : la seule mesure trouvée (Sensor Tower, 2024) place **Messenger devant WhatsApp** en Tunisie, avec environ 6,3 M d'utilisateurs actifs contre 2,3 M. Messenger ne permet pas d'écrire à un numéro de téléphone : le parent doit d'abord écrire à la Page du centre. Le choix du canal est donc à confirmer avec le centre pilote, dans [Caisse et reçus](10-caisse-recus.md).
