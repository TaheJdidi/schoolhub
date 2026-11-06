using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Parts;

/// <summary>
/// PRT-02 : une part marquée payée, figée : le montant du groupe, le pourcentage et la part du centre, l'avance déduite
/// et le montant versé, avec la date. « Non payée » la retire : la ligne se recalcule.
/// </summary>
public sealed class PartPayee : IAppartientAuCentre
{
    internal PartPayee(Guid groupeId, string cle, Guid professeurId, decimal montantDuGroupe, decimal pourcentage, decimal partDuCentre,
        decimal avanceDeduite, decimal aVerser, DateOnly payeeLe)
    {
        GroupeId = groupeId;
        Cle = cle;
        ProfesseurId = professeurId;
        MontantDuGroupe = montantDuGroupe;
        Pourcentage = pourcentage;
        PartDuCentre = partDuCentre;
        AvanceDeduite = avanceDeduite;
        AVerser = aVerser;
        PayeeLe = payeeLe;
    }

    // Pour EF Core.
    private PartPayee()
    {
        Cle = null!;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid GroupeId { get; private set; }

    /// <summary>« Mois 3 », ou « Groupe » pour une Révision ou une Formation.</summary>
    public string Cle { get; private set; }

    public Guid ProfesseurId { get; private set; }

    public decimal MontantDuGroupe { get; private set; }

    public decimal Pourcentage { get; private set; }

    public decimal PartDuCentre { get; private set; }

    public decimal AvanceDeduite { get; private set; }

    public decimal AVerser { get; private set; }

    public DateOnly PayeeLe { get; private set; }
}

/// <summary>PRT-03 : de l'argent avancé à un professeur, déduit de ses parts suivantes.</summary>
public sealed class Avance : IAppartientAuCentre
{
    internal Avance(Guid professeurId, decimal montant, DateOnly le)
    {
        ProfesseurId = professeurId;
        Montant = montant;
        Le = le;
    }

    // Pour EF Core.
    private Avance()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid ProfesseurId { get; private set; }

    public decimal Montant { get; private set; }

    public DateOnly Le { get; private set; }
}

/// <summary>
/// PRT-04 : la part du centre convenue avec un professeur, à partir d'un jour : elle vaut pour les périodes qui
/// commencent après. Sans pourcentage, le professeur revient à la part du centre. Chaque changement est gardé.
/// </summary>
public sealed class PartDUnProfesseur : IAppartientAuCentre
{
    internal PartDUnProfesseur(Guid professeurId, decimal? pourcentage, DateOnly changeeLe)
    {
        ProfesseurId = professeurId;
        Pourcentage = pourcentage;
        ChangeeLe = changeeLe;
    }

    // Pour EF Core.
    private PartDUnProfesseur()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid ProfesseurId { get; private set; }

    public decimal? Pourcentage { get; private set; }

    public DateOnly ChangeeLe { get; private set; }
}

internal sealed class PartPayeeConfiguration : IEntityTypeConfiguration<PartPayee>
{
    public void Configure(EntityTypeBuilder<PartPayee> part)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        part.ToTable("parts_payees");
        part.Property(p => p.Cle).HasMaxLength(30);
        part.Property(p => p.MontantDuGroupe).HasPrecision(12, 3);
        part.Property(p => p.Pourcentage).HasPrecision(5, 2);
        part.Property(p => p.PartDuCentre).HasPrecision(12, 3);
        part.Property(p => p.AvanceDeduite).HasPrecision(12, 3);
        part.Property(p => p.AVerser).HasPrecision(12, 3);
        part.HasOne<Groupe>().WithMany().HasForeignKey(p => p.GroupeId).OnDelete(DeleteBehavior.Restrict);
        part.HasOne<Professeur>().WithMany().HasForeignKey(p => p.ProfesseurId).OnDelete(DeleteBehavior.Restrict);
        part.HasIndex(p => new { p.GroupeId, p.Cle }).IsUnique();
    }
}

internal sealed class AvanceConfiguration : IEntityTypeConfiguration<Avance>
{
    public void Configure(EntityTypeBuilder<Avance> avance)
    {
        avance.ToTable("avances");
        avance.Property(a => a.Montant).HasPrecision(12, 3);
        avance.HasOne<Professeur>().WithMany().HasForeignKey(a => a.ProfesseurId).OnDelete(DeleteBehavior.Restrict);
        avance.HasIndex(a => a.ProfesseurId);
    }
}

internal sealed class PartDUnProfesseurConfiguration : IEntityTypeConfiguration<PartDUnProfesseur>
{
    public void Configure(EntityTypeBuilder<PartDUnProfesseur> part)
    {
        part.ToTable("parts_des_professeurs");
        part.Property(p => p.Pourcentage).HasPrecision(5, 2);
        part.HasOne<Professeur>().WithMany().HasForeignKey(p => p.ProfesseurId).OnDelete(DeleteBehavior.Restrict);
        part.HasIndex(p => p.ProfesseurId);
    }
}
