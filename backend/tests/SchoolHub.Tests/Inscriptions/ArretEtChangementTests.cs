using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Journal;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Inscriptions;

/// <summary>
/// ELV-08 et ELV-09 : arrêter une inscription et changer un élève de groupe. Chaque test part d'un nouveau centre
/// « Jasmins » (année scolaire du 15/09/2027 au 30/06/2028, assurance de 20 TND), avec Karim Ben Salah et Adem Miled ; on
/// est le samedi 27/11/2027. Le groupe d'Étude du samedi a commencé le 18/09 : son Mois 3 va du 13/11 au 04/12, son Mois 4
/// commence le 11/12.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class ArretEtChangementTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "arret_et_changement";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly CreneauSaisi Samedi10h = new("Samedi", "10:00", "12:00");
    private static readonly DateOnly DeuxiemeSeanceDuMois3 = new(2027, 11, 20);
    private static readonly DateOnly DerniereSeanceDuMois3 = new(2027, 12, 4);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private Compte _amira = null!;
    private HttpClient _client = null!;
    private Guid _neuvieme;
    private Guid _karim;
    private Guid _adem;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        var hedi = await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
        var proprietaire = await _application.ClientConnecteAsync(hedi);
        var reglages = await proprietaire.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", "73 221 400", null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), 20m, null, null, null));
        Assert.Equal(HttpStatusCode.OK, reglages.StatusCode);
        _amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        _client = await _application.ClientConnecteAsync(_amira);
        _neuvieme = (await _client.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == "9ᵉ année de base").Id;
        _karim = await ProfesseurAsync(_client);
        _adem = await EleveAsync(_client, _neuvieme);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    /// <summary>Le groupe d'Étude du samedi, à 40 TND le Mois, depuis le 18/09/2027.</summary>
    private CreationDUnGroupe Etude => new(_karim, "Mathématiques", _neuvieme, [Samedi10h], 40m, new DateOnly(2027, 9, 18), null, "");

    // ----- Arrêter (ELV-08) -----

    [Fact]
    public async Task Arreter_une_inscription()
    {
        var inscription = await InscrireAsync(_adem, await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3);

        var arretee = await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/arret", new DateSaisie(DerniereSeanceDuMois3)));

        Assert.Equal(DerniereSeanceDuMois3, arretee.ArreteLe);
        // Le 18/12, le Mois 4 (dû le 11/12, après l'arrêt) n'est pas dû ; le Mois 3 et l'assurance le restent.
        await AllerAuAsync(new DateTimeOffset(2027, 12, 18, 9, 0, 0, TimeSpan.Zero));
        Assert.Equal(["Mois 3 (3 séances)", "Assurance 2027-2028"], (await APayerAsync(_adem)).Lignes.Select(l => l.Libelle));
        var ligne = Assert.Single(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.InscriptionArretee);
        Assert.Equal(("Amira Jlassi", "Adem Miled · Groupe Samedi 10h → 12h (Mathématiques)", "Depuis le 20/11/2027", "Arrêtée le 04/12/2027"),
            (ligne.Auteur, ligne.Detail, ligne.Avant, ligne.Apres));
    }

    [Fact]
    public async Task Un_arret_incomplet_est_refuse()
    {
        var inscription = await InscrireAsync(_adem, await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3);
        var adresse = $"/api/inscriptions/{inscription}/arret";

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(adresse, new DateSaisie(null)), HttpStatusCode.BadRequest, "Choisissez la date de l'arrêt");
        await DecrireAsync(await _client.PostAsJsonAsync(adresse, new DateSaisie(DerniereSeanceDuMois3)));
        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(adresse, new DateSaisie(DerniereSeanceDuMois3)),
            HttpStatusCode.Conflict, "Cette inscription est déjà arrêtée");
        await VerifierLeRefusAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{Guid.NewGuid()}/arret", new DateSaisie(DerniereSeanceDuMois3)),
            HttpStatusCode.NotFound, "Inscription introuvable");
    }

    [Fact]
    public async Task Reinscrire_apres_un_arret()
    {
        var etude = await GroupeAsync(Etude);
        var premiere = await InscrireAsync(_adem, etude, null, DeuxiemeSeanceDuMois3);
        await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{premiere}/arret", new DateSaisie(DerniereSeanceDuMois3)));

        await InscrireAsync(_adem, etude, null, new DateOnly(2027, 12, 11));

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync("/api/inscriptions", new DemandeDInscription(_adem, etude, null, new DateOnly(2027, 12, 18))),
            HttpStatusCode.Conflict, "Cet élève est déjà inscrit dans ce groupe");
    }

    [Fact]
    public async Task Le_groupe_ne_compte_plus_un_eleve_arrete()
    {
        var etude = await GroupeAsync(Etude);
        var inscription = await InscrireAsync(_adem, etude, null, DeuxiemeSeanceDuMois3);
        await InscrireAsync(await EleveAsync(_client, _neuvieme, "Lina"), etude, null, DeuxiemeSeanceDuMois3);

        // Arrêtée aujourd'hui : encore comptée ; le lendemain, plus.
        await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/arret", new DateSaisie(new DateOnly(2027, 11, 27))));
        Assert.Equal(2, (await _client.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{etude}"))!.Eleves);
        await AllerAuAsync(new DateTimeOffset(2027, 11, 28, 9, 0, 0, TimeSpan.Zero));
        Assert.Equal(1, (await _client.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{etude}"))!.Eleves);
        Assert.Equal(1, (await _client.GetFromJsonAsync<List<ResumeDuGroupe>>("/api/groupes"))!.Single(g => g.Id == etude).Eleves);
    }

    [Fact]
    public async Task Un_eleve_arrete_peut_etre_propose_a_nouveau()
    {
        var etude = await GroupeAsync(Etude);
        var inscription = await InscrireAsync(_adem, etude, null, DeuxiemeSeanceDuMois3);

        await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/arret", new DateSaisie(new DateOnly(2027, 12, 18))));

        var possible = (await _client.GetFromJsonAsync<List<GroupePossible>>($"/api/inscriptions/groupes-possibles?eleve={_adem}"))!.Single(g => g.Id == etude);
        Assert.False(possible.DejaInscrit);
    }

    [Fact]
    public async Task Une_inscription_d_un_autre_centre_ne_s_arrete_pas()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        await _application.RecopierLesNiveauxDOfficeAsync(oliviers);
        var secretaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));
        var niveau = (await secretaire.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == "9ᵉ année de base").Id;
        var eleve = await EleveAsync(secretaire, niveau);
        var formation = (await (await secretaire.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(
            await ProfesseurAsync(secretaire), "Informatique", null, [Samedi10h], null, new DateOnly(2027, 12, 4), null, "",
            Type: "Formation", PrixTotal: 300m, NombreDeSeances: 10))).Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        var inscription = (await (await secretaire.PostAsJsonAsync("/api/inscriptions", new DemandeDInscription(eleve, formation, null, new DateOnly(2027, 12, 4))))
            .Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id;

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/arret", new DateSaisie(DerniereSeanceDuMois3)),
            HttpStatusCode.NotFound, "Inscription introuvable");
    }

    // ----- Outils -----

    private async Task AllerAuAsync(DateTimeOffset moment)
    {
        _application.Horloge.SetUtcNow(moment);
        _client = await _application.ClientConnecteAsync(_amira);
    }

    private static async Task<Guid> ProfesseurAsync(HttpClient client)
    {
        var reponse = await client.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null));
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
    }

    private static async Task<Guid> EleveAsync(HttpClient client, Guid niveau, string prenom = "Adem")
    {
        var reponse = await client.PostAsJsonAsync("/api/eleves",
            new FicheSaisieDeLEleve(prenom, "Miled", niveau, false, "98 401 220", null, "22 345 678", "adem@exemple.tn", null));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
    }

    private async Task<Guid> GroupeAsync(CreationDUnGroupe creation)
    {
        var reponse = await _client.PostAsJsonAsync("/api/groupes", creation);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
    }

    private async Task<Guid> InscrireAsync(Guid eleve, Guid groupe, string? mode, DateOnly debutLe)
    {
        var reponse = await _client.PostAsJsonAsync("/api/inscriptions", new DemandeDInscription(eleve, groupe, mode, debutLe));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id;
    }

    private async Task<APayer> APayerAsync(Guid eleve) => (await _client.GetFromJsonAsync<APayer>($"/api/eleves/{eleve}/a-payer"))!;

    private static async Task<InscriptionDeLEleve> DecrireAsync(HttpResponseMessage reponse)
    {
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<InscriptionDeLEleve>())!;
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
