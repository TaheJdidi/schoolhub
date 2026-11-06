using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Modules.Caisse;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Achats;

/// <summary>RHA-04 : la catégorie d'un achat.</summary>
public enum CategorieDAchat
{
    /// <summary>Papier, produits, appareils.</summary>
    Materiel,

    /// <summary>Ménage, jardinage, électricité, l'assurance du centre.</summary>
    Service,
}

/// <summary>
/// RHA-04 : une dépense du centre. « Payé depuis ma caisse », c'est une sortie de caisse : elle baisse les espèces
/// attendues de la caisse du jour de son auteur (CAI-09), et sa clôture la range.
/// </summary>
public sealed class Achat : IAppartientAuCentre
{
    public const int LongueurMaximaleDuLibelle = 200;

    internal Achat(DateOnly jour, string libelle, CategorieDAchat categorie, decimal montant, string? fournisseur, bool payeDepuisLaCaisse,
        Guid auteurId, string auteur, DateTimeOffset creeLe)
    {
        Jour = jour;
        Libelle = libelle;
        Categorie = categorie;
        Montant = montant;
        Fournisseur = fournisseur;
        PayeDepuisLaCaisse = payeDepuisLaCaisse;
        AuteurId = auteurId;
        Auteur = auteur;
        CreeLe = creeLe;
    }

    // Pour EF Core.
    private Achat()
    {
        Libelle = null!;
        Auteur = null!;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public DateOnly Jour { get; private set; }

    public string Libelle { get; private set; }

    public CategorieDAchat Categorie { get; private set; }

    public decimal Montant { get; private set; }

    public string? Fournisseur { get; private set; }

    public bool PayeDepuisLaCaisse { get; private set; }

    public Guid AuteurId { get; private set; }

    /// <summary>Le prénom et le nom de l'auteur, figés.</summary>
    public string Auteur { get; private set; }

    public DateTimeOffset CreeLe { get; private set; }

    /// <summary>CAI-09 : la clôture qui a compté cette sortie de caisse ; <c>null</c> tant que sa caisse est ouverte.</summary>
    public Guid? ClotureId { get; private set; }

    internal void Ranger(Guid clotureId) => ClotureId = clotureId;

    /// <summary>Les nouvelles valeurs, déjà vérifiées. Une sortie rangée dans une clôture garde son montant et sa caisse.</summary>
    internal void Modifier(DateOnly jour, string libelle, CategorieDAchat categorie, decimal montant, string? fournisseur, bool payeDepuisLaCaisse)
    {
        Jour = jour;
        Libelle = libelle;
        Categorie = categorie;
        Fournisseur = fournisseur;
        if (ClotureId is not null)
            return;
        Montant = montant;
        PayeDepuisLaCaisse = payeDepuisLaCaisse;
    }
}

/// <summary>RHA-04 : la photo ou le PDF de la facture d'un achat, gardée à part pour ne pas alourdir la liste.</summary>
public sealed class FactureDAchat : IAppartientAuCentre
{
    /// <summary>5 Mo au plus.</summary>
    public const int TailleMaximale = 5 * 1024 * 1024;

    internal FactureDAchat(Guid achatId, string nom, string type, byte[] contenu)
    {
        AchatId = achatId;
        Nom = nom;
        Type = type;
        Contenu = contenu;
    }

    // Pour EF Core.
    private FactureDAchat()
    {
        Nom = null!;
        Type = null!;
        Contenu = null!;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid AchatId { get; private set; }

    public string Nom { get; private set; }

    public string Type { get; private set; }

    public byte[] Contenu { get; private set; }
}

internal sealed class AchatConfiguration : IEntityTypeConfiguration<Achat>
{
    public void Configure(EntityTypeBuilder<Achat> achat)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        achat.ToTable("achats");
        achat.Property(a => a.Libelle).HasMaxLength(Achat.LongueurMaximaleDuLibelle);
        achat.Property(a => a.Fournisseur).HasMaxLength(Achat.LongueurMaximaleDuLibelle);
        achat.Property(a => a.Categorie).HasConversion<string>().HasMaxLength(20);
        achat.Property(a => a.Montant).HasPrecision(12, 3);
        achat.Property(a => a.Auteur).HasMaxLength(201);
        achat.HasOne<Compte>().WithMany().HasForeignKey(a => a.AuteurId).OnDelete(DeleteBehavior.Restrict);
        achat.HasOne<ClotureDeCaisse>().WithMany().HasForeignKey(a => a.ClotureId).OnDelete(DeleteBehavior.Restrict);
        achat.HasIndex(a => a.Jour);
        achat.HasIndex(a => new { a.AuteurId, a.ClotureId });
    }
}

internal sealed class FactureDAchatConfiguration : IEntityTypeConfiguration<FactureDAchat>
{
    public void Configure(EntityTypeBuilder<FactureDAchat> facture)
    {
        facture.ToTable("factures_d_achat");
        facture.Property(f => f.Nom).HasMaxLength(200);
        facture.Property(f => f.Type).HasMaxLength(50);
        facture.HasOne<Achat>().WithMany().HasForeignKey(f => f.AchatId).OnDelete(DeleteBehavior.Cascade);
        facture.HasIndex(f => f.AchatId).IsUnique();
    }
}
