using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Achats;
using SchoolHub.Modules.Bibliotheque;
using SchoolHub.Modules.Caisse;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Modules.Rh;
using SchoolHub.Modules.Tableau;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Tableau;

/// <summary>
/// TDB-01 à TDB-04 : le tableau de bord et le journal. Chaque test part d'un nouveau centre « Jasmins » (part du centre
/// 5 %, assurance 20 TND, bibliothèque 5 TND, année scolaire du 15/09/2027 au 30/06/2028) et du groupe de Mathématiques
/// de Karim Ben Salah, le samedi depuis le 18/09/2027, à 40 TND le Mois : le Mois 2 finit le 06/11, le Mois 3 commence le
/// 13/11. Adem Miled et Lina Gharbi y sont inscrits depuis le 18/09. En novembre, Adem paie son Mois 3 et son assurance,
/// une entrée à la bibliothèque est payée, un achat de 45 TND sort de la caisse, et l'agent de ménage gagne 900 TND.
/// On est le samedi 27/11/2027 à 10h à Tunis.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class TableauDeBordTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "tableau_de_bord";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _hedi = null!;
    private HttpClient _amira = null!;
    private Compte _compteDAmira = null!;
    private Guid _maths;
    private (Guid Eleve, Guid Inscription) _adem;
    private (Guid Eleve, Guid Inscription) _lina;
    private Guid _entree;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        _hedi = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire));
        Assert.Equal(HttpStatusCode.OK, (await _hedi.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", null, null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), 20m, 5m, null, 5m))).StatusCode);
        _compteDAmira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        _amira = await _application.ClientConnecteAsync(_compteDAmira);
        var niveau = (await _amira.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == "9ᵉ année de base").Id;
        var karim = (await (await _amira.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        _maths = (await (await _amira.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(karim, "Mathématiques", niveau,
            [new CreneauSaisi("Samedi", "10:00", "12:00")], 40m, new DateOnly(2027, 9, 18), null, ""))).Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        _adem = await InscrireAsync("Adem", "Miled", "22 345 678", niveau);
        _lina = await InscrireAsync("Lina", "Gharbi", "50 111 222", niveau);

        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync($"/api/eleves/{_adem.Eleve}/encaissement",
            new EncaissementSaisi([new SommeChoisie(_adem.Inscription, "Mois 3"), new SommeChoisie(null, "Assurance")]))).StatusCode);
        var entree = await _amira.PostAsJsonAsync("/api/bibliotheque/entrees", new EntreeSaisie(_adem.Eleve, null));
        _entree = (await entree.Content.ReadFromJsonAsync<EntreeEnregistree>())!.Recu.Id;
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync("/api/achats",
            new AchatSaisi(new DateOnly(2027, 11, 27), "Ramettes de papier", "Materiel", 45m, null, true))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _hedi.PostAsJsonAsync("/api/employes",
            new EmployeSaisi("Fatma", "Ayari", null, null, "Agent de ménage", null, 900m))).StatusCode);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    // ----- Le tableau de bord (TDB-01, TDB-02) -----

    [Fact]
    public async Task Le_tableau_d_un_mois()
    {
        var novembre = await TableauAsync("?mois=2027-11");

        Assert.Equal(("Novembre 2027", new DateOnly(2027, 11, 1), new DateOnly(2027, 11, 30)), (novembre.Periode, novembre.Du, novembre.Au));
        // Encaissé : 40 (Étude) + 20 (assurance) + 5 (bibliothèque).
        Assert.Equal(65m, novembre.Encaisse);
        Assert.Equal([("Étude", 40m), ("Révision", 0m), ("Formation", 0m), ("Bibliothèque", 5m), ("Assurance", 20m)],
            novembre.EncaisseParOrigine.Select(m => (m.Libelle, m.Valeur)));
        // Impayés : le Mois 3 de Lina, dû le 13/11. Son assurance était due en septembre.
        Assert.Equal((40m, 0m), (novembre.Impayes, novembre.ImpayesDAssurance));
        // Dépenses : la part de Karim pour le Mois 2, fini le 06/11 (80 − 5 % = 76), le salaire, l'achat.
        Assert.Equal((76m, 900m, 45m, 1021m, -956m), (novembre.PartsDesProfesseurs, novembre.Salaires, novembre.Achats, novembre.Depenses, novembre.Benefice));

        var maths = Assert.Single(novembre.ParGroupe);
        Assert.Equal((_maths, "Karim Ben Salah", "Étude", (decimal?)80m, 40m, 40m, (decimal?)76m, (decimal?)4m),
            (maths.GroupeId, maths.Professeur, maths.Type, maths.MontantDuGroupe, maths.Encaisse, maths.Impayes, maths.PartDuProfesseur, maths.PartDuCentre));
    }

    [Fact]
    public async Task Un_recu_annule_ne_compte_pas_et_l_annee_scolaire_cumule()
    {
        Assert.Equal(HttpStatusCode.OK, (await _amira.PostAsJsonAsync($"/api/recus/{_entree}/annulation", new AnnulationDuRecu("Erreur"))).StatusCode);

        Assert.Equal(60m, (await TableauAsync("?mois=2027-11")).Encaisse);

        var annee = await TableauAsync("?annee=true");
        Assert.Equal(("Année 2027-2028", 60m), (annee.Periode, annee.Encaisse));
        // Impayés depuis la rentrée : Lina doit ses Mois 1 à 3 (120) et son assurance (20), Adem ses Mois 1 et 2 (80).
        Assert.Equal((220m, 20m), (annee.Impayes, annee.ImpayesDAssurance));
        // Les parts des Mois 1 et 2 (2 × 76), et trois mois de salaire (septembre à novembre).
        Assert.Equal((152m, 2700m), (annee.PartsDesProfesseurs, annee.Salaires));
    }

    // ----- Prix spéciaux et dispenses (TDB-03) -----

    [Fact]
    public async Task Les_prix_speciaux_et_les_dispenses()
    {
        Assert.Equal(HttpStatusCode.OK, (await _hedi.PutAsJsonAsync($"/api/inscriptions/{_lina.Inscription}/prix-special",
            new PrixSpecialSaisi(20m, "Famille en difficulté"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _hedi.PostAsJsonAsync($"/api/eleves/{_lina.Eleve}/assurance/dispense",
            new RaisonSaisie("Déjà assurée par l'école"))).StatusCode);

        var liste = (await _hedi.GetFromJsonAsync<PrixSpeciauxEtDispenses>("/api/tableau-de-bord/prix-speciaux"))!;

        var prix = Assert.Single(liste.PrixSpeciaux);
        Assert.Equal(("Lina Gharbi", "Mathématiques · Groupe Samedi 10h → 12h", (decimal?)40m, 20m, "Famille en difficulté", (DateOnly?)new DateOnly(2027, 11, 27), "Hedi Ben Salah"),
            (prix.Eleve, prix.Groupe, prix.PrixNormal, prix.PrixSpecial, prix.Raison, prix.Le, prix.Par));
        var dispense = Assert.Single(liste.Dispenses);
        Assert.Equal(("Lina Gharbi", 20m, "Déjà assurée par l'école", "Hedi Ben Salah"), (dispense.Eleve, dispense.Montant, dispense.Raison, dispense.Par));
    }

    // ----- Le journal (TDB-04) -----

    [Fact]
    public async Task Le_journal_se_filtre_par_personne_action_et_periode()
    {
        var tout = (await _hedi.GetFromJsonAsync<JournalFiltre>("/api/journal"))!;
        Assert.Contains(tout.Lignes, l => l.Action == "Encaissement" && l.Auteur == "Amira Jlassi");
        Assert.Contains("SortieDeCaisse", tout.Actions);
        Assert.Contains(tout.Auteurs, a => a.Nom == "Amira Jlassi");
        // Les plus récentes en premier.
        Assert.True(tout.Lignes.Zip(tout.Lignes.Skip(1)).All(p => p.First.Quand >= p.Second.Quand));

        var encaissements = (await _hedi.GetFromJsonAsync<JournalFiltre>($"/api/journal?action=Encaissement&compteId={_compteDAmira.Id}"))!;
        Assert.Equal(2, encaissements.Lignes.Count);
        Assert.Equal(2, (await _hedi.GetFromJsonAsync<JournalFiltre>("/api/journal?action=Encaissement&du=2027-11-27&au=2027-11-27"))!.Lignes.Count);
        Assert.Empty((await _hedi.GetFromJsonAsync<JournalFiltre>("/api/journal?du=2027-11-28"))!.Lignes);
    }

    [Fact]
    public async Task Tout_est_reserve_au_proprietaire()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.GetAsync("/api/tableau-de-bord")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.GetAsync("/api/tableau-de-bord/prix-speciaux")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _amira.GetAsync("/api/journal")).StatusCode);

        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi", RoleDuCompte.Proprietaire));
        var vide = (await rim.GetFromJsonAsync<TableauDUnePeriode>("/api/tableau-de-bord?mois=2027-11"))!;
        Assert.Equal((0m, 0m, 0m), (vide.Encaisse, vide.Depenses, vide.Impayes));
    }

    // ----- Outils -----

    private async Task<TableauDUnePeriode> TableauAsync(string filtres) => (await _hedi.GetFromJsonAsync<TableauDUnePeriode>($"/api/tableau-de-bord{filtres}"))!;

    private async Task<(Guid Eleve, Guid Inscription)> InscrireAsync(string prenom, string nom, string telephone, Guid niveau)
    {
        var reponse = await _amira.PostAsJsonAsync("/api/eleves", new FicheSaisieDeLEleve(prenom, nom, niveau, false, "97 000 111", null, telephone,
            $"{prenom.ToLowerInvariant()}@exemple.tn", null));
        var eleve = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        var inscription = await _amira.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(eleve, _maths, null, new DateOnly(2027, 9, 18)));
        Assert.Equal(HttpStatusCode.Created, inscription.StatusCode);
        return (eleve, (await inscription.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id);
    }
}
