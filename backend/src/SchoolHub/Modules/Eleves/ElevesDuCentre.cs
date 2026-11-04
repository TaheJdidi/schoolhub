using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Eleves;

/// <summary>
/// ELV-02 et ELV-11 : les élèves du centre courant. Le filtre automatique ne montre que ceux du centre : un élève ou un
/// niveau d'un autre centre est « introuvable ». Un homonyme ou un téléphone en commun prévient, sans bloquer.
/// </summary>
public sealed class ElevesDuCentre(SchoolHubDbContext contexte)
{
    /// <summary>Le rang du premier niveau d'office où l'élève a son propre téléphone et son email : la 7ᵉ année de base.</summary>
    private const int RangDeLa7eAnnee = 7;

    private const string PrenomObligatoire = "Le prénom est obligatoire";
    private const string PrenomTropLong = "Le prénom fait au plus 100 caractères";
    private const string NomObligatoire = "Le nom est obligatoire";
    private const string NomTropLong = "Le nom fait au plus 100 caractères";
    private const string ChoisirLeNiveau = "Choisissez le niveau";
    private const string NiveauIntrouvable = "Niveau introuvable";
    private const string TelephoneEleveObligatoire = "Le téléphone de l'élève est obligatoire à partir de la 7ᵉ année de base";
    private const string TelephoneElevePasValable = "Le téléphone de l'élève doit avoir 8 chiffres";
    private const string EmailEleveObligatoire = "L'email de l'élève est obligatoire à partir de la 7ᵉ année de base";
    private const string EmailElevePasValable = "L'email de l'élève n'est pas valable";
    private const string TelephoneParentObligatoire = "Le téléphone du parent est obligatoire";
    private const string TelephoneParentPasValable = "Le téléphone du parent doit avoir 8 chiffres";
    private const string EmailParentPasValable = "L'email du parent n'est pas valable";
    private const string EtablissementTropLong = "L'établissement fait au plus 200 caractères";
    private const string Introuvable = "Élève introuvable";

    /// <summary>Le nombre de résultats d'une recherche : assez pour une page, jamais toute une base.</summary>
    public const int ResultatsAuPlus = 100;

    /// <summary>
    /// ELV-01 : les élèves du centre, par nom puis prénom, avec le nom de leur niveau. Avec une recherche (100 résultats
    /// au plus) : une saisie faite de chiffres cherche dans les deux téléphones (« 22 345 », « +216 98-401-220 ») ; sinon chaque mot, sans
    /// accents ni majuscules, doit se trouver dans le prénom ou le nom (« miled adem », « hedi »).
    /// </summary>
    public async Task<List<EleveTrouve>> ListerAsync(string? recherche)
    {
        var requete = contexte.Eleves.AsQueryable();
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            // Un numéro collé tel quel (« +216 98-401-220 », ou copié de WhatsApp) se nettoie comme à l'enregistrement.
            if (TelephoneTunisien.Chiffres(recherche) is { } chiffres)
            {
                requete = requete.Where(e => (e.TelephoneParent != null && e.TelephoneParent.Contains(chiffres))
                    || (e.TelephoneEleve != null && e.TelephoneEleve.Contains(chiffres)));
            }
            else
            {
                foreach (var mot in Eleve.Normaliser(recherche).Split(' '))
                    requete = requete.Where(e => e.NomDeRecherche.Contains(mot));
            }
        }

