using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// Le fournisseur de jetons des invitations. Il est à part du reste d'Identity pour avoir sa propre durée :
/// un lien d'invitation est valable 7 jours.
/// </summary>
public sealed class JetonDInvitation(
    IDataProtectionProvider protection,
    IOptions<OptionsDuJetonDInvitation> options,
    ILogger<DataProtectorTokenProvider<Compte>> journal)
    : DataProtectorTokenProvider<Compte>(protection, options, journal)
{
    /// <summary>Le nom du fournisseur, à donner à <c>GenerateUserTokenAsync</c> et à <c>VerifyUserTokenAsync</c>.</summary>
    public const string Nom = "Invitation";

    /// <summary>L'usage du jeton : un jeton fabriqué pour un autre usage ne passe pas ici.</summary>
    public const string But = "invitation";
}

/// <summary>Les réglages du jeton d'invitation : son nom, et 7 jours de validité.</summary>
public sealed class OptionsDuJetonDInvitation : DataProtectionTokenProviderOptions
{
    public OptionsDuJetonDInvitation()
    {
        Name = JetonDInvitation.Nom;
        TokenLifespan = TimeSpan.FromDays(7);
    }
}
