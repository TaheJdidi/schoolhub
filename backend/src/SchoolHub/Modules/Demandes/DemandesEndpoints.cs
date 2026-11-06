using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Emails;

namespace SchoolHub.Modules.Demandes;

/// <summary>
/// DEM-01 et DEM-02 : le formulaire public, ouvert à tous, et son lien, pour les personnes connectées. DEM-03 à DEM-05 :
/// la liste des demandes, leur enregistrement comme élève inscrit et leur refus.
/// </summary>
public static class DemandesEndpoints
{
    /// <summary>La limite d'essais du formulaire public : 30 envois par heure et par adresse, acceptés ou refusés (DEM-01).</summary>
    public const string LimiteDuFormulaire = "formulaire-public";

    /// <summary>
    /// La protection du formulaire public contre les envois abusifs : 5 demandes enregistrées par heure et par adresse
    /// (<see cref="LimiteDesDemandes"/>), et 30 essais, pour arrêter un robot qui n'envoie que des demandes fausses.
    /// Derrière l'App Service d'Azure, l'adresse du visiteur est la dernière de « X-Forwarded-For », ajoutée par Azure : un
    /// visiteur ne peut pas se faire passer pour un autre en écrivant cet en-tête lui-même.
    /// </summary>
    public static IServiceCollection AjouterLaProtectionDuFormulaire(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        services.AddSingleton<LimiteDesDemandes>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (contexte, annulation) =>
                new ValueTask(contexte.HttpContext.Response.WriteAsJsonAsync(new Avis(FormulairePublic.TropDEnvois), annulation));
            options.AddPolicy(LimiteDuFormulaire, contexte => RateLimitPartition.GetFixedWindowLimiter(
                contexte.Connection.RemoteIpAddress?.ToString() ?? "inconnue",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
        });
        return services;
    }

    public static IEndpointRouteBuilder MapDemandes(this IEndpointRouteBuilder api)
    {
        var formulaire = api.MapGroup("/public/inscription").AllowAnonymous();
        formulaire.MapGet("/{code}", LireAsync);
        formulaire.MapPost("/{code}", EnvoyerAsync).RequireRateLimiting(LimiteDuFormulaire);

        // DEM-02 : le lien et son QR code, pour toute personne connectée du centre.
        var demandes = api.MapGroup("/demandes");
        demandes.MapGet("/lien", async (SchoolHubDbContext contexte, CentreCourant centre, IOptions<OptionsDeLApplication> options) =>
            new LienPublic(await LienAsync(contexte, centre, options.Value)));
        demandes.MapGet("/qr-code", async (SchoolHubDbContext contexte, CentreCourant centre, IOptions<OptionsDeLApplication> options) =>
        {
            using var generateur = new QRCodeGenerator();
            using var donnees = generateur.CreateQrCode(await LienAsync(contexte, centre, options.Value), QRCodeGenerator.ECCLevel.Q);
            return TypedResults.File(new PngByteQRCode(donnees).GetGraphic(12), "image/png");
        });

        // DEM-03 à DEM-05 : traiter les demandes, pour toute personne connectée du centre.
        demandes.MapGet("", (bool? traitees, DemandesDuCentre demandesDuCentre) => demandesDuCentre.ListerAsync(traitees ?? false));
        demandes.MapGet("/nombre", async (DemandesDuCentre demandesDuCentre) => new NombreDeDemandes(await demandesDuCentre.CompterAsync()));
        demandes.MapGet("/{id:guid}", LireUneDemandeAsync);
        demandes.MapPost("/{id:guid}/inscription", EnregistrerAsync);
        demandes.MapPost("/{id:guid}/refus", RefuserAsync);
        return api;
    }

