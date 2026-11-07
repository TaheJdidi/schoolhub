using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
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
/// LST-03 et LST-04 : importer les élèves et les professeurs depuis le fichier modèle. Chaque test part d'un nouveau centre
/// « Jasmins » avec le groupe de Mathématiques de Karim Ben Salah, le samedi depuis le 18/09/2027 : le Mois 3 commence le
/// 13/11. Sami Chaabane (téléphone du parent 97 000 333) est déjà dans le centre. On est le samedi 27/11/2027 à 10h à Tunis.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class ImportsTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "imports";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private ApplicationDeTest _application = null!;
    private HttpClient _amira = null!;
    private Guid _maths;

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
        var niveau = (await _amira.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == "9ᵉ année de base").Id;
        var karim = (await (await _amira.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        _maths = (await (await _amira.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(karim, "Mathématiques", niveau,
            [new CreneauSaisi("Samedi", "10:00", "12:00")], 40m, new DateOnly(2027, 9, 18), null, ""))).Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync("/api/eleves", new FicheSaisieDeLEleve("Sami", "Chaabane", niveau, false,
            "97 000 333", null, "50 000 333", "sami@exemple.tn", null))).StatusCode);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private static readonly string[] Adem = ["Adem", "Miled", "9ᵉ année de base", "98 401 220", "non", "22 345 678", "adem@exemple.tn", "", "Collège", "Mathématiques · Groupe Samedi 10h → 12h"];

    private static readonly string[] Lina = ["Lina", "Gharbi", "6ᵉ année de base", "97 111 222", "non", "", "", "", "", ""];

    [Fact]
    public async Task Le_modele_des_eleves_a_ses_colonnes_et_les_niveaux()
    {
        var reponse = await _amira.GetAsync("/api/imports/eleves/modele");

        var classeur = new XLWorkbook(new MemoryStream(await reponse.Content.ReadAsByteArrayAsync()));
        Assert.Equal(ImportsDesListes.ColonnesDesEleves, classeur.Worksheet(1).Row(1).Cells(1, 10).Select(c => c.GetString()));
        Assert.Equal("1ʳᵉ année de base", classeur.Worksheet("Niveaux").Cell(2, 1).GetString());
        Assert.Equal(["Prénom", "Nom", "Téléphone", "Email (facultatif)"],
            new XLWorkbook(new MemoryStream(await (await _amira.GetAsync("/api/imports/professeurs/modele")).Content.ReadAsByteArrayAsync())).Worksheet(1).Row(1).Cells(1, 4).Select(c => c.GetString()));
    }

    [Fact]
    public async Task Importer_des_eleves_et_inscrire_ceux_qui_ont_un_groupe()
    {
        var reponse = await _amira.PostAsync("/api/imports/eleves", Fichier(ImportsDesListes.ColonnesDesEleves, Adem, Lina));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var resultat = (await reponse.Content.ReadFromJsonAsync<ResultatDImport>())!;
        Assert.Equal((true, 2, 1), (resultat.Importe, resultat.Importes, resultat.Inscrits));
        Assert.Empty(resultat.Erreurs);
        Assert.Equal(3, (await _amira.GetFromJsonAsync<List<EleveTrouve>>("/api/eleves"))!.Count);
        // Adem est inscrit à partir du Mois en cours du groupe : la première séance du Mois 3, le 13/11.
        var adem = (await _amira.GetFromJsonAsync<List<EleveTrouve>>("/api/eleves?recherche=miled"))!.Single();
        var inscriptions = (await _amira.GetFromJsonAsync<List<InscriptionDeLEleve>>($"/api/inscriptions?eleve={adem.Id}"))!;
        Assert.Equal(new DateOnly(2027, 11, 13), Assert.Single(inscriptions).PremiereSeance!.Date);
    }

    [Fact]
    public async Task Rien_n_est_importe_tant_qu_il_reste_une_erreur()
    {
        string[] sansNiveau = ["Rim", "Saidi", "Terminale", "97 000 555", "non", "", "", "", "", ""];
        string[] sansTelephone = ["Ali", "Ayari", "6ᵉ année de base", "", "non", "", "", "", "", ""];
        string[] groupeInconnu = ["Ines", "Trabelsi", "6ᵉ année de base", "97 000 666", "non", "", "", "", "", "Physique · Groupe Lundi"];

        var reponse = await _amira.PostAsync("/api/imports/eleves", Fichier(ImportsDesListes.ColonnesDesEleves, Lina, sansNiveau, sansTelephone, groupeInconnu));

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        var resultat = (await reponse.Content.ReadFromJsonAsync<ResultatDImport>())!;
        Assert.False(resultat.Importe);
        Assert.Equal(
            ["ligne 3 : niveau inconnu « Terminale »", "ligne 4 : le téléphone du parent est obligatoire", "ligne 5 : groupe introuvable « Physique · Groupe Lundi »"],
            resultat.Erreurs);
        // Lina, sans erreur, n'est pas importée non plus.
        Assert.Single((await _amira.GetFromJsonAsync<List<EleveTrouve>>("/api/eleves"))!);
    }

    [Fact]
    public async Task Les_doublons_possibles_sont_signales_sans_bloquer()
    {
        string[] memeParent = ["Yasmine", "Chaabane", "6ᵉ année de base", "97 000 333", "non", "", "", "", "", ""];
        string[] linaDeuxFois = ["Lina", "Gharbi", "6ᵉ année de base", "97 999 888", "non", "", "", "", "", ""];

        var resultat = (await (await _amira.PostAsync("/api/imports/eleves", Fichier(ImportsDesListes.ColonnesDesEleves, memeParent, Lina, linaDeuxFois)))
            .Content.ReadFromJsonAsync<ResultatDImport>())!;

        Assert.True(resultat.Importe);
        Assert.Equal(["ligne 2 : doublon possible avec Sami Chaabane, déjà dans le centre", "ligne 4 : doublon possible avec la ligne 3"], resultat.Doublons);
    }

    [Fact]
    public async Task Importer_des_professeurs()
    {
        string[] sonia = ["Sonia", "Mabrouk", "55 123 456", "sonia@exemple.tn"];
        string[] memeTelephone = ["Karim", "B.", "98 401 220", ""];

        var resultat = (await (await _amira.PostAsync("/api/imports/professeurs", Fichier(ImportsDesListes.ColonnesDesProfesseurs, sonia, memeTelephone)))
            .Content.ReadFromJsonAsync<ResultatDImport>())!;
        Assert.Equal((true, 2), (resultat.Importe, resultat.Importes));
        Assert.Equal(["ligne 3 : doublon possible avec Karim Ben Salah, même téléphone"], resultat.Doublons);

        string[] mauvaisTelephone = ["Ali", "Ben Ali", "12", ""];
        var refuse = await _amira.PostAsync("/api/imports/professeurs", Fichier(ImportsDesListes.ColonnesDesProfesseurs, mauvaisTelephone));
        Assert.Equal(HttpStatusCode.BadRequest, refuse.StatusCode);
        Assert.Single((await refuse.Content.ReadFromJsonAsync<ResultatDImport>())!.Erreurs);
    }

    [Fact]
    public async Task Un_fichier_qui_n_est_pas_le_modele_est_refuse()
    {
        var texte = new MultipartFormDataContent { { new ByteArrayContent("prénom;nom"u8.ToArray()), "fichier", "eleves.csv" } };

        var reponse = await _amira.PostAsync("/api/imports/eleves", texte);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Equal(["Envoyez le fichier modèle rempli (.xlsx)"], (await reponse.Content.ReadFromJsonAsync<ResultatDImport>())!.Erreurs);
        Assert.Equal(["Le fichier n'a aucune ligne à importer"],
            (await (await _amira.PostAsync("/api/imports/eleves", Fichier(ImportsDesListes.ColonnesDesEleves))).Content.ReadFromJsonAsync<ResultatDImport>())!.Erreurs);
    }

    private static MultipartFormDataContent Fichier(string[] colonnes, params string[][] lignes)
    {
        using var classeur = new XLWorkbook();
        var feuille = classeur.Worksheets.Add("Feuille");
        for (var c = 0; c < colonnes.Length; c++)
            feuille.Cell(1, c + 1).Value = colonnes[c];
        for (var l = 0; l < lignes.Length; l++)
            for (var c = 0; c < lignes[l].Length; c++)
                feuille.Cell(l + 2, c + 1).Value = lignes[l][c];
        using var flux = new MemoryStream();
        classeur.SaveAs(flux);
        var contenu = new ByteArrayContent(flux.ToArray());
        contenu.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent { { contenu, "fichier", "modele.xlsx" } };
    }
}
