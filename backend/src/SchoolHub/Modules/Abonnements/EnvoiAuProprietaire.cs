using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Abonnements;

/// <summary>
/// Un message envoyé au propriétaire d'un centre, daté : une invitation (EDI-03), la confirmation d'un paiement (EDI-04),
/// une relance (EDI-06).
/// Par email, SchoolUp l'envoie lui-même ; par WhatsApp, c'est la date du clic de l'Éditeur sur le bouton,
/// qui ouvre WhatsApp avec le message déjà écrit.
/// </summary>
public sealed class EnvoiAuProprietaire(
    Guid centreId,
    ObjetDeLEnvoi objet,
    CanalDEnvoi canal,
    DateTimeOffset quand,
    string destinataire,
    Guid? paiementId = null,
    DateOnly? echeance = null)
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; } = centreId;

    public ObjetDeLEnvoi Objet { get; private set; } = objet;

    public CanalDEnvoi Canal { get; private set; } = canal;

    public DateTimeOffset Quand { get; private set; } = quand;

    /// <summary>L'email, ou le numéro WhatsApp (8 chiffres), auquel le message est parti.</summary>
    public string Destinataire { get; private set; } = destinataire;

    /// <summary>Le paiement que confirme le message (EDI-04).</summary>
    public Guid? PaiementId { get; private set; } = paiementId;

    /// <summary>La fin d'abonnement dont parle une relance (EDI-06) : une relance automatique part une fois par échéance.</summary>
    public DateOnly? Echeance { get; private set; } = echeance;
}

public enum ObjetDeLEnvoi
{
    Invitation,
    ConfirmationDePaiement,
    /// <summary>L'email automatique 7 jours avant la fin.</summary>
    RelanceAvantLaFin,
    /// <summary>L'email automatique du jour de la fin.</summary>
    RelanceDeLaFin,
    /// <summary>La relance que l'Éditeur envoie lui-même par WhatsApp, depuis la liste « à relancer ».</summary>
    Relance,
}

public enum CanalDEnvoi
{
    Email,
    WhatsApp,
}

internal sealed class EnvoiAuProprietaireConfiguration : IEntityTypeConfiguration<EnvoiAuProprietaire>
{
    public void Configure(EntityTypeBuilder<EnvoiAuProprietaire> envoi)
    {
        envoi.ToTable("envois_au_proprietaire");
        envoi.HasOne<Centre>().WithMany().HasForeignKey(e => e.CentreId).OnDelete(DeleteBehavior.Restrict);
        envoi.HasOne<PaiementDAbonnement>().WithMany().HasForeignKey(e => e.PaiementId).OnDelete(DeleteBehavior.Restrict);
        envoi.Property(e => e.Objet).HasConversion<string>().HasMaxLength(30);
        envoi.Property(e => e.Canal).HasConversion<string>().HasMaxLength(20);
        envoi.Property(e => e.Destinataire).HasMaxLength(256);
        envoi.HasIndex(e => e.CentreId);

        // Une relance automatique part une seule fois par échéance, même si deux serveurs la tentent ensemble (EDI-06).
        envoi.HasIndex(e => new { e.CentreId, e.Objet, e.Echeance })
            .HasDatabaseName("ix_envois_une_relance_automatique_par_echeance")
            .IsUnique()
            .HasFilter("objet IN ('RelanceAvantLaFin', 'RelanceDeLaFin')");
    }
}
