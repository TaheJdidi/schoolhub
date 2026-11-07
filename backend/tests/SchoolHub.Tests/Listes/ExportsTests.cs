using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using SchoolHub.Modules.Achats;
using SchoolHub.Modules.Caisse;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Modules.Rh;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Listes;

/// <summary>
/// LST-02 : exporter une liste en Excel, avec le filtre en cours. Chaque test part d'un nouveau centre « Jasmins » où Adem
/// Miled (9ᵉ année) et Rania Trabelsi (8ᵉ année) sont inscrits en Mathématiques chez Karim Ben Salah. On est le samedi
/// 27/11/2027 à 10h à Tunis.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class ExportsTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "exports";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private ApplicationDeTest _application = null!;
    private HttpClient _hedi = null!;
    private HttpClient _amira = null!;
    private Guid _maths;
    private Guid _adem;
    private Guid _inscriptionDAdem;
    private Guid _rania;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        var jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(jasmins);
        _hedi = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire));
        Assert.Equal(HttpStatusCode.OK, (await _hedi.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", null, null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), null, null, null, null))).StatusCode);
        _amira = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(jasmins, "Amira", "Jlassi"));
        var niveaux = (await _amira.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        var neuvieme = niveaux.Single(n => n.Nom == "9ᵉ année de base").Id;
        var huitieme = niveaux.Single(n => n.Nom == "8ᵉ année de base").Id;
        var karim = (await (await _amira.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        _maths = (await (await _amira.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(karim, "Mathématiques", neuvieme,
            [new CreneauSaisi("Samedi", "10:00", "12:00")], 40m, new DateOnly(2027, 9, 18), null, ""))).Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        (_adem, _inscriptionDAdem) = await InscrireAsync("Adem", "Miled", "22 345 678", neuvieme);
        (_rania, _) = await InscrireAsync("Rania", "Trabelsi", "50 000 444", huitieme);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    [Fact]
    public async Task Les_eleves_avec_le_filtre_en_cours()
    {
        var feuille = await FeuilleAsync(_amira, "/api/exports/eleves");

        Assert.Equal(["Prénom", "Nom", "Niveau", "Téléphone du parent"], Ligne(feuille, 1).Take(4));
        Assert.Equal(["Adem", "Miled", "9ᵉ année de base", "97 000 111"], Ligne(feuille, 2).Take(4));
        Assert.Equal(3, feuille.LastRowUsed()!.RowNumber());

        // « élèves de 9ᵉ année de base » n'exporte qu'eux ; une recherche, ou une sélection, aussi.
        Assert.Equal(2, (await FeuilleAsync(_amira, "/api/exports/eleves?niveau=9ᵉ année de base")).LastRowUsed()!.RowNumber());
        Assert.Equal("Rania", Ligne(await FeuilleAsync(_amira, "/api/exports/eleves?recherche=trabelsi"), 2)[0]);
        Assert.Equal("Rania", Ligne(await FeuilleAsync(_amira, $"/api/exports/eleves?ids={_rania}"), 2)[0]);
    }

    [Fact]
    public async Task Les_montants_sont_des_nombres_et_les_dates_des_dates()
    {
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync($"/api/eleves/{_adem}/encaissement",
            new EncaissementSaisi([new SommeChoisie(_inscriptionDAdem, "Mois 1")]))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync("/api/achats",
            new AchatSaisi(new DateOnly(2027, 11, 20), "Ménage", "Service", 450.5m, null, false))).StatusCode);

        var recus = await FeuilleAsync(_amira, "/api/exports/recus");
        Assert.Equal(1, recus.Cell(2, 1).GetValue<int>());
        Assert.Equal(new DateTime(2027, 11, 27), recus.Cell(2, 2).GetDateTime());
        Assert.Equal(40m, recus.Cell(2, 4).GetValue<decimal>());

        var achats = await FeuilleAsync(_amira, "/api/exports/achats?mois=2027-11");
        Assert.Equal(("Ménage", "Service", 450.5m), (achats.Cell(2, 2).GetString(), achats.Cell(2, 3).GetString(), achats.Cell(2, 4).GetValue<decimal>()));
        Assert.Equal("0.000", achats.Cell(2, 4).Style.NumberFormat.Format);
    }

    [Fact]
    public async Task Un_groupe_avec_ses_eleves_et_les_professeurs()
    {
        var groupe = await FeuilleAsync(_amira, $"/api/exports/groupes/{_maths}");
        Assert.Equal(["Adem", "Rania"], [groupe.Cell(2, 1).GetString(), groupe.Cell(3, 1).GetString()]);
        Assert.Equal(HttpStatusCode.NotFound, (await _amira.GetAsync($"/api/exports/groupes/{Guid.NewGuid()}")).StatusCode);

        Assert.Equal(["Karim", "Ben Salah", "98 401 220"], Ligne(await FeuilleAsync(_amira, "/api/exports/professeurs"), 2).Take(3));
        Assert.Equal(1, (await FeuilleAsync(_amira, "/api/exports/bibliotheque")).LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task Seul_le_proprietaire_exporte_les_employes()
    {
        Assert.Equal(HttpStatusCode.Created, (await _hedi.PostAsJsonAsync("/api/employes", new EmployeSaisi("Fatma", "Ayari", null, null, "Agent de ménage", null, 450m))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.GetAsync("/api/exports/employes")).StatusCode);
        var employes = await FeuilleAsync(_hedi, "/api/exports/employes");
        Assert.Equal(("Fatma", 450m), (employes.Cell(2, 1).GetString(), employes.Cell(2, 5).GetValue<decimal>()));
    }

    // ----- Outils -----

    private static async Task<IXLWorksheet> FeuilleAsync(HttpClient client, string adresse)
    {
        var reponse = await client.GetAsync(adresse);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", reponse.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith("-2027-11-27.xlsx", reponse.Content.Headers.ContentDisposition!.FileNameStar ?? reponse.Content.Headers.ContentDisposition.FileName, StringComparison.Ordinal);
        var classeur = new XLWorkbook(new MemoryStream(await reponse.Content.ReadAsByteArrayAsync()));
        return classeur.Worksheet(1);
    }

    private static string[] Ligne(IXLWorksheet feuille, int numero) =>
        feuille.Row(numero).Cells(1, feuille.LastColumnUsed()!.ColumnNumber()).Select(c => c.GetString()).ToArray();

    private async Task<(Guid Eleve, Guid Inscription)> InscrireAsync(string prenom, string nom, string telephone, Guid niveau)
    {
        var reponse = await _amira.PostAsJsonAsync("/api/eleves", new FicheSaisieDeLEleve(prenom, nom, niveau, false, "97 000 111", null, telephone,
            $"{prenom.ToLowerInvariant()}@exemple.tn", null));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var eleve = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        var inscription = await _amira.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(eleve, _maths, null, new DateOnly(2027, 9, 18)));
        Assert.Equal(HttpStatusCode.Created, inscription.StatusCode);
        return (eleve, (await inscription.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id);
    }
}
