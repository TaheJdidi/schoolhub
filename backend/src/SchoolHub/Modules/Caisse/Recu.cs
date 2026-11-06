using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Caisse;

/// <summary>
/// CAI-03 : la preuve d'un paiement en espèces, un par élève et par encaissement. Numéroté à la suite pour tout le
/// centre, il n'est jamais supprimé ni modifié : il garde ses lignes telles qu'encaissées. Annulé (CAI-07), il reste
/// visible avec son motif.
/// </summary>
public sealed class Recu : IAppartientAuCentre
{
    /// <summary>La taille de la colonne du motif d'annulation.</summary>
    public const int LongueurMaximaleDuMotif = 200;

    private readonly List<LigneDuRecu> _lignes = [];

    internal Recu(int numero, Guid eleveId, Guid auteurId, string auteur, DateTimeOffset creeLe, DateOnly jour, IEnumerable<LigneDuRecu> lignes)
    {
        Numero = numero;
        EleveId = eleveId;
        AuteurId = auteurId;
        Auteur = auteur;
        CreeLe = creeLe;
        Jour = jour;
        _lignes.AddRange(lignes);
        Total = _lignes.Sum(l => l.Montant);
    }

    // Pour EF Core.
    private Recu()
    {
        Auteur = null!;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    /// <summary>Le numéro, à la suite pour tout le centre : 1, 2, 3…, sans trou.</summary>
    public int Numero { get; private set; }

    public Guid EleveId { get; private set; }

    /// <summary>La secrétaire (ou le propriétaire) qui a encaissé : c'est sa caisse.</summary>
    public Guid AuteurId { get; private set; }

    /// <summary>Son prénom et son nom au moment de l'encaissement, tels qu'imprimés sur le reçu.</summary>
    public string Auteur { get; private set; }

    public DateTimeOffset CreeLe { get; private set; }

    /// <summary>Le jour de l'encaissement, à Tunis : celui de la caisse.</summary>
    public DateOnly Jour { get; private set; }

    public decimal Total { get; private set; }

    public IReadOnlyList<LigneDuRecu> Lignes => _lignes;

    /// <summary>CAI-07 : le moment de l'annulation ; <c>null</c> tant que le reçu vaut.</summary>
    public DateTimeOffset? AnnuleLe { get; private set; }

    public string? MotifDAnnulation { get; private set; }

    public Guid? AnnuleParId { get; private set; }

    internal void Annuler(Guid auteurId, string motif, DateTimeOffset maintenant)
    {
        AnnuleLe = maintenant;
        MotifDAnnulation = motif;
        AnnuleParId = auteurId;
    }
}

/// <summary>
/// Une ligne du reçu : le groupe (« Mathématiques · Groupe Samedi 10h → 12h », ou « Assurance »), ce qui est payé
/// (« Mois 3 »), le montant, et la somme qu'elle règle (l'inscription et sa clé, ou l'assurance). Elle fait partie du reçu.
/// </summary>
public sealed class LigneDuRecu(string groupe, string libelle, decimal montant, Guid? inscriptionId, Guid? assuranceId, string cle)
{
    public string Groupe { get; private set; } = groupe;

    public string Libelle { get; private set; } = libelle;

    public decimal Montant { get; private set; } = montant;

    public Guid? InscriptionId { get; private set; } = inscriptionId;

    public Guid? AssuranceId { get; private set; } = assuranceId;

    public string Cle { get; private set; } = cle;
}

internal sealed class RecuConfiguration : IEntityTypeConfiguration<Recu>
{
    public void Configure(EntityTypeBuilder<Recu> recu)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        recu.ToTable("recus");
        recu.Property(r => r.Auteur).HasMaxLength(201);
        recu.Property(r => r.Total).HasPrecision(12, 3);
        recu.Property(r => r.MotifDAnnulation).HasMaxLength(Recu.LongueurMaximaleDuMotif);
        // Le numéro se suit pour tout le centre, sans doublon.
        recu.HasIndex(r => new { r.CentreId, r.Numero }).IsUnique();
        recu.HasIndex(r => new { r.Jour, r.AuteurId });
        recu.HasOne<Eleve>().WithMany().HasForeignKey(r => r.EleveId).OnDelete(DeleteBehavior.Restrict);
        recu.HasOne<Compte>().WithMany().HasForeignKey(r => r.AuteurId).OnDelete(DeleteBehavior.Restrict);
        recu.HasOne<Compte>().WithMany().HasForeignKey(r => r.AnnuleParId).OnDelete(DeleteBehavior.Restrict);
        recu.OwnsMany(r => r.Lignes, ligne =>
        {
            ligne.ToTable("lignes_des_recus");
            ligne.Property(l => l.Groupe).HasMaxLength(250);
            ligne.Property(l => l.Libelle).HasMaxLength(100);
            ligne.Property(l => l.Cle).HasMaxLength(30);
            ligne.Property(l => l.Montant).HasPrecision(12, 3);
        });
        // Les règlements d'un encaissement pointent vers leur reçu.
        recu.HasMany<Reglement>().WithOne().HasForeignKey(r => r.RecuId).OnDelete(DeleteBehavior.Restrict);
    }
}
