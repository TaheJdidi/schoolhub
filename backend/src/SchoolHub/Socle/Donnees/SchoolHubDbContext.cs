using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Socle.Donnees;

/// <summary>
/// Accès à la base PostgreSQL partagée par tous les centres (ADR 0001, ADR 0002).
/// Chaque donnée d'un centre est filtrée automatiquement : un centre ne voit que ses données,
/// et ne peut ni ajouter, ni modifier, ni supprimer celles d'un autre.
/// </summary>
public class SchoolHubDbContext(DbContextOptions options, CentreCourant centreCourant) : DbContext(options)
{
    public const string FiltreDuCentre = "Centre";

    public DbSet<Centre> Centres => Set<Centre>();

    // Lu à chaque requête : le filtre suit le centre courant de ce contexte.
    private Guid? CentreIdCourant => centreCourant.Id;

    public static void Configurer(DbContextOptionsBuilder options, string? chaineDeConnexion) =>
        options.UseNpgsql(chaineDeConnexion).UseSnakeCaseNamingConvention();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchoolHubDbContext).Assembly);

        var donneesDesCentres = modelBuilder.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(typeof(IAppartientAuCentre).IsAssignableFrom)
            .ToList();
        var separer = typeof(SchoolHubDbContext).GetMethod(nameof(SeparerLesCentres), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var type in donneesDesCentres)
            separer.MakeGenericMethod(type).Invoke(this, [modelBuilder]);
    }

    private void SeparerLesCentres<T>(ModelBuilder modelBuilder) where T : class, IAppartientAuCentre
    {
        var entite = modelBuilder.Entity<T>();
        entite.HasQueryFilter(FiltreDuCentre, donnee => donnee.CentreId == CentreIdCourant);
        entite.HasOne<Centre>().WithMany().HasForeignKey(nameof(IAppartientAuCentre.CentreId)).OnDelete(DeleteBehavior.Restrict);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GarderChaqueDonneeDansSonCentre();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GarderChaqueDonneeDansSonCentre();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void GarderChaqueDonneeDansSonCentre()
    {
        foreach (var entree in ChangeTracker.Entries<IAppartientAuCentre>())
        {
            var centreId = entree.Property(nameof(IAppartientAuCentre.CentreId));

            switch (entree.State)
            {
                case EntityState.Added:
                    centreId.CurrentValue = CentreIdCourant
                        ?? throw new DonneeHorsDuCentreException("Impossible d'ajouter une donnée sans centre courant.");
                    break;

                case EntityState.Modified or EntityState.Deleted when centreId.IsModified:
                    throw new DonneeHorsDuCentreException("Une donnée ne peut pas passer d'un centre à un autre.");

                case EntityState.Modified or EntityState.Deleted when (Guid?)centreId.OriginalValue != CentreIdCourant:
                    throw new DonneeHorsDuCentreException("Cette donnée appartient à un autre centre.");
            }
        }
    }
}
