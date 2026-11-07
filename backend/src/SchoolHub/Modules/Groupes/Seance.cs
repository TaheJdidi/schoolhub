using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Groupes;

/// <summary>D'où vient une séance (ticket 02, Q4).</summary>
public enum NatureDeLaSeance
{
    /// <summary>Créée depuis les créneaux du groupe (GRP-04 à GRP-06, SEA-06).</summary>
    Prevue,

    /// <summary>SEA-04 : ajoutée pour remplacer une séance annulée ; elle compte à sa place dans le Mois.</summary>
    Remplacement,

    /// <summary>SEA-05 : offerte, en plus ; elle a son appel mais ne compte pas dans le Mois.</summary>
    Supplementaire,
}

/// <summary>
/// Une séance d'un groupe. Une séance annulée (SEA-03) ou supplémentaire (SEA-05) ne compte pas : les Mois, les numéros et
/// ce que doivent les élèves se calculent sur les séances qui comptent (<see cref="SeancesQuiComptent"/>).
/// </summary>
public sealed class Seance : IAppartientAuCentre
{
    /// <summary>La taille de la colonne du motif d'annulation.</summary>
    public const int LongueurMaximaleDuMotif = 200;

    internal Seance(Guid groupeId, DateOnly date, TimeOnly debut, TimeOnly fin, NatureDeLaSeance nature = NatureDeLaSeance.Prevue, Guid? remplace = null)
    {
        GroupeId = groupeId;
        Date = date;
        Debut = debut;
        Fin = fin;
        Nature = nature;
        RemplaceLaSeanceId = remplace;
    }

    // Pour EF Core.
    private Seance()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid GroupeId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly Debut { get; private set; }

    public TimeOnly Fin { get; private set; }

    public NatureDeLaSeance Nature { get; private set; } = NatureDeLaSeance.Prevue;

    /// <summary>SEA-04 : pour une séance de remplacement, la séance annulée qu'elle remplace.</summary>
    public Guid? RemplaceLaSeanceId { get; private set; }

    /// <summary>SEA-03 : le moment de l'annulation ; <c>null</c> tant que la séance a lieu.</summary>
    public DateTimeOffset? AnnuleeLe { get; private set; }

    /// <summary>SEA-03 : « professeur absent ».</summary>
    public string? MotifDAnnulation { get; private set; }

    /// <summary>SEA-07 : le moment où l'appel a été enregistré la première fois ; <c>null</c> : « appel à faire ».</summary>
    public DateTimeOffset? AppelFaitLe { get; private set; }

    /// <summary>SEA-07 : qui a enregistré l'appel la première fois, « Amira Jlassi » ; <c>null</c> avant, ou pour un appel d'avant ce champ.</summary>
    public string? AppelFaitPar { get; private set; }

    /// <summary>Une séance qui compte dans les Mois et dans ce que doivent les élèves.</summary>
    public bool Compte => AnnuleeLe is null && Nature != NatureDeLaSeance.Supplementaire;

    /// <summary>SEA-07 : la séance passe à « appel fait ». Une correction (SEA-10) garde le premier moment.</summary>
    internal void MarquerLAppelFait(DateTimeOffset maintenant, string auteur)
    {
        if (AppelFaitLe is not null)
            return;
        AppelFaitLe = maintenant;
        AppelFaitPar = auteur;
    }

    /// <summary>SEA-03 : la séance n'aura pas lieu ; elle reste visible, avec son motif.</summary>
    internal void Annuler(string motif, DateTimeOffset maintenant)
    {
        AnnuleeLe = maintenant;
        MotifDAnnulation = motif;
    }
}

/// <summary>Les filtres des séances, à utiliser partout où elles se comptent.</summary>
public static class SeancesQuiComptent
{
    /// <summary>Ni annulées, ni supplémentaires : les séances des Mois et de ce qui est dû.</summary>
    public static IQueryable<Seance> QuiComptent(this IQueryable<Seance> seances) =>
        seances.Where(s => s.AnnuleeLe == null && s.Nature != NatureDeLaSeance.Supplementaire);

    /// <summary>Pas annulées : les séances qui ont lieu, supplémentaires comprises.</summary>
    public static IQueryable<Seance> QuiOntLieu(this IQueryable<Seance> seances) => seances.Where(s => s.AnnuleeLe == null);
}

internal sealed class SeanceConfiguration : IEntityTypeConfiguration<Seance>
{
    public void Configure(EntityTypeBuilder<Seance> seance)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        seance.ToTable("seances");
        seance.Property(s => s.Nature).HasConversion<string>().HasMaxLength(20);
        seance.Property(s => s.MotifDAnnulation).HasMaxLength(Seance.LongueurMaximaleDuMotif);
        seance.Property(s => s.AppelFaitPar).HasMaxLength(201);
        seance.Ignore(s => s.Compte);
        seance.HasOne<Groupe>().WithMany().HasForeignKey(s => s.GroupeId).OnDelete(DeleteBehavior.Restrict);
        // Une séance annulée retirée par la fermeture du groupe (GRP-08) laisse son remplacement sans lien.
        seance.HasOne<Seance>().WithMany().HasForeignKey(s => s.RemplaceLaSeanceId).OnDelete(DeleteBehavior.SetNull);
        seance.HasIndex(s => new { s.GroupeId, s.Date });
    }
}
