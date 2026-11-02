namespace SchoolHub.Socle.Centres;

/// <summary>
/// La commande <c>creer-centre</c> de l'application (CEN-01) : elle lit les options, crée le centre
/// et son propriétaire, écrit le résultat, puis rend le code de sortie (0 si tout va bien, 1 sinon).
/// </summary>
public static class CommandeCreerCentre
{
    /// <summary>Le premier argument de l'application qui lance cette commande au lieu du serveur.</summary>
    public const string Nom = "creer-centre";

    // Les options reconnues, dans l'ordre où l'on vérifie qu'elles sont remplies, avec le message de chacune si elle manque.
    private static readonly (string Option, string Manque)[] Options =
    [
        ("--nom", "Il manque le nom du centre."),
        ("--code", "Il manque le code du centre."),
        ("--prenom", "Il manque le prénom du propriétaire."),
        ("--nom-proprietaire", "Il manque le nom du propriétaire."),
        ("--email", "Il manque l'email du propriétaire."),
    ];

    /// <summary>
    /// Lance la commande avec ses options, par exemple <c>--nom "Centre Les Jasmins" --code les-jasmins …</c>
    /// (sans le mot <see cref="Nom"/>). Le centre, le compte et l'invitation partagent un même scope,
    /// donc une même transaction.
    /// </summary>
    public static async Task<int> ExecuterAsync(IServiceProvider services, string[] arguments, TextWriter sortie)
    {
        var (valeurs, probleme) = Lire(arguments);
        if (probleme is not null)
        {
            await sortie.WriteLineAsync(probleme);
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var resultat = await scope.ServiceProvider.GetRequiredService<CreationDeCentre>().CreerAsync(
            new NouveauCentre(valeurs["--nom"], valeurs["--code"], valeurs["--prenom"], valeurs["--nom-proprietaire"], valeurs["--email"]));

        await sortie.WriteLineAsync(resultat.Message);
        return resultat.Reussi ? 0 : 1;
    }

    /// <summary>Lit les paires « --option valeur », sans les espaces autour des valeurs. Le problème est celui à écrire à la personne.</summary>
    private static (Dictionary<string, string> Valeurs, string? Probleme) Lire(string[] arguments)
    {
        var valeurs = new Dictionary<string, string>();
        for (var i = 0; i < arguments.Length; i++)
        {
            var option = arguments[i];
            if (!Options.Any(o => o.Option == option))
                return (valeurs, $"Option inconnue : {option}.");

            // Une option suivie d'une autre option n'a pas de valeur : c'est la seconde qui est lue ensuite.
            var aUneValeur = i + 1 < arguments.Length && !arguments[i + 1].StartsWith("--", StringComparison.Ordinal);
            valeurs[option] = aUneValeur ? arguments[++i].Trim() : "";
        }

        foreach (var (option, manque) in Options)
            if (!valeurs.TryGetValue(option, out var valeur) || valeur.Length == 0)
                return (valeurs, manque);

        return (valeurs, null);
    }
}
