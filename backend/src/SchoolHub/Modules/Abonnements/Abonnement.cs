using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Abonnements;

/// <summary>
/// L'abonnement d'un centre : le droit d'utiliser SchoolUp que l'Éditeur accorde et suspend (EDI-01 à EDI-08).
/// Une seule offre, avec un prix mensuel que l'Éditeur ajuste par centre. C'est une donnée de l'Éditeur sur le centre,
/// lue dans le back office pour tous les centres : elle n'est pas filtrée par centre, et chaque lecture côté centre
/// passe par le centre courant.
/// </summary>
public sealed class Abonnement(Guid centreId, decimal prixMensuel)
{
    public const int LongueurMaximaleDuMotif = 300;

    public Guid CentreId { get; private set; } = centreId;

    /// <summary>Le prix mensuel de ce centre, en dinars : celui de l'offre à la création, puis celui que l'Éditeur négocie.</summary>
    public decimal PrixMensuel { get; private set; } = prixMensuel;

    /// <summary>
    /// Le dernier jour payé (inclus) : la fin de période la plus tardive des paiements d'abonnement,
    /// ou <c>null</c> tant qu'aucun paiement n'est saisi (En attente).
    /// </summary>
    public DateOnly? PayeJusquAu { get; private set; }

    /// <summary>L'état posé à la main par l'Éditeur, qui l'emporte sur le calcul ; <c>null</c> sinon.</summary>
    public ForcageDeLEtat? Forcage { get; private set; }

    /// <summary>Pourquoi l'Éditeur a suspendu ou réactivé ce centre à la main.</summary>
    public string? MotifDuForcage { get; private set; }

    /// <summary>Le jour où l'Éditeur a suspendu ou réactivé ce centre à la main.</summary>
    public DateOnly? ForceLe { get; private set; }

    /// <summary>La fin de l'accès support que le propriétaire a donné (EDI-07), ou <c>null</c>.</summary>
    public DateTimeOffset? SupportAutoriseJusquAu { get; private set; }

    public void ChangerLePrix(decimal prix) => PrixMensuel = prix;

    public void AutoriserLeSupport(DateTimeOffset jusquAu) => SupportAutoriseJusquAu = jusquAu;

    public void RetirerLeSupport() => SupportAutoriseJusquAu = null;

    /// <summary>L'accès support est-il ouvert à ce moment ?</summary>
    public bool SupportOuvert(DateTimeOffset maintenant) => SupportAutoriseJusquAu is { } fin && maintenant < fin;

    /// <summary>Un paiement prolonge l'abonnement jusqu'à la fin de sa période, et lève tout état posé à la main (EDI-05).</summary>
    public void Prolonger(DateOnly jusquAu)
    {
        if (PayeJusquAu is null || jusquAu > PayeJusquAu)
            PayeJusquAu = jusquAu;
        Forcage = null;
        MotifDuForcage = null;
        ForceLe = null;
    }

    public void Forcer(ForcageDeLEtat? forcage, string motif, DateOnly aujourdhui)
    {
        Forcage = forcage;
        MotifDuForcage = motif;
        ForceLe = aujourdhui;
    }
}

/// <summary>L'état que l'Éditeur pose à la main : une suspension, ou une réactivation d'un centre qui serait suspendu.</summary>
public enum ForcageDeLEtat
{
    Suspendu,
    Actif,
}

internal sealed class AbonnementConfiguration : IEntityTypeConfiguration<Abonnement>
{
    public void Configure(EntityTypeBuilder<Abonnement> abonnement)
    {
        abonnement.ToTable("abonnements");
        abonnement.HasKey(a => a.CentreId);
        abonnement.HasOne<Centre>().WithOne().HasForeignKey<Abonnement>(a => a.CentreId).OnDelete(DeleteBehavior.Restrict);
        abonnement.Property(a => a.PrixMensuel).HasPrecision(12, 3);
        abonnement.Property(a => a.Forcage).HasConversion<string>().HasMaxLength(20);
        abonnement.Property(a => a.MotifDuForcage).HasMaxLength(Abonnement.LongueurMaximaleDuMotif);
    }
}
