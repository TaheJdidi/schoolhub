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

    // ----- Ajouter, renommer, retirer -----

    [Fact]
    public async Task Ajouter_un_niveau()
    {
        var reponse = await AjouterAsync("Université, 1ʳᵉ année");

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var niveau = await reponse.Content.ReadFromJsonAsync<NiveauDuCentre>();
        Assert.Equal(new NiveauDuCentre(niveau!.Id, "Université, 1ʳᵉ année", false), niveau);
        Assert.Equal(niveau, (await ListeAsync(_client))[29]);
    }

    [Fact]
    public async Task Les_niveaux_ajoutes_suivent_les_niveaux_d_office_par_nom()
    {
        await AjouterAsync("Université, 2ᵉ année");
        await AjouterAsync("Adultes, remise à niveau");

        var niveaux = await ListeAsync(_client);

        Assert.Equal(31, niveaux.Count);
        Assert.Equal("Adultes, remise à niveau", niveaux[29].Nom);
        Assert.Equal("Université, 2ᵉ année", niveaux[30].Nom);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Un_nom_vide_est_refuse(string? nom)
    {
        await VerifierLeRefusAsync(await AjouterAsync(nom), HttpStatusCode.BadRequest, "Le nom du niveau est obligatoire");
    }

    [Fact]
    public async Task Un_nom_qui_s_allonge_une_fois_normalise_est_refuse_sans_erreur_de_la_base()
    {
        // « ﷺ » est un seul caractère, qui en devient 18 une fois normalisé : 50 font 900 caractères.
        var reponse = await AjouterAsync(new string('ﷺ', 50));

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, "Le nom du niveau fait au plus 100 caractères");
    }

    [Fact]
    public async Task Un_nom_trop_long_est_refuse()
    {
        var reponse = await AjouterAsync(new string('n', 101));

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, "Le nom du niveau fait au plus 100 caractères");
    }

    [Theory]
    [InlineData(" 9ᵉ Année de base ")]
    [InlineData("UNIVERSITÉ, 1ʳᵉ ANNÉE")]
    // Les lettres en exposant des niveaux d'office ne se tapent pas : « 9e » et « 1re » sont le même niveau.
    [InlineData("9e année de base")]
    [InlineData("1re Année secondaire")]
    [InlineData("Université, 1re année")]
    public async Task Un_nom_deja_dans_la_liste_est_refuse(string nom)
    {
        await AjouterAsync("Université, 1ʳᵉ année");

        await VerifierLeRefusAsync(await AjouterAsync(nom), HttpStatusCode.Conflict, ExisteDeja);
        Assert.Equal(30, (await ListeAsync(_client)).Count);
    }

    [Fact]
    public async Task Renommer_un_niveau_ajoute()
    {
        var universite = await AjouteAsync("Université, 1ʳᵉ année");
        var adultes = await AjouteAsync("Adultes");

        var reponse = await _client.PutAsJsonAsync($"{Adresse}/{universite.Id}", new NomDuNiveau(" Université, première année "));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(universite with { Nom = "Université, première année" }, await reponse.Content.ReadFromJsonAsync<NiveauDuCentre>());
        var doublon = await _client.PutAsJsonAsync($"{Adresse}/{adultes.Id}", new NomDuNiveau("université, première année"));
        await VerifierLeRefusAsync(doublon, HttpStatusCode.Conflict, ExisteDeja);
    }

    [Fact]
    public async Task Renommer_en_changeant_les_majuscules_est_permis()
    {
        var universite = await AjouteAsync("université, 1ʳᵉ année");

        var reponse = await _client.PutAsJsonAsync($"{Adresse}/{universite.Id}", new NomDuNiveau("Université, 1ʳᵉ Année"));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("Université, 1ʳᵉ Année", (await ListeAsync(_client))[29].Nom);
    }

    [Fact]
    public async Task Retirer_un_niveau_ajoute()
    {
        var universite = await AjouteAsync("Université, 1ʳᵉ année");

        var reponse = await _client.DeleteAsync($"{Adresse}/{universite.Id}");

        Assert.Equal(HttpStatusCode.NoContent, reponse.StatusCode);
        Assert.Equal(29, (await ListeAsync(_client)).Count);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Un_niveau_d_office_ne_se_modifie_pas(string methode)
    {
        var premier = (await ListeAsync(_client))[0];

        var reponse = await ModifierAsync(_client, methode, premier.Id);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, "Les niveaux d'office ne se modifient pas");
        Assert.Equal(NiveauxDOffice.Liste, (await ListeAsync(_client)).Select(n => n.Nom));
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Un_niveau_d_un_autre_centre_est_introuvable(string methode)
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var karim = await _application.CreerCompteAsync(oliviers, "Karim", "Ben Salah", RoleDuCompte.Proprietaire);
        var clientDeKarim = await _application.ClientConnecteAsync(karim);
        var reponse = await clientDeKarim.PostAsJsonAsync(Adresse, new NomDuNiveau("Adultes"));
        var adultesDesOliviers = (await reponse.Content.ReadFromJsonAsync<NiveauDuCentre>())!;

        var tentative = await ModifierAsync(_client, methode, adultesDesOliviers.Id);

        await VerifierLeRefusAsync(tentative, HttpStatusCode.NotFound, "Niveau introuvable");
        Assert.Equal("Adultes", Assert.Single(await ListeAsync(clientDeKarim)).Nom);
    }

    [Fact]
    public async Task Une_secretaire_ne_modifie_pas_les_niveaux()
    {
        var universite = await AjouteAsync("Université, 1ʳᵉ année");
        var amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        var clientDAmira = await _application.ClientConnecteAsync(amira);

        await VerifierLeRefusAsync(
            await clientDAmira.PostAsJsonAsync(Adresse, new NomDuNiveau("Adultes")), HttpStatusCode.Forbidden, AccesReserve);
        await VerifierLeRefusAsync(await ModifierAsync(clientDAmira, "PUT", universite.Id), HttpStatusCode.Forbidden, AccesReserve);
        await VerifierLeRefusAsync(await ModifierAsync(clientDAmira, "DELETE", universite.Id), HttpStatusCode.Forbidden, AccesReserve);
    }

    private const string ExisteDeja = "Ce niveau existe déjà";
    private const string AccesReserve = "Accès réservé au propriétaire";

    private Task<HttpResponseMessage> AjouterAsync(string? nom) => _client.PostAsJsonAsync(Adresse, new NomDuNiveau(nom));

    private async Task<NiveauDuCentre> AjouteAsync(string nom)
    {
        var reponse = await AjouterAsync(nom);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<NiveauDuCentre>())!;
    }

    private static Task<HttpResponseMessage> ModifierAsync(HttpClient client, string methode, Guid id) =>
        methode == "PUT"
            ? client.PutAsJsonAsync($"{Adresse}/{id}", new NomDuNiveau("Nouveau nom"))
            : client.DeleteAsync($"{Adresse}/{id}");

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }

    private static async Task<List<NiveauDuCentre>> ListeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<NiveauDuCentre>>(Adresse))!;
}
