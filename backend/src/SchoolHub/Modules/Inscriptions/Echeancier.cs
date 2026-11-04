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

/// <summary>
/// Une somme due par une inscription : « Mois 3 (3 séances) », 30,000 TND, due à la séance 10, le 20/11/2027. Sa clé
/// (« Mois 3 », « Séance 4 », « Totalité », « Une fois », « Tranche 2 », « Reste ») la désigne pour un règlement ; une
/// somme réglée vaut le montant de son règlement.
/// </summary>
public sealed record SommeDue(string Libelle, decimal Montant, int Seance, DateOnly Date, string Cle = "", bool Reglee = false);

/// <summary>
/// Ce que l'inscription ajoute au calendrier du groupe : son mode, sa première séance, son prix spécial (ELV-05), ses
/// règlements par clé, la date où la secrétaire a regroupé le reste d'une Formation (ELV-10), le dernier jour compté
/// d'une inscription arrêtée (ELV-08), et le report venu d'un ancien groupe (ELV-09).
/// </summary>
public sealed record ConditionsDeLInscription(
    ModeDeTarification Mode,
    DateOnly DebutLe,
    decimal? PrixSpecial = null,
    IReadOnlyDictionary<string, decimal>? Reglements = null,
    DateOnly? ResteRegroupeLe = null,
    DateOnly? ArreteLe = null,
    decimal Report = 0m);

/// <summary>
/// ELV-03 : toutes les sommes qu'une inscription doit, depuis la première séance de l'élève, sans base de données.
/// Un élève arrivé en cours de Mois paie la part des séances qu'il suit : chaque séance vaut le quart du prix du Mois,
/// la dernière prenant la différence. En cours de Révision, il paie les séances qui restent. Une Formation se paie
/// en entier ; une tranche dont la séance est passée est due à la première séance de l'élève.
/// ELV-05 : un prix spécial remplace le prix normal pour chaque somme pas encore réglée ; une somme réglée garde son
/// montant. ELV-10 : le reste regroupé d'une Formation réunit ses tranches pas réglées à venir en une somme due ce jour-là.
/// </summary>
public static class Echeancier
{
    public const string CleDuReste = "Reste";

    public static IReadOnlyList<ModeDeTarification> ModesPossibles(TypeDeGroupe type, bool aDesTranches) => type switch
    {
        TypeDeGroupe.Etude => [ModeDeTarification.ParMois],
        TypeDeGroupe.Revision => [ModeDeTarification.ALaSeance, ModeDeTarification.EnTotalite],
        _ => aDesTranches ? [ModeDeTarification.EnUneFois, ModeDeTarification.EnTranches] : [ModeDeTarification.EnUneFois],
    };

    /// <summary>Le numéro de la première séance de l'élève : celle du jour du début, ou la suivante ; <c>null</c> s'il n'y en a plus.</summary>
    public static int? PremiereSeance(CalendrierAPayer groupe, DateOnly debutLe) =>
        Rang(groupe, debutLe) is { } rang ? groupe.PremiereSeance + rang : null;

    /// <summary>Les sommes dues au prix normal, sans règlement : ce que l'élève doit en s'inscrivant.</summary>
    public static IReadOnlyList<SommeDue> Calculer(CalendrierAPayer groupe, ModeDeTarification mode, DateOnly debutLe) =>
        Calculer(groupe, new ConditionsDeLInscription(mode, debutLe));

    /// <summary>Les sommes dues, rangées par date. Le mode doit convenir au groupe (<see cref="ModesPossibles"/>).</summary>
    public static IReadOnlyList<SommeDue> Calculer(CalendrierAPayer groupe, ConditionsDeLInscription conditions)
    {
        if (!ModesPossibles(groupe.Type, groupe.Tranches.Count > 0).Contains(conditions.Mode))
            throw new ArgumentException($"Le mode {conditions.Mode} ne convient pas à ce groupe.", nameof(conditions));
        if (Rang(groupe, conditions.DebutLe) is not { } rang)
            return [];

        var suivies = groupe.Seances
            .Skip(rang)
            .Select((date, i) => new SeanceSuivie(groupe.PremiereSeance + rang + i, date))
            .ToList();
        var reglements = conditions.Reglements ?? new Dictionary<string, decimal>();
        var special = conditions.PrixSpecial;
        var sommes = conditions.Mode switch
        {
            ModeDeTarification.ParMois => ParMois(groupe, suivies, special),
            ModeDeTarification.ALaSeance => ALaSeance(groupe, suivies, special),
            ModeDeTarification.EnTotalite => [EnTotalite(groupe, suivies, special)],
            ModeDeTarification.EnUneFois =>
                [new SommeDue("Formation en une fois", special ?? groupe.PrixTotal!.Value, suivies[0].Numero, suivies[0].Date, "Une fois")],
            _ => EnTranches(groupe, suivies, special, reglements, conditions.ResteRegroupeLe),
        };

        sommes = sommes
            .Select(somme => somme with { Reglee = somme.Reglee || reglements.ContainsKey(somme.Cle) })
            // ELV-08 : après le dernier jour compté, plus rien n'est dû ; ce qui est payé reste payé, sans remboursement.
            .Where(somme => somme.Reglee || conditions.ArreteLe is not { } arreteLe || somme.Date <= arreteLe)
            .OrderBy(s => s.Date).ThenBy(s => s.Seance)
            .ToList();

        // ELV-09 : le report réduit les sommes une à une, dans l'ordre ; une somme réglée garde sa part.
        var report = conditions.Report;
        for (var i = 0; i < sommes.Count && report > 0; i++)
        {
            var part = Math.Min(sommes[i].Montant, report);
            report -= part;
            if (!sommes[i].Reglee)
                sommes[i] = sommes[i] with { Montant = sommes[i].Montant - part };
        }

        // Une somme réglée vaut son règlement : ni un prix spécial, ni un nouveau prix, ni le report ne la change.
        return sommes
            .Select(somme => reglements.TryGetValue(somme.Cle, out var regle) ? somme with { Montant = regle } : somme)
            .ToList();
    }

