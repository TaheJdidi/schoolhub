using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Groupes;

/// <summary>
/// Une séance d'un groupe, créée depuis ses créneaux (GRP-04). Les séances (SEA) viendront l'annuler, la déplacer
/// et y faire l'appel ; une séance annulée sortira du compte des Mois.
/// </summary>
public sealed class Seance : IAppartientAuCentre
{
    internal Seance(Guid groupeId, DateOnly date, TimeOnly debut, TimeOnly fin)
    {
        GroupeId = groupeId;
        Date = date;
        Debut = debut;
        Fin = fin;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid GroupeId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly Debut { get; private set; }

    public TimeOnly Fin { get; private set; }
}

internal sealed class SeanceConfiguration : IEntityTypeConfiguration<Seance>
{
    public void Configure(EntityTypeBuilder<Seance> seance)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        seance.ToTable("seances");
        seance.HasOne<Groupe>().WithMany().HasForeignKey(s => s.GroupeId).OnDelete(DeleteBehavior.Restrict);
        seance.HasIndex(s => new { s.GroupeId, s.Date });
    }
}
