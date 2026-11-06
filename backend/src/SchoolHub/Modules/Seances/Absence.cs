using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Centres;

namespace SchoolHub.Modules.Seances;

/// <summary>
/// SEA-07 : un élève absent à une séance de son groupe. Un élève est présent sauf s'il est marqué absent : seules les
/// absences s'enregistrent (ticket 02, Q5). L'absence vient de l'inscription de l'élève dans le groupe de la séance.
/// </summary>
public sealed class Absence : IAppartientAuCentre
{
    internal Absence(Guid seanceId, Guid inscriptionId)
    {
        SeanceId = seanceId;
        InscriptionId = inscriptionId;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public Guid SeanceId { get; private set; }

    public Guid InscriptionId { get; private set; }

    /// <summary>SEA-11 : le moment où la secrétaire a ouvert le message aux destinataires de l'absent.</summary>
    public DateTimeOffset? PrevenuLe { get; private set; }

    internal void MarquerPrevenu(DateTimeOffset maintenant) => PrevenuLe ??= maintenant;
}

internal sealed class AbsenceConfiguration : IEntityTypeConfiguration<Absence>
{
    public void Configure(EntityTypeBuilder<Absence> absence)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        absence.ToTable("absences");
        // Une séance retirée par la fermeture du groupe (GRP-08) emporte ses absences.
        absence.HasOne<Seance>().WithMany().HasForeignKey(a => a.SeanceId).OnDelete(DeleteBehavior.Cascade);
        absence.HasOne<Inscription>().WithMany().HasForeignKey(a => a.InscriptionId).OnDelete(DeleteBehavior.Restrict);
        absence.HasIndex(a => new { a.SeanceId, a.InscriptionId }).IsUnique();
    }
}
