using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Abonnements;

/// <summary>
/// EDI-05 : un centre Suspendu est en lecture seule. Toute requête d'écriture (POST, PUT, PATCH, DELETE) d'un compte de ce
/// centre est refusée avec un message clair, quelle que soit l'adresse : un module qui arrive plus tard est protégé d'office.
/// Restent permis ce qui sert à se connecter et à se déconnecter, et les adresses marquées
/// <see cref="PermiseEnLectureSeule{TBuilder}"/>.
/// </summary>
public static class LectureSeule
{
    public const string Message =
        "Votre abonnement est suspendu : SchoolUp est en lecture seule. Contactez l'équipe SchoolUp pour le réactiver.";

    /// <summary>Les adresses d'écriture qu'un centre Suspendu garde.</summary>
    private static readonly string[] Permises =
    [
        "/api/connexion",
        "/api/deconnexion",
        "/api/invitation",
        "/api/mot-de-passe-oublie",
        "/api/nouveau-mot-de-passe",
        // Le propriétaire d'un centre Suspendu peut avoir besoin d'aide (EDI-07).
        "/api/abonnement/support",
    ];

    /// <summary>Après la session (le compte et son centre sont connus) : avant l'adresse elle-même.</summary>
    public static IApplicationBuilder UtiliserLaLectureSeule(this IApplicationBuilder app) =>
        app.Use(async (http, suite) =>
        {
            if (EstUneEcriture(http.Request.Method)
                && http.User.FindFirst(RevendicationsDuCompte.Centre)?.Value is { } centre
                && !Permises.Any(adresse => http.Request.Path.StartsWithSegments(adresse))
                && http.GetEndpoint()?.Metadata.GetMetadata<LectureEnPost>() is null)
            {
                var services = http.RequestServices;
                var situation = await AbonnementCoteCentre.SituationAsync(
                    services.GetRequiredService<SchoolHubDbContext>(), Guid.Parse(centre), services.GetRequiredService<TimeProvider>(), http.RequestAborted);
                if (situation.LectureSeule)
                {
                    http.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await http.Response.WriteAsJsonAsync(new Avis(Message), http.RequestAborted);
                    return;
                }
            }

            await suite(http);
        });

    /// <summary>
    /// Une adresse en POST qui ne modifie rien des données du centre : renvoyer un reçu par email, par exemple.
    /// Un centre Suspendu continue de s'en servir.
    /// </summary>
    public static TBuilder PermiseEnLectureSeule<TBuilder>(this TBuilder adresse) where TBuilder : IEndpointConventionBuilder =>
        adresse.WithMetadata(new LectureEnPost());

    private static bool EstUneEcriture(string methode) =>
        HttpMethods.IsPost(methode) || HttpMethods.IsPut(methode) || HttpMethods.IsPatch(methode) || HttpMethods.IsDelete(methode);
}

/// <summary>La marque d'une adresse en POST qui ne modifie rien (<see cref="LectureSeule.PermiseEnLectureSeule{TBuilder}"/>).</summary>
public sealed class LectureEnPost;
