using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// Entre le bon mot de passe de l'Éditeur et sa session, il reste une étape : sa double authentification (EDI-00).
/// Le mot de passe déjà vérifié est retenu dans un cookie à part, <c>schoolhub-etape</c>, valable 10 minutes,
/// qui ne donne accès à rien d'autre qu'à cette étape. Il porte le compte, son tampon de sécurité
/// (un mot de passe changé entre-temps l'annule) et l'étape attendue.
/// </summary>
public static class EtapeDeConnexion
{
    /// <summary>Le schéma d'authentification du cookie d'étape.</summary>
    public const string Schema = "SchoolHub.Etape";

    public const string Cookie = "schoolhub-etape";

    public static readonly TimeSpan Duree = TimeSpan.FromMinutes(10);

    /// <summary>L'Éditeur n'a pas encore de double authentification : il scanne le QR code puis saisit un code.</summary>
    public const string ActivationDeLaDoubleAuthentification = "activation-2fa";

    /// <summary>L'Éditeur saisit le code de son application d'authentification.</summary>
    public const string CodeDeDoubleAuthentification = "code-2fa";

    private const string RevendicationDeLEtape = "schoolhub:etape";
    private const string RevendicationDuTampon = "schoolhub:tampon";

    /// <summary>Pose le cookie d'étape : la prochaine requête pourra finir la connexion de ce compte.</summary>
    public static Task CommencerAsync(HttpContext http, Compte compte, string etape)
    {
        var identite = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, compte.Id.ToString()),
                new Claim(RevendicationDuTampon, compte.SecurityStamp ?? ""),
                new Claim(RevendicationDeLEtape, etape),
            ],
            Schema);
        return http.SignInAsync(Schema, new ClaimsPrincipal(identite), new AuthenticationProperties { IsPersistent = false });
    }

    /// <summary>
    /// Le compte et l'étape du cookie, ou <c>null</c> si l'étape n'est plus valable : pas de cookie,
    /// plus de 10 minutes, autre étape que celles attendues, ou tampon de sécurité changé.
    /// </summary>
    public static async Task<(Guid Compte, string Tampon, string Etape)?> LireAsync(HttpContext http, params string[] etapesAttendues)
    {
        var resultat = await http.AuthenticateAsync(Schema);
        var principal = resultat.Succeeded ? resultat.Principal : null;
        var id = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tampon = principal?.FindFirst(RevendicationDuTampon)?.Value;
        var etape = principal?.FindFirst(RevendicationDeLEtape)?.Value;
        if (!Guid.TryParse(id, out var compte) || tampon is null || etape is null || !etapesAttendues.Contains(etape))
            return null;
        return (compte, tampon, etape);
    }

    /// <summary>Retire le cookie d'étape : la connexion est finie, ou abandonnée.</summary>
    public static Task TerminerAsync(HttpContext http) => http.SignOutAsync(Schema);
}

/// <summary>
/// La réponse 202 de la connexion quand une étape reste à faire : son nom (<see cref="EtapeDeConnexion"/>)
/// et ce qu'il faut dire à la personne.
/// </summary>
public sealed record EtapeSuivante(string Etape, string Message);

/// <summary>Ce que la page envoie à l'étape du code de l'application d'authentification.</summary>
public sealed record SaisieDuCode(string? Code);

/// <summary>De quoi activer la double authentification : le QR code (image PNG en data URI) et la clé à taper à la main.</summary>
public sealed record ActivationDeLaDoubleAuthentification(string QrCode, string Cle);
