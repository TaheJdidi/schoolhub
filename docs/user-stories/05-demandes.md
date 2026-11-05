# 05. Demandes d'inscription (DEM)

Un élève peut s'inscrire au guichet (ELV-03) ou par le formulaire public du centre. Le formulaire crée une **demande d'inscription**, que la secrétaire enregistre ensuite à la main ([ticket 04](../wayfinder/cadrage-mvp/issues/04-parcours-inscription-eleve.md)).

### DEM-01 · Remplir le formulaire public, sans compte

En tant qu'élève ou parent, je veux demander une inscription depuis mon téléphone, sans créer de compte, afin de ne pas devoir passer au centre.

**Critères d'acceptation**

- Le formulaire s'ouvre à l'adresse `…/inscription/les-jasmins`, sans connexion.
- Il demande les mêmes informations obligatoires et facultatives que la fiche élève (ELV-02), dont la case « L'élève est majeur ».
- L'élève choisit ses matières puis, pour chacune, le groupe qu'il préfère parmi les groupes ouverts de son niveau, avec le professeur et l'horaire. Les Formations sans niveau sont proposées à tous.
- Il peut citer les amis qui suivront le même groupe que lui (facultatif).
- Le formulaire ne montre ni les noms des autres élèves, ni le nombre d'inscrits.
- Après l'envoi : « Demande envoyée. Merci Adem ! Le centre vous confirmera le groupe et la date de la première séance sur WhatsApp. »
- Plus de 5 demandes enregistrées en une heure depuis la même adresse sont refusées (une demande refusée ne compte pas, mais 30 essais au plus), et un champ piège invisible arrête les robots.

Maquette : formulaire public d'inscription.

### DEM-02 · Afficher et imprimer le lien public et son QR code

En tant que secrétaire, je veux le lien et le QR code du formulaire, afin de les afficher au guichet et de les partager sur la page Facebook du centre.

**Critères d'acceptation**

- Une page montre le lien public du centre, un bouton pour le copier, et son QR code.
- « Imprimer l'affiche » donne une page A4 avec le nom du centre, le QR code et le lien.
- Le QR code mène bien au formulaire du centre.

Maquette : Demandes d'inscription → Le lien public du centre.

### DEM-03 · Voir les demandes, filtrées par niveau et par matière

En tant que secrétaire, je veux voir toutes les demandes à traiter, afin de n'en oublier aucune.

**Critères d'acceptation**

- La liste « Demandes d'inscription » montre les demandes à traiter, les plus anciennes en premier, avec leur nombre dans le menu et sur l'accueil.
- Elle se filtre par niveau et par matière : ces filtres tiennent lieu de liste d'attente.
- « Doublon possible » s'affiche quand un élève ou une autre demande porte le même nom ou le même téléphone.
- Les demandes déjà enregistrées ou refusées restent consultables dans « Traitées ».

Maquette : Demandes d'inscription.

### DEM-04 · Enregistrer une demande comme élève inscrit

En tant que secrétaire, je veux transformer une demande en élève inscrit sans tout retaper, afin de gagner du temps.

**Critères d'acceptation**

- En ouvrant la demande, la secrétaire trouve la fiche de l'élève pré-remplie. Elle vérifie, puis choisit le groupe de chaque matière : le groupe préféré est proposé, et les amis cités sont affichés.
- Si l'élève a déjà une fiche, elle choisit cette fiche au lieu d'en créer une autre.
- Les règles de l'inscription s'appliquent (ELV-03) : pas de double inscription, avertissement de capacité.
- Après l'enregistrement, la demande passe à « Inscrit », et la plateforme prépare le message WhatsApp de confirmation pour les [destinataires](00-exigences-communes.md#messages) de l'élève : groupe, professeur, date et heure de la première séance.

Maquette : Demandes d'inscription → une demande.

### DEM-05 · Refuser une demande fausse ou en double

En tant que secrétaire, je veux refuser une demande fausse ou en double, afin de garder une liste propre.

**Critères d'acceptation**

- « Refuser » demande le motif, « Doublon » ou « Fausse demande », puis une confirmation.
- La demande sort des demandes à traiter et reste dans « Traitées », avec son motif.
- Aucun message ne part vers la personne.

Maquette : Demandes d'inscription → Refuser.
