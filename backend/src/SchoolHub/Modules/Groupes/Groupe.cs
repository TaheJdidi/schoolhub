using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Groupes;

/// <summary>Le type d'un groupe. Révision et Formation viendront avec GRP-05 et GRP-06.</summary>
public enum TypeDeGroupe
{
    Etude,
}

/// <summary>
/// Un groupe (GRP-04) : un professeur, une matière, un niveau, un ou plusieurs créneaux par semaine et un prix.
/// Ses séances sont enregistrées à part (<see cref="Seance"/>) ; ses Mois se calculent (<see cref="CalendrierDuGroupe"/>).
/// Tous les groupes sont ouverts jusqu'à GRP-08 (fermer un groupe).
/// </summary>
public sealed class Groupe : IAppartientAuCentre
{
    /// <summary>La taille de la colonne du nom.</summary>
    public const int LongueurMaximaleDuNom = 100;

    private readonly List<Creneau> _creneaux = [];
    private readonly List<PrixDuGroupe> _prix = [];

    // Pour EF Core.
    private Groupe()
    {
        Nom = null!;
    }

    private Groupe(
        TypeDeGroupe type,
        string nom,
        Guid professeurId,
        Guid matiereId,
        Guid? niveauId,
        Guid anneeScolaireId,
        int? capacite,
        DateOnly dateDeDebut)
    {
        Type = type;
        Nom = nom;
        ProfesseurId = professeurId;
        MatiereId = matiereId;
        NiveauId = niveauId;
        AnneeScolaireId = anneeScolaireId;
        Capacite = capacite;
        DateDeDebut = dateDeDebut;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public TypeDeGroupe Type { get; private set; }

    public string Nom { get; private set; }

    public Guid ProfesseurId { get; private set; }

    public Guid MatiereId { get; private set; }

    /// <summary>Obligatoire pour une Étude ; une Formation n'en aura pas toujours (GRP-06).</summary>
    public Guid? NiveauId { get; private set; }

    /// <summary>L'année des séances : CEN-04 terminera les groupes de l'année qui finit.</summary>
    public Guid AnneeScolaireId { get; private set; }

    public int? Capacite { get; private set; }

    public DateOnly DateDeDebut { get; private set; }

    /// <summary>Dans l'ordre où la secrétaire les a saisis : le premier donne le nom proposé.</summary>
    public IReadOnlyList<Creneau> Creneaux => _creneaux;

    /// <summary>Les prix, chacun « à partir du Mois N ».</summary>
    public IReadOnlyList<PrixDuGroupe> Prix => _prix;

    /// <summary>Un groupe d'Étude, déjà vérifié, au prix de son premier Mois.</summary>
    internal static Groupe Etude(
        string nom,
        Guid professeurId,
        Guid matiereId,
        Guid niveauId,
        Guid anneeScolaireId,
        int? capacite,
        DateOnly dateDeDebut,
        IEnumerable<Creneau> creneaux,
        decimal prixDuMois)
    {
        var groupe = new Groupe(TypeDeGroupe.Etude, nom, professeurId, matiereId, niveauId, anneeScolaireId, capacite, dateDeDebut);
        groupe._creneaux.AddRange(creneaux);
        groupe._prix.Add(new PrixDuGroupe(1, prixDuMois));
        return groupe;
    }

    /// <summary>Le nouveau nom et la nouvelle capacité, déjà vérifiés.</summary>
    public void Modifier(string nom, int? capacite)
    {
        Nom = nom;
        Capacite = capacite;
    }

    /// <summary>
    /// Le nouveau prix vaut à partir du Mois N, le premier qui n'a pas commencé : il remplace les prix prévus
    /// pour ce Mois et les suivants. Les Mois d'avant gardent le leur.
    /// </summary>
    public void ChangerLePrix(decimal montant, int aPartirDuMois)
    {
        _prix.RemoveAll(p => p.APartirDuMois >= aPartirDuMois);
        _prix.Add(new PrixDuGroupe(aPartirDuMois, montant));
    }
}

/// <summary>Un jour de la semaine, écrit en français : c'est ainsi qu'il s'enregistre et qu'il circule en JSON.</summary>
public enum Jour
{
    Lundi = 1,
    Mardi,
    Mercredi,
    Jeudi,
    Vendredi,
    Samedi,
    Dimanche,
}

public static class Jours
{
    public static DayOfWeek VersDayOfWeek(Jour jour) => (DayOfWeek)((int)jour % 7);

    /// <summary>« Samedi » ou « samedi » ; un nombre ou un jour en anglais ne se lit pas.</summary>
    public static bool EssayerDeLire(string? saisie, out Jour jour)
    {
        var nom = Enum.GetNames<Jour>().FirstOrDefault(n => string.Equals(n, saisie?.Trim(), StringComparison.OrdinalIgnoreCase));
        jour = nom is null ? default : Enum.Parse<Jour>(nom);
        return nom is not null;
    }
}

/// <summary>Un créneau de la semaine : « le samedi de 10h à 12h ». Il fait partie de son groupe.</summary>
public sealed class Creneau(Jour jour, TimeOnly debut, TimeOnly fin)
{
    public Jour Jour { get; private set; } = jour;

    public TimeOnly Debut { get; private set; } = debut;

    public TimeOnly Fin { get; private set; } = fin;
}

/// <summary>
/// Le prix du Mois « à partir du Mois N » : un nouveau prix vaut pour les Mois qui n'ont pas encore commencé.
/// Il fait partie de son groupe.
/// </summary>
public sealed class PrixDuGroupe(int aPartirDuMois, decimal montant)
{
    public int APartirDuMois { get; private set; } = aPartirDuMois;

    public decimal Montant { get; private set; } = montant;
}

internal sealed class GroupeConfiguration : IEntityTypeConfiguration<Groupe>
{
    public void Configure(EntityTypeBuilder<Groupe> groupe)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        groupe.ToTable("groupes");
        groupe.Property(g => g.Type).HasConversion<string>().HasMaxLength(20);
        groupe.Property(g => g.Nom).HasMaxLength(Groupe.LongueurMaximaleDuNom);
        groupe.HasOne<Professeur>().WithMany().HasForeignKey(g => g.ProfesseurId).OnDelete(DeleteBehavior.Restrict);
        groupe.HasOne<Matiere>().WithMany().HasForeignKey(g => g.MatiereId).OnDelete(DeleteBehavior.Restrict);
        groupe.HasOne<Niveau>().WithMany().HasForeignKey(g => g.NiveauId).OnDelete(DeleteBehavior.Restrict);
        groupe.HasOne<AnneeScolaire>().WithMany().HasForeignKey(g => g.AnneeScolaireId).OnDelete(DeleteBehavior.Restrict);

        groupe.OwnsMany(g => g.Creneaux, creneau =>
        {
            creneau.ToTable("creneaux_des_groupes");
            creneau.Property(c => c.Jour).HasConversion<string>().HasMaxLength(10);
        });
        groupe.OwnsMany(g => g.Prix, prix =>
        {
            prix.ToTable("prix_des_groupes");
            prix.Property(p => p.Montant).HasPrecision(12, 3);
        });
    }
}
