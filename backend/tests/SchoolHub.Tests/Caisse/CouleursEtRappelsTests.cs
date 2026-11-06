using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Caisse;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Caisse;

/// <summary>CAI-01 : la règle des couleurs, sans base de données. Le Mois 3 est dû le samedi 02/10.</summary>
public sealed class CouleurDesPaiementsTests
{
    private static readonly List<DateOnly> Seances = [new(2027, 10, 2), new(2027, 10, 9), new(2027, 10, 16), new(2027, 10, 23)];

    [Theory]
    [InlineData(1, "AJour")]
    [InlineData(2, "APayer")]
    [InlineData(8, "APayer")]
    [InlineData(9, "Orange")]
    [InlineData(15, "Orange")]
    [InlineData(16, "Rouge")]
    [InlineData(30, "Rouge")]
    public void Orange_a_la_seance_suivante_rouge_a_celle_d_apres(int jourDOctobre, string couleur)
    {
        Assert.Equal(couleur, CouleurDesPaiements.DUneSomme(Seances, 0, new DateOnly(2027, 10, 2), new DateOnly(2027, 10, jourDOctobre)).ToString());
    }

    [Fact]
    public void Sans_seance_suivante_l_eleve_reste_a_payer()
    {
        Assert.Equal(Couleur.APayer, CouleurDesPaiements.DUneSomme(Seances, 3, new DateOnly(2027, 10, 23), new DateOnly(2027, 12, 1)));
    }
}

