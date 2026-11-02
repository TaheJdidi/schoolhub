using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Emails;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CentreCourant>();
builder.Services.AddDbContext<SchoolHubDbContext>(options =>
    SchoolHubDbContext.Configurer(options, builder.Configuration.GetConnectionString("SchoolHub")));
builder.Services.AjouterLesComptes();

// Les liens des emails partent de l'adresse de l'application : sans elle, l'application ne démarre pas.
builder.Services.AddOptions<OptionsDeLApplication>()
    .BindConfiguration(OptionsDeLApplication.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// En développement, les emails sont écrits dans la console. Ailleurs, aucun expéditeur n'existe encore (Brevo viendra) :
// cet expéditeur-là écrirait des liens de connexion dans les journaux du serveur.
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IEnvoiEmail, EmailDansLaConsole>();

var app = builder.Build();

if (app.Services.GetService<IEnvoiEmail>() is null)
    throw new InvalidOperationException("Aucun envoi d'email configuré");

app.UtiliserLaSession();

var api = app.MapGroup("/api");
api.MapGet("/sante", () => "SchoolHub fonctionne.").AllowAnonymous();
api.MapConnexion();
api.MapInvitation();
api.MapMotDePasseOublie();

app.Run();

// Rend le point d'entrée visible aux tests, qui lancent l'application (WebApplicationFactory).
public partial class Program;
