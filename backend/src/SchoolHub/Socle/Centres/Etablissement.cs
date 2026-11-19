using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Socle.Centres;

/// <summary>
/// Une école, un collège ou un lycée du quartier, que le formulaire public propose dans sa liste « Établissement
/// scolaire ». Chaque centre a sa liste, vide au départ. La fiche d'un élève garde le nom écrit, pas ce lien : retirer un
/// établissement de la liste ne change aucune fiche.
/// </summary>
public sealed class Etablissement : IAppartientAuCentre
{
    /// <summary>La taille de la colonne du nom, celle de l'établissement sur la fiche d'un élève.</summary>
    public const int LongueurMaximaleDuNom = 200;

    internal Etablissement(string nom)
    {
        Nom = nom;
        NomNormalise = Niveau.Normaliser(nom);
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public string Nom { get; private set; }

    /// <summary>Le nom en minuscules : deux établissements d'un centre ne peuvent pas avoir le même.</summary>
    public string NomNormalise { get; private set; }
}

internal sealed class EtablissementConfiguration : IEntityTypeConfiguration<Etablissement>
{
    public void Configure(EntityTypeBuilder<Etablissement> etablissement)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        etablissement.ToTable("etablissements");
        etablissement.Property(e => e.Nom).HasMaxLength(Etablissement.LongueurMaximaleDuNom);
        etablissement.Property(e => e.NomNormalise).HasMaxLength(Etablissement.LongueurMaximaleDuNom);
        etablissement.HasIndex(e => new { e.CentreId, e.NomNormalise }).IsUnique();
    }
}

/// <summary>La liste des établissements du centre courant, que le propriétaire remplit dans les paramètres.</summary>
public sealed class EtablissementsDuCentre(SchoolHubDbContext contexte)
{
    private const string NomObligatoire = "Le nom de l'établissement est obligatoire";
    private const string NomTropLong = "Le nom de l'établissement fait au plus 200 caractères";
    private const string ExisteDeja = "Cet établissement est déjà dans la liste";
    private const string Introuvable = "Établissement introuvable";

    /// <summary>Par nom, dans l'ordre alphabétique.</summary>
    public Task<List<Etablissement>> ListerAsync() => contexte.Etablissements.OrderBy(e => e.Nom).ToListAsync();

    public async Task<IssueSurUnEtablissement> AjouterAsync(string? saisie)
    {
        var nom = saisie?.Trim() ?? "";
        if (nom.Length == 0)
            return IssueSurUnEtablissement.Refusee(StatusCodes.Status400BadRequest, NomObligatoire);
        if (nom.Length > Etablissement.LongueurMaximaleDuNom || Niveau.Normaliser(nom).Length > Etablissement.LongueurMaximaleDuNom)
            return IssueSurUnEtablissement.Refusee(StatusCodes.Status400BadRequest, NomTropLong);
        var normalise = Niveau.Normaliser(nom);
        if (await contexte.Etablissements.AnyAsync(e => e.NomNormalise == normalise))
            return IssueSurUnEtablissement.Refusee(StatusCodes.Status409Conflict, ExisteDeja);

        var etablissement = new Etablissement(nom);
        contexte.Etablissements.Add(etablissement);
        try
        {
            await contexte.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return IssueSurUnEtablissement.Refusee(StatusCodes.Status409Conflict, ExisteDeja);
        }

        return IssueSurUnEtablissement.Reussie(etablissement, StatusCodes.Status201Created);
    }

    /// <summary>Retire un établissement de la liste ; les fiches qui le portent gardent son nom.</summary>
    public async Task<IssueSurUnEtablissement> RetirerAsync(Guid id)
    {
        var etablissement = await contexte.Etablissements.SingleOrDefaultAsync(e => e.Id == id);
        if (etablissement is null)
            return IssueSurUnEtablissement.Refusee(StatusCodes.Status404NotFound, Introuvable);

        contexte.Etablissements.Remove(etablissement);
        await contexte.SaveChangesAsync();
        return IssueSurUnEtablissement.Reussie(etablissement, StatusCodes.Status204NoContent);
    }
}

/// <summary>Ce qu'une action sur un établissement a donné : l'établissement et le statut HTTP, ou un refus.</summary>
public sealed record IssueSurUnEtablissement(Etablissement? Etablissement, int Statut, string? Message)
{
    public static IssueSurUnEtablissement Reussie(Etablissement etablissement, int statut) => new(etablissement, statut, null);

    public static IssueSurUnEtablissement Refusee(int statut, string message) => new(null, statut, message);
}

/// <summary>
/// Les adresses des établissements : toute personne connectée lit la liste, seul le propriétaire la change.
/// Le formulaire public la reçoit avec le reste du formulaire du centre.
/// </summary>
public static class EtablissementsEndpoints
{
    public static IEndpointRouteBuilder MapEtablissements(this IEndpointRouteBuilder api)
    {
        var groupe = api.MapGroup("/etablissements");
        groupe.MapGet("", async (EtablissementsDuCentre etablissements) =>
            (await etablissements.ListerAsync()).Select(EtablissementDuCentre.De).ToList());
        groupe.MapPost("", AjouterAsync).RequireAuthorization(Politiques.Proprietaire);
        groupe.MapDelete("/{id:guid}", RetirerAsync).RequireAuthorization(Politiques.Proprietaire);
        return api;
    }

    private static async Task<Results<Created<EtablissementDuCentre>, JsonHttpResult<Avis>>> AjouterAsync(
        NomDeLEtablissement saisie,
        EtablissementsDuCentre etablissements)
    {
        var issue = await etablissements.AjouterAsync(saisie.Nom);
        return issue.Etablissement is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Created($"/api/etablissements/{issue.Etablissement.Id}", EtablissementDuCentre.De(issue.Etablissement));
    }

    private static async Task<Results<NoContent, JsonHttpResult<Avis>>> RetirerAsync(Guid id, EtablissementsDuCentre etablissements)
    {
        var issue = await etablissements.RetirerAsync(id);
        return issue.Etablissement is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.NoContent();
    }
}

/// <summary>Ce que la page envoie pour ajouter un établissement.</summary>
public sealed record NomDeLEtablissement(string? Nom);

/// <summary>Un établissement tel que l'API le décrit.</summary>
public sealed record EtablissementDuCentre(Guid Id, string Nom)
{
    public static EtablissementDuCentre De(Etablissement etablissement) => new(etablissement.Id, etablissement.Nom);
}
