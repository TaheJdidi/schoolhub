using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// CPT-01 : se connecter, se déconnecter, et savoir qui est connecté.
/// </summary>
public static class ConnexionEndpoints
{
    private const string Incorrect = "Email ou mot de passe incorrect";
    private const string TropDEssais = "Trop d'essais : réessayez dans 15 minutes";
    private const string CompteDesactive = "Ce compte est désactivé : adressez-vous au propriétaire du centre";

    public static IEndpointRouteBuilder MapConnexion(this IEndpointRouteBuilder api)
    {
        api.MapPost("/connexion", SeConnecterAsync).AllowAnonymous();

        // Pas SignInManager.SignOutAsync : il déconnecte aussi des schémas externes, non enregistrés ici, et lève une exception.
        api.MapPost("/deconnexion", async (HttpContext http) =>
        {
            await http.SignOutAsync(IdentityConstants.ApplicationScheme);
            return TypedResults.NoContent();
        });

        api.MapGet("/moi", QuiEstConnecteAsync);
        return api;
    }

    private static async Task<Results<Ok<Moi>, JsonHttpResult<Avis>>> SeConnecterAsync(
        DemandeDeConnexion demande,
        UserManager<Compte> comptes,
        SignInManager<Compte> connexion,
        SchoolHubDbContext contexte)
    {
        var email = demande.Email?.Trim();
        if (string.IsNullOrEmpty(email) || demande.MotDePasse is null)
            return Refus(StatusCodes.Status401Unauthorized, Incorrect);

        // L'email est normalisé par Identity : les majuscules ne comptent pas.
        var compte = await comptes.FindByEmailAsync(email);
        if (compte is null)
            return Refus(StatusCodes.Status401Unauthorized, Incorrect);

        // Identity compte les essais faux de suite : au 5e, le compte est bloqué 15 minutes.
        var resultat = await connexion.CheckPasswordSignInAsync(compte, demande.MotDePasse, lockoutOnFailure: true);
        if (resultat.IsLockedOut)
            return Refus(StatusCodes.Status401Unauthorized, TropDEssais);
        if (!resultat.Succeeded)
            return Refus(StatusCodes.Status401Unauthorized, Incorrect);

        // L'état n'est regardé qu'après le bon mot de passe : le message « désactivé » ne révèle rien.
        if (compte.Etat == EtatDuCompte.Desactive)
            return Refus(StatusCodes.Status403Forbidden, CompteDesactive);
        if (compte.Etat != EtatDuCompte.Actif)
            return Refus(StatusCodes.Status401Unauthorized, Incorrect);

        await OuvrirLaSessionAsync(connexion, compte);
        return TypedResults.Ok(await MoiAsync(compte, contexte));
    }

    private static async Task<Results<Ok<Moi>, UnauthorizedHttpResult>> QuiEstConnecteAsync(
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        SchoolHubDbContext contexte)
    {
        var compte = await comptes.GetUserAsync(utilisateur);
        return compte is null ? TypedResults.Unauthorized() : TypedResults.Ok(await MoiAsync(compte, contexte));
    }

    /// <summary>Ce que l'API dit de la personne connectée, avec le nom de son centre.</summary>
    internal static async Task<Moi> MoiAsync(Compte compte, SchoolHubDbContext contexte)
    {
        var centre = await contexte.Centres
            .Where(c => c.Id == compte.CentreId)
            .Select(c => c.Nom)
            .SingleAsync();
        return new Moi(compte.Prenom, compte.Nom, compte.Role.ToString(), centre);
    }

    /// <summary>
    /// Pose le cookie de session. Il est persistant : il garde sa date d'expiration
    /// même si le navigateur est fermé, et la session dure 12 heures sans activité.
    /// </summary>
    internal static Task OuvrirLaSessionAsync(SignInManager<Compte> connexion, Compte compte) =>
        connexion.SignInAsync(compte, isPersistent: true);

    /// <summary>Un refus de l'API : le statut HTTP, et le message en français.</summary>
    internal static JsonHttpResult<Avis> Refus(int statut, string message) =>
        TypedResults.Json(new Avis(message), statusCode: statut);
}

/// <summary>Un message de l'API, en français : <c>{ "message": "…" }</c>.</summary>
public sealed record Avis(string Message);

/// <summary>Qui est connecté : de quoi accueillir la personne et adapter le menu à son rôle.</summary>
public sealed record Moi(string Prenom, string Nom, string Role, string Centre);

/// <summary>Ce que la page de connexion envoie. Un champ vide reçoit le refus habituel.</summary>
public sealed record DemandeDeConnexion(string? Email, string? MotDePasse);
