using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolHub.Modules.Achats;
using SchoolHub.Modules.Caisse;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Journal;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Achats;

/// <summary>
/// RHA-04 à RHA-06 : les achats. Chaque test part d'un nouveau centre « Jasmins », avec le propriétaire Hedi Ben Salah et
/// deux secrétaires, Amira Jlassi et Sonia Mabrouk. On est le samedi 27/11/2027 à 10h à Tunis.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class AchatsTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "achats";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Le27 = new(2027, 11, 27);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private Compte _compteDAmira = null!;
    private HttpClient _hedi = null!;
    private HttpClient _amira = null!;
    private HttpClient _sonia = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        _hedi = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire));
        _compteDAmira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        _amira = await _application.ClientConnecteAsync(_compteDAmira);
        _sonia = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Sonia", "Mabrouk"));
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private static AchatSaisi Ramettes(bool depuisLaCaisse = true) => new(Le27, "Ramettes de papier", "Materiel", 45m, "Librairie du Centre", depuisLaCaisse);

    // ----- Saisir (RHA-04) -----

    [Fact]
    public async Task Une_sortie_de_caisse_baisse_les_especes_attendues_et_va_au_journal()
    {
        var achat = await AjouterAsync(_amira, Ramettes());

        Assert.Equal(("Ramettes de papier", "Materiel", 45m, "Librairie du Centre", true, "Amira Jlassi", true),
            (achat.Libelle, achat.Categorie, achat.Montant, achat.Fournisseur, achat.PayeDepuisLaCaisse, achat.Auteur, achat.Modifiable));
        var caisse = Assert.Single((await _amira.GetFromJsonAsync<MaCaisse>("/api/caisse"))!.Ouvertes);
        Assert.Equal((0m, 45m, -45m), (caisse.Encaisse, caisse.Sorties, caisse.Attendu));
        var ligne = Assert.Single(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.SortieDeCaisse);
        Assert.Equal(("Ramettes de papier · Matériel", null, "Sortie, 45,000 TND"), (ligne.Detail, ligne.Avant, ligne.Apres));

        // Une caisse qui n'a qu'une sortie se clôture aussi ; la sortie y est rangée.
        var cloture = await _amira.PostAsJsonAsync("/api/caisse/cloture", new ClotureSaisie(Le27, 0m));
        Assert.Equal(HttpStatusCode.Created, cloture.StatusCode);
        Assert.Equal((45m, -45m, 45m), (await cloture.Content.ReadFromJsonAsync<ClotureDecrite>()) is { } c ? (c.Sorties, c.Attendu, c.Ecart) : default);
        Assert.Equal(0m, Assert.Single((await _amira.GetFromJsonAsync<MaCaisse>("/api/caisse"))!.Ouvertes).Sorties);
    }

    [Fact]
    public async Task Un_achat_hors_caisse_ne_touche_pas_la_caisse_ni_le_journal()
    {
        await AjouterAsync(_amira, new AchatSaisi(new DateOnly(2027, 11, 2), "Ménage de novembre", "Service", 450m, null, false));

        Assert.Equal(0m, Assert.Single((await _amira.GetFromJsonAsync<MaCaisse>("/api/caisse"))!.Ouvertes).Sorties);
        Assert.DoesNotContain(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.SortieDeCaisse);
    }

    [Theory]
    [InlineData(null, "Papier", "Materiel", "45", HttpStatusCode.BadRequest, "Choisissez la date")]
    [InlineData("2027-11-27", " ", "Materiel", "45", HttpStatusCode.BadRequest, "Écrivez le libellé")]
    [InlineData("2027-11-27", "Papier", "Fournitures", "45", HttpStatusCode.BadRequest, "Choisissez la catégorie")]
    [InlineData("2027-11-27", "Papier", "1", "45", HttpStatusCode.BadRequest, "Choisissez la catégorie")]
    [InlineData("2027-11-27", "Papier", "Materiel", "0", HttpStatusCode.BadRequest, "Le montant est positif, au millime près")]
    [InlineData("2027-11-27", "Papier", "Materiel", "45.0001", HttpStatusCode.BadRequest, "Le montant est positif, au millime près")]
    [InlineData("2027-11-26", "Papier", "Materiel", "45", HttpStatusCode.BadRequest, "Une sortie de caisse se note le jour même")]
    public async Task Un_achat_refuse(string? jour, string libelle, string categorie, string montant, HttpStatusCode statut, string message)
    {
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync("/api/achats", new AchatSaisi(
                jour is null ? null : DateOnly.Parse(jour, System.Globalization.CultureInfo.InvariantCulture), libelle, categorie,
                decimal.Parse(montant, System.Globalization.CultureInfo.InvariantCulture), null, true)),
            statut, message);
    }

    // ----- Modifier, supprimer -----

    [Fact]
    public async Task L_auteur_change_son_achat_le_jour_meme_avant_sa_cloture()
    {
        var achat = await AjouterAsync(_amira, Ramettes());

        var modifie = await _amira.PutAsJsonAsync($"/api/achats/{achat.Id}", Ramettes() with { Montant = 50m });

        Assert.Equal(HttpStatusCode.OK, modifie.StatusCode);
        Assert.Equal(50m, Assert.Single((await _amira.GetFromJsonAsync<MaCaisse>("/api/caisse"))!.Ouvertes).Sorties);
        var lignes = (await _application.JournalAsync(_jasmins)).Where(l => l.Action == ActionDuJournal.SortieDeCaisse).ToList();
        Assert.Equal(("Sortie, 45,000 TND", "Sortie, 50,000 TND"), (lignes[1].Avant, lignes[1].Apres));

        // Une autre secrétaire ne peut pas.
        await VerifierLeRefusAsync(await _sonia.DeleteAsync($"/api/achats/{achat.Id}"), HttpStatusCode.Forbidden,
            "Cet achat a été saisi par une autre personne : seul le propriétaire peut le changer");

        Assert.Equal(HttpStatusCode.NoContent, (await _amira.DeleteAsync($"/api/achats/{achat.Id}")).StatusCode);
        Assert.Equal("Supprimée", (await _application.JournalAsync(_jasmins)).Last(l => l.Action == ActionDuJournal.SortieDeCaisse).Apres);
        Assert.Empty((await ListeAsync(_amira, "")).Achats);
    }

    [Fact]
    public async Task Apres_la_cloture_ou_le_lendemain_seul_le_proprietaire_change_un_achat()
    {
        var sortie = await AjouterAsync(_amira, Ramettes());
        var horsCaisse = await AjouterAsync(_amira, Ramettes(false));
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync("/api/caisse/cloture", new ClotureSaisie(Le27, 0m))).StatusCode);

        await VerifierLeRefusAsync(await _amira.PutAsJsonAsync($"/api/achats/{sortie.Id}", Ramettes()), HttpStatusCode.Forbidden,
            "Votre caisse est clôturée : seul le propriétaire peut changer cet achat");
        await VerifierLeRefusAsync(await _amira.DeleteAsync($"/api/achats/{horsCaisse.Id}"), HttpStatusCode.Forbidden,
            "Votre caisse est clôturée : seul le propriétaire peut changer cet achat");

        // Le propriétaire peut ; une sortie déjà clôturée garde son montant.
        var modifie = await _hedi.PutAsJsonAsync($"/api/achats/{sortie.Id}", Ramettes() with { Libelle = "Ramettes A4", Montant = 99m });
        Assert.Equal(("Ramettes A4", 45m), (await modifie.Content.ReadFromJsonAsync<AchatDecrit>()) is { } a ? (a.Libelle, a.Montant) : default);

        _application.Horloge.SetUtcNow(Aujourdhui.AddDays(1));
        _amira = await _application.ClientConnecteAsync(_compteDAmira);
        var lendemain = await AjouterAsync(_amira, Ramettes(false) with { Jour = Le27.AddDays(1) });
        Assert.True(lendemain.Modifiable);
        Assert.Equal("Cet achat date d'avant aujourd'hui : seul le propriétaire peut le changer",
            (await ListeAsync(_amira, "?mois=2027-11")).Achats.Single(x => x.Id == horsCaisse.Id).PourquoiPasModifiable);
    }

    // ----- La facture -----

    [Fact]
    public async Task Joindre_lire_et_retirer_la_facture()
    {
        var achat = await AjouterAsync(_amira, Ramettes(false));

        var reponse = await _amira.PutAsync($"/api/achats/{achat.Id}/facture", Facture("image/png", [0x89, 0x50, 0x4E, 0x47]));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.True((await reponse.Content.ReadFromJsonAsync<AchatDecrit>())!.AUneFacture);
        var fichier = await _amira.GetAsync($"/api/achats/{achat.Id}/facture");
        Assert.Equal(("image/png", 4), (fichier.Content.Headers.ContentType!.MediaType, (await fichier.Content.ReadAsByteArrayAsync()).Length));

        await VerifierLeRefusAsync(await _amira.PutAsync($"/api/achats/{achat.Id}/facture", Facture("text/plain", [1, 2, 3])), HttpStatusCode.BadRequest,
            "La facture est une photo (JPEG, PNG, WebP) ou un PDF de 5 Mo au plus");
        await VerifierLeRefusAsync(await _amira.PutAsync($"/api/achats/{achat.Id}/facture", Facture("application/pdf", new byte[5 * 1024 * 1024 + 1])),
            HttpStatusCode.BadRequest, "La facture est une photo (JPEG, PNG, WebP) ou un PDF de 5 Mo au plus");

        Assert.False((await (await _amira.DeleteAsync($"/api/achats/{achat.Id}/facture")).Content.ReadFromJsonAsync<AchatDecrit>())!.AUneFacture);
        Assert.Equal(HttpStatusCode.NotFound, (await _amira.GetAsync($"/api/achats/{achat.Id}/facture")).StatusCode);
    }

    // ----- Les totaux (RHA-06) -----

    [Fact]
    public async Task Les_totaux_par_mois_annee_et_categorie()
    {
        await AjouterAsync(_amira, new AchatSaisi(new DateOnly(2027, 10, 5), "Papier", "Materiel", 320m, null, false));
        await AjouterAsync(_amira, new AchatSaisi(new DateOnly(2027, 10, 20), "Ménage", "Service", 450m, null, false));
        await AjouterAsync(_amira, Ramettes());

        var octobre = await ListeAsync(_amira, "?mois=2027-10");
        Assert.Equal((320m, 450m, 770m), (octobre.TotalMateriel, octobre.TotalService, octobre.Total));
        Assert.Equal(["Ménage", "Papier"], octobre.Achats.Select(a => a.Libelle));
        Assert.Equal(45m, (await ListeAsync(_amira, "")).Total);
        Assert.Equal(815m, (await ListeAsync(_amira, "?annee=2027")).Total);
        Assert.Equal(365m, (await ListeAsync(_amira, "?annee=2027&categorie=Materiel")).Total);
    }

    [Fact]
    public async Task Un_autre_centre_ne_voit_rien()
    {
        var achat = await AjouterAsync(_amira, Ramettes(false));
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi", RoleDuCompte.Proprietaire));

        Assert.Empty((await ListeAsync(rim, "")).Achats);
        await VerifierLeRefusAsync(await rim.DeleteAsync($"/api/achats/{achat.Id}"), HttpStatusCode.NotFound, "Achat introuvable");
        Assert.Equal(HttpStatusCode.NotFound, (await rim.GetAsync($"/api/achats/{achat.Id}/facture")).StatusCode);
    }

    // ----- Outils -----

    private static async Task<AchatDecrit> AjouterAsync(HttpClient client, AchatSaisi saisie)
    {
        var reponse = await client.PostAsJsonAsync("/api/achats", saisie);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<AchatDecrit>())!;
    }

    private static async Task<AchatsEtTotaux> ListeAsync(HttpClient client, string filtres) =>
        (await client.GetFromJsonAsync<AchatsEtTotaux>($"/api/achats{filtres}"))!;

    private static MultipartFormDataContent Facture(string type, byte[] contenu)
    {
        var fichier = new ByteArrayContent(contenu);
        fichier.Headers.ContentType = new MediaTypeHeaderValue(type);
        return new MultipartFormDataContent { { fichier, "facture", "facture.png" } };
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
