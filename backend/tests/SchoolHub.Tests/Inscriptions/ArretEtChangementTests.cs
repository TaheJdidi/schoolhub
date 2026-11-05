using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Journal;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Inscriptions;

/// <summary>
/// ELV-08 et ELV-09 : arrêter une inscription et changer un élève de groupe. Chaque test part d'un nouveau centre
/// « Jasmins » (année scolaire du 15/09/2027 au 30/06/2028, assurance de 20 TND), avec Karim Ben Salah et Adem Miled ; on
/// est le samedi 27/11/2027. Le groupe d'Étude du samedi a commencé le 18/09 : son Mois 3 va du 13/11 au 04/12, son Mois 4
/// commence le 11/12.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class ArretEtChangementTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "arret_et_changement";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly CreneauSaisi Samedi10h = new("Samedi", "10:00", "12:00");
    private static readonly DateOnly DeuxiemeSeanceDuMois3 = new(2027, 11, 20);
    private static readonly DateOnly DerniereSeanceDuMois3 = new(2027, 12, 4);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private Compte _amira = null!;
    private HttpClient _client = null!;
    private HttpClient _proprietaire = null!;
    private Guid _neuvieme;
    private Guid _karim;
    private Guid _adem;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        _application.Horloge.SetUtcNow(Aujourdhui);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        await _application.RecopierLesNiveauxDOfficeAsync(_jasmins);
        var hedi = await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
        _proprietaire = await _application.ClientConnecteAsync(hedi);
        var reglages = await _proprietaire.PutAsJsonAsync("/api/centre/parametres", new ReglagesDuCentre(
            "Centre Les Jasmins", "73 221 400", null, new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30)), 20m, null, null, null));
        Assert.Equal(HttpStatusCode.OK, reglages.StatusCode);
        _amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        _client = await _application.ClientConnecteAsync(_amira);
        _neuvieme = (await _client.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == "9ᵉ année de base").Id;
        _karim = await ProfesseurAsync(_client);
        _adem = await EleveAsync(_client, _neuvieme);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    /// <summary>Le groupe d'Étude du samedi, à 40 TND le Mois, depuis le 18/09/2027.</summary>
    private CreationDUnGroupe Etude => new(_karim, "Mathématiques", _neuvieme, [Samedi10h], 40m, new DateOnly(2027, 9, 18), null, "");

    // ----- Arrêter (ELV-08) -----

    [Fact]
    public async Task Arreter_une_inscription()
    {
        var inscription = await InscrireAsync(_adem, await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3);

        var arretee = await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/arret", new DateSaisie(DerniereSeanceDuMois3)));

        Assert.Equal(DerniereSeanceDuMois3, arretee.ArreteLe);
        // Le 18/12, le Mois 4 (dû le 11/12, après l'arrêt) n'est pas dû ; le Mois 3 et l'assurance le restent.
        await AllerAuAsync(new DateTimeOffset(2027, 12, 18, 9, 0, 0, TimeSpan.Zero));
        Assert.Equal(["Mois 3 (3 séances)", "Assurance 2027-2028"], (await APayerAsync(_adem)).Lignes.Select(l => l.Libelle));
        var ligne = Assert.Single(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.InscriptionArretee);
        Assert.Equal(("Amira Jlassi", "Adem Miled · Groupe Samedi 10h → 12h (Mathématiques)", "Depuis le 20/11/2027", "Arrêtée le 04/12/2027"),
            (ligne.Auteur, ligne.Detail, ligne.Avant, ligne.Apres));
    }

    [Fact]
    public async Task Un_arret_incomplet_est_refuse()
    {
        var inscription = await InscrireAsync(_adem, await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3);
        var adresse = $"/api/inscriptions/{inscription}/arret";

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(adresse, new DateSaisie(null)), HttpStatusCode.BadRequest, "Choisissez la date de l'arrêt");
        await DecrireAsync(await _client.PostAsJsonAsync(adresse, new DateSaisie(DerniereSeanceDuMois3)));
        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(adresse, new DateSaisie(DerniereSeanceDuMois3)),
            HttpStatusCode.Conflict, "Cette inscription est déjà arrêtée");
        await VerifierLeRefusAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{Guid.NewGuid()}/arret", new DateSaisie(DerniereSeanceDuMois3)),
            HttpStatusCode.NotFound, "Inscription introuvable");
    }

    [Fact]
    public async Task Reinscrire_apres_un_arret()
    {
        var etude = await GroupeAsync(Etude);
        var premiere = await InscrireAsync(_adem, etude, null, DeuxiemeSeanceDuMois3);
        await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{premiere}/arret", new DateSaisie(DerniereSeanceDuMois3)));

        await InscrireAsync(_adem, etude, null, new DateOnly(2027, 12, 11));

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(_adem, etude, null, new DateOnly(2027, 12, 18))),
            HttpStatusCode.Conflict, "Cet élève est déjà inscrit dans ce groupe");
    }

    [Fact]
    public async Task Le_groupe_ne_compte_plus_un_eleve_arrete()
    {
        var etude = await GroupeAsync(Etude);
        var inscription = await InscrireAsync(_adem, etude, null, DeuxiemeSeanceDuMois3);
        await InscrireAsync(await EleveAsync(_client, _neuvieme, "Lina"), etude, null, DeuxiemeSeanceDuMois3);

        // Arrêtée aujourd'hui : encore comptée ; le lendemain, plus.
        await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/arret", new DateSaisie(new DateOnly(2027, 11, 27))));
        Assert.Equal(2, (await _client.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{etude}"))!.Eleves);
        await AllerAuAsync(new DateTimeOffset(2027, 11, 28, 9, 0, 0, TimeSpan.Zero));
        Assert.Equal(1, (await _client.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{etude}"))!.Eleves);
        Assert.Equal(1, (await _client.GetFromJsonAsync<List<ResumeDuGroupe>>("/api/groupes"))!.Single(g => g.Id == etude).Eleves);
    }

    [Fact]
    public async Task Un_eleve_arrete_peut_etre_propose_a_nouveau()
    {
        var etude = await GroupeAsync(Etude);
        var inscription = await InscrireAsync(_adem, etude, null, DeuxiemeSeanceDuMois3);

        await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/arret", new DateSaisie(new DateOnly(2027, 12, 18))));

        var possible = (await _client.GetFromJsonAsync<List<GroupePossible>>($"/api/inscriptions/groupes-possibles?eleve={_adem}"))!.Single(g => g.Id == etude);
        Assert.False(possible.DejaInscrit);
    }

    [Fact]
    public async Task Une_inscription_d_un_autre_centre_ne_s_arrete_pas()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        await _application.RecopierLesNiveauxDOfficeAsync(oliviers);
        var secretaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));
        var niveau = (await secretaire.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == "9ᵉ année de base").Id;
        var eleve = await EleveAsync(secretaire, niveau);
        var formation = (await (await secretaire.PostAsJsonAsync("/api/groupes", new CreationDUnGroupe(
            await ProfesseurAsync(secretaire), "Informatique", null, [Samedi10h], null, new DateOnly(2027, 12, 4), null, "",
            Type: "Formation", PrixTotal: 300m, NombreDeSeances: 10))).Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        var inscription = (await (await secretaire.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(eleve, formation, null, new DateOnly(2027, 12, 4))))
            .Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id;

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/arret", new DateSaisie(DerniereSeanceDuMois3)),
            HttpStatusCode.NotFound, "Inscription introuvable");
    }

    // ----- Changer de groupe (ELV-09) -----

    /// <summary>Une Révision déjà en cours : 100 TND en 10 séances, saisie à la séance 4, le samedi 20/11, à 10h.</summary>
    private CreationDUnGroupe RevisionDejaEnCours => new(
        _karim, "Physique", _neuvieme, [Samedi10h], null, DeuxiemeSeanceDuMois3, null, "",
        Type: "Revision", PrixTotal: 100m, NombreDeSeances: 10, SeanceEnCours: 4);

    /// <summary>Une autre Révision de Physique, le samedi à 14h : 100 TND en 10 séances depuis le 13/11.</summary>
    private CreationDUnGroupe AutreRevision => new(
        _karim, "Physique", _neuvieme, [new CreneauSaisi("Samedi", "14:00", "16:00")], null, new DateOnly(2027, 11, 13), null, "",
        Type: "Revision", PrixTotal: 100m, NombreDeSeances: 10);

    /// <summary>Adem en totalité dans la Révision déjà en cours, ses séances 4 à 10 payées avant SchoolHub (70 TND).</summary>
    private async Task<Guid> RevisionPayeeAsync()
    {
        var inscription = await InscrireAsync(_adem, await GroupeAsync(RevisionDejaEnCours), "EnTotalite", DeuxiemeSeanceDuMois3);
        await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/paye-avant-schoolhub", new CleSaisie("Totalité")));
        return inscription;
    }

    [Fact]
    public async Task L_exemple_du_changement()
    {
        // Le 11/12 (séance 7 de l'ancien groupe), Adem passe dans l'autre Révision, à sa séance 5. Les séances 7 à 10, payées
        // et pas suivies, valent 4 × 10 = 40 TND : la totalité des séances 5 à 10 (60 TND) ne lui coûte plus que 20 TND.
        var ancienne = await RevisionPayeeAsync();
        var autre = await GroupeAsync(AutreRevision);

        var reponse = await _client.PostAsJsonAsync($"/api/inscriptions/{ancienne}/changement",
            new DemandeDeChangement(autre, "EnTotalite", new DateOnly(2027, 12, 11)));

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var changement = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!;
        Assert.Equal((autre, 40m), (changement.Inscription.Groupe.Id, changement.Inscription.Report));
        Assert.Equal([new SommeALArrivee("Totalité, séances 5 à 10", 20m)], changement.APayerALArrivee);
        var inscriptions = (await _client.GetFromJsonAsync<List<InscriptionDeLEleve>>($"/api/inscriptions?eleve={_adem}"))!;
        Assert.Equal(new DateOnly(2027, 12, 10), inscriptions.Single(i => i.Id == ancienne).ArreteLe);
        var ligne = Assert.Single(await _application.JournalAsync(_jasmins), l => l.Action == ActionDuJournal.ChangementDeGroupe);
        Assert.Equal(
            ("Adem Miled · Groupe Samedi 10h → 12h (Physique) → Groupe Samedi 14h → 16h (Physique) · report de 40,000 TND",
                "Groupe Samedi 10h → 12h", "Groupe Samedi 14h → 16h, depuis le 11/12/2027"),
            (ligne.Detail, ligne.Avant, ligne.Apres));
    }

    [Theory]
    [InlineData("sans groupe", HttpStatusCode.BadRequest, "Choisissez le nouveau groupe")]
    [InlineData("groupe inconnu", HttpStatusCode.NotFound, "Groupe introuvable")]
    [InlineData("le même groupe", HttpStatusCode.Conflict, "L'élève est déjà dans ce groupe")]
    [InlineData("un autre type", HttpStatusCode.Conflict, "Le nouveau groupe doit être du même type")]
    [InlineData("sans date", HttpStatusCode.BadRequest, "Choisissez la première séance")]
    [InlineData("date sans séance", HttpStatusCode.BadRequest, "Ce groupe n'a pas de séance ce jour-là")]
    [InlineData("avant la première séance", HttpStatusCode.BadRequest, "Le changement vient avant sa première séance dans l'ancien groupe")]
    [InlineData("inscription inconnue", HttpStatusCode.NotFound, "Inscription introuvable")]
    public async Task Un_changement_incomplet_est_refuse(string cas, HttpStatusCode statut, string message)
    {
        var ancienne = await RevisionPayeeAsync();
        var ancienGroupe = (await _client.GetFromJsonAsync<List<InscriptionDeLEleve>>($"/api/inscriptions?eleve={_adem}"))!.Single().Groupe.Id;
        var autre = await GroupeAsync(AutreRevision);
        var etude = await GroupeAsync(Etude);
        var (id, demande) = cas switch
        {
            "sans groupe" => (ancienne, new DemandeDeChangement(null, "EnTotalite", new DateOnly(2027, 12, 11))),
            "groupe inconnu" => (ancienne, new DemandeDeChangement(Guid.NewGuid(), "EnTotalite", new DateOnly(2027, 12, 11))),
            "le même groupe" => (ancienne, new DemandeDeChangement(ancienGroupe, "EnTotalite", new DateOnly(2027, 12, 11))),
            "un autre type" => (ancienne, new DemandeDeChangement(etude, null, new DateOnly(2027, 12, 11))),
            "sans date" => (ancienne, new DemandeDeChangement(autre, "EnTotalite", null)),
            "date sans séance" => (ancienne, new DemandeDeChangement(autre, "EnTotalite", new DateOnly(2027, 12, 12))),
            "avant la première séance" => (ancienne, new DemandeDeChangement(autre, "EnTotalite", new DateOnly(2027, 11, 13))),
            _ => (Guid.NewGuid(), new DemandeDeChangement(autre, "EnTotalite", new DateOnly(2027, 12, 11))),
        };

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{id}/changement", demande), statut, message);
    }

    [Fact]
    public async Task Deux_changements_en_meme_temps()
    {
        var ancienne = await RevisionPayeeAsync();
        var demande = new DemandeDeChangement(await GroupeAsync(AutreRevision), "EnTotalite", new DateOnly(2027, 12, 11));
        var adresse = $"/api/inscriptions/{ancienne}/changement";

        var reponses = await Task.WhenAll(_client.PostAsJsonAsync(adresse, demande), _client.PostAsJsonAsync(adresse, demande));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], reponses.Select(r => r.StatusCode).Order());
        Assert.Equal(new Avis("Cette inscription est déjà arrêtée"),
            await reponses.Single(r => r.StatusCode == HttpStatusCode.Conflict).Content.ReadFromJsonAsync<Avis>());
        Assert.Equal(2, (await _client.GetFromJsonAsync<List<InscriptionDeLEleve>>($"/api/inscriptions?eleve={_adem}"))!.Count);
    }

    [Fact]
    public async Task Les_groupes_possibles_d_un_changement()
    {
        var ancienne = await RevisionPayeeAsync();
        var autre = await GroupeAsync(AutreRevision);
        await GroupeAsync(Etude);

        var groupes = (await _client.GetFromJsonAsync<List<GroupePossible>>($"/api/inscriptions/groupes-possibles?eleve={_adem}&depuis={ancienne}"))!;

        Assert.Equal([autre], groupes.Select(g => g.Id));
        await VerifierLeRefusAsync(await _client.GetAsync($"/api/inscriptions/groupes-possibles?eleve={_adem}&depuis={Guid.NewGuid()}"),
            HttpStatusCode.NotFound, "Inscription introuvable");
    }

    // ----- Corrections de la relecture -----

    private async Task<EnregistrementDeLInscription> ChangerAsync(Guid inscription, DemandeDeChangement demande)
    {
        var reponse = await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/changement", demande);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!;
    }

    private async Task<InscriptionDeLEleve> InscriptionAsync(Guid inscription) =>
        (await _client.GetFromJsonAsync<List<InscriptionDeLEleve>>($"/api/inscriptions?eleve={_adem}"))!.Single(i => i.Id == inscription);

    [Fact]
    public async Task Un_paiement_note_apres_le_changement_donne_son_report()
    {
        // La totalité a été payée en espèces avant SchoolHub, mais la secrétaire ne le note qu'après le changement.
        var ancienne = await InscrireAsync(_adem, await GroupeAsync(RevisionDejaEnCours), "EnTotalite", DeuxiemeSeanceDuMois3);
        var changement = await ChangerAsync(ancienne, new DemandeDeChangement(await GroupeAsync(AutreRevision), "EnTotalite", new DateOnly(2027, 12, 11)));
        Assert.Equal(0m, changement.Inscription.Report);

        await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{ancienne}/paye-avant-schoolhub", new CleSaisie("Totalité")));

        Assert.Equal(40m, (await InscriptionAsync(changement.Inscription.Id)).Report);
    }

    [Fact]
    public async Task Un_second_changement_garde_le_report()
    {
        // Le jour même, la secrétaire voit qu'elle s'est trompée de groupe, et change encore : les 40 TND suivent.
        var ancienne = await RevisionPayeeAsync();
        var versB = await ChangerAsync(ancienne, new DemandeDeChangement(await GroupeAsync(AutreRevision), "EnTotalite", new DateOnly(2027, 12, 11)));
        var c = await GroupeAsync(AutreRevision with { Creneaux = [new CreneauSaisi("Samedi", "16:00", "18:00")] });

        var versC = await ChangerAsync(versB.Inscription.Id, new DemandeDeChangement(c, "EnTotalite", new DateOnly(2027, 12, 11)));

        Assert.Equal(40m, versC.Inscription.Report);
        Assert.Equal([new SommeALArrivee("Totalité, séances 5 à 10", 20m)], versC.APayerALArrivee);
    }

    [Fact]
    public async Task Un_groupe_ne_ferme_pas_avec_des_eleves_encore_inscrits()
    {
        var etude = await GroupeAsync(Etude);
        var inscription = await InscrireAsync(_adem, etude, null, DeuxiemeSeanceDuMois3);
        var fermeture = new FermetureDUnGroupe(new DateOnly(2027, 11, 27), "Trop peu d'élèves");

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync($"/api/groupes/{etude}/fermeture", fermeture), HttpStatusCode.Conflict,
            "Des élèves sont encore inscrits après cette date : arrêtez leur inscription ou changez-les de groupe d'abord");

        await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/arret", new DateSaisie(new DateOnly(2027, 11, 27))));
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync($"/api/groupes/{etude}/fermeture", fermeture)).StatusCode);
    }

    [Fact]
    public async Task Une_inscription_defaite_ne_laisse_pas_d_assurance()
    {
        // Inscrit pour le 04/12, arrêté le 27/11 : l'inscription n'a jamais commencé, l'assurance n'est pas due.
        var inscription = await InscrireAsync(_adem, await GroupeAsync(Etude), null, DerniereSeanceDuMois3);
        await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/arret", new DateSaisie(new DateOnly(2027, 11, 27))));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.GetAsync($"/api/eleves/{_adem}/assurance")).StatusCode);
        await AllerAuAsync(new DateTimeOffset(2027, 12, 11, 9, 0, 0, TimeSpan.Zero));
        Assert.Empty((await APayerAsync(_adem)).Lignes);
    }

    [Fact]
    public async Task Une_reinscription_ne_chevauche_pas_l_arret()
    {
        var etude = await GroupeAsync(Etude);
        var inscription = await InscrireAsync(_adem, etude, null, DeuxiemeSeanceDuMois3);
        await DecrireAsync(await _client.PostAsJsonAsync($"/api/inscriptions/{inscription}/arret", new DateSaisie(new DateOnly(2028, 6, 30))));

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(_adem, etude, null, new DateOnly(2027, 12, 11))),
            HttpStatusCode.Conflict, "Cet élève est déjà inscrit dans ce groupe");
    }

    [Fact]
    public async Task Le_prix_special_ne_suit_pas_le_changement()
    {
        var ancienne = await RevisionPayeeAsync();
        Assert.Equal(HttpStatusCode.OK, (await _proprietaire.PutAsJsonAsync($"/api/inscriptions/{ancienne}/prix-special",
            new PrixSpecialSaisi(50m, "Famille en difficulté"))).StatusCode);

        var changement = await ChangerAsync(ancienne, new DemandeDeChangement(await GroupeAsync(AutreRevision), "EnTotalite", new DateOnly(2027, 12, 11)));

        Assert.Equal("Le prix spécial de l'ancien groupe ne suit pas : prévenez le propriétaire.", changement.Avertissement);
    }

    // ----- Outils -----

    private async Task AllerAuAsync(DateTimeOffset moment)
    {
        _application.Horloge.SetUtcNow(moment);
        _client = await _application.ClientConnecteAsync(_amira);
    }

    private static async Task<Guid> ProfesseurAsync(HttpClient client)
    {
        var reponse = await client.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null));
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
    }

    private static async Task<Guid> EleveAsync(HttpClient client, Guid niveau, string prenom = "Adem")
    {
        var reponse = await client.PostAsJsonAsync("/api/eleves",
            new FicheSaisieDeLEleve(prenom, "Miled", niveau, false, "98 401 220", null, "22 345 678", "adem@exemple.tn", null));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
    }

    private async Task<Guid> GroupeAsync(CreationDUnGroupe creation)
    {
        var reponse = await _client.PostAsJsonAsync("/api/groupes", creation);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
    }

    private async Task<Guid> InscrireAsync(Guid eleve, Guid groupe, string? mode, DateOnly debutLe)
    {
        var reponse = await _client.PostAsJsonAsync("/api/inscriptions", new InscriptionSaisie(eleve, groupe, mode, debutLe));
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!.Inscription.Id;
    }

    private async Task<APayer> APayerAsync(Guid eleve) => (await _client.GetFromJsonAsync<APayer>($"/api/eleves/{eleve}/a-payer"))!;

    private static async Task<InscriptionDeLEleve> DecrireAsync(HttpResponseMessage reponse)
    {
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<InscriptionDeLEleve>())!;
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
