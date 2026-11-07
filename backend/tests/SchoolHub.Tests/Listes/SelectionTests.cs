using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Listes;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Listes;

/// <summary>
/// LST-05 : agir sur plusieurs élèves sélectionnés. Chaque test part d'un nouveau centre « Jasmins » avec le groupe de
/// Mathématiques de Karim Ben Salah, le samedi depuis le 18/09/2027 (Mois 3 depuis le 13/11), où Adem Miled est déjà
/// inscrit ; Lina Gharbi (6ᵉ année, sans email ni téléphone) et Sami Chaabane (9ᵉ année) ne le sont pas. On est le samedi
/// 27/11/2027 à 10h à Tunis.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class SelectionTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "selection";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private ApplicationDeTest _application = null!;
    private HttpClient _amira = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _maths;
    private Guid _adem;
    private Guid _lina;
    private Guid _sami;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        var jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(jasmins);
        var hedi = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire));
        Assert.Equal(HttpStatusCode.OK, (await hedi.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", null, null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), null, null, null, null))).StatusCode);
        _amira = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(jasmins, "Amira", "Jlassi"));
        _niveaux = (await _amira.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        var karim = (await (await _amira.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        _maths = (await (await _amira.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(karim, "Mathématiques", Niveau("9ᵉ année de base"),
            [new CreneauSaisi("Samedi", "10:00", "12:00")], 40m, new DateOnly(2027, 9, 18), null, ""))).Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        _adem = await EleveAsync(new FicheSaisieDeLEleve("Adem", "Miled", Niveau("9ᵉ année de base"), false, "98 401 220", null, "22 345 678", "adem@exemple.tn", null));
        _lina = await EleveAsync(new FicheSaisieDeLEleve("Lina", "Gharbi", Niveau("6ᵉ année de base"), false, "97 111 222", null, null, null, null));
        _sami = await EleveAsync(new FicheSaisieDeLEleve("Sami", "Chaabane", Niveau("9ᵉ année de base"), false, "97 000 333", null, "50 000 333", "sami@exemple.tn", null));
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(_adem, _maths, null, new DateOnly(2027, 11, 13)))).StatusCode);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau(string nom) => _niveaux.Single(n => n.Nom == nom).Id;

    [Fact]
    public async Task Inscrire_la_selection_dans_un_groupe_et_lister_a_part_les_deja_inscrits()
    {
        var resultat = await PosterAsync("/api/selection/inscription", new InscriptionDeLaSelection([_adem, _lina, _sami], _maths, null, null));

        // Par nom : Chaabane, Gharbi ; Miled est déjà inscrit.
        Assert.Equal(["Sami Chaabane", "Lina Gharbi"], resultat.Reussis);
        Assert.Equal(["Adem Miled"], resultat.DejaInscrits);
        Assert.Empty(resultat.Refuses);
        // À partir du Mois en cours du groupe : le 13/11.
        var inscription = Assert.Single((await _amira.GetFromJsonAsync<List<InscriptionDeLEleve>>($"/api/inscriptions?eleve={_sami}"))!);
        Assert.Equal(new DateOnly(2027, 11, 13), inscription.PremiereSeance!.Date);
    }

    [Fact]
    public async Task Changer_le_niveau_de_la_selection_eleve_par_eleve()
    {
        var resultat = await PosterAsync("/api/selection/niveau", new NiveauDeLaSelection([_lina, _sami], Niveau("1ʳᵉ année secondaire")));

        // Sami a ses contacts ; Lina n'a ni téléphone ni email, obligatoires à partir de la 7ᵉ : elle reste en 6ᵉ.
        Assert.Equal(["Sami Chaabane"], resultat.Reussis);
        var lina = Assert.Single(resultat.Refuses);
        Assert.Equal(("Lina Gharbi", "Le téléphone de l'élève est obligatoire à partir de la 7ᵉ année de base"), (lina.Nom, lina.Raison));
        Assert.Equal("1ʳᵉ année secondaire", (await _amira.GetFromJsonAsync<FicheDeLEleve>($"/api/eleves/{_sami}"))!.Niveau.Nom);
    }

    [Fact]
    public async Task Les_refus()
    {
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync("/api/selection/inscription", new InscriptionDeLaSelection([], _maths, null, null)),
            HttpStatusCode.BadRequest, "Sélectionnez au moins un élève");
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync("/api/selection/inscription", new InscriptionDeLaSelection([_lina], null, null, null)),
            HttpStatusCode.BadRequest, "Choisissez le groupe");
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync("/api/selection/inscription", new InscriptionDeLaSelection([_lina], Guid.NewGuid(), null, null)),
            HttpStatusCode.NotFound, "Groupe introuvable");
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync("/api/selection/niveau", new NiveauDeLaSelection([_lina], null)),
            HttpStatusCode.BadRequest, "Choisissez le niveau");
    }

    private async Task<ResultatDeLaSelection> PosterAsync<T>(string adresse, T saisie)
    {
        var reponse = await _amira.PostAsJsonAsync(adresse, saisie);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<ResultatDeLaSelection>())!;
    }

    private async Task<Guid> EleveAsync(FicheSaisieDeLEleve fiche)
    {
        var reponse = await _amira.PostAsJsonAsync("/api/eleves", fiche);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
