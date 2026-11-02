using System.Net;
using System.Net.Http.Json;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Comptes;

/// <summary>
/// CPT-06 : ce qui est réservé au propriétaire reste fermé à la secrétaire, et chaque centre
/// ne voit que ses données, même en tapant une adresse à la main.
/// Les adresses de test (<see cref="AdressesDeTest"/>) servent de cobayes : aucune adresse
/// réservée au propriétaire n'existe encore.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class RolesEtCentreCourantTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string MotDePasse = "jasmins-2027";
    private const string DonneesDuProprietaire = "données du propriétaire";

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, "comptes_roles_centre");
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    [Fact]
    public async Task Une_secretaire_n_a_pas_acces_a_ce_qui_est_reserve_au_proprietaire()
    {
        var amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Trabelsi", RoleDuCompte.Secretaire);
        var client = _application.Client();
        await SeConnecter(client, amira);

        var reponse = await client.GetAsync("/api/test/proprietaire");

        Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);
        Assert.Equal(new Avis("Accès réservé au propriétaire"), await reponse.Content.ReadFromJsonAsync<Avis>());
        Assert.DoesNotContain(DonneesDuProprietaire, await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Le_proprietaire_a_acces_a_ce_qui_lui_est_reserve()
    {
        var hedi = await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
        var client = _application.Client();
        await SeConnecter(client, hedi);

        var reponse = await client.GetAsync("/api/test/proprietaire");

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(DonneesDuProprietaire, await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sans_connexion_une_adresse_reservee_repond_401()
    {
        var reponse = await _application.Client().GetAsync("/api/test/proprietaire");

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    [Fact]
    public async Task La_page_de_sante_reste_ouverte_sans_connexion()
    {
        var reponse = await _application.Client().GetAsync("/api/sante");

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("SchoolHub fonctionne.", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Connecte_aux_Jasmins_on_ne_voit_pas_une_donnee_des_Oliviers_meme_avec_son_identifiant()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var noteDesOliviers = await _application.AjouterNoteAsync(oliviers, "note des Oliviers");
        var noteDesJasmins = await _application.AjouterNoteAsync(_jasmins, "note des Jasmins");
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();
        await SeConnecter(client, amira);

        var chezLesOliviers = await client.GetAsync($"/api/test/notes/{noteDesOliviers.Id}");
        var chezLesJasmins = await client.GetAsync($"/api/test/notes/{noteDesJasmins.Id}");

        Assert.Equal(HttpStatusCode.NotFound, chezLesOliviers.StatusCode);
        Assert.Equal(HttpStatusCode.OK, chezLesJasmins.StatusCode);
        Assert.Equal("note des Jasmins", await chezLesJasmins.Content.ReadAsStringAsync());
    }

    private static async Task SeConnecter(HttpClient client, Compte compte)
    {
        var connexion = await client.PostAsJsonAsync("/api/connexion", new DemandeDeConnexion(compte.Email, MotDePasse));
        Assert.Equal(HttpStatusCode.OK, connexion.StatusCode);
    }
}
