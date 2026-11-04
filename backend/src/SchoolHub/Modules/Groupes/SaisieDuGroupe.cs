using System.Globalization;

namespace SchoolHub.Modules.Groupes;

/// <summary>
/// GRP-04 à GRP-07 : la lecture de ce que la page envoie pour créer un groupe, sans base de données. Chaque fonction rend
/// la valeur lue, ou le message du premier refus.
/// </summary>
public static class SaisieDuGroupe
{
    public const int NombreMaximalDeSeances = 200;
    public const int MoisEnCoursMaximal = 20;

    private const string AuMoinsUnCreneau = "Ajoutez au moins un créneau";
    private const string CreneauIncomplet = "Un créneau doit avoir un jour, une heure de début et une heure de fin";
    private const string CreneauALEnvers = "Un créneau doit finir après son début";
    private const string CreneauxQuiSeChevauchent = "Deux créneaux se chevauchent";
    private const string SeanceIncomplete = "Une séance doit avoir une date, une heure de début et une heure de fin";
    private const string SeanceALEnvers = "Une séance doit finir après son début";
    private const string SeancesQuiSeChevauchent = "Deux séances se chevauchent";
    private const string TrancheSansMontant = "Une tranche doit avoir un montant positif, au millime près";
    private const string TranchesSurLaMemeSeance = "Deux tranches tombent sur la même séance";

    /// <summary>« Revision » ou « revision » ; vide, une Étude, comme avant les Révisions et les Formations.</summary>
    public static bool LireLeType(string? saisie, out TypeDeGroupe type)
    {
        type = TypeDeGroupe.Etude;
        if (string.IsNullOrWhiteSpace(saisie))
            return true;
        var nom = Enum.GetNames<TypeDeGroupe>().FirstOrDefault(n => string.Equals(n, saisie.Trim(), StringComparison.OrdinalIgnoreCase));
        if (nom is null)
            return false;
        type = Enum.Parse<TypeDeGroupe>(nom);
        return true;
    }

    /// <summary>Les créneaux dans l'ordre saisi, ou le refus du premier qui ne va pas.</summary>
    public static (List<Creneau>? Creneaux, string? Refus) LireLesCreneaux(List<CreneauSaisi>? saisis)
    {
        if (saisis is null || saisis.Count == 0)
            return (null, AuMoinsUnCreneau);

        var creneaux = new List<Creneau>();
        foreach (var saisi in saisis)
        {
            if (saisi is null || !Jours.EssayerDeLire(saisi.Jour, out var jour)
                || !LireUneHeure(saisi.Debut, out var debut) || !LireUneHeure(saisi.Fin, out var fin))
                return (null, CreneauIncomplet);
            if (fin <= debut)
                return (null, CreneauALEnvers);
            creneaux.Add(new Creneau(jour, debut, fin));
        }

        // Deux créneaux qui se suivent (10h → 12h, puis 12h → 14h) ne se chevauchent pas.
        var chevauchement = creneaux
            .SelectMany((a, i) => creneaux.Skip(i + 1).Select(b => (a, b)))
            .Any(paire => paire.a.Jour == paire.b.Jour && paire.a.Debut < paire.b.Fin && paire.b.Debut < paire.a.Fin);
        return chevauchement ? (null, CreneauxQuiSeChevauchent) : (creneaux, null);
    }

    /// <summary>
    /// Les dates saisies une à une, dans l'ordre saisi (la première donne le nom proposé), ou le refus de la première
    /// qui ne va pas. Il en faut une par séance à venir.
    /// </summary>
    public static (List<SeancePrevue>? Seances, string? Refus) LireLesDates(List<SeanceSaisie> saisies, int seancesAVenir)
    {
        var seances = new List<SeancePrevue>();
        foreach (var saisie in saisies)
        {
            if (saisie?.Date is not { } date || !LireUneHeure(saisie.Debut, out var debut) || !LireUneHeure(saisie.Fin, out var fin))
                return (null, SeanceIncomplete);
            if (fin <= debut)
                return (null, SeanceALEnvers);
            seances.Add(new SeancePrevue(date, debut, fin));
        }

        var chevauchement = seances
            .SelectMany((a, i) => seances.Skip(i + 1).Select(b => (a, b)))
            .Any(paire => paire.a.Date == paire.b.Date && paire.a.Debut < paire.b.Fin && paire.b.Debut < paire.a.Fin);
        if (chevauchement)
            return (null, SeancesQuiSeChevauchent);
        if (seances.Count != seancesAVenir)
            return (null, $"Il faut une date pour chacune des {seancesAVenir} séances");
        return (seances, null);
    }

    /// <summary>
    /// Les tranches d'une Formation, rangées par séance, ou le refus de la première qui ne va pas. Leur somme doit faire
    /// le prix total ; sinon le refus donne l'écart.
    /// </summary>
    public static (List<Tranche>? Tranches, string? Refus) LireLesTranches(List<TrancheSaisie>? saisies, decimal prixTotal, int nombreDeSeances)
    {
        var tranches = new List<Tranche>();
        foreach (var saisie in saisies ?? [])
        {
            if (!MontantValable(saisie?.Montant))
                return (null, TrancheSansMontant);
            if (saisie!.Seance is not { } seance || seance < 1 || seance > nombreDeSeances)
                return (null, $"Une tranche est due à une séance de 1 à {nombreDeSeances}");
            tranches.Add(new Tranche(saisie.Montant!.Value, seance));
        }

        if (tranches.Select(t => t.Seance).Distinct().Count() != tranches.Count)
            return (null, TranchesSurLaMemeSeance);

        var somme = tranches.Sum(t => t.Montant);
        if (tranches.Count > 0 && somme != prixTotal)
        {
            var ecart = somme < prixTotal
                ? $"il manque {EcrireUnMontant(prixTotal - somme)} TND"
                : $"il y a {EcrireUnMontant(somme - prixTotal)} TND de trop";
            return (null, $"La somme des tranches fait {EcrireUnMontant(somme)} TND au lieu de {EcrireUnMontant(prixTotal)} TND : {ecart}");
        }

        return (tranches.OrderBy(t => t.Seance).ToList(), null);
    }

    /// <summary>Un montant dû : plus que 0, au millime, moins d'un million.</summary>
    public static bool MontantValable(decimal? montant) =>
        montant is { } m && m > 0 && m < 1_000_000 && decimal.Round(m, 3) == m;

    /// <summary>« 250,000 » : un montant au millime, avec une virgule.</summary>
    public static string EcrireUnMontant(decimal montant) => montant.ToString("0.000", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>« 15/09/2027 ».</summary>
    public static string EcrireUneDate(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static bool LireUneHeure(string? saisie, out TimeOnly heure) =>
        TimeOnly.TryParseExact(saisie?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out heure);
}
