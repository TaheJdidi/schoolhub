using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Listes;

/// <summary>LST-02 : les adresses des exports Excel, chacune avec les filtres de sa liste.</summary>
public static class ListesEndpoints
{
    public static IEndpointRouteBuilder MapListes(this IEndpointRouteBuilder api)
    {
        var exports = api.MapGroup("/exports");
        exports.MapGet("/eleves", async (string? recherche, string? niveau, string? ids, ExportsDesListes service) =>
            Fichier(await service.ElevesAsync(recherche, niveau, LireLesIds(ids))));
        exports.MapGet("/professeurs", async (bool? partis, ExportsDesListes service) => Fichier(await service.ProfesseursAsync(partis == true)));
        exports.MapGet("/groupes/{id:guid}", async Task<Results<FileContentHttpResult, JsonHttpResult<Avis>>> (Guid id, ExportsDesListes service) =>
            await service.GroupeAsync(id) is { } fichier ? Fichier(fichier) : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, "Groupe introuvable"));
        exports.MapGet("/recus", async (DateOnly? jour, int? numero, string? eleve, ExportsDesListes service) =>
            Fichier(await service.RecusAsync(jour, numero, eleve)));
        exports.MapGet("/bibliotheque", async (DateOnly? jour, ExportsDesListes service) => Fichier(await service.BibliothequeAsync(jour)));
        exports.MapGet("/achats", async (string? mois, int? annee, string? categorie, ClaimsPrincipal utilisateur, UserManager<Compte> comptes,
            ExportsDesListes service) =>
        {
            var lecteur = await comptes.GetUserAsync(utilisateur) ?? throw new InvalidOperationException("Aucun compte connecté.");
            return Fichier(await service.AchatsAsync(lecteur, mois, annee, categorie));
        });
        // La secrétaire ne peut pas exporter les employés.
        exports.MapGet("/employes", async (ExportsDesListes service) => Fichier(await service.EmployesAsync())).RequireAuthorization(Politiques.Proprietaire);
        return api;
    }

    private static FileContentHttpResult Fichier(ExportsDesListes.Fichier fichier) => TypedResults.File(fichier.Contenu, Classeur.TypeMime, fichier.Nom);

    /// <summary>« a,b,c » : les élèves sélectionnés (LST-05).</summary>
    private static List<Guid>? LireLesIds(string? ids) =>
        string.IsNullOrWhiteSpace(ids) ? null : ids.Split(',').Select(i => Guid.TryParse(i, out var id) ? id : Guid.Empty).Where(i => i != Guid.Empty).ToList();
}
