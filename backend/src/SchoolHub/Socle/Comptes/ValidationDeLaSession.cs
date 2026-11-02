using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// À chaque requête, relit le compte en base : la session tombe dès que le compte est désactivé
/// ou que son tampon de sécurité change. Sinon, elle repart pour 12 heures.
/// Elle remplace la vérification d'Identity, qui ne regarde que le tampon, et seulement après un délai.
/// </summary>
public static class ValidationDeLaSession
{
    public static async Task ValiderAsync(CookieValidatePrincipalContext contexte)
    {
        var services = contexte.HttpContext.RequestServices;
        var comptes = services.GetRequiredService<UserManager<Compte>>();

        var id = contexte.Principal is null ? null : comptes.GetUserId(contexte.Principal);
        var compte = id is null ? null : await comptes.FindByIdAsync(id);
        var tampon = contexte.Principal?.FindFirst(comptes.Options.ClaimsIdentity.SecurityStampClaimType)?.Value;

        if (compte is null || compte.Etat != EtatDuCompte.Actif || compte.SecurityStamp != tampon)
        {
            contexte.RejectPrincipal();
            await contexte.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        // Le cookie est reconstruit depuis la base et renouvelé : le délai de 12 heures repart.
        var connexion = services.GetRequiredService<SignInManager<Compte>>();
        contexte.ReplacePrincipal(await connexion.CreateUserPrincipalAsync(compte));
        contexte.ShouldRenew = true;
    }
}
