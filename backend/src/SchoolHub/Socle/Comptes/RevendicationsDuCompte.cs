using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// Ce que le cookie de session dit du compte : son identifiant et son tampon de sécurité (Identity),
/// plus son centre et son rôle. Il est reconstruit depuis la base à chaque requête.
/// </summary>
public sealed class RevendicationsDuCompte(UserManager<Compte> comptes, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<Compte>(comptes, options)
{
    /// <summary>Le type de la revendication qui porte le centre du compte.</summary>
    public const string Centre = "schoolhub:centre";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Compte compte)
    {
        var identite = await base.GenerateClaimsAsync(compte);
        identite.AddClaim(new Claim(Centre, compte.CentreId.ToString()));
        identite.AddClaim(new Claim(ClaimTypes.Role, compte.Role.ToString()));
        return identite;
    }
}
