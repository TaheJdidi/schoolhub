using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Abonnements;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Abonnements;

/// <summary>EDI-05 : l'état de l'abonnement change tout seul, se force à la main, et un centre Suspendu est en lecture seule.</summary>
public sealed class SituationDeLAbonnementTests
{
    private static readonly DateOnly Fin = new(2027, 4, 4);

    [Theory]
    [InlineData("2027-04-04", EtatDeLAbonnement.Actif)]
    [InlineData("2027-04-05", EtatDeLAbonnement.EnRetard)]
    [InlineData("2027-04-19", EtatDeLAbonnement.EnRetard)]
    [InlineData("2027-04-20", EtatDeLAbonnement.Suspendu)]
    public void Paye_jusqu_au_4_avril_le_centre_est_actif_puis_en_retard_puis_suspendu_le_20(string jour, EtatDeLAbonnement attendu)
    {
        var situation = SituationDeLAbonnement.Calculer(Fin, null, null, DateOnly.Parse(jour));

        Assert.Equal(attendu, situation.Etat);
        if (attendu == EtatDeLAbonnement.EnRetard)
            Assert.Equal(new DateOnly(2027, 4, 19), situation.FinDeLaPeriodeDeGrace);
        if (attendu == EtatDeLAbonnement.Suspendu)
            Assert.Equal(new DateOnly(2027, 4, 20), situation.SuspenduLe);
    }

    [Fact]
    public void Sans_paiement_le_centre_est_en_attente_et_pas_en_lecture_seule()
    {
        var situation = SituationDeLAbonnement.Calculer(null, new DateOnly(2027, 1, 1));

        Assert.Equal(EtatDeLAbonnement.EnAttente, situation.Etat);
        Assert.False(situation.LectureSeule);
    }

    [Fact]
    public void Reactive_a_la_main_sans_paiement_le_centre_reste_actif_15_jours()
    {
        var reactiveLe = new DateOnly(2027, 5, 1);

        var pendant = SituationDeLAbonnement.Calculer(Fin, ForcageDeLEtat.Actif, reactiveLe, new DateOnly(2027, 5, 16));
        var apres = SituationDeLAbonnement.Calculer(Fin, ForcageDeLEtat.Actif, reactiveLe, new DateOnly(2027, 5, 17));

        Assert.Equal(EtatDeLAbonnement.Actif, pendant.Etat);
        Assert.Equal(new DateOnly(2027, 5, 16), pendant.ReactiveJusquAu);
        Assert.Equal(EtatDeLAbonnement.Suspendu, apres.Etat);
        Assert.Equal(new DateOnly(2027, 5, 17), apres.SuspenduLe);
    }

    [Fact]
    public void Suspendu_a_la_main_un_centre_paye_est_suspendu()
    {
        var situation = SituationDeLAbonnement.Calculer(Fin, ForcageDeLEtat.Suspendu, new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 2));

        Assert.Equal(EtatDeLAbonnement.Suspendu, situation.Etat);
        Assert.Equal(new DateOnly(2027, 3, 1), situation.SuspenduLe);
    }
}

