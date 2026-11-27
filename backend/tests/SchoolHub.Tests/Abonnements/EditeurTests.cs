using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Abonnements;

/// <summary>EDI-00 : l'Éditeur entre dans le back office avec son mot de passe et le code de son application.</summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class EditeurTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private ApplicationDeTest _application = null!;
    private BackOfficeDeTest _backOffice = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, "abonnements_editeur");
        await _application.InitialiserLaBaseAsync();
        _backOffice = new BackOfficeDeTest(_application);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    [Fact]
    public async Task La_premiere_connexion_montre_le_QR_code_puis_le_code_active_la_double_authentification()
    {
        var nadia = await _application.CreerEditeurAsync();
        var client = _application.Client();

        var reponse = await BackOfficeDeTest.MotDePasseAsync(client, nadia.Email!);
        Assert.Equal(HttpStatusCode.Accepted, reponse.StatusCode);
        var etape = await reponse.Content.ReadFromJsonAsync<EtapeSuivante>();
        Assert.Equal(EtapeDeConnexion.ActivationDeLaDoubleAuthentification, etape!.Etape);

        // Le mot de passe seul n'ouvre rien.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/moi")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/editeur/centres")).StatusCode);

        var activation = await client.GetFromJsonAsync<ActivationDeLaDoubleAuthentification>("/api/connexion/activation-2fa");
        Assert.StartsWith("data:image/png;base64,", activation!.QrCode);
        var cle = Base32.Decoder(activation.Cle);

        // Recharger la page montre la même clé.
        var encore = await client.GetFromJsonAsync<ActivationDeLaDoubleAuthentification>("/api/connexion/activation-2fa");
        Assert.Equal(activation.Cle, encore!.Cle);

        var code = Totp.Code(cle, Totp.Pas(_application.Horloge.GetUtcNow()));
        var fin = await BackOfficeDeTest.CodeAsync(client, code);
        Assert.Equal(HttpStatusCode.OK, fin.StatusCode);
        var moi = await fin.Content.ReadFromJsonAsync<Moi>();
        Assert.Equal("Editeur", moi!.Role);
        Assert.Null(moi.Centre);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/editeur/centres")).StatusCode);

        // Ensuite, chaque connexion demande le code, sans QR code.
        _application.Horloge.Advance(Totp.Periode);
        var autre = _application.Client();
        var deuxieme = await (await BackOfficeDeTest.MotDePasseAsync(autre, nadia.Email!)).Content.ReadFromJsonAsync<EtapeSuivante>();
        Assert.Equal(EtapeDeConnexion.CodeDeDoubleAuthentification, deuxieme!.Etape);
        Assert.Equal(HttpStatusCode.Unauthorized, (await autre.GetAsync("/api/connexion/activation-2fa")).StatusCode);
    }

    [Fact]
    public async Task Un_code_faux_ou_deja_servi_est_refuse_et_cinq_codes_faux_bloquent_le_compte()
    {
        await _backOffice.EditeurAsync();
        var code = _backOffice.CodeDuMoment();

        // Le code qui vient de servir ne sert pas une seconde fois.
        var autre = _application.Client();
        await BackOfficeDeTest.MotDePasseAsync(autre, _backOffice.Nadia.Email!);
        var reponse = await BackOfficeDeTest.CodeAsync(autre, code);
        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
        Assert.Equal(OuvertureDeSession.CodeDeLApplicationFaux, (await reponse.Content.ReadFromJsonAsync<Avis>())!.Message);

        for (var essai = 1; essai < 5; essai++)
            await BackOfficeDeTest.CodeAsync(autre, "000000");
        var bloque = await BackOfficeDeTest.CodeAsync(autre, "000000");
        Assert.Equal(OuvertureDeSession.TropDEssais, (await bloque.Content.ReadFromJsonAsync<Avis>())!.Message);
    }

    [Fact]
    public async Task L_editeur_ne_voit_aucune_page_d_un_centre_et_un_centre_ne_voit_pas_le_back_office()
    {
        var editeur = await _backOffice.EditeurAsync();
        var reponse = await editeur.GetAsync("/api/eleves");
        Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);
        Assert.Equal("Accès réservé aux comptes d'un centre", (await reponse.Content.ReadFromJsonAsync<Avis>())!.Message);

        var centre = await _application.CreerCentreAsync();
        var proprietaire = await _application.CreerCompteAsync(centre, role: RoleDuCompte.Proprietaire);
        var client = await _application.ClientConnecteAsync(proprietaire);
        var refus = await client.GetAsync("/api/editeur/centres");
        Assert.Equal(HttpStatusCode.Forbidden, refus.StatusCode);
        Assert.Equal("Accès réservé à l'Éditeur", (await refus.Content.ReadFromJsonAsync<Avis>())!.Message);
    }

    [Fact]
    public async Task Un_proprietaire_ne_peut_pas_inviter_un_editeur()
    {
        var centre = await _application.CreerCentreAsync();
        var proprietaire = await _application.CreerCompteAsync(centre, role: RoleDuCompte.Proprietaire);
        var client = await _application.ClientConnecteAsync(proprietaire);

        var reponse = await client.PostAsJsonAsync("/api/comptes", new { Prenom = "Sami", Nom = "Ben Ali", Email = $"sami.{BackOfficeDeTest.Unique()}@exemple.tn", Role = "Editeur" });

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
    }

    [Fact]
    public async Task La_commande_creer_editeur_cree_un_compte_sans_centre_et_l_invite()
    {
        var email = $"taha.{BackOfficeDeTest.Unique()}@schoolup.tn";
        var sortie = new StringWriter();

        var code = await CommandeCreerEditeur.ExecuterAsync(_application.Services, ["--email", email, "--prenom", "Taha"], sortie);

        Assert.Equal(0, code);
        Assert.Equal($"Compte Éditeur créé. Invitation envoyée à {email}.", sortie.ToString().Trim());
        var compte = await _application.DansUnScopeAsync(services => services.GetRequiredService<UserManager<Compte>>().FindByEmailAsync(email));
        Assert.Equal(RoleDuCompte.Editeur, compte!.Role);
        Assert.Null(compte.CentreId);
        var invitation = _application.Emails.Envoyes.Last();
        Assert.Equal("Votre compte Éditeur SchoolUp", invitation.Sujet);

        // Le mot de passe choisi depuis l'invitation mène à la double authentification, pas à une session.
        var (id, jeton) = _application.Emails.LireLeLien(invitation);
        var client = _application.Client();
        var reponse = await client.PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(id, jeton, "back-office-2027"));
        Assert.Equal(HttpStatusCode.Accepted, reponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/moi")).StatusCode);
    }
}
