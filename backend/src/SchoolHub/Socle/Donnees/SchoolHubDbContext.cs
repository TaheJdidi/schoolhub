using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Journal;

namespace SchoolHub.Socle.Donnees;

/// <summary>
/// Accès à la base PostgreSQL partagée par tous les centres (ADR 0001, ADR 0002).
/// Chaque donnée d'un centre est filtrée automatiquement : un centre ne voit que ses données,
/// et ne peut ni ajouter, ni modifier, ni supprimer celles d'un autre.
/// </summary>
public class SchoolHubDbContext(DbContextOptions options, CentreCourant centreCourant)
    : IdentityUserContext<Compte, Guid>(options)
{
    public const string FiltreDuCentre = "Centre";

    public DbSet<Centre> Centres => Set<Centre>();

    public DbSet<LigneDuJournal> LignesDuJournal => Set<LigneDuJournal>();

    public DbSet<AnneeScolaire> AnneesScolaires => Set<AnneeScolaire>();

    public DbSet<Niveau> Niveaux => Set<Niveau>();

    public DbSet<Professeur> Professeurs => Set<Professeur>();

    public DbSet<Matiere> Matieres => Set<Matiere>();

    public DbSet<Groupe> Groupes => Set<Groupe>();

    public DbSet<Seance> Seances => Set<Seance>();

    public DbSet<Eleve> Eleves => Set<Eleve>();

    // Lu à chaque requête : le filtre suit le centre courant de ce contexte.
    private Guid? CentreIdCourant => centreCourant.Id;

    public static void Configurer(DbContextOptionsBuilder options, string? chaineDeConnexion) =>
        options.UseNpgsql(chaineDeConnexion).UseSnakeCaseNamingConvention();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity d'abord : nos configurations renomment ensuite ses tables.
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchoolHubDbContext).Assembly);

        // Les trois tables techniques d'Identity, vides pour l'instant.
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("comptes_revendications");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("comptes_connexions_externes");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("comptes_jetons");

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
