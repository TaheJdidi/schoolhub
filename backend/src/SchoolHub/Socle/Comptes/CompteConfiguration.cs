using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Socle.Comptes;

internal sealed class CompteConfiguration : IEntityTypeConfiguration<Compte>
{
    public void Configure(EntityTypeBuilder<Compte> compte)
    {
        compte.ToTable("comptes");

        compte.Property(c => c.Prenom).HasMaxLength(100);
        compte.Property(c => c.Nom).HasMaxLength(100);

        // Le rôle et l'état sont lisibles en SQL : « Secretaire », « Invite ».
        compte.Property(c => c.Role).HasConversion<string>().HasMaxLength(20);
        compte.Property(c => c.Etat).HasConversion<string>().HasMaxLength(20);

        // Un centre qui a des comptes ne peut pas être supprimé.
        compte.HasOne<Centre>().WithMany().HasForeignKey(c => c.CentreId).OnDelete(DeleteBehavior.Restrict);

        // Un email est unique dans tout SchoolHub, tous centres confondus (CPT-04).
        compte.HasIndex(c => c.NormalizedEmail).IsUnique();
    }
}
