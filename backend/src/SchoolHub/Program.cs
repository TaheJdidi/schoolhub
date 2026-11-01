using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Donnees;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<CentreCourant>();
builder.Services.AddDbContext<SchoolHubDbContext>(options =>
    SchoolHubDbContext.Configurer(options, builder.Configuration.GetConnectionString("SchoolHub")));

var app = builder.Build();

app.MapGet("/api/sante", () => "SchoolHub fonctionne.");

app.Run();
