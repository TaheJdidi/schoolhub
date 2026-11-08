using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Caisse;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Journal;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Caisse;

/// <summary>
/// CAI-04 : l'encaissement groupé. Chaque test part d'un nouveau centre « Jasmins » (téléphone 71 234 567, email du
/// centre, assurance de 20 TND, année scolaire du 15/09/2027 au 30/06/2028) et du groupe de Mathématiques de Karim Ben
/// Salah, le samedi de 10h à 12h depuis le 18/09/2027, à 40 TND le Mois. Depuis le 13/11, la première séance du Mois 3,
/// y sont inscrits Adem Miled (avec des emails), Lina Gharbi (en 6ᵉ, sans email), Sami Chaabane (qui a déjà tout payé) et Rania
/// Trabelsi (arrêtée au 20/11). On est le samedi 27/11/2027 à 10h à Tunis.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class EncaissementGroupeTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "encaissement_groupe";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Le13Novembre = new(2027, 11, 13);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _amira = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _maths;
    private (Guid Eleve, Guid Inscription) _adem;
    private (Guid Eleve, Guid Inscription) _lina;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        var proprietaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire));
        Assert.Equal(HttpStatusCode.OK, (await proprietaire.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", "71 234 567", "contact@jasmins.tn", new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)),
            20m, null, null, null))).StatusCode);
        _amira = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi"));
        _niveaux = (await _amira.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        var karim = (await (await _amira.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        var creation = await _amira.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(karim, "Mathématiques", Niveau9,
            [new CreneauSaisi("Samedi", "10:00", "12:00")], 40m, new DateOnly(2027, 9, 18), null, ""));
        _maths = (await creation.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;

        _adem = await InscrireAsync("Adem", "Miled", "98 401 220", "parent.miled@exemple.tn", "22 345 678", "adem@exemple.tn");
        // Lina est en 6ᵉ : sans email, ni pour elle ni pour son parent.
        _lina = await InscrireAsync("Lina", "Gharbi", "97 111 222", null, null, null, "6ᵉ année de base");
        var sami = await InscrireAsync("Sami", "Chaabane", "97 000 333", null, "50 000 333", "sami@exemple.tn");
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync($"/api/eleves/{sami.Eleve}/encaissement",
            new EncaissementSaisi([new SommeChoisie(sami.Inscription, "Mois 3"), new SommeChoisie(null, "Assurance")]))).StatusCode);
        var rania = await InscrireAsync("Rania", "Trabelsi", "97 000 444", null, "50 000 444", "rania@exemple.tn");
        Assert.Equal(HttpStatusCode.OK, (await _amira.PostAsJsonAsync($"/api/inscriptions/{rania.Inscription}/arret",
            new DateSaisie(new DateOnly(2027, 11, 20)))).StatusCode);
        _application.Emails.Envoyes.Clear();
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau9 => _niveaux.Single(n => n.Nom == "9ᵉ année de base").Id;

    private static EleveCoche Coche((Guid Eleve, Guid Inscription) eleve) =>
        new(eleve.Eleve, [new SommeChoisie(eleve.Inscription, "Mois 3"), new SommeChoisie(null, "Assurance")]);

    [Fact]
    public async Task Ce_que_chacun_doit_dans_le_groupe()
    {
        var encaissement = await EncaissementAsync();

        Assert.Equal(("Groupe Samedi 10h → 12h", "Mathématiques", "Etude"), (encaissement.Groupe.Nom, encaissement.Groupe.Matiere, encaissement.Groupe.Type));
        // Par nom : Gharbi, puis Miled. Le Mois 3 est dû depuis le 13/11 : deux séances sont passées, ils sont en rouge.
        Assert.Equal([("Lina", "Rouge", 60m), ("Adem", "Rouge", 60m)], encaissement.APayer.Select(e => (e.Prenom, e.Couleur, e.Total)));
        Assert.Equal([("Mois 3", 40m, (Guid?)_adem.Inscription), ("Assurance 2027-2028", 20m, null)],
            encaissement.APayer[1].Sommes.Select(s => (s.Libelle, s.Montant, s.InscriptionId)));
        // Sami a tout payé ; Rania, arrêtée, n'est plus dans le groupe.
        Assert.Equal(["Sami"], encaissement.AJour.Select(e => e.Prenom));
    }

    [Fact]
    public async Task Encaisser_deux_eleves_donne_deux_recus_a_la_suite()
    {
        // La page envoie Adem avant Lina : les numéros suivent l'ordre de la page, par nom.
        var reponse = await _amira.PostAsJsonAsync($"/api/groupes/{_maths}/encaissement", new EncaissementGroupeSaisi([Coche(_adem), Coche(_lina)]));

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var recus = (await reponse.Content.ReadFromJsonAsync<RecusDuGroupe>())!.Recus;
        // Le reçu n° 1 est celui de Sami.
        Assert.Equal([(2, "Lina", 60m), (3, "Adem", 60m)], recus.Select(r => (r.Numero, r.Eleve.Prenom, r.Total)));
        Assert.Equal([("Mathématiques · Groupe Samedi 10h → 12h", "Mois 3"), ("Assurance", "Assurance 2027-2028")],
            recus[1].Lignes.Select(l => (l.Groupe, l.Libelle)));

        // Tous les deux sont à jour.
        var apres = await EncaissementAsync();
        Assert.Empty(apres.APayer);
        Assert.Equal(["Sami", "Lina", "Adem"], apres.AJour.Select(e => e.Prenom));

        // Seul Adem a des emails : son reçu part au parent et à lui, avec copie au centre.
        Assert.Equal(["parent.miled@exemple.tn", "adem@exemple.tn", "contact@jasmins.tn"], _application.Emails.Envoyes.Select(e => e.Destinataire));
        Assert.Equal([], recus[0].Emails);

        var lignes = (await _application.JournalAsync(_jasmins)).Where(l => l.Action == ActionDuJournal.Encaissement).Select(l => l.Detail).ToList();
        Assert.Contains("Reçu n° 2 · Lina Gharbi · Mois 3, Assurance 2027-2028", lignes);
        Assert.Contains("Reçu n° 3 · Adem Miled · Mois 3, Assurance 2027-2028", lignes);
    }

    [Fact]
    public async Task Un_cheque_se_note_sur_le_recu_et_se_compte_a_part_de_la_caisse()
    {
        var reponse = await _amira.PostAsJsonAsync($"/api/groupes/{_maths}/encaissement",
            new EncaissementGroupeSaisi([Coche(_adem) with { Mode = "Cheque" }, Coche(_lina)]));

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var recus = (await reponse.Content.ReadFromJsonAsync<RecusDuGroupe>())!.Recus;
        Assert.Equal([("Lina", "Especes"), ("Adem", "Cheque")], recus.Select(r => (r.Eleve.Prenom, r.Mode)));
        Assert.Contains("Total payé par chèque : 60,000 TND", recus[1].Texte);
        Assert.Contains("Total payé en espèces : 60,000 TND", recus[0].Texte);

        // Dans la caisse : les espèces (Lina, et Sami avant elle) sont attendues dans le tiroir ; le chèque est compté à part.
        var caisse = (await _amira.GetFromJsonAsync<MaCaisse>("/api/caisse"))!.Ouvertes.Last();
        Assert.Equal((120m, 120m, 60m, 1), (caisse.Encaisse, caisse.Attendu, caisse.Cheques, caisse.NombreDeCheques));
    }

    [Fact]
    public async Task A_la_date_d_une_seance_a_venir_le_Mois_suivant_est_a_payer()
    {
        // Lina et Adem paient ce qu'ils doivent aujourd'hui : ils sont à jour.
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync($"/api/groupes/{_maths}/encaissement",
            new EncaissementGroupeSaisi([Coche(_adem), Coche(_lina)]))).StatusCode);
        Assert.Empty((await EncaissementAsync()).APayer);

        // Mais à une séance du Mois 4, le Mois 4 sera dû : l'appel de cette séance le montre, et le fait payer d'avance.
        var auMois4 = (await _amira.GetFromJsonAsync<EncaissementDuGroupe>($"/api/groupes/{_maths}/encaissement?au=2028-06-30"))!;
        var lina = auMois4.APayer.Single(e => e.Prenom == "Lina");
        Assert.Contains("Mois 4", lina.Sommes.Select(s => s.Cle));
        Assert.Equal("APayer", lina.Couleur);

        var reponse = await _amira.PostAsJsonAsync($"/api/groupes/{_maths}/encaissement",
            new EncaissementGroupeSaisi([new EleveCoche(_lina.Eleve, [new SommeChoisie(_lina.Inscription, "Mois 4")])], new DateOnly(2028, 6, 30)));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        Assert.DoesNotContain("Mois 4", (await _amira.GetFromJsonAsync<EncaissementDuGroupe>($"/api/groupes/{_maths}/encaissement?au=2028-06-30"))!
            .APayer.Single(e => e.Prenom == "Lina").Sommes.Select(s => s.Cle));
    }

    [Fact]
    public async Task Un_mode_inconnu_est_refuse()
    {
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/groupes/{_maths}/encaissement",
                new EncaissementGroupeSaisi([Coche(_adem) with { Mode = "Carte" }])),
            HttpStatusCode.BadRequest, "Choisissez Espèces ou Chèque");
        Assert.Equal(2, (await EncaissementAsync()).APayer.Count);
    }

    [Fact]
    public async Task Un_eleve_qui_a_paye_entre_temps_refuse_tout()
    {
        // Adem paie son Mois 3 sur sa fiche, pendant que la page du groupe est ouverte.
        Assert.Equal(HttpStatusCode.Created, (await _amira.PostAsJsonAsync($"/api/eleves/{_adem.Eleve}/encaissement",
            new EncaissementSaisi([new SommeChoisie(_adem.Inscription, "Mois 3")]))).StatusCode);

        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/groupes/{_maths}/encaissement", new EncaissementGroupeSaisi([Coche(_lina), Coche(_adem)])),
            HttpStatusCode.Conflict, "Un élève coché a payé entre-temps : rechargez la page");

        // Rien n'est enregistré pour Lina.
        Assert.Equal(60m, (await EncaissementAsync()).APayer.Single(e => e.Prenom == "Lina").Total);
    }

    [Fact]
    public async Task Les_refus()
    {
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/groupes/{_maths}/encaissement", new EncaissementGroupeSaisi([])),
            HttpStatusCode.BadRequest, "Cochez au moins un élève");
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/groupes/{_maths}/encaissement",
                new EncaissementGroupeSaisi([.. Enumerable.Range(0, 101).Select(_ => new EleveCoche(Guid.NewGuid(), []))])),
            HttpStatusCode.BadRequest, "Cochez au plus 100 élèves");
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/groupes/{Guid.NewGuid()}/encaissement", new EncaissementGroupeSaisi([Coche(_lina)])),
            HttpStatusCode.NotFound, "Groupe introuvable");
        // Un élève qui n'est pas dans le groupe, ou une somme qui n'est pas à payer aujourd'hui (le Mois 4).
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/groupes/{_maths}/encaissement",
                new EncaissementGroupeSaisi([new EleveCoche(Guid.NewGuid(), [new SommeChoisie(null, "Assurance")])])),
            HttpStatusCode.Conflict, "Un élève coché a payé entre-temps : rechargez la page");
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/groupes/{_maths}/encaissement",
                new EncaissementGroupeSaisi([new EleveCoche(_lina.Eleve, [new SommeChoisie(_lina.Inscription, "Mois 4")])])),
            HttpStatusCode.Conflict, "Un élève coché a payé entre-temps : rechargez la page");
        Assert.Equal(2, (await EncaissementAsync()).APayer.Count);
    }

    [Fact]
    public async Task Un_autre_centre_ne_voit_pas_le_groupe()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));

        await VerifierLeRefusAsync(await rim.GetAsync($"/api/groupes/{_maths}/encaissement"), HttpStatusCode.NotFound, "Groupe introuvable");
        await VerifierLeRefusAsync(await rim.PostAsJsonAsync($"/api/groupes/{_maths}/encaissement", new EncaissementGroupeSaisi([Coche(_lina)])),
            HttpStatusCode.NotFound, "Groupe introuvable");
    }

    private async Task<EncaissementDuGroupe> EncaissementAsync() =>
        (await _amira.GetFromJsonAsync<EncaissementDuGroupe>($"/api/groupes/{_maths}/encaissement"))!;

    private async Task<(Guid Eleve, Guid Inscription)> InscrireAsync(string prenom, string nom, string parent, string? emailParent, string? telephone, string? email,
        string niveau = "9ᵉ année de base")
    {
        var reponse = await _amira.PostAsJsonAsync("/api/eleves", new FicheSaisieDeLEleve(prenom, nom, _niveaux.Single(n => n.Nom == niveau).Id, false, parent, emailParent, telephone, email, null));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var eleve = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        var inscription = await _amira.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(eleve, _maths, null, Le13Novembre));
        Assert.Equal(HttpStatusCode.Created, inscription.StatusCode);
        return (eleve, (await inscription.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id);
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
