using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Rh;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Rh;

/// <summary>
/// RHA-01 à RHA-03 : les ressources humaines. Chaque test part d'un nouveau centre « Jasmins », avec le propriétaire Hedi
/// Ben Salah et la secrétaire Amira Jlassi. On est le samedi 27/11/2027 à 10h à Tunis.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class RessourcesHumainesTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "ressources_humaines";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _hedi = null!;
    private HttpClient _amira = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        _hedi = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire));
        _amira = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi"));
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private static EmployeSaisi Amira => new("Amira", "Jlassi", "amira@jasmins.tn", "98 111 222", "Secrétaire", "Lundi à samedi, 8h → 16h", 900m);

    private static EmployeSaisi Fatma => new("Fatma", "Ayari", null, null, "Agent de ménage", null, 450m);

    // ----- La fiche (RHA-01) -----

    [Fact]
    public async Task Creer_modifier_et_marquer_parti()
    {
        var amira = await AjouterAsync(Amira);
        await AjouterAsync(Fatma);

        Assert.Equal(("98 111 222", "Lundi à samedi, 8h → 16h", 900m, null), (amira.Telephone, amira.Horaires, amira.Salaire, amira.PartiLe));
        var modifiee = await _hedi.PutAsJsonAsync($"/api/employes/{amira.Id}", Amira with { Salaire = 950m });
        Assert.Equal(950m, (await modifiee.Content.ReadFromJsonAsync<EmployeDecrit>())!.Salaire);

        var partie = await _hedi.PostAsync($"/api/employes/{amira.Id}/depart", null);
        Assert.Equal(new DateOnly(2027, 11, 27), (await partie.Content.ReadFromJsonAsync<EmployeDecrit>())!.PartiLe);
        // Ayari, puis Jlassi : la fiche partie reste dans la liste, avec sa date.
        Assert.Equal([("Fatma", null), ("Amira", (DateOnly?)new DateOnly(2027, 11, 27))], (await ListeAsync("")).Select(e => (e.Prenom, e.PartiLe)));

        var revenue = await _hedi.PostAsync($"/api/employes/{amira.Id}/retour", null);
        Assert.Null((await revenue.Content.ReadFromJsonAsync<EmployeDecrit>())!.PartiLe);
    }

    [Theory]
    [InlineData("", "Jlassi", null, null, "Secrétaire", "900", "Le prénom est obligatoire")]
    [InlineData("Amira", "", null, null, "Secrétaire", "900", "Le nom est obligatoire")]
    [InlineData("Amira", "Jlassi", "amira", null, "Secrétaire", "900", "L'email n'est pas valable")]
    [InlineData("Amira", "Jlassi", null, "98 111", "Secrétaire", "900", "Le téléphone doit avoir 8 chiffres")]
    [InlineData("Amira", "Jlassi", null, null, " ", "900", "Écrivez le poste")]
    [InlineData("Amira", "Jlassi", null, null, "Secrétaire", "-1", "Le salaire est 0 ou plus, au millime près")]
    [InlineData("Amira", "Jlassi", null, null, "Secrétaire", "900.0001", "Le salaire est 0 ou plus, au millime près")]
    public async Task Une_fiche_refusee(string prenom, string nom, string? email, string? telephone, string poste, string salaire, string message)
    {
        await VerifierLeRefusAsync(await _hedi.PostAsJsonAsync("/api/employes", new EmployeSaisi(prenom, nom, email, telephone, poste, null,
            decimal.Parse(salaire, System.Globalization.CultureInfo.InvariantCulture))), HttpStatusCode.BadRequest, message);
    }

    // ----- Les congés (RHA-02) -----

    [Fact]
    public async Task Les_jours_de_conge_de_l_annee()
    {
        var fatma = await AjouterAsync(Fatma);

        await CongeAsync(fatma.Id, new CongeSaisi(new DateOnly(2027, 8, 2), new DateOnly(2027, 8, 6), "Vacances d'été"));
        // Du 30/12/2027 au 03/01/2028 : 2 jours en 2027, 3 en 2028.
        var aCheval = await CongeAsync(fatma.Id, new CongeSaisi(new DateOnly(2027, 12, 30), new DateOnly(2028, 1, 3), null));

        Assert.Equal(5, aCheval.Jours);
        Assert.Equal(7, (await ListeAsync("")).Single().JoursDeConge);
        Assert.Equal(3, (await ListeAsync("?annee=2028")).Single().JoursDeConge);
        Assert.Equal(2, (await _hedi.GetFromJsonAsync<List<CongeDecrit>>($"/api/employes/{fatma.Id}/conges"))!.Count);

        await VerifierLeRefusAsync(await _hedi.PostAsJsonAsync($"/api/employes/{fatma.Id}/conges", new CongeSaisi(new DateOnly(2027, 8, 6), new DateOnly(2027, 8, 2), null)),
            HttpStatusCode.BadRequest, "Le congé finit après son début");
        await VerifierLeRefusAsync(await _hedi.PostAsJsonAsync($"/api/employes/{fatma.Id}/conges", new CongeSaisi(null, new DateOnly(2027, 8, 2), null)),
            HttpStatusCode.BadRequest, "Choisissez les dates du congé");

        Assert.Equal(HttpStatusCode.NoContent, (await _hedi.DeleteAsync($"/api/conges/{aCheval.Id}")).StatusCode);
        Assert.Equal(5, (await ListeAsync("")).Single().JoursDeConge);
    }

    // ----- Les salaires (RHA-03) -----

    [Fact]
    public async Task Les_salaires_d_un_mois_payes_ou_a_payer()
    {
        var amira = await AjouterAsync(Amira);
        var fatma = await AjouterAsync(Fatma);

        var octobre = await MarquerAsync(new SalaireSaisi(amira.Id, "2027-10", true));

        Assert.Equal("2027-10", octobre.Mois);
        Assert.Equal([("Fatma Ayari", 450m, false), ("Amira Jlassi", 900m, true)], octobre.Salaires.Select(s => (s.Nom, s.Montant, s.Paye)));
        Assert.Equal((900m, 450m), (octobre.TotalPaye, octobre.TotalAPayer));
        Assert.Equal(new DateOnly(2027, 11, 27), octobre.Salaires[1].PayeLe);

        // Le salaire payé reste figé, même si la fiche change.
        await _hedi.PutAsJsonAsync($"/api/employes/{amira.Id}", Amira with { Salaire = 1000m });
        Assert.Equal(900m, (await SalairesAsync("2027-10")).Salaires.Single(s => s.EmployeId == amira.Id).Montant);
        // « À payer » le défige.
        Assert.Equal(1000m, (await MarquerAsync(new SalaireSaisi(amira.Id, "2027-10", false))).Salaires.Single(s => s.EmployeId == amira.Id).Montant);

        // Fatma part : elle est encore dans novembre, plus dans décembre.
        await _hedi.PostAsync($"/api/employes/{fatma.Id}/depart", null);
        Assert.Contains((await SalairesAsync("2027-11")).Salaires, s => s.EmployeId == fatma.Id);
        Assert.DoesNotContain((await SalairesAsync("2027-12")).Salaires, s => s.EmployeId == fatma.Id);

        await VerifierLeRefusAsync(await _hedi.PostAsJsonAsync("/api/salaires", new SalaireSaisi(amira.Id, "octobre", true)), HttpStatusCode.BadRequest, "Choisissez le mois");
    }

    [Fact]
    public async Task Tout_est_reserve_au_proprietaire_de_son_centre()
    {
        var fatma = await AjouterAsync(Fatma);

        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.GetAsync("/api/employes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.GetAsync("/api/salaires")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.PostAsJsonAsync("/api/employes", Fatma)).StatusCode);

        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi", RoleDuCompte.Proprietaire));
        Assert.Empty((await rim.GetFromJsonAsync<List<EmployeDecrit>>("/api/employes"))!);
        await VerifierLeRefusAsync(await rim.PutAsJsonAsync($"/api/employes/{fatma.Id}", Fatma), HttpStatusCode.NotFound, "Employé introuvable");
        await VerifierLeRefusAsync(await rim.GetAsync($"/api/employes/{fatma.Id}/conges"), HttpStatusCode.NotFound, "Employé introuvable");
    }

    // ----- Outils -----

    private async Task<EmployeDecrit> AjouterAsync(EmployeSaisi saisie)
    {
        var reponse = await _hedi.PostAsJsonAsync("/api/employes", saisie);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EmployeDecrit>())!;
    }

    private async Task<List<EmployeDecrit>> ListeAsync(string filtres) => (await _hedi.GetFromJsonAsync<List<EmployeDecrit>>($"/api/employes{filtres}"))!;

    private async Task<CongeDecrit> CongeAsync(Guid employe, CongeSaisi saisie)
    {
        var reponse = await _hedi.PostAsJsonAsync($"/api/employes/{employe}/conges", saisie);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<CongeDecrit>())!;
    }

    private async Task<SalairesDuMois> SalairesAsync(string mois) => (await _hedi.GetFromJsonAsync<SalairesDuMois>($"/api/salaires?mois={mois}"))!;

    private async Task<SalairesDuMois> MarquerAsync(SalaireSaisi saisie)
    {
        var reponse = await _hedi.PostAsJsonAsync("/api/salaires", saisie);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<SalairesDuMois>())!;
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
