using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Rh;

/// <summary>
/// RHA-01 : la fiche RH d'un employé du centre (pas d'un professeur) : son poste, ses horaires et son salaire. Ce n'est
/// pas un compte. Parti, il sort de la liste, mais sa fiche, ses congés et ses salaires restent.
/// </summary>
public sealed class Employe : IAppartientAuCentre
{
    public const int LongueurMaximaleDuNom = 100;
    public const int LongueurMaximaleDesHoraires = 300;

    internal Employe(FicheDEmploye fiche)
    {
        Prenom = fiche.Prenom;
        Nom = fiche.Nom;
        Poste = fiche.Poste;
        Modifier(fiche);
    }

    // Pour EF Core.
    private Employe()
    {
        Prenom = null!;
        Nom = null!;
        Poste = null!;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public string Prenom { get; private set; }

    public string Nom { get; private set; }

    public string? Email { get; private set; }

    /// <summary>Les 8 chiffres.</summary>
    public string? Telephone { get; private set; }

    public string Poste { get; private set; }

    /// <summary>« Lundi à vendredi, 8h → 16h ».</summary>
    public string? Horaires { get; private set; }

    public decimal Salaire { get; private set; }

    public DateOnly? PartiLe { get; private set; }

    internal void Modifier(FicheDEmploye fiche)
    {
        Prenom = fiche.Prenom;
        Nom = fiche.Nom;
        Email = fiche.Email;
        Telephone = fiche.Telephone;
        Poste = fiche.Poste;
        Horaires = fiche.Horaires;
        Salaire = fiche.Salaire;
    }

    internal void Partir(DateOnly le) => PartiLe = le;

    internal void Revenir() => PartiLe = null;
}

/// <summary>Une fiche déjà vérifiée.</summary>
internal sealed record FicheDEmploye(string Prenom, string Nom, string? Email, string? Telephone, string Poste, string? Horaires, decimal Salaire);

/// <summary>RHA-02 : des jours de congé, du premier au dernier inclus, avec une note facultative.</summary>
public sealed class Conge : IAppartientAuCentre
{
    public const int LongueurMaximaleDeLaNote = 200;

    internal Conge(Guid employeId, DateOnly du, DateOnly au, string? note)
    {
        EmployeId = employeId;
        Du = du;
        Au = au;
        Note = note;
    }

    // Pour EF Core.
    private Conge()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid EmployeId { get; private set; }

    public DateOnly Du { get; private set; }

    public DateOnly Au { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Les jours du calendrier de ce congé qui tombent dans l'année.</summary>
    public int JoursDans(int annee)
    {
        var debut = Du.Year < annee ? new DateOnly(annee, 1, 1) : Du;
        var fin = Au.Year > annee ? new DateOnly(annee, 12, 31) : Au;
        return fin < debut ? 0 : fin.DayNumber - debut.DayNumber + 1;
    }
}

/// <summary>RHA-03 : le salaire d'un mois marqué payé, figé avec la date.</summary>
public sealed class SalairePaye : IAppartientAuCentre
{
    internal SalairePaye(Guid employeId, DateOnly mois, decimal montant, DateOnly payeLe)
    {
        EmployeId = employeId;
        Mois = mois;
        Montant = montant;
        PayeLe = payeLe;
    }

    // Pour EF Core.
    private SalairePaye()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid EmployeId { get; private set; }

    /// <summary>Le 1er du mois du salaire : il compte dans les dépenses de ce mois (TDB-01).</summary>
    public DateOnly Mois { get; private set; }

    public decimal Montant { get; private set; }

    public DateOnly PayeLe { get; private set; }
}

internal sealed class EmployeConfiguration : IEntityTypeConfiguration<Employe>
{
    public void Configure(EntityTypeBuilder<Employe> employe)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        employe.ToTable("employes");
        employe.Property(e => e.Prenom).HasMaxLength(Employe.LongueurMaximaleDuNom);
        employe.Property(e => e.Nom).HasMaxLength(Employe.LongueurMaximaleDuNom);
        employe.Property(e => e.Email).HasMaxLength(AdresseEmail.LongueurMaximale);
        employe.Property(e => e.Telephone).HasMaxLength(8);
        employe.Property(e => e.Poste).HasMaxLength(Employe.LongueurMaximaleDuNom);
        employe.Property(e => e.Horaires).HasMaxLength(Employe.LongueurMaximaleDesHoraires);
        employe.Property(e => e.Salaire).HasPrecision(12, 3);
    }
}

internal sealed class CongeConfiguration : IEntityTypeConfiguration<Conge>
{
    public void Configure(EntityTypeBuilder<Conge> conge)
    {
        conge.ToTable("conges");
        conge.Property(c => c.Note).HasMaxLength(Conge.LongueurMaximaleDeLaNote);
        conge.HasOne<Employe>().WithMany().HasForeignKey(c => c.EmployeId).OnDelete(DeleteBehavior.Restrict);
        conge.HasIndex(c => c.EmployeId);
    }
}

internal sealed class SalairePayeConfiguration : IEntityTypeConfiguration<SalairePaye>
{
    public void Configure(EntityTypeBuilder<SalairePaye> salaire)
    {
        salaire.ToTable("salaires_payes");
        salaire.Property(s => s.Montant).HasPrecision(12, 3);
        salaire.HasOne<Employe>().WithMany().HasForeignKey(s => s.EmployeId).OnDelete(DeleteBehavior.Restrict);
        salaire.HasIndex(s => new { s.EmployeId, s.Mois }).IsUnique();
    }
}
