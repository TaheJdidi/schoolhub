using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Tests.Outils;

/// <summary>
/// L'application SchoolHub lancée en mémoire pour les tests, avec de vraies requêtes HTTP,
/// sur sa propre base dans le PostgreSQL jetable. Elle a une horloge de test, et le contexte
/// de test (<see cref="ContexteDeTest"/>) pour que les tests d'isolation aient leurs notes.
/// </summary>
public sealed class ApplicationDeTest(PostgreSqlDeTest postgreSql, string nomDeLaBase) : WebApplicationFactory<Program>
{
    /// <summary>
    /// L'horloge du cookie de session. Elle part du vrai « maintenant » : le client HTTP,
    /// qui lit l'heure réelle, ne voit donc jamais le cookie comme expiré.
    /// Identity ne la lit pas pour le blocage ni pour les jetons : il prend l'heure réelle.
    /// </summary>
    public FakeTimeProvider Horloge { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:SchoolHub", postgreSql.ChaineDeConnexion(nomDeLaBase));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Horloge);
            services.RemoveAll<SchoolHubDbContext>();
            services.AddScoped<SchoolHubDbContext, ContexteDeTest>();
        });
    }

    /// <summary>Crée la base de cette classe de tests si elle n'existe pas encore.</summary>
    public Task InitialiserLaBaseAsync() =>
        DansUnScopeAsync(services => services.GetRequiredService<SchoolHubDbContext>().Database.EnsureCreatedAsync());

    /// <summary>
    /// Un client qui garde ses cookies d'une requête à l'autre, comme un navigateur.
    /// Il parle en HTTPS, car le cookie de session est <c>Secure</c>.
    /// </summary>
    public HttpClient Client() =>
        CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    /// <summary>Un nouveau centre, avec un code unique : les tests ne se gênent pas entre eux.</summary>
    public Task<Centre> CreerCentreAsync(string nom = "Centre Les Jasmins") =>
        DansUnScopeAsync(async services =>
        {
            var centre = new Centre(nom, "les-jasmins-" + Unique());
            var contexte = services.GetRequiredService<SchoolHubDbContext>();
            contexte.Centres.Add(centre);
            await contexte.SaveChangesAsync();
            return centre;
        });

    /// <summary>
    /// Un nouveau compte, créé comme le fera l'application : par Identity, qui hache le mot de passe.
    /// Sans mot de passe (<c>null</c>), c'est un compte qui n'a pas encore choisi le sien.
    /// L'email par défaut est unique, par exemple « amira.1a2b3c4d@exemple.tn ».
    /// </summary>
    public Task<Compte> CreerCompteAsync(
        Centre centre,
        string prenom = "Amira",
        string nom = "Trabelsi",
        RoleDuCompte role = RoleDuCompte.Secretaire,
        EtatDuCompte etat = EtatDuCompte.Actif,
        string? motDePasse = "jasmins-2027",
        string? email = null) =>
        DansUnScopeAsync(async services =>
        {
            var compte = new Compte(centre.Id, prenom, nom, email ?? $"{prenom}.{Unique()}@exemple.tn".ToLowerInvariant(), role);
            switch (etat)
            {
                case EtatDuCompte.Actif:
                    compte.Activer();
                    break;
                case EtatDuCompte.Desactive:
                    compte.Desactiver();
                    break;
            }

            var comptes = services.GetRequiredService<UserManager<Compte>>();
            var resultat = motDePasse is null
                ? await comptes.CreateAsync(compte)
                : await comptes.CreateAsync(compte, motDePasse);
            if (!resultat.Succeeded)
                throw new InvalidOperationException(
                    "Le compte de test n'a pas pu être créé : " + string.Join(" ", resultat.Errors.Select(e => e.Description)));

            return compte;
        });

    /// <summary>Lit ou modifie la base depuis un test, avec les services de l'application.</summary>
    public async Task<T> DansUnScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];
}
