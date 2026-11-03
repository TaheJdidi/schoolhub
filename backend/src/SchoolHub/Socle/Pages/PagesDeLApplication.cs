using Microsoft.AspNetCore.StaticFiles;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Socle.Pages;

/// <summary>
/// Le serveur sert aussi les pages Angular, à la même adresse que l'API (ADR 0003).
/// Les fichiers d'Angular ne contiennent aucune donnée : ils sont publics. Toute adresse de page renvoie
/// <c>index.html</c>, et c'est Angular qui choisit la page à afficher, y compris celles des liens des emails.
/// </summary>
public static class PagesDeLApplication
{
    public const string AdresseInconnue = "Adresse inconnue";

    private const string PageParDefaut = "index.html";

    /// <summary>
    /// À appeler avant <c>UtiliserLaSession</c> : sinon la session refuserait les fichiers,
    /// et la page de connexion elle-même répondrait 401.
    /// </summary>
    public static IApplicationBuilder UtiliserLesFichiersDeLApplication(this IApplicationBuilder app)
    {
        // Aucune page ne transmet son adresse à un autre site, en particulier celles qui contiennent un lien d'email.
        app.Use((contexte, suite) =>
        {
            contexte.Response.Headers["Referrer-Policy"] = "no-referrer";
            return suite(contexte);
        });

        app.UseStaticFiles(OptionsDesFichiers());
        return app;
    }

    /// <summary>À appeler après les adresses de l'API, qui passent avant ces adresses de repli.</summary>
    public static WebApplication MapPagesDeLApplication(this WebApplication app, RouteGroupBuilder api)
    {
        // Une adresse de l'API inconnue répond en { "message" }, et pas avec la page Angular.
        api.MapFallback(() => TypedResults.Json(new Avis(AdresseInconnue), statusCode: StatusCodes.Status404NotFound))
            .AllowAnonymous();

        // Sans AllowAnonymous, la politique par défaut, qui exige d'être connecté, répondrait 401.
        app.MapFallbackToFile(PageParDefaut, OptionsDesFichiers()).AllowAnonymous();
        return app;
    }

    private static StaticFileOptions OptionsDesFichiers()
    {
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".webmanifest"] = "application/manifest+json";

        return new StaticFileOptions
        {
            ContentTypeProvider = types,
            // Une mise à jour du soir doit être vue dès le lendemain : index.html n'est jamais gardé en cache.
            // Les autres fichiers changent de nom à chaque version : ils peuvent y rester.
            OnPrepareResponse = fichier =>
            {
                if (fichier.File.Name == PageParDefaut)
                    fichier.Context.Response.Headers.CacheControl = "no-cache";
            },
        };
    }
}
