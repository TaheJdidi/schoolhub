using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Inscriptions;

/// <summary>D'où vient un règlement : noté « payé avant SchoolHub » (ELV-07), ou encaissé avec un reçu (CAI-03).</summary>
public enum OrigineDuReglement
{
    AvantSchoolHub,
    Encaissement,
}

/// <summary>
/// Une somme payée : celle d'une inscription (désignée par sa clé, « Mois 3 », « Tranche 2 »…) ou l'assurance de l'année.
/// Il garde son montant : un prix spécial accordé ensuite ne change pas ce qui est payé (ELV-05). Une somme ne se règle
/// qu'une fois.
/// </summary>
public sealed class Reglement : IAppartientAuCentre
{
    /// <summary>La clé d'un règlement de l'assurance de l'année.</summary>
    public const string CleDeLAssurance = "Assurance";

    private Reglement(
        Guid? inscriptionId, Guid? assuranceId, string cle, decimal montant, OrigineDuReglement origine, DateTimeOffset regleLe, Guid auteurId,
        decimal partDuReport = 0m)
    {
        PartDuReport = partDuReport;
        InscriptionId = inscriptionId;
        AssuranceId = assuranceId;
        Cle = cle;
        Montant = montant;
        Origine = origine;
        RegleLe = regleLe;
        AuteurId = auteurId;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid? InscriptionId { get; private set; }

    public Guid? AssuranceId { get; private set; }

    public string Cle { get; private set; }

    public decimal Montant { get; private set; }

    public OrigineDuReglement Origine { get; private set; }

    public DateTimeOffset RegleLe { get; private set; }

    public Guid AuteurId { get; private set; }

    /// <summary>ELV-09 : la part du report qui a réduit cette somme ; figée au paiement, elle ne sert pas une seconde fois.</summary>
    public decimal PartDuReport { get; private set; }

    /// <summary>CAI-03 : le reçu de l'encaissement. L'annulation du reçu (CAI-07) retire ses règlements : la somme redevient à payer.</summary>
    public Guid? RecuId { get; private set; }

    internal static Reglement DUneSomme(
        Guid inscriptionId, string cle, decimal montant, OrigineDuReglement origine, DateTimeOffset regleLe, Guid auteurId, decimal partDuReport = 0m,
        Guid? recuId = null) =>
        new(inscriptionId, null, cle, montant, origine, regleLe, auteurId, partDuReport) { RecuId = recuId };

    internal static Reglement DeLAssurance(Guid assuranceId, decimal montant, OrigineDuReglement origine, DateTimeOffset regleLe, Guid auteurId,
        Guid? recuId = null) =>
        new(null, assuranceId, CleDeLAssurance, montant, origine, regleLe, auteurId) { RecuId = recuId };
}

internal sealed class ReglementConfiguration : IEntityTypeConfiguration<Reglement>
{
    public void Configure(EntityTypeBuilder<Reglement> reglement)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        reglement.ToTable("reglements");
        reglement.Property(r => r.Cle).HasMaxLength(30);
        reglement.Property(r => r.Montant).HasPrecision(12, 3);
        reglement.Property(r => r.PartDuReport).HasPrecision(12, 3);
        reglement.Property(r => r.Origine).HasConversion<string>().HasMaxLength(20);
        reglement.HasOne<Inscription>().WithMany().HasForeignKey(r => r.InscriptionId).OnDelete(DeleteBehavior.Restrict);
        reglement.HasOne<AssuranceDeLAnnee>().WithMany().HasForeignKey(r => r.AssuranceId).OnDelete(DeleteBehavior.Restrict);
        reglement.HasOne<Compte>().WithMany().HasForeignKey(r => r.AuteurId).OnDelete(DeleteBehavior.Restrict);
        // Une somme ne se règle qu'une fois : deux clics ou deux onglets n'en font pas deux règlements.
        reglement.HasIndex(r => new { r.InscriptionId, r.Cle }).IsUnique().HasFilter("inscription_id IS NOT NULL");
        reglement.HasIndex(r => r.AssuranceId).IsUnique().HasFilter("assurance_id IS NOT NULL");
        reglement.ToTable(t => t.HasCheckConstraint("ck_reglements_une_somme", "(inscription_id IS NULL) <> (assurance_id IS NULL)"));
    }
}
