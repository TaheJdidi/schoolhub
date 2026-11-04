using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Centres;

/// <summary>
/// CEN-02 : le propriétaire règle les coordonnées, l'année scolaire et les prix de son centre.
/// Chaque test part d'un nouveau centre « Jasmins », dont Hedi Ben Salah est le propriétaire, connecté.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class ParametresDuCentreTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "centres_parametres";
    private const string Adresse = "/api/centre/parametres";

    private static readonly ReglagesDuCentre Valides =
        new("Centre Les Jasmins", "71 234 567", "contact@jasmins.tn", null, 20.000m, 2.500m, 40, 5m);

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
    public async Task Un_nouveau_centre_n_a_que_son_nom()
    {
        var reglages = await _client.GetFromJsonAsync<ReglagesDuCentre>(Adresse);

        Assert.Equal(new ReglagesDuCentre("Centre Les Jasmins", null, null, null, null, null, null, null), reglages);
    }

    [Fact]
    public async Task Enregistrer_puis_relire_rend_les_memes_valeurs()
    {
        var reponse = await _client.PutAsJsonAsync(Adresse, Valides);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(Valides, await reponse.Content.ReadFromJsonAsync<ReglagesDuCentre>());
        Assert.Equal(Valides, await _client.GetFromJsonAsync<ReglagesDuCentre>(Adresse));
        Assert.Equal("71234567", (await CentreEnBaseAsync(_jasmins)).Telephone);
    }

    [Fact]
    public async Task Seul_le_nom_suffit()
    {
        var reponse = await _client.PutAsJsonAsync(
            Adresse, new ReglagesDuCentre("Centre Les Jasmins d'El Menzah", null, null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("Centre Les Jasmins d'El Menzah", (await _client.GetFromJsonAsync<ReglagesDuCentre>(Adresse))!.Nom);
    }

    [Theory]
    [InlineData("+216 71 234 567")]
    [InlineData("00216 71234567")]
    [InlineData("71-234-567")]
    [InlineData("71.234.567")]
    public async Task Le_telephone_accepte_espaces_tirets_points_et_indicatif(string saisie)
    {
        var reponse = await _client.PutAsJsonAsync(Adresse, Valides with { Telephone = saisie });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("71234567", (await CentreEnBaseAsync(_jasmins)).Telephone);
    }

    // Un numéro copié depuis WhatsApp ou un tableur sépare souvent ses chiffres par des espaces insécables.
    [Theory]
    [InlineData("71 234 567")]
    [InlineData("+216 71 234 567")]
    public async Task Le_telephone_accepte_les_espaces_insecables(string saisie)
    {
        var reponse = await _client.PutAsJsonAsync(Adresse, Valides with { Telephone = saisie });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("71234567", (await CentreEnBaseAsync(_jasmins)).Telephone);
    }

    [Theory]
    [InlineData("nom", "  ", "Le nom du centre est obligatoire")]
    [InlineData("telephone", "1234", "Le téléphone doit avoir 8 chiffres")]
    [InlineData("email", "centre@jasmins", "Cet email n'est pas valable")]
    [InlineData("assurance", "-5", "Un montant doit être positif, au millime près")]
    [InlineData("assurance", "20.1234", "Un montant doit être positif, au millime près")]
    [InlineData("assurance", "1000000", "Un montant doit être positif, au millime près")]
    [InlineData("entree", "-0.5", "Un montant doit être positif, au millime près")]
    [InlineData("capacite", "0", "La capacité doit être d'au moins 1 place")]
    [InlineData("part", "120", "La part du centre va de 0 à 100 %")]
    [InlineData("part", "5.555", "La part du centre va de 0 à 100 %")]
    public async Task Une_saisie_refusee_n_enregistre_rien(string champ, string valeur, string message)
    {
        await _client.PutAsJsonAsync(Adresse, Valides);
        // Les champs de texte ne se lisent pas comme un nombre : seuls les montants, la capacité et la part s'en servent.
        decimal.TryParse(valeur, NumberStyles.Number, CultureInfo.InvariantCulture, out var nombre);
        var saisie = champ switch
        {
            "nom" => Valides with { Nom = valeur },
            "telephone" => Valides with { Telephone = valeur },
            "email" => Valides with { Email = valeur },
            "assurance" => Valides with { MontantDeLAssurance = nombre },
            "entree" => Valides with { PrixDeLEntreeALaBibliotheque = nombre },
            "capacite" => Valides with { CapaciteDeLaBibliotheque = (int)nombre },
            "part" => Valides with { PartDuCentreParDefaut = nombre },
            _ => throw new ArgumentOutOfRangeException(nameof(champ)),
        };

        var reponse = await _client.PutAsJsonAsync(Adresse, saisie);

        await VerifierLeRefusAsync(reponse, message);
        Assert.Equal(Valides, await _client.GetFromJsonAsync<ReglagesDuCentre>(Adresse));
    }

    [Fact]
    public async Task Un_nom_trop_long_est_refuse()
    {
        var reponse = await _client.PutAsJsonAsync(Adresse, Valides with { Nom = new string('j', 201) });

        await VerifierLeRefusAsync(reponse, "Le nom du centre fait au plus 200 caractères");
    }

    [Fact]
    public async Task Une_secretaire_ne_regle_pas_le_centre()
    {
        var amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        var clientDAmira = await _application.ClientConnecteAsync(amira);
        var sansSession = _application.Client();

        Assert.Equal(HttpStatusCode.Forbidden, (await clientDAmira.GetAsync(Adresse)).StatusCode);
        await VerifierLeRefusAsync(await clientDAmira.PutAsJsonAsync(Adresse, Valides), "Accès réservé au propriétaire", HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Unauthorized, (await sansSession.GetAsync(Adresse)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await sansSession.PutAsJsonAsync(Adresse, Valides)).StatusCode);
    }

    [Fact]
    public async Task Un_centre_ne_touche_jamais_aux_reglages_d_un_autre()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var karim = await _application.CreerCompteAsync(oliviers, "Karim", "Ben Salah", RoleDuCompte.Proprietaire);
        var clientDeKarim = await _application.ClientConnecteAsync(karim);

        await _client.PutAsJsonAsync(Adresse, Valides);

        Assert.Equal(
            new ReglagesDuCentre("Centre Les Oliviers", null, null, null, null, null, null, null),
            await clientDeKarim.GetFromJsonAsync<ReglagesDuCentre>(Adresse));
        Assert.Null((await CentreEnBaseAsync(oliviers)).Telephone);
    }

    // ----- L'année scolaire -----

    [Fact]
    public async Task La_premiere_annee_est_creee_puis_modifiee()
    {
        await _client.PutAsJsonAsync(Adresse, Valides with { AnneeScolaire = Annee2027 });
        Assert.Equal(Annee2027, (await _client.GetFromJsonAsync<ReglagesDuCentre>(Adresse))!.AnneeScolaire);

        var decalee = new AnneeScolaireSaisie(new DateOnly(2027, 9, 16), new DateOnly(2028, 6, 30));
        var reponse = await _client.PutAsJsonAsync(Adresse, Valides with { AnneeScolaire = decalee });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(decalee, (await _client.GetFromJsonAsync<ReglagesDuCentre>(Adresse))!.AnneeScolaire);
        Assert.Equal(1, await AnneesEnBaseAsync(_jasmins));
    }

    [Fact]
    public async Task Une_annee_vide_n_efface_pas_l_annee_existante()
    {
        await _client.PutAsJsonAsync(Adresse, Valides with { AnneeScolaire = Annee2027 });

        var reponse = await _client.PutAsJsonAsync(Adresse, Valides);

        Assert.Equal(Annee2027, (await reponse.Content.ReadFromJsonAsync<ReglagesDuCentre>())!.AnneeScolaire);
        Assert.Equal(Annee2027, (await _client.GetFromJsonAsync<ReglagesDuCentre>(Adresse))!.AnneeScolaire);
    }

    [Theory]
    [InlineData("2027-09-15", null, "L'année scolaire doit avoir une date de début et une date de fin")]
    [InlineData(null, "2028-06-30", "L'année scolaire doit avoir une date de début et une date de fin")]
    [InlineData("2028-06-30", "2027-09-15", "La fin de l'année scolaire doit venir après son début")]
    [InlineData("2027-09-15", "2027-09-15", "La fin de l'année scolaire doit venir après son début")]
    public async Task Une_annee_mal_saisie_est_refusee(string? debut, string? fin, string message)
    {
        var reponse = await _client.PutAsJsonAsync(Adresse, Valides with { AnneeScolaire = Annee(debut, fin) });

        await VerifierLeRefusAsync(reponse, message);
        Assert.Equal(0, await AnneesEnBaseAsync(_jasmins));
    }

    [Theory]
    [InlineData("2028-09-15", false)]
    [InlineData("2028-09-14", true)]
    public async Task L_annee_dure_un_an_au_plus(string fin, bool acceptee)
    {
        var reponse = await _client.PutAsJsonAsync(Adresse, Valides with { AnneeScolaire = Annee("2027-09-15", fin) });

        if (acceptee)
            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        else
            await VerifierLeRefusAsync(reponse, "L'année scolaire dure un an au plus");
    }

    [Fact]
    public async Task Un_centre_ne_voit_pas_l_annee_d_un_autre()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var karim = await _application.CreerCompteAsync(oliviers, "Karim", "Ben Salah", RoleDuCompte.Proprietaire);
        var clientDeKarim = await _application.ClientConnecteAsync(karim);

        await _client.PutAsJsonAsync(Adresse, Valides with { AnneeScolaire = Annee2027 });

        Assert.Null((await clientDeKarim.GetFromJsonAsync<ReglagesDuCentre>(Adresse))!.AnneeScolaire);
        var anneeDesOliviers = Annee("2027-09-01", "2028-06-15");
        await clientDeKarim.PutAsJsonAsync(Adresse, new ReglagesDuCentre("Centre Les Oliviers", null, null, anneeDesOliviers, null, null, null, null));
        Assert.Equal(Annee2027, (await _client.GetFromJsonAsync<ReglagesDuCentre>(Adresse))!.AnneeScolaire);
        Assert.Equal(1, await AnneesEnBaseAsync(_jasmins));
    }

    private static readonly AnneeScolaireSaisie Annee2027 = Annee("2027-09-15", "2028-06-30");

    private static AnneeScolaireSaisie Annee(string? debut, string? fin) => new(
        debut is null ? null : DateOnly.Parse(debut, CultureInfo.InvariantCulture),
        fin is null ? null : DateOnly.Parse(fin, CultureInfo.InvariantCulture));

    /// <summary>Les années scolaires de ce centre en base, sans passer par le filtre du centre courant.</summary>
    private Task<int> AnneesEnBaseAsync(Centre centre) =>
        _application.DansUnScopeAsync(services =>
            services.GetRequiredService<SchoolHubDbContext>().AnneesScolaires
                .IgnoreQueryFilters()
                .CountAsync(a => a.CentreId == centre.Id));

    private Task<Centre> CentreEnBaseAsync(Centre centre) =>
        _application.DansUnScopeAsync(services =>
            services.GetRequiredService<SchoolHubDbContext>().Centres.AsNoTracking().SingleAsync(c => c.Id == centre.Id));

    private static async Task VerifierLeRefusAsync(
        HttpResponseMessage reponse, string message, HttpStatusCode statut = HttpStatusCode.BadRequest)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
