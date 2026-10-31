# 04. Parcours d'inscription d'un élève

Type: grilling
Mode: HITL
Status: resolved
Blocked by: —

## Question

Comment un élève devient-il inscrit au centre ?

- **Canal** : formulaire public (lien ou QR code) qui crée une demande validée par la secrétaire, saisie directe au guichet, ou email avec un objet précis comme dans le cahier des charges ?
- **« Préciser les noms de ses amis »** : à quoi cela sert-il ? Les placer dans le même groupe, une remise de groupe, du parrainage ?
- **Informations obligatoires** : contact du parent pour un mineur, niveau scolaire, établissement. Quelles données ne demande-t-on pas (minimisation) ?
- **Groupe plein** : capacité maximale par groupe, liste d'attente par niveau et matière ?

## Contexte

- Remarque « l'inscription par email est fragile » dans [cadrage.md](../../../cadrage.md).
- [Étude de l'existant](../../../../research/etude-de-l-existant.md), section 8 (inscription en ligne, liste d'attente).
- [Rôles, comptes et accès](03-roles-comptes-acces.md), Q10 : seul le propriétaire accorde un prix spécial ; la secrétaire inscrit l'élève au prix normal et le prévient.

## Avancement

> Grilling terminé le 30/10/2026 : toutes les questions sont tranchées, et la synthèse est dans « Réponse », en bas du ticket. [CONTEXT.md](../../../../CONTEXT.md) est à jour.

### Décisions prises (tour 1, 30/10/2026)

- **Q1. Deux canaux** : au guichet, où la secrétaire saisit l'élève, et par un formulaire public (lien ou QR code). Dans le formulaire, l'élève peut citer d'autres élèves qui suivront le même groupe que lui.
- **Q2. Ces noms servent à placer les amis dans le même groupe** : la secrétaire les voit quand elle choisit le groupe. Pas de règle automatique, pas de remise.
- **Q3. Informations obligatoires** : nom et prénom, niveau scolaire (la même liste que pour les groupes), téléphone WhatsApp d'un parent, téléphone et email de l'élève. *Retenu faute d'objection : l'email du parent et l'établissement scolaire restent facultatifs ; on ne demande jamais la CIN, l'adresse, la date de naissance ni la photo ; le niveau de l'élève se met à jour à chaque rentrée.*
- **Q4. Pas de rattachement des fratries ni de second contact parent**, pour le moment.
- **Q5. Capacité facultative par groupe** : la plateforme prévient quand elle est atteinte, sans bloquer.
- **Q6. Le formulaire ne crée pas d'élève.** Le centre reçoit une **demande d'inscription**, puis la secrétaire enregistre l'élève à la main. Le seul contrôle : à l'ajout dans un groupe, la plateforme vérifie que l'élève n'y est pas déjà inscrit. *Retenu faute d'objection : un élève garde une seule fiche d'une année sur l'autre.*

### Décisions prises (tour 2, 30/10/2026)

- **Q7. Téléphone et email de l'élève obligatoires à partir du collège** (7ᵉ année de base), facultatifs avant. Pour un adulte, la secrétaire coche « élève majeur », et le téléphone du parent devient facultatif.
- **Q8. Le formulaire** demande les informations de Q3, les matières souhaitées et, pour chacune, le groupe préféré parmi ceux du niveau de l'élève (professeur et horaire), puis les amis. Chaque centre a son propre lien et son QR code.
- **Q9. Les demandes arrivent dans une liste « Demandes d'inscription ».** La fiche de l'élève est pré-remplie à partir de la demande ; la secrétaire vérifie, choisit le groupe et enregistre, ou refuse une demande fausse ou en double. Un message WhatsApp confirme ensuite au parent le groupe et la date de la première séance.
- **Q10. Pas de liste d'attente** : les demandes non traitées en tiennent lieu, filtrables par niveau et par matière.

### Questions du tour 2 (répondues le 30/10/2026)

