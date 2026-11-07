using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Modules.Achats;
using SchoolHub.Modules.Caisse;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Caisse;

/// <summary>
/// CAI-09 et CAI-10 : la clôture du soir et son contrôle. Chaque test part d'un nouveau centre « Jasmins » (assurance de
/// 20 TND, année scolaire du 15/09/2027 au 30/06/2028), avec le propriétaire Hedi Ben Salah et deux secrétaires, Amira
/// Jlassi et Sonia Mabrouk, et du groupe de Mathématiques de Karim Ben Salah, le samedi de 10h à 12h, à 40 TND le Mois.
/// Adem Miled y est inscrit depuis le 13/11 : il doit le Mois 3 (40 TND) et l'assurance (20 TND). On est le samedi
/// 27/11/2027 à 10h à Tunis.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class CloturesTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "clotures";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Le27 = new(2027, 11, 27);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _hedi = null!;
    private HttpClient _amira = null!;
    private HttpClient _sonia = null!;
    private Compte _compteDAmira = null!;
    private Compte _compteDeHedi = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _maths;
    private Guid _adem;
    private Guid _inscriptionDAdem;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        _compteDeHedi = await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
        _hedi = await _application.ClientConnecteAsync(_compteDeHedi);
        Assert.Equal(HttpStatusCode.OK, (await _hedi.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", null, null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), 20m, null, null, null))).StatusCode);
        _compteDAmira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        _amira = await _application.ClientConnecteAsync(_compteDAmira);
        _sonia = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Sonia", "Mabrouk"));
        _niveaux = (await _amira.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        var niveau = _niveaux.Single(n => n.Nom == "9ᵉ année de base").Id;
        var karim = (await (await _amira.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        var creation = await _amira.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(karim, "Mathématiques", niveau,
            [new CreneauSaisi("Samedi", "10:00", "12:00")], 40m, new DateOnly(2027, 9, 18), null, ""));
        _maths = (await creation.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        var eleve = await _amira.PostAsJsonAsync("/api/eleves", new FicheSaisieDeLEleve("Adem", "Miled", niveau, false, "98 401 220", null, "22 345 678",
            "adem@exemple.tn", null));
        _adem = (await eleve.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        var inscription = await _amira.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(_adem, _maths, null, new DateOnly(2027, 11, 13)));
        _inscriptionDAdem = (await inscription.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id;
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private SommeChoisie Mois(int numero) => new(_inscriptionDAdem, $"Mois {numero}");

    private static readonly SommeChoisie Assurance = new(null, "Assurance");

    // ----- Ma caisse et sa clôture (CAI-09) -----

    [Fact]
    public async Task Ma_caisse_compte_les_recus_du_jour_sans_les_annules()
    {
        await EncaisserAsync(_amira, Mois(3));
        var assurance = await EncaisserAsync(_amira, Assurance);
        Assert.Equal(HttpStatusCode.OK, (await _amira.PostAsJsonAsync($"/api/recus/{assurance.Id}/annulation", new AnnulationDuRecu("Erreur"))).StatusCode);

        var caisse = await MaCaisseAsync(_amira);

        var ouverte = Assert.Single(caisse.Ouvertes);
        Assert.Equal((Le27, 1, 40m, 0m, 40m, 1, false, 1),
            (ouverte.Jour, ouverte.Rang, ouverte.Encaisse, ouverte.Sorties, ouverte.Attendu, ouverte.Recus, ouverte.Oubliee, ouverte.Annules));
        Assert.Empty(caisse.Clotures);
        // Sonia a sa propre caisse, vide.
        Assert.Equal(0m, Assert.Single((await MaCaisseAsync(_sonia)).Ouvertes).Encaisse);
    }

    [Fact]
    public async Task Cloturer_garde_l_ecart_et_va_au_journal()
    {
        await EncaisserAsync(_amira, Mois(3), Assurance);

        var cloture = await CloturerAsync(_amira, Le27, 55m);

        Assert.Equal((1, "Amira Jlassi", 60m, 60m, 55m, -5m, false), (cloture.Rang, cloture.Secretaire, cloture.Attendu, cloture.Encaisse, cloture.Compte, cloture.Ecart, cloture.ArgentRecu));
        var caisse = await MaCaisseAsync(_amira);
        Assert.Equal((2, 0m), (Assert.Single(caisse.Ouvertes).Rang, caisse.Ouvertes[0].Encaisse));
        Assert.Equal([cloture.Id], caisse.Clotures.Select(c => c.Id));
        var ligne = Assert.Single(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.CaisseCloturee);
        Assert.Equal(("Caisse de Amira Jlassi du 27/11/2027", "Attendu 60,000 TND", "Compté 55,000 TND, écart −5,000 TND"), (ligne.Detail, ligne.Avant, ligne.Apres));
    }

    [Fact]
    public async Task Un_encaissement_apres_la_cloture_ouvre_une_deuxieme_caisse()
    {
        await EncaisserAsync(_amira, Mois(3));
        await CloturerAsync(_amira, Le27, 40m);

        await EncaisserAsync(_amira, Assurance);

        var ouverte = Assert.Single((await MaCaisseAsync(_amira)).Ouvertes);
        Assert.Equal((2, 20m), (ouverte.Rang, ouverte.Attendu));
        var deuxieme = await CloturerAsync(_amira, Le27, 20m);
        Assert.Equal((2, 0m), (deuxieme.Rang, deuxieme.Ecart));
    }

    [Fact]
    public async Task Une_caisse_oubliee_se_cloture_le_lendemain()
    {
        await EncaisserAsync(_amira, Mois(3));
        _application.Horloge.SetUtcNow(Aujourdhui.AddDays(1));
        // Le lendemain, la session a expiré : on se reconnecte.
        _amira = await _application.ClientConnecteAsync(_compteDAmira);
        _hedi = await _application.ClientConnecteAsync(_compteDeHedi);

        var caisse = await MaCaisseAsync(_amira);
        Assert.Equal([(Le27, true, 40m), (Le27.AddDays(1), false, 0m)], caisse.Ouvertes.Select(o => (o.Jour, o.Oubliee, o.Attendu)));
        var controle = await ControleAsync("");
        Assert.Equal(("Amira Jlassi", Le27, 40m, 1), (Assert.Single(controle.NonCloturees).Secretaire, controle.NonCloturees[0].Jour, controle.NonCloturees[0].Attendu,
            controle.NonCloturees[0].Recus));

        var cloture = await CloturerAsync(_amira, Le27, 40m);

        Assert.Equal(Le27, cloture.Jour);
        Assert.Empty((await ControleAsync("")).NonCloturees);
        Assert.Single((await MaCaisseAsync(_amira)).Ouvertes);
    }

    [Fact]
    public async Task La_migration_range_les_anciens_recus_dans_une_cloture_historique()
    {
        // Le 27 : deux reçus d'Amira (l'un annulé), une sortie de caisse ; un reçu de Sonia.
        await EncaisserAsync(_amira, Mois(3));
        var annule = await EncaisserAsync(_amira, Assurance);
        Assert.Equal(HttpStatusCode.OK, (await _amira.PostAsJsonAsync($"/api/recus/{annule.Id}/annulation", new AnnulationDuRecu("Erreur"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync("/api/achats", new AchatSaisi(Le27, "Papier", "Materiel", 15m, null, true))).StatusCode);
        await EncaisserAsync(_sonia, Assurance);
        // Le 28, la migration passe : la caisse du jour reste ouverte.
        _application.Horloge.SetUtcNow(Aujourdhui.AddDays(1));
        _amira = await _application.ClientConnecteAsync(_compteDAmira);
        _hedi = await _application.ClientConnecteAsync(_compteDeHedi);

        await _application.DansUnScopeAsync(services =>
            services.GetRequiredService<SchoolHubDbContext>().Database.ExecuteSqlRawAsync(CloturesHistoriques.Sql("DATE '2027-11-28'")));
        // Une seconde fois ne change rien.
        await _application.DansUnScopeAsync(services =>
            services.GetRequiredService<SchoolHubDbContext>().Database.ExecuteSqlRawAsync(CloturesHistoriques.Sql("DATE '2027-11-28'")));

        var controle = await ControleAsync("");
        Assert.Empty(controle.NonCloturees);
        Assert.Equal(0m, controle.ARecuperer);
        Assert.Equal([("Amira Jlassi", 40m, 15m, 25m, 25m, 0m, true), ("Sonia Mabrouk", 20m, 0m, 20m, 20m, 0m, true)],
            controle.Clotures.OrderBy(c => c.Secretaire).Select(c => (c.Secretaire, c.Encaisse, c.Sorties, c.Attendu, c.Compte, c.Ecart, c.ArgentRecu)));
        Assert.All(controle.Clotures, c => Assert.Equal((Le27, 1), (c.Jour, c.Rang)));
        var caisse = await MaCaisseAsync(_amira);
        Assert.Equal(Le27.AddDays(1), Assert.Single(caisse.Ouvertes).Jour);
    }

    [Theory]
    [InlineData(null, "40", HttpStatusCode.BadRequest, "Choisissez le jour")]
    [InlineData("2027-11-28", "40", HttpStatusCode.BadRequest, "Ce jour est à venir")]
    [InlineData("2027-11-27", null, HttpStatusCode.BadRequest, "Saisissez le montant compté, au millime près")]
    [InlineData("2027-11-27", "-1", HttpStatusCode.BadRequest, "Saisissez le montant compté, au millime près")]
    [InlineData("2027-11-27", "40.0005", HttpStatusCode.BadRequest, "Saisissez le montant compté, au millime près")]
    [InlineData("2027-11-26", "40", HttpStatusCode.Conflict, "Rien à clôturer ce jour-là")]
    public async Task Une_cloture_refusee(string? jour, string? compte, HttpStatusCode statut, string message)
    {
        await EncaisserAsync(_amira, Mois(3));

        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync("/api/caisse/cloture", new ClotureSaisie(
            jour is null ? null : DateOnly.Parse(jour, System.Globalization.CultureInfo.InvariantCulture),
            compte is null ? null : decimal.Parse(compte, System.Globalization.CultureInfo.InvariantCulture))), statut, message);
        // Sonia n'a rien encaissé : sa caisse est vide.
        await VerifierLeRefusAsync(await _sonia.PostAsJsonAsync("/api/caisse/cloture", new ClotureSaisie(Le27, 0m)), HttpStatusCode.Conflict, "Rien à clôturer ce jour-là");
    }

    [Fact]
    public async Task Apres_la_cloture_seul_le_proprietaire_annule_un_recu()
    {
        var recu = await EncaisserAsync(_amira, Mois(3));
        await CloturerAsync(_amira, Le27, 40m);

        var lu = (await _amira.GetFromJsonAsync<RecuDecrit>($"/api/recus/{recu.Id}"))!;
        Assert.Equal((false, "Votre caisse est clôturée : seul le propriétaire peut annuler ce reçu"), (lu.Annulable, lu.PourquoiPasAnnulable));
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/recus/{recu.Id}/annulation", new AnnulationDuRecu("Erreur")),
            HttpStatusCode.Forbidden, "Votre caisse est clôturée : seul le propriétaire peut annuler ce reçu");
        Assert.Equal(HttpStatusCode.OK, (await _hedi.PostAsJsonAsync($"/api/recus/{recu.Id}/annulation", new AnnulationDuRecu("Erreur"))).StatusCode);
    }

    // ----- Le contrôle des clôtures (CAI-10) -----

    [Fact]
    public async Task Le_proprietaire_controle_les_clotures_et_marque_l_argent_recu()
    {
        await EncaisserAsync(_amira, Mois(3));
        var amira = await CloturerAsync(_amira, Le27, 40m);
        await EncaisserAsync(_sonia, Assurance);
        var sonia = await CloturerAsync(_sonia, Le27, 18m);

        var controle = await ControleAsync("");
        Assert.Equal([sonia.Id, amira.Id], controle.Clotures.Select(c => c.Id));
        Assert.Equal(-2m, controle.Clotures[0].Ecart);
        Assert.Equal(58m, controle.ARecuperer);
        Assert.Equal(["Amira Jlassi", "Hedi Ben Salah", "Sonia Mabrouk"], controle.Comptes.Select(c => c.Nom));

        var reponse = await _hedi.PutAsJsonAsync($"/api/caisse/clotures/{amira.Id}/argent-recu", new ArgentRecuSaisi(true));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.True((await reponse.Content.ReadFromJsonAsync<ClotureDecrite>())!.ArgentRecu);
        Assert.Equal(18m, (await ControleAsync("")).ARecuperer);
        // Filtré par secrétaire, puis par une période sans clôture.
        Assert.Equal([amira.Id], (await ControleAsync($"?compteId={_compteDAmira.Id}")).Clotures.Select(c => c.Id));
        Assert.Empty((await ControleAsync("?du=2027-11-01&au=2027-11-20")).Clotures);
        // Décoché, il redevient à récupérer.
        Assert.Equal(HttpStatusCode.OK, (await _hedi.PutAsJsonAsync($"/api/caisse/clotures/{amira.Id}/argent-recu", new ArgentRecuSaisi(false))).StatusCode);
        Assert.Equal(58m, (await ControleAsync("")).ARecuperer);
    }

    [Fact]
    public async Task Le_controle_est_reserve_au_proprietaire_de_son_centre()
    {
        await EncaisserAsync(_amira, Mois(3));
        var cloture = await CloturerAsync(_amira, Le27, 40m);

        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.GetAsync("/api/caisse/clotures")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.PutAsJsonAsync($"/api/caisse/clotures/{cloture.Id}/argent-recu", new ArgentRecuSaisi(true))).StatusCode);

        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi", RoleDuCompte.Proprietaire));
        Assert.Empty((await rim.GetFromJsonAsync<ControleDesClotures>("/api/caisse/clotures"))!.Clotures);
        await VerifierLeRefusAsync(await rim.PutAsJsonAsync($"/api/caisse/clotures/{cloture.Id}/argent-recu", new ArgentRecuSaisi(true)),
            HttpStatusCode.NotFound, "Clôture introuvable");
    }

    // ----- Outils -----

    private async Task<RecuDecrit> EncaisserAsync(HttpClient client, params SommeChoisie[] sommes)
    {
        var reponse = await client.PostAsJsonAsync($"/api/eleves/{_adem}/encaissement", new EncaissementSaisi([.. sommes]));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<RecuDecrit>())!;
    }

    private static async Task<ClotureDecrite> CloturerAsync(HttpClient client, DateOnly jour, decimal compte)
    {
        var reponse = await client.PostAsJsonAsync("/api/caisse/cloture", new ClotureSaisie(jour, compte));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<ClotureDecrite>())!;
    }

    private static async Task<MaCaisse> MaCaisseAsync(HttpClient client) => (await client.GetFromJsonAsync<MaCaisse>("/api/caisse"))!;

    private async Task<ControleDesClotures> ControleAsync(string filtres) => (await _hedi.GetFromJsonAsync<ControleDesClotures>($"/api/caisse/clotures{filtres}"))!;

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
