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
/// CPT-03 : retrouver l'accès par « mot de passe oublié ». Amira (secrétaire des Jasmins) a un compte actif,
/// avec le mot de passe <c>jasmins-2027</c> : chaque test lui fait demander un lien, puis suit le lien de l'email.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class MotDePasseOublieTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "comptes_mot_de_passe_oublie";

    // Le mot de passe que CreerCompteAsync donne à Amira.
    private const string AncienMotDePasse = "jasmins-2027";
    private const string NouveauMotDePasse = "oliviers-2027";
    private const string MauvaisMotDePasse = "inconnu-2027";

    private static readonly Avis LienEnvoye = new("Si un compte existe pour cet email, un lien vient de partir");
    private static readonly Avis LienExpire = new("Ce lien a expiré : refaites une demande de mot de passe oublié");
    private static readonly Avis LienDInvitationExpire = new("Ce lien a expiré : demandez au propriétaire une nouvelle invitation");
    private static readonly Avis TropCourt = new("Le mot de passe doit faire au moins 8 caractères");
    private static readonly Avis Incorrect = new("Email ou mot de passe incorrect");
    private static readonly Avis TropDEssais = new("Trop d'essais : réessayez dans 15 minutes");
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
    public async Task La_reponse_est_la_meme_que_le_compte_existe_ou_non()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();

        var existant = await DemanderUnLien(client, amira.Email);
        var inconnu = await DemanderUnLien(client, "inconnue@exemple.tn");

        Assert.Equal(HttpStatusCode.OK, existant.StatusCode);
        Assert.Equal(LienEnvoye, await existant.Content.ReadFromJsonAsync<Avis>());
        Assert.Equal(HttpStatusCode.OK, inconnu.StatusCode);
        Assert.Equal(LienEnvoye, await inconnu.Content.ReadFromJsonAsync<Avis>());
    }

    [Fact]
    public async Task Un_compte_actif_recoit_un_lien()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);

        await DemanderUnLien(_application.Client(), amira.Email);

        var email = Assert.Single(_application.Emails.Envoyes);
        var (compte, jeton) = _application.Emails.LireLeLien(email);
        Assert.Equal(amira.Id.ToString(), compte);
        Assert.Equal(amira.Email, email.Destinataire);
        Assert.Equal("Changer votre mot de passe SchoolHub", email.Sujet);
        Assert.Equal(
            "Bonjour Amira,\n\n"
            + "Pour changer votre mot de passe SchoolHub, ouvrez ce lien, valable 1 heure :\n"
            + $"http://localhost:5087/nouveau-mot-de-passe?compte={amira.Id}&jeton={Uri.EscapeDataString(jeton)}\n\n"
            + "Si vous n'avez rien demandé, ignorez cet email.",
            email.Texte);
    }

    [Theory]
    [InlineData("inconnu")]
    [InlineData("desactive")]
    [InlineData("invite")]
    public async Task Aucun_email_pour_un_compte_inconnu_desactive_ou_invite(string cas)
    {
        var email = cas switch
        {
            "desactive" => (await _application.CreerCompteAsync(_jasmins, etat: EtatDuCompte.Desactive)).Email,
            "invite" => (await _application.CreerCompteAsync(_jasmins, etat: EtatDuCompte.Invite, motDePasse: null)).Email,
            _ => "inconnue@exemple.tn",
        };

        var reponse = await DemanderUnLien(_application.Client(), email);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(LienEnvoye, await reponse.Content.ReadFromJsonAsync<Avis>());
        Assert.Empty(_application.Emails.Envoyes);
    }

    [Fact]
    public async Task Le_lien_change_le_mot_de_passe_et_connecte()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var (compte, jeton) = await DemanderEtLireLeLienAsync(amira);
        var client = _application.Client();

        var reponse = await ChangerLeMotDePasse(client, compte, jeton, NouveauMotDePasse);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(Amira, await reponse.Content.ReadFromJsonAsync<Moi>());

        // La personne est déjà connectée, avec le cookie posé par cette réponse.
        var moi = await client.GetAsync("/api/moi");
        Assert.Equal(HttpStatusCode.OK, moi.StatusCode);
        Assert.Equal(Amira, await moi.Content.ReadFromJsonAsync<Moi>());

        // L'ancien mot de passe ne sert plus, le nouveau sert.
        var autreClient = _application.Client();
        await VerifierRefus(await Connexion(autreClient, amira.Email, AncienMotDePasse), HttpStatusCode.Unauthorized, Incorrect);
        Assert.Equal(HttpStatusCode.OK, (await Connexion(autreClient, amira.Email, NouveauMotDePasse)).StatusCode);
    }

    [Fact]
    public async Task Le_lien_ne_sert_qu_une_fois()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var (compte, jeton) = await DemanderEtLireLeLienAsync(amira);
        var premier = await ChangerLeMotDePasse(_application.Client(), compte, jeton, NouveauMotDePasse);
        Assert.Equal(HttpStatusCode.OK, premier.StatusCode);

        var second = await ChangerLeMotDePasse(_application.Client(), compte, jeton, "un-autre-mot-de-passe");

        await VerifierRefus(second, HttpStatusCode.Gone, LienExpire);
        Assert.Equal(HttpStatusCode.OK, (await Connexion(_application.Client(), amira.Email, NouveauMotDePasse)).StatusCode);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Un_mot_de_passe_trop_court_est_refuse(string? motDePasse)
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var (compte, jeton) = await DemanderEtLireLeLienAsync(amira);
        var client = _application.Client();

        var reponse = await ChangerLeMotDePasse(client, compte, jeton, motDePasse);

        await VerifierRefus(reponse, HttpStatusCode.BadRequest, TropCourt);
        Assert.Equal(HttpStatusCode.OK, (await Connexion(_application.Client(), amira.Email, AncienMotDePasse)).StatusCode);

        // Le lien n'est pas consommé par un refus : la personne peut réessayer.
        var encore = await ChangerLeMotDePasse(client, compte, jeton, NouveauMotDePasse);
        Assert.Equal(HttpStatusCode.OK, encore.StatusCode);
    }

    [Fact]
    public void Le_lien_est_valable_1_heure()
    {
        var options = _application.Services.GetRequiredService<IOptions<OptionsDuJetonDeMotDePasseOublie>>();

        Assert.Equal(TimeSpan.FromHours(1), options.Value.TokenLifespan);
    }

    [Fact]
    public async Task Un_lien_dont_la_duree_est_ecoulee_est_refuse()
    {
        // Identity calcule l'expiration des jetons sur l'heure réelle : on ne peut pas avancer l'horloge.
        // On règle donc la durée à zéro, pour que le lien soit déjà périmé quand on le suit.
        await using var application = new ApplicationDeTest(
            postgreSql,
            NomDeLaBase,
            services => services.Configure<OptionsDuJetonDeMotDePasseOublie>(options => options.TokenLifespan = TimeSpan.Zero));
        var amira = await application.CreerCompteAsync(_jasmins);
        var (compte, jeton) = await DemanderEtLireLeLienAsync(amira, application);

        var reponse = await ChangerLeMotDePasse(application.Client(), compte, jeton, NouveauMotDePasse);

        await VerifierRefus(reponse, HttpStatusCode.Gone, LienExpire);
        Assert.Equal(HttpStatusCode.OK, (await Connexion(application.Client(), amira.Email, AncienMotDePasse)).StatusCode);
    }

    [Fact]
    public async Task Le_lien_leve_le_blocage()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();
        for (var essai = 1; essai <= 4; essai++)
            await VerifierRefus(await Connexion(client, amira.Email, MauvaisMotDePasse), HttpStatusCode.Unauthorized, Incorrect);
        await VerifierRefus(await Connexion(client, amira.Email, MauvaisMotDePasse), HttpStatusCode.Unauthorized, TropDEssais);
        await VerifierRefus(await Connexion(client, amira.Email, AncienMotDePasse), HttpStatusCode.Unauthorized, TropDEssais);

        var (compte, jeton) = await DemanderEtLireLeLienAsync(amira);
        var reponse = await ChangerLeMotDePasse(_application.Client(), compte, jeton, NouveauMotDePasse);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

        // Pas besoin d'attendre les 15 minutes.
        var connexion = await Connexion(_application.Client(), amira.Email, NouveauMotDePasse);
        Assert.Equal(HttpStatusCode.OK, connexion.StatusCode);
    }

    [Fact]
    public async Task Le_lien_remet_le_compteur_d_essais_a_zero()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();
        for (var essai = 1; essai <= 3; essai++)
            await VerifierRefus(await Connexion(client, amira.Email, MauvaisMotDePasse), HttpStatusCode.Unauthorized, Incorrect);

        var (compte, jeton) = await DemanderEtLireLeLienAsync(amira);
        Assert.Equal(HttpStatusCode.OK, (await ChangerLeMotDePasse(_application.Client(), compte, jeton, NouveauMotDePasse)).StatusCode);

        // Les 3 essais faux d'avant ne comptent plus : il faut de nouveau 5 essais faux de suite pour être bloqué.
        for (var essai = 1; essai <= 4; essai++)
            await VerifierRefus(await Connexion(client, amira.Email, MauvaisMotDePasse), HttpStatusCode.Unauthorized, Incorrect);
    }

    // {email} est remplacé par l'email du compte, {EMAIL} par le même en majuscules.
    [Theory]
    [InlineData("  {email}  ")]
    [InlineData("{EMAIL}")]
    [InlineData("  {EMAIL}  ")]
    public async Task Un_email_avec_majuscules_ou_espaces_recoit_son_lien(string saisie)
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var email = saisie.Replace("{email}", amira.Email).Replace("{EMAIL}", amira.Email!.ToUpperInvariant());

        var reponse = await DemanderUnLien(_application.Client(), email);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var envoye = Assert.Single(_application.Emails.Envoyes);
        Assert.Equal(amira.Email, envoye.Destinataire);
    }

    [Fact]
    public async Task Un_formulaire_vide_donne_la_reponse_habituelle()
    {
        await _application.CreerCompteAsync(_jasmins);
        var client = _application.Client();

        var vide = await client.PostAsync("/api/mot-de-passe-oublie", JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.OK, vide.StatusCode);
        Assert.Equal(LienEnvoye, await vide.Content.ReadFromJsonAsync<Avis>());

        foreach (var email in new string?[] { null, "", "   " })
        {
            var reponse = await DemanderUnLien(client, email);
            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
            Assert.Equal(LienEnvoye, await reponse.Content.ReadFromJsonAsync<Avis>());
        }

        Assert.Empty(_application.Emails.Envoyes);
    }

    [Fact]
    public async Task Une_panne_d_envoi_donne_la_meme_reponse_que_si_tout_allait_bien()
    {
        // Si la panne se voyait seulement pour un compte qui existe, elle révélerait qu'il existe.
        var amira = await _application.CreerCompteAsync(_jasmins);
        _application.Emails.Echouer = true;

        var reponse = await DemanderUnLien(_application.Client(), amira.Email);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(LienEnvoye, await reponse.Content.ReadFromJsonAsync<Avis>());
    }

    [Theory]
    [InlineData("compte qui n'est pas un identifiant")]
    [InlineData("compte inconnu")]
    [InlineData("jeton coupé de moitié")]
    [InlineData("jeton vide")]
    [InlineData("rien du tout")]
    public async Task Un_lien_abime_donne_le_message_du_lien_expire(string abime)
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var (compte, jeton) = await DemanderEtLireLeLienAsync(amira);
        (string? compte, string? jeton) lien = abime switch
        {
            "compte qui n'est pas un identifiant" => ("pas-un-guid", jeton),
            "compte inconnu" => (Guid.NewGuid().ToString(), jeton),
            "jeton coupé de moitié" => (compte, jeton[..(jeton.Length / 2)]),
            "jeton vide" => (compte, ""),
            _ => (null, null),
        };

        var reponse = await ChangerLeMotDePasse(_application.Client(), lien.compte, lien.jeton, NouveauMotDePasse);

        await VerifierRefus(reponse, HttpStatusCode.Gone, LienExpire);
        Assert.Equal(HttpStatusCode.OK, (await Connexion(_application.Client(), amira.Email, AncienMotDePasse)).StatusCode);
    }

    [Fact]
    public async Task Un_lien_invalide_donne_le_message_du_lien_expire_meme_avec_un_mot_de_passe_trop_court()
    {
        // Le lien est vérifié avant le mot de passe : inutile de faire corriger un mot de passe qui ne servira pas.
        var amira = await _application.CreerCompteAsync(_jasmins);
        var (compte, jeton) = await DemanderEtLireLeLienAsync(amira);

        var reponse = await ChangerLeMotDePasse(_application.Client(), compte, jeton[..(jeton.Length / 2)], "1234567");

        await VerifierRefus(reponse, HttpStatusCode.Gone, LienExpire);
    }

    [Fact]
    public async Task Un_lien_dont_le_compte_est_desactive_est_refuse()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var (compte, jeton) = await DemanderEtLireLeLienAsync(amira);

        // Modifier le compte ne change pas son tampon de sécurité : le jeton, lui, reste valable.
        await _application.DansUnScopeAsync(async services =>
        {
            var comptes = services.GetRequiredService<UserManager<Compte>>();
            var charge = await comptes.FindByIdAsync(amira.Id.ToString());
            charge!.Desactiver();
            return await comptes.UpdateAsync(charge);
        });

        var reponse = await ChangerLeMotDePasse(_application.Client(), compte, jeton, NouveauMotDePasse);

        await VerifierRefus(reponse, HttpStatusCode.Gone, LienExpire);
        var apres = await ChargerAsync(amira);
        Assert.Equal(EtatDuCompte.Desactive, apres.Etat);
        Assert.Equal(amira.PasswordHash, apres.PasswordHash);
    }

    // Point de vigilance : les liens d'invitation et de mot de passe oublié sont des jetons de la même famille.
    // Aucun des deux ne doit servir à la place de l'autre, ni par le fournisseur, ni par le but.
    [Fact]
    public async Task Un_jeton_d_invitation_ne_sert_pas_a_changer_de_mot_de_passe()
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var jeton = await FabriquerUnJetonAsync(amira, JetonDInvitation.Nom, JetonDInvitation.But);

        var reponse = await ChangerLeMotDePasse(_application.Client(), amira.Id.ToString(), jeton, NouveauMotDePasse);

        await VerifierRefus(reponse, HttpStatusCode.Gone, LienExpire);
        Assert.Equal(HttpStatusCode.OK, (await Connexion(_application.Client(), amira.Email, AncienMotDePasse)).StatusCode);
    }

    [Fact]
    public async Task Un_jeton_de_mot_de_passe_oublie_ne_sert_pas_a_choisir_son_mot_de_passe_d_invitation()
    {
        var invitee = await _application.CreerCompteAsync(_jasmins, etat: EtatDuCompte.Invite, motDePasse: null);
        var jeton = await _application.DansUnScopeAsync(services =>
            services.GetRequiredService<UserManager<Compte>>().GeneratePasswordResetTokenAsync(invitee));

        var reponse = await _application.Client().PostAsJsonAsync(
            "/api/invitation",
            new ChoixDuMotDePasse(invitee.Id.ToString(), jeton, NouveauMotDePasse));

        await VerifierRefus(reponse, HttpStatusCode.Gone, LienDInvitationExpire);
        Assert.Equal(EtatDuCompte.Invite, (await ChargerAsync(invitee)).Etat);
    }

    // Un jeton qui a le bon fournisseur et le mauvais but, ou l'inverse, est refusé des deux côtés :
    // chacune des deux protections suffit seule à séparer les deux liens.
    [Theory]
    [InlineData(JetonDInvitation.Nom, UserManager<Compte>.ResetPasswordTokenPurpose)]
    [InlineData(JetonDeMotDePasseOublie.Nom, JetonDInvitation.But)]
    public async Task Un_jeton_a_moitie_confondu_est_refuse_des_deux_cotes(string fournisseur, string but)
    {
        var amira = await _application.CreerCompteAsync(_jasmins);
        var invitee = await _application.CreerCompteAsync(_jasmins, etat: EtatDuCompte.Invite, motDePasse: null);
        var jetonDAmira = await FabriquerUnJetonAsync(amira, fournisseur, but);
        var jetonDeLInvitee = await FabriquerUnJetonAsync(invitee, fournisseur, but);

        var motDePasseOublie = await ChangerLeMotDePasse(_application.Client(), amira.Id.ToString(), jetonDAmira, NouveauMotDePasse);
        var invitation = await _application.Client().PostAsJsonAsync(
            "/api/invitation",
            new ChoixDuMotDePasse(invitee.Id.ToString(), jetonDeLInvitee, NouveauMotDePasse));

        await VerifierRefus(motDePasseOublie, HttpStatusCode.Gone, LienExpire);
        await VerifierRefus(invitation, HttpStatusCode.Gone, LienDInvitationExpire);
    }

    /// <summary>Fait la demande de lien d'un compte, puis lit le compte et le jeton du dernier email envoyé.</summary>
    private async Task<(string Compte, string Jeton)> DemanderEtLireLeLienAsync(Compte compte, ApplicationDeTest? application = null)
    {
        application ??= _application;
        var reponse = await DemanderUnLien(application.Client(), compte.Email);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return application.Emails.LireLeLien(application.Emails.Envoyes.Last());
    }

    /// <summary>Un jeton fabriqué pour ce compte avec un fournisseur et un but au choix, comme le ferait un autre morceau de l'application.</summary>
    private Task<string> FabriquerUnJetonAsync(Compte compte, string fournisseur, string but) =>
        _application.DansUnScopeAsync(services =>
            services.GetRequiredService<UserManager<Compte>>().GenerateUserTokenAsync(compte, fournisseur, but));

    private Task<Compte> ChargerAsync(Compte compte) =>
        _application.DansUnScopeAsync(async services =>
            (await services.GetRequiredService<UserManager<Compte>>().FindByIdAsync(compte.Id.ToString()))!);

    private static Task<HttpResponseMessage> DemanderUnLien(HttpClient client, string? email) =>
        client.PostAsJsonAsync("/api/mot-de-passe-oublie", new DemandeDeLien(email));

    private static Task<HttpResponseMessage> ChangerLeMotDePasse(HttpClient client, string? compte, string? jeton, string? motDePasse) =>
        client.PostAsJsonAsync("/api/nouveau-mot-de-passe", new ChoixDuMotDePasse(compte, jeton, motDePasse));

    private static Task<HttpResponseMessage> Connexion(HttpClient client, string? email, string? motDePasse) =>
        client.PostAsJsonAsync("/api/connexion", new DemandeDeConnexion(email, motDePasse));

    private static async Task VerifierRefus(HttpResponseMessage reponse, HttpStatusCode statut, Avis avis)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(avis, await reponse.Content.ReadFromJsonAsync<Avis>());
    }
}
