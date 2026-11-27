using System.Net;
using System.Net.Http.Json;
using SchoolHub.Modules.Abonnements;
using SchoolHub.Socle.Comptes;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Abonnements;

/// <summary>
/// Les gestes du back office pour les tests : l'Éditeur Nadia Mansour connectée (mot de passe et code de son application),
/// le Centre Les Jasmins créé avec son propriétaire Hedi Ben Salah, ses paiements, et Hedi inscrit par son invitation.
/// </summary>
public sealed class BackOfficeDeTest(ApplicationDeTest application)
{
    public const string MotDePasseDeNadia = "back-office-2027";
    public const string MotDePasseDeHedi = "jasmins-2027";
    public const string TelephoneDeHedi = "98 401 220";

    private Compte? _nadia;
    private byte[]? _cle;

    public ApplicationDeTest Application => application;

    public Compte Nadia => _nadia ?? throw new InvalidOperationException("Nadia n'est pas encore créée.");

    /// <summary>Un client où Nadia, l'Éditeur, est connectée. Elle est créée la première fois ; ensuite, elle se reconnecte.</summary>
    public async Task<HttpClient> EditeurAsync()
    {
        if (_nadia is null)
        {
            _nadia = await application.CreerEditeurAsync(motDePasse: MotDePasseDeNadia);
            var premier = application.Client();
            Assert.Equal(HttpStatusCode.Accepted, (await MotDePasseAsync(premier, _nadia.Email!)).StatusCode);
            var activation = await premier.GetFromJsonAsync<ActivationDeLaDoubleAuthentification>("/api/connexion/activation-2fa");
            _cle = Base32.Decoder(activation!.Cle);
            Assert.Equal(HttpStatusCode.OK, (await CodeAsync(premier, CodeDuMoment())).StatusCode);
            return premier;
        }

        // Un code de l'application ne sert qu'une fois : on passe à la période suivante.
        application.Horloge.Advance(Totp.Periode);
        var client = application.Client();
        Assert.Equal(HttpStatusCode.Accepted, (await MotDePasseAsync(client, _nadia.Email!)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CodeAsync(client, CodeDuMoment())).StatusCode);
        return client;
    }

    public static string Unique() => Guid.NewGuid().ToString("N")[..8];

    public static DemandeDeCentre Jasmins(string? email = null, string? code = null, decimal? prix = null) => new(
        $"Centre Les Jasmins {Unique()}",
        code ?? $"les-jasmins-{Unique()}",
        "Hedi",
        "Ben Salah",
        email ?? $"hedi.{Unique()}@exemple.tn",
        TelephoneDeHedi,
        prix);

    public static async Task<FicheDuCentre> CreerAsync(HttpClient editeur, DemandeDeCentre? demande = null)
    {
        var reponse = await editeur.PostAsJsonAsync("/api/editeur/centres", demande ?? Jasmins());
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<FicheDuCentre>())!;
    }

    public static SaisieDePaiement Paiement(DateOnly du, DateOnly au, decimal montant = 270m, string facture = "F-2027-003", string mode = "Virement") =>
        new(du, montant, mode, du, au, facture);

    public static async Task<PaiementEnregistre> PayerAsync(HttpClient editeur, Guid centre, SaisieDePaiement paiement)
    {
        var reponse = await editeur.PostAsJsonAsync($"/api/editeur/centres/{centre}/paiements", paiement);
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return (await reponse.Content.ReadFromJsonAsync<PaiementEnregistre>())!;
    }

    public static async Task<FicheDuCentre> FicheAsync(HttpClient editeur, Guid centre) =>
        (await editeur.GetFromJsonAsync<FicheDuCentre>($"/api/editeur/centres/{centre}"))!;

    /// <summary>
    /// Hedi, le propriétaire, choisit son mot de passe depuis l'invitation reçue par email : le client rendu est connecté.
    /// Le centre doit avoir son premier paiement.
    /// </summary>
    public async Task<HttpClient> ProprietaireInscritAsync(HttpClient editeur, Guid centre)
    {
        Assert.Equal(HttpStatusCode.OK, (await editeur.PostAsync($"/api/editeur/centres/{centre}/invitation/email", null)).StatusCode);
        var (compte, jeton) = application.Emails.LireLeLien(application.Emails.Envoyes.Last(e => e.Sujet.StartsWith("Rejoindre", StringComparison.Ordinal)));
        var client = application.Client();
        var reponse = await client.PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(compte, jeton, MotDePasseDeHedi));
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        return client;
    }

    public string CodeDuMoment() => Totp.Code(_cle!, Totp.Pas(application.Horloge.GetUtcNow()));

    public static Task<HttpResponseMessage> MotDePasseAsync(HttpClient client, string email, string motDePasse = MotDePasseDeNadia) =>
        client.PostAsJsonAsync("/api/connexion", new DemandeDeConnexion(email, motDePasse));

    public static Task<HttpResponseMessage> CodeAsync(HttpClient client, string code) =>
        client.PostAsJsonAsync("/api/connexion/code-2fa", new SaisieDuCode(code));
}
