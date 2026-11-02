using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Emails;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// CPT-03 : demander un lien de mot de passe oublié, puis choisir un nouveau mot de passe depuis ce lien.
/// Les deux adresses sont ouvertes sans connexion : la personne ne peut plus se connecter.
/// </summary>
public static class MotDePasseOublieEndpoints
{
    private const string LienEnvoye = "Si un compte existe pour cet email, un lien vient de partir";
    private const string LienExpire = "Ce lien a expiré : refaites une demande de mot de passe oublié";

    public static IEndpointRouteBuilder MapMotDePasseOublie(this IEndpointRouteBuilder api)
    {
        api.MapPost("/mot-de-passe-oublie", DemanderUnLienAsync).AllowAnonymous();
        api.MapPost("/nouveau-mot-de-passe", ChangerLeMotDePasseAsync).AllowAnonymous();
        return api;
    }

    /// <summary>
    /// Envoie le lien si le compte existe et est actif. La réponse est toujours la même : elle ne dit pas
    /// si l'email est connu, ce qui permettrait à n'importe qui de vérifier qui a un compte.
    /// </summary>
    private static async Task<Ok<Avis>> DemanderUnLienAsync(
        DemandeDeLien demande,
        UserManager<Compte> comptes,
        IEnvoiEmail emails,
        IOptions<OptionsDeLApplication> application,
        ILoggerFactory journaux)
    {
        var reponse = TypedResults.Ok(new Avis(LienEnvoye));

        var email = demande.Email?.Trim();
        if (string.IsNullOrEmpty(email))
            return reponse;

        // Un compte désactivé n'a pas de lien (la story le dit), un compte invité a déjà son lien d'invitation.
        var compte = await comptes.FindByEmailAsync(email);
        if (compte is null || compte.Etat != EtatDuCompte.Actif)
            return reponse;

        var jeton = await comptes.GeneratePasswordResetTokenAsync(compte);

        // Le jeton contient des « + », des « / » et des « = » : il doit être encodé pour passer dans une adresse.
        var lien = $"{application.Value.Adresse.TrimEnd('/')}/nouveau-mot-de-passe?compte={compte.Id}&jeton={Uri.EscapeDataString(jeton)}";
        var texte = string.Join(
            '\n',
            $"Bonjour {compte.Prenom},",
            "",
            "Pour changer votre mot de passe SchoolHub, ouvrez ce lien, valable 1 heure :",
            lien,
            "",
            "Si vous n'avez rien demandé, ignorez cet email.");

        try
        {
            await emails.EnvoyerAsync(compte.Email!, "Changer votre mot de passe SchoolHub", texte);
        }
        catch (Exception erreur)
        {
            // Une panne d'envoi ne change pas la réponse : sinon elle se verrait seulement pour un compte qui existe.
            // Le journal ne garde que l'identifiant du compte, jamais le lien.
            journaux.CreateLogger(nameof(MotDePasseOublieEndpoints))
                .LogError(erreur, "Le lien de mot de passe oublié du compte {Compte} n'a pas pu partir", compte.Id);
        }

        return reponse;
    }

    private static async Task<Results<Ok<Moi>, JsonHttpResult<Avis>>> ChangerLeMotDePasseAsync(
        ChoixDuMotDePasse choix,
        UserManager<Compte> comptes,
        SignInManager<Compte> connexion,
        SchoolHubDbContext contexte)
    {
        // Le lien d'abord : un lien expiré passe avant un mot de passe trop court, inutile de corriger un mot de passe qui ne servira pas.
        var compte = await VerifierLeLienAsync(comptes, choix.Compte, choix.Jeton);
        if (compte is null)
            return ConnexionEndpoints.Refus(StatusCodes.Status410Gone, LienExpire);

        if (choix.MotDePasse is null || choix.MotDePasse.Length < ConfigurationDesComptes.LongueurMinimaleDuMotDePasse)
            return ConnexionEndpoints.Refus(StatusCodes.Status400BadRequest, ConfigurationDesComptes.MotDePasseTropCourt);

        // L'enregistrement du mot de passe change le tampon de sécurité du compte : le lien ne sert plus,
        // et les sessions ouvertes ailleurs avec l'ancien mot de passe sont fermées.
        var resultat = await comptes.ResetPasswordAsync(compte, choix.Jeton!, choix.MotDePasse);
        if (!resultat.Succeeded)
            // La longueur est déjà vérifiée : il reste le cas où ce même lien vient d'être utilisé par un autre envoi simultané.
            return ConnexionEndpoints.Refus(StatusCodes.Status410Gone, LienExpire);

        // Le blocage après 5 essais faux n'a plus de raison d'être : la personne a prouvé qu'elle a accès à son email.
        await comptes.ResetAccessFailedCountAsync(compte);
        await comptes.SetLockoutEndDateAsync(compte, null);

        await ConnexionEndpoints.OuvrirLaSessionAsync(connexion, compte);
        return TypedResults.Ok(await ConnexionEndpoints.MoiAsync(compte, contexte));
    }

    /// <summary>
    /// Le compte actif à qui le lien est destiné, ou <c>null</c> si le lien ne vaut plus rien :
    /// compte inconnu, compte qui n'est plus actif, ou jeton faux, expiré, déjà utilisé, abîmé,
    /// ou fabriqué pour un autre usage (une invitation, par exemple).
    /// </summary>
    private static async Task<Compte?> VerifierLeLienAsync(UserManager<Compte> comptes, string? compte, string? jeton)
    {
        if (!Guid.TryParse(compte, out var id))
            return null;

        var actif = await comptes.FindByIdAsync(id.ToString());
        if (actif is null || actif.Etat != EtatDuCompte.Actif)
            return null;

        var valable = await comptes.VerifyUserTokenAsync(
            actif,
            comptes.Options.Tokens.PasswordResetTokenProvider,
            UserManager<Compte>.ResetPasswordTokenPurpose,
            jeton ?? "");
        return valable ? actif : null;
    }
}

/// <summary>Ce que la page « mot de passe oublié » envoie : l'email du compte. Un champ vide reçoit la réponse habituelle.</summary>
public sealed record DemandeDeLien(string? Email);
