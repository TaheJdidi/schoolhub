using System.Net;
using System.Net.Http.Json;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Centres;

/// <summary>
/// CEN-03 : la liste des niveaux du centre, avec les 29 niveaux scolaires tunisiens d'office.
/// Chaque test part d'un nouveau centre « Jasmins », avec ses niveaux d'office, dont Hedi est le propriétaire, connecté.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class NiveauxTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "centres_niveaux";
    private const string Adresse = "/api/niveaux";

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        var hedi = await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
        _client = await _application.ClientConnecteAsync(hedi);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    [Fact]
    public async Task La_liste_contient_les_29_niveaux_d_office_dans_l_ordre_scolaire()
    {
        var niveaux = await ListeAsync(_client);

        Assert.Equal(29, niveaux.Count);
        Assert.All(niveaux, n => Assert.True(n.DOffice));
        Assert.Equal("1ʳᵉ année de base", niveaux[0].Nom);
        Assert.Equal("9ᵉ année de base", niveaux[8].Nom);
        Assert.Equal("1ʳᵉ année secondaire", niveaux[9].Nom);
        Assert.Equal("2ᵉ année secondaire, section sciences", niveaux[10].Nom);
        Assert.Equal("3ᵉ année secondaire, section mathématiques", niveaux[15].Nom);
        Assert.Equal("4ᵉ année secondaire, section sport", niveaux[28].Nom);
        Assert.Equal(NiveauxDOffice.Liste, niveaux.Select(n => n.Nom));
    }

    [Fact]
    public async Task Une_secretaire_lit_la_liste()
    {
        var amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        var clientDAmira = await _application.ClientConnecteAsync(amira);

        Assert.Equal(29, (await ListeAsync(clientDAmira)).Count);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _application.Client().GetAsync(Adresse)).StatusCode);
    }

    [Fact]
    public async Task Un_centre_ne_voit_pas_les_niveaux_d_un_autre()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        await _application.RecopierLesNiveauxDOfficeAsync(oliviers);

        // Chaque centre a ses 29 niveaux : sans le filtre du centre, les Jasmins en verraient 58.
        Assert.Equal(29, (await ListeAsync(_client)).Count);
    }

    private static async Task<List<NiveauDuCentre>> ListeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<NiveauDuCentre>>(Adresse))!;
}
