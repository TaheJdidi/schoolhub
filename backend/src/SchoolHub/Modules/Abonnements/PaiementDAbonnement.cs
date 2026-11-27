using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Abonnements;

/// <summary>
/// Un paiement du centre à l'Éditeur, saisi à la main (EDI-02) : « 270,000 TND, virement, du 05/01 au 04/04, facture F-2027-003 ».
/// La facture est faite hors de SchoolUp. À ne pas confondre avec le paiement d'un élève. Comme l'abonnement, c'est une donnée
/// de l'Éditeur, non filtrée par centre.
/// </summary>
public sealed class PaiementDAbonnement(
    Guid centreId,
    DateOnly date,
    decimal montant,
    ModeDePaiement mode,
    DateOnly du,
    DateOnly au,
    string numeroDeFacture,
    DateTimeOffset saisiLe,
    string saisiPar)
{
    public const int LongueurMaximaleDuNumeroDeFacture = 50;

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; } = centreId;

    /// <summary>Le jour où le centre a payé.</summary>
    public DateOnly Date { get; private set; } = date;

    /// <summary>En dinars, au millime.</summary>
    public decimal Montant { get; private set; } = montant;

    public ModeDePaiement Mode { get; private set; } = mode;

    /// <summary>Le premier jour de la période couverte.</summary>
    public DateOnly Du { get; private set; } = du;

    /// <summary>Le dernier jour de la période couverte, inclus.</summary>
    public DateOnly Au { get; private set; } = au;

    public string NumeroDeFacture { get; private set; } = numeroDeFacture;

    public DateTimeOffset SaisiLe { get; private set; } = saisiLe;

    /// <summary>Le prénom et le nom de l'Éditeur qui l'a saisi.</summary>
    public string SaisiPar { get; private set; } = saisiPar;
}

/// <summary>Comment le centre a payé l'Éditeur.</summary>
public enum ModeDePaiement
{
    Virement,
    Especes,
    Cheque,
}

internal sealed class PaiementDAbonnementConfiguration : IEntityTypeConfiguration<PaiementDAbonnement>
{
    public void Configure(EntityTypeBuilder<PaiementDAbonnement> paiement)
    {
        paiement.ToTable("paiements_d_abonnement");
        paiement.HasOne<Centre>().WithMany().HasForeignKey(p => p.CentreId).OnDelete(DeleteBehavior.Restrict);
        paiement.Property(p => p.Montant).HasPrecision(12, 3);
        paiement.Property(p => p.Mode).HasConversion<string>().HasMaxLength(20);
        paiement.Property(p => p.NumeroDeFacture).HasMaxLength(PaiementDAbonnement.LongueurMaximaleDuNumeroDeFacture);
        paiement.Property(p => p.SaisiPar).HasMaxLength(201);
        paiement.HasIndex(p => p.CentreId);
    }
}
