using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Inscriptions;

/// <summary>
/// ELV-03 : inscrire un élève dans un groupe. Chaque test part d'un nouveau centre « Jasmins » (année scolaire du
/// 15/09/2027 au 30/06/2028, assurance de 20 TND, téléphone 73 221 400), avec Karim Ben Salah et Adem Miled ; on est le
/// mercredi 10/11/2027. Le groupe d'Étude du samedi a commencé le 18/09 : son Mois 3 va du 13/11 au 04/12.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class InscriptionsTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "inscriptions";
    private const string Adresse = "/api/inscriptions";

    private static readonly DateTimeOffset Aujourdhui = new(2027, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly CreneauSaisi Samedi10h = new("Samedi", "10:00", "12:00");
    private static readonly AnneeScolaireSaisie Annee = new(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30));

    /// <summary>La 2ᵉ séance du Mois 3 du groupe d'Étude.</summary>
    private static readonly DateOnly DeuxiemeSeanceDuMois3 = new(2027, 11, 20);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private Compte _amira = null!;
    private HttpClient _client = null!;
    private HttpClient _proprietaire = null!;
    private List<NiveauDuCentre> _niveaux = null!;
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
        await ReglerLeCentreAsync("73 221 400", 20m);
        _amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        _client = await _application.ClientConnecteAsync(_amira);
        _niveaux = (await _client.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        _karim = await ProfesseurAsync(_client);
        _adem = await EleveAsync(Adem);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private Guid Niveau(string nom) => _niveaux.Single(n => n.Nom == nom).Id;

    /// <summary>Adem Miled, en 9ᵉ année de base, mineur : son téléphone et celui de son parent.</summary>
    private FicheSaisieDeLEleve Adem => new(
        "Adem", "Miled", Niveau("9ᵉ année de base"), false, "98 401 220", null, "22 345 678", "adem@exemple.tn", null);

    /// <summary>Le groupe d'Étude du samedi, à 40 TND le Mois, depuis le 18/09/2027.</summary>
    private CreationDUnGroupe Etude => new(
        _karim, "Mathématiques", Niveau("9ᵉ année de base"), [Samedi10h], 40m, new DateOnly(2027, 9, 18), null, "");

    /// <summary>Une Révision de 10 séances à 100 TND, à partir du samedi 13/11/2027.</summary>
    private CreationDUnGroupe Revision => new(
        _karim, "Physique", Niveau("9ᵉ année de base"), [Samedi10h], null, new DateOnly(2027, 11, 13), null, "",
        Type: "Revision", PrixTotal: 100m, NombreDeSeances: 10);

    /// <summary>Une Formation sans niveau, 300 TND en 10 séances, en trois tranches de 100 TND aux séances 1, 5 et 9.</summary>
    private CreationDUnGroupe Formation => new(
        _karim, "Informatique", null, [Samedi10h], null, new DateOnly(2027, 11, 13), null, "",
        Type: "Formation", PrixTotal: 300m, NombreDeSeances: 10,
        Tranches: [new TrancheSaisie(100m, 1), new TrancheSaisie(100m, 5), new TrancheSaisie(100m, 9)]);

    // ----- Inscrire -----

    [Fact]
    public async Task Inscrire_dans_une_etude()
    {
        var etude = await GroupeAsync(Etude);

        var reponse = await _client.PostAsJsonAsync(Adresse, new InscriptionSaisie(_adem, etude, null, DeuxiemeSeanceDuMois3));

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var enregistrement = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!;
        var inscription = enregistrement.Inscription;
        Assert.Equal((_adem, "Adem", "Miled"), (inscription.Eleve.Id, inscription.Eleve.Prenom, inscription.Eleve.Nom));
        Assert.Equal((etude, "Groupe Samedi 10h → 12h", "Etude", "Mathématiques"),
            (inscription.Groupe.Id, inscription.Groupe.Nom, inscription.Groupe.Type, inscription.Groupe.Matiere));
        Assert.Equal(("Karim Ben Salah", "ParMois", 40m, 0), (inscription.Professeur, inscription.Mode, inscription.PrixNormal, inscription.NombreDeTranches));
        Assert.Equal(new SeanceDeDebut(DeuxiemeSeanceDuMois3, new TimeOnly(10, 0)), inscription.PremiereSeance);
        Assert.Equal([new SommeALArrivee("Mois 3 (3 séances)", 30m), new SommeALArrivee("Assurance 2027-2028", 20m)], enregistrement.APayerALArrivee);
        Assert.Null(enregistrement.Avertissement);
    }

    [Fact]
    public async Task Inscrire_dans_une_revision_ou_une_formation()
    {
        var revision = await GroupeAsync(Revision);
        var formation = await GroupeAsync(Formation);

        var enTotalite = await InscrireAsync(new InscriptionSaisie(_adem, revision, "EnTotalite", new DateOnly(2027, 11, 13)));
        var enTranches = await InscrireAsync(new InscriptionSaisie(_adem, formation, "EnTranches", new DateOnly(2027, 11, 13)));

        Assert.Equal(("EnTotalite", 100m), (enTotalite.Inscription.Mode, enTotalite.Inscription.PrixNormal));
        Assert.Equal([new SommeALArrivee("Totalité", 100m)], enTotalite.APayerALArrivee);
        Assert.Equal(("EnTranches", 300m, 3), (enTranches.Inscription.Mode, enTranches.Inscription.PrixNormal, enTranches.Inscription.NombreDeTranches));
        Assert.Equal([new SommeALArrivee("Tranche 1", 100m)], enTranches.APayerALArrivee);
    }

    [Theory]
    [InlineData("sans élève", HttpStatusCode.BadRequest, "Choisissez l'élève")]
    [InlineData("élève inconnu", HttpStatusCode.NotFound, "Élève introuvable")]
    [InlineData("sans groupe", HttpStatusCode.BadRequest, "Choisissez le groupe")]
    [InlineData("groupe inconnu", HttpStatusCode.NotFound, "Groupe introuvable")]
    [InlineData("sans mode", HttpStatusCode.BadRequest, "Choisissez le mode de tarification")]
    [InlineData("mode inconnu", HttpStatusCode.BadRequest, "Ce mode de tarification ne convient pas à ce groupe")]
    [InlineData("mode d'un autre type", HttpStatusCode.BadRequest, "Ce mode de tarification ne convient pas à ce groupe")]
    [InlineData("sans date", HttpStatusCode.BadRequest, "Choisissez la première séance")]
    public async Task Une_demande_incomplete_est_refusee(string cas, HttpStatusCode statut, string message)
    {
        var etude = await GroupeAsync(Etude);
        var revision = await GroupeAsync(Revision);
        var date = new DateOnly(2027, 11, 13);
        var demande = cas switch
        {
            "sans élève" => new InscriptionSaisie(null, etude, null, date),
            "élève inconnu" => new InscriptionSaisie(Guid.NewGuid(), etude, null, date),
            "sans groupe" => new InscriptionSaisie(_adem, null, null, date),
            "groupe inconnu" => new InscriptionSaisie(_adem, Guid.NewGuid(), null, date),
            "sans mode" => new InscriptionSaisie(_adem, revision, null, date),
            "mode inconnu" => new InscriptionSaisie(_adem, revision, "Gratuit", date),
            "mode d'un autre type" => new InscriptionSaisie(_adem, etude, "EnTotalite", date),
            _ => new InscriptionSaisie(_adem, etude, null, null),
        };

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, demande), statut, message);
    }

    [Fact]
    public async Task Une_date_sans_seance_est_refusee()
    {
        var etude = await GroupeAsync(Etude);

        // Un dimanche, puis un samedi d'avant le début du groupe.
        foreach (var date in new[] { new DateOnly(2027, 11, 14), new DateOnly(2027, 9, 11) })
        {
            await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, new InscriptionSaisie(_adem, etude, null, date)),
                HttpStatusCode.BadRequest, "Ce groupe n'a pas de séance ce jour-là");
        }
    }

    [Fact]
    public async Task Deja_inscrit()
    {
        var etude = await GroupeAsync(Etude);
        await InscrireAsync(new InscriptionSaisie(_adem, etude, null, DeuxiemeSeanceDuMois3));

        await VerifierLeRefusAsync(
            await _client.PostAsJsonAsync(Adresse, new InscriptionSaisie(_adem, etude, null, new DateOnly(2027, 11, 27))),
            HttpStatusCode.Conflict, "Cet élève est déjà inscrit dans ce groupe");
    }

    [Fact]
    public async Task Un_groupe_ferme_ou_termine_est_refuse()
    {
        var etude = await GroupeAsync(Etude);
        var fermeture = await _client.PostAsJsonAsync($"/api/groupes/{etude}/fermeture", new FermetureDUnGroupe(new DateOnly(2027, 11, 10), "Trop peu d'élèves"));
        Assert.Equal(HttpStatusCode.OK, fermeture.StatusCode);
        await VerifierLeRefusAsync(
            await _client.PostAsJsonAsync(Adresse, new InscriptionSaisie(_adem, etude, null, new DateOnly(2027, 11, 6))),
            HttpStatusCode.Conflict, "Ce groupe est fermé : on n'y inscrit plus");

        // Une Révision de 2 séances, les 13 et 20/11 : elle est terminée le 21.
        var courte = await GroupeAsync(Revision with { NombreDeSeances = 2 });
        await AllerAuAsync(new DateTimeOffset(2027, 11, 21, 9, 0, 0, TimeSpan.Zero));
        await VerifierLeRefusAsync(
            await _client.PostAsJsonAsync(Adresse, new InscriptionSaisie(_adem, courte, "EnTotalite", new DateOnly(2027, 11, 20))),
            HttpStatusCode.Conflict, "Ce groupe est terminé : on n'y inscrit plus");
    }

    [Fact]
    public async Task Deux_inscriptions_en_meme_temps()
    {
        var etude = await GroupeAsync(Etude);
        var demande = new InscriptionSaisie(_adem, etude, null, DeuxiemeSeanceDuMois3);

        var reponses = await Task.WhenAll(_client.PostAsJsonAsync(Adresse, demande), _client.PostAsJsonAsync(Adresse, demande));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], reponses.Select(r => r.StatusCode).Order());
        Assert.Equal(1, await CompterAsync(contexte => contexte.Inscriptions));
        Assert.Equal(1, await CompterAsync(contexte => contexte.Assurances));
    }

    [Fact]
    public async Task La_capacite_previent_sans_bloquer()
    {
        var etude = await GroupeAsync(Etude with { Capacite = 1 });
        var lina = await EleveAsync(Lina);

        var premier = await InscrireAsync(new InscriptionSaisie(_adem, etude, null, DeuxiemeSeanceDuMois3));
        var second = await InscrireAsync(new InscriptionSaisie(lina, etude, null, DeuxiemeSeanceDuMois3));

        Assert.Null(premier.Avertissement);
        Assert.Equal("Ce groupe a atteint sa capacité. L'inscription reste possible.", second.Avertissement);
    }

    [Fact]
    public async Task L_assurance_n_est_due_qu_une_fois_et_seulement_pour_une_etude()
    {
        var revision = await GroupeAsync(Revision);
        var etude = await GroupeAsync(Etude);
        var autreEtude = await GroupeAsync(Etude with { Matiere = "Arabe" });

        Assert.DoesNotContain((await InscrireAsync(new InscriptionSaisie(_adem, revision, "EnTotalite", new DateOnly(2027, 11, 13)))).APayerALArrivee,
            s => s.Libelle.StartsWith("Assurance", StringComparison.Ordinal));
        Assert.Contains((await InscrireAsync(new InscriptionSaisie(_adem, etude, null, DeuxiemeSeanceDuMois3))).APayerALArrivee,
            s => s.Libelle == "Assurance 2027-2028");
        Assert.DoesNotContain((await InscrireAsync(new InscriptionSaisie(_adem, autreEtude, null, DeuxiemeSeanceDuMois3))).APayerALArrivee,
            s => s.Libelle.StartsWith("Assurance", StringComparison.Ordinal));
        Assert.Equal(1, await CompterAsync(contexte => contexte.Assurances));
    }

    [Fact]
    public async Task L_assurance_s_annonce_a_la_seance_ou_elle_devient_due()
    {
        // Adem commence les Maths le 27/11 : l'assurance y est due. Il commence ensuite l'Arabe plus tôt, le 20/11 :
        // c'est à cette séance qu'il la paiera. Une Physique d'Étude à partir du 04/12 ne la change plus.
        var maths = await GroupeAsync(Etude);
        var arabe = await GroupeAsync(Etude with { Matiere = "Arabe" });
        var physique = await GroupeAsync(Etude with { Matiere = "Physique" });

        var deMaths = await InscrireAsync(new InscriptionSaisie(_adem, maths, null, new DateOnly(2027, 11, 27)));
        var dArabe = await InscrireAsync(new InscriptionSaisie(_adem, arabe, null, DeuxiemeSeanceDuMois3));
        var dePhysique = await InscrireAsync(new InscriptionSaisie(_adem, physique, null, new DateOnly(2027, 12, 4)));

        Assert.Contains(new SommeALArrivee("Assurance 2027-2028", 20m), deMaths.APayerALArrivee);
        Assert.Contains(new SommeALArrivee("Assurance 2027-2028", 20m), dArabe.APayerALArrivee);
        Assert.DoesNotContain(dePhysique.APayerALArrivee, s => s.Libelle.StartsWith("Assurance", StringComparison.Ordinal));
        Assert.Equal(1, await CompterAsync(contexte => contexte.Assurances));
    }

    [Fact]
    public async Task Pas_d_assurance_sans_montant()
    {
        await ReglerLeCentreAsync("73 221 400", null);
        var etude = await GroupeAsync(Etude);

        var enregistrement = await InscrireAsync(new InscriptionSaisie(_adem, etude, null, DeuxiemeSeanceDuMois3));

        Assert.Equal([new SommeALArrivee("Mois 3 (3 séances)", 30m)], enregistrement.APayerALArrivee);
        Assert.Equal(0, await CompterAsync(contexte => contexte.Assurances));
    }

    // ----- Le message de confirmation -----

    [Fact]
    public async Task Les_destinataires_du_message()
    {
        var formation = await GroupeAsync(Formation);
        var sami = await EleveAsync(new FicheSaisieDeLEleve(
            "Sami", "Chaabane", Niveau("9ᵉ année de base"), true, null, null, "55 111 222", "sami@exemple.tn", null));
        var lina = await EleveAsync(Lina);
        var nour = await EleveAsync(new FicheSaisieDeLEleve("Nour", "Gharbi", Niveau("6ᵉ année de base"), true, "97 000 002", null, null, null, null));

        async Task<List<(string, string)>> DestinatairesAsync(Guid eleve) =>
            (await InscrireAsync(new InscriptionSaisie(eleve, formation, "EnUneFois", new DateOnly(2027, 11, 13)))).Messages
                .Select(m => (m.Destinataire, m.Telephone)).ToList();

        Assert.Equal([("Sami", "55 111 222")], await DestinatairesAsync(sami));
        Assert.Equal([("Parent", "98 401 220"), ("Adem", "22 345 678")], await DestinatairesAsync(_adem));
        Assert.Equal([("Parent", "98 000 001")], await DestinatairesAsync(lina));
        Assert.Equal([("Parent", "97 000 002")], await DestinatairesAsync(nour));
    }

    [Fact]
    public async Task Le_texte_du_message()
    {
        var etude = await GroupeAsync(Etude);
        var lina = await EleveAsync(Lina);

        var adem = await InscrireAsync(new InscriptionSaisie(_adem, etude, null, DeuxiemeSeanceDuMois3));

        Assert.Equal(
            "Bonjour, l'inscription d'Adem Miled est enregistrée.\n"
            + "Mathématiques avec Karim Ben Salah, Groupe Samedi 10h → 12h : première séance le samedi 20/11/2027 à 10h.\n"
            + "À bientôt. Centre Les Jasmins, 73 221 400",
            adem.Texte);
        Assert.Equal($"https://wa.me/21698401220?text={Uri.EscapeDataString(adem.Texte)}", adem.Messages[0].Lien);

        await ReglerLeCentreAsync(null, 20m);
        var sansTelephone = await InscrireAsync(new InscriptionSaisie(lina, etude, null, DeuxiemeSeanceDuMois3));
        Assert.StartsWith("Bonjour, l'inscription de Lina Trabelsi est enregistrée.\n", sansTelephone.Texte);
        Assert.EndsWith("\nÀ bientôt. Centre Les Jasmins", sansTelephone.Texte);
    }

    // ----- Ce que l'inscription change ailleurs -----

    [Fact]
    public async Task Une_fiche_inscrite_ne_se_supprime_pas()
    {
        await InscrireAsync(new InscriptionSaisie(_adem, await GroupeAsync(Etude), null, DeuxiemeSeanceDuMois3));

        await VerifierLeRefusAsync(await _client.DeleteAsync($"/api/eleves/{_adem}"),
            HttpStatusCode.Conflict, "Cette fiche a un historique : elle ne se supprime pas");
    }

    [Fact]
    public async Task Le_groupe_compte_ses_eleves()
    {
        var etude = await GroupeAsync(Etude);
        await InscrireAsync(new InscriptionSaisie(_adem, etude, null, DeuxiemeSeanceDuMois3));
        await InscrireAsync(new InscriptionSaisie(await EleveAsync(Lina), etude, null, DeuxiemeSeanceDuMois3));

        Assert.Equal(2, (await _client.GetFromJsonAsync<FicheDuGroupe>($"/api/groupes/{etude}"))!.Eleves);
        Assert.Equal(2, (await _client.GetFromJsonAsync<List<ResumeDuGroupe>>("/api/groupes"))!.Single(g => g.Id == etude).Eleves);
    }

    [Fact]
    public async Task Un_eleve_ou_un_groupe_d_un_autre_centre_est_introuvable()
    {
        var etude = await GroupeAsync(Etude);
        var (eleveDesOliviers, formationDesOliviers) = await DonneesDesOliviersAsync();

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, new InscriptionSaisie(eleveDesOliviers, etude, null, DeuxiemeSeanceDuMois3)),
            HttpStatusCode.NotFound, "Élève introuvable");
        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, new InscriptionSaisie(_adem, formationDesOliviers, "EnUneFois", new DateOnly(2027, 11, 13))),
            HttpStatusCode.NotFound, "Groupe introuvable");
    }

    // ----- Les inscriptions d'un élève et d'un groupe -----

    [Fact]
    public async Task Les_inscriptions_d_un_eleve()
    {
        var physique = await GroupeAsync(Revision with { Nom = "Physique du samedi" });
        var informatique = await GroupeAsync(Formation with { Nom = "Informatique du samedi" });
        var maths = await GroupeAsync(Etude with { Nom = "Mathématiques du samedi" });
        await InscrireAsync(new InscriptionSaisie(_adem, physique, "ALaSeance", new DateOnly(2027, 11, 20)));
        await InscrireAsync(new InscriptionSaisie(_adem, informatique, "EnTranches", new DateOnly(2027, 11, 13)));
        await InscrireAsync(new InscriptionSaisie(_adem, maths, null, DeuxiemeSeanceDuMois3));

        var inscriptions = await InscriptionsAsync($"eleve={_adem}");

        Assert.Equal(["Informatique du samedi", "Mathématiques du samedi", "Physique du samedi"], inscriptions.Select(i => i.Groupe.Nom));
        Assert.Equal([("EnTranches", 300m, 3), ("ParMois", 40m, 0), ("ALaSeance", 100m, 0)],
            inscriptions.Select(i => (i.Mode, i.PrixNormal, i.NombreDeTranches)));
        Assert.Equal(new SeanceDeDebut(new DateOnly(2027, 11, 20), new TimeOnly(10, 0)), inscriptions[2].PremiereSeance);
        Assert.Equal("Karim Ben Salah", inscriptions[0].Professeur);
    }

    [Fact]
    public async Task Les_eleves_d_un_groupe()
    {
        var etude = await GroupeAsync(Etude);
        foreach (var eleve in new[] { _adem, await EleveAsync(Lina), await EleveAsync(Adem with { Prenom = "Amel", Nom = "Chaabane" }) })
            await InscrireAsync(new InscriptionSaisie(eleve, etude, null, DeuxiemeSeanceDuMois3));

        var inscriptions = await InscriptionsAsync($"groupe={etude}");

        Assert.Equal(["Amel Chaabane", "Adem Miled", "Lina Trabelsi"], inscriptions.Select(i => $"{i.Eleve.Prenom} {i.Eleve.Nom}"));
    }

    [Fact]
    public async Task Sans_eleve_ni_groupe()
    {
        await VerifierLeRefusAsync(await _client.GetAsync(Adresse), HttpStatusCode.BadRequest, "Choisissez l'élève ou le groupe");
        await VerifierLeRefusAsync(await _client.GetAsync($"{Adresse}/groupes-possibles"), HttpStatusCode.BadRequest, "Choisissez l'élève");
    }

    [Fact]
    public async Task Les_groupes_possibles()
    {
        var maths = await GroupeAsync(Etude with { Capacite = 15 });
        var physique = await GroupeAsync(Revision);
        var arabe = await GroupeAsync(Etude with { Matiere = "Arabe", NiveauId = Niveau("6ᵉ année de base") });
        var informatique = await GroupeAsync(Formation);
        var anglais = await GroupeAsync(Etude with { Matiere = "Anglais" });
        await _client.PostAsJsonAsync($"/api/groupes/{anglais}/fermeture", new FermetureDUnGroupe(new DateOnly(2027, 11, 10), "Trop peu d'élèves"));
        await GroupeAsync(Revision with { Matiere = "Chimie", NombreDeSeances = 2 });
        await InscrireAsync(new InscriptionSaisie(_adem, maths, null, DeuxiemeSeanceDuMois3));
        // Le 21/11, la Révision de Chimie (13 et 20/11) est terminée.
        await AllerAuAsync(new DateTimeOffset(2027, 11, 21, 9, 0, 0, TimeSpan.Zero));

        var groupes = (await _client.GetFromJsonAsync<List<GroupePossible>>($"{Adresse}/groupes-possibles?eleve={_adem}"))!;

        Assert.Equal([maths, physique, arabe, informatique], groupes.Select(g => g.Id));
        Assert.Equal([true, true, false, false], groupes.Select(g => g.DuNiveauDeLEleve));
        Assert.Equal(
            new GroupePossible(maths, "Groupe Samedi 10h → 12h", "Etude", "Mathématiques", "Karim Ben Salah", "9ᵉ année de base", true, 1, 15, true),
            groupes[0]);
        Assert.Equal(((string?)null, 0, false), (groupes[3].Niveau, groupes[3].Eleves, groupes[3].DejaInscrit));
        await VerifierLeRefusAsync(await _client.GetAsync($"{Adresse}/groupes-possibles?eleve={Guid.NewGuid()}"),
            HttpStatusCode.NotFound, "Élève introuvable");
    }

    [Fact]
    public async Task L_assurance_est_due_a_la_premiere_seance_d_etude()
    {
        var maths = await GroupeAsync(Etude);
        var arabe = await GroupeAsync(Etude with { Matiere = "Arabe" });
        await InscrireAsync(new InscriptionSaisie(_adem, maths, null, new DateOnly(2027, 11, 27)));
        await InscrireAsync(new InscriptionSaisie(_adem, arabe, null, DeuxiemeSeanceDuMois3));

        var assurance = await _client.GetFromJsonAsync<AssuranceDeLEleve>($"/api/eleves/{_adem}/assurance");

        Assert.Equal(new AssuranceDeLEleve("2027-2028", 20m, DeuxiemeSeanceDuMois3, "APayer", null), assurance);
    }

    [Fact]
    public async Task Sans_etude_l_assurance_est_sans_objet()
    {
        await InscrireAsync(new InscriptionSaisie(_adem, await GroupeAsync(Revision), "EnTotalite", new DateOnly(2027, 11, 13)));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.GetAsync($"/api/eleves/{_adem}/assurance")).StatusCode);
        await VerifierLeRefusAsync(await _client.GetAsync($"/api/eleves/{Guid.NewGuid()}/assurance"), HttpStatusCode.NotFound, "Élève introuvable");
    }

    [Fact]
    public async Task Les_listes_d_un_autre_centre_sont_introuvables()
    {
        var (eleveDesOliviers, formationDesOliviers) = await DonneesDesOliviersAsync();

        await VerifierLeRefusAsync(await _client.GetAsync($"{Adresse}?eleve={eleveDesOliviers}"), HttpStatusCode.NotFound, "Élève introuvable");
        await VerifierLeRefusAsync(await _client.GetAsync($"{Adresse}?groupe={formationDesOliviers}"), HttpStatusCode.NotFound, "Groupe introuvable");
        await VerifierLeRefusAsync(await _client.GetAsync($"{Adresse}/groupes-possibles?eleve={eleveDesOliviers}"),
            HttpStatusCode.NotFound, "Élève introuvable");
        await VerifierLeRefusAsync(await _client.GetAsync($"/api/eleves/{eleveDesOliviers}/assurance"), HttpStatusCode.NotFound, "Élève introuvable");
    }

    // ----- Outils -----

    private async Task<List<InscriptionDeLEleve>> InscriptionsAsync(string filtre) =>
        (await _client.GetFromJsonAsync<List<InscriptionDeLEleve>>($"{Adresse}?{filtre}"))!;

    /// <summary>Le centre « Oliviers », son élève et sa Formation : les Jasmins ne doivent jamais les voir.</summary>
    private async Task<(Guid Eleve, Guid Formation)> DonneesDesOliviersAsync()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        await _application.RecopierLesNiveauxDOfficeAsync(oliviers);
        var secretaire = await _application.ClientConnecteAsync(await _application.CreerCompteAsync(oliviers, "Rim", "Saidi"));
        var niveaux = (await secretaire.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!;
        var reponse = await secretaire.PostAsJsonAsync("/api/eleves", Adem with { NiveauId = niveaux.Single(n => n.Nom == "9ᵉ année de base").Id });
        var eleve = (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
        var professeur = await ProfesseurAsync(secretaire);
        var formation = (await (await secretaire.PostAsJsonAsync("/api/groupes", Formation with { ProfesseurId = professeur }))
            .Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
        return (eleve, formation);
    }

    /// <summary>Lina Trabelsi, en 6ᵉ année de base : seul le téléphone de son parent.</summary>
    private FicheSaisieDeLEleve Lina => new("Lina", "Trabelsi", Niveau("6ᵉ année de base"), false, "98 000 001", null, null, null, null);

    private async Task ReglerLeCentreAsync(string? telephone, decimal? assurance)
    {
        var reponse = await _proprietaire.PutAsJsonAsync("/api/centre/parametres",
            new ReglagesDuCentre("Centre Les Jasmins", telephone, null, Annee, assurance, null, null, null));
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
    }

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

    private async Task<Guid> EleveAsync(FicheSaisieDeLEleve fiche)
    {
        var reponse = await _client.PostAsJsonAsync("/api/eleves", fiche);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLEleve>())!.Eleve.Id;
    }

    private async Task<Guid> GroupeAsync(CreationDUnGroupe creation)
    {
        var reponse = await _client.PostAsJsonAsync("/api/groupes", creation);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!.Id;
    }

    private async Task<EnregistrementDeLInscription> InscrireAsync(InscriptionSaisie demande)
    {
        var reponse = await _client.PostAsJsonAsync(Adresse, demande);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDeLInscription>())!;
    }

    /// <summary>Les lignes de ce centre dans une table, lues sans le filtre du centre courant.</summary>
    private Task<int> CompterAsync<T>(Func<SchoolHubDbContext, IQueryable<T>> table) where T : class, IAppartientAuCentre =>
        _application.DansUnScopeAsync(services =>
            table(services.GetRequiredService<SchoolHubDbContext>()).IgnoreQueryFilters().CountAsync(d => d.CentreId == _jasmins.Id));

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
