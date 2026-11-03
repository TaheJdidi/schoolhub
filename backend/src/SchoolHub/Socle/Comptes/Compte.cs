using Microsoft.AspNetCore.Identity;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// Personne qui se connecte à SchoolHub : elle appartient à un seul centre et a un rôle.
/// L'email, qui sert aussi d'identifiant de connexion, est unique dans tout SchoolHub.
/// Le compte se lit avant de connaître le centre (à la connexion) : c'est une donnée commune,
/// qui n'implémente pas <see cref="IAppartientAuCentre"/>.
/// </summary>
public sealed class Compte : IdentityUser<Guid>
{
    /// <summary>La taille des colonnes du prénom et du nom.</summary>
    public const int LongueurMaximaleDuNom = 100;

    public Compte(Guid centreId, string prenom, string nom, string email, RoleDuCompte role)
    {
        Id = Guid.CreateVersion7();
        UserName = Email = email;
        CentreId = centreId;
        Prenom = prenom;
        Nom = nom;
        Role = role;
        Etat = EtatDuCompte.Invite;
    }

    public Guid CentreId { get; private set; }

    public string Prenom { get; private set; }

    public string Nom { get; private set; }

    public RoleDuCompte Role { get; private set; }

    public EtatDuCompte Etat { get; private set; }

    /// <summary>La personne a choisi son mot de passe : elle peut se connecter.</summary>
    public void Activer() => Etat = EtatDuCompte.Actif;

    /// <summary>Le compte ne peut plus se connecter, mais il reste en base.</summary>
    public void Desactiver() => Etat = EtatDuCompte.Desactive;

    /// <summary>
    /// Le compte retrouve l'état qu'il aurait sans la désactivation : actif s'il avait choisi son mot de passe,
    /// invité sinon (un email mal tapé, par exemple).
    /// </summary>
    public void Reactiver() => Etat = PasswordHash is null ? EtatDuCompte.Invite : EtatDuCompte.Actif;
}

/// <summary>Ce que la personne a le droit de faire dans son centre.</summary>
public enum RoleDuCompte
{
    Proprietaire,
    Secretaire,
}

/// <summary>Où en est le compte : invité (pas encore de mot de passe), actif ou désactivé.</summary>
public enum EtatDuCompte
{
    Invite,
    Actif,
    Desactive,
}
