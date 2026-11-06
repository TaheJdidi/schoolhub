using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Modules.Seances;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Seances;

/// <summary>
/// SEA-08 et SEA-12 : la feuille d'appel et le rattrapage. Chaque test part d'un nouveau centre « Jasmins » (année
/// scolaire du 15/09/2027 au 30/06/2028) et de deux groupes de Mathématiques, 9ᵉ année, de Karim Ben Salah : le samedi de
/// 10h à 12h et le mardi de 17h à 19h, depuis le 18/09/2027. On est le samedi 27/11/2027 à 10h à Tunis. Adem Miled est
/// dans le groupe du samedi depuis le 20/11 ; Lina Gharbi dans celui du mardi depuis le 23/11.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class RattrapageEtFeuilleTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "rattrapage_et_feuille";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _secretaire = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _karim;
    private Guid _samedi;
    private Guid _mardi;
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
        _karim = await ProfesseurAsync("Karim", "Ben Salah", "98 401 220");
        _samedi = await GroupeAsync(_karim, "Mathématiques", Niveau("9ᵉ année de base"), new CreneauSaisi("Samedi", "10:00", "12:00"));
        _mardi = await GroupeAsync(_karim, "Mathématiques", Niveau("9ᵉ année de base"), new CreneauSaisi("Mardi", "17:00", "19:00"));
        _adem = await InscrireAsync("Adem", "Miled", "98 401 220", "22 345 678", _samedi, Le(20, 11));
        _lina = await InscrireAsync("Lina", "Gharbi", "97 111 222", "50 111 222", _mardi, Le(23, 11));
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau(string nom) => _niveaux.Single(n => n.Nom == nom).Id;

    private static DateOnly Le(int jour, int mois, int annee = 2027) => new(annee, mois, jour);

    // ----- Le rattrapage (SEA-12) -----

    [Fact]
    public async Task Les_eleves_qui_peuvent_venir_en_rattrapage()
    {
        // Un groupe d'un autre niveau, ou d'une autre matière, ne compte pas.
        var autreNiveau = await GroupeAsync(_karim, "Mathématiques", Niveau("8ᵉ année de base"), new CreneauSaisi("Lundi", "17:00", "19:00"));
        await InscrireAsync("Sami", "Chaabane", "97 000 333", "50 000 333", autreNiveau, Le(22, 11), Niveau("8ᵉ année de base"));
        var physique = await GroupeAsync(_karim, "Physique", Niveau("9ᵉ année de base"), new CreneauSaisi("Jeudi", "17:00", "19:00"));
        await InscrireAsync("Rania", "Trabelsi", "97 000 444", "50 000 444", physique, Le(25, 11));

        var possibles = await PossiblesAsync(await SeanceAsync(_samedi, Le(27, 11)));

        var lina = Assert.Single(possibles);
        Assert.Equal((_lina.Inscription, "Lina", "Gharbi", _mardi), (lina.InscriptionId, lina.Prenom, lina.Nom, lina.Groupe.Id));
        // Ses séances du mardi, depuis son arrivée le 23/11, jusqu'à 8 semaines après le 27/11.
        Assert.Equal(Le(23, 11), lina.Seances[0].Date);
        Assert.Equal(Le(18, 1, 2028), lina.Seances[^1].Date);
    }

    [Fact]
    public async Task Ajouter_un_eleve_en_rattrapage()
    {
        var seance = await SeanceAsync(_samedi, Le(27, 11));
        var duMardi = await SeanceAsync(_mardi, Le(30, 11));

        var appel = await RattraperAsync(seance, new RattrapageSaisi(_lina.Inscription, duMardi));

        // Lina est à part : elle n'entre pas dans les élèves du groupe.
        Assert.Equal(["Adem"], appel.Eleves.Select(e => e.Prenom));
        var lina = Assert.Single(appel.Rattrapages!);
        Assert.Equal(("Lina", "Groupe Mardi 17h → 19h", Le(30, 11), false), (lina.Prenom, lina.GroupeDOrigine, lina.SeanceRattrapee, lina.Absent));

        // Dans son groupe, la séance rattrapée le montre.
        var origine = (await _secretaire.GetFromJsonAsync<AppelDeLaSeance>($"/api/seances/{duMardi}/appel"))!;
        Assert.Equal(Le(27, 11), origine.Eleves.Single(e => e.Prenom == "Lina").RattrapeeLe);
        // Elle continue de payer dans son groupe : le groupe d'accueil n'a pas d'élève de plus.
        Assert.Equal(1, (await _secretaire.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{_samedi}"))!.Eleves);
    }

    [Fact]
    public async Task L_appel_d_un_eleve_en_rattrapage()
    {
        var seance = await SeanceAsync(_samedi, Le(27, 11));
        await RattraperAsync(seance, new RattrapageSaisi(_lina.Inscription, await SeanceAsync(_mardi, Le(30, 11))));

        var reponse = await _secretaire.PutAsJsonAsync($"/api/seances/{seance}/appel", new AppelSaisi([_lina.Inscription]));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var lina = Assert.Single((await reponse.Content.ReadFromJsonAsync<AppelDeLaSeance>())!.Rattrapages!);
        Assert.True(lina.Absent);
        Assert.Equal(["Parent", "Lina"], lina.Messages.Select(m => m.Destinataire));
    }

    [Theory]
    [InlineData("même groupe", HttpStatusCode.BadRequest, "Cet élève est déjà dans ce groupe")]
    [InlineData("autre niveau", HttpStatusCode.BadRequest, "Le rattrapage se fait dans un groupe du même professeur, de la même matière et du même niveau")]
    [InlineData("séance d'un autre groupe", HttpStatusCode.BadRequest, "Choisissez la séance de son groupe qu'il rattrape")]
    [InlineData("séance d'avant son arrivée", HttpStatusCode.BadRequest, "Choisissez la séance de son groupe qu'il rattrape")]
    [InlineData("séance à plus de 8 semaines", HttpStatusCode.BadRequest, "Choisissez la séance de son groupe qu'il rattrape")]
    [InlineData("inscription inconnue", HttpStatusCode.NotFound, "Inscription introuvable")]
    [InlineData("deux fois la même séance", HttpStatusCode.Conflict, "Cette séance est déjà rattrapée")]
    public async Task Un_rattrapage_refuse(string cas, HttpStatusCode statut, string message)
    {
        var seance = await SeanceAsync(_samedi, Le(27, 11));
        var duMardi = await SeanceAsync(_mardi, Le(30, 11));
        RattrapageSaisi saisie;
        switch (cas)
        {
            case "même groupe":
                saisie = new RattrapageSaisi(_adem.Inscription, await SeanceAsync(_samedi, Le(20, 11)));
                break;
            case "autre niveau":
                var autreNiveau = await GroupeAsync(_karim, "Mathématiques", Niveau("8ᵉ année de base"), new CreneauSaisi("Lundi", "17:00", "19:00"));
                var sami = await InscrireAsync("Sami", "Chaabane", "97 000 333", "50 000 333", autreNiveau, Le(22, 11), Niveau("8ᵉ année de base"));
                saisie = new RattrapageSaisi(sami.Inscription, await SeanceAsync(autreNiveau, Le(29, 11)));
                break;
            case "séance d'un autre groupe":
                saisie = new RattrapageSaisi(_lina.Inscription, await SeanceAsync(_samedi, Le(4, 12)));
                break;
            case "séance d'avant son arrivée":
                // Lina est dans le groupe du mardi depuis le 23/11.
                saisie = new RattrapageSaisi(_lina.Inscription, await SeanceAsync(_mardi, Le(16, 11)));
                break;
            case "séance à plus de 8 semaines":
                saisie = new RattrapageSaisi(_lina.Inscription, await SeanceAsync(_mardi, Le(1, 2, 2028)));
                break;
            case "inscription inconnue":
                saisie = new RattrapageSaisi(Guid.NewGuid(), duMardi);
                break;
            default:
                await RattraperAsync(await SeanceAsync(_samedi, Le(4, 12)), new RattrapageSaisi(_lina.Inscription, duMardi));
                saisie = new RattrapageSaisi(_lina.Inscription, duMardi);
                break;
        }

        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/seances/{seance}/rattrapages", saisie), statut, message);
    }

    [Fact]
    public async Task Retirer_un_rattrapage()
    {
        var seance = await SeanceAsync(_samedi, Le(27, 11));
        var appel = await RattraperAsync(seance, new RattrapageSaisi(_lina.Inscription, await SeanceAsync(_mardi, Le(30, 11))));

        var reponse = await _secretaire.DeleteAsync($"/api/seances/{seance}/rattrapages/{appel.Rattrapages![0].RattrapageId}");

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Empty((await reponse.Content.ReadFromJsonAsync<AppelDeLaSeance>())!.Rattrapages!);
        Assert.Single(await PossiblesAsync(seance));
    }

    [Fact]
    public async Task Un_rattrapage_prevu_bloque_le_changement_du_calendrier()
    {
        // Lina viendra le samedi 04/12, à la place de son mardi 07/12.
        var accueil = await SeanceAsync(_samedi, Le(4, 12));
        await RattraperAsync(accueil, new RattrapageSaisi(_lina.Inscription, await SeanceAsync(_mardi, Le(7, 12))));

        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/seances/{accueil}/annulation", new AnnulationSaisie("Professeur absent")),
            HttpStatusCode.Conflict, "Un élève vient en rattrapage à cette séance : retirez son rattrapage d'abord");
        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/seances/{accueil}/deplacement", new NouvelleSeanceSaisie(Le(5, 12), "10:00", "12:00")),
            HttpStatusCode.Conflict, "Un élève vient en rattrapage à cette séance : retirez son rattrapage d'abord");
        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/groupes/{_mardi}/horaire",
                new ChangementDHoraire([new CreneauSaisi("Mercredi", "17:00", "19:00")], Le(1, 12))),
            HttpStatusCode.Conflict, "Un rattrapage est prévu sur une séance à recréer : retirez-le avant de changer l'horaire");
        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/groupes/{_samedi}/horaire",
                new ChangementDHoraire([new CreneauSaisi("Dimanche", "10:00", "12:00")], Le(1, 12))),
            HttpStatusCode.Conflict, "Un rattrapage est prévu sur une séance à recréer : retirez-le avant de changer l'horaire");
    }

    // ----- La feuille d'appel (SEA-08) -----

    [Fact]
    public async Task La_feuille_d_appel_d_un_mois()
    {
        await InscrireAsync("Sara", "Ayari", "97 222 333", "50 222 333", _samedi, Le(4, 12));
        // Une séance annulée n'a pas de colonne ; une séance supplémentaire en a une.
        Assert.Equal(HttpStatusCode.OK, (await _secretaire.PostAsJsonAsync($"/api/seances/{await SeanceAsync(_samedi, Le(20, 11))}/annulation",
            new AnnulationSaisie("Professeur absent"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _secretaire.PostAsJsonAsync($"/api/groupes/{_samedi}/seances",
            new NouvelleSeanceSaisie(Le(2, 12), "17:00", "19:00"))).StatusCode);

        var feuille = (await _secretaire.GetFromJsonAsync<FeuilleDAppel>($"/api/groupes/{_samedi}/feuille-d-appel?du=2027-11-13&au=2027-12-04"))!;

        Assert.Equal(("Centre Les Jasmins", "2027-2028", "Groupe Samedi 10h → 12h", "Mathématiques", "9ᵉ année de base", "Karim Ben Salah"),
            (feuille.Centre, feuille.Annee, feuille.Groupe, feuille.Matiere, feuille.Niveau, feuille.Professeur));
        Assert.Equal([Le(13, 11), Le(27, 11), Le(2, 12), Le(4, 12)], feuille.Seances.Select(s => s.Date));
        Assert.Equal([new EleveDeLaFeuille("Ayari", "Sara"), new EleveDeLaFeuille("Miled", "Adem")], feuille.Eleves);
        var texte = await _secretaire.GetStringAsync($"/api/groupes/{_samedi}/feuille-d-appel?du=2027-11-13&au=2027-12-04");
        Assert.DoesNotContain("telephone", texte, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", HttpStatusCode.BadRequest, "Choisissez la période")]
    [InlineData("?du=2027-12-04&au=2027-11-13", HttpStatusCode.BadRequest, "La période finit avant de commencer")]
    [InlineData("?du=2027-09-18&au=2028-01-31", HttpStatusCode.BadRequest, "La période fait au plus 3 mois")]
    public async Task Une_feuille_refusee(string periode, HttpStatusCode statut, string message)
    {
        await VerifierLeRefusAsync(await _secretaire.GetAsync($"/api/groupes/{_samedi}/feuille-d-appel{periode}"), statut, message);
    }

    [Fact]
    public async Task Un_autre_centre_ne_voit_rien()
    {
        var seance = await SeanceAsync(_samedi, Le(27, 11));
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));

        await VerifierLeRefusAsync(await rim.GetAsync($"/api/seances/{seance}/rattrapages/possibles"), HttpStatusCode.NotFound, "Séance introuvable");
        await VerifierLeRefusAsync(await rim.PostAsJsonAsync($"/api/seances/{seance}/rattrapages",
            new RattrapageSaisi(_lina.Inscription, await SeanceAsync(_mardi, Le(30, 11)))), HttpStatusCode.NotFound, "Séance introuvable");
        await VerifierLeRefusAsync(await rim.GetAsync($"/api/groupes/{_samedi}/feuille-d-appel?du=2027-11-13&au=2027-12-04"),
            HttpStatusCode.NotFound, "Groupe introuvable");
    }

    // ----- Outils -----

    private async Task<Guid> ProfesseurAsync(string prenom, string nom, string telephone) =>
        (await (await _secretaire.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur(prenom, nom, telephone, null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;

    private async Task<Guid> GroupeAsync(Guid professeur, string matiere, Guid niveau, CreneauSaisi creneau)
    {
        var reponse = await _secretaire.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(professeur, matiere, niveau, [creneau], 40m, Le(18, 9), null, ""));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
    }

    private async Task<Inscrit> InscrireAsync(string prenom, string nom, string parent, string telephone, Guid groupe, DateOnly debutLe, Guid? niveau = null)
    {
        var reponse = await _secretaire.PostAsJsonAsync("/api/eleves", new FicheSaisieDeLEleve(prenom, nom, niveau ?? Niveau("9ᵉ année de base"), false,
            parent, null, telephone, $"{prenom.ToLowerInvariant()}@exemple.tn", null));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var eleve = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        var inscription = await _secretaire.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(eleve, groupe, null, debutLe));
        Assert.Equal(HttpStatusCode.Created, inscription.StatusCode);
        return new Inscrit(eleve, (await inscription.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id);
    }

    private async Task<Guid> SeanceAsync(Guid groupe, DateOnly date)
    {
        var fiche = (await _secretaire.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{groupe}"))!;
        return fiche.Mois.SelectMany(m => m.Seances).Single(s => s.Date == date).Id;
    }

    private async Task<List<EleveRattrapable>> PossiblesAsync(Guid seance) =>
        (await _secretaire.GetFromJsonAsync<List<EleveRattrapable>>($"/api/seances/{seance}/rattrapages/possibles"))!;

    private async Task<AppelDeLaSeance> RattraperAsync(Guid seance, RattrapageSaisi saisie)
    {
        var reponse = await _secretaire.PostAsJsonAsync($"/api/seances/{seance}/rattrapages", saisie);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<AppelDeLaSeance>())!;
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
