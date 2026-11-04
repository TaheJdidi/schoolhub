using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Professeurs;

/// <summary>
/// GRP-01, GRP-03 et la liste de GRP-02 : les professeurs du centre. Ils n'ont pas de compte.
/// Chaque test part d'un nouveau centre « Jasmins », dont Amira est la secrétaire, connectée.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class ProfesseursTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "professeurs";
    private const string Adresse = "/api/professeurs";

    private static readonly FicheDuProfesseur Karim = new("Karim", "Ben Salah", "98 401 220", "karim@exemple.tn");
    private static readonly FicheDuProfesseur Leila = new("Leila", "Khelifi", "22 918 004", null);
    private static readonly FicheDuProfesseur Sami = new("Sami", "Chaabane", "55 230 671", null);

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        var amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        _client = await _application.ClientConnecteAsync(amira);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    // ----- Ajouter -----

    [Fact]
    public async Task Ajouter_un_professeur()
    {
        var reponse = await _client.PostAsJsonAsync(Adresse, Karim with { Telephone = "+216 98401220" });

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var enregistrement = await reponse.Content.ReadFromJsonAsync<EnregistrementDuProfesseur>();
        var id = enregistrement!.Professeur.Id;
        Assert.Equal($"/api/professeurs/{id}", reponse.Headers.Location?.ToString());
        Assert.Equal(new ProfesseurDuCentre(id, "Karim", "Ben Salah", "98 401 220", "karim@exemple.tn", false, 0), enregistrement.Professeur);
        Assert.Null(enregistrement.Avertissement);
        Assert.Equal([enregistrement.Professeur], await ListeAsync(_client));
    }

    [Fact]
    public async Task L_email_est_facultatif()
    {
        var enregistrement = await AjouterAsync(_client, Karim with { Email = "  " });

        Assert.Null(enregistrement.Professeur.Email);
    }

    [Fact]
    public async Task Les_espaces_autour_des_textes_sont_retires()
    {
        var enregistrement = await AjouterAsync(_client, new FicheDuProfesseur("  Karim ", " Ben Salah  ", " 98 401 220 ", " karim@exemple.tn "));

        Assert.Equal(("Karim", "Ben Salah", "karim@exemple.tn"),
            (enregistrement.Professeur.Prenom, enregistrement.Professeur.Nom, enregistrement.Professeur.Email));
    }

    [Theory]
    [InlineData("prenom", "", "Le prénom est obligatoire")]
    [InlineData("prenom", "   ", "Le prénom est obligatoire")]
    [InlineData("prenom", "101", "Le prénom fait au plus 100 caractères")]
    [InlineData("nom", "", "Le nom est obligatoire")]
    [InlineData("nom", "101", "Le nom fait au plus 100 caractères")]
    [InlineData("telephone", "", "Le téléphone est obligatoire")]
    [InlineData("telephone", "9840122", "Le téléphone doit avoir 8 chiffres")]
    [InlineData("email", "karim@", "Cet email n'est pas valable")]
    public async Task Une_fiche_incomplete_est_refusee(string champ, string valeur, string message)
    {
        // « 101 » veut dire : 101 caractères.
        var saisie = valeur == "101" ? new string('a', 101) : valeur;
        var fiche = champ switch
        {
            "prenom" => Karim with { Prenom = saisie },
            "nom" => Karim with { Nom = saisie },
            "telephone" => Karim with { Telephone = saisie },
            _ => Karim with { Email = saisie },
        };

        await VerifierLeRefusAsync(await _client.PostAsJsonAsync(Adresse, fiche), HttpStatusCode.BadRequest, message);
        Assert.Empty(await ListeAsync(_client));
    }

    [Fact]
    public async Task Une_fiche_sans_aucun_champ_est_refusee()
    {
        var reponse = await _client.PostAsJsonAsync(Adresse, new FicheDuProfesseur(null, null, null, null));

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, "Le prénom est obligatoire");
    }

    // ----- Le téléphone déjà pris -----

    [Fact]
    public async Task Un_telephone_deja_pris_previent_sans_bloquer()
    {
        await AjouterAsync(_client, Karim);

        var enregistrement = await AjouterAsync(_client, Leila with { Telephone = "+216 98401220" });

        Assert.Equal("Ce téléphone est aussi celui de Karim Ben Salah", enregistrement.Avertissement);
        Assert.Equal(2, (await ListeAsync(_client)).Count);
    }

    [Fact]
    public async Task Deux_doublons_sont_nommes_ensemble()
    {
        await AjouterAsync(_client, Leila with { Telephone = Karim.Telephone });
        await AjouterAsync(_client, Karim);

        var enregistrement = await AjouterAsync(_client, Sami with { Telephone = Karim.Telephone });

        Assert.Equal("Ce téléphone est aussi celui de Karim Ben Salah et Leila Khelifi", enregistrement.Avertissement);
    }

    [Fact]
    public async Task Trois_doublons_sont_nommes_avec_des_virgules()
    {
        await AjouterAsync(_client, Karim);
        await AjouterAsync(_client, Leila with { Telephone = Karim.Telephone });
        await AjouterAsync(_client, Sami with { Telephone = Karim.Telephone });

        var enregistrement = await AjouterAsync(_client, new FicheDuProfesseur("Nadia", "Bouzid", Karim.Telephone, null));

        Assert.Equal("Ce téléphone est aussi celui de Karim Ben Salah, Leila Khelifi et Sami Chaabane", enregistrement.Avertissement);
    }

    [Fact]
    public async Task Un_professeur_parti_compte_pour_le_doublon()
    {
        var karim = await AjouterAsync(_client, Karim);
        await _application.DansUnScopeAsync(async services =>
        {
            services.GetRequiredService<CentreCourant>().Definir(_jasmins.Id);
            var contexte = services.GetRequiredService<SchoolHubDbContext>();
            (await contexte.Professeurs.SingleAsync(p => p.Id == karim.Professeur.Id)).Partir();
            return await contexte.SaveChangesAsync();
        });

        var enregistrement = await AjouterAsync(_client, Karim with { Email = null });

        Assert.Equal("Ce téléphone est aussi celui de Karim Ben Salah", enregistrement.Avertissement);
    }

    // ----- La liste -----

    [Fact]
    public async Task La_liste_est_dans_l_ordre_des_prenoms()
    {
        await AjouterAsync(_client, Sami);
        await AjouterAsync(_client, Karim);
        await AjouterAsync(_client, Leila);
        await AjouterAsync(_client, new FicheDuProfesseur("Karim", "Ayari", "29 660 315", null));

        var liste = await ListeAsync(_client);

        Assert.Equal(["Karim Ayari", "Karim Ben Salah", "Leila Khelifi", "Sami Chaabane"], liste.Select(p => $"{p.Prenom} {p.Nom}"));
        Assert.All(liste, p => Assert.Equal(0, p.GroupesOuverts));
    }

    // ----- Les droits -----

    [Fact]
    public async Task Une_personne_non_connectee_est_refusee()
    {
        var anonyme = _application.Client();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonyme.GetAsync(Adresse)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonyme.PostAsJsonAsync(Adresse, Karim)).StatusCode);
    }

    [Fact]
    public async Task Le_proprietaire_ajoute_aussi()
    {
        var hedi = await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
        var clientDeHedi = await _application.ClientConnecteAsync(hedi);

        Assert.Equal(HttpStatusCode.Created, (await clientDeHedi.PostAsJsonAsync(Adresse, Karim)).StatusCode);
    }

    [Fact]
    public async Task Un_centre_ne_voit_pas_les_professeurs_d_un_autre()
    {
        var clientDesOliviers = await ClientDesOliviersAsync();
        await AjouterAsync(clientDesOliviers, Karim);

        Assert.Empty(await ListeAsync(_client));
        Assert.Null((await AjouterAsync(_client, Leila with { Telephone = Karim.Telephone })).Avertissement);
    }

    // ----- Modifier, supprimer -----

    [Fact]
    public async Task Modifier_un_professeur()
    {
        var karim = (await AjouterAsync(_client, Karim)).Professeur;

        var reponse = await _client.PutAsJsonAsync($"{Adresse}/{karim.Id}", new FicheDuProfesseur("Karim", "Ben Salah Jr", "22 918 004", null));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var attendu = new ProfesseurDuCentre(karim.Id, "Karim", "Ben Salah Jr", "22 918 004", null, false, 0);
        Assert.Equal(new EnregistrementDuProfesseur(attendu, null), await reponse.Content.ReadFromJsonAsync<EnregistrementDuProfesseur>());
        Assert.Equal([attendu], await ListeAsync(_client));
    }

    [Fact]
    public async Task Modifier_sans_changer_le_telephone_ne_previent_pas()
    {
        var karim = (await AjouterAsync(_client, Karim)).Professeur;

        var reponse = await _client.PutAsJsonAsync($"{Adresse}/{karim.Id}", Karim with { Email = "karim.bensalah@exemple.tn" });

        Assert.Null((await reponse.Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Avertissement);
    }

    [Fact]
    public async Task Modifier_vers_un_telephone_pris_previent()
    {
        await AjouterAsync(_client, Karim);
        var leila = (await AjouterAsync(_client, Leila)).Professeur;

        var reponse = await _client.PutAsJsonAsync($"{Adresse}/{leila.Id}", Leila with { Telephone = Karim.Telephone });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("Ce téléphone est aussi celui de Karim Ben Salah",
            (await reponse.Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!.Avertissement);
    }

    [Fact]
    public async Task Une_modification_incomplete_est_refusee()
    {
        var karim = (await AjouterAsync(_client, Karim)).Professeur;

        var reponse = await _client.PutAsJsonAsync($"{Adresse}/{karim.Id}", Karim with { Prenom = "" });

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, "Le prénom est obligatoire");
        Assert.Equal([karim], await ListeAsync(_client));
    }

    [Fact]
    public async Task Supprimer_un_professeur()
    {
        var karim = (await AjouterAsync(_client, Karim)).Professeur;

        var reponse = await _client.DeleteAsync($"{Adresse}/{karim.Id}");

        Assert.Equal(HttpStatusCode.NoContent, reponse.StatusCode);
        Assert.Empty(await ListeAsync(_client));
    }

    // ----- Partir, revenir (GRP-03) -----

    [Fact]
    public async Task Partir_puis_revenir()
    {
        var karim = (await AjouterAsync(_client, Karim)).Professeur;

        var depart = await _client.PostAsync($"{Adresse}/{karim.Id}/depart", null);

        Assert.Equal(HttpStatusCode.OK, depart.StatusCode);
        Assert.Equal(karim with { Parti = true }, await depart.Content.ReadFromJsonAsync<ProfesseurDuCentre>());
        Assert.Equal([karim with { Parti = true }], await ListeAsync(_client));

        var retour = await _client.PostAsync($"{Adresse}/{karim.Id}/retour", null);

        Assert.Equal(HttpStatusCode.OK, retour.StatusCode);
        Assert.Equal(karim, await retour.Content.ReadFromJsonAsync<ProfesseurDuCentre>());
    }

    [Fact]
    public async Task Un_second_depart_ne_change_rien()
    {
        var karim = (await AjouterAsync(_client, Karim)).Professeur;

        await _client.PostAsync($"{Adresse}/{karim.Id}/depart", null);
        var second = await _client.PostAsync($"{Adresse}/{karim.Id}/depart", null);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True((await second.Content.ReadFromJsonAsync<ProfesseurDuCentre>())!.Parti);

        await _client.PostAsync($"{Adresse}/{karim.Id}/retour", null);
        var secondRetour = await _client.PostAsync($"{Adresse}/{karim.Id}/retour", null);

        Assert.Equal(HttpStatusCode.OK, secondRetour.StatusCode);
        Assert.False((await secondRetour.Content.ReadFromJsonAsync<ProfesseurDuCentre>())!.Parti);
    }

    // ----- Introuvable -----

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("depart")]
    [InlineData("retour")]
    public async Task Un_professeur_inconnu_est_introuvable(string action)
    {
        var reponse = await AgirAsync(_client, action, Guid.NewGuid());

        await VerifierLeRefusAsync(reponse, HttpStatusCode.NotFound, "Professeur introuvable");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("depart")]
    [InlineData("retour")]
    public async Task Un_centre_ne_touche_pas_aux_professeurs_d_un_autre(string action)
    {
        var clientDesOliviers = await ClientDesOliviersAsync();
        var karimDesOliviers = (await AjouterAsync(clientDesOliviers, Karim)).Professeur;

        var reponse = await AgirAsync(_client, action, karimDesOliviers.Id);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.NotFound, "Professeur introuvable");
        Assert.Equal([karimDesOliviers], await ListeAsync(clientDesOliviers));
    }

    // ----- Outils -----

    /// <summary>Une action sur un professeur : PUT (avec la fiche de Leila), DELETE, « depart » ou « retour ».</summary>
    private static Task<HttpResponseMessage> AgirAsync(HttpClient client, string action, Guid id) => action switch
    {
        "PUT" => client.PutAsJsonAsync($"{Adresse}/{id}", Leila),
        "DELETE" => client.DeleteAsync($"{Adresse}/{id}"),
        _ => client.PostAsync($"{Adresse}/{id}/{action}", null),
    };

    private async Task<HttpClient> ClientDesOliviersAsync()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var mounir = await _application.CreerCompteAsync(oliviers, "Mounir", "Gharbi");
        return await _application.ClientConnecteAsync(mounir);
    }

    private static async Task<EnregistrementDuProfesseur> AjouterAsync(HttpClient client, FicheDuProfesseur fiche)
    {
        var reponse = await client.PostAsJsonAsync(Adresse, fiche);
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<EnregistrementDuProfesseur>())!;
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }

    private static async Task<List<ProfesseurDuCentre>> ListeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<ProfesseurDuCentre>>(Adresse))!;
}
