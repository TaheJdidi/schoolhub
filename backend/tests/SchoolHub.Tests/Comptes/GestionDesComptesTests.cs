using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Emails;
using SchoolHub.Socle.Journal;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Comptes;

/// <summary>
/// CPT-04 et CPT-05 : le propriétaire gère les comptes de son centre depuis la page « Comptes ».
/// Chaque test part d'un nouveau centre « Jasmins », dont Hedi Ben Salah est le propriétaire, connecté.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class GestionDesComptesTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "comptes_gestion";

    private const string ChampsObligatoires = "Le prénom, le nom et le rôle sont obligatoires";
    private const string NomTropLong = "Le prénom et le nom font au plus 100 caractères";
    private const string EmailPasValable = "Cet email n'est pas valable";
    private const string EmailDejaUtilise = "Cet email est déjà utilisé sur SchoolHub";
    private const string InvitationPasPartie = "L'invitation n'est pas partie : réessayez dans un moment";
    private const string AccesReserve = "Accès réservé au propriétaire";
    private const string Introuvable = "Compte introuvable";
    private const string PlusDInvitation = "Ce compte n'attend plus d'invitation";
    private const string PasSoiMeme = "Vous ne pouvez pas désactiver votre propre compte";
    private const string DejaDesactive = "Ce compte est déjà désactivé";
    private const string PasDesactive = "Ce compte n'est pas désactivé";
    private const string CompteDesactive = "Ce compte est désactivé : adressez-vous au propriétaire du centre";
    private const string VientDeChanger = "Ce compte vient de changer : rechargez la page";

    private readonly List<ApplicationDeTest> _autresApplications = [];
    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private Compte _hedi = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        _hedi = await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);
        _client = await _application.ClientConnecteAsync(_hedi);
    }

    public async Task DisposeAsync()
    {
        foreach (var application in _autresApplications)
            await application.DisposeAsync();
        await _application.DisposeAsync();
    }

    // ----- Inviter (CPT-04) -----

    [Fact]
    public async Task Inviter_cree_un_compte_invite_envoie_l_email_et_note_le_journal()
    {
        var email = EmailUnique("sonia");

        var reponse = await InviterAsync("Sonia", "Mabrouk", email, "Secretaire");

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var sonia = await ChargerAsync(email);
        Assert.NotNull(sonia);
        Assert.Equal(
            new CompteDuCentre(sonia.Id, "Sonia", "Mabrouk", email, "Secretaire", "Invite", Moi: false),
            await reponse.Content.ReadFromJsonAsync<CompteDuCentre>());
        Assert.Equal(EtatDuCompte.Invite, sonia.Etat);
        Assert.Equal(RoleDuCompte.Secretaire, sonia.Role);
        Assert.Equal(_jasmins.Id, sonia.CentreId);

        // L'invitation est celle de CPT-02 : son lien accueille Sonia.
        var invitation = Assert.Single(_application.Emails.Envoyes);
        Assert.Equal(email, invitation.Destinataire);
        var (compte, jeton) = _application.Emails.LireLeLien(invitation);
        var accueil = await _application.Client().GetAsync($"/api/invitation?compte={compte}&jeton={Uri.EscapeDataString(jeton)}");
        Assert.Equal(HttpStatusCode.OK, accueil.StatusCode);

        var ligne = Assert.Single(await _application.JournalAsync(_jasmins));
        Assert.Equal(ActionDuJournal.CompteCree, ligne.Action);
        Assert.Equal(_hedi.Id, ligne.AuteurId);
        Assert.Equal("Hedi Ben Salah", ligne.Auteur);
        Assert.Equal($"Sonia Mabrouk · Secrétaire · {email}", ligne.Detail);
        Assert.Null(ligne.Avant);
        Assert.Null(ligne.Apres);
    }

    [Theory]
    [InlineData(null, "Mabrouk", "Secretaire")]
    [InlineData("Sonia", "  ", "Secretaire")]
    [InlineData("Sonia", "Mabrouk", null)]
    public async Task Un_champ_manquant_est_refuse(string? prenom, string? nom, string? role)
    {
        var email = EmailUnique("sonia");

        var reponse = await InviterAsync(prenom, nom, email, role);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, ChampsObligatoires);
        await VerifierQueRienNEstCreeAsync(email);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("proprietaire")]
    [InlineData("0")]
    public async Task Un_role_inconnu_est_refuse(string role)
    {
        var email = EmailUnique("sonia");

        var reponse = await InviterAsync("Sonia", "Mabrouk", email, role);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, ChampsObligatoires);
        await VerifierQueRienNEstCreeAsync(email);
    }

    [Fact]
    public async Task Un_prenom_trop_long_est_refuse()
    {
        var email = EmailUnique("sonia");

        var reponse = await InviterAsync(new string('a', 101), "Mabrouk", email, "Secretaire");

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, NomTropLong);
        await VerifierQueRienNEstCreeAsync(email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sonia")]
    [InlineData("Sonia <sonia@exemple.tn>")]
    public async Task Un_email_pas_valable_est_refuse(string email)
    {
        var reponse = await InviterAsync("Sonia", "Mabrouk", email, "Secretaire");

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, EmailPasValable);
        Assert.Empty(_application.Emails.Envoyes);
    }

    [Fact]
    public async Task Un_email_trop_long_pour_la_base_est_refuse()
    {
        // 257 caractères : la colonne de l'email en garde 256.
        var email = new string('s', 257 - "@exemple.tn".Length) + "@exemple.tn";

        var reponse = await InviterAsync("Sonia", "Mabrouk", email, "Secretaire");

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, EmailPasValable);
    }

    [Fact]
    public async Task Un_email_deja_utilise_dans_un_autre_centre_est_refuse()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var karim = await _application.CreerCompteAsync(oliviers, "Karim", "Ben Salah");

        var reponse = await InviterAsync("Karim", "Ben Salah", karim.Email, "Secretaire");

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, EmailDejaUtilise);
        Assert.Empty(_application.Emails.Envoyes);
        Assert.Empty(await _application.JournalAsync(_jasmins));
    }

    [Fact]
    public async Task Inviter_retire_les_espaces_et_l_email_en_majuscules_est_deja_pris()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var email = $"Sonia.Mabrouk.{unique}@Exemple.tn";

        var reponse = await InviterAsync("  Sonia ", " Mabrouk  ", $"  {email}  ", "Secretaire");

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var sonia = await ChargerAsync(email);
        Assert.NotNull(sonia);
        Assert.Equal(("Sonia", "Mabrouk", email), (sonia.Prenom, sonia.Nom, sonia.Email));

        var memeEmail = await InviterAsync("Sonia", "Mabrouk", email.ToLowerInvariant(), "Secretaire");
        await VerifierLeRefusAsync(memeEmail, HttpStatusCode.Conflict, EmailDejaUtilise);
    }

    [Fact]
    public async Task Si_l_email_ne_part_pas_rien_n_est_cree()
    {
        var email = EmailUnique("sonia");
        _application.Emails.Echouer = true;

        var reponse = await InviterAsync("Sonia", "Mabrouk", email, "Secretaire");

        await VerifierLeRefusAsync(reponse, HttpStatusCode.ServiceUnavailable, InvitationPasPartie);
        await VerifierQueRienNEstCreeAsync(email);
    }

    // ----- Renvoyer l'invitation (CPT-04) -----

    [Fact]
    public async Task Renvoyer_l_invitation_donne_un_nouveau_lien_et_tue_l_ancien()
    {
        var (sonia, premierLien) = await InviterSoniaAsync();

        var reponse = await _client.PostAsync($"/api/comptes/{sonia}/invitation", null);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("Invite", (await reponse.Content.ReadFromJsonAsync<CompteDuCentre>())!.Etat);
        Assert.Equal(2, _application.Emails.Envoyes.Count);
        Assert.Equal(HttpStatusCode.Gone, (await OuvrirLeLienAsync(premierLien)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await OuvrirLeLienAsync(_application.Emails.Envoyes.Last())).StatusCode);

        // Renvoyer l'invitation ne va pas au journal : seule la création y est.
        Assert.Equal(ActionDuJournal.CompteCree, Assert.Single(await _application.JournalAsync(_jasmins)).Action);
    }

    [Fact]
    public async Task Renvoyer_l_invitation_d_un_compte_actif_est_refuse()
    {
        var bilel = await _application.CreerCompteAsync(_jasmins, "Bilel", "Haddad");

        var reponse = await _client.PostAsync($"/api/comptes/{bilel.Id}/invitation", null);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, PlusDInvitation);
        Assert.Empty(_application.Emails.Envoyes);
    }

    [Fact]
    public async Task Si_le_nouvel_email_ne_part_pas_l_ancien_lien_marche_encore()
    {
        var (sonia, premierLien) = await InviterSoniaAsync();
        _application.Emails.Echouer = true;

        var reponse = await _client.PostAsync($"/api/comptes/{sonia}/invitation", null);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.ServiceUnavailable, InvitationPasPartie);
        Assert.Equal(HttpStatusCode.OK, (await OuvrirLeLienAsync(premierLien)).StatusCode);
    }

    [Fact]
    public async Task Renvoyer_l_invitation_d_un_compte_qui_vient_de_changer_est_refuse()
    {
        var (sonia, _) = await InviterSoniaAsync();

        // Une première requête lit Sonia ; une autre renvoie son invitation et enregistre avant elle.
        await using var scope = _application.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CentreCourant>().Definir(_jasmins.Id);
        await scope.ServiceProvider.GetRequiredService<UserManager<Compte>>().FindByIdAsync(sonia.ToString());
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/comptes/{sonia}/invitation", null)).StatusCode);
        var emailsAvant = _application.Emails.Envoyes.Count;

        var issue = await scope.ServiceProvider.GetRequiredService<GestionDesComptes>().RenvoyerLInvitationAsync(sonia);

        // Son enregistrement échoue : elle ne doit ni envoyer un lien mort, ni dire que tout va bien.
        Assert.Equal((StatusCodes.Status409Conflict, VientDeChanger), (issue.Statut, issue.Message));
        Assert.Equal(emailsAvant, _application.Emails.Envoyes.Count);
        Assert.Equal(HttpStatusCode.OK, (await OuvrirLeLienAsync(_application.Emails.Envoyes.Last())).StatusCode);
    }

    [Fact]
    public async Task Renvoyer_l_invitation_d_un_compte_d_un_autre_centre_repond_introuvable()
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var karim = await _application.CreerCompteAsync(oliviers, "Karim", "Ben Salah", etat: EtatDuCompte.Invite, motDePasse: null);

        var reponse = await _client.PostAsync($"/api/comptes/{karim.Id}/invitation", null);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.NotFound, Introuvable);
        Assert.Empty(_application.Emails.Envoyes);
    }

    // ----- Désactiver (CPT-05) -----

    [Fact]
    public async Task Desactiver_coupe_la_session_et_la_connexion_et_note_le_journal()
    {
        var sonia = await _application.CreerCompteAsync(_jasmins, "Sonia", "Mabrouk");
        var clientDeSonia = await _application.ClientConnecteAsync(sonia);

        var reponse = await _client.PostAsync($"/api/comptes/{sonia.Id}/desactivation", null);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("Desactive", (await reponse.Content.ReadFromJsonAsync<CompteDuCentre>())!.Etat);

        // Sa session tombe à sa requête suivante, et elle ne peut plus se connecter.
        Assert.Equal(HttpStatusCode.Unauthorized, (await clientDeSonia.GetAsync("/api/moi")).StatusCode);
        var connexion = await _application.Client().PostAsJsonAsync(
            "/api/connexion", new DemandeDeConnexion(sonia.Email, "jasmins-2027"));
        await VerifierLeRefusAsync(connexion, HttpStatusCode.Forbidden, CompteDesactive);

        var ligne = Assert.Single(await _application.JournalAsync(_jasmins));
        Assert.Equal(ActionDuJournal.CompteDesactive, ligne.Action);
        Assert.Equal("Hedi Ben Salah", ligne.Auteur);
        Assert.Equal("Sonia Mabrouk · Secrétaire", ligne.Detail);
        Assert.Equal(("Actif", "Désactivé"), (ligne.Avant, ligne.Apres));
    }

    [Fact]
    public async Task Desactiver_un_invite_tue_son_lien()
    {
        var (sonia, lien) = await InviterSoniaAsync();

        var reponse = await _client.PostAsync($"/api/comptes/{sonia}/desactivation", null);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await OuvrirLeLienAsync(lien)).StatusCode);
        var ligne = (await _application.JournalAsync(_jasmins)).Last();
        Assert.Equal(("Invité", "Désactivé"), (ligne.Avant, ligne.Apres));
    }

    [Fact]
    public async Task Personne_ne_desactive_son_propre_compte()
    {
        var reponse = await _client.PostAsync($"/api/comptes/{_hedi.Id}/desactivation", null);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.BadRequest, PasSoiMeme);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/moi")).StatusCode);
        Assert.Empty(await _application.JournalAsync(_jasmins));
    }

    [Fact]
    public async Task Desactiver_un_compte_deja_desactive_est_refuse()
    {
        var amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi", etat: EtatDuCompte.Desactive);

        var reponse = await _client.PostAsync($"/api/comptes/{amira.Id}/desactivation", null);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, DejaDesactive);
    }

    [Fact]
    public async Task Deux_proprietaires_qui_se_desactivent_en_meme_temps_en_laissent_un()
    {
        var mehdi = await _application.CreerCompteAsync(_jasmins, "Mehdi", "Trabelsi", RoleDuCompte.Proprietaire);
        var clientDeMehdi = await _application.ClientConnecteAsync(mehdi);

        var reponses = await Task.WhenAll(
            _client.PostAsync($"/api/comptes/{mehdi.Id}/desactivation", null),
            clientDeMehdi.PostAsync($"/api/comptes/{_hedi.Id}/desactivation", null));

        // L'une passe ; l'autre arrive d'une personne déjà désactivée : sa session tombe (401),
        // ou le serveur la relit une fois le verrou pris (403).
        Assert.Single(reponses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(reponses, r => r.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        var proprietairesActifs = await _application.DansUnScopeAsync(services =>
            services.GetRequiredService<UserManager<Compte>>().Users.CountAsync(c =>
                c.CentreId == _jasmins.Id && c.Role == RoleDuCompte.Proprietaire && c.Etat == EtatDuCompte.Actif));
        Assert.Equal(1, proprietairesActifs);
    }

    // ----- Réactiver (CPT-05) -----

    [Fact]
    public async Task Reactiver_rend_l_acces_avec_l_ancien_mot_de_passe()
    {
        var amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi", etat: EtatDuCompte.Desactive);

        var reponse = await _client.PostAsync($"/api/comptes/{amira.Id}/reactivation", null);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("Actif", (await reponse.Content.ReadFromJsonAsync<CompteDuCentre>())!.Etat);
        await _application.ClientConnecteAsync(amira, "jasmins-2027");

        var ligne = Assert.Single(await _application.JournalAsync(_jasmins));
        Assert.Equal(ActionDuJournal.CompteReactive, ligne.Action);
        Assert.Equal("Amira Jlassi · Secrétaire", ligne.Detail);
        Assert.Equal(("Désactivé", "Actif"), (ligne.Avant, ligne.Apres));
    }

    [Fact]
    public async Task Un_invite_desactive_puis_reactive_garde_son_ancien_lien_mort()
    {
        var (sonia, premierLien) = await InviterSoniaAsync();
        await _client.PostAsync($"/api/comptes/{sonia}/desactivation", null);

        var reponse = await _client.PostAsync($"/api/comptes/{sonia}/reactivation", null);

        Assert.Equal("Invite", (await reponse.Content.ReadFromJsonAsync<CompteDuCentre>())!.Etat);
        var reactivation = (await _application.JournalAsync(_jasmins)).Last();
        Assert.Equal(("Désactivé", "Invité"), (reactivation.Avant, reactivation.Apres));
        Assert.Equal(HttpStatusCode.Gone, (await OuvrirLeLienAsync(premierLien)).StatusCode);

        // Seul « Renvoyer l'invitation » donne un nouveau lien.
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/comptes/{sonia}/invitation", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await OuvrirLeLienAsync(_application.Emails.Envoyes.Last())).StatusCode);
    }

    [Fact]
    public async Task Reactiver_un_compte_qui_n_est_pas_desactive_est_refuse()
    {
        var bilel = await _application.CreerCompteAsync(_jasmins, "Bilel", "Haddad");

        var reponse = await _client.PostAsync($"/api/comptes/{bilel.Id}/reactivation", null);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.Conflict, PasDesactive);
    }

    // ----- Les actions sur un compte : centre et droits -----

    [Theory]
    [InlineData("desactivation", EtatDuCompte.Actif)]
    [InlineData("reactivation", EtatDuCompte.Desactive)]
    public async Task Agir_sur_un_compte_d_un_autre_centre_repond_introuvable(string action, EtatDuCompte etat)
    {
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        var karim = await _application.CreerCompteAsync(oliviers, "Karim", "Ben Salah", etat: etat);

        var reponse = await _client.PostAsync($"/api/comptes/{karim.Id}/{action}", null);

        await VerifierLeRefusAsync(reponse, HttpStatusCode.NotFound, Introuvable);
        Assert.Equal(etat, (await ChargerAsync(karim.Email!))!.Etat);
        Assert.Empty(await _application.JournalAsync(oliviers));
    }

    [Theory]
    [InlineData("desactivation")]
    [InlineData("reactivation")]
    [InlineData("invitation")]
    public async Task Une_secretaire_n_agit_pas_sur_un_compte(string action)
    {
        var amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        var bilel = await _application.CreerCompteAsync(_jasmins, "Bilel", "Haddad");
        var clientDAmira = await _application.ClientConnecteAsync(amira);

        var secretaire = await clientDAmira.PostAsync($"/api/comptes/{bilel.Id}/{action}", null);
        var sansSession = await _application.Client().PostAsync($"/api/comptes/{bilel.Id}/{action}", null);

        await VerifierLeRefusAsync(secretaire, HttpStatusCode.Forbidden, AccesReserve);
        Assert.Equal(HttpStatusCode.Unauthorized, sansSession.StatusCode);
    }

    // ----- Un onglet fermé pendant l'envoi de l'email -----

    [Fact]
    public async Task Une_invitation_partie_est_enregistree_meme_si_l_onglet_se_ferme_pendant_l_envoi()
    {
        var (application, envoi) = await ApplicationQuiCoupeLeClientAsync();
        var email = EmailUnique("sonia");

        await DansLeCentreAsync(application, gestion =>
            gestion.InviterAsync(_hedi, new NouvelleInvitation("Sonia", "Mabrouk", email, "Secretaire"), envoi.Onglet.Token));

        // L'email est parti : le compte vers lequel pointe son lien doit exister.
        Assert.Single(envoi.Envoyes);
        var sonia = await ChargerAsync(email);
        Assert.NotNull(sonia);
        Assert.Equal(ActionDuJournal.CompteCree, Assert.Single(await _application.JournalAsync(_jasmins)).Action);
    }

    [Fact]
    public async Task Une_invitation_renvoyee_garde_son_nouveau_lien_meme_si_l_onglet_se_ferme_pendant_l_envoi()
    {
        var (sonia, premierLien) = await InviterSoniaAsync();
        var (application, envoi) = await ApplicationQuiCoupeLeClientAsync();

        await DansLeCentreAsync(application, gestion => gestion.RenvoyerLInvitationAsync(sonia, envoi.Onglet.Token));

        Assert.Single(envoi.Envoyes);
        Assert.Equal(HttpStatusCode.OK, (await OuvrirLeLienAsync(envoi.Envoyes.Single())).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await OuvrirLeLienAsync(premierLien)).StatusCode);
    }

    // ----- La liste -----

    [Fact]
    public async Task La_liste_montre_les_comptes_du_centre_dans_l_ordre()
    {
        await _application.CreerCompteAsync(_jasmins, "Sonia", "Mabrouk", etat: EtatDuCompte.Invite, motDePasse: null);
        await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi", etat: EtatDuCompte.Desactive);
        await _application.CreerCompteAsync(_jasmins, "Bilel", "Haddad");
        var oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
        await _application.CreerCompteAsync(oliviers, "Karim", "Ben Salah");

        var liste = await _client.GetFromJsonAsync<List<CompteDuCentre>>("/api/comptes");

        Assert.NotNull(liste);
        Assert.Equal(new[] { "Bilel", "Hedi", "Sonia", "Amira" }, liste.Select(c => c.Prenom));
        Assert.Equal(_hedi.Id, Assert.Single(liste, c => c.Moi).Id);
        Assert.Equal("Desactive", liste.Last().Etat);
    }

    // ----- Les droits -----

    [Fact]
    public async Task Une_secretaire_ne_gere_pas_les_comptes()
    {
        var amira = await _application.CreerCompteAsync(_jasmins, "Amira", "Jlassi");
        var client = await _application.ClientConnecteAsync(amira);

        var liste = await client.GetAsync("/api/comptes");
        var invitation = await client.PostAsJsonAsync(
            "/api/comptes", new NouvelleInvitation("Sonia", "Mabrouk", EmailUnique("sonia"), "Secretaire"));

        await VerifierLeRefusAsync(liste, HttpStatusCode.Forbidden, AccesReserve);
        await VerifierLeRefusAsync(invitation, HttpStatusCode.Forbidden, AccesReserve);
    }

    [Fact]
    public async Task Sans_session_les_comptes_repondent_401()
    {
        var client = _application.Client();

        var liste = await client.GetAsync("/api/comptes");
        var invitation = await client.PostAsJsonAsync(
            "/api/comptes", new NouvelleInvitation("Sonia", "Mabrouk", EmailUnique("sonia"), "Secretaire"));

        Assert.Equal(HttpStatusCode.Unauthorized, liste.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invitation.StatusCode);
    }

    // ----- Outils -----

    private Task<HttpResponseMessage> InviterAsync(string? prenom, string? nom, string? email, string? role) =>
        _client.PostAsJsonAsync("/api/comptes", new NouvelleInvitation(prenom, nom, email, role));

    /// <summary>Hedi invite Sonia par l'API : l'identifiant de son compte, et l'email de son invitation.</summary>
    private async Task<(Guid Compte, EmailEnvoye Invitation)> InviterSoniaAsync()
    {
        var reponse = await InviterAsync("Sonia", "Mabrouk", EmailUnique("sonia"), "Secretaire");
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var sonia = await reponse.Content.ReadFromJsonAsync<CompteDuCentre>();
        return (sonia!.Id, _application.Emails.Envoyes.Last());
    }

    /// <summary>Ouvre le lien d'une invitation, comme la page d'invitation : 200 s'il est valable, 410 sinon.</summary>
    private Task<HttpResponseMessage> OuvrirLeLienAsync(EmailEnvoye invitation)
    {
        var (compte, jeton) = _application.Emails.LireLeLien(invitation);
        return _application.Client().GetAsync($"/api/invitation?compte={compte}&jeton={Uri.EscapeDataString(jeton)}");
    }

    /// <summary>
    /// L'application, sur la même base, avec un expéditeur dont l'email part, mais pendant l'envoi duquel
    /// le propriétaire ferme son onglet : la requête est alors annulée.
    /// </summary>
    private async Task<(ApplicationDeTest Application, EnvoiPendantLequelLOngletSeFerme Envoi)> ApplicationQuiCoupeLeClientAsync()
    {
        var envoi = new EnvoiPendantLequelLOngletSeFerme();
        var application = new ApplicationDeTest(postgreSql, NomDeLaBase, services =>
        {
            services.RemoveAll<IEnvoiEmail>();
            services.AddSingleton<IEnvoiEmail>(envoi);
        });
        await application.InitialiserLaBaseAsync();
        _autresApplications.Add(application);
        return (application, envoi);
    }

    /// <summary>Lance une action de la gestion des comptes comme une requête de Hedi, dans le centre des Jasmins.</summary>
    private Task<IssueDeLAction> DansLeCentreAsync(ApplicationDeTest application, Func<GestionDesComptes, Task<IssueDeLAction>> action) =>
        application.DansUnScopeAsync(services =>
        {
            services.GetRequiredService<CentreCourant>().Definir(_jasmins.Id);
            return action(services.GetRequiredService<GestionDesComptes>());
        });

    private Task<Compte?> ChargerAsync(string email) =>
        _application.DansUnScopeAsync(services => services.GetRequiredService<UserManager<Compte>>().FindByEmailAsync(email));

    private async Task VerifierQueRienNEstCreeAsync(string email)
    {
        Assert.Null(await ChargerAsync(email));
        Assert.Empty(_application.Emails.Envoyes);
        Assert.Empty(await _application.JournalAsync(_jasmins));
    }

    private static async Task VerifierLeRefusAsync(HttpResponseMessage reponse, HttpStatusCode statut, string message)
    {
        Assert.Equal(statut, reponse.StatusCode);
        Assert.Equal(new Avis(message), await reponse.Content.ReadFromJsonAsync<Avis>());
    }

    /// <summary>Un email que personne d'autre n'a, par exemple « sonia.1a2b3c4d@exemple.tn ».</summary>
    private static string EmailUnique(string prenom) => $"{prenom}.{Guid.NewGuid().ToString("N")[..8]}@exemple.tn";

    /// <summary>Un expéditeur qui accepte l'email, pendant que le propriétaire ferme son onglet : la requête est annulée.</summary>
    private sealed class EnvoiPendantLequelLOngletSeFerme : IEnvoiEmail
    {
        public CancellationTokenSource Onglet { get; } = new();

        public List<EmailEnvoye> Envoyes { get; } = [];

        public Task EnvoyerAsync(string destinataire, string sujet, string texte, CancellationToken annulation = default)
        {
            Envoyes.Add(new EmailEnvoye(destinataire, sujet, texte));
            Onglet.Cancel();
            return Task.CompletedTask;
        }
    }
}
