namespace SchoolHub.Socle.Centres;

/// <summary>
/// Établissement client qui utilise SchoolHub. L'équipe SchoolHub le crée (CEN-01), puis son propriétaire
/// le règle (CEN-02). Seul le nom est connu au départ : les autres réglages restent vides tant qu'il ne les a pas saisis.
/// </summary>
public sealed class Centre(string nom, string code)
{
    /// <summary>La taille de la colonne du nom.</summary>
    public const int LongueurMaximaleDuNom = 200;

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string Nom { get; private set; } = nom;

    /// <summary>Code du lien public du formulaire d'inscription, par exemple « les-jasmins ».</summary>
    public string Code { get; private set; } = code;

    /// <summary>Les 8 chiffres du téléphone, sans espaces ni indicatif : « 71234567 ».</summary>
    public string? Telephone { get; private set; }

    public string? Email { get; private set; }

    /// <summary>L'assurance annuelle d'un élève d'Étude, en dinars au millime.</summary>
    public decimal? MontantDeLAssurance { get; private set; }

    /// <summary>Le prix d'une entrée à la journée à la bibliothèque, en dinars au millime.</summary>
    public decimal? PrixDeLEntreeALaBibliotheque { get; private set; }

    /// <summary>Le nombre de places de la bibliothèque, s'il est limité.</summary>
    public int? CapaciteDeLaBibliotheque { get; private set; }

    /// <summary>Le pourcentage que le centre garde par défaut sur le montant d'un groupe, par exemple 5.</summary>
    public decimal? PartDuCentreParDefaut { get; private set; }

    /// <summary>
    /// Les réglages saisis par le propriétaire, déjà vérifiés. Seule la valeur actuelle est gardée :
    /// ce qui devient dû recopie la valeur du moment (exigences communes, « Ce qui change en cours d'année »).
    /// </summary>
    public void Regler(
        string nom,
        string? telephone,
        string? email,
        decimal? montantDeLAssurance,
        decimal? prixDeLEntreeALaBibliotheque,
        int? capaciteDeLaBibliotheque,
        decimal? partDuCentreParDefaut)
    {
        Nom = nom;
        Telephone = telephone;
        Email = email;
        MontantDeLAssurance = montantDeLAssurance;
        PrixDeLEntreeALaBibliotheque = prixDeLEntreeALaBibliotheque;
        CapaciteDeLaBibliotheque = capaciteDeLaBibliotheque;
        PartDuCentreParDefaut = partDuCentreParDefaut;
    }
}
