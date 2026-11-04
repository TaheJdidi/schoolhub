using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Inscriptions;

/// <summary>
/// ELV-03 : le rattachement d'un élève à un groupe, avec son mode de tarification et la date de sa première séance.
/// Une date plutôt qu'une séance : une séance annulée plus tard (SEA) ne casse rien, la suivante devient la première.
/// Les paiements de l'étape suivante viendront s'y rattacher.
/// </summary>
public sealed class Inscription : IAppartientAuCentre
{
    internal Inscription(Guid eleveId, Guid groupeId, ModeDeTarification mode, DateOnly debutLe, DateTimeOffset inscritLe)
    {
        EleveId = eleveId;
        GroupeId = groupeId;
        Mode = mode;
        DebutLe = debutLe;
        InscritLe = inscritLe;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid EleveId { get; private set; }

    public Guid GroupeId { get; private set; }

    public ModeDeTarification Mode { get; private set; }

    /// <summary>La date de la première séance de l'élève : l'échéancier part de la séance de ce jour, ou de la suivante.</summary>
    public DateOnly DebutLe { get; private set; }

    public DateTimeOffset InscritLe { get; private set; }
}

/// <summary>
/// L'assurance d'un élève pour une année scolaire : créée à sa première inscription en Étude de l'année, au montant des
/// paramètres du centre ce jour-là (ce qui est dû garde sa valeur). Payer et dispenser viendront ensuite (ELV-06).
/// </summary>
public sealed class AssuranceDeLAnnee : IAppartientAuCentre
{
    internal AssuranceDeLAnnee(Guid eleveId, Guid anneeScolaireId, decimal montant)
    {
        EleveId = eleveId;
        AnneeScolaireId = anneeScolaireId;
        Montant = montant;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid EleveId { get; private set; }

    public Guid AnneeScolaireId { get; private set; }

    public decimal Montant { get; private set; }

    /// <summary>« Assurance 2027-2028 ».</summary>
    public static string Libelle(AnneeScolaire annee) => $"Assurance {Annee(annee)}";

    /// <summary>« 2027-2028 ».</summary>
    public static string Annee(AnneeScolaire annee) => $"{annee.Debut.Year}-{annee.Fin.Year}";
}

internal sealed class InscriptionConfiguration : IEntityTypeConfiguration<Inscription>
{
    public void Configure(EntityTypeBuilder<Inscription> inscription)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        inscription.ToTable("inscriptions");
        inscription.Property(i => i.Mode).HasConversion<string>().HasMaxLength(20);
        inscription.HasOne<Eleve>().WithMany().HasForeignKey(i => i.EleveId).OnDelete(DeleteBehavior.Restrict);
        inscription.HasOne<Groupe>().WithMany().HasForeignKey(i => i.GroupeId).OnDelete(DeleteBehavior.Restrict);
        // Un élève n'est inscrit qu'une fois dans un groupe ; l'arrêt (ELV-08) rendra la réinscription possible.
        inscription.HasIndex(i => new { i.EleveId, i.GroupeId }).IsUnique();
        inscription.HasIndex(i => i.GroupeId);
    }
}

internal sealed class AssuranceDeLAnneeConfiguration : IEntityTypeConfiguration<AssuranceDeLAnnee>
{
    public void Configure(EntityTypeBuilder<AssuranceDeLAnnee> assurance)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        assurance.ToTable("assurances");
        assurance.Property(a => a.Montant).HasPrecision(12, 3);
        assurance.HasOne<Eleve>().WithMany().HasForeignKey(a => a.EleveId).OnDelete(DeleteBehavior.Restrict);
        assurance.HasOne<AnneeScolaire>().WithMany().HasForeignKey(a => a.AnneeScolaireId).OnDelete(DeleteBehavior.Restrict);
        assurance.HasIndex(a => new { a.EleveId, a.AnneeScolaireId }).IsUnique();
    }
}
