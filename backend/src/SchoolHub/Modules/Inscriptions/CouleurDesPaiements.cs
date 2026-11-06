namespace SchoolHub.Modules.Inscriptions;

/// <summary>CAI-01 : où en est un élève avec ses paiements, du mieux au pire.</summary>
public enum Couleur
{
    /// <summary>Rien de dû à ce jour qui ne soit payé.</summary>
    AJour,

    /// <summary>Une somme est due, depuis sa séance, et la séance suivante n'a pas encore eu lieu : « à payer ».</summary>
    APayer,

    /// <summary>Une somme due à la séance k n'est pas payée à la séance k+1.</summary>
    Orange,

    /// <summary>Une somme due à la séance k n'est pas payée à la séance k+2.</summary>
    Rouge,
}

/// <summary>
/// CAI-01 : la couleur d'une somme pas payée, sans base de données. Chaque somme est due à une séance précise (k) : le
/// jour même, l'élève est « à payer » ; à la séance k+1, orange ; à la séance k+2, rouge. Les séances sont celles qui
/// comptent, rangées : un Mois dû le samedi 02/10 et pas payé passe orange le 09/10 et rouge le 16/10.
/// </summary>
public static class CouleurDesPaiements
{
    /// <summary>
    /// La couleur d'une somme due à la séance de rang <paramref name="rang"/> (0 pour la première séance du calendrier),
    /// due le <paramref name="dueLe"/>, et pas payée. Une somme pas encore due ne colore pas.
    /// </summary>
    public static Couleur DUneSomme(IReadOnlyList<DateOnly> seances, int rang, DateOnly dueLe, DateOnly aujourdhui)
    {
        if (dueLe > aujourdhui)
            return Couleur.AJour;
        DateOnly? Date(int i) => i >= 0 && i < seances.Count ? seances[i] : null;
        if (Date(rang + 2) is { } deuxiemeApres && deuxiemeApres <= aujourdhui)
            return Couleur.Rouge;
        if (Date(rang + 1) is { } suivante && suivante <= aujourdhui)
            return Couleur.Orange;
        return Couleur.APayer;
    }

    /// <summary>La pire de deux couleurs : un élève prend la pire couleur de ses inscriptions.</summary>
    public static Couleur Pire(Couleur a, Couleur b) => a > b ? a : b;
}
