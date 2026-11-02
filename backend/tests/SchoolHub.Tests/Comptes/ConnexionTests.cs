using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Comptes;

/// <summary>
/// CPT-01 : se connecter et se déconnecter, et la session de 12 heures sans activité.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class ConnexionTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string MotDePasse = "jasmins-2027";
    private const string MauvaisMotDePasse = "oliviers-2027";

    private static readonly Avis Incorrect = new("Email ou mot de passe incorrect");
    private static readonly Avis TropDEssais = new("Trop d'essais : réessayez dans 15 minutes");
    private static readonly Avis Desactive = new("Ce compte est désactivé : adressez-vous au propriétaire du centre");

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, "comptes_connexion");
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    [Fact]
    public async Task Une_secretaire_se_connecte_et_retrouve_son_centre()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();
        var attendu = new Moi("Amira", "Trabelsi", "Secretaire", "Centre Les Jasmins");

        var connexion = await Connexion(client, amira.Email, MotDePasse);
        Assert.Equal(HttpStatusCode.OK, connexion.StatusCode);
        Assert.Equal(attendu, await connexion.Content.ReadFromJsonAsync<Moi>());

        var moi = await client.GetAsync("/api/moi");
        Assert.Equal(HttpStatusCode.OK, moi.StatusCode);
        Assert.Equal(attendu, await moi.Content.ReadFromJsonAsync<Moi>());
    }

    [Fact]
    public async Task Un_mauvais_mot_de_passe_et_un_email_inconnu_donnent_le_meme_message()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();

        await VerifierRefus(await Connexion(client, amira.Email, MauvaisMotDePasse), HttpStatusCode.Unauthorized, Incorrect);
        await VerifierRefus(await Connexion(client, "inconnue@exemple.tn", MotDePasse), HttpStatusCode.Unauthorized, Incorrect);
    }

    [Fact]
    public async Task Un_compte_invite_ne_peut_pas_se_connecter()
    {
        var invitee = await _application.CreerCompteAsync(_jasmins, etat: EtatDuCompte.Invite, motDePasse: null);

        var reponse = await Connexion(_application.Client(), invitee.Email, MotDePasse);

        await VerifierRefus(reponse, HttpStatusCode.Unauthorized, Incorrect);
    }

    [Fact]
    public async Task Au_cinquieme_essai_faux_le_compte_est_bloque_15_minutes()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();

        for (var essai = 1; essai <= 4; essai++)
            await VerifierRefus(await Connexion(client, amira.Email, MauvaisMotDePasse), HttpStatusCode.Unauthorized, Incorrect);
        await VerifierRefus(await Connexion(client, amira.Email, MauvaisMotDePasse), HttpStatusCode.Unauthorized, TropDEssais);

        // Même le bon mot de passe est refusé tant que dure le blocage.
        await VerifierRefus(await Connexion(client, amira.Email, MotDePasse), HttpStatusCode.Unauthorized, TropDEssais);

        var finDuBlocage = await _application.DansUnScopeAsync(async services =>
            (await services.GetRequiredService<UserManager<Compte>>().FindByIdAsync(amira.Id.ToString()))!.LockoutEnd);
        Assert.NotNull(finDuBlocage);
        Assert.InRange(finDuBlocage.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task Apres_le_blocage_le_compte_se_debloque()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();
        for (var essai = 1; essai <= 5; essai++)
            await Connexion(client, amira.Email, MauvaisMotDePasse);

        // Le blocage d'Identity lit l'heure réelle, pas l'horloge de test : on avance la fin du blocage en base.
        await _application.DansUnScopeAsync(async services =>
        {
            var comptes = services.GetRequiredService<UserManager<Compte>>();
            var compte = await comptes.FindByIdAsync(amira.Id.ToString());
            return await comptes.SetLockoutEndDateAsync(compte!, DateTimeOffset.UtcNow.AddSeconds(-1));
        });

        var reponse = await Connexion(client, amira.Email, MotDePasse);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
    }

    [Fact]
    public async Task Un_essai_reussi_remet_le_compteur_a_zero()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();

        for (var essai = 1; essai <= 4; essai++)
            await Connexion(client, amira.Email, MauvaisMotDePasse);
        Assert.Equal(HttpStatusCode.OK, (await Connexion(client, amira.Email, MotDePasse)).StatusCode);

        for (var essai = 1; essai <= 4; essai++)
            await VerifierRefus(await Connexion(client, amira.Email, MauvaisMotDePasse), HttpStatusCode.Unauthorized, Incorrect);
    }

    [Fact]
    public async Task Un_compte_desactive_ne_peut_pas_se_connecter()
    {
        var amira = await _application.CreerCompteAsync(_jasmins, etat: EtatDuCompte.Desactive);
        var client = _application.Client();

        await VerifierRefus(await Connexion(client, amira.Email, MotDePasse), HttpStatusCode.Forbidden, Desactive);

        // Sans le bon mot de passe, rien ne dit que le compte existe.
        await VerifierRefus(await Connexion(client, amira.Email, MauvaisMotDePasse), HttpStatusCode.Unauthorized, Incorrect);
    }

    [Fact]
    public async Task La_session_dure_12_heures_sans_activite()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();
        Assert.Equal(HttpStatusCode.OK, (await Connexion(client, amira.Email, MotDePasse)).StatusCode);

        _application.Horloge.Advance(TimeSpan.FromHours(11));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/moi")).StatusCode);

        // Chaque requête fait repartir le délai de 12 heures.
        _application.Horloge.Advance(TimeSpan.FromHours(11));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/moi")).StatusCode);

        _application.Horloge.Advance(TimeSpan.FromHours(12) + TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/moi")).StatusCode);
    }

    [Fact]
    public async Task Le_delai_repart_a_chaque_requete_et_pas_seulement_a_mi_parcours()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();
        Assert.Equal(HttpStatusCode.OK, (await Connexion(client, amira.Email, MotDePasse)).StatusCode);

        // Par défaut, ASP.NET ne renouvelle le cookie qu'après la moitié du délai :
        // une requête au bout d'1 heure ne le prolongerait pas, et la session finirait à 12 heures.
        _application.Horloge.Advance(TimeSpan.FromHours(1));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/moi")).StatusCode);

        _application.Horloge.Advance(TimeSpan.FromHours(11) + TimeSpan.FromMinutes(30));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/moi")).StatusCode);
    }

    [Fact]
    public async Task Le_cookie_est_protege()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);

        var reponse = await Connexion(_application.Client(), amira.Email, MotDePasse);

        var cookie = string.Join("; ", reponse.Headers.GetValues("Set-Cookie")).ToLowerInvariant();
        Assert.Contains("schoolhub=", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("expires=", cookie);
    }

    [Fact]
    public async Task Apres_la_deconnexion_on_ne_voit_plus_rien()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();
        Assert.Equal(HttpStatusCode.OK, (await Connexion(client, amira.Email, MotDePasse)).StatusCode);

        var deconnexion = await client.PostAsync("/api/deconnexion", content: null);
        Assert.Equal(HttpStatusCode.NoContent, deconnexion.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/moi")).StatusCode);
    }

    [Fact]
    public async Task Les_reponses_de_l_api_ne_sont_pas_gardees_en_cache()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();
        Assert.Equal(HttpStatusCode.OK, (await Connexion(client, amira.Email, MotDePasse)).StatusCode);

        var moi = await client.GetAsync("/api/moi");

        Assert.Equal(HttpStatusCode.OK, moi.StatusCode);
        Assert.True(moi.Headers.CacheControl?.NoStore);

        // Le renouvellement du cookie pose déjà no-store : une réponse sans session vérifie que c'est bien toute l'API.
        var sante = await _application.Client().GetAsync("/api/sante");
        Assert.Equal(HttpStatusCode.OK, sante.StatusCode);
        Assert.True(sante.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Sans_connexion_l_api_repond_401_sans_redirection()
    {
        var reponse = await _application.Client().GetAsync("/api/moi");

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
        Assert.Null(reponse.Headers.Location);
    }

    [Fact]
    public async Task Un_compte_desactive_pendant_sa_session_est_deconnecte_a_la_requete_suivante()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();
        Assert.Equal(HttpStatusCode.OK, (await Connexion(client, amira.Email, MotDePasse)).StatusCode);

        await _application.DansUnScopeAsync(async services =>
        {
            var comptes = services.GetRequiredService<UserManager<Compte>>();
            var compte = await comptes.FindByIdAsync(amira.Id.ToString());
            compte!.Desactiver();
            return await comptes.UpdateAsync(compte);
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/moi")).StatusCode);
    }

    [Fact]
    public async Task Un_compte_dont_le_tampon_de_securite_change_est_deconnecte()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();
        Assert.Equal(HttpStatusCode.OK, (await Connexion(client, amira.Email, MotDePasse)).StatusCode);

        await _application.DansUnScopeAsync(async services =>
        {
            var comptes = services.GetRequiredService<UserManager<Compte>>();
            var compte = await comptes.FindByIdAsync(amira.Id.ToString());
            return await comptes.UpdateSecurityStampAsync(compte!);
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/moi")).StatusCode);
    }

    // {email} est remplacé par l'email du compte, {EMAIL} par le même en majuscules.
    [Theory]
    [InlineData("  {email}  ")]
    [InlineData("{EMAIL}")]
    public async Task Un_email_avec_majuscules_ou_espaces_marche_comme_en_minuscules(string saisie)
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var email = saisie.Replace("{email}", amira.Email).Replace("{EMAIL}", amira.Email!.ToUpperInvariant());

        var reponse = await Connexion(_application.Client(), email, MotDePasse);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
    }

    [Fact]
    public async Task Un_formulaire_vide_donne_le_message_habituel()
    {
        var client = _application.Client();

        var vide = await client.PostAsync("/api/connexion", JsonContent.Create(new { }));
        await VerifierRefus(vide, HttpStatusCode.Unauthorized, Incorrect);

        await VerifierRefus(await Connexion(client, email: null, motDePasse: null), HttpStatusCode.Unauthorized, Incorrect);
    }

    private static Task<HttpResponseMessage> Connexion(HttpClient client, string? email, string? motDePasse) =>
        client.PostAsJsonAsync("/api/connexion", new DemandeDeConnexion(email, motDePasse));

    private static async Task VerifierRefus(HttpResponseMessage reponse, HttpStatusCode statut, Avis avis)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(avis, await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
