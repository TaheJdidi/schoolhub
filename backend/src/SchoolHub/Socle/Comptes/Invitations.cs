using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Emails;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// Envoie l'invitation d'un compte : un email avec le lien pour choisir son mot de passe.
/// </summary>
public sealed class Invitations(UserManager<Compte> comptes, IEnvoiEmail emails, IOptions<OptionsDeLApplication> application)
{
    /// <summary>
    /// Fabrique le jeton d'invitation du compte, puis envoie l'email avec le lien, valable 7 jours.
    /// Si l'envoi échoue, l'exception remonte : c'est à l'appelant de décider quoi faire.
    /// </summary>
    public async Task EnvoyerAsync(Compte compte, Centre centre, CancellationToken annulation = default)
    {
        var jeton = await comptes.GenerateUserTokenAsync(compte, JetonDInvitation.Nom, JetonDInvitation.But);

        // Le jeton contient des « + », des « / » et des « = » : il doit être encodé pour passer dans une adresse.
        var lien = $"{application.Value.Adresse.TrimEnd('/')}/invitation?compte={compte.Id}&jeton={Uri.EscapeDataString(jeton)}";
        var texte = string.Join(
            '\n',
            $"Bonjour {compte.Prenom},",
            "",
            $"Vous êtes invité(e) à rejoindre le {centre.Nom} sur SchoolHub.",
            "Choisissez votre mot de passe en ouvrant ce lien, valable 7 jours :",
            lien);

        await emails.EnvoyerAsync(compte.Email!, $"Rejoindre le {centre.Nom} sur SchoolHub", texte, annulation);
    }
}
