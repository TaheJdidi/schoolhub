using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Groupes;

/// <summary>
/// GRP-05 à GRP-07 : les Révisions, les Formations et les groupes déjà en cours. Chaque test part d'un nouveau centre
/// « Jasmins », avec son année scolaire du 15/09/2027 au 30/06/2028, ses niveaux d'office et Karim Ben Salah ;
/// Amira, la secrétaire, est connectée.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class RevisionsEtFormationsTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "revisions_formations";
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

    private static readonly CreneauSaisi Samedi10h = new("Samedi", "10:00", "12:00");

    /// <summary>L'exemple de la story : 50 TND en 3 séances, le samedi de 10h à 12h, à partir du 20/05/2028.</summary>
    private CreationDUnGroupe Revision => new(
        _karim, "Mathématiques", _neuviemeAnnee, [Samedi10h], null, new DateOnly(2028, 5, 20), 12, "",
        Type: "Revision", PrixTotal: 50m, NombreDeSeances: 3);

    /// <summary>L'exemple de la story : 300 TND en 10 séances, trois tranches de 100 TND aux séances 1, 5 et 9.</summary>
    private CreationDUnGroupe Formation => new(
        _karim, "Informatique", null, [Samedi10h], null, new DateOnly(2027, 10, 2), 10, "",
        Type: "Formation", PrixTotal: 300m, NombreDeSeances: 10,
        Tranches: [new TrancheSaisie(100m, 1), new TrancheSaisie(100m, 5), new TrancheSaisie(100m, 9)]);

    // ----- Révision (GRP-05) -----

    [Fact]
    public async Task Creer_une_revision_avec_des_creneaux()
    {
        var reponse = await _client.PostAsJsonAsync(Adresse, Revision);

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var fiche = (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!;
        Assert.Equal(("Revision", "Groupe Samedi 10h → 12h", false, 1), (fiche.Type, fiche.Nom, fiche.Termine, fiche.PremiereSeance));
        Assert.Equal((50m, (int?)3, (decimal?)null), (fiche.PrixTotal, fiche.NombreDeSeances, fiche.PrixDuMois));
        Assert.Empty(fiche.Mois);
        Assert.Empty(fiche.Tranches);
        Assert.Equal([1, 2, 3], fiche.Seances.Select(s => s.Numero));
        Assert.Equal([new DateOnly(2028, 5, 20), new DateOnly(2028, 5, 27), new DateOnly(2028, 6, 3)], fiche.Seances.Select(s => s.Date));
        Assert.Equal([16.667m, 16.667m, 16.666m], fiche.Seances.Select(s => s.Prix));
    }

    [Fact]
    public async Task Creer_une_revision_avec_des_dates_une_a_une()
    {
        var fiche = await CreerAsync(Revision with
        {
            Creneaux = null,
            DateDeDebut = null,
            Dates =
            [
                new SeanceSaisie(new DateOnly(2028, 5, 20), "10:00", "12:00"),
                new SeanceSaisie(new DateOnly(2028, 5, 24), "17:00", "19:00"),
                new SeanceSaisie(new DateOnly(2028, 5, 27), "10:00", "12:00"),
            ],
        });

        Assert.Equal(
            [(new DateOnly(2028, 5, 20), "10:00"), (new DateOnly(2028, 5, 24), "17:00"), (new DateOnly(2028, 5, 27), "10:00")],
            fiche.Seances.Select(s => (s.Date, s.Debut)));
        Assert.Equal(new DateOnly(2028, 5, 20), fiche.DateDeDebut);
        // Les créneaux se déduisent des dates : le filtre par jour et le nom proposé s'en servent.
        Assert.Equal([new CreneauDuGroupe("Samedi", "10:00", "12:00"), new CreneauDuGroupe("Mercredi", "17:00", "19:00")], fiche.Creneaux);
    }

    [Fact]
    public async Task Les_dates_une_a_une_sont_rangees()
    {
        var fiche = await CreerAsync(Revision with
        {
            Creneaux = null,
            DateDeDebut = null,
            Dates =
            [
                new SeanceSaisie(new DateOnly(2028, 5, 31), "17:00", "19:00"),
                new SeanceSaisie(new DateOnly(2028, 5, 20), "10:00", "12:00"),
                new SeanceSaisie(new DateOnly(2028, 5, 27), "10:00", "12:00"),
            ],
        });

        Assert.Equal([new DateOnly(2028, 5, 20), new DateOnly(2028, 5, 27), new DateOnly(2028, 5, 31)], fiche.Seances.Select(s => s.Date));
        Assert.Equal("Groupe Mercredi 17h → 19h", fiche.Nom);
    }

    // ----- Formation (GRP-06) -----

    [Fact]
    public async Task Creer_une_formation_avec_ses_tranches()
    {
        var fiche = await CreerAsync(Formation with
        {
            NiveauId = _neuviemeAnnee,
            Tranches = [new TrancheSaisie(100m, 9), new TrancheSaisie(100m, 1), new TrancheSaisie(100m, 5)],
        });

        Assert.Equal(("Formation", 300m, (int?)10), (fiche.Type, fiche.PrixTotal, fiche.NombreDeSeances));
        Assert.Equal([new TrancheDuGroupe(100m, 1), new TrancheDuGroupe(100m, 5), new TrancheDuGroupe(100m, 9)], fiche.Tranches);
        Assert.Equal(10, fiche.Seances.Count);
        Assert.All(fiche.Seances, s => Assert.Null(s.Prix));
        Assert.Equal(new NiveauDuGroupe(_neuviemeAnnee, "9ᵉ année de base"), fiche.Niveau);
    }

    [Fact]
    public async Task Une_formation_sans_tranche_ni_niveau()
    {
        var fiche = await CreerAsync(Formation with { Tranches = null });

        Assert.Empty(fiche.Tranches);
        Assert.Null(fiche.Niveau);
    }

    [Fact]
    public async Task Une_formation_d_ete_sans_annee_scolaire()
    {
        var (_, clientDesOliviers, _) = await CentreAsync("Centre Les Oliviers", avecAnneeScolaire: false);
        var leila = await ProfesseurAsync(clientDesOliviers);

        var reponse = await clientDesOliviers.PostAsJsonAsync(Adresse, Formation with { ProfesseurId = leila, DateDeDebut = new DateOnly(2028, 7, 15) });

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
    }

    [Theory]
    [InlineData(100, 150, "La somme des tranches fait 250,000 TND au lieu de 300,000 TND : il manque 50,000 TND")]
    [InlineData(200, 150, "La somme des tranches fait 350,000 TND au lieu de 300,000 TND : il y a 50,000 TND de trop")]
    public async Task Les_tranches_qui_ne_font_pas_le_prix_total_sont_refusees(int premiere, int seconde, string message)
    {
        var reponse = await _client.PostAsJsonAsync(Adresse, Formation with
        {
            Tranches = [new TrancheSaisie(premiere, 1), new TrancheSaisie(seconde, 5)],
        });

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, message);
    }

    // ----- Les refus -----

    [Theory]
    [InlineData("type inconnu", "Le type du groupe est inconnu")]
    [InlineData("revision sans niveau", "Choisissez le niveau")]
    [InlineData("sans prix total", "Le prix total doit être positif, au millime près")]
    [InlineData("prix total nul", "Le prix total doit être positif, au millime près")]
    [InlineData("aucune seance", "Le nombre de séances va de 1 à 200")]
    [InlineData("trop de seances", "Le nombre de séances va de 1 à 200")]
    [InlineData("date incomplete", "Une séance doit avoir une date, une heure de début et une heure de fin")]
    [InlineData("date a l'envers", "Une séance doit finir après son début")]
    [InlineData("dates qui se chevauchent", "Deux séances se chevauchent")]
    [InlineData("pas assez de dates", "Il faut une date pour chacune des 3 séances")]
    [InlineData("sans date de debut", "La date de début est obligatoire")]
    [InlineData("sans creneau", "Ajoutez au moins un créneau")]
    [InlineData("tranche nulle", "Une tranche doit avoir un montant positif, au millime près")]
    [InlineData("tranche hors limite", "Une tranche est due à une séance de 1 à 10")]
    [InlineData("deux tranches sur la meme seance", "Deux tranches tombent sur la même séance")]
    public async Task Une_saisie_de_revision_ou_de_formation_incomplete_est_refusee(string cas, string message)
    {
        var mai = (int jour, string debut, string? fin) => new SeanceSaisie(new DateOnly(2028, 5, jour), debut, fin);
        var creation = cas switch
        {
            "type inconnu" => Revision with { Type = "Stage" },
            "revision sans niveau" => Revision with { NiveauId = null },
            "sans prix total" => Revision with { PrixTotal = null },
            "prix total nul" => Revision with { PrixTotal = 0m },
            "aucune seance" => Revision with { NombreDeSeances = 0 },
            "trop de seances" => Revision with { NombreDeSeances = 201 },
            "date incomplete" => Revision with { Dates = [mai(20, "10:00", null), mai(27, "10:00", "12:00"), mai(31, "10:00", "12:00")] },
            "date a l'envers" => Revision with { Dates = [mai(20, "12:00", "10:00"), mai(27, "10:00", "12:00"), mai(31, "10:00", "12:00")] },
            "dates qui se chevauchent" => Revision with { Dates = [mai(20, "10:00", "12:00"), mai(20, "11:00", "13:00"), mai(27, "10:00", "12:00")] },
            "pas assez de dates" => Revision with { Dates = [mai(20, "10:00", "12:00"), mai(27, "10:00", "12:00")] },
            "sans date de debut" => Revision with { DateDeDebut = null },
            "sans creneau" => Revision with { Creneaux = [] },
            "tranche nulle" => Formation with { Tranches = [new TrancheSaisie(0m, 1), new TrancheSaisie(300m, 5)] },
            "tranche hors limite" => Formation with { Tranches = [new TrancheSaisie(300m, 11)] },
            _ => Formation with { Tranches = [new TrancheSaisie(150m, 1), new TrancheSaisie(150m, 1)] },
        };

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, creation), HttpStatusCode.BadRequest, message);
        Assert.Equal(0, await NombreDeGroupesAsync());
    }

    [Fact]
    public async Task Le_resume_dit_si_le_groupe_est_termine()
    {
        await CreerAsync(Revision);

        var resume = (await _client.GetFromJsonAsync<List<ResumeDuGroupe>>(Adresse))!.Single();

        Assert.Equal(("Revision", false), (resume.Type, resume.Termine));
    }

    // ----- Un groupe déjà en cours (GRP-07) -----

    [Fact]
    public async Task Une_etude_deja_en_cours()
    {
        // « Mois 5, à partir du samedi 15/01/2028 » : il n'y a ni Mois 1 à 4, ni séances avant.
        var fiche = await CreerAsync(new CreationDUnGroupe(
            _karim, "Mathématiques", _neuviemeAnnee, [Samedi10h], 60m, new DateOnly(2028, 1, 15), 16, "", MoisEnCours: 5));

        Assert.Equal(17, fiche.PremiereSeance);
        Assert.Equal([5, 6, 7, 8, 9, 10], fiche.Mois.Select(m => m.Numero));
        Assert.Equal(
            [new DateOnly(2028, 1, 15), new DateOnly(2028, 1, 22), new DateOnly(2028, 1, 29), new DateOnly(2028, 2, 5)],
            fiche.Mois[0].Seances.Select(s => s.Date));
        Assert.All(fiche.Mois, m => Assert.Equal(60m, m.Prix));
        Assert.Equal(60m, fiche.PrixDuMois);
    }

    [Fact]
    public async Task Une_revision_deja_en_cours()
    {
        // « Séance 4 sur 10, le 15/01/2028 » : les séances 4 à 10, au prix calculé sur les 10.
        var fiche = await CreerAsync(Revision with
        {
            PrixTotal = 50m,
            NombreDeSeances = 10,
            SeanceEnCours = 4,
            DateDeDebut = new DateOnly(2028, 1, 15),
        });

        Assert.Equal(4, fiche.PremiereSeance);
        Assert.Equal([4, 5, 6, 7, 8, 9, 10], fiche.Seances.Select(s => s.Numero));
        Assert.Equal(new DateOnly(2028, 1, 15), fiche.Seances[0].Date);
        Assert.All(fiche.Seances, s => Assert.Equal(5m, s.Prix));
    }

    [Fact]
    public async Task Une_formation_deja_en_cours_garde_toutes_ses_tranches()
    {
        var fiche = await CreerAsync(Formation with { SeanceEnCours = 4, DateDeDebut = new DateOnly(2028, 1, 15) });

        Assert.Equal([4, 5, 6, 7, 8, 9, 10], fiche.Seances.Select(s => s.Numero));
        Assert.Equal([1, 5, 9], fiche.Tranches.Select(t => t.Seance));
    }

    [Fact]
    public async Task Des_dates_une_a_une_pour_les_seances_a_venir()
    {
        var dates = Enumerable.Range(0, 7).Select(i => new SeanceSaisie(new DateOnly(2028, 1, 15).AddDays(7 * i), "10:00", "12:00")).ToList();
        var revision = Revision with { NombreDeSeances = 10, SeanceEnCours = 4, Creneaux = null, DateDeDebut = null };

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, revision with { Dates = dates.Take(6).ToList() }),
            HttpStatusCode.BadRequest, "Il faut une date pour chacune des 7 séances");
        var fiche = await CreerAsync(revision with { Dates = dates });

        Assert.Equal([4, 5, 6, 7, 8, 9, 10], fiche.Seances.Select(s => s.Numero));
    }

    [Theory]
    [InlineData("mois 0", "Le Mois en cours va de 1 à 20")]
    [InlineData("mois 21", "Le Mois en cours va de 1 à 20")]
    [InlineData("seance 0", "La séance en cours va de 1 à 10")]
    [InlineData("seance 11", "La séance en cours va de 1 à 10")]
    public async Task Ou_en_est_le_groupe_doit_etre_possible(string cas, string message)
    {
        var etude = new CreationDUnGroupe(_karim, "Mathématiques", _neuviemeAnnee, [Samedi10h], 60m, new DateOnly(2028, 1, 15), 16, "");
        var creation = cas switch
        {
            "mois 0" => etude with { MoisEnCours = 0 },
            "mois 21" => etude with { MoisEnCours = 21 },
            "seance 0" => Revision with { NombreDeSeances = 10, SeanceEnCours = 0 },
            _ => Revision with { NombreDeSeances = 10, SeanceEnCours = 11 },
        };

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, creation), HttpStatusCode.BadRequest, message);
    }

    // ----- Terminé, professeurs, modification -----

    [Fact]
    public async Task Une_revision_finie_est_terminee()
    {
        // Ses séances : les samedis 20/05, 27/05 et 03/06/2028.
        var revision = await CreerAsync(Revision);

        await AllerAuAsync(new DateTimeOffset(2028, 6, 3, 9, 0, 0, TimeSpan.Zero));
        Assert.False((await LireAsync(revision.Id)).Termine);

        await AllerAuAsync(new DateTimeOffset(2028, 6, 4, 9, 0, 0, TimeSpan.Zero));
        Assert.True((await LireAsync(revision.Id)).Termine);
        Assert.True((await _client.GetFromJsonAsync<List<ResumeDuGroupe>>(Adresse))!.Single().Termine);
    }

    [Fact]
    public async Task Un_professeur_dont_les_groupes_sont_termines_peut_partir()
    {
        await CreerAsync(Revision);
        Assert.Equal(1, (await ProfesseursAsync()).Single().GroupesOuverts);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/professeurs/{_karim}/depart", null)).StatusCode);

        await AllerAuAsync(new DateTimeOffset(2028, 6, 4, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(0, (await ProfesseursAsync()).Single().GroupesOuverts);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/professeurs/{_karim}/depart", null)).StatusCode);
    }

    [Fact]
    public async Task Modifier_une_revision_ne_touche_pas_a_son_prix()
    {
        var revision = await CreerAsync(Revision);

        var reponse = await _client.PutAsJsonAsync($"{Adresse}/{revision.Id}", new ModificationDUnGroupe("Révision du bac", 20, 99m));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var fiche = (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!;
        Assert.Equal(("Révision du bac", (int?)20, (decimal?)null, (decimal?)50m), (fiche.Nom, fiche.Capacite, fiche.PrixDuMois, fiche.PrixTotal));
        Assert.Equal([16.667m, 16.667m, 16.666m], fiche.Seances.Select(s => s.Prix));
        var sansPrix = await _client.PutAsJsonAsync($"{Adresse}/{revision.Id}", new ModificationDUnGroupe("Révision du bac", null, null));
        Assert.Equal(HttpStatusCode.OK, sansPrix.StatusCode);
    }

    // ----- Des dates possibles (relecture) -----

    [Fact]
    public async Task Une_revision_deja_finie_est_refusee()
    {
        // Le 17/05/2025 au lieu du 17/05/2028 : ses trois séances seraient toutes passées.
        var reponse = await _client.PostAsJsonAsync(Adresse, Revision with { DateDeDebut = new DateOnly(2025, 5, 17) });

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, "Ce groupe serait déjà terminé : vérifiez les dates");
        Assert.Equal(0, await NombreDeGroupesAsync());
    }

    [Fact]
    public async Task Une_formation_deja_en_cours_peut_avoir_des_seances_passees()
    {
        // « Séance 4 sur 10 » depuis trois semaines : ses dernières séances sont encore à venir.
        var aujourdhui = DateOnly.FromDateTime(DateTime.UtcNow);
        var reponse = await _client.PostAsJsonAsync(Adresse, Formation with { SeanceEnCours = 4, DateDeDebut = aujourdhui.AddDays(-21) });

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
    }

    [Theory]
    [InlineData("creneaux")]
    [InlineData("dates")]
    public async Task Une_date_trop_lointaine_est_refusee(string mode)
    {
        var creation = mode == "creneaux"
            ? Revision with { DateDeDebut = new DateOnly(9999, 12, 25) }
            : Revision with
            {
                Creneaux = null,
                DateDeDebut = null,
                Dates =
                [
                    new SeanceSaisie(new DateOnly(2028, 5, 20), "10:00", "12:00"),
                    new SeanceSaisie(new DateOnly(2028, 5, 27), "10:00", "12:00"),
                    new SeanceSaisie(new DateOnly(2040, 5, 26), "10:00", "12:00"),
                ],
            };

        var reponse = await _client.PostAsJsonAsync(Adresse, creation);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, "Une date est à plus de 5 ans d'ici : vérifiez les dates");
    }

    // ----- Outils -----

    /// <summary>Avance l'horloge des tests, puis reconnecte Amira : sa session a pu expirer entre-temps.</summary>
    private async Task AllerAuAsync(DateTimeOffset moment)
    {
        _application.Horloge.SetUtcNow(moment);
        _client = await _application.ClientConnecteAsync(_amira);
    }

    private async Task<FicheDuGroupe> LireAsync(Guid id) => (await _client.GetFromJsonAsync<FicheDuGroupe>($"{Adresse}/{id}"))!;

    private async Task<List<ProfesseurDuCentre>> ProfesseursAsync() =>
        (await _client.GetFromJsonAsync<List<ProfesseurDuCentre>>("/api/professeurs"))!;

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

    private async Task<FicheDuGroupe> CreerAsync(CreationDUnGroupe creation)
    {
        var reponse = await _client.PostAsJsonAsync(Adresse, creation);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!;
    }

    private Task<int> NombreDeGroupesAsync() =>
        _application.DansUnScopeAsync(services =>
        {
            services.GetRequiredService<CentreCourant>().Definir(_jasmins.Id);
            return services.GetRequiredService<SchoolHubDbContext>().Groupes.CountAsync();
        });

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