/// <summary>
/// CAI-01, CAI-02 et CAI-08 : la couleur des élèves, le badge des groupes et le rappel de paiement. Chaque test part d'un
/// nouveau centre « Jasmins » (assurance de 20 TND, année scolaire du 15/09/2027 au 30/06/2028) et du groupe de
/// Mathématiques de Karim Ben Salah, le samedi de 10h à 12h depuis le 18/09/2027, à 40 TND le Mois (le Mois 3 va du 13/11
/// au 04/12). On est le samedi 27/11/2027 à 10h à Tunis. Adem Miled est arrivé le 13/11 et n'a rien payé : son Mois 3 et
/// son assurance, dus le 13/11, le font rouge depuis le 27/11. Lina Gharbi arrive aujourd'hui : à payer. Sara Ayari,
/// arrivée le 20/11, a tout payé.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class CouleursEtRappelsTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "couleurs_et_rappels";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _proprietaire = null!;
    private HttpClient _amira = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _maths;
    private (Guid Eleve, Guid Inscription) _adem;
    private (Guid Eleve, Guid Inscription) _lina;
    private (Guid Eleve, Guid Inscription) _sara;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        _proprietaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire));
        Assert.Equal(HttpStatusCode.OK, (await _proprietaire.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", null, null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), 20m, null, null, null))).StatusCode);
        _amira = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi"));
        _niveaux = (await _amira.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        var karim = (await (await _amira.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        _maths = (await (await _amira.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(karim, "Mathématiques", Niveau9,
            [new CreneauSaisi("Samedi", "10:00", "12:00")], 40m, new DateOnly(2027, 9, 18), null, ""))).Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        _adem = await InscrireAsync("Adem", "Miled", "98 401 220", "22 345 678", new DateOnly(2027, 11, 13));
        _lina = await InscrireAsync("Lina", "Gharbi", "97 111 222", "50 111 222", new DateOnly(2027, 11, 27));
        _sara = await InscrireAsync("Sara", "Ayari", "97 222 333", "50 222 333", new DateOnly(2027, 11, 20));
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync($"/api/eleves/{_sara.Eleve}/encaissement",
            new EncaissementSaisi([new SommeChoisie(_sara.Inscription, "Mois 3"), new SommeChoisie(null, "Assurance")]))).StatusCode);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau9 => _niveaux.Single(n => n.Nom == "9ᵉ année de base").Id;

    // ----- La couleur (CAI-01) -----

    [Fact]
    public async Task La_couleur_de_chaque_eleve()
    {
        var eleves = (await _amira.GetFromJsonAsync<List<EleveTrouve>>("/api/eleves"))!;

        Assert.Equal([("Sara", "AJour"), ("Lina", "APayer"), ("Adem", "Rouge")], eleves.Select(e => (e.Prenom, e.Couleur)));
        Assert.Equal("Rouge", (await APayerAsync(_adem.Eleve)).Couleur);
        var inscriptions = (await _amira.GetFromJsonAsync<List<InscriptionDeLEleve>>($"/api/inscriptions?groupe={_maths}"))!;
        Assert.Equal([("Sara", "AJour"), ("Lina", "APayer"), ("Adem", "Rouge")], inscriptions.Select(i => (i.Eleve.Prenom, i.Couleur)));
    }

    [Fact]
    public async Task Payer_remet_l_eleve_a_jour()
    {
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync($"/api/eleves/{_adem.Eleve}/encaissement",
            new EncaissementSaisi([new SommeChoisie(_adem.Inscription, "Mois 3"), new SommeChoisie(null, "Assurance")]))).StatusCode);

        Assert.Equal("AJour", (await APayerAsync(_adem.Eleve)).Couleur);
    }

    [Fact]
    public async Task Un_prix_special_a_zero_n_a_jamais_de_couleur()
    {
        Assert.Equal(HttpStatusCode.OK, (await _proprietaire.PutAsJsonAsync($"/api/inscriptions/{_lina.Inscription}/prix-special",
            new PrixSpecialSaisi(0m, "Famille en difficulté"))).StatusCode);

        var lina = (await _amira.GetFromJsonAsync<List<InscriptionDeLEleve>>($"/api/inscriptions?eleve={_lina.Eleve}"))!.Single();
        Assert.Null(lina.Couleur);
    }

    // ----- Le badge du groupe (CAI-02) -----

    [Fact]
    public async Task Le_badge_compte_les_rappels_a_envoyer()
    {
        Assert.Equal(new BadgeDuGroupe("Rappels", 1), (await FicheAsync()).Badge);
        Assert.Equal(new BadgeDuGroupe("Rappels", 1), (await _amira.GetFromJsonAsync<List<ResumeDuGroupe>>("/api/groupes"))!.Single().Badge);
    }

    [Fact]
    public async Task Le_badge_compte_les_eleves_a_payer_puis_dit_groupe_paye()
    {
        await EncaisserAsync(_adem, "Mois 3", assurance: true);
        Assert.Equal(new BadgeDuGroupe("APayer", 1), (await FicheAsync()).Badge);

        await EncaisserAsync(_lina, "Mois 3", assurance: true);
        Assert.Equal(new BadgeDuGroupe("Paye", 0), (await FicheAsync()).Badge);
    }

    // ----- Le rappel de paiement (CAI-08) -----

    [Fact]
    public async Task Le_rappel_de_paiement_d_adem()
    {
        var rappel = (await _amira.GetFromJsonAsync<RappelDePaiement>($"/api/eleves/{_adem.Eleve}/rappel"))!;

        Assert.Equal("Bonjour, il reste à payer pour Adem Miled : Mois 3 de mathématiques, 40,000 TND ; assurance 2027-2028, 20,000 TND. "
            + "Total : 60,000 TND. Centre Les Jasmins.", rappel.Texte);
        Assert.Equal(["Parent", "Adem"], rappel.Messages.Select(m => m.Destinataire));
        Assert.Null(rappel.DernierRappelLe);

        Assert.Equal(HttpStatusCode.NoContent, (await _amira.PostAsync($"/api/eleves/{_adem.Eleve}/rappel/envoye", null)).StatusCode);

        Assert.Equal(Aujourdhui, (await APayerAsync(_adem.Eleve)).DernierRappelLe);
        Assert.Equal(Aujourdhui, (await _amira.GetFromJsonAsync<RappelDePaiement>($"/api/eleves/{_adem.Eleve}/rappel"))!.DernierRappelLe);
    }

    [Fact]
    public async Task Un_eleve_a_jour_n_a_pas_de_rappel()
    {
        var reponse = await _amira.GetAsync($"/api/eleves/{_sara.Eleve}/rappel");

        Assert.Equal(HttpStatusCode.Conflict, reponse.StatusCode);
        Assert.Equal(new Avis("Cet élève n'a rien à payer aujourd'hui"), await reponse.Content.ReadFromJsonAsync<Avis>());
    }

    [Fact]
    public async Task Les_eleves_en_rouge()
    {
        var rouges = (await _amira.GetFromJsonAsync<List<EleveEnRouge>>("/api/eleves/en-rouge"))!;

        var adem = Assert.Single(rouges);
        Assert.Equal((_adem.Eleve, 60m), (adem.Id, adem.Total));
        Assert.Equal(["Mois 3 de mathématiques", "assurance 2027-2028"], adem.Dues);
    }

    [Fact]
    public async Task Le_rappel_d_un_autre_centre_est_introuvable()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));

        Assert.Equal(HttpStatusCode.NotFound, (await rim.GetAsync($"/api/eleves/{_adem.Eleve}/rappel")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rim.PostAsync($"/api/eleves/{_adem.Eleve}/rappel/envoye", null)).StatusCode);
        Assert.Empty((await rim.GetFromJsonAsync<List<EleveEnRouge>>("/api/eleves/en-rouge"))!);
    }

    // ----- Outils -----

    private async Task<(Guid Eleve, Guid Inscription)> InscrireAsync(string prenom, string nom, string parent, string telephone, DateOnly debutLe)
    {
        var reponse = await _amira.PostAsJsonAsync("/api/eleves", new FicheSaisieDeLEleve(prenom, nom, Niveau9, false, parent, null, telephone,
            $"{prenom.ToLowerInvariant()}@exemple.tn", null));
        var eleve = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        var inscription = await _amira.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(eleve, _maths, null, debutLe));
        Assert.Equal(HttpStatusCode.Created, inscription.StatusCode);
        return (eleve, (await inscription.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id);
    }

    private async Task EncaisserAsync((Guid Eleve, Guid Inscription) eleve, string cle, bool assurance)
    {
        List<SommeChoisie> sommes = [new(eleve.Inscription, cle)];
        if (assurance)
            sommes.Add(new SommeChoisie(null, "Assurance"));
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync($"/api/eleves/{eleve.Eleve}/encaissement", new EncaissementSaisi(sommes))).StatusCode);
    }

    private async Task<APayer> APayerAsync(Guid eleve) => (await _amira.GetFromJsonAsync<APayer>($"/api/eleves/{eleve}/a-payer"))!;

    private async Task<FicheDuGroupe> FicheAsync() => (await _amira.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{_maths}"))!;
}
