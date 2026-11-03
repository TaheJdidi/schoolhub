using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Socle.Centres;

/// <summary>
/// CEN-02 : lire et enregistrer les réglages du centre courant. Le centre est une donnée commune, sans filtre
/// automatique : il est toujours lu par l'identifiant du centre courant. Seul le nom est obligatoire.
/// </summary>
public sealed class ParametresDuCentre(SchoolHubDbContext contexte, CentreCourant centreCourant)
{
    private const string NomObligatoire = "Le nom du centre est obligatoire";
    private const string NomTropLong = "Le nom du centre fait au plus 200 caractères";
    private const string TelephonePasValable = "Le téléphone doit avoir 8 chiffres";
    private const string EmailPasValable = "Cet email n'est pas valable";
    private const string AnneeIncomplete = "L'année scolaire doit avoir une date de début et une date de fin";
    private const string FinAvantLeDebut = "La fin de l'année scolaire doit venir après son début";
    private const string AnneeTropLongue = "L'année scolaire dure un an au plus";
    private const string MontantPasValable = "Un montant doit être positif, au millime près";
    private const string CapacitePasValable = "La capacité doit être d'au moins 1 place";
    private const string PartPasValable = "La part du centre va de 0 à 100 %";

    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    public async Task<ReglagesDuCentre> LireAsync() => Reglages(await CentreAsync(), await AnneeScolaireAsync());

    /// <summary>
    /// Enregistre tout le formulaire d'un coup. Les champs sont vérifiés dans l'ordre de la page : le premier refusé
    /// donne la réponse, et rien n'est enregistré.
    /// </summary>
    public async Task<IssueDesReglages> EnregistrerAsync(ReglagesDuCentre saisie)
    {
        var nom = saisie.Nom?.Trim() ?? "";
        if (nom.Length == 0)
            return Refus(NomObligatoire);
        if (nom.Length > Centre.LongueurMaximaleDuNom)
            return Refus(NomTropLong);

        string? telephone = null;
        if (!string.IsNullOrWhiteSpace(saisie.Telephone))
        {
            telephone = TelephoneTunisien.Normaliser(saisie.Telephone);
            if (telephone is null)
                return Refus(TelephonePasValable);
        }

        var email = string.IsNullOrWhiteSpace(saisie.Email) ? null : saisie.Email.Trim();
        if (email is not null && !AdresseEmail.EstValable(email))
            return Refus(EmailPasValable);

        // Une année sans aucune date ne change rien ; avec une seule, elle est refusée.
        (DateOnly Debut, DateOnly Fin)? dates = null;
        if (saisie.AnneeScolaire is { } annee && (annee.Debut is not null || annee.Fin is not null))
        {
            if (annee.Debut is not { } debut || annee.Fin is not { } fin)
                return Refus(AnneeIncomplete);
            if (fin <= debut)
                return Refus(FinAvantLeDebut);
            if (fin >= debut.AddYears(1))
                return Refus(AnneeTropLongue);
            dates = (debut, fin);
        }

        if (!MontantValable(saisie.MontantDeLAssurance) || !MontantValable(saisie.PrixDeLEntreeALaBibliotheque))
            return Refus(MontantPasValable);

        if (saisie.CapaciteDeLaBibliotheque is < 1)
            return Refus(CapacitePasValable);

        if (saisie.PartDuCentreParDefaut is { } part && (part < 0 || part > 100 || decimal.Round(part, 2) != part))
            return Refus(PartPasValable);

        var centre = await CentreAsync();
        centre.Regler(
            nom,
            telephone,
            email,
            saisie.MontantDeLAssurance,
            saisie.PrixDeLEntreeALaBibliotheque,
            saisie.CapaciteDeLaBibliotheque,
            saisie.PartDuCentreParDefaut);

        // Rien ne disparaît : sans dates, l'année enregistrée reste telle quelle.
        var anneeScolaire = await AnneeScolaireAsync();
        if (dates is var (nouveauDebut, nouvelleFin))
        {
            if (anneeScolaire is null)
            {
                anneeScolaire = new AnneeScolaire(nouveauDebut, nouvelleFin);
                contexte.AnneesScolaires.Add(anneeScolaire);
            }
            else
                anneeScolaire.Changer(nouveauDebut, nouvelleFin);
        }

        await contexte.SaveChangesAsync();
        return new IssueDesReglages(Reglages(centre, anneeScolaire), null);
    }

    private Task<Centre> CentreAsync() => contexte.Centres.SingleAsync(c => c.Id == CentreId);

    /// <summary>L'année réglée sur la page : la plus récente du centre (le filtre automatique garde celles du centre courant).</summary>
    private Task<AnneeScolaire?> AnneeScolaireAsync() =>
        contexte.AnneesScolaires.OrderByDescending(a => a.Debut).FirstOrDefaultAsync();

    private static ReglagesDuCentre Reglages(Centre centre, AnneeScolaire? anneeScolaire) => new(
        centre.Nom,
        centre.Telephone is null ? null : TelephoneTunisien.Grouper(centre.Telephone),
        centre.Email,
        anneeScolaire is null ? null : new AnneeScolaireSaisie(anneeScolaire.Debut, anneeScolaire.Fin),
        centre.MontantDeLAssurance,
        centre.PrixDeLEntreeALaBibliotheque,
        centre.CapaciteDeLaBibliotheque,
        centre.PartDuCentreParDefaut);

    /// <summary>Un montant vide, ou de 0 à moins d'un million de dinars, au millime près.</summary>
    private static bool MontantValable(decimal? montant) =>
        montant is not { } m || (m >= 0 && m < 1_000_000 && decimal.Round(m, 3) == m);

    private static IssueDesReglages Refus(string message) => new(null, message);
}

/// <summary>
/// Les réglages du centre, tels que la page les montre et les envoie. Un réglage vide vaut <c>null</c> ;
/// le téléphone est renvoyé groupé (« 71 234 567 »).
/// </summary>
public sealed record ReglagesDuCentre(
    string? Nom,
    string? Telephone,
    string? Email,
    AnneeScolaireSaisie? AnneeScolaire,
    decimal? MontantDeLAssurance,
    decimal? PrixDeLEntreeALaBibliotheque,
    int? CapaciteDeLaBibliotheque,
    decimal? PartDuCentreParDefaut);

/// <summary>Les deux dates de l'année scolaire : elles se saisissent ensemble.</summary>
public sealed record AnneeScolaireSaisie(DateOnly? Debut, DateOnly? Fin);

/// <summary>Ce que l'enregistrement a donné : les réglages enregistrés, ou le message du premier champ refusé.</summary>
public sealed record IssueDesReglages(ReglagesDuCentre? Reglages, string? Refus);
