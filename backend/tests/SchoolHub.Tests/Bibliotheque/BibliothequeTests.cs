using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Bibliotheque;
using SchoolHub.Modules.Caisse;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Bibliotheque;

/// <summary>
/// BIB-01 à BIB-03 : la bibliothèque. Chaque test part d'un nouveau centre « Jasmins » (entrée à 5 TND, capacité de 2
/// places, année scolaire du 15/09/2027 au 30/06/2028) où Adem Miled est inscrit en Mathématiques. On est le samedi
/// 27/11/2027 à 10h à Tunis (9h UTC).
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class BibliothequeTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "bibliotheque";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _proprietaire = null!;
    private HttpClient _amira = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _adem;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        _proprietaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire));
        await ReglerAsync(5m);
        _amira = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi"));
        _niveaux = (await _amira.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        var karim = (await (await _amira.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        var groupe = (await (await _amira.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(karim, "Mathématiques", Niveau9,
            [new CreneauSaisi("Samedi", "10:00", "12:00")], 40m, new DateOnly(2027, 9, 18), null, ""))).Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        _adem = (await (await _amira.PostAsJsonAsync("/api/eleves", Fiche("Adem", "Miled", "22 345 678")))
            .Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(_adem, groupe, null, new DateOnly(2027, 11, 13)))).StatusCode);
        _application.Emails.Envoyes.Clear();
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau9 => _niveaux.Single(n => n.Nom == "9ᵉ année de base").Id;

    private FicheSaisieDeLEleve Fiche(string prenom, string nom, string telephone) =>
        new(prenom, nom, Niveau9, false, "98 401 220", null, telephone, $"{prenom.ToLowerInvariant()}@exemple.tn", null);

    private async Task ReglerAsync(decimal? prix) =>
        Assert.Equal(HttpStatusCode.OK, (await _proprietaire.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", null, null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), 20m, prix, 2, null))).StatusCode);

    // ----- Une entrée (BIB-01) -----

    [Fact]
    public async Task Une_entree_d_un_eleve_du_centre_a_son_recu_dans_la_caisse()
    {
        var enregistree = await EntrerAsync(new EntreeSaisie(_adem, null));

        Assert.Equal(("Adem", false, "10:00", null), (enregistree.Entree.Prenom, enregistree.Entree.Exterieur, enregistree.Entree.Arrivee, enregistree.Entree.Depart));
        Assert.Empty(enregistree.Avertissements);
        Assert.Equal((1, 5m), (enregistree.Recu.Numero, enregistree.Recu.Total));
        Assert.Equal([("Bibliothèque", "Entrée à la bibliothèque (journée)")], enregistree.Recu.Lignes.Select(l => (l.Groupe, l.Libelle)));
        Assert.Contains("• Entrée à la bibliothèque (journée) : 5,000 TND", enregistree.Recu.Texte, StringComparison.Ordinal);
        // Le reçu se remet seulement sur demande : aucun email ne part.
        Assert.Empty(_application.Emails.Envoyes);
        // Il entre dans la caisse du jour d'Amira ; Adem ne doit rien de plus.
        Assert.Equal(5m, Assert.Single((await _amira.GetFromJsonAsync<MaCaisse>("/api/caisse"))!.Ouvertes).Encaisse);
    }

    [Fact]
    public async Task Une_personne_de_l_exterieur_a_sa_fiche_et_son_entree_ensemble()
    {
        var enregistree = await EntrerAsync(new EntreeSaisie(null, Fiche("Rim", "Saidi", "50 333 444")));

        Assert.Equal(("Rim", true), (enregistree.Entree.Prenom, enregistree.Entree.Exterieur));
        Assert.Contains((await _amira.GetFromJsonAsync<List<EleveTrouve>>("/api/eleves?recherche=saidi"))!, e => e.Id == enregistree.Entree.EleveId);
        // Une fiche qui a une entrée a un historique.
        Assert.Equal(HttpStatusCode.Conflict, (await _amira.DeleteAsync($"/api/eleves/{enregistree.Entree.EleveId}")).StatusCode);
    }

    [Fact]
    public async Task Une_fiche_refusee_n_enregistre_rien()
    {
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync("/api/bibliotheque/entrees", new EntreeSaisie(null, Fiche("", "Saidi", "50 333 444"))),
            HttpStatusCode.BadRequest, "Le prénom est obligatoire");
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync("/api/bibliotheque/entrees", new EntreeSaisie(Guid.NewGuid(), null)),
            HttpStatusCode.NotFound, "Élève introuvable");

        Assert.Empty((await JourneeAsync("")).Liste);
        Assert.Empty((await _amira.GetFromJsonAsync<List<RecuDeLaListe>>("/api/recus"))!);
    }

    [Fact]
    public async Task Prevenir_sans_bloquer_d_une_seconde_entree_et_d_une_bibliotheque_pleine()
    {
        await EntrerAsync(new EntreeSaisie(_adem, null));
        await EntrerAsync(new EntreeSaisie(null, Fiche("Rim", "Saidi", "50 333 444")));

        var troisieme = await EntrerAsync(new EntreeSaisie(_adem, null));

        Assert.Equal(["Adem Miled a déjà une entrée aujourd'hui.", "La bibliothèque est pleine (2 / 2) : l'entrée est quand même notée."],
            troisieme.Avertissements);
        Assert.Equal(3, (await JourneeAsync("")).Entrees);
    }

    [Fact]
    public async Task Sans_prix_fixe_l_entree_est_refusee()
    {
        await ReglerAsync(null);

        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync("/api/bibliotheque/entrees", new EntreeSaisie(_adem, null)),
            HttpStatusCode.Conflict, "Le prix de l'entrée n'est pas fixé : voyez les paramètres du centre");
    }

    // ----- La liste du jour et les départs (BIB-02, BIB-03) -----

    [Fact]
    public async Task Noter_le_depart_maintenant_ou_a_une_heure_corrigee()
    {
        var adem = (await EntrerAsync(new EntreeSaisie(_adem, null))).Entree;
        var rim = (await EntrerAsync(new EntreeSaisie(null, Fiche("Rim", "Saidi", "50 333 444")))).Entree;
        _application.Horloge.SetUtcNow(Aujourdhui.AddHours(2).AddMinutes(15));

        Assert.Equal("12:15", (await DepartAsync(adem.Id, null)).Depart);
        Assert.Equal("11:30", (await DepartAsync(rim.Id, "11:30")).Depart);

        await VerifierLeRefusAsync(await _amira.PutAsJsonAsync($"/api/bibliotheque/entrees/{rim.Id}/depart", new DepartSaisi("9h")),
            HttpStatusCode.BadRequest, "L'heure de départ s'écrit HH:mm, après l'arrivée");
        await VerifierLeRefusAsync(await _amira.PutAsJsonAsync($"/api/bibliotheque/entrees/{rim.Id}/depart", new DepartSaisi("09:59")),
            HttpStatusCode.BadRequest, "L'heure de départ s'écrit HH:mm, après l'arrivée");
        await VerifierLeRefusAsync(await _amira.PutAsJsonAsync($"/api/bibliotheque/entrees/{Guid.NewGuid()}/depart", new DepartSaisi(null)),
            HttpStatusCode.NotFound, "Entrée introuvable");
    }

    [Fact]
    public async Task La_liste_du_jour_compte_les_presents_et_l_encaisse_sans_les_recus_annules()
    {
        var adem = await EntrerAsync(new EntreeSaisie(_adem, null));
        await EntrerAsync(new EntreeSaisie(null, Fiche("Rim", "Saidi", "50 333 444")));
        var sami = await EntrerAsync(new EntreeSaisie(null, Fiche("Sami", "Chaabane", "50 000 333")));
        await DepartAsync(adem.Entree.Id, null);
        Assert.Equal(HttpStatusCode.OK, (await _amira.PostAsJsonAsync($"/api/recus/{sami.Recu.Id}/annulation", new AnnulationDuRecu("Erreur"))).StatusCode);

        var journee = await JourneeAsync("");

        Assert.Equal((5m, 2, 1, 2, 10m), (journee.Prix!.Value, journee.Capacite!.Value, journee.Presents, journee.Entrees, journee.Encaisse));
        Assert.Equal([("Adem", false), ("Rim", false), ("Sami", true)], journee.Liste.Select(e => (e.Prenom, e.Annule)));

        // Un jour passé : la liste de ce jour-là.
        _application.Horloge.SetUtcNow(Aujourdhui.AddDays(1));
        _amira = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Sonia", "Mabrouk"));
        Assert.Empty((await JourneeAsync("")).Liste);
        Assert.Equal(3, (await JourneeAsync("?jour=2027-11-27")).Liste.Count);
    }

    [Fact]
    public async Task Un_autre_centre_ne_voit_rien()
    {
        var entree = (await EntrerAsync(new EntreeSaisie(_adem, null))).Entree;
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));

        Assert.Empty((await rim.GetFromJsonAsync<JourneeALaBibliotheque>("/api/bibliotheque"))!.Liste);
        await VerifierLeRefusAsync(await rim.PutAsJsonAsync($"/api/bibliotheque/entrees/{entree.Id}/depart", new DepartSaisi(null)),
            HttpStatusCode.NotFound, "Entrée introuvable");
        await VerifierLeRefusAsync(await rim.PostAsJsonAsync("/api/bibliotheque/entrees", new EntreeSaisie(_adem, null)),
            HttpStatusCode.Conflict, "Le prix de l'entrée n'est pas fixé : voyez les paramètres du centre");
    }

    // ----- Outils -----

    private async Task<EntreeEnregistree> EntrerAsync(EntreeSaisie saisie)
    {
        var reponse = await _amira.PostAsJsonAsync("/api/bibliotheque/entrees", saisie);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EntreeEnregistree>())!;
    }

    private async Task<EntreeDecrite> DepartAsync(Guid entree, string? heure)
    {
        var reponse = await _amira.PutAsJsonAsync($"/api/bibliotheque/entrees/{entree}/depart", new DepartSaisi(heure));
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EntreeDecrite>())!;
    }

    private async Task<JourneeALaBibliotheque> JourneeAsync(string filtres) =>
        (await _amira.GetFromJsonAsync<JourneeALaBibliotheque>($"/api/bibliotheque{filtres}"))!;

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
