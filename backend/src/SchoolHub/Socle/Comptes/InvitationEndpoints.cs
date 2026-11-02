using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// CPT-02 : le lien d'invitation est-il valable, et choisir son mot de passe depuis ce lien.
/// Les deux adresses sont ouvertes sans connexion : la personne n'a pas encore de mot de passe.
/// </summary>
public static class InvitationEndpoints
{
    private const string LienExpire = "Ce lien a expiré : demandez au propriétaire une nouvelle invitation";

    public static IEndpointRouteBuilder MapInvitation(this IEndpointRouteBuilder api)
    {
        api.MapGet("/invitation", AccueillirAsync).AllowAnonymous();
        api.MapPost("/invitation", ChoisirLeMotDePasseAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<Accueil>, JsonHttpResult<Avis>>> AccueillirAsync(
        string? compte,
        string? jeton,
        UserManager<Compte> comptes,
        SchoolHubDbContext contexte)
    {
        var invite = await VerifierLeLienAsync(comptes, compte, jeton);
        if (invite is null)
            return ConnexionEndpoints.Refus(StatusCodes.Status410Gone, LienExpire);

        var moi = await ConnexionEndpoints.MoiAsync(invite, contexte);
        return TypedResults.Ok(new Accueil(moi.Prenom, moi.Centre));
    }

    private static async Task<Results<Ok<Moi>, JsonHttpResult<Avis>>> ChoisirLeMotDePasseAsync(
        ChoixDuMotDePasse choix,
        UserManager<Compte> comptes,
        SignInManager<Compte> connexion,
        SchoolHubDbContext contexte)
    {
        var invite = await VerifierLeLienAsync(comptes, choix.Compte, choix.Jeton);
        if (invite is null)
            return ConnexionEndpoints.Refus(StatusCodes.Status410Gone, LienExpire);

        if (choix.MotDePasse is null || choix.MotDePasse.Length < ConfigurationDesComptes.LongueurMinimaleDuMotDePasse)
            return ConnexionEndpoints.Refus(StatusCodes.Status400BadRequest, ConfigurationDesComptes.MotDePasseTropCourt);

        // L'enregistrement du mot de passe change le tampon de sécurité du compte : le lien ne sert plus.
        invite.Activer();
        var resultat = await comptes.AddPasswordAsync(invite, choix.MotDePasse);
        if (!resultat.Succeeded)
            // La longueur est déjà vérifiée : il reste le cas où ce même lien vient d'être utilisé par un autre envoi simultané.
            return ConnexionEndpoints.Refus(StatusCodes.Status410Gone, LienExpire);

        await ConnexionEndpoints.OuvrirLaSessionAsync(connexion, invite);
        return TypedResults.Ok(await ConnexionEndpoints.MoiAsync(invite, contexte));
    }

    /// <summary>
    /// Le compte invité à qui le lien est destiné, ou <c>null</c> si le lien ne vaut plus rien :
    /// compte inconnu, compte qui n'est plus invité, ou jeton faux, expiré, déjà utilisé ou abîmé.
    /// C'est la seule vérification, partagée par l'accueil et le choix du mot de passe.
    /// </summary>
    private static async Task<Compte?> VerifierLeLienAsync(UserManager<Compte> comptes, string? compte, string? jeton)
    {
        if (!Guid.TryParse(compte, out var id))
            return null;

        var invite = await comptes.FindByIdAsync(id.ToString());
        if (invite is null || invite.Etat != EtatDuCompte.Invite)
            return null;

        var valable = await comptes.VerifyUserTokenAsync(invite, JetonDInvitation.Nom, JetonDInvitation.But, jeton ?? "");
        return valable ? invite : null;
    }
}

/// <summary>Ce que la page d'invitation affiche pour accueillir la personne : son prénom et son centre.</summary>
public sealed record Accueil(string Prenom, string Centre);

/// <summary>Ce que la page d'invitation envoie : le lien reçu par email (compte et jeton) et le mot de passe choisi.</summary>
public sealed record ChoixDuMotDePasse(string? Compte, string? Jeton, string? MotDePasse);
