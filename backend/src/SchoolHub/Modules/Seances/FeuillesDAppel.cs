using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Seances;

/// <summary>
/// SEA-08 : la feuille d'appel d'un groupe, pour la séance du jour, un Mois ou des dates : le centre, l'année scolaire, le
/// groupe, sa matière, son niveau et son professeur ; une colonne par séance qui a lieu (supplémentaires comprises) ; une
/// ligne par élève inscrit pendant la période, par nom puis prénom. Ni téléphone, ni paiement : elle circule dans la salle.
/// </summary>
public sealed class FeuillesDAppel(SchoolHubDbContext contexte, CentreCourant centreCourant)
{
    /// <summary>Une feuille tient sur une page : 3 mois au plus.</summary>
    private const int JoursAuPlus = 92;

    private const string GroupeIntrouvable = "Groupe introuvable";
    private const string ChoisirLaPeriode = "Choisissez la période";
    private const string PeriodeALEnvers = "La période finit avant de commencer";
    private const string PeriodeTropLongue = "La période fait au plus 3 mois";

    public async Task<IssueDeLaFeuille> LireAsync(Guid groupeId, DateOnly? du, DateOnly? au)
    {
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == groupeId);
        if (groupe is null)
            return Refus(StatusCodes.Status404NotFound, GroupeIntrouvable);
        if (du is not { } debut || au is not { } fin)
            return Refus(StatusCodes.Status400BadRequest, ChoisirLaPeriode);
        if (fin < debut)
            return Refus(StatusCodes.Status400BadRequest, PeriodeALEnvers);
        if (fin.DayNumber - debut.DayNumber > JoursAuPlus)
            return Refus(StatusCodes.Status400BadRequest, PeriodeTropLongue);

        var seances = await contexte.Seances.QuiOntLieu()
            .Where(s => s.GroupeId == groupeId && s.Date >= debut && s.Date <= fin)
            .OrderBy(s => s.Date).ThenBy(s => s.Debut).ThenBy(s => s.Id)
            .ToListAsync();
        var eleves = await contexte.Inscriptions
            .Where(i => i.GroupeId == groupeId && i.DebutLe <= fin && (i.ArreteLe == null || i.ArreteLe >= debut))
            .Join(contexte.Eleves, i => i.EleveId, e => e.Id, (i, e) => e)
            .Distinct()
            .OrderBy(e => e.Nom).ThenBy(e => e.Prenom)
            .Select(e => new EleveDeLaFeuille(e.Nom, e.Prenom))
            .ToListAsync();
        var centre = await contexte.Centres.Where(c => c.Id == centreCourant.Id).Select(c => c.Nom).SingleAsync();
        var annee = groupe.AnneeScolaireId is { } anneeId ? await contexte.AnneesScolaires.SingleAsync(a => a.Id == anneeId) : null;
        var matiere = await contexte.Matieres.Where(m => m.Id == groupe.MatiereId).Select(m => m.Nom).SingleAsync();
        var niveau = groupe.NiveauId is { } niveauId ? await contexte.Niveaux.Where(n => n.Id == niveauId).Select(n => n.Nom).SingleAsync() : null;
        var professeur = await contexte.Professeurs.Where(p => p.Id == groupe.ProfesseurId).Select(p => p.Prenom + " " + p.Nom).SingleAsync();

        return new IssueDeLaFeuille(new FeuilleDAppel(
            centre,
            annee is null ? null : $"{annee.Debut.Year}-{annee.Fin.Year}",
            groupe.Nom,
            matiere,
            niveau,
            professeur,
            seances.Select(s => new SeanceDeLaFeuille(s.Date, s.Debut.ToString("HH:mm", CultureInfo.InvariantCulture))).ToList(),
            eleves), StatusCodes.Status200OK, null);
    }

    private static IssueDeLaFeuille Refus(int statut, string message) => new(null, statut, message);
}

/// <summary>Ce que la lecture d'une feuille a donné : la feuille et le statut HTTP, ou un refus.</summary>
public sealed record IssueDeLaFeuille(FeuilleDAppel? Feuille, int Statut, string? Message);
