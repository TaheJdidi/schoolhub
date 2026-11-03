using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Socle.Centres;

internal sealed class CentreConfiguration : IEntityTypeConfiguration<Centre>
{
    public void Configure(EntityTypeBuilder<Centre> centre)
    {
        centre.Property(c => c.Nom).HasMaxLength(Centre.LongueurMaximaleDuNom);
        centre.Property(c => c.Code).HasMaxLength(100);

        // Un code de lien public déjà pris est refusé (CEN-01).
        centre.HasIndex(c => c.Code).IsUnique();

        centre.Property(c => c.Telephone).HasMaxLength(8);
        centre.Property(c => c.Email).HasMaxLength(AdresseEmail.LongueurMaximale);

        // Les montants sont au millime, et la part du centre a deux décimales au plus.
        centre.Property(c => c.MontantDeLAssurance).HasPrecision(12, 3);
        centre.Property(c => c.PrixDeLEntreeALaBibliotheque).HasPrecision(12, 3);
        centre.Property(c => c.PartDuCentreParDefaut).HasPrecision(5, 2);
    }
}
