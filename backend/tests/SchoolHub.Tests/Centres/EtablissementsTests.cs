using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Demandes;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Centres;

/// <summary>
/// La liste des établissements du centre, que le formulaire public propose. Chaque test part d'un nouveau centre
/// « Jasmins », sans établissement, dont Hedi est le propriétaire, connecté.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class EtablissementsTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "centres_etablissements";
    private const string Adresse = "/api/etablissements";

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        var hedi = await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
        _client = await _application.ClientConnecteAsync(hedi);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    [Fact]
    public async Task Ajouter_des_etablissements_les_range_par_nom()
    {
        var reponse = await AjouterAsync("  Lycée Pilote Ariana ");
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        await AjouterAsync("Collège Ibn Khaldoun");

        Assert.Equal(["Collège Ibn Khaldoun", "Lycée Pilote Ariana"], (await ListeAsync(_client)).Select(e => e.Nom));
    }

    [Theory]
    [InlineData(" ", HttpStatusCode.BadRequest, "Le nom de l'établissement est obligatoire")]
    [InlineData("collège ibn khaldoun", HttpStatusCode.Conflict, "Cet établissement est déjà dans la liste")]
    public async Task Un_nom_vide_ou_deja_dans_la_liste_est_refuse(string nom, HttpStatusCode statut, string message)
    {
        await AjouterAsync("Collège Ibn Khaldoun");

        var reponse = await AjouterAsync(nom);

        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
        Assert.Single(await ListeAsync(_client));
    }

    [Fact]
    public async Task Retirer_un_etablissement()
    {
        var ajoute = (await (await AjouterAsync("Collège Ibn Khaldoun")).Content.ReadFromJsonAsync<EtablissementDuCentre>())!;

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"{Adresse}/{ajoute.Id}")).StatusCode);
        Assert.Empty(await ListeAsync(_client));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"{Adresse}/{ajoute.Id}")).StatusCode);
    }

    [Fact]
    public async Task Une_secretaire_lit_la_liste_sans_la_changer()
    {
        await AjouterAsync("Collège Ibn Khaldoun");
        var amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        var clientDAmira = await _application.ClientConnecteAsync(amira);

        Assert.Single(await ListeAsync(clientDAmira));
        Assert.Equal(HttpStatusCode.Forbidden, (await clientDAmira.PostAsJsonAsync(Adresse, new NomDeLEtablissement("Lycée"))).StatusCode);
    }

    [Fact]
    public async Task Un_centre_ne_voit_pas_les_etablissements_d_un_autre_et_le_formulaire_public_montre_les_siens()
    {
        await AjouterAsync("Collège Ibn Khaldoun");
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.CreerCompteAsync(oliviers, "Rim", "Saidi", RoleDuCompte.Proprietaire);
        var clientDeRim = await _application.ClientConnecteAsync(rim);
        await clientDeRim.PostAsJsonAsync(Adresse, new NomDeLEtablissement("Lycée Pilote Ariana"));

        Assert.Equal(["Collège Ibn Khaldoun"], (await ListeAsync(_client)).Select(e => e.Nom));
        var formulaire = (await _application.CreateClient().GetFromJsonAsync<FormulaireDuCentre>($"/api/public/inscription/{_jasmins.Code}"))!;
        Assert.Equal(["Collège Ibn Khaldoun"], formulaire.Etablissements);
    }

    private Task<HttpResponseMessage> AjouterAsync(string nom) => _client.PostAsJsonAsync(Adresse, new NomDeLEtablissement(nom));

    private static async Task<List<EtablissementDuCentre>> ListeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<EtablissementDuCentre>>(Adresse))!;
}
