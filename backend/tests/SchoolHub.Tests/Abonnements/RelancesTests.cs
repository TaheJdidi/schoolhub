using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Modules.Abonnements;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Abonnements;

/// <summary>EDI-06 : la liste « à relancer », les emails automatiques à J-7 et le jour de la fin, et la relance WhatsApp.</summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class RelancesTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private ApplicationDeTest _application = null!;
    private BackOfficeDeTest _backOffice = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, "abonnements_relances");
        await _application.InitialiserLaBaseAsync();
        _backOffice = new BackOfficeDeTest(_application);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private DateOnly Aujourdhui => DateDeTunis.Aujourdhui(_application.Horloge);

    private Task<int> RelancerAsync() =>
        _application.DansUnScopeAsync(services => services.GetRequiredService<Relances>().EnvoyerAsync());

    [Fact]
    public async Task La_relance_part_une_fois_a_J_moins_7_puis_une_fois_le_jour_de_la_fin()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);
        var fin = Aujourdhui.AddDays(5);
        await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(fin.AddMonths(-1), fin));
        _application.Emails.Envoyes.Clear();

        Assert.True(await RelancerAsync() >= 1);
        var relance = Assert.Single(_application.Emails.Envoyes, e => e.Destinataire == fiche.Proprietaire!.Email);
        Assert.Equal(MessagesAuProprietaire.FinDeLAbonnement(fin), relance.Sujet);
        Assert.Contains("90,000 TND par mois", relance.Texte);

        // Un second passage le même jour n'envoie rien de plus.
        await RelancerAsync();
        Assert.Single(_application.Emails.Envoyes, e => e.Destinataire == fiche.Proprietaire!.Email);

        // Le jour de la fin, la seconde relance part.
        _application.Horloge.Advance(TimeSpan.FromDays(5));
        await RelancerAsync();
        var duJour = _application.Emails.Envoyes.Where(e => e.Destinataire == fiche.Proprietaire!.Email).ToList();
        Assert.Equal(2, duJour.Count);
        Assert.Contains("aujourd'hui", duJour[1].Sujet);

        var apres = await BackOfficeDeTest.FicheAsync(await _backOffice.EditeurAsync(), fiche.Id);
        Assert.Equal(["RelanceDeLaFin", "RelanceAvantLaFin"], apres.Relances.Select(r => r.Objet));
    }

    [Fact]
    public async Task Une_relance_qui_ne_part_pas_repart_au_passage_suivant()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);
        var fin = Aujourdhui.AddDays(3);
        await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(fin.AddMonths(-1), fin));
        _application.Emails.Envoyes.Clear();

        _application.Emails.Echouer = true;
        await RelancerAsync();
        _application.Emails.Echouer = false;
        Assert.Empty((await BackOfficeDeTest.FicheAsync(editeur, fiche.Id)).Relances);

        await RelancerAsync();
        Assert.Single(_application.Emails.Envoyes, e => e.Destinataire == fiche.Proprietaire!.Email);
    }

    [Fact]
    public async Task La_liste_a_relancer_montre_le_centre_et_la_relance_WhatsApp_s_ouvre_ecrite()
    {
        var editeur = await _backOffice.EditeurAsync();
        var proche = await BackOfficeDeTest.CreerAsync(editeur);
        var fin = Aujourdhui.AddDays(6);
        await BackOfficeDeTest.PayerAsync(editeur, proche.Id, BackOfficeDeTest.Paiement(fin.AddMonths(-1), fin));
        var lointain = await BackOfficeDeTest.CreerAsync(editeur);
        await BackOfficeDeTest.PayerAsync(editeur, lointain.Id, BackOfficeDeTest.Paiement(Aujourdhui, Aujourdhui.AddMonths(3)));

        var liste = await editeur.GetFromJsonAsync<List<LigneDeCentre>>("/api/editeur/centres?filtre=a-relancer");
        Assert.Contains(liste!, l => l.Id == proche.Id && l.TelephoneConnu);
        Assert.DoesNotContain(liste!, l => l.Id == lointain.Id);

        var reponse = await editeur.PostAsync($"/api/editeur/centres/{proche.Id}/relance/whatsapp", null);
        var lien = await reponse.Content.ReadFromJsonAsync<LienWhatsApp>();
        Assert.StartsWith("https://wa.me/21698401220?text=Bonjour%20Hedi", lien!.Lien);
        Assert.Equal("WhatsApp", Assert.Single(lien.Fiche.Relances).Canal);

        // Chez le propriétaire, le bandeau « Votre abonnement se termine le … ».
        await BackOfficeDeTest.PayerAsync(editeur, proche.Id, BackOfficeDeTest.Paiement(fin.AddMonths(-1), fin, facture: "F-2"));
        var hedi = await _backOffice.ProprietaireInscritAsync(editeur, proche.Id);
        Assert.True((await hedi.GetFromJsonAsync<AbonnementDuCentre>("/api/abonnement"))!.FinProche);
    }

    [Fact]
    public async Task Un_centre_en_attente_n_a_rien_a_relancer()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);

        var reponse = await editeur.PostAsync($"/api/editeur/centres/{fiche.Id}/relance/whatsapp", null);

        Assert.Equal(HttpStatusCode.Conflict, reponse.StatusCode);
        Assert.Equal(BackOfficeDesCentres.RienARelancer, (await reponse.Content.ReadFromJsonAsync<Avis>())!.Message);
    }
}
