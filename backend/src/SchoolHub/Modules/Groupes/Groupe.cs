namespace SchoolHub.Modules.Groupes;

/// <summary>Un jour de la semaine, écrit en français : c'est ainsi qu'il s'enregistre et qu'il circule en JSON.</summary>
public enum Jour
{
    Lundi = 1,
    Mardi,
    Mercredi,
    Jeudi,
    Vendredi,
    Samedi,
    Dimanche,
}

public static class Jours
{
    public static DayOfWeek VersDayOfWeek(Jour jour) => (DayOfWeek)((int)jour % 7);

    /// <summary>« Samedi » ou « samedi » ; un nombre ou un jour en anglais ne se lit pas.</summary>
    public static bool EssayerDeLire(string? saisie, out Jour jour)
    {
        var nom = Enum.GetNames<Jour>().FirstOrDefault(n => string.Equals(n, saisie?.Trim(), StringComparison.OrdinalIgnoreCase));
        jour = nom is null ? default : Enum.Parse<Jour>(nom);
        return nom is not null;
    }
}

/// <summary>Un créneau de la semaine : « le samedi de 10h à 12h ». Il fait partie de son groupe.</summary>
public sealed class Creneau(Jour jour, TimeOnly debut, TimeOnly fin)
{
    public Jour Jour { get; private set; } = jour;

    public TimeOnly Debut { get; private set; } = debut;

    public TimeOnly Fin { get; private set; } = fin;
}

/// <summary>
/// Le prix du Mois « à partir du Mois N » : un nouveau prix vaut pour les Mois qui n'ont pas encore commencé.
/// Il fait partie de son groupe.
/// </summary>
public sealed class PrixDuGroupe(int aPartirDuMois, decimal montant)
{
    public int APartirDuMois { get; private set; } = aPartirDuMois;

    public decimal Montant { get; private set; } = montant;
}