    /// <summary>
    /// ELV-09 : la valeur des séances payées et pas suivies à partir de cette date. Pour chaque somme réglée, son montant
    /// réglé × ses séances à cette date ou après ÷ ses séances, au millime. Une Formation compte ensemble tout ce qui est
    /// réglé, sur toutes les séances de l'élève.
    /// </summary>
    public static decimal ValeurNonSuivie(CalendrierAPayer groupe, ConditionsDeLInscription conditions, DateOnly depuisLe)
    {
        if (Rang(groupe, conditions.DebutLe) is not { } rang)
            return 0m;
        var suivies = groupe.Seances.Skip(rang).Select((date, i) => new SeanceSuivie(groupe.PremiereSeance + rang + i, date)).ToList();
        var reglees = Calculer(groupe, conditions with { ArreteLe = null }).Where(s => s.Reglee).ToList();

        if (groupe.Type == TypeDeGroupe.Formation)
            return Part(reglees.Sum(s => s.Montant), suivies, depuisLe);
        return reglees.Sum(somme => Part(somme.Montant, SeancesDeLaSomme(somme, suivies), depuisLe));
    }

    /// <summary>Les séances qu'une somme paie : celles de son Mois, sa séance, ou toutes celles de l'élève pour la totalité.</summary>
    private static List<SeanceSuivie> SeancesDeLaSomme(SommeDue somme, List<SeanceSuivie> suivies)
    {
        if (somme.Cle.StartsWith("Mois ", StringComparison.Ordinal) && int.TryParse(somme.Cle["Mois ".Length..], out var mois))
            return suivies.Where(s => CalendrierDuGroupe.MoisDeLaSeance(s.Numero) == mois).ToList();
        if (somme.Cle.StartsWith("Séance ", StringComparison.Ordinal) && int.TryParse(somme.Cle["Séance ".Length..], out var numero))
            return suivies.Where(s => s.Numero == numero).ToList();
        return suivies;
    }

    /// <summary>La part d'un montant pour ses séances à cette date ou après, au millime.</summary>
    private static decimal Part(decimal montant, List<SeanceSuivie> seances, DateOnly depuisLe) =>
        seances.Count == 0 ? 0m : decimal.Round(montant * seances.Count(s => s.Date >= depuisLe) / seances.Count, 3, MidpointRounding.AwayFromZero);

    /// <summary>
    /// ELV-10 : « Payer tout le reste » est possible pour une Formation en tranches pas encore regroupée, qui a une tranche à
    /// regrouper aujourd'hui, et de plus de 0 TND.
    /// </summary>
    public static bool PeutRegrouperLeReste(CalendrierAPayer groupe, ConditionsDeLInscription conditions, DateOnly aujourdhui)
    {
        if (conditions.Mode != ModeDeTarification.EnTranches || conditions.ResteRegroupeLe is not null || conditions.ArreteLe is not null
            || PremiereSeance(groupe, conditions.DebutLe) is not { } premiere)
            return false;
        return Calculer(groupe, conditions).Any(s => Regroupable(s, aujourdhui, premiere) && s.Montant > 0);
    }

    /// <summary>
    /// Une tranche se regroupe si elle n'est pas réglée, vient après le jour du regroupement, et n'est pas due à la première
    /// séance de l'élève : ce qui était dû à son arrivée (ou avant SchoolHub) reste à part.
    /// </summary>
    private static bool Regroupable(SommeDue somme, DateOnly regroupeLe, int premiereSeance) =>
        !somme.Reglee && somme.Date > regroupeLe && somme.Seance > premiereSeance;

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

    private static List<SommeDue> ParMois(CalendrierAPayer groupe, List<SeanceSuivie> suivies, decimal? special) =>
        suivies
            .GroupBy(s => CalendrierDuGroupe.MoisDeLaSeance(s.Numero))
            .Select(mois =>
            {
                var prix = special ?? CalendrierDuGroupe.PrixDuMois(groupe.PrixDuMois, mois.Key);
                var parts = CalendrierDuGroupe.PrixDesSeances(prix, CalendrierDuGroupe.SeancesParMois);
                var seances = mois.ToList();
                var libelle = seances.Count == CalendrierDuGroupe.SeancesParMois
                    ? $"Mois {mois.Key}"
                    : $"Mois {mois.Key} ({seances.Count} {(seances.Count == 1 ? "séance" : "séances")})";
                // La place de la séance dans son Mois donne sa part : la 4ᵉ prend la différence.
                var montant = seances.Sum(s => parts[(s.Numero - 1) % CalendrierDuGroupe.SeancesParMois]);
                return new SommeDue(libelle, montant, seances[0].Numero, seances[0].Date, $"Mois {mois.Key}");
            })
            .ToList();

