using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Centres;

/// <summary>
/// CEN-01 : la commande <c>creer-centre</c> ouvre un centre et invite son premier propriétaire.
/// Chaque test la lance comme le fera <c>Program.cs</c>, avec les services de l'application de test,
/// et un code de centre et un email qui lui sont propres.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class CreationDeCentreTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "centres_creation";

    private const string CodeMalForme = "Le code ne peut contenir que des lettres minuscules, des chiffres et des tirets.";

    private ApplicationDeTest _application = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        await _application.InitialiserLaBaseAsync();
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    [Fact]
    public async Task La_commande_cree_le_centre_et_son_proprietaire_invite()
    {
        var code = CodeUnique();
        var email = EmailUnique();

        var (resultat, sortie) = await LancerAsync(Arguments(code, email));

        Assert.Equal(0, resultat);
        Assert.Equal($"Centre « Centre Les Jasmins » créé. Invitation envoyée à {email}.{Environment.NewLine}", sortie);

        var centre = await CentreAsync(code);
        Assert.NotNull(centre);
        Assert.Equal("Centre Les Jasmins", centre.Nom);

        var hedi = await CompteAsync(email);
        Assert.NotNull(hedi);
        Assert.Equal("Hedi", hedi.Prenom);
        Assert.Equal("Ben Salah", hedi.Nom);
        Assert.Equal(RoleDuCompte.Proprietaire, hedi.Role);
        Assert.Equal(EtatDuCompte.Invite, hedi.Etat);
        Assert.Equal(centre.Id, hedi.CentreId);

        var invitation = Assert.Single(_application.Emails.Envoyes);
        Assert.Equal(email, invitation.Destinataire);
        Assert.Equal("Rejoindre le Centre Les Jasmins sur SchoolHub", invitation.Sujet);
    }

    [Fact]
    public async Task Le_lien_de_l_email_permet_au_proprietaire_de_choisir_son_mot_de_passe()
    {
        // Le jeton est fabriqué avant la validation de la transaction : il doit être valable une fois le compte enregistré.
        var email = EmailUnique();
        await LancerAsync(Arguments(CodeUnique(), email));
        var (compte, jeton) = _application.Emails.LireLeLien(Assert.Single(_application.Emails.Envoyes));
        var client = _application.Client();

        var reponse = await client.PostAsJsonAsync("/api/invitation", new ChoixDuMotDePasse(compte, jeton, "motdepas"));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(
            new Moi("Hedi", "Ben Salah", "Proprietaire", "Centre Les Jasmins"),
            await reponse.Content.ReadFromJsonAsync<Moi>());
        Assert.Equal(EtatDuCompte.Actif, (await CompteAsync(email))!.Etat);
    }

    [Fact]
    public async Task Un_code_deja_pris_est_refuse_sans_rien_creer()
    {
        var existant = await _application.CreerCentreAsync();
        var email = EmailUnique();

        var (resultat, sortie) = await LancerAsync(Arguments(existant.Code, email));

        VerifierRefus(resultat, sortie, $"Le code « {existant.Code} » est déjà pris par un autre centre.");
        Assert.Null(await CompteAsync(email));
        Assert.Empty(_application.Emails.Envoyes);
    }

    [Fact]
    public async Task Un_email_deja_utilise_est_refuse_sans_rien_creer()
    {
        var centre = await _application.CreerCentreAsync();
        var existant = await _application.CreerCompteAsync(centre);
        var code = CodeUnique();

        var (resultat, sortie) = await LancerAsync(Arguments(code, existant.Email!));

        VerifierRefus(resultat, sortie, $"L'email {existant.Email} est déjà utilisé sur SchoolHub.");
        Assert.Null(await CentreAsync(code));
        Assert.Empty(_application.Emails.Envoyes);
    }

    [Fact]
    public async Task Un_email_deja_utilise_avec_une_autre_casse_est_refuse()
    {
        // Un même email écrit autrement reste le même email : sinon, deux comptes se partageraient une adresse.
        var centre = await _application.CreerCentreAsync();
        var suffixe = Guid.NewGuid().ToString("N")[..8];
        await _application.CreerCompteAsync(centre, email: $"hedi.x.{suffixe}@exemple.tn");
        var autreCasse = $"HEDI.X.{suffixe}@Exemple.tn";
        var code = CodeUnique();

        var (resultat, sortie) = await LancerAsync(Arguments(code, autreCasse));

        VerifierRefus(resultat, sortie, $"L'email {autreCasse} est déjà utilisé sur SchoolHub.");
        Assert.Null(await CentreAsync(code));
        Assert.Empty(_application.Emails.Envoyes);
    }

    [Theory]
    [InlineData("Les Jasmins")]
    [InlineData("les_jasmins")]
    [InlineData("-jasmins")]
    [InlineData("jasmins-")]
    [InlineData("LES-JASMINS")]
    [InlineData("les--jasmins")]
    public async Task Un_code_mal_forme_est_refuse(string code)
    {
        var email = EmailUnique();

        var (resultat, sortie) = await LancerAsync(Arguments(code, email));

        VerifierRefus(resultat, sortie, CodeMalForme);
        Assert.Null(await CentreAsync(code));
        Assert.Null(await CompteAsync(email));
        Assert.Empty(_application.Emails.Envoyes);
    }

    [Theory]
    [MemberData(nameof(ChampsManquants))]
    public async Task Un_champ_manquant_ou_vide_est_refuse(string option, string forme, string message)
    {
        var code = CodeUnique();
        var email = EmailUnique();
        var arguments = Arguments(code, email).ToList();
        var position = arguments.IndexOf(option);
        switch (forme)
        {
            case "absent":
                arguments.RemoveRange(position, 2);
                break;
            case "vide":
                arguments[position + 1] = "";
                break;
            case "espaces":
                arguments[position + 1] = "   ";
                break;
            case "sans valeur, en dernier":
                arguments.RemoveRange(position, 2);
                arguments.Add(option);
                break;
            case "sans valeur, suivi d'une autre option":
                arguments.RemoveAt(position + 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(forme), forme, null);
        }

        var (resultat, sortie) = await LancerAsync([.. arguments]);

        VerifierRefus(resultat, sortie, message);
        Assert.Null(await CentreAsync(code));
        Assert.Empty(_application.Emails.Envoyes);
    }

    public static TheoryData<string, string, string> ChampsManquants()
    {
        var champs = new Dictionary<string, string>
        {
            ["--nom"] = "Il manque le nom du centre.",
            ["--code"] = "Il manque le code du centre.",
            ["--prenom"] = "Il manque le prénom du propriétaire.",
            ["--nom-proprietaire"] = "Il manque le nom du propriétaire.",
            ["--email"] = "Il manque l'email du propriétaire.",
        };
        var formes = new[] { "absent", "vide", "espaces", "sans valeur, en dernier", "sans valeur, suivi d'une autre option" };

        var donnees = new TheoryData<string, string, string>();
        foreach (var (option, message) in champs)
            foreach (var forme in formes)
                donnees.Add(option, forme, message);
        return donnees;
    }

    [Fact]
    public async Task Les_valeurs_sont_nettoyees_des_espaces_qui_les_entourent()
    {
        var code = CodeUnique();
        var email = EmailUnique();

        var (resultat, _) = await LancerAsync(
            ["--nom", "  Centre Les Jasmins ", "--code", $" {code}", "--prenom", "Hedi  ", "--nom-proprietaire", " Ben Salah", "--email", $"  {email} "]);

        Assert.Equal(0, resultat);
        Assert.Equal("Centre Les Jasmins", (await CentreAsync(code))!.Nom);
        Assert.Equal("Ben Salah", (await CompteAsync(email))!.Nom);
    }

    [Theory]
    [InlineData("hedi@exemple")]
    [InlineData("hedi")]
    [InlineData("Hedi <hedi@exemple.tn>")]
    [InlineData("hedi@exemple.tn, amira@exemple.tn")]
    public async Task Un_email_mal_forme_est_refuse(string email)
    {
        var code = CodeUnique();

        var (resultat, sortie) = await LancerAsync(Arguments(code, email));

        VerifierRefus(resultat, sortie, $"L'email {email} n'est pas valable.");
        Assert.Null(await CentreAsync(code));
        Assert.Empty(_application.Emails.Envoyes);
    }

    [Fact]
    public async Task Une_option_inconnue_est_refusee()
    {
        var code = CodeUnique();

        var (resultat, sortie) = await LancerAsync([.. Arguments(code, EmailUnique()), "--ville", "Tunis"]);

        VerifierRefus(resultat, sortie, "Option inconnue : --ville.");
        Assert.Null(await CentreAsync(code));
        Assert.Empty(_application.Emails.Envoyes);
    }

    [Fact]
    public async Task Si_l_invitation_ne_part_pas_rien_n_est_cree()
    {
        var code = CodeUnique();
        var email = EmailUnique();
        _application.Emails.Echouer = true;

        var (resultat, sortie) = await LancerAsync(Arguments(code, email));

        VerifierRefus(resultat, sortie, "Rien n'a été créé : l'invitation n'est pas partie (Brevo ne répond pas).");
        Assert.Null(await CentreAsync(code));
        Assert.Null(await CompteAsync(email));
        Assert.Equal(0, await LignesDuJournalAsync(email));

        // On relance simplement la commande, avec les mêmes valeurs : rien n'est resté de la première fois.
        _application.Emails.Echouer = false;
        var (encore, _) = await LancerAsync(Arguments(code, email));
        Assert.Equal(0, encore);
        Assert.NotNull(await CentreAsync(code));
        Assert.NotNull(await CompteAsync(email));
        Assert.Equal(1, await LignesDuJournalAsync(email));
    }

    [Fact]
    public async Task La_commande_donne_les_29_niveaux_d_office_au_centre()
    {
        var code = CodeUnique();

        await LancerAsync(Arguments(code, EmailUnique()));

        var centre = await CentreAsync(code);
        var noms = await _application.DansUnScopeAsync(services =>
            services.GetRequiredService<SchoolHubDbContext>().Niveaux
                .IgnoreQueryFilters()
                .Where(n => n.CentreId == centre!.Id && n.DOffice)
                .OrderBy(n => n.Rang)
                .Select(n => n.Nom)
                .ToListAsync());
        Assert.Equal(NiveauxDOffice.Liste, noms);
    }

    [Fact]
    public async Task La_commande_note_la_creation_du_proprietaire_au_journal()
    {
        var code = CodeUnique();
        var email = EmailUnique();

        await LancerAsync(Arguments(code, email));

        var centre = await CentreAsync(code);
        var ligne = Assert.Single(await _application.JournalAsync(centre!));
        Assert.Equal(ActionDuJournal.CompteCree, ligne.Action);
        Assert.Null(ligne.AuteurId);
        Assert.Equal("SchoolHub", ligne.Auteur);
        Assert.Equal($"Hedi Ben Salah · Propriétaire · {email}", ligne.Detail);
    }

    [Fact]
    public async Task Si_le_compte_ne_peut_pas_etre_cree_le_centre_n_est_pas_cree()
    {
        // Le centre est déjà enregistré dans la transaction quand le compte est refusé : il doit disparaître avec elle.
        await using var application = new ApplicationDeTest(
            postgreSql,
            NomDeLaBase,
            services => services.AddScoped<IUserValidator<Compte>, ValidateurQuiRefuse>());
        var code = CodeUnique();
        var email = EmailUnique();

        var (resultat, sortie) = await LancerAsync(Arguments(code, email), application);

        VerifierRefus(resultat, sortie, ValidateurQuiRefuse.Description);
        Assert.Null(await CentreAsync(code));
        Assert.Null(await CompteAsync(email));
        Assert.Empty(application.Emails.Envoyes);
    }

    private static string CodeUnique() => "les-jasmins-" + Guid.NewGuid().ToString("N")[..8];

    private static string EmailUnique() => $"hedi.{Guid.NewGuid().ToString("N")[..8]}@exemple.tn";

    /// <summary>Les options de la commande, toutes bien remplies (Hedi Ben Salah, du Centre Les Jasmins).</summary>
    private static string[] Arguments(string code, string email) =>
        ["--nom", "Centre Les Jasmins", "--code", code, "--prenom", "Hedi", "--nom-proprietaire", "Ben Salah", "--email", email];

    /// <summary>Lance la commande, et rend son code de sortie et ce qu'elle a écrit.</summary>
    private async Task<(int Resultat, string Sortie)> LancerAsync(string[] arguments, ApplicationDeTest? application = null)
    {
        var sortie = new StringWriter();
        var resultat = await CommandeCreerCentre.ExecuterAsync((application ?? _application).Services, arguments, sortie);
        return (resultat, sortie.ToString());
    }

    private static void VerifierRefus(int resultat, string sortie, string message)
    {
        Assert.Equal(1, resultat);
        Assert.Equal(message + Environment.NewLine, sortie);
    }

    private Task<Centre?> CentreAsync(string code) =>
        _application.DansUnScopeAsync(services =>
            services.GetRequiredService<SchoolHubDbContext>().Centres.SingleOrDefaultAsync(c => c.Code == code));

    private Task<Compte?> CompteAsync(string email) =>
        _application.DansUnScopeAsync(services =>
            services.GetRequiredService<SchoolHubDbContext>().Users.SingleOrDefaultAsync(c => c.Email == email));

    /// <summary>Les lignes du journal qui parlent de cet email, tous centres confondus : le centre peut ne pas exister.</summary>
    private Task<int> LignesDuJournalAsync(string email) =>
        _application.DansUnScopeAsync(services =>
            services.GetRequiredService<SchoolHubDbContext>().LignesDuJournal
                .IgnoreQueryFilters()
                .CountAsync(l => l.Detail.Contains(email)));

    /// <summary>Un contrôle de plus sur les comptes, qui les refuse tous : pour tester ce que fait la commande quand le compte échoue.</summary>
    private sealed class ValidateurQuiRefuse : IUserValidator<Compte>
    {
        public const string Description = "Ce compte est refusé pour l'essai.";

        public Task<IdentityResult> ValidateAsync(UserManager<Compte> manager, Compte compte) =>
            Task.FromResult(IdentityResult.Failed(new IdentityError { Description = Description }));
    }
}
