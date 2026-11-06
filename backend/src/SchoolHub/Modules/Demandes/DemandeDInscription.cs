using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Demandes;

/// <summary>Où en est une demande : à traiter, puis inscrite (DEM-04) ou refusée (DEM-05). Une demande traitée le reste.</summary>
public enum EtatDeLaDemande
{
    ATraiter,
    Inscrite,
    Refusee,
}

/// <summary>DEM-05 : pourquoi la secrétaire refuse une demande.</summary>
public enum MotifDuRefus
{
    Doublon,
    FausseDemande,
}

/// <summary>
/// DEM-01 : une demande d'inscription, envoyée depuis le formulaire public du centre. Ce n'est pas encore un élève : la
/// secrétaire l'enregistre ensuite à la main (ticket 04). Elle garde la fiche telle qu'envoyée, ses groupes préférés (un
/// par matière) et les amis cités.
/// </summary>
public sealed class DemandeDInscription : IAppartientAuCentre
{
    /// <summary>La taille de la colonne des amis cités.</summary>
    public const int LongueurMaximaleDesAmis = 500;

    private readonly List<GroupeDeLaDemande> _groupesPreferes = [];

    internal DemandeDInscription(Coordonnees fiche, IEnumerable<Guid> groupesPreferes, string? amis, DateTimeOffset recueLe)
    {
        Prenom = fiche.Prenom;
        Nom = fiche.Nom;
        NiveauId = fiche.NiveauId;
        Majeur = fiche.Majeur;
        TelephoneParent = fiche.TelephoneParent;
        EmailParent = fiche.EmailParent;
        TelephoneEleve = fiche.TelephoneEleve;
        EmailEleve = fiche.EmailEleve;
        Etablissement = fiche.Etablissement;
        NomDeRecherche = Eleve.Normaliser($"{fiche.Prenom} {fiche.Nom}");
        Amis = amis;
        RecueLe = recueLe;
        _groupesPreferes.AddRange(groupesPreferes.Select(id => new GroupeDeLaDemande(id)));
    }

    // Pour EF Core.
    private DemandeDInscription()
    {
        Prenom = null!;
        Nom = null!;
        NomDeRecherche = null!;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public string Prenom { get; private set; }

    public string Nom { get; private set; }

    public Guid NiveauId { get; private set; }

    public bool Majeur { get; private set; }

    public string? TelephoneParent { get; private set; }

    public string? EmailParent { get; private set; }

    public string? TelephoneEleve { get; private set; }

    public string? EmailEleve { get; private set; }

    public string? Etablissement { get; private set; }

    /// <summary>« hedi ben salah » : pour le « Doublon possible » de la liste (DEM-03).</summary>
    public string NomDeRecherche { get; private set; }

    /// <summary>Les amis qui suivront le même groupe, tels qu'écrits : la secrétaire les voit en choisissant le groupe.</summary>
    public string? Amis { get; private set; }

    public DateTimeOffset RecueLe { get; private set; }

    public EtatDeLaDemande Etat { get; private set; } = EtatDeLaDemande.ATraiter;

    /// <summary>DEM-05 : le motif d'une demande refusée.</summary>
    public MotifDuRefus? Motif { get; private set; }

    /// <summary>DEM-04 : l'élève inscrit depuis cette demande, nouvelle fiche ou fiche existante.</summary>
    public Guid? EleveId { get; private set; }

    /// <summary>Le moment où la demande a été inscrite ou refusée.</summary>
    public DateTimeOffset? TraiteeLe { get; private set; }

    /// <summary>Le groupe préféré de chaque matière demandée, dans l'ordre du formulaire.</summary>
    public IReadOnlyList<GroupeDeLaDemande> GroupesPreferes => _groupesPreferes;

    /// <summary>DEM-04 : la demande devient l'inscription de cet élève.</summary>
    internal void Inscrire(Guid eleveId, DateTimeOffset maintenant)
    {
        Etat = EtatDeLaDemande.Inscrite;
        EleveId = eleveId;
        TraiteeLe = maintenant;
    }

    /// <summary>DEM-05 : la demande sort des demandes à traiter, avec son motif.</summary>
    internal void Refuser(MotifDuRefus motif, DateTimeOffset maintenant)
    {
        Etat = EtatDeLaDemande.Refusee;
        Motif = motif;
        TraiteeLe = maintenant;
    }
}

/// <summary>Le groupe préféré pour une matière d'une demande ; il fait partie de sa demande.</summary>
public sealed class GroupeDeLaDemande(Guid groupeId)
{
    public Guid GroupeId { get; private set; } = groupeId;
}

internal sealed class DemandeDInscriptionConfiguration : IEntityTypeConfiguration<DemandeDInscription>
{
    public void Configure(EntityTypeBuilder<DemandeDInscription> demande)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        demande.ToTable("demandes_d_inscription");
        demande.Property(d => d.Prenom).HasMaxLength(Eleve.LongueurMaximaleDuNom);
        demande.Property(d => d.Nom).HasMaxLength(Eleve.LongueurMaximaleDuNom);
        demande.Property(d => d.NomDeRecherche).HasMaxLength(2 * Eleve.LongueurMaximaleDuNom + 1);
        demande.Property(d => d.TelephoneParent).HasMaxLength(8);
        demande.Property(d => d.TelephoneEleve).HasMaxLength(8);
        demande.Property(d => d.EmailParent).HasMaxLength(AdresseEmail.LongueurMaximale);
        demande.Property(d => d.EmailEleve).HasMaxLength(AdresseEmail.LongueurMaximale);
        demande.Property(d => d.Etablissement).HasMaxLength(Eleve.LongueurMaximaleDeLEtablissement);
        demande.Property(d => d.Amis).HasMaxLength(DemandeDInscription.LongueurMaximaleDesAmis);
        demande.Property(d => d.Etat).HasConversion<string>().HasMaxLength(20);
        demande.Property(d => d.Motif).HasConversion<string>().HasMaxLength(20);
        demande.HasOne<Niveau>().WithMany().HasForeignKey(d => d.NiveauId).OnDelete(DeleteBehavior.Restrict);
        demande.HasOne<Eleve>().WithMany().HasForeignKey(d => d.EleveId).OnDelete(DeleteBehavior.Restrict);
        demande.HasIndex(d => new { d.Etat, d.RecueLe });

        demande.OwnsMany(d => d.GroupesPreferes, groupe =>
        {
            groupe.ToTable("groupes_demandes");
            groupe.HasOne<Groupe>().WithMany().HasForeignKey(g => g.GroupeId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
