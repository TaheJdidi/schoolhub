using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Socle.Journal;

/// <summary>
/// Une ligne du journal : qui a fait quoi, et quand, avec la valeur d'avant et celle d'après.
/// Elle appartient à son centre, et personne ne peut la modifier ni l'effacer (exigences communes).
/// Elle s'écrit avec <see cref="JournalDuCentre.Noter"/>.
/// </summary>
public sealed class LigneDuJournal : IAppartientAuCentre
{
    internal LigneDuJournal(
        DateTimeOffset quand,
        Guid? auteurId,
        string auteur,
        ActionDuJournal action,
        string detail,
        string? avant,
        string? apres)
    {
        Quand = quand;
        AuteurId = auteurId;
        Auteur = auteur;
        Action = action;
        Detail = detail;
        Avant = avant;
        Apres = apres;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public DateTimeOffset Quand { get; private set; }

    /// <summary>Le compte qui a fait l'action, ou <c>null</c> quand c'est SchoolHub (la commande CEN-01).</summary>
    public Guid? AuteurId { get; private set; }

    /// <summary>Le prénom et le nom de l'auteur, figés au moment de l'action.</summary>
    public string Auteur { get; private set; }

    public ActionDuJournal Action { get; private set; }

    /// <summary>Ce qui s'est passé, en français : « Sonia Mabrouk · Secrétaire ».</summary>
    public string Detail { get; private set; }

    public string? Avant { get; private set; }

    public string? Apres { get; private set; }
}

/// <summary>
/// Les actions qui vont au journal. Chaque story qui touche à l'argent ou à la présence ajoute les siennes.
/// Le nom est enregistré en texte : c'est lui que filtrera l'écran du journal (TDB-04).
/// </summary>
public enum ActionDuJournal
{
    CompteCree,
    CompteDesactive,
    CompteReactive,
    GroupeFerme,
    PrixSpecialAccorde,
    PrixSpecialRetire,
    AssuranceDispensee,
    PayeAvantSchoolHub,
    InscriptionArretee,
    ChangementDeGroupe,
    PresenceCorrigee,
    SeanceAnnulee,
    SeanceDeplacee,
    Encaissement,
    RecuAnnule,
    CaisseCloturee,
    SortieDeCaisse,
}

internal sealed class LigneDuJournalConfiguration : IEntityTypeConfiguration<LigneDuJournal>
{
    public void Configure(EntityTypeBuilder<LigneDuJournal> ligne)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        ligne.ToTable("journal");

        // Le prénom et le nom d'un compte font au plus 100 caractères chacun, plus l'espace.
        ligne.Property(l => l.Auteur).HasMaxLength(201);
        ligne.Property(l => l.Action).HasConversion<string>().HasMaxLength(30);
        ligne.Property(l => l.Detail).HasMaxLength(500);
        ligne.Property(l => l.Avant).HasMaxLength(100);
        ligne.Property(l => l.Apres).HasMaxLength(100);

        // Un compte qui a écrit au journal ne peut pas être supprimé.
        ligne.HasOne<Compte>().WithMany().HasForeignKey(l => l.AuteurId).OnDelete(DeleteBehavior.Restrict);
    }
}
