using Microsoft.AspNetCore.Identity;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// Personne qui se connecte à SchoolHub. Le propriétaire et la secrétaire appartiennent à un seul centre ;
/// l'Éditeur (l'équipe SchoolHub, EDI-00) n'appartient à aucun : son <see cref="CentreId"/> est vide.
/// L'email, qui sert aussi d'identifiant de connexion, est unique dans tout SchoolHub.
/// Le compte se lit avant de connaître le centre (à la connexion) : c'est une donnée commune,
/// qui n'implémente pas <see cref="IAppartientAuCentre"/>.
/// </summary>
public sealed class Compte : IdentityUser<Guid>
{
    /// <summary>La taille des colonnes du prénom et du nom.</summary>
    public const int LongueurMaximaleDuNom = 100;

    public Compte(Guid? centreId, string prenom, string nom, string email, RoleDuCompte role)
    {
        if ((role == RoleDuCompte.Editeur) != (centreId is null))
            throw new ArgumentException("L'Éditeur n'a pas de centre ; le propriétaire et la secrétaire en ont un.", nameof(centreId));

        Id = Guid.CreateVersion7();
        UserName = Email = email;
        CentreId = centreId;
        Prenom = prenom;
        Nom = nom;
        Role = role;
        Etat = EtatDuCompte.Invite;
    }

    /// <summary>Un compte Éditeur, sans centre, encore invité (EDI-00).</summary>
    public static Compte Editeur(string prenom, string nom, string email) => new(null, prenom, nom, email, RoleDuCompte.Editeur);

    /// <summary>Le centre du compte, ou <c>null</c> pour l'Éditeur.</summary>
    public Guid? CentreId { get; private set; }

    public string Prenom { get; private set; }

    public string Nom { get; private set; }

    public RoleDuCompte Role { get; private set; }

    public EtatDuCompte Etat { get; private set; }

    /// <summary>Le prénom et le nom corrigés (l'Éditeur corrige le propriétaire avant de renvoyer son invitation, EDI-03).</summary>
    public void Renommer(string prenom, string nom)
    {
        Prenom = prenom;
        Nom = nom;
    }

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

/// <summary>Ce que la personne a le droit de faire : dans son centre, ou dans le back office pour l'Éditeur.</summary>
public enum RoleDuCompte
{
    Proprietaire,
    Secretaire,
    Editeur,
}

/// <summary>Où en est le compte : invité (pas encore de mot de passe), actif ou désactivé.</summary>
public enum EtatDuCompte
{
    Invite,
    Actif,
    Desactive,
}
