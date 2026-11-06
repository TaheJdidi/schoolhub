using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Achats;

/// <summary>RHA-04 à RHA-06 : les adresses des achats, pour toute personne connectée du centre.</summary>
public static class AchatsEndpoints
{
    public static IEndpointRouteBuilder MapAchats(this IEndpointRouteBuilder api)
    {
        var achats = api.MapGroup("/achats");
        achats.MapGet("", async (string? mois, int? annee, string? categorie, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, AchatsDuCentre service) =>
            await service.ListerAsync(await AuteurAsync(utilisateur, comptes), mois, annee, categorie));
        achats.MapPost("", async Task<Results<Created<AchatDecrit>, JsonHttpResult<Avis>>> (
            AchatSaisi saisie, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, AchatsDuCentre service) =>
        {
            var issue = await service.AjouterAsync(await AuteurAsync(utilisateur, comptes), saisie);
            return issue.Achat is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Created($"/api/achats/{issue.Achat.Id}", issue.Achat);
        });
        achats.MapPut("/{id:guid}", async (Guid id, AchatSaisi saisie, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, AchatsDuCentre service) =>
            Repondre(await service.ModifierAsync(await AuteurAsync(utilisateur, comptes), id, saisie)));
        achats.MapDelete("/{id:guid}", async Task<Results<NoContent, JsonHttpResult<Avis>>> (
            Guid id, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, AchatsDuCentre service) =>
        {
            var issue = await service.SupprimerAsync(await AuteurAsync(utilisateur, comptes), id);
            return issue.Message is null ? TypedResults.NoContent() : ConnexionEndpoints.Refus(issue.Statut, issue.Message);
        });

        // La facture arrive en multipart (champ « facture »). Le cookie de session est SameSite=Strict : un formulaire
        // d'un autre site n'arrive pas connecté.
        achats.MapPut("/{id:guid}/facture", async (Guid id, HttpRequest requete, ClaimsPrincipal utilisateur, UserManager<Compte> comptes,
            AchatsDuCentre service) =>
        {
            var fichier = requete.HasFormContentType ? (await requete.ReadFormAsync()).Files.GetFile("facture") : null;
            return Repondre(await service.JoindreLaFactureAsync(await AuteurAsync(utilisateur, comptes), id, fichier));
        });
        achats.MapGet("/{id:guid}/facture", async Task<Results<FileContentHttpResult, JsonHttpResult<Avis>>> (Guid id, AchatsDuCentre service) =>
            await service.FactureAsync(id) is { } facture
                ? TypedResults.File(facture.Contenu, facture.Type, facture.Nom)
                : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, "Facture introuvable"));
        achats.MapDelete("/{id:guid}/facture", async (Guid id, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, AchatsDuCentre service) =>
            Repondre(await service.RetirerLaFactureAsync(await AuteurAsync(utilisateur, comptes), id)));
        return api;
    }

    private static Results<Ok<AchatDecrit>, JsonHttpResult<Avis>> Repondre(IssueDeLAchat issue) =>
        issue.Achat is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Achat);

    private static async Task<Compte> AuteurAsync(ClaimsPrincipal utilisateur, UserManager<Compte> comptes) =>
        await comptes.GetUserAsync(utilisateur) ?? throw new InvalidOperationException("Aucun compte connecté.");
}
