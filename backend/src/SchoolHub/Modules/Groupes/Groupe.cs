using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Groupes;

/// <summary>Le type d'un groupe (ticket 01, Q8) : il fixe la façon de payer.</summary>
public enum TypeDeGroupe
{
    /// <summary>Toute l'année, payée par Mois de 4 séances.</summary>
    Etude,

    /// <summary>Quelques séances, au prix total ou à la séance (GRP-05).</summary>
    Revision,

    /// <summary>Un prix total, en une fois ou en tranches (GRP-06).</summary>
    Formation,
}

/// <summary>
/// Un groupe (GRP-04 à GRP-07) : un professeur, une matière, un niveau, un ou plusieurs créneaux par semaine et un prix.
/// Ses séances sont enregistrées à part (<see cref="Seance"/>) ; leurs numéros, les Mois d'une Étude, les prix des
/// séances d'une Révision et l'état « terminé » se calculent (<see cref="CalendrierDuGroupe"/>).
/// </summary>
public sealed class Groupe : IAppartientAuCentre
{
    /// <summary>La taille de la colonne du nom.</summary>
    public const int LongueurMaximaleDuNom = 100;

    /// <summary>La taille de la colonne du motif de fermeture.</summary>
    public const int LongueurMaximaleDuMotif = 200;

