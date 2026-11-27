using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Modules.Abonnements;
using SchoolHub.Modules.Bibliotheque;
using SchoolHub.Modules.Demandes;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Abonnements;

/// <summary>
/// EDI-08 : pendant 12 mois après la suspension, le propriétaire télécharge ses données ; ensuite, l'Éditeur supprime le centre
/// en retapant son nom. Le centre des tests a de vraies données : un professeur, un groupe, un élève inscrit, une entrée à la
/// bibliothèque avec son reçu, et son journal. On part du samedi 27/11/2027.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class ExportEtSuppressionTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private static readonly DateTimeOffset Depart = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private ApplicationDeTest _application = null!;
    private BackOfficeDeTest _backOffice = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, "abonnements_suppression");
        _application.Horloge.SetUtcNow(Depart);
        await _application.InitialiserLaBaseAsync();
        _backOffice = new BackOfficeDeTest(_application);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    /// <summary>Un centre payé jusqu'au 30/11/2027 (donc Suspendu le 16/12/2027), rempli par son propriétaire Hedi.</summary>
    private async Task<FicheDuCentre> CentreRempliAsync()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);
        await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(new DateOnly(2027, 9, 1), new DateOnly(2027, 11, 30)));
        var hedi = await _backOffice.ProprietaireInscritAsync(editeur, fiche.Id);

        Assert.Equal(HttpStatusCode.OK, (await hedi.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            fiche.Nom, null, null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), 20m, 5m, 2, null))).StatusCode);
        var niveau = (await hedi.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == "9ᵉ année de base").Id;
        var karim = (await (await hedi.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        var groupe = (await (await hedi.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(karim, "Mathématiques", niveau,
            [new CreneauSaisi("Samedi", "10:00", "12:00")], 40m, new DateOnly(2027, 9, 18), null, ""))).Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        var adem = (await (await hedi.PostAsJsonAsync("/api/eleves",
                new FicheSaisieDeLEleve("Adem", "Miled", niveau, false, "98 401 220", null, "22 345 678", "adem@exemple.tn", null)))
            .Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        Assert.Equal(HttpStatusCode.Created,
            (await hedi.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(adem, groupe, null, new DateOnly(2027, 11, 13)))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await hedi.PostAsJsonAsync("/api/bibliotheque/entrees", new EntreeSaisie(adem, null))).StatusCode);
        return fiche;
    }

    /// <summary>Hedi se reconnecte, après un saut de l'horloge qui a fait tomber sa session.</summary>
    private async Task<HttpClient> HediAsync(FicheDuCentre fiche)
    {
        var client = _application.Client();
        var reponse = await BackOfficeDeTest.MotDePasseAsync(client, fiche.Proprietaire!.Email, BackOfficeDeTest.MotDePasseDeHedi);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return client;
    }

    [Fact]
    public async Task Suspendu_le_centre_exporte_ses_donnees_puis_apres_12_mois_l_editeur_le_supprime()
    {
        var fiche = await CentreRempliAsync();

        // Le 01/03/2028, le centre est Suspendu depuis le 16/12/2027 : son propriétaire télécharge ses données.
        _application.Horloge.SetUtcNow(new DateTimeOffset(2028, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var hedi = await HediAsync(fiche);
        var abonnement = await hedi.GetFromJsonAsync<AbonnementDuCentre>("/api/abonnement");
        Assert.True(abonnement!.ExportPossible);
        Assert.Equal(new DateOnly(2028, 12, 15), abonnement.ExportPossibleJusquAu);

        var export = await hedi.GetAsync("/api/abonnement/export");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        using (var classeur = new XLWorkbook(await export.Content.ReadAsStreamAsync()))
        {
            Assert.Equal(ExportDuCentre.Feuilles, classeur.Worksheets.Select(f => f.Name));
            Assert.Equal("Adem", classeur.Worksheet("Élèves").Cell(2, 1).GetString());
            Assert.Equal("Karim", classeur.Worksheet("Professeurs").Cell(2, 1).GetString());
            Assert.False(classeur.Worksheet("Reçus").Cell(2, 1).IsEmpty());
        }

        // Trop tôt pour supprimer.
        var editeur = await _backOffice.EditeurAsync();
        var tropTot = await editeur.PostAsJsonAsync($"/api/editeur/centres/{fiche.Id}/suppression", new ConfirmationDeSuppression(fiche.Nom));
        Assert.Equal(HttpStatusCode.Conflict, tropTot.StatusCode);
        Assert.Contains("16/12/2028", (await tropTot.Content.ReadFromJsonAsync<Avis>())!.Message);

        // Le 10/01/2029, plus de 12 mois après : le centre est dans la liste « à supprimer ».
        _application.Horloge.SetUtcNow(new DateTimeOffset(2029, 1, 10, 9, 0, 0, TimeSpan.Zero));
        editeur = await _backOffice.EditeurAsync();
        Assert.Contains((await editeur.GetFromJsonAsync<List<LigneDeCentre>>("/api/editeur/centres?filtre=a-supprimer"))!, l => l.Id == fiche.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await (await HediAsync(fiche)).GetAsync("/api/abonnement/export")).StatusCode);

        var mauvaisNom = await editeur.PostAsJsonAsync($"/api/editeur/centres/{fiche.Id}/suppression", new ConfirmationDeSuppression("Centre"));
        Assert.Equal(BackOfficeDesCentres.NomARetaper, (await mauvaisNom.Content.ReadFromJsonAsync<Avis>())!.Message);

        var suppression = await editeur.PostAsJsonAsync($"/api/editeur/centres/{fiche.Id}/suppression", new ConfirmationDeSuppression(fiche.Nom));
        Assert.Equal(HttpStatusCode.OK, suppression.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await editeur.GetAsync($"/api/editeur/centres/{fiche.Id}")).StatusCode);
        Assert.False(await _application.DansUnScopeAsync(services => services.GetRequiredService<SchoolHubDbContext>().Users
            .AnyAsync(c => c.CentreId == fiche.Id)));
        var journal = await editeur.GetFromJsonAsync<List<LigneDuJournalVue>>("/api/editeur/journal");
        Assert.Contains(journal!, l => l.Action == "CentreSupprime" && l.Centre == fiche.Nom);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await BackOfficeDeTest.MotDePasseAsync(_application.Client(), fiche.Proprietaire!.Email, BackOfficeDeTest.MotDePasseDeHedi)).StatusCode);
    }

    [Fact]
    public async Task Le_journal_d_un_centre_ne_s_efface_toujours_pas_hors_de_sa_suppression()
    {
        var fiche = await CentreRempliAsync();

        var erreur = await Assert.ThrowsAnyAsync<Exception>(() => _application.DansUnScopeAsync(services =>
            services.GetRequiredService<SchoolHubDbContext>().Database
                .ExecuteSqlAsync($"DELETE FROM journal WHERE centre_id = {fiche.Id}")));
        Assert.Contains("Le journal ne se modifie pas", erreur.Message);
    }

    [Fact]
    public async Task Le_formulaire_public_d_un_centre_suspendu_est_ferme()
    {
        var editeur = await _backOffice.EditeurAsync();
        var demande = BackOfficeDeTest.Jasmins();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur, demande);
        Assert.Equal(HttpStatusCode.OK, (await _application.Client().GetAsync($"/api/public/inscription/{demande.Code}")).StatusCode);

        await editeur.PostAsJsonAsync($"/api/editeur/centres/{fiche.Id}/suspension", new Motif("Impayé"));

        var reponse = await _application.Client().GetAsync($"/api/public/inscription/{demande.Code}");
        Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);
        Assert.Equal(FormulairePublic.InscriptionsFermees, (await reponse.Content.ReadFromJsonAsync<Avis>())!.Message);
    }

    [Fact]
    public void Chaque_donnee_d_un_centre_part_avant_celles_qu_elle_designe()
    {
        var modele = _application.Services.CreateScope().ServiceProvider.GetRequiredService<SchoolHubDbContext>().Model;
        var ordre = SuppressionDUnCentre.OrdreDeSuppression(modele).Select(t => t.GetTableName()).ToList();

        Assert.Contains("journal", ordre);
        foreach (var type in SuppressionDUnCentre.OrdreDeSuppression(modele))
            foreach (var cle in type.GetForeignKeys())
                if (ordre.Contains(cle.PrincipalEntityType.GetTableName()) && cle.PrincipalEntityType.GetTableName() != type.GetTableName())
                    Assert.True(
                        ordre.IndexOf(type.GetTableName()) < ordre.IndexOf(cle.PrincipalEntityType.GetTableName()),
                        $"{type.GetTableName()} désigne {cle.PrincipalEntityType.GetTableName()} et doit partir avant");
    }
}
