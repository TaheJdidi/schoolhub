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
/// SEA-03 à SEA-06 : changer le calendrier. Chaque test part d'un nouveau centre « Jasmins » (année scolaire du
/// 15/09/2027 au 30/06/2028) et du groupe de Mathématiques de Karim Ben Salah, le samedi de 10h à 12h depuis le
/// 18/09/2027, à 40 TND le Mois : le Mois 3 va du 13/11 au 04/12. On est le samedi 27/11/2027 à 10h à Tunis. Adem Miled
/// y est inscrit depuis le 20/11, Lina Gharbi depuis le 27/11.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class CalendrierTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "calendrier";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly CreneauSaisi Samedi10h = new("Samedi", "10:00", "12:00");

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _secretaire = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _karim;
    private Guid _maths;
    private Guid _lina;

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
        await InscrireAsync("Adem", "Miled", "98 401 220", "22 345 678", _maths, new DateOnly(2027, 11, 20));
        _lina = await InscrireAsync("Lina", "Gharbi", "97 111 222", "50 111 222", _maths, new DateOnly(2027, 11, 27));
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau9 => _niveaux.Single(n => n.Nom == "9ᵉ année de base").Id;

    private static DateOnly Le(int jour, int mois, int annee = 2027) => new(annee, mois, jour);

    // ----- Annuler (SEA-03) -----

    [Fact]
    public async Task Annuler_une_seance_allonge_le_mois()
    {
        var changement = await ActionAsync($"/api/seances/{await SeanceAsync(_maths, Le(4, 12))}/annulation", new AnnulationSaisie("Professeur absent"));

        var fiche = await FicheAsync(_maths);
        var mois3 = fiche.Mois.Single(m => m.Numero == 3);
        Assert.Equal([Le(13, 11), Le(20, 11), Le(27, 11), Le(11, 12)], mois3.Seances.Select(s => s.Date));
        Assert.Equal(Le(18, 12), fiche.Mois.Single(m => m.Numero == 4).Seances[0].Date);
        var annulee = Assert.Single(fiche.HorsCompte!);
        Assert.Equal((Le(4, 12), true, "Professeur absent", "Prevue"), (annulee.Date, annulee.Annulee, annulee.Motif, annulee.Nature));

        Assert.Equal("Bonjour, la séance de mathématiques du samedi 04/12/2027, de 10h à 12h, est annulée. Centre Les Jasmins.", changement.Texte);
        Assert.Equal(["Lina", "Adem"], changement.Eleves.Select(e => e.Prenom));
        Assert.Equal(["Parent", "Adem"], changement.Eleves[1].Messages.Select(m => m.Destinataire));
        var ligne = Assert.Single(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.SeanceAnnulee);
        Assert.Equal(("Mathématiques, Groupe Samedi 10h → 12h · Karim Ben Salah · Professeur absent", "samedi 04/12/2027, 10h → 12h", "Annulée"),
            (ligne.Detail, ligne.Avant, ligne.Apres));
    }

    [Fact]
    public async Task Une_seance_annulee_change_ce_que_l_eleve_doit()
    {
        // Lina arrive le 27/11, la 3ᵉ séance du Mois 3 : elle doit 2 séances, 20 TND.
        Assert.Equal(20m, await TotalAPayerAsync(_lina));

        // Le 13/11 n'a pas eu lieu : le 27/11 devient la 2ᵉ séance du Mois 3, et Lina en doit 3.
        await ActionAsync($"/api/seances/{await SeanceAsync(_maths, Le(13, 11))}/annulation", new AnnulationSaisie("Jour férié"));

        Assert.Equal(30m, await TotalAPayerAsync(_lina));
    }

    [Theory]
    [InlineData("", HttpStatusCode.BadRequest, "Écrivez le motif de l'annulation")]
    [InlineData("deux fois", HttpStatusCode.Conflict, "Cette séance est déjà annulée")]
    [InlineData("appel fait", HttpStatusCode.Conflict, "Cette séance a son appel : elle a eu lieu")]
    public async Task Une_annulation_refusee(string cas, HttpStatusCode statut, string message)
    {
        var seance = await SeanceAsync(_maths, Le(27, 11));
        if (cas == "deux fois")
            await ActionAsync($"/api/seances/{seance}/annulation", new AnnulationSaisie("Professeur absent"));
        if (cas == "appel fait")
            Assert.Equal(HttpStatusCode.OK, (await _secretaire.PutAsJsonAsync($"/api/seances/{seance}/appel", new AppelSaisi([]))).StatusCode);

        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/seances/{seance}/annulation", new AnnulationSaisie(cas == "" ? " " : "Professeur absent")),
            statut, message);
    }

    [Fact]
    public async Task Une_seance_annulee_n_a_pas_d_appel()
    {
        var seance = await SeanceAsync(_maths, Le(27, 11));
        await ActionAsync($"/api/seances/{seance}/annulation", new AnnulationSaisie("Professeur absent"));

        Assert.False((await _secretaire.GetFromJsonAsync<AppelDeLaSeance>($"/api/seances/{seance}/appel"))!.Ouvert);
        await VerifierLeRefusAsync(await _secretaire.PutAsJsonAsync($"/api/seances/{seance}/appel", new AppelSaisi([])),
            HttpStatusCode.Conflict, "Cette séance est annulée : elle n'a pas d'appel");
        Assert.DoesNotContain((await _secretaire.GetFromJsonAsync<List<AppelARecopier>>("/api/seances/a-recopier"))!, a => a.SeanceId == seance);
    }

    // ----- Déplacer (SEA-04) -----

    [Fact]
    public async Task Deplacer_une_seance()
    {
        var seance = await SeanceAsync(_maths, Le(4, 12));

        var changement = await ActionAsync($"/api/seances/{seance}/deplacement", new NouvelleSeanceSaisie(Le(7, 12), "17:00", "19:00"));

        var fiche = await FicheAsync(_maths);
        var mois3 = fiche.Mois.Single(m => m.Numero == 3).Seances;
        Assert.Equal([Le(13, 11), Le(20, 11), Le(27, 11), Le(7, 12)], mois3.Select(s => s.Date));
        Assert.Equal(("17:00", "Remplacement"), (mois3[3].Debut, mois3[3].Nature));
        var annulee = Assert.Single(fiche.HorsCompte!);
        Assert.Equal(("Déplacée au 07/12/2027", Le(7, 12)), (annulee.Motif, annulee.RemplaceeLe));
        Assert.Equal("Bonjour, la séance de mathématiques du samedi 04/12/2027 à 10h est déplacée au mardi 07/12/2027, de 17h à 19h. Centre Les Jasmins.",
            changement.Texte);
        var ligne = Assert.Single(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.SeanceDeplacee);
        Assert.Equal(("samedi 04/12/2027, 10h → 12h", "mardi 07/12/2027, 17h → 19h"), (ligne.Avant, ligne.Apres));
    }

    [Theory]
    [InlineData(null, "17:00", "19:00", HttpStatusCode.BadRequest, "Choisissez la date")]
    [InlineData("2027-12-07", "17h", "19:00", HttpStatusCode.BadRequest, "Choisissez l'heure de début et l'heure de fin")]
    [InlineData("2027-12-07", "19:00", "17:00", HttpStatusCode.BadRequest, "La séance doit finir après son début")]
    [InlineData("2027-12-11", "11:00", "13:00", HttpStatusCode.Conflict, "Le groupe a déjà une séance à ce moment-là")]
    public async Task Un_deplacement_refuse(string? date, string debut, string fin, HttpStatusCode statut, string message)
    {
        var seance = await SeanceAsync(_maths, Le(4, 12));

        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/seances/{seance}/deplacement",
            new NouvelleSeanceSaisie(date is null ? null : DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), debut, fin)), statut, message);
        Assert.Empty((await FicheAsync(_maths)).HorsCompte!);
    }

    [Fact]
    public async Task Remplacer_une_seance_deja_annulee()
    {
        var revision = await GroupeAsync(new CreationDUnGroupe(_karim, "Physique", Niveau9, [Samedi10h], null, Le(20, 11), null, "",
            Type: "Revision", PrixTotal: 60m, NombreDeSeances: 3));
        var seance = await SeanceAsync(revision, Le(4, 12));
        await ActionAsync($"/api/seances/{seance}/annulation", new AnnulationSaisie("Professeur absent"));
        Assert.Equal(2, (await FicheAsync(revision)).Seances.Count);

        var changement = await ActionAsync($"/api/seances/{seance}/remplacement", new NouvelleSeanceSaisie(Le(6, 12), "17:00", "19:00"));

        var fiche = await FicheAsync(revision);
        Assert.Equal([(1, Le(20, 11)), (2, Le(27, 11)), (3, Le(6, 12))], fiche.Seances.Select(s => (s.Numero, s.Date)));
        Assert.Equal(20m, fiche.Seances[2].Prix);
        Assert.StartsWith("Bonjour, la séance de physique du samedi 04/12/2027, annulée, est remplacée le lundi 06/12/2027", changement.Texte, StringComparison.Ordinal);
        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/seances/{seance}/remplacement", new NouvelleSeanceSaisie(Le(8, 12), "17:00", "19:00")),
            HttpStatusCode.Conflict, "Cette séance est déjà remplacée");
        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/seances/{await SeanceAsync(revision, Le(27, 11))}/remplacement",
            new NouvelleSeanceSaisie(Le(8, 12), "17:00", "19:00")), HttpStatusCode.Conflict, "Seule une séance annulée se remplace");
    }

    // ----- Ajouter une séance supplémentaire (SEA-05) -----

    [Fact]
    public async Task Une_seance_supplementaire_ne_compte_pas()
    {
        var changement = await ActionAsync($"/api/groupes/{_maths}/seances", new NouvelleSeanceSaisie(Le(2, 12), "17:00", "19:00"));

        var fiche = await FicheAsync(_maths);
        Assert.Equal([Le(13, 11), Le(20, 11), Le(27, 11), Le(4, 12)], fiche.Mois.Single(m => m.Numero == 3).Seances.Select(s => s.Date));
        var supplementaire = Assert.Single(fiche.HorsCompte!);
        Assert.Equal((Le(2, 12), "Supplementaire", false), (supplementaire.Date, supplementaire.Nature, supplementaire.Annulee));
        Assert.Equal(20m, await TotalAPayerAsync(_lina));
        Assert.Equal("Bonjour, une séance supplémentaire de mathématiques est ajoutée le jeudi 02/12/2027, de 17h à 19h. Elle est offerte. Centre Les Jasmins.",
            changement.Texte);
        // Elle a son appel, comme les autres.
        Assert.Equal(2, (await _secretaire.GetFromJsonAsync<AppelDeLaSeance>($"/api/seances/{supplementaire.Id}/appel"))!.Eleves.Count);
    }

    // ----- Changer l'horaire (SEA-06) -----

    [Fact]
    public async Task Changer_l_horaire_d_une_etude_a_partir_d_une_date()
    {
        var changement = await ActionAsync($"/api/groupes/{_maths}/horaire",
            new ChangementDHoraire([new CreneauSaisi("Mardi", "17:00", "19:00")], Le(8, 1, 2028)));

        var fiche = await FicheAsync(_maths);
        var dates = fiche.Mois.SelectMany(m => m.Seances).Select(s => s.Date).ToList();
        Assert.Contains(Le(1, 1, 2028), dates);
        Assert.DoesNotContain(Le(8, 1, 2028), dates);
        Assert.Equal(Le(11, 1, 2028), dates.First(d => d >= Le(8, 1, 2028)));
        Assert.All(dates.Where(d => d >= Le(8, 1, 2028)), d => Assert.Equal(DayOfWeek.Tuesday, d.DayOfWeek));
        Assert.Equal(Le(27, 6, 2028), dates[^1]);
        Assert.Equal([new CreneauDuGroupe("Mardi", "17:00", "19:00")], fiche.Creneaux);
        Assert.Equal("Bonjour, à partir du samedi 08/01/2028, la séance de mathématiques a lieu le mardi de 17h à 19h. Centre Les Jasmins.", changement.Texte);
    }

    [Fact]
    public async Task Changer_l_horaire_d_une_revision_garde_son_nombre_de_seances()
    {
        var revision = await GroupeAsync(new CreationDUnGroupe(_karim, "Physique", Niveau9, [Samedi10h], null, Le(20, 11), null, "",
            Type: "Revision", PrixTotal: 60m, NombreDeSeances: 3));

        await ActionAsync($"/api/groupes/{revision}/horaire", new ChangementDHoraire([new CreneauSaisi("Lundi", "17:00", "19:00")], Le(28, 11)));

        Assert.Equal([Le(20, 11), Le(27, 11), Le(29, 11)], (await FicheAsync(revision)).Seances.Select(s => s.Date));
    }

    [Fact]
    public async Task Un_jour_annule_le_reste_avec_le_nouvel_horaire()
    {
        var onze = await SeanceAsync(_maths, Le(11, 12));
        await ActionAsync($"/api/seances/{onze}/annulation", new AnnulationSaisie("Jour férié"));

        await ActionAsync($"/api/groupes/{_maths}/horaire", new ChangementDHoraire([new CreneauSaisi("Samedi", "14:00", "16:00")], Le(4, 12)));

        var fiche = await FicheAsync(_maths);
        var seances = fiche.Mois.SelectMany(m => m.Seances).Where(s => s.Date >= Le(4, 12)).ToList();
        Assert.Equal([Le(4, 12), Le(18, 12)], seances.Take(2).Select(s => s.Date));
        Assert.All(seances, s => Assert.Equal("14:00", s.Debut));
        // La séance annulée reste visible, avec son motif.
        var annulee = Assert.Single(fiche.HorsCompte!);
        Assert.Equal((onze, "Jour férié"), (annulee.Id, annulee.Motif));
    }

    [Fact]
    public async Task Une_seance_annulee_et_remplacee_bloque_le_nouvel_horaire()
    {
        await ActionAsync($"/api/seances/{await SeanceAsync(_maths, Le(11, 12))}/deplacement", new NouvelleSeanceSaisie(Le(14, 12), "17:00", "19:00"));

        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/groupes/{_maths}/horaire",
                new ChangementDHoraire([new CreneauSaisi("Mardi", "17:00", "19:00")], Le(4, 12))),
            HttpStatusCode.Conflict, "La séance du 11/12/2027 est annulée et remplacée : faites partir le nouvel horaire après cette date");
        // Après elle, le nouvel horaire passe.
        await ActionAsync($"/api/groupes/{_maths}/horaire", new ChangementDHoraire([new CreneauSaisi("Mardi", "17:00", "19:00")], Le(15, 12)));
    }

    [Fact]
    public async Task Le_nouvel_horaire_part_au_plus_tot_demain()
    {
        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/groupes/{_maths}/horaire",
            new ChangementDHoraire([new CreneauSaisi("Mardi", "17:00", "19:00")], Le(27, 11))), HttpStatusCode.BadRequest, "Le nouvel horaire part au plus tôt demain");
        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/groupes/{_maths}/horaire",
            new ChangementDHoraire([], Le(4, 12))), HttpStatusCode.BadRequest, "Ajoutez au moins un créneau");
    }

    // ----- Un groupe fermé, un autre centre -----

    [Fact]
    public async Task Un_groupe_ferme_ne_change_plus()
    {
        var anglais = await GroupeAsync(new CreationDUnGroupe(_karim, "Anglais", Niveau9, [Samedi10h], 40m, Le(18, 9), null, ""));
        var seance = await SeanceAsync(anglais, Le(20, 11));
        Assert.Equal(HttpStatusCode.OK, (await _secretaire.PostAsJsonAsync($"/api/groupes/{anglais}/fermeture",
            new FermetureDUnGroupe(Le(27, 11), "Trop peu d'élèves"))).StatusCode);

        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/seances/{seance}/annulation", new AnnulationSaisie("Professeur absent")),
            HttpStatusCode.Conflict, "Ce groupe est fermé : ses séances ne changent plus");
        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/groupes/{anglais}/seances", new NouvelleSeanceSaisie(Le(2, 12), "17:00", "19:00")),
            HttpStatusCode.Conflict, "Ce groupe est fermé : ses séances ne changent plus");
    }

    [Fact]
    public async Task Le_calendrier_d_un_autre_centre_est_introuvable()
    {
        var seance = await SeanceAsync(_maths, Le(4, 12));
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));

        await VerifierLeRefusAsync(await rim.PostAsJsonAsync($"/api/seances/{seance}/annulation", new AnnulationSaisie("Professeur absent")),
            HttpStatusCode.NotFound, "Séance introuvable");
        await VerifierLeRefusAsync(await rim.PostAsJsonAsync($"/api/groupes/{_maths}/seances", new NouvelleSeanceSaisie(Le(2, 12), "17:00", "19:00")),
            HttpStatusCode.NotFound, "Groupe introuvable");
        Assert.Empty((await FicheAsync(_maths)).HorsCompte!);
    }

    // ----- Outils -----

    private async Task<ChangementDuCalendrier> ActionAsync<T>(string adresse, T saisie)
    {
        var reponse = await _secretaire.PostAsJsonAsync(adresse, saisie);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<ChangementDuCalendrier>())!;
    }

    private async Task<Guid> GroupeAsync(CreationDUnGroupe creation)
    {
        var reponse = await _secretaire.PostAsJsonAsync("/api/groupes", creation);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
    }

    private async Task<Guid> InscrireAsync(string prenom, string nom, string parent, string telephone, Guid groupe, DateOnly debutLe)
    {
        var reponse = await _secretaire.PostAsJsonAsync("/api/eleves", new FicheSaisieDeLEleve(prenom, nom, Niveau9, false, parent, null, telephone,
            $"{prenom.ToLowerInvariant()}@exemple.tn", null));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var eleve = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        Assert.Equal(HttpStatusCode.Created, (await _secretaire.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(eleve, groupe, null, debutLe))).StatusCode);
        return eleve;
    }

    private async Task<FicheDuGroupe> FicheAsync(Guid groupe) => (await _secretaire.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{groupe}"))!;

    /// <summary>La séance qui compte du groupe à cette date.</summary>
    private async Task<Guid> SeanceAsync(Guid groupe, DateOnly date)
    {
        var fiche = await FicheAsync(groupe);
        return fiche.Mois.SelectMany(m => m.Seances).Select(s => (s.Id, s.Date))
            .Concat(fiche.Seances.Select(s => (s.Id, s.Date)))
            .Single(s => s.Date == date).Id;
    }

    private async Task<decimal> TotalAPayerAsync(Guid eleve) => (await _secretaire.GetFromJsonAsync<APayer>($"/api/eleves/{eleve}/a-payer"))!.Total;

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
