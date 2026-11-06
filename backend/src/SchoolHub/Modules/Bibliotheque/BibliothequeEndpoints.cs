using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Bibliotheque;

/// <summary>BIB-01 à BIB-03 : les adresses de la bibliothèque, pour toute personne connectée du centre.</summary>
public static class BibliothequeEndpoints
{
    public static IEndpointRouteBuilder MapBibliotheque(this IEndpointRouteBuilder api)
    {
        var groupe = api.MapGroup("/bibliotheque");
        groupe.MapGet("", (DateOnly? jour, BibliothequeDuCentre bibliotheque) => bibliotheque.JourneeAsync(jour));
        groupe.MapPost("/entrees", async Task<Results<Created<EntreeEnregistree>, JsonHttpResult<Avis>>> (
            EntreeSaisie saisie, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, BibliothequeDuCentre bibliotheque) =>
        {
            var auteur = await comptes.GetUserAsync(utilisateur) ?? throw new InvalidOperationException("Aucun compte connecté.");
            var issue = await bibliotheque.EntrerAsync(auteur, saisie);
            return issue.Enregistree is null
                ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
                : TypedResults.Created("/api/bibliotheque", issue.Enregistree);
        });
        groupe.MapPut("/entrees/{id:guid}/depart", async Task<Results<Ok<EntreeDecrite>, JsonHttpResult<Avis>>> (
            Guid id, DepartSaisi saisie, BibliothequeDuCentre bibliotheque) =>
        {
            var issue = await bibliotheque.NoterLeDepartAsync(id, saisie);
            return issue.Entree is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Entree);
        });
        return api;
    }
}