[Collection(CollectionPostgreSql.Nom)]
public sealed class EtatDeLAbonnementTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private ApplicationDeTest _application = null!;
    private BackOfficeDeTest _backOffice = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, "abonnements_etat");
        await _application.InitialiserLaBaseAsync();
        _backOffice = new BackOfficeDeTest(_application);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private DateOnly Aujourdhui => DateDeTunis.Aujourdhui(_application.Horloge);

    [Fact]
    public async Task Suspendu_a_la_main_le_centre_lit_mais_n_ecrit_plus_puis_un_paiement_le_rend_actif()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);
        await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(Aujourdhui, Aujourdhui.AddMonths(1)));
        var hedi = await _backOffice.ProprietaireInscritAsync(editeur, fiche.Id);

        var sansMotif = await editeur.PostAsJsonAsync($"/api/editeur/centres/{fiche.Id}/suspension", new Motif(" "));
        Assert.Equal(HttpStatusCode.BadRequest, sansMotif.StatusCode);

        var suspension = await editeur.PostAsJsonAsync($"/api/editeur/centres/{fiche.Id}/suspension", new Motif("Chèque sans provision"));
        var suspendu = (await suspension.Content.ReadFromJsonAsync<FicheDuCentre>())!;
        Assert.Equal("Suspendu", suspendu.Etat);
        Assert.Equal("Chèque sans provision", suspendu.MotifDuForcage);

        var abonnement = await hedi.GetFromJsonAsync<AbonnementDuCentre>("/api/abonnement");
        Assert.True(abonnement!.LectureSeule);

        // La lecture continue, l'écriture est refusée avec un message clair.
        Assert.Equal(HttpStatusCode.OK, (await hedi.GetAsync("/api/eleves")).StatusCode);
        var ecriture = await hedi.PostAsJsonAsync("/api/professeurs", new { Prenom = "Karim", Nom = "Ben Salah" });
        Assert.Equal(HttpStatusCode.Forbidden, ecriture.StatusCode);
        Assert.Equal(LectureSeule.Message, (await ecriture.Content.ReadFromJsonAsync<Avis>())!.Message);

        // Se déconnecter reste permis.
        Assert.Equal(HttpStatusCode.NoContent, (await hedi.PostAsync("/api/deconnexion", null)).StatusCode);

        var paiement = await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(Aujourdhui, Aujourdhui.AddMonths(2), facture: "F-2027-004"));
        Assert.Equal("Actif", paiement.Fiche.Etat);
        Assert.Null(paiement.Fiche.Forcage);
    }

    [Fact]
    public async Task Un_centre_impaye_reactive_a_la_main_est_actif_15_jours()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);
        var finPayee = Aujourdhui.AddDays(-(SituationDeLAbonnement.JoursDeGrace + 5));
        await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(finPayee.AddMonths(-1), finPayee));
        Assert.Equal("Suspendu", (await BackOfficeDeTest.FicheAsync(editeur, fiche.Id)).Etat);

        var pasSuspendu = await editeur.PostAsJsonAsync($"/api/editeur/centres/{fiche.Id}/suspension", new Motif("Déjà"));
        Assert.Equal(BackOfficeDesCentres.DejaSuspendu, (await pasSuspendu.Content.ReadFromJsonAsync<Avis>())!.Message);

        var reponse = await editeur.PostAsJsonAsync($"/api/editeur/centres/{fiche.Id}/reactivation", new Motif("Paiement promis le 10"));
        var reactive = (await reponse.Content.ReadFromJsonAsync<FicheDuCentre>())!;

        Assert.Equal("Actif", reactive.Etat);
        Assert.Equal(Aujourdhui.AddDays(SituationDeLAbonnement.JoursDeReactivation), reactive.ReactiveJusquAu);

        var journal = await editeur.GetFromJsonAsync<List<LigneDuJournalVue>>("/api/editeur/journal");
        Assert.Contains(journal!, l => l.Action == "CentreReactive" && l.Detail.Contains("Paiement promis le 10"));
    }

    [Fact]
    public async Task En_retard_le_centre_fonctionne_et_voit_la_fin_de_sa_periode_de_grace()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);
        var finPayee = Aujourdhui.AddDays(-3);
        await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(finPayee.AddMonths(-1), finPayee));
        var hedi = await _backOffice.ProprietaireInscritAsync(editeur, fiche.Id);

        var abonnement = await hedi.GetFromJsonAsync<AbonnementDuCentre>("/api/abonnement");

        Assert.Equal("EnRetard", abonnement!.Etat);
        Assert.False(abonnement.LectureSeule);
        Assert.Equal(finPayee.AddDays(SituationDeLAbonnement.JoursDeGrace), abonnement.FinDeLaPeriodeDeGrace);
    }
}