    private static List<SommeDue> ALaSeance(CalendrierAPayer groupe, List<SeanceSuivie> suivies, decimal? special)
    {
        var parts = PartsDeLaRevision(groupe, special);
        return suivies.Select(s => new SommeDue($"Séance {s.Numero}", parts[s.Numero - 1], s.Numero, s.Date, $"Séance {s.Numero}")).ToList();
    }

    private static SommeDue EnTotalite(CalendrierAPayer groupe, List<SeanceSuivie> suivies, decimal? special)
    {
        var parts = PartsDeLaRevision(groupe, special);
        var (premiere, derniere) = (suivies[0], suivies[^1]);
        var libelle = premiere.Numero == 1 && derniere.Numero == groupe.NombreDeSeances
            ? "Totalité"
            : $"Totalité, séances {premiere.Numero} à {derniere.Numero}";
        return new SommeDue(libelle, suivies.Sum(s => parts[s.Numero - 1]), premiere.Numero, premiere.Date, "Totalité");
    }

    /// <summary>
    /// Le prix de chaque séance de la Révision, celles d'avant SchoolHub comprises : le total (ou le prix spécial)
    /// divisé par leur nombre.
    /// </summary>
    private static IReadOnlyList<decimal> PartsDeLaRevision(CalendrierAPayer groupe, decimal? special) =>
        CalendrierDuGroupe.PrixDesSeances(special ?? groupe.PrixTotal!.Value, groupe.NombreDeSeances!.Value);

    /// <summary>
    /// Chaque tranche à sa séance, ou à la première de l'élève si elle est passée. Avec un prix spécial, les tranches pas
    /// réglées se partagent ce qui reste du prix spécial une fois retirés les règlements (le reste regroupé compris), sans
    /// descendre sous 0. ELV-10 : regroupées, les tranches à venir deviennent « Reste de la Formation », due ce jour-là.
    /// </summary>
    private static List<SommeDue> EnTranches(
        CalendrierAPayer groupe,
        List<SeanceSuivie> suivies,
        decimal? special,
        IReadOnlyDictionary<string, decimal> reglements,
        DateOnly? regroupeLe)
    {
        var premiere = suivies[0].Numero;
        var sommes = groupe.Tranches
            .OrderBy(t => t.Seance)
            .Select((tranche, i) => (Cle: $"Tranche {i + 1}", tranche.Montant,
                Seance: suivies.FirstOrDefault(s => s.Numero == Math.Max(tranche.Seance, premiere))))
            .Where(t => t.Seance is not null)
            .Select(t => reglements.TryGetValue(t.Cle, out var regle)
                ? new SommeDue(t.Cle, regle, t.Seance!.Numero, t.Seance.Date, t.Cle, true)
                : new SommeDue(t.Cle, t.Montant, t.Seance!.Numero, t.Seance.Date, t.Cle))
            .ToList();
        var regroupees = regroupeLe is { } le ? sommes.Where(s => Regroupable(s, le, premiere)).Select(s => s.Cle).ToHashSet() : [];
        var resteRegle = regroupees.Count > 0 && reglements.ContainsKey(CleDuReste);

        if (special is { } prix)
        {
            // Le reste regroupé et payé compte comme ses tranches payées.
            var regle = sommes.Where(s => s.Reglee).Sum(s => s.Montant) + (resteRegle ? reglements[CleDuReste] : 0m);
            var aRegler = sommes.Where(s => !s.Reglee && !(resteRegle && regroupees.Contains(s.Cle))).Select(s => s.Cle).ToList();
            var reste = Math.Max(0m, prix - regle);
            var parts = aRegler.Count == 0 ? [] : reste == 0 ? Enumerable.Repeat(0m, aRegler.Count).ToList() : CalendrierDuGroupe.PrixDesSeances(reste, aRegler.Count);
            sommes = sommes.Select(s => aRegler.IndexOf(s.Cle) is var rang and >= 0 ? s with { Montant = parts[rang] } : s).ToList();
        }

        if (regroupees.Count == 0)
            return sommes;
        var aVenir = sommes.Where(s => regroupees.Contains(s.Cle)).ToList();
        var leReste = new SommeDue("Reste de la Formation", aVenir.Sum(s => s.Montant), aVenir.Min(s => s.Seance), regroupeLe!.Value, CleDuReste);
        if (reglements.TryGetValue(CleDuReste, out var montantDuReste))
            leReste = leReste with { Montant = montantDuReste, Reglee = true };
        return [.. sommes.Where(s => !regroupees.Contains(s.Cle)), leReste];
    }
}
