using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Abonnements;

/// <summary>L'abonnement vu depuis le centre courant : de quoi afficher ses bandeaux (EDI-05).</summary>
public sealed class AbonnementCoteCentre(SchoolHubDbContext contexte, CentreCourant centreCourant, TimeProvider temps)
{
    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    /// <summary>La situation de l'abonnement d'un centre aujourd'hui (aussi lue par la lecture seule, à chaque écriture).</summary>
    public static async Task<SituationDeLAbonnement> SituationAsync(
        SchoolHubDbContext contexte,
        Guid centreId,
        TimeProvider temps,
        CancellationToken annulation = default)
    {
        var abonnement = await contexte.Set<Abonnement>().AsNoTracking().SingleOrDefaultAsync(a => a.CentreId == centreId, annulation);
        return SituationDeLAbonnement.Calculer(abonnement, DateDeTunis.Aujourdhui(temps));
    }

    public async Task<AbonnementDuCentre> LireAsync(CancellationToken annulation = default)
    {
        var situation = await SituationAsync(contexte, CentreId, temps, annulation);
        return new AbonnementDuCentre(situation.Etat.ToString(), situation.PayeJusquAu, situation.FinDeLaPeriodeDeGrace, situation.LectureSeule);
    }
}
