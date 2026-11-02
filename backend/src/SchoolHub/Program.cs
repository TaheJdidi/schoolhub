using Microsoft.AspNetCore.DataProtection;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Emails;

// « creer-centre » lance la commande au lieu du serveur. Ses options (--nom, --code…) ne doivent pas
// se mélanger à la configuration de l'application, qui lit les arguments : la commande n'en reçoit aucun.
var enCommande = args is [CommandeCreerCentre.Nom, ..];
var builder = WebApplication.CreateBuilder(enCommande ? Array.Empty<string>() : args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CentreCourant>();
builder.Services.AddDbContext<SchoolHubDbContext>(options =>
    SchoolHubDbContext.Configurer(options, builder.Configuration.GetConnectionString("SchoolHub")));
builder.Services.AjouterLesComptes();
builder.Services.AddScoped<CreationDeCentre>();

// Les liens des emails contiennent un jeton protégé par ASP.NET. La commande creer-centre le fabrique dans un autre
// processus que le serveur web, parfois lancé depuis un autre dossier (console SSH) : par défaut, la protection
// dépend de ce dossier et le serveur ne pourrait pas lire le lien. Avec un nom fixe, les deux se comprennent.
builder.Services.AddDataProtection().SetApplicationName("SchoolHub");

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

// La commande s'exécute puis l'application s'arrête, avec le code de sortie de la commande : le serveur ne démarre pas.
if (enCommande)
    return await CommandeCreerCentre.ExecuterAsync(app.Services, args[1..], Console.Out);

app.UtiliserLaSession();

var api = app.MapGroup("/api");
api.MapGet("/sante", () => "SchoolHub fonctionne.").AllowAnonymous();
api.MapConnexion();
api.MapInvitation();
api.MapMotDePasseOublie();

app.Run();
return 0;

// Rend le point d'entrée visible aux tests, qui lancent l'application (WebApplicationFactory).
public partial class Program;
