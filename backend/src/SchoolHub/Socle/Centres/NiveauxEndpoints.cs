using Microsoft.AspNetCore.Http.HttpResults;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Socle.Centres;

/// <summary>
/// CEN-03 : les adresses des niveaux. Toute personne connectée lit la liste : la secrétaire en a besoin
/// pour les groupes, les élèves et le formulaire public. Seul le propriétaire la modifie.
/// </summary>
public static class NiveauxEndpoints
{
    public static IEndpointRouteBuilder MapNiveaux(this IEndpointRouteBuilder api)
    {
        var groupe = api.MapGroup("/niveaux");
        groupe.MapGet("", async (NiveauxDuCentre niveaux) => (await niveaux.ListerAsync()).Select(NiveauDuCentre.De).ToList());
        groupe.MapPost("", AjouterAsync).RequireAuthorization(Politiques.Proprietaire);
        groupe.MapPut("/{id:guid}", RenommerAsync).RequireAuthorization(Politiques.Proprietaire);
        groupe.MapDelete("/{id:guid}", RetirerAsync).RequireAuthorization(Politiques.Proprietaire);
        return api;
    }

    private static async Task<Results<Created<NiveauDuCentre>, JsonHttpResult<Avis>>> AjouterAsync(
        NomDuNiveau saisie,
        NiveauxDuCentre niveaux)
    {
        var issue = await niveaux.AjouterAsync(saisie.Nom);
        return issue.Niveau is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Created($"/api/niveaux/{issue.Niveau.Id}", NiveauDuCentre.De(issue.Niveau));
    }

    private static async Task<Results<Ok<NiveauDuCentre>, JsonHttpResult<Avis>>> RenommerAsync(
        Guid id,
        NomDuNiveau saisie,
        NiveauxDuCentre niveaux)
    {
        var issue = await niveaux.RenommerAsync(id, saisie.Nom);
        return issue.Niveau is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Ok(NiveauDuCentre.De(issue.Niveau));
    }

    private static async Task<Results<NoContent, JsonHttpResult<Avis>>> RetirerAsync(Guid id, NiveauxDuCentre niveaux)
    {
        var issue = await niveaux.RetirerAsync(id);
        return issue.Niveau is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.NoContent();
    }
}

/// <summary>Ce que la page envoie pour ajouter ou renommer un niveau.</summary>
public sealed record NomDuNiveau(string? Nom);

/// <summary>Un niveau tel que l'API le décrit : <see cref="DOffice"/> dit s'il vient de la liste tunisienne.</summary>
public sealed record NiveauDuCentre(Guid Id, string Nom, bool DOffice)
{
    public static NiveauDuCentre De(Niveau niveau) => new(niveau.Id, niveau.Nom, niveau.DOffice);
}