    private static async Task<Results<Ok<DetailDeLaDemande>, JsonHttpResult<Avis>>> LireUneDemandeAsync(Guid id, DemandesDuCentre demandes) =>
        await demandes.LireAsync(id) is { } detail
            ? TypedResults.Ok(detail)
            : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, DemandesDuCentre.DemandeIntrouvable);

    private static async Task<Results<Created<DemandeEnregistree>, JsonHttpResult<Avis>>> EnregistrerAsync(
        Guid id,
        DemandeAEnregistrer saisie,
        DemandesDuCentre demandes)
    {
        var issue = await demandes.EnregistrerAsync(id, saisie);
        return issue.Enregistree is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Created($"/api/eleves/{issue.Enregistree.Eleve.Id}", issue.Enregistree);
    }

    private static async Task<Results<NoContent, JsonHttpResult<Avis>>> RefuserAsync(Guid id, RefusSaisi saisie, DemandesDuCentre demandes)
    {
        var issue = await demandes.RefuserAsync(id, saisie);
        return issue.Message is null ? TypedResults.NoContent() : ConnexionEndpoints.Refus(issue.Statut, issue.Message);
    }

    /// <summary>« https://app.schoolhub.tn/inscription/les-jasmins » : l'adresse de l'application et le code du centre.</summary>
    private static async Task<string> LienAsync(SchoolHubDbContext contexte, CentreCourant centre, OptionsDeLApplication options)
    {
        var id = centre.Id ?? throw new InvalidOperationException("Aucun centre courant.");
        var code = await contexte.Centres.Where(c => c.Id == id).Select(c => c.Code).SingleAsync();
        return $"{options.Adresse.TrimEnd('/')}/inscription/{code}";
    }

    private static async Task<Results<Ok<FormulaireDuCentre>, JsonHttpResult<Avis>>> LireAsync(string code, FormulairePublic formulaire) =>
        await formulaire.LireAsync(code) is { } trouve
            ? TypedResults.Ok(trouve)
            : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, "Ce lien d'inscription n'existe pas");

    private static async Task<Results<Created<DemandeRecue>, JsonHttpResult<Avis>>> EnvoyerAsync(
        string code,
        DemandeEnvoyee envoi,
        FormulairePublic formulaire,
        HttpContext requete)
    {
        var issue = await formulaire.EnvoyerAsync(code, envoi, requete.Connection.RemoteIpAddress?.ToString() ?? "inconnue");
        return issue.Recue is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Created((string?)null, issue.Recue);
    }
}

/// <summary>Un niveau proposé par le formulaire ; « dOffice » sert à la règle de la 7ᵉ année, comme sur la fiche élève.</summary>
public sealed record NiveauDuFormulaire(Guid Id, string Nom, bool DOffice);

/// <summary>Un groupe proposé : la matière, le niveau (vide pour une Formation ouverte à tous), le type, le professeur et l'horaire.</summary>
public sealed record GroupeDuFormulaire(Guid Id, string Matiere, Guid? NiveauId, string Type, string Professeur, List<CreneauDuGroupe> Creneaux);

/// <summary>Le formulaire public d'un centre : son nom, son année scolaire (« 2027-2028 »), ses niveaux et ses groupes.</summary>
public sealed record FormulaireDuCentre(string Centre, string? Annee, List<NiveauDuFormulaire> Niveaux, List<GroupeDuFormulaire> Groupes);

/// <summary>
/// Ce que le formulaire envoie : la fiche de l'élève, son groupe préféré pour chaque matière, les amis cités, et le champ
/// piège <see cref="SiteWeb"/>, invisible, qu'un robot remplit.
/// </summary>
public sealed record DemandeEnvoyee(
    string? Prenom,
    string? Nom,
    Guid? NiveauId,
    bool Majeur,
    string? TelephoneParent,
    string? EmailParent,
    string? TelephoneEleve,
    string? EmailEleve,
    string? Etablissement,
    List<Guid>? Groupes,
    string? Amis,
    string? SiteWeb)
{
    /// <summary>La fiche de l'élève, vérifiée par les règles d'ELV-02.</summary>
    public FicheSaisieDeLEleve Fiche => new(Prenom, Nom, NiveauId, Majeur, TelephoneParent, EmailParent, TelephoneEleve, EmailEleve, Etablissement);
}

