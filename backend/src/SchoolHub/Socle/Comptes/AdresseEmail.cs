using System.Net.Mail;

namespace SchoolHub.Socle.Comptes;

/// <summary>La règle d'un email valable, la même pour la commande de création d'un centre et pour une invitation.</summary>
public static class AdresseEmail
{
    /// <summary>La taille de la colonne de l'email dans la table des comptes (Identity).</summary>
    public const int LongueurMaximale = 256;

    /// <summary>
    /// Une adresse écrite telle quelle (pas de nom devant, pas de liste), dont le domaine a un point,
    /// et qui tient dans sa colonne.
    /// </summary>
    public static bool EstValable(string email) =>
        email.Length <= LongueurMaximale
        && MailAddress.TryCreate(email, out var adresse)
        && adresse.Address == email
        && adresse.Host.Contains('.');
}
