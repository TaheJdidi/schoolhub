namespace SchoolHub.Modules.Groupes;

/// <summary>Une séance calculée depuis un créneau, avant d'être enregistrée.</summary>
public sealed record SeancePrevue(DateOnly Date, TimeOnly Debut, TimeOnly Fin);

/// <summary>
/// GRP-04 : les calculs du calendrier d'un groupe, sans base de données. Les Mois ne s'enregistrent pas :
/// ce sont les séances qui comptent, rangées par date, par paquets de 4 (ticket 01, Q17c). Une séance annulée (SEA)
/// sortira du compte, et le Mois s'allongera de lui-même.
/// </summary>
public static class CalendrierDuGroupe
{
    /// <summary>Le nombre de séances d'un Mois : fixe (ticket 01, Q9).</summary>
    public const int SeancesParMois = 4;

    /// <summary>Une séance pour chaque jour de la période (bornes comprises) qui a un créneau, rangées par date puis par heure.</summary>
    public static IReadOnlyList<SeancePrevue> Seances(IEnumerable<Creneau> creneaux, DateOnly du, DateOnly au)
    {
        var parJour = creneaux.ToLookup(c => Jours.VersDayOfWeek(c.Jour));
        var seances = new List<SeancePrevue>();
        for (var date = du; date <= au; date = date.AddDays(1))
            seances.AddRange(parJour[date.DayOfWeek].Select(c => new SeancePrevue(date, c.Debut, c.Fin)));

        return seances.OrderBy(s => s.Date).ThenBy(s => s.Debut).ToList();
    }

    /// <summary>Les séances déjà rangées, par paquets de 4 : le premier est le Mois 1. Le dernier peut être incomplet.</summary>
    public static IReadOnlyList<IReadOnlyList<T>> EnMois<T>(IEnumerable<T> seancesRangees) =>
        seancesRangees.Chunk(SeancesParMois).Select(IReadOnlyList<T> (mois) => mois).ToList();

    /// <summary>Le montant de la ligne de prix au plus grand « à partir du Mois » qui ne dépasse pas ce Mois.</summary>
    public static decimal PrixDuMois(IEnumerable<PrixDuGroupe> prix, int numero) =>
        prix.Where(p => p.APartirDuMois <= numero).MaxBy(p => p.APartirDuMois)?.Montant
        ?? throw new InvalidOperationException($"Aucun prix pour le Mois {numero}.");

    /// <summary>
    /// Le numéro du premier Mois dont la première séance vient après aujourd'hui : un Mois dont la séance 1 a lieu
    /// aujourd'hui a commencé. Si tous ont commencé, le numéro qui suit le dernier.
    /// </summary>
    public static int PremierMoisNonCommence(IReadOnlyList<DateOnly> premieresDatesDesMois, DateOnly aujourdhui)
    {
        for (var i = 0; i < premieresDatesDesMois.Count; i++)
        {
            if (premieresDatesDesMois[i] > aujourdhui)
                return i + 1;
        }

        return premieresDatesDesMois.Count + 1;
    }

    /// <summary>
    /// Les <paramref name="nombre"/> premières séances depuis <paramref name="du"/> (compris), en suivant les créneaux :
    /// une Révision ou une Formation s'arrête à son nombre de séances, pas à la fin de l'année.
    /// </summary>
    public static IReadOnlyList<SeancePrevue> SeancesEnNombre(IEnumerable<Creneau> creneaux, DateOnly du, int nombre)
    {
        var parJour = creneaux.ToLookup(c => Jours.VersDayOfWeek(c.Jour));
        var seances = new List<SeancePrevue>();
        for (var date = du; seances.Count < nombre; date = date.AddDays(1))
            seances.AddRange(parJour[date.DayOfWeek].OrderBy(c => c.Debut).Select(c => new SeancePrevue(date, c.Debut, c.Fin)));

        return seances.Take(nombre).ToList();
    }

    /// <summary>
    /// Le prix de chaque séance d'une Révision : le total divisé par le nombre de séances, arrondi au millime, et la
    /// dernière prend la différence (exigences communes) : 50 TND en 3 séances donnent 16,667 + 16,667 + 16,666.
    /// </summary>
    public static IReadOnlyList<decimal> PrixDesSeances(decimal total, int nombre)
    {
        var part = decimal.Round(total / nombre, 3, MidpointRounding.AwayFromZero);
        return [.. Enumerable.Repeat(part, nombre - 1), total - part * (nombre - 1)];
    }

    /// <summary>Le Mois d'une séance d'Étude, d'après son numéro : les séances 17 à 20 forment le Mois 5.</summary>
    public static int MoisDeLaSeance(int numero) => (numero - 1) / SeancesParMois + 1;

    /// <summary>Le numéro de la séance 1 d'un Mois : le Mois 5 commence à la séance 17.</summary>
    public static int PremiereSeanceDuMois(int mois) => (mois - 1) * SeancesParMois + 1;

    /// <summary>
    /// Une Révision ou une Formation est terminée le lendemain de sa dernière séance. Une Étude ne l'est jamais avant
    /// le passage à l'année suivante (CEN-04).
    /// </summary>
    public static bool EstTermine(TypeDeGroupe type, DateOnly? derniereSeance, DateOnly aujourdhui) =>
        type != TypeDeGroupe.Etude && derniereSeance is { } derniere && derniere < aujourdhui;

    /// <summary>« Groupe Samedi 10h → 12h » : le nom que la page propose depuis le premier créneau.</summary>
    public static string NomPropose(Creneau premier) => $"Groupe {premier.Jour} {Heure(premier.Debut)} → {Heure(premier.Fin)}";

    /// <summary>Le nom proposé depuis la première date saisie une à une : le jour de cette date, et ses heures.</summary>
    public static string NomPropose(SeancePrevue premiere) =>
        NomPropose(new Creneau(Jours.De(premiere.Date.DayOfWeek), premiere.Debut, premiere.Fin));

    /// <summary>« 10h », « 10h30 », « 8h ».</summary>
    public static string Heure(TimeOnly heure) => heure.Minute == 0 ? $"{heure.Hour}h" : $"{heure.Hour}h{heure.Minute:00}";
}
