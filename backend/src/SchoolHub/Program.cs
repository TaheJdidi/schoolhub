using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CentreCourant>();
builder.Services.AddDbContext<SchoolHubDbContext>(options =>
    SchoolHubDbContext.Configurer(options, builder.Configuration.GetConnectionString("SchoolHub")));
builder.Services.AjouterLesComptes();

var app = builder.Build();

app.UtiliserLaSession();

var api = app.MapGroup("/api");
api.MapGet("/sante", () => "SchoolHub fonctionne.").AllowAnonymous();
api.MapConnexion();

app.Run();

// Rend le point d'entrée visible aux tests, qui lancent l'application (WebApplicationFactory).
public partial class Program;
