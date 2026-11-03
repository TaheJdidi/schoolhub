using System.Net;
using System.Net.Http.Json;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Socle;

/// <summary>
/// Le serveur sert aussi les pages Angular : les fichiers sont publics, toute adresse de page renvoie
/// <c>index.html</c> (c'est Angular qui choisit la page), et une adresse de l'API inconnue répond en <c>{ "message" }</c>.
/// Les fichiers viennent de <c>wwwroot-de-test</c>, à la place de l'application Angular construite.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class PagesDeLApplicationTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private ApplicationDeTest _application = null!;

    public Task InitializeAsync()
    {
        // Aucune de ces adresses ne lit la base : elle n'a pas besoin d'être créée.
        _application = new ApplicationDeTest(postgreSql, "pages_application");
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    [Theory]
    [InlineData("/invitation?compte=1&jeton=2")]
    [InlineData("/aujourdhui")]
    public async Task Une_page_de_l_application_renvoie_index_html_sans_connexion(string adresse)
    {
        var reponse = await _application.Client().GetAsync(adresse);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("text/html", reponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<!-- index de test -->", await reponse.Content.ReadAsStringAsync());
        // Une mise à jour du soir doit être vue dès le lendemain : index.html n'est jamais gardé en cache.
        Assert.True(reponse.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task Les_fichiers_de_l_application_sont_servis_sans_connexion()
    {
        var client = _application.Client();

        var script = await client.GetAsync("/main-TEST1234.js");
        var manifeste = await client.GetAsync("/manifest.webmanifest");

        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal(HttpStatusCode.OK, manifeste.StatusCode);
        Assert.Equal("application/manifest+json", manifeste.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Une_adresse_de_l_api_inconnue_repond_introuvable()
    {
        var reponse = await _application.Client().GetAsync("/api/inconnue");

        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);
        Assert.Equal(new Avis("Adresse inconnue"), await reponse.Content.ReadFromJsonAsync<Avis>());
    }

    [Fact]
    public async Task L_api_reste_fermee_sans_connexion()
    {
        var reponse = await _application.Client().GetAsync("/api/moi");

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    [Theory]
    [InlineData("/aujourdhui")]
    [InlineData("/main-TEST1234.js")]
    [InlineData("/api/moi")]
    [InlineData("/api/sante")]
    public async Task Chaque_reponse_porte_Referrer_Policy_no_referrer(string adresse)
    {
        var reponse = await _application.Client().GetAsync(adresse);

        Assert.Equal("no-referrer", Assert.Single(reponse.Headers.GetValues("Referrer-Policy")));
    }
}
