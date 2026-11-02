using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Comptes;

/// <summary>
/// CPT-02 : choisir son mot de passe depuis le lien d'invitation.
/// Chaque test invite Amira (secrétaire des Jasmins) comme le fera l'application,
/// puis suit le lien de l'email.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class InvitationTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "comptes_invitation";

    // 8 minuscules : la seule règle est la longueur, il n'y a ni majuscule, ni chiffre, ni symbole à mettre.
    private const string MotDePasseChoisi = "motdepas";

    private static readonly Avis LienExpire = new("Ce lien a expiré : demandez au propriétaire une nouvelle invitation");
    private static readonly Avis TropCourt = new("Le mot de passe doit faire au moins 8 caractères");
    private static readonly Moi Amira = new("Amira", "Trabelsi", "Secretaire", "Centre Les Jasmins");

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    [Fact]
    public async Task L_invitation_part_avec_le_bon_sujet_et_un_lien()
    {
        var (amira, email) = await InviterAsync();

        var (compte, jeton) = _application.Emails.LireLeLien(email);
        Assert.Equal(amira.Id.ToString(), compte);
        Assert.Equal(amira.Email, email.Destinataire);
        Assert.Equal("Rejoindre le Centre Les Jasmins sur SchoolHub", email.Sujet);
        Assert.Equal(
            "Bonjour Amira,\n\n"
            + "Vous êtes invité(e) à rejoindre le Centre Les Jasmins sur SchoolHub.\n"
            + "Choisissez votre mot de passe en ouvrant ce lien, valable 7 jours :\n"
            + $"http://localhost:5087/invitation?compte={amira.Id}&jeton={Uri.EscapeDataString(jeton)}",
            email.Texte);
    }

    [Fact]
    public async Task Le_lien_accueille_la_personne()
    {
        var (_, email) = await InviterAsync();
        var (compte, jeton) = _application.Emails.LireLeLien(email);

        var reponse = await _application.Client().GetAsync(AdresseDuLien(compte, jeton));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(new Accueil("Amira", "Centre Les Jasmins"), await reponse.Content.ReadFromJsonAsync<Accueil>());
    }

    [Fact]
    public async Task Choisir_son_mot_de_passe_active_le_compte_et_connecte()
    {
        var (amira, email) = await InviterAsync();
        var (compte, jeton) = _application.Emails.LireLeLien(email);
        var client = _application.Client();

        var reponse = await client.PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(compte, jeton, MotDePasseChoisi));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(Amira, await reponse.Content.ReadFromJsonAsync<Moi>());

        // La personne est déjà connectée, avec le cookie posé par cette réponse.
        var moi = await client.GetAsync("/api/moi");
        Assert.Equal(HttpStatusCode.OK, moi.StatusCode);
        Assert.Equal(Amira, await moi.Content.ReadFromJsonAsync<Moi>());

        Assert.Equal(EtatDuCompte.Actif, (await ChargerAsync(amira)).Etat);

        // Et elle pourra se reconnecter plus tard avec ce mot de passe.
        var autreClient = _application.Client();
        var connexion = await autreClient.PostAsJsonAsync("/api/connexion", new DemandeDeConnexion(amira.Email, MotDePasseChoisi));
        Assert.Equal(HttpStatusCode.OK, connexion.StatusCode);
    }

    [Theory]
    [InlineData("motdepa")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Un_mot_de_passe_de_moins_de_8_caracteres_est_refuse(string? motDePasse)
    {
        var (amira, email) = await InviterAsync();
        var (compte, jeton) = _application.Emails.LireLeLien(email);
        var client = _application.Client();

        var reponse = await client.PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(compte, jeton, motDePasse));

        await VerifierRefus(reponse, HttpStatusCode.BadRequest, TropCourt);
        Assert.Equal(EtatDuCompte.Invite, (await ChargerAsync(amira)).Etat);

        // Le lien n'est pas consommé par un refus : la personne peut réessayer.
        var encore = await client.PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(compte, jeton, MotDePasseChoisi));
        Assert.Equal(HttpStatusCode.OK, encore.StatusCode);
    }

    [Fact]
    public async Task Le_lien_ne_sert_qu_une_fois()
    {
        var (_, email) = await InviterAsync();
        var (compte, jeton) = _application.Emails.LireLeLien(email);
        var premier = await _application.Client().PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(compte, jeton, MotDePasseChoisi));
        Assert.Equal(HttpStatusCode.OK, premier.StatusCode);

        var client = _application.Client();
        await VerifierRefus(await client.GetAsync(AdresseDuLien(compte, jeton)), HttpStatusCode.Gone, LienExpire);
        await VerifierRefus(
            await client.PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(compte, jeton, "un-autre-mot-de-passe")),
            HttpStatusCode.Gone,
            LienExpire);
    }

    [Fact]
    public void Le_lien_est_valable_7_jours()
    {
        var options = _application.Services.GetRequiredService<IOptions<OptionsDuJetonDInvitation>>();

        Assert.Equal(TimeSpan.FromDays(7), options.Value.TokenLifespan);
    }

    [Fact]
    public async Task Un_lien_dont_la_duree_est_ecoulee_est_refuse()
    {
        // Identity calcule l'expiration des jetons sur l'heure réelle : on ne peut pas avancer l'horloge.
        // On règle donc la durée à zéro, pour que le lien soit déjà périmé quand on le suit.
        await using var application = new ApplicationDeTest(
            postgreSql,
            NomDeLaBase,
            services => services.Configure<OptionsDuJetonDInvitation>(options => options.TokenLifespan = TimeSpan.Zero));
        var (amira, email) = await InviterAsync(application);
        var (compte, jeton) = application.Emails.LireLeLien(email);
        var client = application.Client();

        await VerifierRefus(await client.GetAsync(AdresseDuLien(compte, jeton)), HttpStatusCode.Gone, LienExpire);
        await VerifierRefus(
            await client.PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(compte, jeton, MotDePasseChoisi)),
            HttpStatusCode.Gone,
            LienExpire);
        Assert.Equal(EtatDuCompte.Invite, (await ChargerAsync(amira)).Etat);
    }

    [Theory]
    [InlineData("compte qui n'est pas un identifiant")]
    [InlineData("compte inconnu")]
    [InlineData("jeton coupé de moitié")]
    [InlineData("jeton vide")]
    [InlineData("rien du tout")]
    public async Task Un_lien_abime_donne_le_message_du_lien_expire(string abime)
    {
        var (amira, email) = await InviterAsync();
        var (compte, jeton) = _application.Emails.LireLeLien(email);
        (string? compte, string? jeton) lien = abime switch
        {
            "compte qui n'est pas un identifiant" => ("pas-un-guid", jeton),
            "compte inconnu" => (Guid.NewGuid().ToString(), jeton),
            "jeton coupé de moitié" => (compte, jeton[..(jeton.Length / 2)]),
            "jeton vide" => (compte, ""),
            _ => (null, null),
        };
        var client = _application.Client();

        await VerifierRefus(await client.GetAsync(AdresseDuLien(lien.compte, lien.jeton)), HttpStatusCode.Gone, LienExpire);
        await VerifierRefus(
            await client.PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(lien.compte, lien.jeton, MotDePasseChoisi)),
            HttpStatusCode.Gone,
            LienExpire);
        Assert.Equal(EtatDuCompte.Invite, (await ChargerAsync(amira)).Etat);
    }

    [Fact]
    public async Task Un_lien_dont_le_compte_n_est_plus_invite_est_refuse()
    {
        var (amira, email) = await InviterAsync();
        var (compte, jeton) = _application.Emails.LireLeLien(email);

        // Modifier le compte ne change pas son tampon de sécurité : le jeton, lui, reste valable.
        await _application.DansUnScopeAsync(async services =>
        {
            var comptes = services.GetRequiredService<UserManager<Compte>>();
            var charge = await comptes.FindByIdAsync(amira.Id.ToString());
            charge!.Desactiver();
            return await comptes.UpdateAsync(charge);
        });
        var client = _application.Client();

        await VerifierRefus(await client.GetAsync(AdresseDuLien(compte, jeton)), HttpStatusCode.Gone, LienExpire);
        await VerifierRefus(
            await client.PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(compte, jeton, MotDePasseChoisi)),
            HttpStatusCode.Gone,
            LienExpire);
        Assert.Equal(EtatDuCompte.Desactive, (await ChargerAsync(amira)).Etat);
    }

    [Fact]
    public async Task Un_lien_n_est_plus_valable_quand_le_tampon_de_securite_du_compte_change()
    {
        var (amira, email) = await InviterAsync();
        var (compte, jeton) = _application.Emails.LireLeLien(email);

        // C'est ce qui rendra caduc l'ancien lien quand l'invitation sera renvoyée (CPT-04).
        await _application.DansUnScopeAsync(async services =>
        {
            var comptes = services.GetRequiredService<UserManager<Compte>>();
            var charge = await comptes.FindByIdAsync(amira.Id.ToString());
            return await comptes.UpdateSecurityStampAsync(charge!);
        });
        var client = _application.Client();

        await VerifierRefus(await client.GetAsync(AdresseDuLien(compte, jeton)), HttpStatusCode.Gone, LienExpire);
        await VerifierRefus(
            await client.PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(compte, jeton, MotDePasseChoisi)),
            HttpStatusCode.Gone,
            LienExpire);
    }

    /// <summary>Un compte invité sans mot de passe (Amira), à qui l'invitation part dans un scope de l'application.</summary>
    private async Task<(Compte Compte, EmailEnvoye Email)> InviterAsync(ApplicationDeTest? application = null)
    {
        application ??= _application;
        var amira = await application.CreerCompteAsync(_jasmins, etat: EtatDuCompte.Invite, motDePasse: null);

        await application.DansUnScopeAsync(async services =>
        {
            await services.GetRequiredService<Invitations>().EnvoyerAsync(amira, _jasmins);
            return true;
        });

        return (amira, application.Emails.Envoyes.Last());
    }

    private Task<Compte> ChargerAsync(Compte compte) =>
        _application.DansUnScopeAsync(async services =>
            (await services.GetRequiredService<UserManager<Compte>>().FindByIdAsync(compte.Id.ToString()))!);

    /// <summary>L'adresse de l'API pour un lien, avec les valeurs ré-encodées comme le fait un navigateur.</summary>
    private static string AdresseDuLien(string? compte, string? jeton)
    {
        var parametres = new List<string>();
        if (compte is not null)
            parametres.Add("compte=" + Uri.EscapeDataString(compte));
        if (jeton is not null)
            parametres.Add("jeton=" + Uri.EscapeDataString(jeton));
        return "/api/invitation?" + string.Join('&', parametres);
    }

    private static async Task VerifierRefus(HttpResponseMessage reponse, HttpStatusCode statut, Avis avis)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(avis, await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
