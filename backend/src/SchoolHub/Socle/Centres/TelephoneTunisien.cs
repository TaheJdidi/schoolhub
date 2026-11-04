namespace SchoolHub.Socle.Centres;

/// <summary>Un numéro de téléphone tunisien : 8 chiffres, écrits « 71 234 567 ».</summary>
public static class TelephoneTunisien
{
    /// <summary>
    /// Les 8 chiffres d'une saisie, sans les espaces (insécables compris), tirets et points qui les séparent,
    /// ni l'indicatif « +216 » ou « 00216 » devant ; ou <c>null</c> si ce n'est pas un numéro tunisien.
    /// </summary>
    public static string? Normaliser(string saisie)
    {
        var chiffres = new string(saisie.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '.')).ToArray());
        if (chiffres.StartsWith("+216", StringComparison.Ordinal))
            chiffres = chiffres[4..];
        else if (chiffres.StartsWith("00216", StringComparison.Ordinal))
            chiffres = chiffres[5..];

        return chiffres.Length == 8 && chiffres.All(char.IsAsciiDigit) ? chiffres : null;
    }

    /// <summary>Les 8 chiffres, groupés comme on les lit : « 71 234 567 ».</summary>
    public static string Grouper(string chiffres) => $"{chiffres[..2]} {chiffres[2..5]} {chiffres[5..]}";
}
