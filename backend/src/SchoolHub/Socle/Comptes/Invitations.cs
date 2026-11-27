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
    /// L'Éditeur n'a pas de centre (<paramref name="centre"/> vide) : son email parle du back office.
    /// Si l'envoi échoue, l'exception remonte : c'est à l'appelant de décider quoi faire.
    /// </summary>
    public async Task EnvoyerAsync(Compte compte, Centre? centre, CancellationToken annulation = default)
    {
        var lien = await LienAsync(compte);
        var (sujet, invitation) = centre is null
            ? ("Votre compte Éditeur SchoolUp", "Vous êtes invité(e) à gérer les centres de SchoolUp depuis le back office.")
            : ($"Rejoindre le {centre.Nom} sur SchoolUp", $"Vous êtes invité(e) à rejoindre le {centre.Nom} sur SchoolUp.");
        var texte = string.Join(
            '\n',
            $"Bonjour {compte.Prenom},",
            "",
            invitation,
            "Choisissez votre mot de passe en ouvrant ce lien, valable 7 jours :",
            lien);

        await emails.EnvoyerAsync(compte.Email!, sujet, texte, annulation);
    }

    /// <summary>
    /// Un nouveau lien d'invitation du compte, valable 7 jours : celui de l'email, ou celui que l'Éditeur envoie
    /// par WhatsApp (EDI-03). Il ne rend pas caducs les liens déjà envoyés : seul un changement du tampon de sécurité le fait.
    /// </summary>
    public async Task<string> LienAsync(Compte compte)
    {
        var jeton = await comptes.GenerateUserTokenAsync(compte, JetonDInvitation.Nom, JetonDInvitation.But);

        // Le jeton contient des « + », des « / » et des « = » : il doit être encodé pour passer dans une adresse.
        return $"{application.Value.Adresse.TrimEnd('/')}/invitation?compte={compte.Id}&jeton={Uri.EscapeDataString(jeton)}";
    }
}
