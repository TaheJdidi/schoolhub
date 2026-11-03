using Microsoft.AspNetCore.Http.HttpResults;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Socle.Centres;

/// <summary>CEN-02 : les adresses de la page « Paramètres du centre », réservées au propriétaire.</summary>
public static class ParametresEndpoints
{
    public static IEndpointRouteBuilder MapParametres(this IEndpointRouteBuilder api)
    {
        var groupe = api.MapGroup("/centre/parametres").RequireAuthorization(Politiques.Proprietaire);
        groupe.MapGet("", (ParametresDuCentre parametres) => parametres.LireAsync());
        groupe.MapPut("", EnregistrerAsync);
        return api;
    }

    private static async Task<Results<Ok<ReglagesDuCentre>, JsonHttpResult<Avis>>> EnregistrerAsync(
        ReglagesDuCentre saisie,
        ParametresDuCentre parametres)
    {
        var issue = await parametres.EnregistrerAsync(saisie);
        return issue.Reglages is null
            ? ConnexionEndpoints.Refus(StatusCodes.Status400BadRequest, issue.Refus!)
            : TypedResults.Ok(issue.Reglages);
    }
}
