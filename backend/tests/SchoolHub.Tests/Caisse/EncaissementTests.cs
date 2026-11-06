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
/// CAI-03, CAI-05 à CAI-07 : encaisser, remettre, retrouver et annuler un reçu. Chaque test part d'un nouveau centre
/// « Jasmins » (téléphone 71 234 567, email du centre, assurance de 20 TND, année scolaire du 15/09/2027 au 30/06/2028)
/// et du groupe de Mathématiques de Karim Ben Salah, le samedi de 10h à 12h depuis le 18/09/2027, à 40 TND le Mois. Adem
/// Miled y est inscrit depuis le 13/11, la première séance du Mois 3. On est le samedi 27/11/2027 à 10h à Tunis.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class EncaissementTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "encaissement";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _proprietaire = null!;
    private HttpClient _amira = null!;
    private Compte _compteDAmira = null!;
    private Compte _compteDeHedi = null!;
    private List<NiveauDuCentre> _niveaux = null!;
    private Guid _maths;
    private Guid _adem;
    private Guid _inscriptionDAdem;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        _compteDeHedi = await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
        _proprietaire = await _application.ClientConnecteAsync(_compteDeHedi);
        Assert.Equal(HttpStatusCode.OK, (await _proprietaire.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", "71 234 567", "contact@jasmins.tn", new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)),
            20m, null, null, null))).StatusCode);
        _compteDAmira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        _amira = await _application.ClientConnecteAsync(_compteDAmira);
        _niveaux = (await _amira.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        var karim = (await (await _amira.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null)))
            .Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
        var creation = await _amira.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(karim, "Mathématiques", Niveau("9ᵉ année de base"),
            [new CreneauSaisi("Samedi", "10:00", "12:00")], 40m, new DateOnly(2027, 9, 18), null, ""));
        _maths = (await creation.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        (_adem, _inscriptionDAdem) = await InscrireAsync(new FicheSaisieDeLEleve("Adem", "Miled", Niveau("9ᵉ année de base"), false, "98 401 220",
            "parent.miled@exemple.tn", "22 345 678", "adem@exemple.tn", null), new DateOnly(2027, 11, 13));
        _application.Emails.Envoyes.Clear();
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau(string nom) => _niveaux.Single(n => n.Nom == nom).Id;

    private SommeChoisie Mois(int numero) => new(_inscriptionDAdem, $"Mois {numero}");

    private static readonly SommeChoisie Assurance = new(null, "Assurance");

    // ----- Ce qui peut se payer -----

    [Fact]
    public async Task Ce_que_l_eleve_peut_payer()
    {
        var encaissable = (await _amira.GetFromJsonAsync<Encaissable>($"/api/eleves/{_adem}/encaissable"))!;

        Assert.True(encaissable.AUnEmail);
        Assert.Equal(
            [("Mois 3", 40m, true), ("Assurance 2027-2028", 20m, true), ("Mois 4", 40m, false)],
            encaissable.Sommes.Take(3).Select(s => (s.Libelle, s.Montant, s.Due)));
        Assert.Equal(("Mathématiques · Groupe Samedi 10h → 12h", "Mois 3", _inscriptionDAdem),
            (encaissable.Sommes[0].Groupe, encaissable.Sommes[0].Cle, encaissable.Sommes[0].InscriptionId));
    }

    // ----- Encaisser (CAI-03) -----

    [Fact]
    public async Task Encaisser_le_mois_3_et_le_mois_4_d_avance()
    {
        var recu = await EncaisserAsync(_amira, _adem, Mois(3), Mois(4));

        Assert.Equal((1, 80m, "Amira Jlassi", false), (recu.Numero, recu.Total, recu.Secretaire, recu.Annule));
        Assert.Equal(
            "Reçu n° 1 · Centre Les Jasmins, 71 234 567\n"
            + "Le 27/11/2027\n"
            + "Élève : Adem Miled\n"
            + "• Mathématiques · Groupe Samedi 10h → 12h, Mois 3 : 40,000 TND\n"
            + "• Mathématiques · Groupe Samedi 10h → 12h, Mois 4 : 40,000 TND\n"
            + "Total payé en espèces : 80,000 TND\n"
            + "Reçu par Amira Jlassi",
            recu.Texte);
        Assert.Equal(["Parent", "Adem"], recu.Messages.Select(m => m.Destinataire));
        Assert.True(recu.Annulable);

        // Il ne reste que l'assurance à payer aujourd'hui.
        var aPayer = (await _amira.GetFromJsonAsync<APayer>($"/api/eleves/{_adem}/a-payer"))!;
        Assert.Equal([("Assurance 2027-2028", 20m)], aPayer.Lignes.Select(l => (l.Libelle, l.Montant)));

        // Le reçu est parti par email au parent et à l'élève, avec copie au centre.
        Assert.Equal(["parent.miled@exemple.tn", "adem@exemple.tn", "contact@jasmins.tn"], _application.Emails.Envoyes.Select(e => e.Destinataire));
        Assert.All(_application.Emails.Envoyes, e => Assert.Equal("Reçu n° 1 · Centre Les Jasmins", e.Sujet));

        var ligne = Assert.Single(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.Encaissement);
        Assert.Equal(("Reçu n° 1 · Adem Miled · Mois 3, Mois 4", "Encaissé, 80,000 TND"), (ligne.Detail, ligne.Apres));
    }

    [Fact]
    public async Task L_assurance_s_encaisse_aussi()
    {
        var recu = await EncaisserAsync(_amira, _adem, Mois(3), Assurance);

        Assert.Equal(60m, recu.Total);
        Assert.Contains("• Assurance 2027-2028 : 20,000 TND", recu.Texte, StringComparison.Ordinal);
        Assert.Empty((await _amira.GetFromJsonAsync<APayer>($"/api/eleves/{_adem}/a-payer"))!.Lignes);
        Assert.Equal("Payee", (await _amira.GetFromJsonAsync<AssuranceDeLEleve>($"/api/eleves/{_adem}/assurance"))!.Etat);
    }

    [Fact]
    public async Task Les_recus_se_numerotent_a_la_suite_pour_tout_le_centre()
    {
        var (lina, inscriptionDeLina) = await InscrireAsync(new FicheSaisieDeLEleve("Lina", "Gharbi", Niveau("9ᵉ année de base"), false, "97 111 222",
            null, "50 111 222", "lina@exemple.tn", null), new DateOnly(2027, 11, 20));

        var premier = await EncaisserAsync(_amira, _adem, Mois(3));
        var deuxieme = await EncaisserAsync(_proprietaire, lina, new SommeChoisie(inscriptionDeLina, "Mois 3"));
        var troisieme = await EncaisserAsync(_amira, _adem, Mois(4));

        Assert.Equal([1, 2, 3], new[] { premier.Numero, deuxieme.Numero, troisieme.Numero });
        Assert.Equal("Hedi Ben Salah", deuxieme.Secretaire);

        // Un autre centre a sa propre suite.
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        Assert.Empty((await (await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi")))
            .GetFromJsonAsync<List<RecuDeLaListe>>("/api/recus"))!);
    }

    [Theory]
    [InlineData("rien", HttpStatusCode.BadRequest, "Cochez au moins une somme")]
    [InlineData("déjà payée", HttpStatusCode.Conflict, "Une somme cochée n'est plus à payer : rechargez la page")]
    [InlineData("inconnue", HttpStatusCode.Conflict, "Une somme cochée n'est plus à payer : rechargez la page")]
    public async Task Un_encaissement_refuse(string cas, HttpStatusCode statut, string message)
    {
        if (cas == "déjà payée")
            await EncaisserAsync(_amira, _adem, Mois(3));
        var saisie = cas switch
        {
            "rien" => new EncaissementSaisi([]),
            "inconnue" => new EncaissementSaisi([new SommeChoisie(_inscriptionDAdem, "Mois 99")]),
            _ => new EncaissementSaisi([Mois(3)]),
        };

        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/eleves/{_adem}/encaissement", saisie), statut, message);
    }

    // ----- Retrouver (CAI-06) -----

    [Fact]
    public async Task Retrouver_un_recu()
    {
        var recu = await EncaisserAsync(_amira, _adem, Mois(3));
        await LeLendemainAsync();
        await EncaisserAsync(_amira, _adem, Mois(4));

        Assert.Equal([2, 1], (await ListeAsync("")).Select(r => r.Numero));
        Assert.Equal([1], (await ListeAsync("?jour=2027-11-27")).Select(r => r.Numero));
        Assert.Equal([1], (await ListeAsync("?numero=1")).Select(r => r.Numero));
        Assert.Equal([2, 1], (await ListeAsync("?eleve=miled")).Select(r => r.Numero));
        Assert.Empty(await ListeAsync("?eleve=gharbi"));
        Assert.Equal(("Adem Miled", 40m, "Amira Jlassi"), ((await ListeAsync("?numero=1"))[0].Eleve, (await ListeAsync("?numero=1"))[0].Total,
            (await ListeAsync("?numero=1"))[0].Secretaire));
        Assert.Equal(recu.Numero, (await _amira.GetFromJsonAsync<RecuDecrit>($"/api/recus/{recu.Id}"))!.Numero);
    }

    // ----- Annuler (CAI-07) -----

    [Fact]
    public async Task Annuler_son_recu_du_jour()
    {
        var recu = await EncaisserAsync(_amira, _adem, Mois(3), Mois(4));

        var annule = await AnnulerAsync(_amira, recu.Id, "Mauvais élève sélectionné");

        Assert.Equal((true, "Mauvais élève sélectionné", false), (annule.Annule, annule.MotifDAnnulation, annule.Annulable));
        Assert.StartsWith("REÇU ANNULÉ : Mauvais élève sélectionné\nReçu n° 1", annule.Texte, StringComparison.Ordinal);
        // Ce qui était payé redevient à payer.
        Assert.Contains((await _amira.GetFromJsonAsync<APayer>($"/api/eleves/{_adem}/a-payer"))!.Lignes, l => l.Libelle == "Mois 3");
        Assert.True((await ListeAsync("")).Single().Annule);
        var ligne = Assert.Single(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.RecuAnnule);
        Assert.Equal(("Reçu n° 1 · Adem Miled · Mauvais élève sélectionné", "Encaissé, 80,000 TND", "Annulé"), (ligne.Detail, ligne.Avant, ligne.Apres));
        // La somme se paie de nouveau, avec un nouveau numéro.
        Assert.Equal(2, (await EncaisserAsync(_amira, _adem, Mois(3))).Numero);
    }

    [Fact]
    public async Task Seul_le_proprietaire_annule_le_recu_d_une_autre_secretaire_ou_d_hier()
    {
        var sonia = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(_jasmins, "Sonia", "Mabrouk"));
        var recu = await EncaisserAsync(_amira, _adem, Mois(3));

        Assert.False((await sonia.GetFromJsonAsync<RecuDecrit>($"/api/recus/{recu.Id}"))!.Annulable);
        await VerifierLeRefusAsync(await sonia.PostAsJsonAsync($"/api/recus/{recu.Id}/annulation", new AnnulationDuRecu("Erreur")),
            HttpStatusCode.Forbidden, "Ce reçu a été fait par une autre secrétaire : seul le propriétaire peut l'annuler");

        await LeLendemainAsync();
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/recus/{recu.Id}/annulation", new AnnulationDuRecu("Erreur")),
            HttpStatusCode.Forbidden, "Ce reçu date d'avant aujourd'hui : seul le propriétaire peut l'annuler");

        Assert.True((await AnnulerAsync(_proprietaire, recu.Id, "Erreur de saisie")).Annule);
    }

    [Fact]
    public async Task Une_annulation_refusee()
    {
        var recu = await EncaisserAsync(_amira, _adem, Mois(3));

        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/recus/{recu.Id}/annulation", new AnnulationDuRecu(" ")),
            HttpStatusCode.BadRequest, "Écrivez le motif de l'annulation");
        await AnnulerAsync(_amira, recu.Id, "Erreur");
        await VerifierLeRefusAsync(await _amira.PostAsJsonAsync($"/api/recus/{recu.Id}/annulation", new AnnulationDuRecu("Erreur")),
            HttpStatusCode.Conflict, "Ce reçu est déjà annulé");
    }

    // ----- Remettre (CAI-05) -----

    [Fact]
    public async Task Renvoyer_l_email()
    {
        var recu = await EncaisserAsync(_amira, _adem, Mois(3));
        _application.Emails.Envoyes.Clear();

        Assert.Equal(HttpStatusCode.OK, (await _amira.PostAsync($"/api/recus/{recu.Id}/email", null)).StatusCode);

        Assert.Equal(3, _application.Emails.Envoyes.Count);
    }

    [Fact]
    public async Task Sans_email_le_recu_se_remet_par_whatsapp()
    {
        var (sami, inscription) = await InscrireAsync(new FicheSaisieDeLEleve("Sami", "Chaabane", Niveau("6ᵉ année de base"), false, "97 000 333",
            null, null, null, null), new DateOnly(2027, 11, 20));

        var recu = await EncaisserAsync(_amira, sami, new SommeChoisie(inscription, "Mois 3"));

        Assert.Empty(recu.Emails);
        Assert.Empty(_application.Emails.Envoyes);
        Assert.Equal(["Parent"], recu.Messages.Select(m => m.Destinataire));
        await VerifierLeRefusAsync(await _amira.PostAsync($"/api/recus/{recu.Id}/email", null), HttpStatusCode.BadRequest,
            "Aucun email n'est connu pour cet élève. Ajoutez-en un sur sa fiche, ou utilisez WhatsApp.");
    }

    [Fact]
    public async Task Un_email_qui_echoue_ne_defait_pas_l_encaissement()
    {
        _application.Emails.Echouer = true;

        var recu = await EncaisserAsync(_amira, _adem, Mois(3));

        Assert.Equal(1, recu.Numero);
        Assert.DoesNotContain((await _amira.GetFromJsonAsync<APayer>($"/api/eleves/{_adem}/a-payer"))!.Lignes, l => l.Libelle == "Mois 3");
    }

    // ----- La séparation des centres -----

    [Fact]
    public async Task Un_recu_d_un_autre_centre_est_introuvable()
    {
        var recu = await EncaisserAsync(_amira, _adem, Mois(3));
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var rim = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi", RoleDuCompte.Proprietaire));

        await VerifierLeRefusAsync(await rim.GetAsync($"/api/recus/{recu.Id}"), HttpStatusCode.NotFound, "Reçu introuvable");
        await VerifierLeRefusAsync(await rim.PostAsJsonAsync($"/api/recus/{recu.Id}/annulation", new AnnulationDuRecu("Erreur")),
            HttpStatusCode.NotFound, "Reçu introuvable");
        await VerifierLeRefusAsync(await rim.PostAsJsonAsync($"/api/eleves/{_adem}/encaissement", new EncaissementSaisi([Mois(3)])),
            HttpStatusCode.NotFound, "Élève introuvable");
    }

    // ----- Outils -----

    /// <summary>Le lendemain : la session de 12 heures a expiré, chacun se reconnecte.</summary>
    private async Task LeLendemainAsync()
    {
        _application.Horloge.Advance(TimeSpan.FromDays(1));
        _amira = await _application.ClientConnecteAsync(_compteDAmira);
        _proprietaire = await _application.ClientConnecteAsync(_compteDeHedi);
    }

    private async Task<(Guid Eleve, Guid Inscription)> InscrireAsync(FicheSaisieDeLEleve fiche, DateOnly debutLe)
    {
        var reponse = await _amira.PostAsJsonAsync("/api/eleves", fiche);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var eleve = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        var inscription = await _amira.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(eleve, _maths, null, debutLe));
        Assert.Equal(HttpStatusCode.Created, inscription.StatusCode);
        return (eleve, (await inscription.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id);
    }

    private static async Task<RecuDecrit> EncaisserAsync(HttpClient client, Guid eleve, params SommeChoisie[] sommes)
    {
        var reponse = await client.PostAsJsonAsync($"/api/eleves/{eleve}/encaissement", new EncaissementSaisi([.. sommes]));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<RecuDecrit>())!;
    }

    private static async Task<RecuDecrit> AnnulerAsync(HttpClient client, Guid recu, string motif)
    {
        var reponse = await client.PostAsJsonAsync($"/api/recus/{recu}/annulation", new AnnulationDuRecu(motif));
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<RecuDecrit>())!;
    }

    private async Task<List<RecuDeLaListe>> ListeAsync(string filtres) => (await _amira.GetFromJsonAsync<List<RecuDeLaListe>>($"/api/recus{filtres}"))!;

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
