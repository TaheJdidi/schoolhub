using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// Ce qui suit un mot de passe vérifié : ouvrir la session, ou, pour l'Éditeur, demander d'abord le code
/// de son application d'authentification (EDI-00). Les connexions de l'Éditeur, qui n'a pas de centre,
/// vont dans le journal technique du serveur.
/// </summary>
public sealed class OuvertureDeSession(
    UserManager<Compte> comptes,
    SignInManager<Compte> connexion,
    SchoolHubDbContext contexte,
    TimeProvider temps,
    ILogger<OuvertureDeSession> journaux)
{
    public const string TropDEssais = "Trop d'essais : réessayez dans 15 minutes";
    public const string EtapeExpiree = "Cette étape a expiré : reconnectez-vous";
    public const string ScannerLeQrCode = "Scannez le QR code avec votre application d'authentification, puis saisissez le code qu'elle affiche";
    public const string SaisirLeCodeDeLApplication = "Saisissez le code à 6 chiffres de votre application d'authentification";
    public const string CodeDeLApplicationFaux = "Ce code n'est pas le bon : saisissez celui que votre application affiche en ce moment";

    // Où SchoolHub range les secrets de l'Éditeur dans la table des jetons d'Identity (comptes_jetons).
    private const string Fournisseur = "SchoolHub";
    private const string JetonDeLaCleTotp = "CleTotp";
    private const string JetonDuDernierPasTotp = "DernierPasTotp";

    /// <summary>
    /// Le mot de passe est bon et le compte actif : la session s'ouvre, sauf pour l'Éditeur,
    /// qui passe d'abord par son application d'authentification (réponse 202 avec l'étape).
    /// </summary>
    public async Task<Results<Ok<Moi>, Accepted<EtapeSuivante>, JsonHttpResult<Avis>>> ApresLeMotDePasseAsync(HttpContext http, Compte compte)
    {
        if (compte.Role == RoleDuCompte.Editeur)
        {
            var etape = compte.TwoFactorEnabled
                ? EtapeDeConnexion.CodeDeDoubleAuthentification
                : EtapeDeConnexion.ActivationDeLaDoubleAuthentification;
            await EtapeDeConnexion.CommencerAsync(http, compte, etape);
            return TypedResults.Accepted((string?)null, new EtapeSuivante(
                etape,
                compte.TwoFactorEnabled ? SaisirLeCodeDeLApplication : ScannerLeQrCode));
        }

        return TypedResults.Ok(await OuvrirAsync(http, compte));
    }

    /// <summary>
    /// Le QR code de la première connexion de l'Éditeur. La clé est tirée une fois, puis gardée :
    /// recharger la page montre le même QR code, que l'application a peut-être déjà scanné.
    /// </summary>
    public async Task<Results<Ok<ActivationDeLaDoubleAuthentification>, JsonHttpResult<Avis>>> ActivationAsync(HttpContext http)
    {
        var compte = await CompteDeLEtapeAsync(http, EtapeDeConnexion.ActivationDeLaDoubleAuthentification);
        if (compte is null)
            return ConnexionEndpoints.Refus(StatusCodes.Status401Unauthorized, EtapeExpiree);

        var cle = await CleTotpAsync(compte);
        if (cle is null)
        {
            cle = Totp.NouvelleCle();
            await comptes.SetAuthenticationTokenAsync(compte, Fournisseur, JetonDeLaCleTotp, Base32.Encoder(cle));
        }

        using var generateur = new QRCodeGenerator();
        using var donnees = generateur.CreateQrCode(Totp.Adresse(compte.Email!, cle), QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(donnees).GetGraphic(6);

        // La clé en groupes de 4 lettres, pour la taper à la main si le QR code ne passe pas.
        var texte = Base32.Encoder(cle);
        var groupes = string.Join(' ', Enumerable.Range(0, (texte.Length + 3) / 4).Select(i => texte.Substring(i * 4, Math.Min(4, texte.Length - i * 4))));
        return TypedResults.Ok(new ActivationDeLaDoubleAuthentification($"data:image/png;base64,{Convert.ToBase64String(png)}", groupes));
    }

    /// <summary>
    /// Le code de l'application d'authentification de l'Éditeur. À la première connexion, il confirme
    /// que le QR code est bien scanné et active la double authentification ; ensuite, il est demandé à chaque connexion.
    /// Un code ne sert qu'une fois, même dans ses 30 secondes. Un code faux compte comme un mot de passe faux.
    /// </summary>
    public async Task<Results<Ok<Moi>, JsonHttpResult<Avis>>> CodeDeLApplicationAsync(HttpContext http, string? code)
    {
        var compte = await CompteDeLEtapeAsync(
            http,
            EtapeDeConnexion.ActivationDeLaDoubleAuthentification,
            EtapeDeConnexion.CodeDeDoubleAuthentification);
        if (compte is null)
            return ConnexionEndpoints.Refus(StatusCodes.Status401Unauthorized, EtapeExpiree);
        if (await comptes.IsLockedOutAsync(compte))
            return ConnexionEndpoints.Refus(StatusCodes.Status401Unauthorized, TropDEssais);

        var cle = await CleTotpAsync(compte);
        if (cle is null)
            return ConnexionEndpoints.Refus(StatusCodes.Status401Unauthorized, EtapeExpiree);

        var pas = Totp.Verifier(cle, code ?? "", temps.GetUtcNow());
        var dernier = await comptes.GetAuthenticationTokenAsync(compte, Fournisseur, JetonDuDernierPasTotp);
        if (pas is null || (dernier is not null && pas <= long.Parse(dernier, CultureInfo.InvariantCulture)))
        {
            await comptes.AccessFailedAsync(compte);
            var bloque = await comptes.IsLockedOutAsync(compte);
            journaux.LogWarning("Code de l'application d'authentification faux pour l'Éditeur {Compte}{Bloque}", compte.Id, bloque ? " : compte bloqué 15 minutes" : "");
            return ConnexionEndpoints.Refus(StatusCodes.Status401Unauthorized, bloque ? TropDEssais : CodeDeLApplicationFaux);
        }

        await comptes.SetAuthenticationTokenAsync(compte, Fournisseur, JetonDuDernierPasTotp, pas.Value.ToString(CultureInfo.InvariantCulture));
        await comptes.ResetAccessFailedCountAsync(compte);

        // Activer la double authentification change le tampon de sécurité : la session s'ouvre avec le nouveau.
        if (!compte.TwoFactorEnabled)
            await comptes.SetTwoFactorEnabledAsync(compte, true);

        journaux.LogInformation("Connexion de l'Éditeur {Compte}", compte.Id);
        return TypedResults.Ok(await OuvrirAsync(http, compte));
    }

    /// <summary>Ouvre la session de 12 heures et retire le cookie d'étape.</summary>
    private async Task<Moi> OuvrirAsync(HttpContext http, Compte compte)
    {
        await EtapeDeConnexion.TerminerAsync(http);
        await ConnexionEndpoints.OuvrirLaSessionAsync(connexion, compte);
        return await ConnexionEndpoints.MoiAsync(compte, contexte);
    }

    /// <summary>
    /// Le compte du cookie d'étape, s'il attend l'une de ces étapes, est encore actif et n'a pas changé de mot de passe
    /// depuis ; sinon <c>null</c>.
    /// </summary>
    private async Task<Compte?> CompteDeLEtapeAsync(HttpContext http, params string[] etapes)
    {
        var etape = await EtapeDeConnexion.LireAsync(http, etapes);
        if (etape is null)
            return null;

        var compte = await contexte.Users.SingleOrDefaultAsync(c => c.Id == etape.Value.Compte);
        return compte is { Etat: EtatDuCompte.Actif } && compte.SecurityStamp == etape.Value.Tampon ? compte : null;
    }

    private async Task<byte[]?> CleTotpAsync(Compte compte)
    {
        var cle = await comptes.GetAuthenticationTokenAsync(compte, Fournisseur, JetonDeLaCleTotp);
        return cle is null ? null : Base32.Decoder(cle);
    }
}
