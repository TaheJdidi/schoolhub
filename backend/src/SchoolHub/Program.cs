using Microsoft.AspNetCore.DataProtection;
using SchoolHub.Modules.Achats;
using SchoolHub.Modules.Bibliotheque;
using SchoolHub.Modules.Caisse;
using SchoolHub.Modules.Demandes;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Listes;
using SchoolHub.Modules.Parts;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Modules.Rh;
using SchoolHub.Modules.Tableau;
using SchoolHub.Modules.Seances;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Emails;
using SchoolHub.Socle.Journal;
using SchoolHub.Socle.Pages;

// « creer-centre » lance la commande au lieu du serveur. Ses options (--nom, --code…) ne doivent pas
// se mélanger à la configuration de l'application, qui lit les arguments : la commande n'en reçoit aucun.
var enCommande = args is [CommandeCreerCentre.Nom, ..];
var builder = WebApplication.CreateBuilder(enCommande ? Array.Empty<string>() : args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CentreCourant>();
builder.Services.AddDbContext<SchoolHubDbContext>(options =>
    SchoolHubDbContext.Configurer(options, builder.Configuration.GetConnectionString("SchoolHub")));
builder.Services.AjouterLesComptes();
builder.Services.AddScoped<JournalDuCentre>();
builder.Services.AddScoped<GestionDesComptes>();
builder.Services.AddScoped<CreationDeCentre>();
builder.Services.AddScoped<ParametresDuCentre>();
builder.Services.AddScoped<NiveauxDuCentre>();
builder.Services.AddScoped<ProfesseursDuCentre>();
builder.Services.AddScoped<GroupesDuCentre>();
builder.Services.AddScoped<ElevesDuCentre>();
builder.Services.AddScoped<InscriptionsDuCentre>();
builder.Services.AddScoped<FormulairePublic>();
builder.Services.AddScoped<DemandesDuCentre>();
builder.Services.AddScoped<AppelsDuCentre>();
builder.Services.AddScoped<CalendrierDesSeances>();
builder.Services.AddScoped<FeuillesDAppel>();
builder.Services.AddScoped<RecusDuCentre>();
builder.Services.AddScoped<RappelsDePaiement>();
builder.Services.AddScoped<CaissesDuCentre>();
builder.Services.AddScoped<BibliothequeDuCentre>();
builder.Services.AddScoped<PartsDesProfesseurs>();
builder.Services.AddScoped<AchatsDuCentre>();
builder.Services.AddScoped<RessourcesHumaines>();
builder.Services.AddScoped<TableauDeBord>();
builder.Services.AddScoped<ExportsDesListes>();
builder.Services.AddScoped<ImportsDesListes>();
builder.Services.AddScoped<SelectionDEleves>();
builder.Services.AjouterLaProtectionDuFormulaire();

// Les liens des emails contiennent un jeton protégé par ASP.NET. La commande creer-centre le fabrique dans un autre
// processus que le serveur web, parfois lancé depuis un autre dossier (console SSH) : par défaut, la protection
// dépend de ce dossier et le serveur ne pourrait pas lire le lien. Avec un nom fixe, les deux se comprennent.
// Ce nom commun est nécessaire mais pas suffisant : la commande et le serveur doivent aussi lire le même anneau
// de clés (le dossier où ASP.NET garde ses clés). À vérifier au déploiement sur App Service.
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
// L'application est libérée avant de rendre la main : la console écrit ses journaux en arrière-plan,
// et l'email d'invitation (écrit dans le journal en développement) pourrait sinon se perdre.
if (enCommande)
{
    var codeDeSortie = await CommandeCreerCentre.ExecuterAsync(app.Services, args[1..], Console.Out);
    await app.DisposeAsync();
    return codeDeSortie;
}

// L'adresse du visiteur, derrière l'App Service d'Azure, avant la limite d'envois du formulaire public (DEM-01).
// Avec ASPNETCORE_FORWARDEDHEADERS_ENABLED=true, ASP.NET lit déjà l'en-tête : une seconde lecture prendrait l'adresse
// écrite par le visiteur lui-même, et la limite tomberait.
if (!string.Equals(app.Configuration["ForwardedHeaders_Enabled"], "true", StringComparison.OrdinalIgnoreCase))
    app.UseForwardedHeaders();
// Les fichiers d'Angular passent avant la session : ils sont publics, sinon la page de connexion répondrait 401.
app.UtiliserLesFichiersDeLApplication();
app.UtiliserLaSession();
app.UseRateLimiter();

var api = app.MapGroup("/api");
api.MapGet("/sante", () => "SchoolHub fonctionne.").AllowAnonymous();
api.MapConnexion();
api.MapInvitation();
api.MapMotDePasseOublie();
api.MapComptes();
api.MapParametres();
api.MapNiveaux();
api.MapProfesseurs();
api.MapGroupes();
api.MapEleves();
api.MapInscriptions();
api.MapDemandes();
api.MapSeances();
api.MapCaisse();
api.MapBibliotheque();
api.MapParts();
api.MapAchats();
api.MapRessourcesHumaines();
api.MapTableauDeBord();
api.MapListes();
app.MapPagesDeLApplication(api);

app.Run();
return 0;

// Rend le point d'entrée visible aux tests, qui lancent l'application (WebApplicationFactory).
public partial class Program;
