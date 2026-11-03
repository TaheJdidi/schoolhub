using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SchoolHub.Socle.Centres;

/// <summary>
/// Période fixée par le centre, de la rentrée à la fin des cours, par exemple du 15/09/2027 au 30/06/2028 (CEN-02).
/// Pour l'instant, un centre en a une seule ; CEN-04 ajoutera l'année suivante pendant que celle-ci tourne encore.
/// </summary>
public sealed class AnneeScolaire : IAppartientAuCentre
{
    internal AnneeScolaire(DateOnly debut, DateOnly fin)
    {
        Debut = debut;
        Fin = fin;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public DateOnly Debut { get; private set; }

    public DateOnly Fin { get; private set; }

    /// <summary>Les nouvelles dates, déjà vérifiées.</summary>
    public void Changer(DateOnly debut, DateOnly fin)
    {
        Debut = debut;
        Fin = fin;
    }
}

internal sealed class AnneeScolaireConfiguration : IEntityTypeConfiguration<AnneeScolaire>
{
    // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
    public void Configure(EntityTypeBuilder<AnneeScolaire> annee) => annee.ToTable("annees_scolaires");
}
