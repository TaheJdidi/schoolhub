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
/// ELV-04 à ELV-07 et ELV-10 : ce que l'élève doit, le prix spécial, la dispense, ce qui est payé avant SchoolHub et le
/// reste d'une Formation. Chaque test part d'un nouveau centre « Jasmins » (année scolaire du 15/09/2027 au 30/06/2028,
/// assurance de 20 TND), avec Karim Ben Salah et Adem Miled ; on est le samedi 27/11/2027. Le groupe d'Étude du samedi a
/// commencé le 18/09 : son Mois 3 va du 13/11 au 04/12, son Mois 4 commence le 11/12.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class CeQueLEleveDoitTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "ce_que_l_eleve_doit";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly CreneauSaisi Samedi10h = new("Samedi", "10:00", "12:00");
    private static readonly DateOnly DeuxiemeSeanceDuMois3 = new(2027, 11, 20);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _client = null!;
    private HttpClient _proprietaire = null!;
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
        _proprietaire = await _application.ClientConnecteAsync(hedi);
        var reglages = await _proprietaire.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", "73 221 400", null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), 20m, null, null, null));
        Assert.Equal(HttpStatusCode.OK, reglages.StatusCode);
        _client = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi"));
        _neuvieme = (await _client.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == "9ᵉ année de base").Id;
        _karim = await ProfesseurAsync(_client);
        _adem = await EleveAsync(_client, _neuvieme);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    /// <summary>Le groupe d'Étude du samedi, à 40 TND le Mois, depuis le 18/09/2027.</summary>
    private CreationDUnGroupe Etude => new(_karim, "Mathématiques", _neuvieme, [Samedi10h], 40m, new DateOnly(2027, 9, 18), null, "");

    // ----- À payer (ELV-04) -----

    [Fact]
    public async Task A_payer_ce_qui_est_du_a_ce_jour()
    {
        var inscription = await InscrireAsync(await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3);

        var aPayer = await APayerAsync(_adem);

        Assert.Equal(
            [
                new LigneAPayer(inscription, "Mathématiques · Groupe Samedi 10h → 12h", "Mois 3 (3 séances)", "Mois 3", 30m, DeuxiemeSeanceDuMois3),
                new LigneAPayer(null, "Assurance", "Assurance 2027-2028", "Assurance", 20m, DeuxiemeSeanceDuMois3),
            ],
            aPayer.Lignes);
        Assert.Equal(50m, aPayer.Total);
    }

    [Fact]
    public async Task Rien_a_payer()
    {
        // Une première séance le 04/12 : rien n'est encore dû le 27/11.
        await InscrireAsync(await GroupeAsync(Etude), null, new DateOnly(2027, 12, 4));

        var aPayer = await APayerAsync(_adem);

        Assert.Empty(aPayer.Lignes);
        Assert.Equal(0m, aPayer.Total);
    }

    // ----- Le prix spécial (ELV-05) -----

    [Fact]
    public async Task Accorder_changer_et_retirer_un_prix_special()
    {
        var inscription = await InscrireAsync(await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3);

        var accorde = await DecrireAsync(await _proprietaire.PutAsJsonAsync($"/api/inscriptions/{inscription}/prix-special",
            new PrixSpecialSaisi(20m, "  Famille en difficulté ")));
        Assert.Equal((20m, "Famille en difficulté"), (accorde.PrixSpecial, accorde.RaisonDuPrixSpecial));
        Assert.Equal(15m, (await APayerAsync(_adem)).Lignes[0].Montant);

        await DecrireAsync(await _proprietaire.PutAsJsonAsync($"/api/inscriptions/{inscription}/prix-special", new PrixSpecialSaisi(25m, "Proche du centre")));
        var retire = await DecrireAsync(await _proprietaire.DeleteAsync($"/api/inscriptions/{inscription}/prix-special"));
        Assert.Equal(((decimal?)null, (string?)null), (retire.PrixSpecial, retire.RaisonDuPrixSpecial));
        Assert.Equal(30m, (await APayerAsync(_adem)).Lignes[0].Montant);

        var journal = (await _application.JournalAsync(_jasmins)).Where(l => l.Action is ActionDuJournal.PrixSpecialAccorde or ActionDuJournal.PrixSpecialRetire)
            .Select(l => (l.Action, l.Auteur, l.Detail, l.Avant, l.Apres)).ToList();
        Assert.Equal(
            [
                (ActionDuJournal.PrixSpecialAccorde, "Hedi Ben Salah", "Adem Miled · Groupe Samedi 10h → 12h (Mathématiques) · Famille en difficulté",
                    "40,000 TND par Mois", "20,000 TND par Mois"),
                (ActionDuJournal.PrixSpecialAccorde, "Hedi Ben Salah", "Adem Miled · Groupe Samedi 10h → 12h (Mathématiques) · Proche du centre",
                    "20,000 TND par Mois", "25,000 TND par Mois"),
                (ActionDuJournal.PrixSpecialRetire, "Hedi Ben Salah", "Adem Miled · Groupe Samedi 10h → 12h (Mathématiques)",
                    "25,000 TND par Mois", "40,000 TND par Mois"),
            ],
            journal);
    }

    [Theory]
    [InlineData("sans montant", HttpStatusCode.BadRequest, "Le prix spécial doit être positif ou nul, au millime près")]
    [InlineData("négatif", HttpStatusCode.BadRequest, "Le prix spécial doit être positif ou nul, au millime près")]
    [InlineData("au-delà du millime", HttpStatusCode.BadRequest, "Le prix spécial doit être positif ou nul, au millime près")]
    [InlineData("sans raison", HttpStatusCode.BadRequest, "Écrivez la raison du prix spécial")]
    [InlineData("raison trop longue", HttpStatusCode.BadRequest, "La raison fait au plus 200 caractères")]
    [InlineData("inscription inconnue", HttpStatusCode.NotFound, "Inscription introuvable")]
    public async Task Un_prix_special_incomplet_est_refuse(string cas, HttpStatusCode statut, string message)
    {
        var inscription = await InscrireAsync(await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3);
        var (id, saisie) = cas switch
        {
            "sans montant" => (inscription, new PrixSpecialSaisi(null, "Famille en difficulté")),
            "négatif" => (inscription, new PrixSpecialSaisi(-1m, "Famille en difficulté")),
            "au-delà du millime" => (inscription, new PrixSpecialSaisi(20.0005m, "Famille en difficulté")),
            "sans raison" => (inscription, new PrixSpecialSaisi(20m, "  ")),
            "raison trop longue" => (inscription, new PrixSpecialSaisi(20m, new string('a', 201))),
            _ => (Guid.NewGuid(), new PrixSpecialSaisi(20m, "Famille en difficulté")),
        };

        await VerifierLeRefusAsync(await _proprietaire.PutAsJsonAsync($"/api/inscriptions/{id}/prix-special", saisie), statut, message);
    }

    [Fact]
    public async Task Retirer_sans_prix_special_est_refuse()
    {
        var inscription = await InscrireAsync(await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3);

        await VerifierLeRefusAsync(await _proprietaire.DeleteAsync($"/api/inscriptions/{inscription}/prix-special"),
            HttpStatusCode.Conflict, "Cette inscription n'a pas de prix spécial");
    }

    [Fact]
    public async Task La_secretaire_n_accorde_pas_de_prix_special()
    {
        var inscription = await InscrireAsync(await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3);

        await VerifierLeRefusAsync(await _client.PutAsJsonAsync($"/api/inscriptions/{inscription}/prix-special", new PrixSpecialSaisi(0m, "Proche du centre")),
            HttpStatusCode.Forbidden, "Accès réservé au propriétaire");
        await VerifierLeRefusAsync(await _client.DeleteAsync($"/api/inscriptions/{inscription}/prix-special"),
            HttpStatusCode.Forbidden, "Accès réservé au propriétaire");

        Assert.Equal(30m, (await APayerAsync(_adem)).Lignes[0].Montant);
        Assert.DoesNotContain(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.PrixSpecialAccorde);
    }

    [Fact]
    public async Task Un_prix_special_nul_ne_laisse_rien_a_payer()
    {
        var inscription = await InscrireAsync(await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3);

        await DecrireAsync(await _proprietaire.PutAsJsonAsync($"/api/inscriptions/{inscription}/prix-special", new PrixSpecialSaisi(0m, "Proche du centre")));

        Assert.Equal(["Assurance 2027-2028"], (await APayerAsync(_adem)).Lignes.Select(l => l.Libelle));
    }

    // ----- La dispense d'assurance (ELV-06) -----

    [Fact]
    public async Task Dispenser_l_assurance()
    {
        await InscrireAsync(await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3);
        var adresse = $"/api/eleves/{_adem}/assurance/dispense";

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(adresse, new RaisonSaisie("Orphelin")), HttpStatusCode.Forbidden, "Accès réservé au propriétaire");
        await VerifierLeRefusAsync(await _proprietaire.PostAsJsonAsync(adresse, new RaisonSaisie(" ")), HttpStatusCode.BadRequest, "Écrivez la raison de la dispense");
        await VerifierLeRefusAsync(await _proprietaire.PostAsJsonAsync(adresse, new RaisonSaisie(new string('a', 201))),
            HttpStatusCode.BadRequest, "La raison fait au plus 200 caractères");

        var reponse = await _proprietaire.PostAsJsonAsync(adresse, new RaisonSaisie("Orphelin"));
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(new AssuranceDeLEleve("2027-2028", 20m, DeuxiemeSeanceDuMois3, "Dispensee", "Orphelin"),
            await reponse.Content.ReadFromJsonAsync<AssuranceDeLEleve>());
        Assert.Equal(["Mois 3 (3 séances)"], (await APayerAsync(_adem)).Lignes.Select(l => l.Libelle));
        var ligne = Assert.Single(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.AssuranceDispensee);
        Assert.Equal(("Adem Miled · Assurance 2027-2028 · Orphelin", "À payer", "Dispensée"), (ligne.Detail, ligne.Avant, ligne.Apres));

        await VerifierLeRefusAsync(await _proprietaire.PostAsJsonAsync(adresse, new RaisonSaisie("Orphelin")),
            HttpStatusCode.Conflict, "L'assurance est déjà dispensée");
        var lina = await EleveAsync(_client, _neuvieme, "Lina");
        await VerifierLeRefusAsync(await _proprietaire.PostAsJsonAsync($"/api/eleves/{lina}/assurance/dispense", new RaisonSaisie("Orphelin")),
            HttpStatusCode.NotFound, "Aucune assurance cette année");
    }

    // ----- Les autres centres -----

    [Fact]
    public async Task Ce_que_doit_un_eleve_d_un_autre_centre_est_introuvable()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        await _application.RecopierLesNiveauxDOfficeAsync(oliviers);
        var secretaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));
        var niveau = (await secretaire.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == "9ᵉ année de base").Id;
        var eleve = await EleveAsync(secretaire, niveau);
        var professeur = await ProfesseurAsync(secretaire);
        var formation = (await (await secretaire.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(
            professeur, "Informatique", null, [Samedi10h], null, new DateOnly(2027, 12, 4), null, "", Type: "Formation", PrixTotal: 300m, NombreDeSeances: 10)))
            .Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        var inscription = (await (await secretaire.PostAsJsonAsync("/api/inscriptions",
            new DemandeDInscription(eleve, formation, null, new DateOnly(2027, 12, 4)))).Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id;

        await VerifierLeRefusAsync(await _client.GetAsync($"/api/eleves/{eleve}/a-payer"), HttpStatusCode.NotFound, "Élève introuvable");
        await VerifierLeRefusAsync(await _proprietaire.PutAsJsonAsync($"/api/inscriptions/{inscription}/prix-special", new PrixSpecialSaisi(0m, "Proche")),
            HttpStatusCode.NotFound, "Inscription introuvable");
        await VerifierLeRefusAsync(await _proprietaire.PostAsJsonAsync($"/api/eleves/{eleve}/assurance/dispense", new RaisonSaisie("Orphelin")),
            HttpStatusCode.NotFound, "Élève introuvable");
    }

    // ----- Outils -----

    private static async Task<Guid> ProfesseurAsync(HttpClient client)
    {
        var reponse = await client.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null));
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
    }

    /// <summary>Adem Miled (ou un autre prénom), en 9ᵉ année de base, mineur, avec son téléphone et celui de son parent.</summary>
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

    private async Task<Guid> InscrireAsync(Guid groupe, string? mode, DateOnly debutLe)
    {
        var reponse = await _client.PostAsJsonAsync("/api/inscriptions", new DemandeDInscription(_adem, groupe, mode, debutLe));
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
