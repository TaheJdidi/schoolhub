using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Journal;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Groupes;

/// <summary>
/// GRP-08 : fermer un groupe à une date, avec un motif. Chaque test part d'un nouveau centre « Jasmins », avec son année
/// scolaire du 15/09/2027 au 30/06/2028, et Karim Ben Salah, professeur ; Amira, la secrétaire, est connectée.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class FermetureTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "fermeture_des_groupes";
    private const string Adresse = "/api/groupes";

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _client = null!;
    private Compte _amira = null!;
    private Guid _karim;
    private Guid _neuviemeAnnee;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        await _application.InitialiserLaBaseAsync();
        (_jasmins, _client, _amira) = await CentreAsync("Centre Les Jasmins", avecAnneeScolaire: true);
        _karim = await ProfesseurAsync(_client);
        _neuviemeAnnee = (await _client.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == "9ᵉ année de base").Id;
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    /// <summary>Karim, le samedi de 10h à 12h, à partir du 18/09/2027, 60 TND le Mois.</summary>
    private CreationDUnGroupe Etude => new(
        _karim, "Mathématiques", _neuviemeAnnee, [new CreneauSaisi("Samedi", "10:00", "12:00")], 60m, new DateOnly(2027, 9, 18), 16, "");

    private DateOnly Aujourdhui => DateDeTunis.Aujourdhui(_application.Horloge);

    [Fact]
    public async Task Fermer_supprime_les_seances_apres_la_date()
    {
        var groupe = await CreerAsync(Etude);
        await AllerAuAsync(new DateTimeOffset(2028, 1, 20, 9, 0, 0, TimeSpan.Zero));

        var fiche = await FermerAsync(groupe.Id, new FermetureDUnGroupe(new DateOnly(2028, 1, 15), " Trop peu d'élèves "));

        Assert.Equal((new DateOnly(2028, 1, 15), "Trop peu d'élèves"), (fiche.FermeLe, fiche.MotifDeFermeture));
        var dates = fiche.Mois.SelectMany(m => m.Seances).Select(s => s.Date).ToList();
        Assert.Equal(18, dates.Count);
        Assert.Equal(new DateOnly(2028, 1, 15), dates[^1]);
        Assert.True((await _client.GetFromJsonAsync<List<ResumeDuGroupe>>(Adresse))!.Single().Ferme);
    }

    [Fact]
    public async Task La_fermeture_va_au_journal()
    {
        var groupe = await CreerAsync(Etude);
        await AllerAuAsync(new DateTimeOffset(2028, 1, 20, 9, 0, 0, TimeSpan.Zero));

        await FermerAsync(groupe.Id, new FermetureDUnGroupe(new DateOnly(2028, 1, 15), "Trop peu d'élèves"));

        var ligne = Assert.Single(await _application.JournalAsync(_jasmins));
        Assert.Equal(
            (ActionDuJournal.GroupeFerme, _amira.Id, "Amira Jlassi", "Groupe Samedi 10h → 12h · Karim Ben Salah · Trop peu d'élèves", "Ouvert", "Fermé le 15/01/2028"),
            (ligne.Action, ligne.AuteurId, ligne.Auteur, ligne.Detail, ligne.Avant, ligne.Apres));
    }

    [Fact]
    public async Task Un_groupe_pas_encore_commence_se_ferme()
    {
        var groupe = await CreerAsync(Etude);

        var fiche = await FermerAsync(groupe.Id, new FermetureDUnGroupe(Aujourdhui, "Le professeur ne vient plus"));

        Assert.Empty(fiche.Mois);
        Assert.Equal(Aujourdhui, fiche.FermeLe);
        Assert.Empty((await LireAsync(groupe.Id)).Mois);
    }

    [Fact]
    public async Task Un_groupe_ne_se_ferme_pas_deux_fois()
    {
        var groupe = await CreerAsync(Etude);
        await FermerAsync(groupe.Id, new FermetureDUnGroupe(Aujourdhui, "Trop peu d'élèves"));

        var reponse = await _client.PostAsJsonAsync($"{Adresse}/{groupe.Id}/fermeture", new FermetureDUnGroupe(Aujourdhui, "Encore"));

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, "Ce groupe est déjà fermé");
        Assert.Single(await _application.JournalAsync(_jasmins));
    }

    [Theory]
    [InlineData("sans date", "La date de fermeture est obligatoire")]
    [InlineData("demain", "La date de fermeture ne peut pas être après aujourd'hui")]
    [InlineData("motif vide", "Écrivez le motif de la fermeture")]
    [InlineData("motif long", "Le motif fait au plus 200 caractères")]
    public async Task Une_fermeture_incomplete_est_refusee(string cas, string message)
    {
        var groupe = await CreerAsync(Etude);
        var fermeture = cas switch
        {
            "sans date" => new FermetureDUnGroupe(null, "Trop peu d'élèves"),
            "demain" => new FermetureDUnGroupe(Aujourdhui.AddDays(1), "Trop peu d'élèves"),
            "motif vide" => new FermetureDUnGroupe(Aujourdhui, "  "),
            _ => new FermetureDUnGroupe(Aujourdhui, new string('a', 201)),
        };

        var reponse = await _client.PostAsJsonAsync($"{Adresse}/{groupe.Id}/fermeture", fermeture);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, message);
        Assert.Null((await LireAsync(groupe.Id)).FermeLe);
        Assert.Empty(await _application.JournalAsync(_jasmins));
    }

    [Fact]
    public async Task Un_groupe_inconnu_ou_d_un_autre_centre_ne_se_ferme_pas()
    {
        var (_, clientDesOliviers, _) = await CentreAsync("Centre Les Oliviers", avecAnneeScolaire: false);
        var leila = await ProfesseurAsync(clientDesOliviers);
        var formation = new CreationDUnGroupe(leila, "Informatique", null, [new CreneauSaisi("Samedi", "10:00", "12:00")], null,
            new DateOnly(2028, 5, 20), null, "", Type: "Formation", PrixTotal: 300m, NombreDeSeances: 3);
        var groupeDesOliviers = await CreerAsync(formation, clientDesOliviers);

        foreach (var id in new[] { groupeDesOliviers.Id, Guid.NewGuid() })
        {
            var reponse = await _client.PostAsJsonAsync($"{Adresse}/{id}/fermeture", new FermetureDUnGroupe(Aujourdhui, "Erreur"));
            await VerifierLeRefusAsync(reponse, HttpStatusCode.NotFound, "Groupe introuvable");
        }
    }

    [Fact]
    public async Task Un_groupe_ferme_ne_se_modifie_plus()
    {
        var groupe = await CreerAsync(Etude);
        await FermerAsync(groupe.Id, new FermetureDUnGroupe(Aujourdhui, "Trop peu d'élèves"));

        var reponse = await _client.PutAsJsonAsync($"{Adresse}/{groupe.Id}", new ModificationDUnGroupe("Autre nom", 16, 60m));

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, "Ce groupe est fermé : il ne se modifie plus");
    }

    [Fact]
    public async Task Un_professeur_dont_les_groupes_sont_fermes_peut_partir()
    {
        var groupe = await CreerAsync(Etude);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/professeurs/{_karim}/depart", null)).StatusCode);

        await FermerAsync(groupe.Id, new FermetureDUnGroupe(Aujourdhui, "Le professeur part"));

        var professeurs = (await _client.GetFromJsonAsync<List<ProfesseurDuCentre>>("/api/professeurs"))!;
        Assert.Equal(0, professeurs.Single().GroupesOuverts);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/professeurs/{_karim}/depart", null)).StatusCode);
    }

    [Fact]
    public async Task Deux_fermetures_en_meme_temps_n_en_font_qu_une()
    {
        // Deux onglets (ou deux secrétaires) ferment le même groupe au même moment.
        var groupe = await CreerAsync(Etude);

        var reponses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            _client.PostAsJsonAsync($"{Adresse}/{groupe.Id}/fermeture", new FermetureDUnGroupe(Aujourdhui, $"Motif {i}"))));

        Assert.Single(reponses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(reponses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Single(await _application.JournalAsync(_jasmins));
    }

    // ----- Outils -----

    /// <summary>Un centre avec ses niveaux d'office, et le client de sa secrétaire ; son propriétaire règle l'année scolaire.</summary>
    private async Task<(Centre Centre, HttpClient Client, Compte Secretaire)> CentreAsync(string nom, bool avecAnneeScolaire)
    {
        var centre = await _application.CreerCentreAsync(nom);
        await _application.RecopierLesNiveauxDOfficeAsync(centre);
        if (avecAnneeScolaire)
        {
            var proprietaire = await _application.CreerCompteAsync(centre, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
            var annee = new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30));
            var reponse = await (await _application.ClientConnecteAsync(proprietaire)).PutAsJsonAsync(
                "/api/centre/parametres", new ReglagesDuCentre(nom, null, null, annee, null, null, null, null));
            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        }

        var secretaire = await _application.CreerCompteAsync(centre, "Amira", "Jlassi");
        return (centre, await _application.ClientConnecteAsync(secretaire), secretaire);
    }

    private static async Task<Guid> ProfesseurAsync(HttpClient client)
    {
        var reponse = await client.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null));
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
    }

    private async Task<FicheDuGroupe> CreerAsync(CreationDUnGroupe creation, HttpClient? client = null)
    {
        var reponse = await (client ?? _client).PostAsJsonAsync(Adresse, creation);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!;
    }

    private async Task<FicheDuGroupe> FermerAsync(Guid id, FermetureDUnGroupe fermeture)
    {
        var reponse = await _client.PostAsJsonAsync($"{Adresse}/{id}/fermeture", fermeture);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!;
    }

    private async Task<FicheDuGroupe> LireAsync(Guid id) => (await _client.GetFromJsonAsync<FicheDuGroupe>($"{Adresse}/{id}"))!;

    /// <summary>Avance l'horloge des tests, puis reconnecte Amira : sa session a pu expirer entre-temps.</summary>
    private async Task AllerAuAsync(DateTimeOffset moment)
    {
        _application.Horloge.SetUtcNow(moment);
        _client = await _application.ClientConnecteAsync(_amira);
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