- **Q7. Téléphone et email de l'élève pour les plus jeunes et les adultes** : un enfant de primaire n'a souvent ni téléphone ni email, et un adulte en Formation n'a pas de parent à prévenir. ➡️ Téléphone et email de l'élève obligatoires à partir du collège (7ᵉ année de base), facultatifs avant. Pour un adulte, la secrétaire coche « élève majeur », et le téléphone du parent devient facultatif.
- **Q8. Contenu du formulaire** : ➡️ les informations de Q3, les matières souhaitées et, pour chacune, le groupe préféré parmi ceux de son niveau (professeur et horaire), puis les amis. Chaque centre a son propre lien et son QR code.
- **Q9. Traitement d'une demande** : ➡️ elle arrive dans une liste « Demandes d'inscription » de la plateforme. Quand la secrétaire l'ouvre, la fiche de l'élève est pré-remplie ; elle vérifie, choisit le groupe et enregistre. Elle peut aussi refuser une demande fausse ou en double. Après l'enregistrement, la plateforme prépare un message WhatsApp pour confirmer au parent le groupe et la date de la première séance.
- **Q10. Liste d'attente** quand le groupe voulu est plein : (a) pas de liste d'attente, les demandes non traitées en tiennent lieu, filtrables par niveau et matière ; (b) une liste d'attente par groupe, dans l'ordre d'arrivée. ➡️ (a) : la capacité ne bloque pas, c'est la secrétaire qui juge.

## Réponse

Résolu le 30/10/2026, par grilling. Le détail des échanges est dans « Avancement » ci-dessus ; le vocabulaire est dans [CONTEXT.md](../../../../CONTEXT.md).

### Canaux

- **Au guichet** : la secrétaire saisit l'élève et l'inscrit dans un groupe.
- **Par un formulaire public**, propre à chaque centre (lien et QR code), sans compte. Il crée une **demande d'inscription**, pas un élève.
- Pas d'inscription par email.

### Le formulaire

- Il demande les informations obligatoires (ci-dessous), les matières souhaitées et, pour chacune, le groupe préféré parmi ceux du niveau de l'élève (professeur et horaire).
- L'élève peut citer les amis qui suivront le même groupe que lui. La secrétaire voit ces noms quand elle choisit le groupe ; pas de règle automatique, pas de remise.

### Traitement d'une demande

- Les demandes arrivent dans une liste « Demandes d'inscription ». En ouvrant une demande, la secrétaire trouve la fiche de l'élève pré-remplie ; elle vérifie, choisit le groupe et enregistre. Elle peut refuser une demande fausse ou en double.
- Après l'enregistrement, la plateforme prépare un message WhatsApp qui confirme au parent le groupe et la date de la première séance.
- Pas de liste d'attente : les demandes non traitées en tiennent lieu, filtrables par niveau et par matière.

### Informations sur l'élève

- **Obligatoires** : nom et prénom, niveau scolaire (la même liste que pour les groupes), téléphone WhatsApp d'un parent, et téléphone et email de l'élève à partir du collège (7ᵉ année de base).
- **Élève majeur**, souvent en Formation : le téléphone du parent devient facultatif.
- **Facultatifs** : téléphone et email de l'élève avant le collège, email du parent, établissement scolaire.
- **Jamais demandés** : CIN, adresse, date de naissance, photo.
- Un seul contact parent, et pas de lien entre frères et sœurs, pour le moment.
- Une seule fiche par élève d'une année sur l'autre ; son niveau se met à jour à chaque rentrée.

### Contrôles

- **Pas de double inscription dans un même groupe** : la plateforme le vérifie à l'ajout.
- **Capacité facultative par groupe** : la plateforme prévient quand elle est atteinte, sans bloquer.

### Renvoyé à d'autres tickets

- **[Caisse et reçus](10-caisse-recus.md)** : encaisser un parent qui paie pour deux enfants, sans lien entre eux.
- **[Maquette des écrans clés](11-maquette-ecrans-cles.md)** : formulaire public et liste des demandes.
- **[Architecture technique](12-architecture-technique.md)** : lien public par centre, protection contre les envois abusifs.

### Plus tard

- Lien entre frères et sœurs, et second contact parent.
- Liste d'attente par groupe.

### Écarté

- Inscription par email avec un objet précis, comme le prévoyait le cahier des charges.
