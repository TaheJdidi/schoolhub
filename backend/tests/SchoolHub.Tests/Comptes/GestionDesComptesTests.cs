using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
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

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

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
}
