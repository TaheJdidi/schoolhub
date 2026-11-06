using Microsoft.AspNetCore.Http.HttpResults;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Parts;

/// <summary>PRT-01 à PRT-04 : les adresses des parts des professeurs, réservées au propriétaire.</summary>
public static class PartsEndpoints
{
    public static IEndpointRouteBuilder MapParts(this IEndpointRouteBuilder api)
    {
        var parts = api.MapGroup("/parts").RequireAuthorization(Politiques.Proprietaire);
        parts.MapGet("", (PartsDesProfesseurs service) => service.ListerAsync());
        parts.MapPost("/paiement", async Task<Results<Ok<PartsDuCentre>, JsonHttpResult<Avis>>> (PaiementDUnePart saisie, PartsDesProfesseurs service) =>
        {
            var issue = await service.MarquerAsync(saisie);
            return issue.Parts is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Parts);
        });
        parts.MapPost("/avances", async Task<Results<Created<PartsDuCentre>, JsonHttpResult<Avis>>> (AvanceSaisie saisie, PartsDesProfesseurs service) =>
        {
            var issue = await service.AvancerAsync(saisie);
            return issue.Parts is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Created("/api/parts", issue.Parts);
        });

        var professeur = api.MapGroup("/professeurs/{id:guid}/part-du-centre").RequireAuthorization(Politiques.Proprietaire);
        professeur.MapGet("", async Task<Results<Ok<PartDuCentreDUnProfesseur>, JsonHttpResult<Avis>>> (Guid id, PartsDesProfesseurs service) =>
            await service.LirePartAsync(id) is { } part
                ? TypedResults.Ok(part)
                : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, PartsDesProfesseurs.ProfesseurIntrouvable));
        professeur.MapPut("", async Task<Results<Ok<PartDuCentreDUnProfesseur>, JsonHttpResult<Avis>>> (Guid id, PartSaisie saisie, PartsDesProfesseurs service) =>
        {
            var issue = await service.ChangerLaPartAsync(id, saisie);
            return issue.Part is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Part);
        });
        return api;
    }
}
