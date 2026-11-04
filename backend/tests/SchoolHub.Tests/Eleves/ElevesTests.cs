using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Eleves;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Eleves;

/// <summary>
/// ELV-02, ELV-11 et ELV-01 : la fiche d'un élève, sa suppression et la recherche. Chaque test part d'un nouveau centre
/// « Jasmins », avec ses niveaux d'office ; Amira, la secrétaire, est connectée.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class ElevesTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "eleves";
    private const string Adresse = "/api/eleves";

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _client = null!;
    private HttpClient _proprietaire = null!;
    private List<NiveauDuCentre> _niveaux = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        var amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        _client = await _application.ClientConnecteAsync(amira);
        var hedi = await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
        _proprietaire = await _application.ClientConnecteAsync(hedi);
        _niveaux = (await _client.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau(string nom) => _niveaux.Single(n => n.Nom == nom).Id;

    /// <summary>Adem Miled, en 9ᵉ année de base : son téléphone et son email sont obligatoires.</summary>
    private FicheSaisieDeLEleve Adem => new(
        "Adem", "Miled", Niveau("9ᵉ année de base"), false, "+216 98401220", null, "22 345 678", "adem@exemple.tn", "Collège Ibn Khaldoun");

    // ----- Créer (ELV-02) -----

    [Fact]
    public async Task Creer_un_eleve()
    {
        var reponse = await _client.PostAsJsonAsync(Adresse, Adem);

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var enregistrement = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!;
        var id = enregistrement.Eleve.Id;
        Assert.Equal($"/api/eleves/{id}", reponse.Headers.Location?.ToString());
        var attendu = new FicheDeLEleve(id, "Adem", "Miled", new NiveauDeLEleve(Niveau("9ᵉ année de base"), "9ᵉ année de base"), false,
            "98 401 220", null, "22 345 678", "adem@exemple.tn", "Collège Ibn Khaldoun");
        Assert.Equal(attendu, enregistrement.Eleve);
        Assert.Null(enregistrement.Avertissement);
        Assert.Equal(attendu, await _client.GetFromJsonAsync<FicheDeLEleve>($"{Adresse}/{id}"));
    }

    [Fact]
    public async Task Les_champs_facultatifs_vides_valent_null()
    {
        var eleve = await AjouterAsync(Adem with { EmailParent = "  ", Etablissement = "" });

        Assert.Equal((null, null), (eleve.Eleve.EmailParent, eleve.Eleve.Etablissement));
    }

    [Theory]
    [InlineData("prenom vide", "Le prénom est obligatoire")]
    [InlineData("prenom long", "Le prénom fait au plus 100 caractères")]
    [InlineData("nom vide", "Le nom est obligatoire")]
    [InlineData("nom long", "Le nom fait au plus 100 caractères")]
    [InlineData("sans niveau", "Choisissez le niveau")]
    [InlineData("sans telephone eleve", "Le téléphone de l'élève est obligatoire à partir de la 7ᵉ année de base")]
    [InlineData("telephone eleve", "Le téléphone de l'élève doit avoir 8 chiffres")]
    [InlineData("sans email eleve", "L'email de l'élève est obligatoire à partir de la 7ᵉ année de base")]
    [InlineData("email eleve", "L'email de l'élève n'est pas valable")]
    [InlineData("sans telephone parent", "Le téléphone du parent est obligatoire")]
    [InlineData("telephone parent", "Le téléphone du parent doit avoir 8 chiffres")]
    [InlineData("email parent", "L'email du parent n'est pas valable")]
    [InlineData("etablissement long", "L'établissement fait au plus 200 caractères")]
    public async Task Une_fiche_incomplete_est_refusee(string cas, string message)
    {
        var fiche = cas switch
        {
            "prenom vide" => Adem with { Prenom = " " },
            "prenom long" => Adem with { Prenom = new string('a', 101) },
            "nom vide" => Adem with { Nom = "" },
            "nom long" => Adem with { Nom = new string('a', 101) },
            "sans niveau" => Adem with { NiveauId = null },
            "sans telephone eleve" => Adem with { TelephoneEleve = "" },
            "telephone eleve" => Adem with { TelephoneEleve = "2234567" },
            "sans email eleve" => Adem with { EmailEleve = null },
            "email eleve" => Adem with { EmailEleve = "adem@" },
            "sans telephone parent" => Adem with { TelephoneParent = " " },
            "telephone parent" => Adem with { TelephoneParent = "123" },
            "email parent" => Adem with { EmailParent = "parent@" },
            _ => Adem with { Etablissement = new string('a', 201) },
        };

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, fiche), HttpStatusCode.BadRequest, message);
        Assert.Empty((await _client.GetFromJsonAsync<List<EleveTrouve>>(Adresse))!);
    }

    [Fact]
    public async Task La_regle_de_la_7e_annee()
    {
        var sansContacts = Adem with { TelephoneEleve = null, EmailEleve = null };

        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync(Adresse, sansContacts with { NiveauId = Niveau("6ᵉ année de base") })).StatusCode);
        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, sansContacts with { NiveauId = Niveau("7ᵉ année de base") }),
            HttpStatusCode.BadRequest, "Le téléphone de l'élève est obligatoire à partir de la 7ᵉ année de base");
        var universite = await AjouterUnNiveauAsync("Université, 1ʳᵉ année");
        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, sansContacts with { NiveauId = universite }),
            HttpStatusCode.BadRequest, "Le téléphone de l'élève est obligatoire à partir de la 7ᵉ année de base");
    }

    [Fact]
    public async Task Le_telephone_du_parent_est_facultatif_pour_un_eleve_majeur()
    {
        var eleve = await AjouterAsync(Adem with { Majeur = true, TelephoneParent = null });

        Assert.Equal((true, (string?)null), (eleve.Eleve.Majeur, eleve.Eleve.TelephoneParent));
    }

    [Fact]
    public async Task Un_niveau_d_un_autre_centre_est_introuvable()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        await _application.RecopierLesNiveauxDOfficeAsync(oliviers);
        var mounir = await _application.CreerCompteAsync(oliviers, "Mounir", "Gharbi");
        var niveauDesOliviers = (await (await _application.ClientConnecteAsync(mounir)).GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!
            .Single(n => n.Nom == "9ᵉ année de base").Id;

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, Adem with { NiveauId = niveauDesOliviers }),
            HttpStatusCode.NotFound, "Niveau introuvable");
    }

    // ----- Doublon possible -----

    [Fact]
    public async Task Un_homonyme_previent_d_un_doublon()
    {
        await AjouterAsync(new FicheSaisieDeLEleve("Hédi", "Ben Salah", Niveau("6ᵉ année de base"), false, "98 000 001", null, null, null, null));

        var eleve = await AjouterAsync(new FicheSaisieDeLEleve("HEDI", "ben salah", Niveau("6ᵉ année de base"), false, "98 000 002", null, null, null, null));

        Assert.Equal("Doublon possible : Hédi Ben Salah (6ᵉ année de base)", eleve.Avertissement);
    }

    [Fact]
    public async Task Un_telephone_croise_previent_d_un_doublon()
    {
        await AjouterAsync(Adem);
        var sami = new FicheSaisieDeLEleve("Sami", "Chaabane", Niveau("9ᵉ année de base"), false, "55 000 000", null, "55 000 001", "sami@exemple.tn", null);

        // Le téléphone de Sami est celui du parent d'Adem ; puis celui du parent de Sami est celui d'Adem.
        Assert.Equal("Doublon possible : Adem Miled (9ᵉ année de base)", (await AjouterAsync(sami with { TelephoneEleve = "98 401 220" })).Avertissement);
        Assert.Equal("Doublon possible : Adem Miled (9ᵉ année de base)",
            (await AjouterAsync(sami with { Prenom = "Amel", TelephoneParent = "22 345 678" })).Avertissement);
    }

    // ----- Modifier, supprimer (ELV-02, ELV-11) -----

    [Fact]
    public async Task Modifier_un_eleve_sans_doublon_contre_lui_meme()
    {
        var adem = (await AjouterAsync(Adem)).Eleve;

        var reponse = await _client.PutAsJsonAsync($"{Adresse}/{adem.Id}", Adem with { NiveauId = Niveau("1ʳᵉ année secondaire"), Etablissement = "Lycée Pilote" });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var enregistrement = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!;
        Assert.Null(enregistrement.Avertissement);
        Assert.Equal(("1ʳᵉ année secondaire", "Lycée Pilote"), (enregistrement.Eleve.Niveau.Nom, enregistrement.Eleve.Etablissement));
    }

    [Fact]
    public async Task Supprimer_un_eleve()
    {
        var adem = (await AjouterAsync(Adem)).Eleve;

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"{Adresse}/{adem.Id}")).StatusCode);

        await VerifierLeRefusAsync(await _client.GetAsync($"{Adresse}/{adem.Id}"), HttpStatusCode.NotFound, "Élève introuvable");
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Un_eleve_d_un_autre_centre_est_introuvable(string methode)
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        await _application.RecopierLesNiveauxDOfficeAsync(oliviers);
        var mounir = await _application.CreerCompteAsync(oliviers, "Mounir", "Gharbi");
        var clientDesOliviers = await _application.ClientConnecteAsync(mounir);
        var niveau = (await clientDesOliviers.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == "9ᵉ année de base").Id;
        var reponseDesOliviers = await clientDesOliviers.PostAsJsonAsync(Adresse, Adem with { NiveauId = niveau });
        var eleveDesOliviers = (await reponseDesOliviers.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve;

        var reponse = methode switch
        {
            "GET" => await _client.GetAsync($"{Adresse}/{eleveDesOliviers.Id}"),
            "PUT" => await _client.PutAsJsonAsync($"{Adresse}/{eleveDesOliviers.Id}", Adem),
            _ => await _client.DeleteAsync($"{Adresse}/{eleveDesOliviers.Id}"),
        };

        await VerifierLeRefusAsync(reponse, HttpStatusCode.NotFound, "Élève introuvable");
        Assert.Empty((await _client.GetFromJsonAsync<List<EleveTrouve>>(Adresse))!);
    }

    [Fact]
    public async Task La_liste_est_par_nom_puis_prenom()
    {
        var neuvieme = Niveau("9ᵉ année de base");
        await AjouterAsync(Adem with { Prenom = "Sami", Nom = "Chaabane", TelephoneEleve = "55 000 001", TelephoneParent = "55 000 002" });
        await AjouterAsync(Adem);
        await AjouterAsync(Adem with { Prenom = "Amel", Nom = "Chaabane", TelephoneEleve = "55 000 003", TelephoneParent = "55 000 004" });

        var liste = (await _client.GetFromJsonAsync<List<EleveTrouve>>(Adresse))!;

        Assert.Equal(["Amel Chaabane", "Sami Chaabane", "Adem Miled"], liste.Select(e => $"{e.Prenom} {e.Nom}"));
        Assert.All(liste, e => Assert.Equal("9ᵉ année de base", e.Niveau));
        Assert.NotEqual(Guid.Empty, neuvieme);
    }

    [Fact]
    public async Task Un_niveau_utilise_par_un_eleve_ne_se_retire_pas()
    {
        var universite = await AjouterUnNiveauAsync("Université, 1ʳᵉ année");
        await AjouterAsync(Adem with { NiveauId = universite });

        var reponse = await _proprietaire.DeleteAsync($"/api/niveaux/{universite}");

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, "Ce niveau est utilisé par un groupe ou un élève");
    }

    [Fact]
    public async Task Une_personne_non_connectee_est_refusee()
    {
        var anonyme = _application.Client();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonyme.GetAsync(Adresse)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonyme.PostAsJsonAsync(Adresse, Adem)).StatusCode);
    }

    // ----- Outils -----

    private async Task<EnregistrementDeLEleve> AjouterAsync(FicheSaisieDeLEleve fiche)
    {
        var reponse = await _client.PostAsJsonAsync(Adresse, fiche);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!;
    }

    private async Task<Guid> AjouterUnNiveauAsync(string nom)
    {
        var reponse = await _proprietaire.PostAsJsonAsync("/api/niveaux", new NomDuNiveau(nom));
        return (await reponse.Content.ReadFromJsonAsync<NiveauDuCentre>())!.Id;
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
