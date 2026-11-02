using Microsoft.AspNetCore.WebUtilities;
using SchoolHub.Socle.Emails;

namespace SchoolHub.Tests.Outils;

/// <summary>
/// Le faux expéditeur des tests : il garde les emails au lieu de les envoyer,
/// pour que les tests lisent les liens et les suivent. Mis à <see cref="Echouer"/>,
/// il se comporte comme un expéditeur qui ne répond pas.
/// </summary>
public sealed class EmailsCaptures : IEnvoiEmail
{
    public List<EmailEnvoye> Envoyes { get; } = [];

    /// <summary>Si vrai, l'envoi échoue comme lorsque le service d'emails ne répond pas.</summary>
    public bool Echouer { get; set; }

    public Task EnvoyerAsync(string destinataire, string sujet, string texte, CancellationToken annulation = default)
    {
        if (Echouer)
            throw new InvalidOperationException("Brevo ne répond pas");

        Envoyes.Add(new EmailEnvoye(destinataire, sujet, texte));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Le compte et le jeton du lien de l'email, décodés : le jeton contient des « + », « / » et « = »,
    /// que le lien écrit en « %2B », « %2F » et « %3D ».
    /// </summary>
    public (string Compte, string Jeton) LireLeLien(EmailEnvoye email)
    {
        var adresse = email.Texte
            .Split([' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .First(mot => mot.StartsWith("http", StringComparison.Ordinal));
        var requete = QueryHelpers.ParseQuery(new Uri(adresse).Query);
        return (requete["compte"].ToString(), requete["jeton"].ToString());
    }
}

/// <summary>Un email gardé par <see cref="EmailsCaptures"/>.</summary>
public sealed record EmailEnvoye(string Destinataire, string Sujet, string Texte);
