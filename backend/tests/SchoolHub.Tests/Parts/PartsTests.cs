using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Caisse;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Parts;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Modules.Seances;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Parts;

/// <summary>
/// PRT-01 à PRT-04 : les parts des professeurs. Chaque test part d'un nouveau centre « Jasmins » (part du centre 5 %,
/// année scolaire du 15/09/2027 au 30/06/2028) et du groupe de Mathématiques de Karim Ben Salah, le samedi de 10h à 12h
/// depuis le 18/09/2027, à 193,750 TND le Mois : Mois 1 du 18/09 au 09/10, Mois 2 du 16/10 au 06/11, Mois 3 du 13/11 au
/// 04/12. Adem Miled et Lina Gharbi y sont inscrits depuis le 18/09. On est le samedi 27/11/2027 à 10h à Tunis.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class PartsTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "parts";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _hedi = null!;
    private Compte _compteDeHedi = null!;
    private HttpClient _amira = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _karim;
    private Guid _maths;
    private (Guid Eleve, Guid Inscription) _adem;
    private (Guid Eleve, Guid Inscription) _lina;

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
            "Centre Les Jasmins", null, null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), null, null, null, 5m))).StatusCode);
        _amira = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi"));
        _niveaux = (await _amira.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        _karim = (await (await _amira.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        _maths = await GroupeAsync(new CreationDUnGroupe(_karim, "Mathématiques", Niveau9, [Samedi10h], 193.75m, Le(18, 9), null, ""));
        _adem = await InscrireAsync("Adem", "Miled", "22 345 678", _maths, Le(18, 9));
        _lina = await InscrireAsync("Lina", "Gharbi", "50 111 222", _maths, Le(18, 9));
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private static readonly CreneauSaisi Samedi10h = new("Samedi", "10:00", "12:00");

    private Guid Niveau9 => _niveaux.Single(n => n.Nom == "9ᵉ année de base").Id;

    private static DateOnly Le(int jour, int mois, int annee = 2027) => new(annee, mois, jour);

    // ----- Les parts à payer (PRT-01) -----

    [Fact]
    public async Task Une_ligne_par_mois_fini_sur_le_montant_du_groupe_paye_ou_non()
    {
        // Adem a payé son Mois 1, Lina non : le montant du groupe est le même.
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync($"/api/eleves/{_adem.Eleve}/encaissement",
            new EncaissementSaisi([new SommeChoisie(_adem.Inscription, "Mois 1")]))).StatusCode);

        var karim = Assert.Single((await PartsAsync()).Professeurs);

        Assert.Equal(("Karim Ben Salah", 5m), (karim.Nom, karim.Pourcentage));
        // Le Mois 3 finit le 04/12 : pas encore de ligne.
        Assert.Equal(["Mois 1", "Mois 2"], karim.Lignes.Select(l => l.Periode));
        var mois1 = karim.Lignes[0];
        // 2 × 193,750 = 387,500 ; 5 % = 19,375 : les millimes sont gardés.
        Assert.Equal((387.5m, 19.375m, 0m, 368.125m, false), (mois1.MontantDuGroupe, mois1.PartDuCentre, mois1.AvanceDeduite, mois1.AVerser, mois1.Payee));
        Assert.Equal((Le(18, 9), Le(9, 10)), (mois1.Debut, mois1.Fin));
        Assert.Equal((736.25m, 0m, 0m), (karim.ResteAVerser, karim.DejaVerse, karim.AvancesRestantes));
    }

    [Fact]
    public async Task Un_prix_special_compte_pour_son_prix()
    {
        Assert.Equal(HttpStatusCode.OK, (await _hedi.PutAsJsonAsync($"/api/inscriptions/{_lina.Inscription}/prix-special",
            new PrixSpecialSaisi(0m, "Famille en difficulté"))).StatusCode);

        var mois1 = (await PartsAsync()).Professeurs[0].Lignes[0];

        // Lina, gratuite, compte pour 0.
        Assert.Equal(193.75m, mois1.MontantDuGroupe);
    }

    [Fact]
    public async Task Une_seance_annulee_allonge_le_mois_et_retarde_sa_ligne()
    {
        // Un groupe du 30/10 : son Mois 1 (30/10, 06/11, 13/11, 20/11) est fini.
        var physique = await GroupeAsync(new CreationDUnGroupe(_karim, "Physique", Niveau9, [new CreneauSaisi("Samedi", "14:00", "16:00")], 40m, Le(30, 10), null, ""));
        await InscrireAsync("Sami", "Chaabane", "50 000 333", physique, Le(30, 10));
        Assert.Contains((await PartsAsync()).Professeurs[0].Lignes, l => l.GroupeId == physique && l.Periode == "Mois 1");

        var fiche = (await _amira.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{physique}"))!;
        var treize = fiche.Mois.SelectMany(m => m.Seances).Single(s => s.Date == Le(13, 11)).Id;
        Assert.Equal(HttpStatusCode.OK, (await _amira.PostAsJsonAsync($"/api/seances/{treize}/annulation", new AnnulationSaisie("Professeur absent"))).StatusCode);

        // Le Mois 1 finit maintenant le 27/11 : il n'est pas encore fini.
        Assert.DoesNotContain((await PartsAsync()).Professeurs[0].Lignes, l => l.GroupeId == physique);
    }

    [Fact]
    public async Task Une_revision_a_sa_ligne_apres_sa_derniere_seance()
    {
        // Une Révision de 3 séances, les 20/11, 27/11 et 04/12 : sa ligne naît le lendemain de la dernière.
        var revision = await GroupeAsync(new CreationDUnGroupe(_karim, "Physique", Niveau9, [new CreneauSaisi("Samedi", "14:00", "16:00")], null, Le(20, 11), null, "",
            Type: "Revision", PrixTotal: 60m, NombreDeSeances: 3));
        await InscrireAsync("Sami", "Chaabane", "50 000 333", revision, Le(20, 11), "EnTotalite");
        Assert.DoesNotContain((await PartsAsync()).Professeurs[0].Lignes, l => l.GroupeId == revision);
        _application.Horloge.SetUtcNow(Aujourdhui.AddDays(8));
        _hedi = await _application.ClientConnecteAsync(_compteDeHedi);

        var ligne = Assert.Single((await PartsAsync()).Professeurs[0].Lignes, l => l.GroupeId == revision);

        Assert.Equal(("Toute la Révision", 60m, 3m, 57m), (ligne.Periode, ligne.MontantDuGroupe, ligne.PartDuCentre, ligne.AVerser));
    }

    // ----- Payée (PRT-02) et avances (PRT-03) -----

    [Fact]
    public async Task Une_avance_se_deduit_des_parts_suivantes_et_une_part_payee_reste_figee()
    {
        var parts = await AvancerAsync(new AvanceSaisie(_karim, 500m, Le(5, 11)));

        // 500 TND : 368,125 sur le Mois 1, le reste (131,875) sur le Mois 2.
        var karim = parts.Professeurs[0];
        Assert.Equal([(368.125m, 0m), (131.875m, 236.25m)], karim.Lignes.Select(l => (l.AvanceDeduite, l.AVerser)));
        Assert.Equal((236.25m, 0m), (karim.ResteAVerser, karim.AvancesRestantes));

        parts = await MarquerAsync("Mois 1", true);
        Assert.Equal((true, Le(27, 11)), (parts.Professeurs[0].Lignes[0].Payee, parts.Professeurs[0].Lignes[0].PayeeLe));

        // Une nouvelle avance ne touche pas la part payée.
        parts = await AvancerAsync(new AvanceSaisie(_karim, 300m, Le(20, 11)));
        Assert.Equal([(368.125m, 0m), (368.125m, 0m)], parts.Professeurs[0].Lignes.Select(l => (l.AvanceDeduite, l.AVerser)));
        Assert.Equal((0m, 0m, 63.75m), (parts.ResteAVerser, parts.DejaVerse, parts.AvancesRestantes));

        // « Non payée » la remet à verser : elle se recalcule.
        parts = await MarquerAsync("Mois 1", false);
        Assert.False(parts.Professeurs[0].Lignes[0].Payee);
    }

    [Fact]
    public async Task Les_refus()
    {
        await VerifierLeRefusAsync(await _hedi.PostAsJsonAsync("/api/parts/paiement", new PaiementDUnePart(_maths, "Mois 3", true)),
            HttpStatusCode.NotFound, "Part introuvable");
        await VerifierLeRefusAsync(await _hedi.PostAsJsonAsync("/api/parts/avances", new AvanceSaisie(null, 100m, Le(5, 11))),
            HttpStatusCode.BadRequest, "Choisissez le professeur");
        await VerifierLeRefusAsync(await _hedi.PostAsJsonAsync("/api/parts/avances", new AvanceSaisie(_karim, 0m, Le(5, 11))),
            HttpStatusCode.BadRequest, "Le montant de l'avance est positif, au millime près");
        await VerifierLeRefusAsync(await _hedi.PostAsJsonAsync("/api/parts/avances", new AvanceSaisie(_karim, 100m, null)),
            HttpStatusCode.BadRequest, "Choisissez la date");
        await VerifierLeRefusAsync(await _hedi.PostAsJsonAsync("/api/parts/avances", new AvanceSaisie(Guid.NewGuid(), 100m, Le(5, 11))),
            HttpStatusCode.NotFound, "Professeur introuvable");
    }

    // ----- La part du centre d'un professeur (PRT-04) -----

    [Fact]
    public async Task La_part_d_un_professeur_vaut_pour_les_periodes_suivantes()
    {
        Assert.Equal(new PartDuCentreDUnProfesseur(null, 5m, null), await _hedi.GetFromJsonAsync<PartDuCentreDUnProfesseur>($"/api/professeurs/{_karim}/part-du-centre"));

        var reponse = await _hedi.PutAsJsonAsync($"/api/professeurs/{_karim}/part-du-centre", new PartSaisie(10m));

        Assert.Equal(new PartDuCentreDUnProfesseur(10m, 5m, Le(27, 11)), await reponse.Content.ReadFromJsonAsync<PartDuCentreDUnProfesseur>());
        // Les Mois déjà commencés gardent 5 %.
        var karim = (await PartsAsync()).Professeurs[0];
        Assert.Equal(10m, karim.Pourcentage);
        Assert.All(karim.Lignes, l => Assert.Equal(5m, l.Pourcentage));
        await VerifierLeRefusAsync(await _hedi.PutAsJsonAsync($"/api/professeurs/{_karim}/part-du-centre", new PartSaisie(100.5m)),
            HttpStatusCode.BadRequest, "La part du centre va de 0 à 100 %, au centième près");
    }

    [Fact]
    public async Task Tout_est_reserve_au_proprietaire_de_son_centre()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.GetAsync("/api/parts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.PostAsJsonAsync("/api/parts/avances", new AvanceSaisie(_karim, 100m, Le(5, 11)))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.GetAsync($"/api/professeurs/{_karim}/part-du-centre")).StatusCode);

        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi", RoleDuCompte.Proprietaire));
        Assert.Empty((await rim.GetFromJsonAsync<PartsDuCentre>("/api/parts"))!.Professeurs);
        await VerifierLeRefusAsync(await rim.GetAsync($"/api/professeurs/{_karim}/part-du-centre"), HttpStatusCode.NotFound, "Professeur introuvable");
    }

    // ----- Outils -----

    private async Task<PartsDuCentre> PartsAsync() => (await _hedi.GetFromJsonAsync<PartsDuCentre>("/api/parts"))!;

    private async Task<PartsDuCentre> AvancerAsync(AvanceSaisie saisie)
    {
        var reponse = await _hedi.PostAsJsonAsync("/api/parts/avances", saisie);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<PartsDuCentre>())!;
    }

    private async Task<PartsDuCentre> MarquerAsync(string cle, bool payee)
    {
        var reponse = await _hedi.PostAsJsonAsync("/api/parts/paiement", new PaiementDUnePart(_maths, cle, payee));
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<PartsDuCentre>())!;
    }

    private async Task<Guid> GroupeAsync(CreationDUnGroupe creation)
    {
        var reponse = await _amira.PostAsJsonAsync("/api/groupes", creation);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
    }

    private async Task<(Guid Eleve, Guid Inscription)> InscrireAsync(string prenom, string nom, string telephone, Guid groupe, DateOnly debutLe, string? mode = null)
    {
        var reponse = await _amira.PostAsJsonAsync("/api/eleves", new FicheSaisieDeLEleve(prenom, nom, Niveau9, false, "97 000 111", null, telephone,
            $"{prenom.ToLowerInvariant()}@exemple.tn", null));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var eleve = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        var inscription = await _amira.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(eleve, groupe, mode, debutLe));
        Assert.Equal(HttpStatusCode.Created, inscription.StatusCode);
        return (eleve, (await inscription.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id);
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
