using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Professeurs;

/// <summary>
/// Un professeur du centre (GRP-01). Il n'a pas de compte : il ne se connecte pas.
/// Quand il quitte le centre, il est marqué « parti » (GRP-03) : il garde sa fiche et son historique.
/// </summary>
public sealed class Professeur : IAppartientAuCentre
{
    /// <summary>La taille des colonnes du prénom et du nom.</summary>
    public const int LongueurMaximaleDuNom = 100;

    internal Professeur(string prenom, string nom, string telephone, string? email, string? matiere = null)
    {
        Prenom = prenom;
        Nom = nom;
        Telephone = telephone;
        Email = email;
        Matiere = matiere;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public string Prenom { get; private set; }

    public string Nom { get; private set; }

    /// <summary>Les 8 chiffres, sans espaces ni indicatif (<see cref="TelephoneTunisien"/>).</summary>
    public string Telephone { get; private set; }

    public string? Email { get; private set; }

    /// <summary>
    /// La matière qu'il enseigne, « Mathématiques » : un nouveau groupe la prend d'office. Vide pour un professeur
    /// ajouté avant, ou importé sans elle : le nouveau groupe la fait alors choisir.
    /// </summary>
    public string? Matiere { get; private set; }

    /// <summary>
    /// Le professeur a quitté le centre : il sort de la liste des professeurs (GRP-03).
    /// Un professeur parti ne reçoit pas de nouveau groupe (GRP-04).
    /// </summary>
    public bool Parti { get; private set; }

    /// <summary>La nouvelle fiche, déjà vérifiée.</summary>
    public void Modifier(string prenom, string nom, string telephone, string? email, string? matiere)
    {
        Prenom = prenom;
        Nom = nom;
        Telephone = telephone;
        Email = email;
        Matiere = matiere;
    }

    public void Partir() => Parti = true;

    public void Revenir() => Parti = false;
}

internal sealed class ProfesseurConfiguration : IEntityTypeConfiguration<Professeur>
{
    public void Configure(EntityTypeBuilder<Professeur> professeur)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        // Pas d'index unique sur le téléphone : deux professeurs peuvent l'avoir, la plateforme prévient seulement.
        professeur.ToTable("professeurs");
        professeur.Property(p => p.Prenom).HasMaxLength(Professeur.LongueurMaximaleDuNom);
        professeur.Property(p => p.Nom).HasMaxLength(Professeur.LongueurMaximaleDuNom);
        professeur.Property(p => p.Telephone).HasMaxLength(8);
        professeur.Property(p => p.Email).HasMaxLength(AdresseEmail.LongueurMaximale);
        professeur.Property(p => p.Matiere).HasMaxLength(Matiere.LongueurMaximaleDuNom);
    }
}
