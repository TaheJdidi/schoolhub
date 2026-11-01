using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SchoolHub.Socle.Centres;

internal sealed class CentreConfiguration : IEntityTypeConfiguration<Centre>
{
    public void Configure(EntityTypeBuilder<Centre> centre)
    {
        centre.Property(c => c.Nom).HasMaxLength(200);
        centre.Property(c => c.Code).HasMaxLength(100);

        // Un code de lien public déjà pris est refusé (CEN-01).
        centre.HasIndex(c => c.Code).IsUnique();
    }
}
