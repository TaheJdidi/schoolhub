using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Groupes;

/// <summary>
/// Une matière enseignée au centre, « Mathématiques » (GRP-04). Elle n'a pas d'écran : une matière tapée
/// à la création d'un groupe s'ajoute à la liste du centre si elle n'y est pas.
/// </summary>
public sealed class Matiere : IAppartientAuCentre
{
    /// <summary>La taille de la colonne du nom.</summary>
    public const int LongueurMaximaleDuNom = 100;

    internal Matiere(string nom)
    {
        Nom = nom;
        NomNormalise = Normaliser(nom);
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public string Nom { get; private set; }

    /// <summary>Le nom en minuscules : « mathématiques » et « Mathématiques » sont la même matière.</summary>
    public string NomNormalise { get; private set; }

    /// <summary>La forme NFKC en minuscules, comme pour les niveaux.</summary>
    public static string Normaliser(string nom) => nom.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
}

internal sealed class MatiereConfiguration : IEntityTypeConfiguration<Matiere>
{
    public void Configure(EntityTypeBuilder<Matiere> matiere)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        matiere.ToTable("matieres");
        matiere.Property(m => m.Nom).HasMaxLength(Matiere.LongueurMaximaleDuNom);
        matiere.Property(m => m.NomNormalise).HasMaxLength(Matiere.LongueurMaximaleDuNom);
        matiere.HasIndex(m => new { m.CentreId, m.NomNormalise }).IsUnique();
    }
}
