using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Tests.Outils;

/// <summary>
/// Le contexte de l'application, plus une donnée de test qui appartient à un centre.
/// Il vérifie la séparation des centres avant que les modules aient leurs propres données.
/// </summary>
public sealed class ContexteDeTest(DbContextOptions options, CentreCourant centreCourant)
    : SchoolHubDbContext(options, centreCourant)
{
    public DbSet<NoteDeTest> Notes => Set<NoteDeTest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Déclarée avant l'appel à la base, qui filtre chaque donnée d'un centre.
        modelBuilder.Entity<NoteDeTest>();
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class NoteDeTest(string texte) : IAppartientAuCentre
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid CentreId { get; private set; }
    public string Texte { get; set; } = texte;
}