    private readonly List<Creneau> _creneaux = [];
    private readonly List<PrixDuGroupe> _prix = [];
    private readonly List<Tranche> _tranches = [];

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
        Guid? anneeScolaireId,
        int? capacite,
        DateOnly dateDeDebut,
        int premiereSeance)
    {
        Type = type;
        Nom = nom;
        ProfesseurId = professeurId;
        MatiereId = matiereId;
        NiveauId = niveauId;
        AnneeScolaireId = anneeScolaireId;
        Capacite = capacite;
        DateDeDebut = dateDeDebut;
        PremiereSeance = premiereSeance;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public TypeDeGroupe Type { get; private set; }

    public string Nom { get; private set; }

    public Guid ProfesseurId { get; private set; }

    public Guid MatiereId { get; private set; }

    /// <summary>Obligatoire pour une Étude et une Révision ; facultatif pour une Formation (GRP-06).</summary>
    public Guid? NiveauId { get; private set; }

    /// <summary>
    /// L'année des séances : CEN-04 terminera les groupes de l'année qui finit. Obligatoire pour une Étude ; une Révision
    /// ou une Formation garde l'année de sa première séance, s'il y en a une (une Formation peut se faire l'été).
    /// </summary>
    public Guid? AnneeScolaireId { get; private set; }

    public int? Capacite { get; private set; }

    public DateOnly DateDeDebut { get; private set; }

    /// <summary>
    /// Le numéro de la première séance dans SchoolHub : 1, sauf pour un groupe déjà en cours (GRP-07). « Séance 4 sur 10 »
    /// donne 4 ; « Mois 5 » d'une Étude donne 17, la séance 1 du Mois 5.
    /// </summary>
    public int PremiereSeance { get; private set; } = 1;

    /// <summary>Révision et Formation : le prix de toutes les séances ; vide pour une Étude.</summary>
    public decimal? PrixTotal { get; private set; }

    /// <summary>Révision et Formation : toutes les séances, celles d'avant SchoolHub comprises ; vide pour une Étude.</summary>
    public int? NombreDeSeances { get; private set; }

    /// <summary>Dans l'ordre où la secrétaire les a saisis : le premier donne le nom proposé.</summary>
    public IReadOnlyList<Creneau> Creneaux => _creneaux;

    /// <summary>Étude : les prix, chacun « à partir du Mois N ».</summary>
    public IReadOnlyList<PrixDuGroupe> Prix => _prix;

    /// <summary>Formation : les tranches, facultatives, chacune due à une séance (GRP-06).</summary>
    public IReadOnlyList<Tranche> Tranches => _tranches;

    /// <summary>La date où le groupe a été fermé (GRP-08) ; vide tant qu'il est ouvert.</summary>
    public DateOnly? FermeLe { get; private set; }

    public string? MotifDeFermeture { get; private set; }

    /// <summary>Un groupe d'Étude, déjà vérifié, au prix de son premier Mois dans SchoolHub.</summary>
    internal static Groupe Etude(
        string nom,
        Guid professeurId,
        Guid matiereId,
        Guid niveauId,
        Guid anneeScolaireId,
        int? capacite,
        DateOnly dateDeDebut,
        IEnumerable<Creneau> creneaux,
        decimal prixDuMois,
        int premiereSeance = 1)
    {
        var groupe = new Groupe(TypeDeGroupe.Etude, nom, professeurId, matiereId, niveauId, anneeScolaireId, capacite, dateDeDebut,
            premiereSeance);
        groupe._creneaux.AddRange(creneaux);
        groupe._prix.Add(new PrixDuGroupe(CalendrierDuGroupe.MoisDeLaSeance(premiereSeance), prixDuMois));
        return groupe;
    }

    /// <summary>Une Révision ou une Formation, déjà vérifiée : son prix total, son nombre de séances et ses tranches.</summary>
    internal static Groupe RevisionOuFormation(
        TypeDeGroupe type,
        string nom,
        Guid professeurId,
        Guid matiereId,
        Guid? niveauId,
        Guid? anneeScolaireId,
        int? capacite,
        DateOnly dateDeDebut,
        IEnumerable<Creneau> creneaux,
        decimal prixTotal,
        int nombreDeSeances,
        int premiereSeance,
        IEnumerable<Tranche> tranches)
    {
        var groupe = new Groupe(type, nom, professeurId, matiereId, niveauId, anneeScolaireId, capacite, dateDeDebut, premiereSeance)
        {
            PrixTotal = prixTotal,
            NombreDeSeances = nombreDeSeances,
        };
        groupe._creneaux.AddRange(creneaux);
        groupe._tranches.AddRange(tranches);
        return groupe;
    }

    /// <summary>Le nouveau nom et la nouvelle capacité, déjà vérifiés.</summary>
    public void Modifier(string nom, int? capacite)
    {
        Nom = nom;
        Capacite = capacite;
    }

    /// <summary>SEA-06 : les nouveaux créneaux, déjà vérifiés ; les séances se recréent à part.</summary>
    internal void ChangerLesCreneaux(IEnumerable<Creneau> creneaux)
    {
        _creneaux.Clear();
        _creneaux.AddRange(creneaux);
    }

    /// <summary>Ferme le groupe à une date, avec son motif, déjà vérifiés : il ne se modifie plus ensuite.</summary>
    public void Fermer(DateOnly date, string motif)
    {
        FermeLe = date;
        MotifDeFermeture = motif;
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

    public static Jour De(DayOfWeek jour) => jour == DayOfWeek.Sunday ? Jour.Dimanche : (Jour)(int)jour;

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

/// <summary>Une tranche d'une Formation : un montant dû à une séance donnée, le même pour tous les élèves (GRP-06).</summary>
public sealed class Tranche(decimal montant, int seance)
{
    public decimal Montant { get; private set; } = montant;

    /// <summary>Le numéro de la séance où la tranche est due, de 1 au nombre de séances.</summary>
    public int Seance { get; private set; } = seance;
}

internal sealed class GroupeConfiguration : IEntityTypeConfiguration<Groupe>
{
    public void Configure(EntityTypeBuilder<Groupe> groupe)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        groupe.ToTable("groupes");
        groupe.Property(g => g.Type).HasConversion<string>().HasMaxLength(20);
        groupe.Property(g => g.Nom).HasMaxLength(Groupe.LongueurMaximaleDuNom);
        groupe.Property(g => g.PrixTotal).HasPrecision(12, 3);
        groupe.Property(g => g.PremiereSeance).HasDefaultValue(1);
        groupe.Property(g => g.MotifDeFermeture).HasMaxLength(Groupe.LongueurMaximaleDuMotif);
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
        groupe.OwnsMany(g => g.Tranches, tranche =>
        {
            tranche.ToTable("tranches_des_groupes");
            tranche.Property(t => t.Montant).HasPrecision(12, 3);
        });
    }
}
