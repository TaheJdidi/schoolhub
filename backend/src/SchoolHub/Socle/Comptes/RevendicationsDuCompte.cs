using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// Ce que le cookie de session dit du compte : son identifiant et son tampon de sécurité (Identity),
/// plus son centre (sauf l'Éditeur), son rôle, et la double authentification de l'Éditeur.
/// Il est reconstruit depuis la base à chaque requête.
/// </summary>
public sealed class RevendicationsDuCompte(UserManager<Compte> comptes, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<Compte>(comptes, options)
{
    /// <summary>Le type de la revendication qui porte le centre du compte.</summary>
    public const string Centre = "schoolhub:centre";

    /// <summary>
    /// Le type de la revendication que seul porte l'Éditeur dont la double authentification est active (EDI-00) :
    /// sans elle, aucune adresse du back office ne répond.
    /// </summary>
    public const string DoubleAuthentification = "schoolhub:2fa";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Compte compte)
    {
        var identite = await base.GenerateClaimsAsync(compte);
        if (compte.CentreId is { } centre)
            identite.AddClaim(new Claim(Centre, centre.ToString()));
        identite.AddClaim(new Claim(ClaimTypes.Role, compte.Role.ToString()));
        if (compte.Role == RoleDuCompte.Editeur && compte.TwoFactorEnabled)
            identite.AddClaim(new Claim(DoubleAuthentification, "oui"));
        return identite;
    }
}
