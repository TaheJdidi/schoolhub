using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Groupes;

/// <summary>
/// GRP-04 : créer un groupe d'Étude, le lire et le modifier. Chaque test part d'un nouveau centre « Jasmins », avec son
/// année scolaire du 15/09/2027 au 30/06/2028, ses niveaux d'office et Karim Ben Salah, professeur ; Amira, la secrétaire,
/// est connectée.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class GroupesTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "groupes";
    private const string Adresse = "/api/groupes";
    private const string NeufiemeAnnee = "9ᵉ année de base";

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _client = null!;
    private Compte _amira = null!;
    private Guid _karim;
    private Guid _neuviemeAnnee;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        await _application.InitialiserLaBaseAsync();
        (_jasmins, _client, _amira) = await CentreAsync("Centre Les Jasmins", avecAnneeScolaire: true);
        _karim = await ProfesseurAsync(_client, "Karim", "Ben Salah");
        _neuviemeAnnee = await NiveauAsync(_client, NeufiemeAnnee);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    /// <summary>L'exemple de la story : Karim, le samedi de 10h à 12h, à partir du 18/09/2027, 60 TND le Mois.</summary>
    private CreationDUnGroupe Exemple => new(
        _karim,
        "Mathématiques",
        _neuviemeAnnee,
        [new CreneauSaisi("Samedi", "10:00", "12:00")],
        60m,
        new DateOnly(2027, 9, 18),
        16,
        "");

    // ----- Créer -----

    [Fact]
    public async Task Creer_un_groupe_d_etude()
    {
        var reponse = await _client.PostAsJsonAsync(Adresse, Exemple);

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var fiche = (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!;
        Assert.Equal($"/api/groupes/{fiche.Id}", reponse.Headers.Location?.ToString());
        Assert.Equal("Groupe Samedi 10h → 12h", fiche.Nom);
        Assert.Equal("Etude", fiche.Type);
        Assert.Equal(new ProfesseurDuGroupe(_karim, "Karim", "Ben Salah"), fiche.Professeur);
        Assert.Equal("Mathématiques", fiche.Matiere);
        Assert.Equal(new NiveauDuGroupe(_neuviemeAnnee, NeufiemeAnnee), fiche.Niveau);
        Assert.Equal([new CreneauDuGroupe("Samedi", "10:00", "12:00")], fiche.Creneaux);
        Assert.Equal((16, 0, new DateOnly(2027, 9, 18), 60m), (fiche.Capacite, fiche.Eleves, fiche.DateDeDebut, fiche.PrixDuMois));

        Assert.Equal(11, fiche.Mois.Count);
        var mois1 = fiche.Mois[0];
        Assert.Equal((1, 60m, false), (mois1.Numero, mois1.Prix, mois1.Commence));
        Assert.Equal(
            [new DateOnly(2027, 9, 18), new DateOnly(2027, 9, 25), new DateOnly(2027, 10, 2), new DateOnly(2027, 10, 9)],
            mois1.Seances.Select(s => s.Date));
        Assert.All(mois1.Seances, s => Assert.Equal(("10:00", "12:00"), (s.Debut, s.Fin)));
        Assert.Equal([new DateOnly(2028, 6, 24)], fiche.Mois[10].Seances.Select(s => s.Date));
        Assert.Equal(41, fiche.Mois.Sum(m => m.Seances.Count));

        var relue = await _client.GetFromJsonAsync<FicheDuGroupe>($"{Adresse}/{fiche.Id}");
        Assert.Equal(fiche.Nom, relue!.Nom);
        Assert.Equal(fiche.Mois.SelectMany(m => m.Seances).Select(s => s.Id), relue.Mois.SelectMany(m => m.Seances).Select(s => s.Id));
    }

    [Fact]
    public async Task Le_nom_choisi_par_la_secretaire_est_garde()
    {
        var fiche = await CreerAsync(Exemple with { Nom = "  Maths 9ᵉ, samedi matin " });

        Assert.Equal("Maths 9ᵉ, samedi matin", fiche.Nom);
    }

    [Fact]
    public async Task La_capacite_est_facultative()
    {
        var fiche = await CreerAsync(Exemple with { Capacite = null });

        Assert.Null(fiche.Capacite);
    }

    [Fact]
    public async Task Une_matiere_nouvelle_s_ajoute_au_centre()
    {
        await CreerAsync(Exemple);

        var matieres = await _client.GetFromJsonAsync<List<MatiereDuCentre>>("/api/matieres");

        Assert.Equal(["Mathématiques"], matieres!.Select(m => m.Nom));
    }

    [Fact]
    public async Task Une_matiere_existante_est_reprise()
    {
        await CreerAsync(Exemple);

        var fiche = await CreerAsync(Exemple with { Matiere = "  mathématiques " });

        Assert.Equal("Mathématiques", fiche.Matiere);
        Assert.Single((await _client.GetFromJsonAsync<List<MatiereDuCentre>>("/api/matieres"))!);
    }

    [Theory]
    [InlineData("professeur", "Choisissez le professeur")]
    [InlineData("matiere vide", "La matière est obligatoire")]
    [InlineData("matiere longue", "La matière fait au plus 100 caractères")]
    [InlineData("niveau", "Choisissez le niveau")]
    [InlineData("sans creneau", "Ajoutez au moins un créneau")]
    [InlineData("jour inconnu", "Un créneau doit avoir un jour, une heure de début et une heure de fin")]
    [InlineData("heure illisible", "Un créneau doit avoir un jour, une heure de début et une heure de fin")]
    [InlineData("fin avant debut", "Un créneau doit finir après son début")]
    [InlineData("chevauchement", "Deux créneaux se chevauchent")]
    [InlineData("sans prix", "Le prix du Mois doit être positif, au millime près")]
    [InlineData("prix nul", "Le prix du Mois doit être positif, au millime près")]
    [InlineData("prix au-dela du millime", "Le prix du Mois doit être positif, au millime près")]
    [InlineData("prix d'un million", "Le prix du Mois doit être positif, au millime près")]
    [InlineData("sans date", "La date de début est obligatoire")]
    [InlineData("capacite nulle", "La capacité doit être d'au moins 1 place")]
    [InlineData("nom long", "Le nom du groupe fait au plus 100 caractères")]
    public async Task Une_creation_incomplete_est_refusee(string cas, string message)
    {
        var creation = cas switch
        {
            "professeur" => Exemple with { ProfesseurId = null },
            "matiere vide" => Exemple with { Matiere = "  " },
            "matiere longue" => Exemple with { Matiere = new string('a', 101) },
            "niveau" => Exemple with { NiveauId = null },
            "sans creneau" => Exemple with { Creneaux = [] },
            "jour inconnu" => Exemple with { Creneaux = [new CreneauSaisi("Saturday", "10:00", "12:00")] },
            "heure illisible" => Exemple with { Creneaux = [new CreneauSaisi("Samedi", "10h", "12:00")] },
            "fin avant debut" => Exemple with { Creneaux = [new CreneauSaisi("Samedi", "10:00", "10:00")] },
            "chevauchement" => Exemple with
            {
                Creneaux = [new CreneauSaisi("Samedi", "10:00", "12:00"), new CreneauSaisi("Samedi", "11:00", "13:00")],
            },
            "sans prix" => Exemple with { PrixDuMois = null },
            "prix nul" => Exemple with { PrixDuMois = 0m },
            "prix au-dela du millime" => Exemple with { PrixDuMois = 60.0005m },
            "prix d'un million" => Exemple with { PrixDuMois = 1_000_000m },
            "sans date" => Exemple with { DateDeDebut = null },
            "capacite nulle" => Exemple with { Capacite = 0 },
            _ => Exemple with { Nom = new string('a', 101) },
        };

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, creation), HttpStatusCode.BadRequest, message);
        Assert.Equal(0, await NombreDeGroupesAsync(_jasmins));
    }

    [Fact]
    public async Task Deux_creneaux_qui_se_suivent_ne_se_chevauchent_pas()
    {
        var fiche = await CreerAsync(Exemple with
        {
            Creneaux = [new CreneauSaisi("Samedi", "10:00", "12:00"), new CreneauSaisi("samedi", "12:00", "14:00")],
        });

        Assert.Equal(82, fiche.Mois.Sum(m => m.Seances.Count));
    }

    [Fact]
    public async Task Deux_creneaux_par_semaine_font_des_mois_de_deux_semaines()
    {
        var fiche = await CreerAsync(Exemple with
        {
            Creneaux = [new CreneauSaisi("Mardi", "17:00", "19:00"), new CreneauSaisi("Samedi", "10:00", "12:00")],
        });

        Assert.Equal("Groupe Mardi 17h → 19h", fiche.Nom);
        Assert.Equal(
            [new DateOnly(2027, 9, 18), new DateOnly(2027, 9, 21), new DateOnly(2027, 9, 25), new DateOnly(2027, 9, 28)],
            fiche.Mois[0].Seances.Select(s => s.Date));
    }

    [Fact]
    public async Task Sans_annee_scolaire_la_creation_est_refusee()
    {
        var (_, clientDesOliviers, _) = await CentreAsync("Centre Les Oliviers", avecAnneeScolaire: false);
        var creation = Exemple with
        {
            ProfesseurId = await ProfesseurAsync(clientDesOliviers, "Leila", "Khelifi"),
            NiveauId = await NiveauAsync(clientDesOliviers, NeufiemeAnnee),
        };

        var reponse = await clientDesOliviers.PostAsJsonAsync(Adresse, creation);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, "Réglez d'abord l'année scolaire dans les paramètres du centre");
    }

    [Theory]
    [InlineData(2027, 9, 14)]
    [InlineData(2028, 7, 1)]
    public async Task La_date_de_debut_hors_de_l_annee_est_refusee(int annee, int mois, int jour)
    {
        var reponse = await _client.PostAsJsonAsync(Adresse, Exemple with { DateDeDebut = new DateOnly(annee, mois, jour) });

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest,
            "La date de début doit être dans l'année scolaire, du 15/09/2027 au 30/06/2028");
    }

    [Fact]
    public async Task Un_groupe_sans_seance_est_refuse()
    {
        // Le mercredi 28/06/2028 : le samedi suivant, le 01/07, est après la fin de l'année.
        var reponse = await _client.PostAsJsonAsync(Adresse, Exemple with { DateDeDebut = new DateOnly(2028, 6, 28) });

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, "Aucune séance entre la date de début et la fin de l'année scolaire");
    }

    [Fact]
    public async Task Un_professeur_parti_est_refuse()
    {
        await _client.PostAsync($"/api/professeurs/{_karim}/depart", null);

        var reponse = await _client.PostAsJsonAsync(Adresse, Exemple);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, "Ce professeur a quitté le centre");
    }

    [Fact]
    public async Task Un_professeur_ou_un_niveau_d_un_autre_centre_est_introuvable()
    {
        var (_, clientDesOliviers, _) = await CentreAsync("Centre Les Oliviers", avecAnneeScolaire: true);
        var leila = await ProfesseurAsync(clientDesOliviers, "Leila", "Khelifi");
        var niveauDesOliviers = await NiveauAsync(clientDesOliviers, NeufiemeAnnee);

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, Exemple with { ProfesseurId = leila }),
            HttpStatusCode.NotFound, "Professeur introuvable");
        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, Exemple with { NiveauId = niveauDesOliviers }),
            HttpStatusCode.NotFound, "Niveau introuvable");
    }

    // ----- Lire -----

    [Fact]
    public async Task Un_groupe_inconnu_ou_d_un_autre_centre_est_introuvable()
    {
        var (_, clientDesOliviers, _) = await CentreAsync("Centre Les Oliviers", avecAnneeScolaire: true);
        var groupeDesOliviers = await CreerAsync(Exemple with
        {
            ProfesseurId = await ProfesseurAsync(clientDesOliviers, "Leila", "Khelifi"),
            NiveauId = await NiveauAsync(clientDesOliviers, NeufiemeAnnee),
        }, clientDesOliviers);

        await VerifierLeRefusAsync(await _client.GetAsync($"{Adresse}/{groupeDesOliviers.Id}"), HttpStatusCode.NotFound, "Groupe introuvable");
        await VerifierLeRefusAsync(await _client.GetAsync($"{Adresse}/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "Groupe introuvable");
        await VerifierLeRefusAsync(await _client.PutAsJsonAsync($"{Adresse}/{groupeDesOliviers.Id}", new ModificationDUnGroupe("A", null, 45m)),
            HttpStatusCode.NotFound, "Groupe introuvable");
        Assert.Empty((await _client.GetFromJsonAsync<List<MatiereDuCentre>>("/api/matieres"))!);
        Assert.Empty((await _client.GetFromJsonAsync<List<ResumeDuGroupe>>(Adresse))!);
    }

    [Fact]
    public async Task Une_personne_non_connectee_est_refusee()
    {
        var anonyme = _application.Client();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonyme.GetAsync("/api/matieres")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonyme.PostAsJsonAsync(Adresse, Exemple)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonyme.GetAsync($"{Adresse}/{Guid.NewGuid()}")).StatusCode);
    }

    // ----- La liste -----

    [Fact]
    public async Task La_liste_des_groupes()
    {
        var samedi = await CreerAsync(Exemple);
        var dimanche = await CreerAsync(Exemple with
        {
            Matiere = "Physique",
            Capacite = null,
            Creneaux = [new CreneauSaisi("Dimanche", "08:00", "10:00")],
        });

        var liste = (await _client.GetFromJsonAsync<List<ResumeDuGroupe>>(Adresse))!;

        Assert.Equal(2, liste.Count);
        var resume = liste.Single(g => g.Id == samedi.Id);
        Assert.Equal(("Groupe Samedi 10h → 12h", "Etude", _karim, "Mathématiques", NeufiemeAnnee, (int?)16, 0),
            (resume.Nom, resume.Type, resume.ProfesseurId, resume.Matiere, resume.Niveau, resume.Capacite, resume.Eleves));
        Assert.Equal([new CreneauDuGroupe("Samedi", "10:00", "12:00")], resume.Creneaux);
        var autre = liste.Single(g => g.Id == dimanche.Id);
        Assert.Equal(("Physique", (int?)null), (autre.Matiere, autre.Capacite));
    }

    // ----- Modifier -----

    [Fact]
    public async Task Modifier_le_nom_et_la_capacite()
    {
        var groupe = await CreerAsync(Exemple);

        var fiche = await ModifierAsync(groupe.Id, new ModificationDUnGroupe("  Maths 9ᵉ ", 20, 60m));

        Assert.Equal(("Maths 9ᵉ", (int?)20), (fiche.Nom, fiche.Capacite));
        var sansNom = await ModifierAsync(groupe.Id, new ModificationDUnGroupe("", null, 60m));
        Assert.Equal(("Groupe Samedi 10h → 12h", (int?)null), (sansNom.Nom, sansNom.Capacite));
    }

    [Theory]
    [InlineData("nom long", "Le nom du groupe fait au plus 100 caractères")]
    [InlineData("capacite nulle", "La capacité doit être d'au moins 1 place")]
    [InlineData("prix nul", "Le prix du Mois doit être positif, au millime près")]
    [InlineData("sans prix", "Le prix du Mois doit être positif, au millime près")]
    public async Task Une_modification_incomplete_est_refusee(string cas, string message)
    {
        var groupe = await CreerAsync(Exemple);
        var modification = cas switch
        {
            "nom long" => new ModificationDUnGroupe(new string('a', 101), 16, 60m),
            "capacite nulle" => new ModificationDUnGroupe("Maths", 0, 60m),
            "prix nul" => new ModificationDUnGroupe("Maths", 16, 0m),
            _ => new ModificationDUnGroupe("Maths", 16, null),
        };

        await VerifierLeRefusAsync(await _client.PutAsJsonAsync($"{Adresse}/{groupe.Id}", modification), HttpStatusCode.BadRequest, message);
        Assert.Equal("Groupe Samedi 10h → 12h", (await LireAsync(groupe.Id)).Nom);
    }

    [Fact]
    public async Task Le_nouveau_prix_vaut_pour_les_mois_pas_encore_commences()
    {
        // L'exemple de la story : dans ce calendrier, le Mois 5 commence le 08/01/2028 et le Mois 6 le 05/02/2028.
        var groupe = await CreerAsync(Exemple with { PrixDuMois = 40m });
        await AllerAuAsync(new DateTimeOffset(2028, 2, 1, 9, 0, 0, TimeSpan.Zero));

        var fiche = await ModifierAsync(groupe.Id, new ModificationDUnGroupe(groupe.Nom, 16, 45m));

        Assert.Equal(new DateOnly(2028, 1, 8), fiche.Mois[4].Seances[0].Date);
        Assert.Equal([40m, 40m, 40m, 40m, 40m, 45m, 45m, 45m, 45m, 45m, 45m], fiche.Mois.Select(m => m.Prix));
        Assert.Equal([true, true, true, true, true, false], fiche.Mois.Take(6).Select(m => m.Commence));
        Assert.Equal(45m, fiche.PrixDuMois);
    }

    [Fact]
    public async Task Avant_tout_mois_commence_le_prix_remplace_celui_du_mois_1()
    {
        var groupe = await CreerAsync(Exemple with { PrixDuMois = 40m });
        await AllerAuAsync(new DateTimeOffset(2027, 9, 1, 9, 0, 0, TimeSpan.Zero));

        var fiche = await ModifierAsync(groupe.Id, new ModificationDUnGroupe(groupe.Nom, 16, 45m));

        Assert.All(fiche.Mois, m => Assert.Equal(45m, m.Prix));
        Assert.Equal([(1, 45m)], await PrixEnBaseAsync(groupe.Id));
    }

    [Fact]
    public async Task Un_mois_dont_la_seance_1_est_aujourd_hui_a_commence()
    {
        // Le Mois 2 commence le samedi 16/10/2027 ; il est 10h à Tunis.
        var groupe = await CreerAsync(Exemple with { PrixDuMois = 40m });
        await AllerAuAsync(new DateTimeOffset(2027, 10, 16, 9, 0, 0, TimeSpan.Zero));

        var fiche = await ModifierAsync(groupe.Id, new ModificationDUnGroupe(groupe.Nom, 16, 45m));

        Assert.True(fiche.Mois[1].Commence);
        Assert.Equal([40m, 40m, 45m], fiche.Mois.Take(3).Select(m => m.Prix));
    }

    [Fact]
    public async Task Un_prix_inchange_n_ajoute_rien()
    {
        var groupe = await CreerAsync(Exemple with { PrixDuMois = 40m });
        await AllerAuAsync(new DateTimeOffset(2028, 2, 1, 9, 0, 0, TimeSpan.Zero));

        await ModifierAsync(groupe.Id, new ModificationDUnGroupe("Maths", 16, 40m));

        Assert.Equal([(1, 40m)], await PrixEnBaseAsync(groupe.Id));
    }

    [Fact]
    public async Task Un_second_changement_de_prix_remplace_le_premier_pour_les_memes_mois()
    {
        var groupe = await CreerAsync(Exemple with { PrixDuMois = 40m });
        await AllerAuAsync(new DateTimeOffset(2028, 2, 1, 9, 0, 0, TimeSpan.Zero));

        await ModifierAsync(groupe.Id, new ModificationDUnGroupe(groupe.Nom, 16, 45m));
        await ModifierAsync(groupe.Id, new ModificationDUnGroupe(groupe.Nom, 16, 50m));

        Assert.Equal([(1, 40m), (6, 50m)], await PrixEnBaseAsync(groupe.Id));
    }

    // ----- Les professeurs et les niveaux connaissent les groupes -----

    [Fact]
    public async Task Les_groupes_ouverts_du_professeur_sont_comptes()
    {
        await CreerAsync(Exemple);
        await CreerAsync(Exemple with { Creneaux = [new CreneauSaisi("Dimanche", "08:00", "10:00")] });
        await ProfesseurAsync(_client, "Leila", "Khelifi");

        var professeurs = (await _client.GetFromJsonAsync<List<ProfesseurDuCentre>>("/api/professeurs"))!;

        Assert.Equal([("Karim", 2), ("Leila", 0)], professeurs.Select(p => (p.Prenom, p.GroupesOuverts)));
    }

    [Fact]
    public async Task Modifier_un_professeur_garde_le_compte_de_ses_groupes()
    {
        await CreerAsync(Exemple);

        var reponse = await _client.PutAsJsonAsync($"/api/professeurs/{_karim}", new FicheDuProfesseur("Karim", "Ben Salah", "98 401 220", null));

        Assert.Equal(1, (await reponse.Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.GroupesOuverts);
    }

    [Fact]
    public async Task Un_professeur_qui_a_des_groupes_ouverts_ne_part_pas()
    {
        await CreerAsync(Exemple);

        var reponse = await _client.PostAsync($"/api/professeurs/{_karim}/depart", null);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, "Fermez d'abord ses groupes");
        Assert.False((await _client.GetFromJsonAsync<List<ProfesseurDuCentre>>("/api/professeurs"))!.Single().Parti);
    }

    [Fact]
    public async Task Un_professeur_qui_a_eu_des_groupes_ne_se_supprime_pas()
    {
        await CreerAsync(Exemple);

        var reponse = await _client.DeleteAsync($"/api/professeurs/{_karim}");

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, "Ce professeur a eu des groupes : marquez-le parti");
        Assert.Single((await _client.GetFromJsonAsync<List<ProfesseurDuCentre>>("/api/professeurs"))!);
    }

    [Fact]
    public async Task Un_niveau_utilise_par_un_groupe_ne_se_retire_pas()
    {
        var sonia = await _application.CreerCompteAsync(_jasmins, "Sonia", "Mabrouk", RoleDuCompte.Proprietaire);
        var proprietaire = await _application.ClientConnecteAsync(sonia);
        var ajout = await proprietaire.PostAsJsonAsync("/api/niveaux", new NomDuNiveau("Université, 1ʳᵉ année"));
        var universite = (await ajout.Content.ReadFromJsonAsync<NiveauDuCentre>())!;
        await CreerAsync(Exemple with { NiveauId = universite.Id });

        var reponse = await proprietaire.DeleteAsync($"/api/niveaux/{universite.Id}");

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, "Ce niveau est utilisé par un groupe, un élève ou une demande");
        Assert.Contains(await _client.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux") ?? [], n => n.Id == universite.Id);
    }

    // ----- Outils -----

    /// <summary>Avance l'horloge des tests, puis reconnecte Amira : sa session a pu expirer entre-temps.</summary>
    private async Task AllerAuAsync(DateTimeOffset moment)
    {
        _application.Horloge.SetUtcNow(moment);
        _client = await _application.ClientConnecteAsync(_amira);
    }

    private async Task<FicheDuGroupe> ModifierAsync(Guid id, ModificationDUnGroupe modification)
    {
        var reponse = await _client.PutAsJsonAsync($"{Adresse}/{id}", modification);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!;
    }

    private async Task<FicheDuGroupe> LireAsync(Guid id) => (await _client.GetFromJsonAsync<FicheDuGroupe>($"{Adresse}/{id}"))!;

    private Task<List<(int, decimal)>> PrixEnBaseAsync(Guid groupeId) =>
        _application.DansUnScopeAsync(async services =>
        {
            services.GetRequiredService<CentreCourant>().Definir(_jasmins.Id);
            var groupe = await services.GetRequiredService<SchoolHubDbContext>().Groupes.SingleAsync(g => g.Id == groupeId);
            return groupe.Prix.OrderBy(p => p.APartirDuMois).Select(p => (p.APartirDuMois, p.Montant)).ToList();
        });


    /// <summary>Un centre avec ses niveaux d'office, et le client de sa secrétaire ; son propriétaire règle l'année scolaire.</summary>
    private async Task<(Centre Centre, HttpClient Client, Compte Secretaire)> CentreAsync(string nom, bool avecAnneeScolaire)
    {
        var centre = await _application.CreerCentreAsync(nom);
        await _application.RecopierLesNiveauxDOfficeAsync(centre);
        if (avecAnneeScolaire)
        {
            var proprietaire = await _application.CreerCompteAsync(centre, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
            var annee = new AnneeScolaireSaisie(new DateOnly(2027, 9, 15), new DateOnly(2028, 6, 30));
            var reponse = await (await _application.ClientConnecteAsync(proprietaire)).PutAsJsonAsync(
                "/api/centre/parametres", new ReglagesDuCentre(nom, null, null, annee, null, null, null, null));
            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        }

        var secretaire = await _application.CreerCompteAsync(centre, "Amira", "Jlassi");
        return (centre, await _application.ClientConnecteAsync(secretaire), secretaire);
    }

    private static async Task<Guid> ProfesseurAsync(HttpClient client, string prenom, string nom)
    {
        var reponse = await client.PostAsJsonAsync("/api/professeurs", new FicheDuProfesseur(prenom, nom, "98 401 220", null));
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Professeur.Id;
    }

    private static async Task<Guid> NiveauAsync(HttpClient client, string nom) =>
        (await client.GetFromJsonAsync<List<NiveauDuCentre>>("/api/niveaux"))!.Single(n => n.Nom == nom).Id;

    private async Task<FicheDuGroupe> CreerAsync(CreationDUnGroupe creation, HttpClient? client = null)
    {
        var reponse = await (client ?? _client).PostAsJsonAsync(Adresse, creation);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuGroupe>())!;
    }

    private Task<int> NombreDeGroupesAsync(Centre centre) =>
        _application.DansUnScopeAsync(services =>
        {
            services.GetRequiredService<CentreCourant>().Definir(centre.Id);
            return services.GetRequiredService<SchoolHubDbContext>().Groupes.CountAsync();
        });

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
