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

        // LST-03 et LST-04 : les modèles, et l'import d'un modèle rempli (multipart, champ « fichier »). Le cookie de
        // session est SameSite=Strict : un formulaire d'un autre site n'arrive pas connecté.
        var imports = api.MapGroup("/imports");
        imports.MapGet("/eleves/modele", async (ImportsDesListes service) =>
            TypedResults.File(await service.ModeleDesElevesAsync(), Classeur.TypeMime, "modele-eleves.xlsx"));
        imports.MapGet("/professeurs/modele", () => TypedResults.File(ImportsDesListes.ModeleDesProfesseurs(), Classeur.TypeMime, "modele-professeurs.xlsx"));
        imports.MapPost("/eleves", async (HttpRequest requete, ImportsDesListes service) =>
            Repondre(await LireAsync(requete) is { } flux ? await service.ImporterDesElevesAsync(flux) : ResultatDImport.Refuse([ImportsDesListes.FichierPasValable])));
        imports.MapPost("/professeurs", async (HttpRequest requete, ImportsDesListes service) =>
            Repondre(await LireAsync(requete) is { } flux ? await service.ImporterDesProfesseursAsync(flux) : ResultatDImport.Refuse([ImportsDesListes.FichierPasValable])));

        // LST-05 : la même action pour plusieurs élèves sélectionnés.
        var selection = api.MapGroup("/selection");
        selection.MapPost("/inscription", async (InscriptionDeLaSelection saisie, SelectionDEleves service) => Repondre(await service.InscrireAsync(saisie)));
        selection.MapPost("/niveau", async (NiveauDeLaSelection saisie, SelectionDEleves service) => Repondre(await service.ChangerLeNiveauAsync(saisie)));
        return api;
    }

    private static Results<Ok<ResultatDeLaSelection>, JsonHttpResult<Avis>> Repondre(IssueDeLaSelection issue) =>
        issue.Resultat is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Resultat);

    /// <summary>Le fichier envoyé, en mémoire (10 Mo au plus) ; <c>null</c> sans fichier.</summary>
    private static async Task<Stream?> LireAsync(HttpRequest requete)
    {
        if (!requete.HasFormContentType)
            return null;
        var fichier = (await requete.ReadFormAsync()).Files.GetFile("fichier");
        if (fichier is null || fichier.Length == 0 || fichier.Length > 10 * 1024 * 1024)
            return null;
        var flux = new MemoryStream();
        await fichier.CopyToAsync(flux);
        flux.Position = 0;
        return flux;
    }

    /// <summary>200 avec le résultat ; 400 avec les erreurs à corriger (rien n'est importé).</summary>
    private static IResult Repondre(ResultatDImport resultat) => resultat.Importe ? Results.Ok(resultat) : Results.BadRequest(resultat);

    private static FileContentHttpResult Fichier(ExportsDesListes.Fichier fichier) => TypedResults.File(fichier.Contenu, Classeur.TypeMime, fichier.Nom);

    /// <summary>« a,b,c » : les élèves sélectionnés (LST-05).</summary>
    private static List<Guid>? LireLesIds(string? ids) =>
        string.IsNullOrWhiteSpace(ids) ? null : ids.Split(',').Select(i => Guid.TryParse(i, out var id) ? id : Guid.Empty).Where(i => i != Guid.Empty).ToList();
}
