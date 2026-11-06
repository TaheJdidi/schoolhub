using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Seances;

/// <summary>
/// SEA-12 : un élève vient à une séance d'un autre groupe du même professeur, de la même matière et du même niveau, à la
/// place d'une séance précise de son groupe, déjà manquée ou à venir (ticket 02, Q7). Il continue de payer dans son
/// groupe : le rattrapage n'est pas une inscription, et il n'entre ni dans le badge « groupe payé » ni dans les couleurs.
/// </summary>
public sealed class Rattrapage : IAppartientAuCentre
{
    internal Rattrapage(Guid seanceId, Guid inscriptionId, Guid seanceRattrapeeId)
    {
        SeanceId = seanceId;
        InscriptionId = inscriptionId;
        SeanceRattrapeeId = seanceRattrapeeId;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    /// <summary>La séance du groupe d'accueil, où l'élève vient.</summary>
    public Guid SeanceId { get; private set; }

    /// <summary>L'inscription de l'élève dans son groupe : il y paie.</summary>
    public Guid InscriptionId { get; private set; }

    /// <summary>La séance de son groupe qu'il rattrape.</summary>
    public Guid SeanceRattrapeeId { get; private set; }
}

internal sealed class RattrapageConfiguration : IEntityTypeConfiguration<Rattrapage>
{
    public void Configure(EntityTypeBuilder<Rattrapage> rattrapage)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        rattrapage.ToTable("rattrapages");
        // Une séance retirée par la fermeture d'un groupe (GRP-08) emporte ses rattrapages.
        rattrapage.HasOne<Seance>().WithMany().HasForeignKey(r => r.SeanceId).OnDelete(DeleteBehavior.Cascade);
        rattrapage.HasOne<Seance>().WithMany().HasForeignKey(r => r.SeanceRattrapeeId).OnDelete(DeleteBehavior.Cascade);
        rattrapage.HasOne<Inscription>().WithMany().HasForeignKey(r => r.InscriptionId).OnDelete(DeleteBehavior.Restrict);
        rattrapage.HasIndex(r => new { r.SeanceId, r.InscriptionId }).IsUnique();
        // Chaque séance de son groupe ne se rattrape qu'une fois.
        rattrapage.HasIndex(r => new { r.InscriptionId, r.SeanceRattrapeeId }).IsUnique();
    }
}
