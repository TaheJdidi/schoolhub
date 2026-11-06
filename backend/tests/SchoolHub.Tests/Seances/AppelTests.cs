using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Modules.Seances;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Journal;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Seances;

/// <summary>
/// SEA-07, SEA-09 à SEA-11 : l'appel. Chaque test part d'un nouveau centre « Jasmins » (année scolaire du 15/09/2027 au
/// 30/06/2028) et du groupe de Mathématiques de Karim Ben Salah, le samedi de 10h à 12h depuis le 18/09/2027. On est le
/// samedi 27/11/2027 à 10h à Tunis. Adem Miled et Lina Gharbi y sont inscrits depuis le 20/11, Sami Chaabane à partir
/// du 04/12.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class AppelTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "appel";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly CreneauSaisi Samedi10h = new("Samedi", "10:00", "12:00");

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _secretaire = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _karim;
    private Guid _maths;
    private Inscrit _adem = null!;
    private Inscrit _lina = null!;

    private sealed record Inscrit(Guid Eleve, Guid Inscription);

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        var proprietaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire));
        Assert.Equal(HttpStatusCode.OK, (await proprietaire.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", null, null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), null, null, null, null))).StatusCode);
        _secretaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi"));
        _niveaux = (await _secretaire.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        _karim = (await (await _secretaire.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        _maths = await GroupeAsync(new CreationDUnGroupe(_karim, "Mathématiques", Niveau9, [Samedi10h], 40m, new DateOnly(2027, 9, 18), null, ""));
        _adem = await InscrireAsync("Adem", "Miled", "98 401 220", "22 345 678", _maths, new DateOnly(2027, 11, 20));
        _lina = await InscrireAsync("Lina", "Gharbi", "97 111 222", "50 111 222", _maths, new DateOnly(2027, 11, 20));
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau9 => _niveaux.Single(n => n.Nom == "9ᵉ année de base").Id;

    // ----- Faire l'appel (SEA-07) -----

    [Fact]
    public async Task L_appel_s_ouvre_avec_tous_les_eleves_presents()
    {
        await InscrireAsync("Sami", "Chaabane", "97 000 333", "50 000 333", _maths, new DateOnly(2027, 12, 4));

        var appel = await AppelAsync(await SeanceAsync(_maths, new DateOnly(2027, 11, 27)));

        Assert.Equal((new DateOnly(2027, 11, 27), "10:00", "12:00", "Mathématiques", "Karim Ben Salah"),
            (appel.Date, appel.Debut, appel.Fin, appel.Groupe.Matiere, appel.Professeur));
        Assert.True(appel.Ouvert);
        Assert.False(appel.AppelFait);
        // Sami n'arrive que le 04/12 : il n'est pas attendu.
        Assert.Equal(["Lina", "Adem"], appel.Eleves.Select(e => e.Prenom));
        Assert.All(appel.Eleves, e => Assert.False(e.Absent));
    }

    [Fact]
    public async Task Enregistrer_l_appel()
    {
        var seance = await SeanceAsync(_maths, new DateOnly(2027, 11, 27));

        var appel = await EnregistrerAsync(seance, [_adem.Inscription]);

        Assert.True(appel.AppelFait);
        var adem = appel.Eleves.Single(e => e.Prenom == "Adem");
        Assert.True(adem.Absent);
        Assert.False(appel.Eleves.Single(e => e.Prenom == "Lina").Absent);
        Assert.Equal(["Parent", "Adem"], adem.Messages.Select(m => m.Destinataire));
        Assert.Empty(appel.Eleves.Single(e => e.Prenom == "Lina").Messages);
        var texte = Uri.UnescapeDataString(adem.Messages[0].Lien[(adem.Messages[0].Lien.IndexOf("text=", StringComparison.Ordinal) + 5)..]);
        Assert.Equal("Bonjour, Adem Miled était absent à la séance de mathématiques du samedi 27/11/2027, de 10h à 12h. Centre Les Jasmins.", texte);
        Assert.StartsWith("https://wa.me/21698401220?text=", adem.Messages[0].Lien, StringComparison.Ordinal);

        // SEA-02 : la fiche du groupe montre la séance « appel fait ».
        var fiche = (await _secretaire.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{_maths}"))!;
        Assert.True(fiche.Mois.SelectMany(m => m.Seances).Single(s => s.Id == seance).AppelFait);
        // Un premier appel n'est pas une correction : rien au journal.
        Assert.DoesNotContain(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.PresenceCorrigee);
    }

    [Fact]
    public async Task L_appel_ne_s_ouvre_pas_avant_le_jour_de_la_seance()
    {
        var seance = await SeanceAsync(_maths, new DateOnly(2027, 12, 4));

        Assert.False((await AppelAsync(seance)).Ouvert);
        await VerifierLeRefusAsync(await _secretaire.PutAsJsonAsync($"/api/seances/{seance}/appel", new AppelSaisi([])),
            HttpStatusCode.Conflict, "L'appel s'ouvre le jour de la séance");
    }

    [Fact]
    public async Task Un_eleve_pas_attendu_est_refuse()
    {
        var seance = await SeanceAsync(_maths, new DateOnly(2027, 11, 27));

        await VerifierLeRefusAsync(await _secretaire.PutAsJsonAsync($"/api/seances/{seance}/appel", new AppelSaisi([Guid.NewGuid()])),
            HttpStatusCode.BadRequest, "Cet élève n'est pas attendu à cette séance");
        Assert.False((await AppelAsync(seance)).AppelFait);
    }

    // ----- Corriger (SEA-10) -----

    [Fact]
    public async Task Corriger_l_appel_va_au_journal()
    {
        var seance = await SeanceAsync(_maths, new DateOnly(2027, 11, 27));
        await EnregistrerAsync(seance, [_adem.Inscription]);

        var appel = await EnregistrerAsync(seance, [_lina.Inscription]);

        Assert.Equal([("Lina", true), ("Adem", false)], appel.Eleves.Select(e => (e.Prenom, e.Absent)));
        var corrections = (await _application.JournalAsync(_jasmins)).Where(l => l.Action == ActionDuJournal.PresenceCorrigee).ToList();
        Assert.Equal(
            [
                ("Lina Gharbi · Mathématiques, Groupe Samedi 10h → 12h · séance du 27/11/2027", "Présent", "Absent"),
                ("Adem Miled · Mathématiques, Groupe Samedi 10h → 12h · séance du 27/11/2027", "Absent", "Présent"),
            ],
            corrections.Select(l => (l.Detail, l.Avant, l.Apres)));
        Assert.All(corrections, l => Assert.Equal("Amira Jlassi", l.Auteur));
    }

    [Fact]
    public async Task Une_correction_sans_changement_n_ecrit_rien()
    {
        var seance = await SeanceAsync(_maths, new DateOnly(2027, 11, 27));
        await EnregistrerAsync(seance, [_adem.Inscription]);

        await EnregistrerAsync(seance, [_adem.Inscription]);

        Assert.DoesNotContain(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.PresenceCorrigee);
    }

    // ----- Ce qui est dû -----

    [Fact]
    public async Task Une_revision_a_la_seance_ne_doit_pas_la_seance_manquee()
    {
        // Une Révision de 3 séances à 60 TND : 20 TND la séance, les 20/11, 27/11 et 04/12.
        var revision = await GroupeAsync(new CreationDUnGroupe(_karim, "Physique", Niveau9, [Samedi10h], null, new DateOnly(2027, 11, 20), null, "",
            Type: "Revision", PrixTotal: 60m, NombreDeSeances: 3));
        var sara = await InscrireAsync("Sara", "Ayari", "97 222 333", "50 222 333", revision, new DateOnly(2027, 11, 20), "ALaSeance");
        Assert.Equal(40m, await TotalAPayerAsync(sara.Eleve));

        await EnregistrerAsync(await SeanceAsync(revision, new DateOnly(2027, 11, 20)), [sara.Inscription]);

        Assert.Equal(20m, await TotalAPayerAsync(sara.Eleve));

        // La correction la remet présente : la séance est de nouveau due (SEA-10).
        await EnregistrerAsync(await SeanceAsync(revision, new DateOnly(2027, 11, 20)), []);
        Assert.Equal(40m, await TotalAPayerAsync(sara.Eleve));
    }

    [Fact]
    public async Task Un_absent_en_etude_doit_quand_meme_son_mois()
    {
        var avant = await TotalAPayerAsync(_adem.Eleve);

        await EnregistrerAsync(await SeanceAsync(_maths, new DateOnly(2027, 11, 20)), [_adem.Inscription]);

        Assert.Equal(avant, await TotalAPayerAsync(_adem.Eleve));
    }

    // ----- Prévenir les absents (SEA-11) -----

    [Fact]
    public async Task Un_message_ouvert_est_marque()
    {
        var seance = await SeanceAsync(_maths, new DateOnly(2027, 11, 27));
        await EnregistrerAsync(seance, [_adem.Inscription]);

        Assert.Equal(HttpStatusCode.NoContent, (await _secretaire.PostAsync($"/api/seances/{seance}/absences/{_adem.Inscription}/prevenu", null)).StatusCode);

        Assert.Equal(Aujourdhui, (await AppelAsync(seance)).Eleves.Single(e => e.Prenom == "Adem").PrevenuLe);
        await VerifierLeRefusAsync(await _secretaire.PostAsync($"/api/seances/{seance}/absences/{_lina.Inscription}/prevenu", null),
            HttpStatusCode.NotFound, "Absence introuvable");
    }

    // ----- Les appels à recopier (SEA-09) -----

    [Fact]
    public async Task Les_appels_a_recopier()
    {
        var du20 = await SeanceAsync(_maths, new DateOnly(2027, 11, 20));
        var du13 = await SeanceAsync(_maths, new DateOnly(2027, 11, 13));
        await EnregistrerAsync(du13, []);

        var aRecopier = (await _secretaire.GetFromJsonAsync<List<AppelARecopier>>("/api/seances/a-recopier"))!;

        // Les séances passées sans appel, de la plus ancienne (18/09) à la plus récente (20/11) ; ni celle du 13/11, faite,
        // ni celle d'aujourd'hui.
        Assert.Equal(new DateOnly(2027, 9, 18), aRecopier[0].Date);
        Assert.Equal(du20, aRecopier[^1].SeanceId);
        Assert.DoesNotContain(aRecopier, a => a.SeanceId == du13);
        Assert.DoesNotContain(aRecopier, a => a.Date == new DateOnly(2027, 11, 27));
        Assert.Equal(("Mathématiques", "Karim Ben Salah"), (aRecopier[0].Groupe.Matiere, aRecopier[0].Professeur));
    }

    // ----- La séparation des centres -----

    [Fact]
    public async Task L_appel_d_un_autre_centre_est_introuvable()
    {
        var seance = await SeanceAsync(_maths, new DateOnly(2027, 11, 27));
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));

        await VerifierLeRefusAsync(await rim.GetAsync($"/api/seances/{seance}/appel"), HttpStatusCode.NotFound, "Séance introuvable");
        await VerifierLeRefusAsync(await rim.PutAsJsonAsync($"/api/seances/{seance}/appel", new AppelSaisi([])), HttpStatusCode.NotFound, "Séance introuvable");
        Assert.Empty((await rim.GetFromJsonAsync<List<AppelARecopier>>("/api/seances/a-recopier"))!);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _application.CreateClient().GetAsync("/api/seances/a-recopier")).StatusCode);
    }

    // ----- Outils -----

    private async Task<Guid> GroupeAsync(CreationDUnGroupe creation)
    {
        var reponse = await _secretaire.PostAsJsonAsync("/api/groupes", creation);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
    }

    private async Task<Inscrit> InscrireAsync(string prenom, string nom, string parent, string telephone, Guid groupe, DateOnly debutLe, string? mode = null)
    {
        var reponse = await _secretaire.PostAsJsonAsync("/api/eleves", new FicheSaisieDeLEleve(prenom, nom, Niveau9, false, parent, null, telephone,
            $"{prenom.ToLowerInvariant()}@exemple.tn", null));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var eleve = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        var inscription = await _secretaire.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(eleve, groupe, mode, debutLe));
        Assert.Equal(HttpStatusCode.Created, inscription.StatusCode);
        return new Inscrit(eleve, (await inscription.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id);
    }

    /// <summary>La séance du groupe à cette date, dans la fiche du groupe.</summary>
    private async Task<Guid> SeanceAsync(Guid groupe, DateOnly date)
    {
        var fiche = (await _secretaire.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{groupe}"))!;
        return fiche.Mois.SelectMany(m => m.Seances).Select(s => (s.Id, s.Date))
            .Concat(fiche.Seances.Select(s => (s.Id, s.Date)))
            .Single(s => s.Date == date).Id;
    }

    private async Task<AppelDeLaSeance> AppelAsync(Guid seance) => (await _secretaire.GetFromJsonAsync<AppelDeLaSeance>($"/api/seances/{seance}/appel"))!;

    private async Task<AppelDeLaSeance> EnregistrerAsync(Guid seance, List<Guid> absents)
    {
        var reponse = await _secretaire.PutAsJsonAsync($"/api/seances/{seance}/appel", new AppelSaisi(absents));
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<AppelDeLaSeance>())!;
    }

    private async Task<decimal> TotalAPayerAsync(Guid eleve) => (await _secretaire.GetFromJsonAsync<APayer>($"/api/eleves/{eleve}/a-payer"))!.Total;

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
