using SchoolHub.Modules.Groupes;

namespace SchoolHub.Modules.Inscriptions;

/// <summary>La façon dont l'élève paie son inscription : le type du groupe fixe les choix possibles (ticket 01).</summary>
public enum ModeDeTarification
{
    /// <summary>Étude : chaque Mois de 4 séances, à sa première séance.</summary>
    ParMois,

    /// <summary>Révision : chaque séance, le jour même.</summary>
    ALaSeance,

    /// <summary>Révision : toutes les séances qui restent, à la première de l'élève.</summary>
    EnTotalite,

    /// <summary>Formation : le prix total, à la première séance de l'élève.</summary>
    EnUneFois,

    /// <summary>Formation : chaque tranche du groupe, à sa séance.</summary>
    EnTranches,
}

/// <summary>
/// Ce qu'il faut savoir d'un groupe pour calculer ce que doit un élève : son type, le numéro de sa première séance dans
/// SchoolHub (GRP-07), les dates de ses séances rangées comme on les compte, et ses prix.
/// </summary>
public sealed record CalendrierAPayer(
    TypeDeGroupe Type,
    int PremiereSeance,
    IReadOnlyList<DateOnly> Seances,
    IReadOnlyList<PrixDuGroupe> PrixDuMois,
    decimal? PrixTotal,
    int? NombreDeSeances,
    IReadOnlyList<Tranche> Tranches);

/// <summary>Une somme due par une inscription : « Mois 3 (3 séances) », 30,000 TND, due à la séance 10, le 20/11/2027.</summary>
public sealed record SommeDue(string Libelle, decimal Montant, int Seance, DateOnly Date);

/// <summary>
/// ELV-03 : toutes les sommes qu'une inscription doit, depuis la première séance de l'élève, sans base de données.
/// Un élève arrivé en cours de Mois paie la part des séances qu'il suit : chaque séance vaut le quart du prix du Mois,
/// la dernière prenant la différence. En cours de Révision, il paie les séances qui restent. Une Formation se paie
/// en entier ; une tranche dont la séance est passée est due à la première séance de l'élève.
/// </summary>
public static class Echeancier
{
    public static IReadOnlyList<ModeDeTarification> ModesPossibles(TypeDeGroupe type, bool aDesTranches) => type switch
    {
        TypeDeGroupe.Etude => [ModeDeTarification.ParMois],
        TypeDeGroupe.Revision => [ModeDeTarification.ALaSeance, ModeDeTarification.EnTotalite],
        _ => aDesTranches ? [ModeDeTarification.EnUneFois, ModeDeTarification.EnTranches] : [ModeDeTarification.EnUneFois],
    };

    /// <summary>Le numéro de la première séance de l'élève : celle du jour du début, ou la suivante ; <c>null</c> s'il n'y en a plus.</summary>
    public static int? PremiereSeance(CalendrierAPayer groupe, DateOnly debutLe) =>
        Rang(groupe, debutLe) is { } rang ? groupe.PremiereSeance + rang : null;

    /// <summary>Les sommes dues, rangées par séance. Le mode doit convenir au groupe (<see cref="ModesPossibles"/>).</summary>
    public static IReadOnlyList<SommeDue> Calculer(CalendrierAPayer groupe, ModeDeTarification mode, DateOnly debutLe)
    {
        if (!ModesPossibles(groupe.Type, groupe.Tranches.Count > 0).Contains(mode))
            throw new ArgumentException($"Le mode {mode} ne convient pas à ce groupe.", nameof(mode));
        if (Rang(groupe, debutLe) is not { } rang)
            return [];

        var suivies = groupe.Seances
            .Skip(rang)
            .Select((date, i) => new SeanceSuivie(groupe.PremiereSeance + rang + i, date))
            .ToList();
        return mode switch
        {
            ModeDeTarification.ParMois => ParMois(groupe, suivies),
            ModeDeTarification.ALaSeance => ALaSeance(groupe, suivies),
            ModeDeTarification.EnTotalite => [EnTotalite(groupe, suivies)],
            ModeDeTarification.EnUneFois => [new SommeDue("Formation en une fois", groupe.PrixTotal!.Value, suivies[0].Numero, suivies[0].Date)],
            _ => EnTranches(groupe, suivies),
        };
    }

    private sealed record SeanceSuivie(int Numero, DateOnly Date);

    /// <summary>La place, parmi les séances du groupe, de la première à la date du début ou après.</summary>
    private static int? Rang(CalendrierAPayer groupe, DateOnly debutLe)
    {
        for (var i = 0; i < groupe.Seances.Count; i++)
        {
            if (groupe.Seances[i] >= debutLe)
                return i;
        }

        return null;
    }

    private static List<SommeDue> ParMois(CalendrierAPayer groupe, List<SeanceSuivie> suivies) =>
        suivies
            .GroupBy(s => CalendrierDuGroupe.MoisDeLaSeance(s.Numero))
            .Select(mois =>
            {
                var parts = CalendrierDuGroupe.PrixDesSeances(CalendrierDuGroupe.PrixDuMois(groupe.PrixDuMois, mois.Key),
                    CalendrierDuGroupe.SeancesParMois);
                var seances = mois.ToList();
                var libelle = seances.Count == CalendrierDuGroupe.SeancesParMois
                    ? $"Mois {mois.Key}"
                    : $"Mois {mois.Key} ({seances.Count} {(seances.Count == 1 ? "séance" : "séances")})";
                // La place de la séance dans son Mois donne sa part : la 4ᵉ prend la différence.
                var montant = seances.Sum(s => parts[(s.Numero - 1) % CalendrierDuGroupe.SeancesParMois]);
                return new SommeDue(libelle, montant, seances[0].Numero, seances[0].Date);
            })
            .ToList();

    private static List<SommeDue> ALaSeance(CalendrierAPayer groupe, List<SeanceSuivie> suivies)
    {
        var parts = PartsDeLaRevision(groupe);
        return suivies.Select(s => new SommeDue($"Séance {s.Numero}", parts[s.Numero - 1], s.Numero, s.Date)).ToList();
    }

    private static SommeDue EnTotalite(CalendrierAPayer groupe, List<SeanceSuivie> suivies)
    {
        var parts = PartsDeLaRevision(groupe);
        var (premiere, derniere) = (suivies[0], suivies[^1]);
        var libelle = premiere.Numero == 1 && derniere.Numero == groupe.NombreDeSeances
            ? "Totalité"
            : $"Totalité, séances {premiere.Numero} à {derniere.Numero}";
        return new SommeDue(libelle, suivies.Sum(s => parts[s.Numero - 1]), premiere.Numero, premiere.Date);
    }

    /// <summary>Le prix de chaque séance de la Révision, celles d'avant SchoolHub comprises : le total divisé par leur nombre.</summary>
    private static IReadOnlyList<decimal> PartsDeLaRevision(CalendrierAPayer groupe) =>
        CalendrierDuGroupe.PrixDesSeances(groupe.PrixTotal!.Value, groupe.NombreDeSeances!.Value);

    private static List<SommeDue> EnTranches(CalendrierAPayer groupe, List<SeanceSuivie> suivies) =>
        groupe.Tranches
            .OrderBy(t => t.Seance)
            .Select((tranche, i) => (Libelle: $"Tranche {i + 1}", tranche.Montant,
                Seance: suivies.FirstOrDefault(s => s.Numero == Math.Max(tranche.Seance, suivies[0].Numero))))
            .Where(t => t.Seance is not null)
            .Select(t => new SommeDue(t.Libelle, t.Montant, t.Seance!.Numero, t.Seance.Date))
            .ToList();
}
