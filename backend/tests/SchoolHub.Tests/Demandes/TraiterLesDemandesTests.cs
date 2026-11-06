using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Modules.Demandes;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Demandes;

/// <summary>
/// DEM-03 à DEM-05 : traiter les demandes d'inscription. Chaque test part d'un nouveau centre « Jasmins » (année scolaire
/// du 15/09/2027 au 30/06/2028, assurance de 20 TND) et des groupes de Karim Ben Salah ; on est le samedi 27/11/2027 à
/// 10h à Tunis. Les demandes arrivent par le formulaire public, comme en vrai.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class TraiterLesDemandesTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "traiter_les_demandes";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly CreneauSaisi Samedi10h = new("Samedi", "10:00", "12:00");

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _secretaire = null!;
    private HttpClient _visiteur = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _maths;
    private Guid _informatique;
    private Guid _anglaisFerme;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        var proprietaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire));
        Assert.Equal(HttpStatusCode.OK, (await proprietaire.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", null, null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), 20m, null, null, null))).StatusCode);
        _secretaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi"));
        _niveaux = (await _secretaire.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        var karim = (await (await _secretaire.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        _maths = await GroupeAsync(new CreationDUnGroupe(karim, "Mathématiques", Niveau("9ᵉ année de base"), [Samedi10h], 40m, new DateOnly(2027, 9, 18), 15, ""));
        _informatique = await GroupeAsync(new CreationDUnGroupe(karim, "Informatique", null, [Samedi10h], null, new DateOnly(2027, 12, 4), null, "",
            Type: "Formation", PrixTotal: 300m, NombreDeSeances: 10));
        _anglaisFerme = await GroupeAsync(new CreationDUnGroupe(karim, "Anglais", Niveau("9ᵉ année de base"), [Samedi10h], 40m, new DateOnly(2027, 9, 18), null, ""));
        _visiteur = _application.CreateClient();
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau(string nom) => _niveaux.Single(n => n.Nom == nom).Id;

    /// <summary>Adem Miled, en 9ᵉ année de base, qui veut les Mathématiques et l'Informatique, avec son ami Sami.</summary>
    private DemandeEnvoyee Adem => new(
        "Adem", "Miled", Niveau("9ᵉ année de base"), false, "98 401 220", null, "22 345 678", "adem@exemple.tn", null,
        [_maths, _informatique], "Sami Chaabane", null);

    private DemandeEnvoyee Lina => new(
        "Lina", "Gharbi", Niveau("9ᵉ année de base"), false, "97 111 222", null, "50 111 222", "lina@exemple.tn", null, [_maths], null, null);

    /// <summary>La fiche telle que la demande l'a envoyée, vérifiée par la secrétaire.</summary>
    private FicheSaisieDeLEleve FicheDAdem => new("Adem", "Miled", Niveau("9ᵉ année de base"), false, "98 401 220", null, "22 345 678", "adem@exemple.tn", null);

    // ----- La liste (DEM-03) -----

    [Fact]
    public async Task Les_demandes_a_traiter_les_plus_anciennes_en_premier()
    {
        await EnvoyerAsync(Adem);
        _application.Horloge.Advance(TimeSpan.FromHours(1));
        await EnvoyerAsync(Lina);

        var demandes = await AtraiterAsync();

        Assert.Equal(["Adem", "Lina"], demandes.Select(d => d.Prenom));
        var adem = demandes[0];
        Assert.Equal(("9ᵉ année de base", "ATraiter", false, "Sami Chaabane"), (adem.Niveau.Nom, adem.Etat, adem.DoublonPossible, adem.Amis));
        Assert.Equal(["Mathématiques", "Informatique"], adem.Matieres.Select(m => m.Matiere));
        Assert.Equal(("Karim Ben Salah", "Samedi", "10:00"), (adem.Matieres[0].Professeur, adem.Matieres[0].Creneaux[0].Jour, adem.Matieres[0].Creneaux[0].Debut));
        Assert.Equal(new NombreDeDemandes(2), await _secretaire.GetFromJsonAsync<NombreDeDemandes>("/api/demandes/nombre"));
    }

    [Fact]
    public async Task Doublon_possible_avec_un_eleve()
    {
        Assert.Equal(HttpStatusCode.Created, (await _secretaire.PostAsJsonAsync("/api/eleves", FicheDAdem with { TelephoneEleve = "21 000 000" })).StatusCode);
        await EnvoyerAsync(Adem);
        await EnvoyerAsync(Lina);

        var demandes = await AtraiterAsync();

        Assert.True(demandes.Single(d => d.Prenom == "Adem").DoublonPossible);
        Assert.False(demandes.Single(d => d.Prenom == "Lina").DoublonPossible);
    }

    [Fact]
    public async Task Doublon_possible_avec_une_autre_demande_par_le_telephone()
    {
        await EnvoyerAsync(Adem);
        // Sa sœur, avec le même téléphone de parent.
        await EnvoyerAsync(Lina with { Prenom = "Yasmine", Nom = "Miled", TelephoneParent = "98 401 220" });

        Assert.All(await AtraiterAsync(), d => Assert.True(d.DoublonPossible));
    }

    // ----- Le détail d'une demande -----

    [Fact]
    public async Task Le_detail_d_une_demande()
    {
        // Sami, cité comme ami, est déjà en Mathématiques.
        var sami = await EleveAsync(new FicheSaisieDeLEleve("Sami", "Chaabane", Niveau("9ᵉ année de base"), false, "97 000 333", null, "50 000 333", "sami@exemple.tn", null));
        Assert.Equal(HttpStatusCode.Created, (await _secretaire.PostAsJsonAsync("/api/inscriptions",
            new InscriptionSaisie(sami, _maths, null, new DateOnly(2027, 11, 27)))).StatusCode);
        await EnvoyerAsync(Adem);

        var detail = await DetailAsync((await AtraiterAsync())[0].Id);

        Assert.Equal(("Adem", "Miled", "98 401 220", "22 345 678", "ATraiter"), (detail.Prenom, detail.Nom, detail.TelephoneParent, detail.TelephoneEleve, detail.Etat));
        Assert.Empty(detail.FichesSemblables);
        Assert.Equal(["Mathématiques", "Informatique"], detail.Matieres.Select(m => m.Matiere));
        var maths = detail.Matieres[0];
        Assert.Equal(_maths, maths.GroupePrefere);
        var groupe = Assert.Single(maths.Groupes);
        Assert.Equal((_maths, 1, 15, "Etude"), (groupe.Id, groupe.Eleves, groupe.Capacite, groupe.Type));
        Assert.Empty(groupe.Modes);
        Assert.Equal(new SeanceDeDebut(new DateOnly(2027, 11, 27), new TimeOnly(10, 0)), groupe.PremiereSeance);
        Assert.Equal(["Sami Chaabane"], groupe.Amis);
        Assert.Equal(new DateOnly(2027, 12, 4), detail.Matieres[1].Groupes.Single().PremiereSeance.Date);
    }

    [Fact]
    public async Task Le_detail_montre_les_fiches_semblables()
    {
        var existant = await EleveAsync(FicheDAdem with { Prenom = "Adam", TelephoneEleve = "21 000 000" });
        await EnvoyerAsync(Adem);

        var detail = await DetailAsync((await AtraiterAsync())[0].Id);

        // Même téléphone de parent, nom un peu différent : la secrétaire voit la fiche et peut la choisir.
        Assert.Equal([existant], detail.FichesSemblables.Select(e => e.Id));
    }

    [Fact]
    public async Task Le_groupe_ferme_n_est_plus_propose()
    {
        await EnvoyerAsync(Adem with { Groupes = [_anglaisFerme] });
        Assert.Equal(HttpStatusCode.OK, (await _secretaire.PostAsJsonAsync($"/api/groupes/{_anglaisFerme}/fermeture",
            new FermetureDUnGroupe(new DateOnly(2027, 11, 20), "Trop peu d'élèves"))).StatusCode);

        var detail = await DetailAsync((await AtraiterAsync())[0].Id);

        Assert.Equal(_anglaisFerme, detail.Matieres.Single().GroupePrefere);
        Assert.Empty(detail.Matieres.Single().Groupes);
    }

    // ----- Enregistrer (DEM-04) -----

    [Fact]
    public async Task Enregistrer_une_demande_avec_une_nouvelle_fiche()
    {
        await EnvoyerAsync(Adem);
        var id = (await AtraiterAsync())[0].Id;

        var reponse = await _secretaire.PostAsJsonAsync($"/api/demandes/{id}/inscription",
            new DemandeAEnregistrer(null, FicheDAdem, [new GroupeChoisi(_maths, null), new GroupeChoisi(_informatique, null)]));

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var enregistree = (await reponse.Content.ReadFromJsonAsync<DemandeEnregistree>())!;
        Assert.Equal(("Adem", "Miled"), (enregistree.Eleve.Prenom, enregistree.Eleve.Nom));
        Assert.Equal([_maths, _informatique], enregistree.Inscriptions.Select(i => i.Inscription.Groupe.Id));
        Assert.Equal(new DateOnly(2027, 11, 27), enregistree.Inscriptions[0].Inscription.PremiereSeance!.Date);
        Assert.Equal(new DateOnly(2027, 12, 4), enregistree.Inscriptions[1].Inscription.PremiereSeance!.Date);
        Assert.Equal(
            "Bonjour, l'inscription d'Adem Miled est enregistrée.\n"
            + "• Mathématiques avec Karim Ben Salah, Groupe Samedi 10h → 12h : première séance le samedi 27/11/2027 à 10h.\n"
            + "• Informatique avec Karim Ben Salah, Groupe Samedi 10h → 12h : première séance le samedi 04/12/2027 à 10h.\n"
            + "À bientôt. Centre Les Jasmins",
            enregistree.Texte);
        Assert.Equal(["Parent", "Adem"], enregistree.Messages.Select(m => m.Destinataire));

        var demande = Assert.Single(await DemandesEnBaseAsync());
        Assert.Equal((EtatDeLaDemande.Inscrite, enregistree.Eleve.Id, Aujourdhui), (demande.Etat, demande.EleveId, demande.TraiteeLe));
        Assert.Empty(await AtraiterAsync());
        var traitee = Assert.Single(await TraiteesAsync());
        Assert.Equal("Inscrite", traitee.Etat);
        Assert.Equal(new NombreDeDemandes(0), await _secretaire.GetFromJsonAsync<NombreDeDemandes>("/api/demandes/nombre"));
    }

    [Fact]
    public async Task Une_seule_matiere_garde_le_message_de_l_inscription()
    {
        await EnvoyerAsync(Adem);
        var id = (await AtraiterAsync())[0].Id;

        var enregistree = await EnregistrerAsync(id, new DemandeAEnregistrer(null, FicheDAdem, [new GroupeChoisi(_maths, null)]));

        Assert.StartsWith("Bonjour, l'inscription d'Adem Miled est enregistrée.\nMathématiques avec Karim Ben Salah", enregistree.Texte, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enregistrer_avec_une_fiche_existante()
    {
        var existant = await EleveAsync(FicheDAdem);
        await EnvoyerAsync(Adem);
        var id = (await AtraiterAsync())[0].Id;

        var enregistree = await EnregistrerAsync(id, new DemandeAEnregistrer(existant, null, [new GroupeChoisi(_maths, null)]));

        Assert.Equal(existant, enregistree.Eleve.Id);
        Assert.Single(await _secretaire.GetFromJsonAsync<List<EleveTrouve>>("/api/eleves") ?? []);
        Assert.Equal(existant, Assert.Single(await DemandesEnBaseAsync()).EleveId);
    }

    [Fact]
    public async Task La_capacite_atteinte_previent_sans_bloquer()
    {
        for (var i = 0; i < 15; i++)
        {
            var eleve = await EleveAsync(new FicheSaisieDeLEleve($"Eleve{i}", "Test", Niveau("9ᵉ année de base"), false, $"9{i:0000000}", null,
                $"5{i:0000000}", $"eleve{i}@exemple.tn", null));
            Assert.Equal(HttpStatusCode.Created, (await _secretaire.PostAsJsonAsync("/api/inscriptions",
                new InscriptionSaisie(eleve, _maths, null, new DateOnly(2027, 11, 27)))).StatusCode);
        }
        await EnvoyerAsync(Adem);

        var enregistree = await EnregistrerAsync((await AtraiterAsync())[0].Id, new DemandeAEnregistrer(null, FicheDAdem, [new GroupeChoisi(_maths, null)]));

        Assert.Equal("Ce groupe a atteint sa capacité. L'inscription reste possible.", enregistree.Avertissement);
    }

    [Fact]
    public async Task Tout_ou_rien()
    {
        await EnvoyerAsync(Adem with { Groupes = [_maths, _anglaisFerme] });
        Assert.Equal(HttpStatusCode.OK, (await _secretaire.PostAsJsonAsync($"/api/groupes/{_anglaisFerme}/fermeture",
            new FermetureDUnGroupe(new DateOnly(2027, 11, 20), "Trop peu d'élèves"))).StatusCode);
        var id = (await AtraiterAsync())[0].Id;

        var reponse = await _secretaire.PostAsJsonAsync($"/api/demandes/{id}/inscription",
            new DemandeAEnregistrer(null, FicheDAdem, [new GroupeChoisi(_maths, null), new GroupeChoisi(_anglaisFerme, null)]));

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, "Ce groupe est fermé : on n'y inscrit plus");
        Assert.Empty(await _secretaire.GetFromJsonAsync<List<EleveTrouve>>("/api/eleves") ?? []);
        Assert.Equal(EtatDeLaDemande.ATraiter, Assert.Single(await DemandesEnBaseAsync()).Etat);
    }

    [Theory]
    [InlineData("aucun groupe", HttpStatusCode.BadRequest, "Choisissez au moins un groupe")]
    [InlineData("fiche incomplète", HttpStatusCode.BadRequest, "Le prénom est obligatoire")]
    [InlineData("fiche introuvable", HttpStatusCode.NotFound, "Élève introuvable")]
    [InlineData("groupe introuvable", HttpStatusCode.NotFound, "Groupe introuvable")]
    [InlineData("groupe déjà suivi", HttpStatusCode.Conflict, "Cet élève est déjà inscrit dans ce groupe")]
    public async Task Un_enregistrement_refuse(string cas, HttpStatusCode statut, string message)
    {
        await EnvoyerAsync(Adem);
        var id = (await AtraiterAsync())[0].Id;
        var maths = new List<GroupeChoisi> { new(_maths, null) };
        var saisie = cas switch
        {
            "aucun groupe" => new DemandeAEnregistrer(null, FicheDAdem, []),
            "fiche incomplète" => new DemandeAEnregistrer(null, FicheDAdem with { Prenom = " " }, maths),
            "fiche introuvable" => new DemandeAEnregistrer(Guid.NewGuid(), null, maths),
            "groupe introuvable" => new DemandeAEnregistrer(null, FicheDAdem, [new GroupeChoisi(Guid.NewGuid(), null)]),
            _ => new DemandeAEnregistrer(null, FicheDAdem, [new GroupeChoisi(_maths, null), new GroupeChoisi(_maths, null)]),
        };

        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/demandes/{id}/inscription", saisie), statut, message);
        Assert.Equal(EtatDeLaDemande.ATraiter, Assert.Single(await DemandesEnBaseAsync()).Etat);
    }

    [Fact]
    public async Task Une_demande_deja_traitee()
    {
        await EnvoyerAsync(Adem);
        var id = (await AtraiterAsync())[0].Id;
        await EnregistrerAsync(id, new DemandeAEnregistrer(null, FicheDAdem, [new GroupeChoisi(_maths, null)]));

        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/demandes/{id}/inscription",
            new DemandeAEnregistrer(null, FicheDAdem, [new GroupeChoisi(_maths, null)])), HttpStatusCode.Conflict, "Cette demande est déjà traitée");
        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/demandes/{id}/refus", new RefusSaisi("Doublon")),
            HttpStatusCode.Conflict, "Cette demande est déjà traitée");
    }

    // ----- Refuser (DEM-05) -----

    [Fact]
    public async Task Refuser_une_demande()
    {
        await EnvoyerAsync(Adem);
        var id = (await AtraiterAsync())[0].Id;

        Assert.Equal(HttpStatusCode.NoContent, (await _secretaire.PostAsJsonAsync($"/api/demandes/{id}/refus", new RefusSaisi("FausseDemande"))).StatusCode);

        Assert.Empty(await AtraiterAsync());
        var traitee = Assert.Single(await TraiteesAsync());
        Assert.Equal(("Refusee", "FausseDemande"), (traitee.Etat, traitee.Motif));
        Assert.Empty(await _secretaire.GetFromJsonAsync<List<EleveTrouve>>("/api/eleves") ?? []);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Autre")]
    [InlineData("1")]
    public async Task Le_motif_du_refus_est_obligatoire(string? motif)
    {
        await EnvoyerAsync(Adem);
        var id = (await AtraiterAsync())[0].Id;

        await VerifierLeRefusAsync(await _secretaire.PostAsJsonAsync($"/api/demandes/{id}/refus", new RefusSaisi(motif)),
            HttpStatusCode.BadRequest, "Choisissez le motif du refus");
    }

    // ----- La séparation des centres -----

    [Fact]
    public async Task Les_demandes_d_un_autre_centre_sont_introuvables()
    {
        await EnvoyerAsync(Adem);
        var id = (await AtraiterAsync())[0].Id;
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));

        Assert.Empty((await rim.GetFromJsonAsync<List<DemandeDeLaListe>>("/api/demandes"))!);
        Assert.Equal(new NombreDeDemandes(0), await rim.GetFromJsonAsync<NombreDeDemandes>("/api/demandes/nombre"));
        await VerifierLeRefusAsync(await rim.GetAsync($"/api/demandes/{id}"), HttpStatusCode.NotFound, "Demande introuvable");
        await VerifierLeRefusAsync(await rim.PostAsJsonAsync($"/api/demandes/{id}/refus", new RefusSaisi("Doublon")),
            HttpStatusCode.NotFound, "Demande introuvable");
        await VerifierLeRefusAsync(await rim.PostAsJsonAsync($"/api/demandes/{id}/inscription",
            new DemandeAEnregistrer(null, FicheDAdem, [new GroupeChoisi(_maths, null)])), HttpStatusCode.NotFound, "Demande introuvable");
    }

    [Fact]
    public async Task Les_demandes_sont_reservees_aux_personnes_connectees()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _visiteur.GetAsync("/api/demandes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _visiteur.GetAsync("/api/demandes/nombre")).StatusCode);
    }

    // ----- Outils -----

    private async Task EnvoyerAsync(DemandeEnvoyee demande) =>
        Assert.Equal(HttpStatusCode.Created, (await _visiteur.PostAsJsonAsync($"/api/public/inscription/{_jasmins.Code}", demande)).StatusCode);

    private async Task<List<DemandeDeLaListe>> AtraiterAsync() => (await _secretaire.GetFromJsonAsync<List<DemandeDeLaListe>>("/api/demandes"))!;

    private async Task<List<DemandeDeLaListe>> TraiteesAsync() =>
        (await _secretaire.GetFromJsonAsync<List<DemandeDeLaListe>>("/api/demandes?traitees=true"))!;

    private async Task<DetailDeLaDemande> DetailAsync(Guid id) => (await _secretaire.GetFromJsonAsync<DetailDeLaDemande>($"/api/demandes/{id}"))!;

    private async Task<DemandeEnregistree> EnregistrerAsync(Guid id, DemandeAEnregistrer saisie)
    {
        var reponse = await _secretaire.PostAsJsonAsync($"/api/demandes/{id}/inscription", saisie);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<DemandeEnregistree>())!;
    }

    private async Task<Guid> EleveAsync(FicheSaisieDeLEleve fiche)
    {
        var reponse = await _secretaire.PostAsJsonAsync("/api/eleves", fiche);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
    }

    private async Task<Guid> GroupeAsync(CreationDUnGroupe creation)
    {
        var reponse = await _secretaire.PostAsJsonAsync("/api/groupes", creation);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
    }

    /// <summary>Les demandes du centre du test, lues sans le filtre du centre courant.</summary>
    private Task<List<DemandeDInscription>> DemandesEnBaseAsync() =>
        _application.DansUnScopeAsync(services =>
            services.GetRequiredService<SchoolHubDbContext>().DemandesDInscription.IgnoreQueryFilters()
                .Where(d => d.CentreId == _jasmins.Id)
                .ToListAsync());

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
