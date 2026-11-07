using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Inscriptions;

/// <summary>
/// ELV-03 : le rattachement d'un élève à un groupe, avec son mode de tarification et la date de sa première séance.
/// Une date plutôt qu'une séance : une séance annulée plus tard (SEA) ne casse rien, la suivante devient la première.
/// Les paiements de l'étape suivante viendront s'y rattacher.
/// </summary>
public sealed class Inscription : IAppartientAuCentre
{
    internal Inscription(Guid eleveId, Guid groupeId, ModeDeTarification mode, DateOnly debutLe, DateTimeOffset inscritLe, Guid? depuisInscriptionId = null)
    {
        EleveId = eleveId;
        GroupeId = groupeId;
        Mode = mode;
        DebutLe = debutLe;
        InscritLe = inscritLe;
        DepuisInscriptionId = depuisInscriptionId;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid EleveId { get; private set; }

    public Guid GroupeId { get; private set; }

    public ModeDeTarification Mode { get; private set; }

    /// <summary>La date de la première séance de l'élève : l'échéancier part de la séance de ce jour, ou de la suivante.</summary>
    public DateOnly DebutLe { get; private set; }

    public DateTimeOffset InscritLe { get; private set; }

    /// <summary>ELV-05 : le prix qui remplace le prix normal pour ce qui n'est pas payé ; 0 : gratuit.</summary>
    public decimal? PrixSpecial { get; private set; }

    public string? RaisonDuPrixSpecial { get; private set; }

    /// <summary>TDB-03 : le jour du prix spécial, et qui l'a accordé (prénom et nom, figés).</summary>
    public DateOnly? PrixSpecialAccordeLe { get; private set; }

    public string? PrixSpecialAccordePar { get; private set; }

    /// <summary>ELV-10 : le jour où les tranches à venir d'une Formation sont devenues une seule somme, due ce jour-là.</summary>
    public DateOnly? ResteRegroupeLe { get; private set; }

    /// <summary>Le prix spécial et sa raison, déjà vérifiés.</summary>
    public void AccorderUnPrixSpecial(decimal montant, string raison, DateOnly? le = null, string? par = null)
    {
        PrixSpecial = montant;
        RaisonDuPrixSpecial = raison;
        PrixSpecialAccordeLe = le;
        PrixSpecialAccordePar = par;
    }

    public void RetirerLePrixSpecial()
    {
        PrixSpecial = null;
        RaisonDuPrixSpecial = null;
        PrixSpecialAccordeLe = null;
        PrixSpecialAccordePar = null;
    }

    public void RegrouperLeReste(DateOnly aujourdhui) => ResteRegroupeLe = aujourdhui;

    /// <summary>ELV-08 : le dernier jour compté ; rien n'est dû après, ce qui est payé le reste.</summary>
    public DateOnly? ArreteLe { get; private set; }

    /// <summary>
    /// ELV-09 : l'inscription quittée pour celle-ci. Son report (la valeur de ses séances payées et pas suivies) se calcule à
    /// chaque lecture : un paiement noté plus tard sur l'ancienne inscription, ou un second changement, le suit.
    /// </summary>
    public Guid? DepuisInscriptionId { get; private set; }

    public void Arreter(DateOnly le) => ArreteLe = le;
}

/// <summary>
/// L'assurance d'un élève pour une année scolaire : créée à sa première inscription en Étude de l'année, au montant des
/// paramètres du centre ce jour-là (ce qui est dû garde sa valeur). Payer et dispenser viendront ensuite (ELV-06).
/// </summary>
public sealed class AssuranceDeLAnnee : IAppartientAuCentre
{
    internal AssuranceDeLAnnee(Guid eleveId, Guid anneeScolaireId, decimal montant)
    {
        EleveId = eleveId;
        AnneeScolaireId = anneeScolaireId;
        Montant = montant;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid EleveId { get; private set; }

    public Guid AnneeScolaireId { get; private set; }

    public decimal Montant { get; private set; }

    /// <summary>ELV-06 : le jour où le propriétaire l'a dispensée ; elle n'est plus due.</summary>
    public DateOnly? DispenseeLe { get; private set; }

    public string? RaisonDeLaDispense { get; private set; }

    /// <summary>La dispense et sa raison, déjà vérifiées.</summary>
    public void Dispenser(DateOnly aujourdhui, string raison, string? par = null)
    {
        DispenseeLe = aujourdhui;
        RaisonDeLaDispense = raison;
        DispenseePar = par;
    }

    /// <summary>TDB-03 : qui a accordé la dispense (prénom et nom, figés).</summary>
    public string? DispenseePar { get; private set; }

    /// <summary>« Assurance 2027-2028 ».</summary>
    public static string Libelle(AnneeScolaire annee) => $"Assurance {Annee(annee)}";

    /// <summary>« 2027-2028 ».</summary>
    public static string Annee(AnneeScolaire annee) => $"{annee.Debut.Year}-{annee.Fin.Year}";
}

internal sealed class InscriptionConfiguration : IEntityTypeConfiguration<Inscription>
{
    public void Configure(EntityTypeBuilder<Inscription> inscription)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        inscription.ToTable("inscriptions");
        inscription.Property(i => i.Mode).HasConversion<string>().HasMaxLength(20);
        inscription.Property(i => i.PrixSpecial).HasPrecision(12, 3);
        inscription.HasOne<Inscription>().WithMany().HasForeignKey(i => i.DepuisInscriptionId).OnDelete(DeleteBehavior.Restrict);
        inscription.Property(i => i.RaisonDuPrixSpecial).HasMaxLength(InscriptionsDuCentre.LongueurMaximaleDUneRaison);
        inscription.Property(i => i.PrixSpecialAccordePar).HasMaxLength(201);
        inscription.HasOne<Eleve>().WithMany().HasForeignKey(i => i.EleveId).OnDelete(DeleteBehavior.Restrict);
        inscription.HasOne<Groupe>().WithMany().HasForeignKey(i => i.GroupeId).OnDelete(DeleteBehavior.Restrict);
        // Un élève n'a qu'une inscription pas arrêtée par groupe : arrêté (ELV-08), il peut s'y réinscrire.
        inscription.HasIndex(i => new { i.EleveId, i.GroupeId }).IsUnique().HasFilter("arrete_le IS NULL");
        inscription.HasIndex(i => i.GroupeId);
    }
}

internal sealed class AssuranceDeLAnneeConfiguration : IEntityTypeConfiguration<AssuranceDeLAnnee>
{
    public void Configure(EntityTypeBuilder<AssuranceDeLAnnee> assurance)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        assurance.ToTable("assurances");
        assurance.Property(a => a.Montant).HasPrecision(12, 3);
        assurance.Property(a => a.RaisonDeLaDispense).HasMaxLength(InscriptionsDuCentre.LongueurMaximaleDUneRaison);
        assurance.Property(a => a.DispenseePar).HasMaxLength(201);
        assurance.HasOne<Eleve>().WithMany().HasForeignKey(a => a.EleveId).OnDelete(DeleteBehavior.Restrict);
        assurance.HasOne<AnneeScolaire>().WithMany().HasForeignKey(a => a.AnneeScolaireId).OnDelete(DeleteBehavior.Restrict);
        assurance.HasIndex(a => new { a.EleveId, a.AnneeScolaireId }).IsUnique();
    }
}
