using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// Branche ASP.NET Core Identity derrière nos propres adresses d'API : hachage des mots de passe,
/// blocage après 5 essais faux, et session dans un cookie protégé de 12 heures sans activité.
/// </summary>
public static class ConfigurationDesComptes
{
    public const string MotDePasseTropCourt = "Le mot de passe doit faire au moins 8 caractères";

    public const int LongueurMinimaleDuMotDePasse = 8;

    public static IServiceCollection AjouterLesComptes(this IServiceCollection services)
    {
        services
            .AddIdentityCore<Compte>(options =>
            {
                // Au moins 8 caractères, et c'est tout : la story ne demande ni chiffre, ni majuscule, ni symbole.
                options.Password.RequiredLength = LongueurMinimaleDuMotDePasse;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                // L'email sert de nom d'utilisateur : tous ses caractères sont permis.
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "";
            })
            .AddEntityFrameworkStores<SchoolHubDbContext>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<RevendicationsDuCompte>();

        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "schoolhub";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;

                // 12 heures sans activité : la validation renouvelle le cookie à chaque requête,
                // et pas seulement à mi-parcours comme le ferait l'expiration glissante d'ASP.NET.
                options.ExpireTimeSpan = TimeSpan.FromHours(12);
                options.SlidingExpiration = false;

                options.Events.OnValidatePrincipal = ValidationDeLaSession.ValiderAsync;

                // L'API répond sans rediriger : c'est l'application Angular qui affiche la page de connexion.
                options.Events.OnRedirectToLogin = contexte =>
                {
                    contexte.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = contexte =>
                {
                    contexte.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return contexte.Response.WriteAsJsonAsync(new Avis("Accès réservé au propriétaire"));
                };
            });

        // Le cookie lit l'horloge de l'application, que les tests remplacent.
        services
            .AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<TimeProvider>((options, temps) => options.TimeProvider = temps);

        services
            .AddAuthorizationBuilder()
            // Toute adresse exige d'être connecté, sauf celles marquées AllowAnonymous : une adresse oubliée est protégée d'office.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Politiques.Proprietaire, politique => politique.RequireRole(nameof(RoleDuCompte.Proprietaire)));

        return services;
    }

    /// <summary>
    /// La session de la requête : rien de l'API n'est gardé en cache, puis le cookie est lu
    /// et les droits sont vérifiés.
    /// </summary>
    public static IApplicationBuilder UtiliserLaSession(this IApplicationBuilder app)
    {
        // Après « Se déconnecter », le bouton « retour » du navigateur ne montre plus aucune donnée.
        app.Use((contexte, suite) =>
        {
            if (contexte.Request.Path.StartsWithSegments("/api"))
                contexte.Response.Headers.CacheControl = "no-store";
            return suite(contexte);
        });

        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}

/// <summary>Les politiques d'accès, à poser sur les adresses réservées.</summary>
public static class Politiques
{
    /// <summary>Réservé au propriétaire : une secrétaire reçoit 403 « Accès réservé au propriétaire ».</summary>
    public const string Proprietaire = "Proprietaire";
}