        var niveaux = await contexte.Niveaux.ToDictionaryAsync(n => n.Id, n => n.Nom);
        var eleves = await requete.OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ThenBy(e => e.Id)
            .Take(string.IsNullOrWhiteSpace(recherche) ? int.MaxValue : ResultatsAuPlus)
            .ToListAsync();
        return eleves.Select(e => new EleveTrouve(e.Id, e.Prenom, e.Nom, niveaux[e.NiveauId])).ToList();
    }

    public async Task<IssueSurUnEleve> AjouterAsync(FicheSaisieDeLEleve saisie)
    {
        var (coordonnees, refus) = await VerifierAsync(saisie);
        if (refus is not null)
            return refus;

        var avertissement = await AvertissementAsync(coordonnees!, sauf: null);
        var eleve = new Eleve(coordonnees!);
        contexte.Eleves.Add(eleve);
        await contexte.SaveChangesAsync();
        return new IssueSurUnEleve(await FicheAsync(eleve), StatusCodes.Status201Created, null, avertissement);
    }

    public async Task<FicheDeLEleve?> LireAsync(Guid id)
    {
        var eleve = await contexte.Eleves.SingleOrDefaultAsync(e => e.Id == id);
        return eleve is null ? null : await FicheAsync(eleve);
    }

    public async Task<IssueSurUnEleve> ModifierAsync(Guid id, FicheSaisieDeLEleve saisie)
    {
        var eleve = await contexte.Eleves.SingleOrDefaultAsync(e => e.Id == id);
        if (eleve is null)
            return EleveIntrouvable;

        var (coordonnees, refus) = await VerifierAsync(saisie);
        if (refus is not null)
            return refus;

        // L'élève lui-même ne compte pas : garder son nom et ses téléphones ne fait pas un doublon.
        var avertissement = await AvertissementAsync(coordonnees!, sauf: id);
        eleve.Modifier(coordonnees!);
        await contexte.SaveChangesAsync();
        return new IssueSurUnEleve(await FicheAsync(eleve), StatusCodes.Status200OK, null, avertissement);
    }

    /// <summary>
    /// ELV-11 : supprime une fiche créée par erreur. Il n'y a encore ni inscription, ni paiement, ni présence :
    /// chaque étape qui en ajoute refusera de supprimer une fiche qui a un historique.
    /// </summary>
    public async Task<IssueSurUnEleve> SupprimerAsync(Guid id)
    {
        var eleve = await contexte.Eleves.SingleOrDefaultAsync(e => e.Id == id);
        if (eleve is null)
            return EleveIntrouvable;

        contexte.Eleves.Remove(eleve);
        await contexte.SaveChangesAsync();
        return new IssueSurUnEleve(null, StatusCodes.Status204NoContent, null);
    }

    public static IssueSurUnEleve EleveIntrouvable => new(null, StatusCodes.Status404NotFound, Introuvable);

    /// <summary>Les champs dans l'ordre du formulaire, sans les espaces autour : le premier refusé donne la réponse.</summary>
    private async Task<(Coordonnees? Coordonnees, IssueSurUnEleve? Refus)> VerifierAsync(FicheSaisieDeLEleve saisie)
    {
        var prenom = saisie.Prenom?.Trim() ?? "";
        var nom = saisie.Nom?.Trim() ?? "";
        // La forme sans accents peut s'allonger (une syllabe coréenne donne 3 lettres) : elle doit tenir dans sa colonne aussi.
        if (prenom.Length == 0)
            return Refus(PrenomObligatoire);
        if (prenom.Length > Eleve.LongueurMaximaleDuNom || Eleve.Normaliser(prenom).Length > Eleve.LongueurMaximaleDuNom)
            return Refus(PrenomTropLong);
        if (nom.Length == 0)
            return Refus(NomObligatoire);
        if (nom.Length > Eleve.LongueurMaximaleDuNom || Eleve.Normaliser(nom).Length > Eleve.LongueurMaximaleDuNom)
            return Refus(NomTropLong);

        if (saisie.NiveauId is not { } niveauId)
            return Refus(ChoisirLeNiveau);
        var niveau = await contexte.Niveaux.SingleOrDefaultAsync(n => n.Id == niveauId);
        if (niveau is null)
            return (null, new IssueSurUnEleve(null, StatusCodes.Status404NotFound, NiveauIntrouvable));
        // À partir de la 7ᵉ année de base, et pour un niveau ajouté (université, adultes), l'élève a ses propres contacts.
        var sesPropresContacts = !niveau.DOffice || niveau.Rang >= RangDeLa7eAnnee;

        var (telephoneEleve, refusDuTelephoneEleve) = LireUnTelephone(saisie.TelephoneEleve, sesPropresContacts,
            TelephoneEleveObligatoire, TelephoneElevePasValable);
        if (refusDuTelephoneEleve is not null)
            return Refus(refusDuTelephoneEleve);
        var (emailEleve, refusDeLEmailEleve) = LireUnEmail(saisie.EmailEleve, sesPropresContacts, EmailEleveObligatoire, EmailElevePasValable);
        if (refusDeLEmailEleve is not null)
            return Refus(refusDeLEmailEleve);

        var (telephoneParent, refusDuTelephoneParent) = LireUnTelephone(saisie.TelephoneParent, !saisie.Majeur,
            TelephoneParentObligatoire, TelephoneParentPasValable);
        if (refusDuTelephoneParent is not null)
            return Refus(refusDuTelephoneParent);
        var (emailParent, refusDeLEmailParent) = LireUnEmail(saisie.EmailParent, obligatoire: false, "", EmailParentPasValable);
        if (refusDeLEmailParent is not null)
            return Refus(refusDeLEmailParent);

        var etablissement = string.IsNullOrWhiteSpace(saisie.Etablissement) ? null : saisie.Etablissement.Trim();
        if (etablissement?.Length > Eleve.LongueurMaximaleDeLEtablissement)
            return Refus(EtablissementTropLong);

        return (new Coordonnees(prenom, nom, niveauId, saisie.Majeur, telephoneParent, emailParent, telephoneEleve, emailEleve, etablissement), null);
    }

    private static (string? Telephone, string? Refus) LireUnTelephone(string? saisie, bool obligatoire, string vide, string pasValable)
    {
        if (string.IsNullOrWhiteSpace(saisie))
            return (null, obligatoire ? vide : null);
        var chiffres = TelephoneTunisien.Normaliser(saisie);
        return chiffres is null ? (null, pasValable) : (chiffres, null);
    }

    private static (string? Email, string? Refus) LireUnEmail(string? saisie, bool obligatoire, string vide, string pasValable)
    {
        if (string.IsNullOrWhiteSpace(saisie))
            return (null, obligatoire ? vide : null);
        var email = saisie.Trim();
        return AdresseEmail.EstValable(email) ? (email, null) : (null, pasValable);
    }

    /// <summary>
    /// « Doublon possible : Adem Miled (9ᵉ année de base) » : un autre élève du centre au même prénom et au même nom
    /// (sans accents ni majuscules), ou qui partage un téléphone, celui de l'élève ou du parent, dans un sens ou dans l'autre.
    /// </summary>
    private async Task<string?> AvertissementAsync(Coordonnees coordonnees, Guid? sauf)
    {
        var nomDeRecherche = Eleve.Normaliser($"{coordonnees.Prenom} {coordonnees.Nom}");
        var telephones = new[] { coordonnees.TelephoneParent, coordonnees.TelephoneEleve }.OfType<string>().ToList();
        var doublons = await contexte.Eleves
            .Where(e => e.Id != sauf
                && (e.NomDeRecherche == nomDeRecherche
                    || (e.TelephoneParent != null && telephones.Contains(e.TelephoneParent))
                    || (e.TelephoneEleve != null && telephones.Contains(e.TelephoneEleve))))
            .OrderBy(e => e.Nom).ThenBy(e => e.Prenom)
            .Join(contexte.Niveaux, e => e.NiveauId, n => n.Id, (e, n) => e.Prenom + " " + e.Nom + " (" + n.Nom + ")")
            .ToListAsync();
        return doublons.Count == 0 ? null : $"Doublon possible : {string.Join(", ", doublons)}";
    }

    private async Task<FicheDeLEleve> FicheAsync(Eleve eleve)
    {
        var niveau = await contexte.Niveaux.SingleAsync(n => n.Id == eleve.NiveauId);
        return FicheDeLEleve.De(eleve, niveau);
    }

    private static (Coordonnees?, IssueSurUnEleve?) Refus(string message) =>
        (null, new IssueSurUnEleve(null, StatusCodes.Status400BadRequest, message));
}

/// <summary>Ce qu'une action sur un élève a donné : la fiche et le statut HTTP, avec l'avertissement ; ou un refus.</summary>
public sealed record IssueSurUnEleve(FicheDeLEleve? Fiche, int Statut, string? Message, string? Avertissement = null);
