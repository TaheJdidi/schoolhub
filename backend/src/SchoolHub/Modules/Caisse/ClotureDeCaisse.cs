using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Caisse;

/// <summary>
/// CAI-09 : le comptage des espèces d'une caisse, le soir. La plateforme compare le montant compté aux espèces attendues
/// (l'encaissé moins les sorties) et garde l'écart, figés. Le propriétaire marque « argent reçu » quand il récupère les
/// espèces (CAI-10). Les reçus comptés pointent vers leur clôture.
/// </summary>
public sealed class ClotureDeCaisse : IAppartientAuCentre
{
    internal ClotureDeCaisse(Guid compteId, string secretaire, DateOnly jour, int rang, decimal encaisse, decimal sorties, decimal compte,
        DateTimeOffset clotureeLe, decimal cheques = 0m, int nombreDeCheques = 0)
    {
        Cheques = cheques;
        NombreDeCheques = nombreDeCheques;
        CompteId = compteId;
        Secretaire = secretaire;
        Jour = jour;
        Rang = rang;
        Encaisse = encaisse;
        Sorties = sorties;
        Attendu = encaisse - sorties;
        Compte = compte;
        Ecart = compte - Attendu;
        ClotureeLe = clotureeLe;
    }

    // Pour EF Core.
    private ClotureDeCaisse()
    {
        Secretaire = null!;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    /// <summary>Le compte dont c'est la caisse : une secrétaire, ou le propriétaire qui encaisse.</summary>
    public Guid CompteId { get; private set; }

    /// <summary>Son prénom et son nom au moment de la clôture.</summary>
    public string Secretaire { get; private set; }

    /// <summary>Le jour de la caisse, à Tunis.</summary>
    public DateOnly Jour { get; private set; }

    /// <summary>1 pour la première caisse du jour ; 2 pour celle qu'ouvre un encaissement après la clôture.</summary>
    public int Rang { get; private set; }

    public decimal Encaisse { get; private set; }

    /// <summary>Les chèques de cette caisse : comptés à part, ils ne sont pas dans le tiroir.</summary>
    public decimal Cheques { get; private set; }

    public int NombreDeCheques { get; private set; }

    /// <summary>Les achats payés en espèces (RHA-04) ; 0 tant qu'ils ne sont pas construits.</summary>
    public decimal Sorties { get; private set; }

    public decimal Attendu { get; private set; }

    /// <summary>Le montant compté dans la caisse.</summary>
    public decimal Compte { get; private set; }

    /// <summary>Compté moins attendu : négatif, il manque de l'argent.</summary>
    public decimal Ecart { get; private set; }

    public DateTimeOffset ClotureeLe { get; private set; }

    /// <summary>CAI-10 : le moment où le propriétaire a récupéré les espèces ; <c>null</c> : « à récupérer ».</summary>
    public DateTimeOffset? ArgentRecuLe { get; private set; }

    public Guid? ArgentRecuParId { get; private set; }

    internal void MarquerLArgentRecu(bool recu, Guid auteurId, DateTimeOffset maintenant)
    {
        ArgentRecuLe = recu ? ArgentRecuLe ?? maintenant : null;
        ArgentRecuParId = recu ? ArgentRecuParId ?? auteurId : null;
    }
}

internal sealed class ClotureDeCaisseConfiguration : IEntityTypeConfiguration<ClotureDeCaisse>
{
    public void Configure(EntityTypeBuilder<ClotureDeCaisse> cloture)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        cloture.ToTable("clotures_de_caisse");
        cloture.Property(c => c.Secretaire).HasMaxLength(201);
        cloture.Property(c => c.Encaisse).HasPrecision(12, 3);
        // 0 par défaut : les clôtures d'avant les chèques, et le rangement des anciens reçus (CloturesHistoriques), n'en ont pas.
        cloture.Property(c => c.Cheques).HasPrecision(12, 3).HasDefaultValue(0m);
        cloture.Property(c => c.NombreDeCheques).HasDefaultValue(0);
        cloture.Property(c => c.Sorties).HasPrecision(12, 3);
        cloture.Property(c => c.Attendu).HasPrecision(12, 3);
        cloture.Property(c => c.Compte).HasPrecision(12, 3);
        cloture.Property(c => c.Ecart).HasPrecision(12, 3);
        cloture.HasOne<Compte>().WithMany().HasForeignKey(c => c.CompteId).OnDelete(DeleteBehavior.Restrict);
        cloture.HasOne<Compte>().WithMany().HasForeignKey(c => c.ArgentRecuParId).OnDelete(DeleteBehavior.Restrict);
        // Une caisse par rang, chaque jour, pour chaque compte.
        cloture.HasIndex(c => new { c.CompteId, c.Jour, c.Rang }).IsUnique();
        cloture.HasIndex(c => c.Jour);
    }
}
