using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Modules.Abonnements;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Abonnements;

/// <summary>EDI-07 : l'Éditeur voit un centre seulement pendant l'accès support que son propriétaire donne, 24 h au plus.</summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class AccesSupportTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private ApplicationDeTest _application = null!;
    private BackOfficeDeTest _backOffice = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, "abonnements_support");
        await _application.InitialiserLaBaseAsync();
        _backOffice = new BackOfficeDeTest(_application);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private DateOnly Aujourdhui => DateDeTunis.Aujourdhui(_application.Horloge);

    private async Task<(HttpClient Editeur, FicheDuCentre Fiche, HttpClient Hedi)> CentreInscritAsync()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);
        await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(Aujourdhui, Aujourdhui.AddMonths(1)));
        var hedi = await _backOffice.ProprietaireInscritAsync(editeur, fiche.Id);
        return (editeur, fiche, hedi);
    }

    [Fact]
    public async Task Sans_autorisation_l_editeur_ne_voit_rien_du_centre()
    {
        var (editeur, fiche, _) = await CentreInscritAsync();

        var reponse = await editeur.GetAsync($"/api/editeur/centres/{fiche.Id}/support/accueil");

        Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);
        Assert.Equal(AccesSupport.SansAutorisation, (await reponse.Content.ReadFromJsonAsync<Avis>())!.Message);
    }

    [Fact]
    public async Task Pendant_24_heures_l_editeur_voit_le_centre_et_chaque_page_va_au_journal()
    {
        var (editeur, fiche, hedi) = await CentreInscritAsync();

        var autorise = await hedi.PostAsync("/api/abonnement/support", null);
        Assert.Equal(HttpStatusCode.OK, autorise.StatusCode);
        var jusquAu = (await autorise.Content.ReadFromJsonAsync<AbonnementDuCentre>())!.SupportJusquAu;
        // PostgreSQL garde le moment à la microseconde.
        Assert.Equal(_application.Horloge.GetUtcNow().AddHours(24), jusquAu!.Value, TimeSpan.FromMilliseconds(1));
        Assert.Equal(jusquAu, (await BackOfficeDeTest.FicheAsync(editeur, fiche.Id)).SupportJusquAu);

        var accueil = await editeur.GetFromJsonAsync<CentreEnAccesSupport>($"/api/editeur/centres/{fiche.Id}/support/accueil");
        Assert.Equal(fiche.Nom, accueil!.Nom);
        Assert.Equal((0, 0, 0), (accueil.Eleves, accueil.GroupesOuverts, accueil.Professeurs));
        var comptes = await editeur.GetFromJsonAsync<List<CompteEnAccesSupport>>($"/api/editeur/centres/{fiche.Id}/support/comptes");
        Assert.Equal("Hedi", Assert.Single(comptes!).Prenom);

        // Le reste du centre lui reste fermé.
        Assert.Equal(HttpStatusCode.Forbidden, (await editeur.GetAsync("/api/eleves")).StatusCode);
        var lignes = await _application.JournalAsync(await CentreAsync(fiche.Id));
        Assert.Contains(lignes, l => l.Action == ActionDuJournal.AccesSupportAutorise);
        Assert.Equal(2, lignes.Count(l => l.Action == ActionDuJournal.AccesSupportConsultation));

        // Après 24 heures, plus rien.
        _application.Horloge.Advance(TimeSpan.FromHours(24));
        editeur = await _backOffice.EditeurAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await editeur.GetAsync($"/api/editeur/centres/{fiche.Id}/support/accueil")).StatusCode);
    }

    [Fact]
    public async Task Le_proprietaire_retire_l_autorisation_avant_la_fin()
    {
        var (editeur, fiche, hedi) = await CentreInscritAsync();
        await hedi.PostAsync("/api/abonnement/support", null);

        var retrait = await hedi.DeleteAsync("/api/abonnement/support");

        Assert.Null((await retrait.Content.ReadFromJsonAsync<AbonnementDuCentre>())!.SupportJusquAu);
        Assert.Equal(HttpStatusCode.Forbidden, (await editeur.GetAsync($"/api/editeur/centres/{fiche.Id}/support/comptes")).StatusCode);
    }

    [Fact]
    public async Task Un_centre_suspendu_peut_encore_demander_de_l_aide_mais_pas_une_secretaire()
    {
        var (editeur, fiche, hedi) = await CentreInscritAsync();
        await editeur.PostAsJsonAsync($"/api/editeur/centres/{fiche.Id}/suspension", new Motif("Impayé"));

        Assert.Equal(HttpStatusCode.OK, (await hedi.PostAsync("/api/abonnement/support", null)).StatusCode);

        var amira = await _application.CreerCompteAsync(await CentreAsync(fiche.Id), "Amira", "Jlassi");
        var secretaire = await _application.ClientConnecteAsync(amira);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretaire.DeleteAsync("/api/abonnement/support")).StatusCode);
    }

    private Task<Centre> CentreAsync(Guid id) =>
        _application.DansUnScopeAsync(services => services.GetRequiredService<SchoolHubDbContext>().Centres.SingleAsync(c => c.Id == id));
}
