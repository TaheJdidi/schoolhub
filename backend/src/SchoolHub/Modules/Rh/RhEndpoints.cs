using Microsoft.AspNetCore.Http.HttpResults;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Rh;

/// <summary>RHA-01 à RHA-03 : les adresses des ressources humaines, réservées au propriétaire.</summary>
public static class RhEndpoints
{
    public static IEndpointRouteBuilder MapRessourcesHumaines(this IEndpointRouteBuilder api)
    {
        var employes = api.MapGroup("/employes").RequireAuthorization(Politiques.Proprietaire);
        employes.MapGet("", (int? annee, RessourcesHumaines rh) => rh.ListerAsync(annee));
        employes.MapPost("", async Task<Results<Created<EmployeDecrit>, JsonHttpResult<Avis>>> (EmployeSaisi saisie, RessourcesHumaines rh) =>
        {
            var issue = await rh.AjouterAsync(saisie);
            return issue.Message is null ? TypedResults.Created($"/api/employes/{issue.Resultat!.Id}", issue.Resultat) : ConnexionEndpoints.Refus(issue.Statut, issue.Message);
        });
        employes.MapPut("/{id:guid}", async (Guid id, EmployeSaisi saisie, RessourcesHumaines rh) => Repondre(await rh.ModifierAsync(id, saisie)));
        employes.MapPost("/{id:guid}/depart", async (Guid id, RessourcesHumaines rh) => Repondre(await rh.ChangerLaPresenceAsync(id, true)));
        employes.MapPost("/{id:guid}/retour", async (Guid id, RessourcesHumaines rh) => Repondre(await rh.ChangerLaPresenceAsync(id, false)));
        employes.MapGet("/{id:guid}/conges", async Task<Results<Ok<List<CongeDecrit>>, JsonHttpResult<Avis>>> (Guid id, RessourcesHumaines rh) =>
            await rh.CongesAsync(id) is { } conges ? TypedResults.Ok(conges) : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, RessourcesHumaines.EmployeIntrouvable));
        employes.MapPost("/{id:guid}/conges", async Task<Results<Created<CongeDecrit>, JsonHttpResult<Avis>>> (Guid id, CongeSaisi saisie, RessourcesHumaines rh) =>
        {
            var issue = await rh.AjouterUnCongeAsync(id, saisie);
            return issue.Message is null ? TypedResults.Created($"/api/employes/{id}/conges", issue.Resultat!) : ConnexionEndpoints.Refus(issue.Statut, issue.Message);
        });

        var conges = api.MapGroup("/conges").RequireAuthorization(Politiques.Proprietaire);
        conges.MapDelete("/{id:guid}", async Task<Results<NoContent, JsonHttpResult<Avis>>> (Guid id, RessourcesHumaines rh) =>
        {
            var issue = await rh.RetirerUnCongeAsync(id);
            return issue.Message is null ? TypedResults.NoContent() : ConnexionEndpoints.Refus(issue.Statut, issue.Message);
        });

        var salaires = api.MapGroup("/salaires").RequireAuthorization(Politiques.Proprietaire);
        salaires.MapGet("", (string? mois, RessourcesHumaines rh) => rh.SalairesAsync(mois));
        salaires.MapPost("", async Task<Results<Ok<SalairesDuMois>, JsonHttpResult<Avis>>> (SalaireSaisi saisie, RessourcesHumaines rh) =>
        {
            var issue = await rh.MarquerUnSalaireAsync(saisie);
            return issue.Message is null ? TypedResults.Ok(issue.Resultat!) : ConnexionEndpoints.Refus(issue.Statut, issue.Message);
        });
        return api;
    }

    private static Results<Ok<EmployeDecrit>, JsonHttpResult<Avis>> Repondre(IssueRh<EmployeDecrit> issue) =>
        issue.Message is null ? TypedResults.Ok(issue.Resultat!) : ConnexionEndpoints.Refus(issue.Statut, issue.Message);
}
