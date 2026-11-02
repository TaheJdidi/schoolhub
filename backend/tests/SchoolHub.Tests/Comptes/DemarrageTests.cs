using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolHub.Socle.Emails;

namespace SchoolHub.Tests.Comptes;

/// <summary>
/// Le démarrage de l'application, sans base : les emails et l'adresse qui sert à fabriquer les liens.
/// Ces tests lancent l'application à part (pas <c>ApplicationDeTest</c>, qui remplace l'expéditeur d'emails).
/// </summary>
public sealed class DemarrageTests
{
    [Fact]
    public void En_production_sans_envoi_d_email_l_application_refuse_de_demarrer()
    {
        // Le seul expéditeur qui existe écrit les emails dans la console, et en production
        // il y écrirait des liens de connexion : il n'est donc branché qu'en développement.
        using var usine = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var erreur = Record.Exception(() => usine.CreateClient());

        Assert.NotNull(erreur);
        Assert.Contains("Aucun envoi d'email configuré", erreur.ToString());
    }

    [Fact]
    public async Task En_developpement_les_emails_sont_ecrits_dans_la_console()
    {
        var console = new JournalCapture();
        using var usine = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.AddProvider(console));
        });

        await usine.Services.GetRequiredService<IEnvoiEmail>().EnvoyerAsync(
            "amira@exemple.tn",
            "Rejoindre le Centre Les Jasmins sur SchoolHub",
            "Bonjour Amira,\nhttp://localhost:5087/invitation?compte=1&jeton=2");

        var (niveau, message) = Assert.Single(console.Messages["SchoolHub.Socle.Emails.EmailDansLaConsole"]);
        Assert.Equal(LogLevel.Information, niveau);
        Assert.Contains("amira@exemple.tn", message);
        Assert.Contains("Rejoindre le Centre Les Jasmins sur SchoolHub", message);
        Assert.Contains("http://localhost:5087/invitation?compte=1&jeton=2", message);
    }

    [Fact]
    public void Sans_adresse_de_l_application_elle_refuse_de_demarrer()
    {
        // Sans adresse, les liens des emails n'iraient nulle part.
        using var usine = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Application:Adresse", "");
        });

        var erreur = Record.Exception(() => usine.CreateClient());

        Assert.NotNull(erreur);
        Assert.Contains("Adresse", erreur.ToString());
    }

    [Fact]
    public void Les_liens_des_emails_se_lisent_depuis_n_importe_quel_dossier_de_l_application()
    {
        // La commande creer-centre fabrique le jeton d'invitation dans un autre processus que le serveur web, parfois
        // lancé depuis un autre dossier. Par défaut, ASP.NET sépare les clés de protection selon ce dossier : le serveur
        // ne pourrait plus lire le lien. Le nom de l'application, lui, est le même partout.
        using var usine = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        var options = usine.Services.GetRequiredService<IOptions<DataProtectionOptions>>();

        Assert.Equal("SchoolHub", options.Value.ApplicationDiscriminator);
    }

    /// <summary>Garde ce que l'application écrit dans la console, par catégorie de journal.</summary>
    private sealed class JournalCapture : ILoggerProvider
    {
        public Dictionary<string, List<(LogLevel Niveau, string Message)>> Messages { get; } = [];

        public ILogger CreateLogger(string categorie) => new Capture(this, categorie);

        public void Dispose()
        {
        }

        private sealed class Capture(JournalCapture parent, string categorie) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel niveau) => true;

            public void Log<TState>(LogLevel niveau, EventId evenement, TState etat, Exception? exception, Func<TState, Exception?, string> formateur)
            {
                lock (parent.Messages)
                {
                    if (!parent.Messages.TryGetValue(categorie, out var messages))
                        parent.Messages[categorie] = messages = [];
                    messages.Add((niveau, formateur(etat, exception)));
                }
            }
        }
    }
}