/// <summary>DEM-02 : le lien public du formulaire du centre.</summary>
public sealed record LienPublic(string Lien);

/// <summary>La réponse d'un envoi : le prénom, pour « Demande envoyée. Merci Adem ! ».</summary>
public sealed record DemandeRecue(string Prenom);

/// <summary>DEM-03 : le nombre de demandes à traiter, pour le menu et l'accueil.</summary>
public sealed record NombreDeDemandes(int ATraiter);

/// <summary>Une matière demandée, avec le professeur et l'horaire du groupe préféré : « Mathématiques (samedi 10h) ».</summary>
public sealed record MatiereDemandee(string Matiere, string Professeur, List<CreneauDuGroupe> Creneaux);

/// <summary>
/// DEM-03 : une demande dans la liste. <see cref="Etat"/> vaut « ATraiter », « Inscrite » ou « Refusee » ;
/// <see cref="Motif"/>, « Doublon » ou « FausseDemande » pour une demande refusée.
/// </summary>
public sealed record DemandeDeLaListe(
    Guid Id,
    string Prenom,
    string Nom,
    NiveauDeLEleve Niveau,
    List<MatiereDemandee> Matieres,
    string? Amis,
    DateTimeOffset RecueLe,
    bool DoublonPossible,
    string Etat,
    string? Motif,
    DateTimeOffset? TraiteeLe);

/// <summary>
/// DEM-04 : un groupe où inscrire l'élève pour une matière : ses élèves et sa capacité, les modes à choisir (vide s'il n'y
/// a pas de choix), sa prochaine séance (la première de l'élève), et les amis cités déjà inscrits.
/// </summary>
public sealed record GroupeProposeALaDemande(
    Guid Id,
    string Nom,
    string Type,
    string Professeur,
    List<CreneauDuGroupe> Creneaux,
    int Eleves,
    int? Capacite,
    List<string> Modes,
    int NombreDeTranches,
    SeanceDeDebut PremiereSeance,
    List<string> Amis);

/// <summary>Une matière demandée : le groupe préféré, et les groupes où l'inscrire (le préféré en tête, s'il est encore proposé).</summary>
public sealed record MatiereDeLaDemande(string Matiere, Guid GroupePrefere, List<GroupeProposeALaDemande> Groupes);

/// <summary>DEM-04 : une demande ouverte, sa fiche telle qu'envoyée, les fiches qui lui ressemblent, et ses matières.</summary>
public sealed record DetailDeLaDemande(
    Guid Id,
    string Prenom,
    string Nom,
    NiveauDeLEleve Niveau,
    bool Majeur,
    string? TelephoneParent,
    string? EmailParent,
    string? TelephoneEleve,
    string? EmailEleve,
    string? Etablissement,
    string? Amis,
    DateTimeOffset RecueLe,
    string Etat,
    string? Motif,
    DateTimeOffset? TraiteeLe,
    Guid? EleveId,
    List<EleveTrouve> FichesSemblables,
    bool AutreDemandeSemblable,
    List<MatiereDeLaDemande> Matieres);

/// <summary>Un groupe choisi pour une matière, et son mode quand le groupe offre un choix.</summary>
public sealed record GroupeChoisi(Guid? GroupeId, string? Mode);

/// <summary>DEM-04 : l'élève (une fiche existante, ou la fiche vérifiée de la demande) et les groupes choisis.</summary>
public sealed record DemandeAEnregistrer(Guid? EleveId, FicheSaisieDeLEleve? Fiche, List<GroupeChoisi>? Groupes);

/// <summary>DEM-04 : l'élève inscrit, ses inscriptions, les avertissements, et le message de confirmation pour ses destinataires.</summary>
public sealed record DemandeEnregistree(
    EleveInscrit Eleve,
    List<EnregistrementDeLInscription> Inscriptions,
    string? Avertissement,
    string Texte,
    List<MessageWhatsApp> Messages);

/// <summary>DEM-05 : le motif du refus, « Doublon » ou « FausseDemande ».</summary>
public sealed record RefusSaisi(string? Motif);
