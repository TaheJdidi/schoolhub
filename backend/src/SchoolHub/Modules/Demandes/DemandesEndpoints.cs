using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Emails;

namespace SchoolHub.Modules.Demandes;

/// <summary>DEM-01 et DEM-02 : le formulaire public, ouvert à tous, et son lien, pour les personnes connectées.</summary>
public static class DemandesEndpoints
{
    /// <summary>La limite d'essais du formulaire public : 30 envois par heure et par adresse, acceptés ou refusés (DEM-01).</summary>
    public const string LimiteDuFormulaire = "formulaire-public";

    /// <summary>
    /// La protection du formulaire public contre les envois abusifs : 5 demandes enregistrées par heure et par adresse
    /// (<see cref="LimiteDesDemandes"/>), et 30 essais, pour arrêter un robot qui n'envoie que des demandes fausses.
    /// Derrière l'App Service d'Azure, l'adresse du visiteur est la dernière de « X-Forwarded-For », ajoutée par Azure : un
    /// visiteur ne peut pas se faire passer pour un autre en écrivant cet en-tête lui-même.
    /// </summary>
    public static IServiceCollection AjouterLaProtectionDuFormulaire(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        services.AddSingleton<LimiteDesDemandes>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (contexte, annulation) =>
                new ValueTask(contexte.HttpContext.Response.WriteAsJsonAsync(new Avis(FormulairePublic.TropDEnvois), annulation));
            options.AddPolicy(LimiteDuFormulaire, contexte => RateLimitPartition.GetFixedWindowLimiter(
                contexte.Connection.RemoteIpAddress?.ToString() ?? "inconnue",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
        });
        return services;
    }

    public static IEndpointRouteBuilder MapDemandes(this IEndpointRouteBuilder api)
    {
        var formulaire = api.MapGroup("/public/inscription").AllowAnonymous();
        formulaire.MapGet("/{code}", LireAsync);
        formulaire.MapPost("/{code}", EnvoyerAsync).RequireRateLimiting(LimiteDuFormulaire);

        // DEM-02 : le lien et son QR code, pour toute personne connectée du centre.
        var demandes = api.MapGroup("/demandes");
        demandes.MapGet("/lien", async (SchoolHubDbContext contexte, CentreCourant centre, IOptions<OptionsDeLApplication> options) =>
            new LienPublic(await LienAsync(contexte, centre, options.Value)));
        demandes.MapGet("/qr-code", async (SchoolHubDbContext contexte, CentreCourant centre, IOptions<OptionsDeLApplication> options) =>
        {
            using var generateur = new QRCodeGenerator();
            using var donnees = generateur.CreateQrCode(await LienAsync(contexte, centre, options.Value), QRCodeGenerator.ECCLevel.Q);
            return TypedResults.File(new PngByteQRCode(donnees).GetGraphic(12), "image/png");
        });
        return api;
    }

    /// <summary>« https://app.schoolhub.tn/inscription/les-jasmins » : l'adresse de l'application et le code du centre.</summary>
    private static async Task<string> LienAsync(SchoolHubDbContext contexte, CentreCourant centre, OptionsDeLApplication options)
    {
        var id = centre.Id ?? throw new InvalidOperationException("Aucun centre courant.");
        var code = await contexte.Centres.Where(c => c.Id == id).Select(c => c.Code).SingleAsync();
        return $"{options.Adresse.TrimEnd('/')}/inscription/{code}";
    }

    private static async Task<Results<Ok<FormulaireDuCentre>, JsonHttpResult<Avis>>> LireAsync(string code, FormulairePublic formulaire) =>
        await formulaire.LireAsync(code) is { } trouve
            ? TypedResults.Ok(trouve)
            : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, "Ce lien d'inscription n'existe pas");

    private static async Task<Results<Created<DemandeRecue>, JsonHttpResult<Avis>>> EnvoyerAsync(
        string code,
        DemandeEnvoyee envoi,
        FormulairePublic formulaire,
        HttpContext requete)
    {
        var issue = await formulaire.EnvoyerAsync(code, envoi, requete.Connection.RemoteIpAddress?.ToString() ?? "inconnue");
        return issue.Recue is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Created((string?)null, issue.Recue);
    }
}

/// <summary>Un niveau proposé par le formulaire ; « dOffice » sert à la règle de la 7ᵉ année, comme sur la fiche élève.</summary>
public sealed record NiveauDuFormulaire(Guid Id, string Nom, bool DOffice);

/// <summary>Un groupe proposé : la matière, le niveau (vide pour une Formation ouverte à tous), le type, le professeur et l'horaire.</summary>
public sealed record GroupeDuFormulaire(Guid Id, string Matiere, Guid? NiveauId, string Type, string Professeur, List<CreneauDuGroupe> Creneaux);

/// <summary>Le formulaire public d'un centre : son nom, son année scolaire (« 2027-2028 »), ses niveaux et ses groupes.</summary>
public sealed record FormulaireDuCentre(string Centre, string? Annee, List<NiveauDuFormulaire> Niveaux, List<GroupeDuFormulaire> Groupes);

/// <summary>
/// Ce que le formulaire envoie : la fiche de l'élève, son groupe préféré pour chaque matière, les amis cités, et le champ
/// piège <see cref="SiteWeb"/>, invisible, qu'un robot remplit.
/// </summary>
public sealed record DemandeEnvoyee(
    string? Prenom,
    string? Nom,
    Guid? NiveauId,
    bool Majeur,
    string? TelephoneParent,
    string? EmailParent,
    string? TelephoneEleve,
    string? EmailEleve,
    string? Etablissement,
    List<Guid>? Groupes,
    string? Amis,
    string? SiteWeb)
{
    /// <summary>La fiche de l'élève, vérifiée par les règles d'ELV-02.</summary>
    public FicheSaisieDeLEleve Fiche => new(Prenom, Nom, NiveauId, Majeur, TelephoneParent, EmailParent, TelephoneEleve, EmailEleve, Etablissement);
}

/// <summary>DEM-02 : le lien public du formulaire du centre.</summary>
public sealed record LienPublic(string Lien);

/// <summary>La réponse d'un envoi : le prénom, pour « Demande envoyée. Merci Adem ! ».</summary>
public sealed record DemandeRecue(string Prenom);
