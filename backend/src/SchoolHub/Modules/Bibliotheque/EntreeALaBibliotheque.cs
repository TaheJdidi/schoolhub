using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Modules.Caisse;
using SchoolHub.Modules.Eleves;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Bibliotheque;

/// <summary>
/// BIB-01 : une entrée à la bibliothèque pour une journée, payée en arrivant, avec son reçu. L'heure d'arrivée se note
/// toute seule ; l'heure de départ est facultative (BIB-02).
/// </summary>
public sealed class EntreeALaBibliotheque : IAppartientAuCentre
{
    internal EntreeALaBibliotheque(Guid eleveId, DateOnly jour, DateTimeOffset arriveeLe, Guid recuId, decimal montant)
    {
        EleveId = eleveId;
        Jour = jour;
        ArriveeLe = arriveeLe;
        RecuId = recuId;
        Montant = montant;
    }

    // Pour EF Core.
    private EntreeALaBibliotheque()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid EleveId { get; private set; }

    /// <summary>Le jour de l'entrée, à Tunis.</summary>
    public DateOnly Jour { get; private set; }

    public DateTimeOffset ArriveeLe { get; private set; }

    /// <summary>BIB-02 : l'heure de départ à Tunis, si la secrétaire la connaît.</summary>
    public TimeOnly? Depart { get; private set; }

    public Guid RecuId { get; private set; }

    public decimal Montant { get; private set; }

    internal void NoterLeDepart(TimeOnly heure) => Depart = heure;
}

internal sealed class EntreeALaBibliothequeConfiguration : IEntityTypeConfiguration<EntreeALaBibliotheque>
{
    public void Configure(EntityTypeBuilder<EntreeALaBibliotheque> entree)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        entree.ToTable("entrees_a_la_bibliotheque");
        entree.Property(e => e.Montant).HasPrecision(12, 3);
        entree.HasOne<Eleve>().WithMany().HasForeignKey(e => e.EleveId).OnDelete(DeleteBehavior.Restrict);
        entree.HasOne<Recu>().WithMany().HasForeignKey(e => e.RecuId).OnDelete(DeleteBehavior.Restrict);
        entree.HasIndex(e => e.RecuId).IsUnique();
        entree.HasIndex(e => new { e.Jour, e.EleveId });
    }
}
