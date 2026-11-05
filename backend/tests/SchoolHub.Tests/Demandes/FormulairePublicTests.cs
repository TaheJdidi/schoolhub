using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Modules.Demandes;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Demandes;

/// <summary>
/// DEM-01 : le formulaire public d'inscription, sans compte. Chaque test part d'un nouveau centre « Jasmins » (année
/// scolaire du 15/09/2027 au 30/06/2028) et de ses groupes de Karim Ben Salah ; on est le samedi 27/11/2027. Un visiteur
/// n'est pas connecté.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class FormulairePublicTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "formulaire_public";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly CreneauSaisi Samedi10h = new("Samedi", "10:00", "12:00");

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _secretaire = null!;
    private HttpClient _visiteur = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _maths;
    private Guid _arabe;
    private Guid _informatique;
    private Guid _anglaisFerme;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        var proprietaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire));
        Assert.Equal(HttpStatusCode.OK, (await proprietaire.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", null, null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), 20m, null, null, null))).StatusCode);
        _secretaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi"));
        _niveaux = (await _secretaire.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        var karim = (await (await _secretaire.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        _maths = await GroupeAsync(new CreationDUnGroupe(karim, "Mathématiques", Niveau("9ᵉ année de base"), [Samedi10h], 40m, new DateOnly(2027, 9, 18), 15, ""));
        _arabe = await GroupeAsync(new CreationDUnGroupe(karim, "Arabe", Niveau("6ᵉ année de base"), [Samedi10h], 35m, new DateOnly(2027, 9, 18), null, ""));
        _informatique = await GroupeAsync(new CreationDUnGroupe(karim, "Informatique", null, [Samedi10h], null, new DateOnly(2027, 12, 4), null, "",
            Type: "Formation", PrixTotal: 300m, NombreDeSeances: 10));
        _anglaisFerme = await GroupeAsync(new CreationDUnGroupe(karim, "Anglais", Niveau("9ᵉ année de base"), [Samedi10h], 40m, new DateOnly(2027, 9, 18), null, ""));
        Assert.Equal(HttpStatusCode.OK, (await _secretaire.PostAsJsonAsync($"/api/groupes/{_anglaisFerme}/fermeture",
            new FermetureDUnGroupe(new DateOnly(2027, 11, 20), "Trop peu d'élèves"))).StatusCode);
        _visiteur = _application.CreateClient();
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau(string nom) => _niveaux.Single(n => n.Nom == nom).Id;

    private string Adresse => $"/api/public/inscription/{_jasmins.Code}";

    /// <summary>Adem Miled, en 9ᵉ année de base, qui veut les Mathématiques et l'Informatique.</summary>
    private DemandeEnvoyee Adem => new(
        "Adem", "Miled", Niveau("9ᵉ année de base"), false, "98 401 220", null, "22 345 678", "adem@exemple.tn", null,
        [_maths, _informatique], "  Sami Chaabane ", null);

    // ----- Le formulaire -----

    [Fact]
    public async Task Le_formulaire_du_centre()
    {
        var reponse = await _visiteur.GetAsync(Adresse);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var texte = await reponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("eleves", texte, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("capacite", texte, StringComparison.OrdinalIgnoreCase);
        var formulaire = (await reponse.Content.ReadFromJsonAsync<FormulaireDuCentre>())!;
        Assert.Equal(("Centre Les Jasmins", "2027-2028"), (formulaire.Centre, formulaire.Annee));
        Assert.Equal([_arabe, _informatique, _maths], formulaire.Groupes.Select(g => g.Id));
        var maths = formulaire.Groupes.Single(g => g.Id == _maths);
        Assert.Equal(("Mathématiques", Niveau("9ᵉ année de base"), "Etude", "Karim Ben Salah"), (maths.Matiere, maths.NiveauId, maths.Type, maths.Professeur));
        Assert.Equal([new CreneauDuGroupe("Samedi", "10:00", "12:00")], maths.Creneaux);
        Assert.Null(formulaire.Groupes.Single(g => g.Id == _informatique).NiveauId);
        Assert.Contains(formulaire.Niveaux, n => n.Nom == "9ᵉ année de base" && n.DOffice);
    }

    [Fact]
    public async Task Un_lien_inconnu()
    {
        await VerifierLeRefusAsync(await _visiteur.GetAsync("/api/public/inscription/inconnu"), HttpStatusCode.NotFound, "Ce lien d'inscription n'existe pas");
        await VerifierLeRefusAsync(await _visiteur.PostAsJsonAsync("/api/public/inscription/inconnu", Adem),
            HttpStatusCode.NotFound, "Ce lien d'inscription n'existe pas");
    }

    // ----- Envoyer -----

    [Fact]
    public async Task Envoyer_une_demande()
    {
        var reponse = await _visiteur.PostAsJsonAsync(Adresse, Adem);

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        Assert.Equal(new DemandeRecue("Adem"), await reponse.Content.ReadFromJsonAsync<DemandeRecue>());
        var demande = Assert.Single(await DemandesAsync());
        Assert.Equal((_jasmins.Id, "Adem", "Miled", "98401220", "22345678", "Sami Chaabane", EtatDeLaDemande.ATraiter),
            (demande.CentreId, demande.Prenom, demande.Nom, demande.TelephoneParent, demande.TelephoneEleve, demande.Amis, demande.Etat));
        Assert.Equal([_maths, _informatique], demande.GroupesPreferes.Select(g => g.GroupeId));
        Assert.Equal(Aujourdhui, demande.RecueLe);
    }

    [Theory]
    [InlineData("sans prénom", "Le prénom est obligatoire")]
    [InlineData("aucun groupe", "Choisissez au moins une matière")]
    [InlineData("groupe d'un autre niveau", "Ce groupe n'est plus proposé")]
    [InlineData("groupe inconnu", "Ce groupe n'est plus proposé")]
    [InlineData("deux de la même matière", "Choisissez un seul groupe par matière")]
    [InlineData("amis trop longs", "Les amis font au plus 500 caractères")]
    public async Task Une_demande_incomplete_est_refusee(string cas, string message)
    {
        var demande = cas switch
        {
            "sans prénom" => Adem with { Prenom = " " },
            "aucun groupe" => Adem with { Groupes = [] },
            "groupe d'un autre niveau" => Adem with { Groupes = [_arabe] },
            "groupe inconnu" => Adem with { Groupes = [Guid.NewGuid()] },
            "deux de la même matière" => Adem with { Groupes = [_maths, await AutreGroupeDeMathsAsync()] },
            _ => Adem with { Amis = new string('a', 501) },
        };

        await VerifierLeRefusAsync(await _visiteur.PostAsJsonAsync(Adresse, demande), HttpStatusCode.BadRequest, message);
        Assert.Empty(await DemandesAsync());
    }

    [Fact]
    public async Task Un_groupe_qui_n_est_plus_propose()
    {
        // Le groupe d'Anglais a fermé pendant que le parent remplissait le formulaire.
        await VerifierLeRefusAsync(await _visiteur.PostAsJsonAsync(Adresse, Adem with { Groupes = [_anglaisFerme] }),
            HttpStatusCode.BadRequest, "Ce groupe n'est plus proposé");
    }

    [Fact]
    public async Task Le_champ_piege_n_enregistre_rien()
    {
        var reponse = await _visiteur.PostAsJsonAsync(Adresse, Adem with { SiteWeb = "https://robot.example" });

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        Assert.Empty(await DemandesAsync());
    }

    [Fact]
    public async Task Six_envois_dans_l_heure()
    {
        HttpRequestMessage Envoi(string adresseIp)
        {
            var requete = new HttpRequestMessage(HttpMethod.Post, Adresse) { Content = JsonContent.Create(Adem) };
            requete.Headers.Add("X-Forwarded-For", adresseIp);
            return requete;
        }

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Created, (await _visiteur.SendAsync(Envoi("41.226.10.1"))).StatusCode);

        await VerifierLeRefusAsync(await _visiteur.SendAsync(Envoi("41.226.10.1")), HttpStatusCode.TooManyRequests,
            "Trop de demandes envoyées depuis cette connexion. Réessayez dans une heure.");
        Assert.Equal(HttpStatusCode.Created, (await _visiteur.SendAsync(Envoi("41.226.10.2"))).StatusCode);
    }

    [Fact]
    public async Task Une_personne_connectee_ailleurs_envoie_au_bon_centre()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));

        var formulaire = (await rim.GetFromJsonAsync<FormulaireDuCentre>(Adresse))!;
        var reponse = await rim.PostAsJsonAsync(Adresse, Adem);

        Assert.Equal("Centre Les Jasmins", formulaire.Centre);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        Assert.Single(await DemandesAsync());
        Assert.Empty(await DemandesAsync(oliviers.Id));
    }

    // ----- Outils -----

    private async Task<Guid> GroupeAsync(CreationDUnGroupe creation)
    {
        var reponse = await _secretaire.PostAsJsonAsync("/api/groupes", creation);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
    }

    private async Task<Guid> AutreGroupeDeMathsAsync()
    {
        var professeur = (await (await _secretaire.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Lina", "Gharbi", "97 000 001", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        return await GroupeAsync(new CreationDUnGroupe(professeur, "Mathématiques", Niveau("9ᵉ année de base"),
            [new CreneauSaisi("Mardi", "17:00", "19:00")], 45m, new DateOnly(2027, 9, 21), null, ""));
    }

    /// <summary>Les demandes d'un centre (celui du test par défaut), lues sans le filtre du centre courant.</summary>
    private Task<List<DemandeDInscription>> DemandesAsync(Guid? centre = null) =>
        _application.DansUnScopeAsync(services =>
            services.GetRequiredService<SchoolHubDbContext>().DemandesDInscription.IgnoreQueryFilters()
                .Where(d => d.CentreId == (centre ?? _jasmins.Id))
                .Include(d => d.GroupesPreferes)
                .ToListAsync());

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
