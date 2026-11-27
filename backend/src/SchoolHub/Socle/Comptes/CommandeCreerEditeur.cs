using Microsoft.AspNetCore.Identity;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// La commande <c>creer-editeur</c> : elle crée un compte Éditeur invité, sans centre, et lui envoie son invitation (EDI-00).
/// C'est ainsi que naît un compte de l'équipe SchoolHub. Après son mot de passe, l'Éditeur scanne le QR code
/// de son application d'authentification. Tout ou rien : sans email parti, aucun compte.
/// </summary>
public static class CommandeCreerEditeur
{
    /// <summary>Le premier argument de l'application qui lance cette commande au lieu du serveur.</summary>
    public const string Nom = "creer-editeur";

    private static readonly string[] Options = ["--email", "--prenom", "--nom"];

    /// <summary>
    /// Lance la commande avec ses options : <c>--email</c> (obligatoire), <c>--prenom</c> et <c>--nom</c>
    /// (« Équipe » et « SchoolUp » par défaut). Rend 0 si le compte est créé, 1 sinon.
    /// </summary>
    public static async Task<int> ExecuterAsync(IServiceProvider services, string[] arguments, TextWriter sortie)
    {
        var valeurs = new Dictionary<string, string>();
        for (var i = 0; i < arguments.Length; i++)
        {
            var option = arguments[i];
            if (!Options.Contains(option))
            {
                await sortie.WriteLineAsync($"Option inconnue : {option}.");
                return 1;
            }

            // Une option suivie d'une autre option n'a pas de valeur : c'est la seconde qui est lue ensuite.
            var aUneValeur = i + 1 < arguments.Length && !arguments[i + 1].StartsWith("--", StringComparison.Ordinal);
            valeurs[option] = aUneValeur ? arguments[++i].Trim() : "";
        }

        var email = valeurs.GetValueOrDefault("--email", "");
        if (email.Length == 0)
        {
            await sortie.WriteLineAsync("Il manque l'email de l'Éditeur.");
            return 1;
        }

        var prenom = valeurs.GetValueOrDefault("--prenom") is { Length: > 0 } p ? p : "Équipe";
        var nom = valeurs.GetValueOrDefault("--nom") is { Length: > 0 } n ? n : "SchoolUp";

        await using var scope = services.CreateAsyncScope();
        var (editeur, message) = await CreerAsync(scope.ServiceProvider, prenom, nom, email);
        await sortie.WriteLineAsync(message);
        return editeur is null ? 1 : 0;
    }

    /// <summary>Crée le compte Éditeur invité et lui envoie son invitation, dans une seule transaction.</summary>
    public static async Task<(Compte? Compte, string Message)> CreerAsync(IServiceProvider services, string prenom, string nom, string email)
    {
        if (!AdresseEmail.EstValable(email))
            return (null, $"L'email {email} n'est pas valable.");
        if (prenom.Length > Compte.LongueurMaximaleDuNom || nom.Length > Compte.LongueurMaximaleDuNom)
            return (null, $"Le prénom et le nom font au plus {Compte.LongueurMaximaleDuNom} caractères.");

        var comptes = services.GetRequiredService<UserManager<Compte>>();
        if (await comptes.FindByEmailAsync(email) is not null)
            return (null, $"L'email {email} est déjà utilisé sur SchoolUp.");

        var contexte = services.GetRequiredService<SchoolHubDbContext>();
        await using var transaction = await contexte.Database.BeginTransactionAsync();

        var editeur = Compte.Editeur(prenom, nom, email);
        var creation = await comptes.CreateAsync(editeur);
        if (!creation.Succeeded)
            return (null, creation.Errors.First().Description);

        try
        {
            await services.GetRequiredService<Invitations>().EnvoyerAsync(editeur, centre: null);
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync();
            return (null, $"Rien n'a été créé : l'invitation n'est pas partie ({exception.Message}).");
        }

        await transaction.CommitAsync();
        return (editeur, $"Compte Éditeur créé. Invitation envoyée à {email}.");
    }
}
