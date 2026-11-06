using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Seances;

/// <summary>SEA-07, SEA-09 à SEA-11 : les adresses de l'appel, pour toute personne connectée du centre.</summary>
public static class SeancesEndpoints
{
    public static IEndpointRouteBuilder MapSeances(this IEndpointRouteBuilder api)
    {
        var seances = api.MapGroup("/seances");
        seances.MapGet("/a-recopier", (AppelsDuCentre appels) => appels.ARecopierAsync());
        seances.MapGet("/{id:guid}/appel", LireAsync);
        seances.MapPut("/{id:guid}/appel", EnregistrerAsync);
        seances.MapPost("/{id:guid}/absences/{inscription:guid}/prevenu", PrevenirAsync);
        return api;
    }

    private static async Task<Results<Ok<AppelDeLaSeance>, JsonHttpResult<Avis>>> LireAsync(Guid id, AppelsDuCentre appels) =>
        await appels.LireAsync(id) is { } appel
            ? TypedResults.Ok(appel)
            : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, AppelsDuCentre.SeanceIntrouvable);

    /// <summary>La personne connectée fait l'appel : elle est l'auteur des corrections au journal.</summary>
    private static async Task<Results<Ok<AppelDeLaSeance>, JsonHttpResult<Avis>>> EnregistrerAsync(
        Guid id,
        AppelSaisi saisie,
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        AppelsDuCentre appels)
    {
        var auteur = await comptes.GetUserAsync(utilisateur) ?? throw new InvalidOperationException("Aucun compte connecté.");
        var issue = await appels.EnregistrerAsync(auteur, id, saisie);
        return issue.Appel is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Appel);
    }

    private static async Task<Results<NoContent, JsonHttpResult<Avis>>> PrevenirAsync(Guid id, Guid inscription, AppelsDuCentre appels)
    {
        var issue = await appels.PrevenirAsync(id, inscription);
        return issue.Message is null ? TypedResults.NoContent() : ConnexionEndpoints.Refus(issue.Statut, issue.Message);
    }
}

/// <summary>Ce que la page envoie pour l'appel : les inscriptions des élèves absents ; tous les autres sont présents.</summary>
public sealed record AppelSaisi(List<Guid>? Absents);

/// <summary>Le groupe d'une séance : son nom, son type et sa matière.</summary>
public sealed record GroupeDeLAppel(Guid Id, string Nom, string Type, string Matiere);

/// <summary>
/// Un élève à l'appel : absent ou présent. Pour un absent : le moment où ses destinataires ont été prévenus (SEA-11),
/// et le message prêt pour chacun.
/// </summary>
public sealed record EleveALAppel(
    Guid InscriptionId,
    Guid EleveId,
    string Prenom,
    string Nom,
    bool Absent,
    DateTimeOffset? PrevenuLe,
    List<MessageWhatsApp> Messages);

/// <summary>
/// L'appel d'une séance : sa date et ses heures « HH:mm », son groupe et son professeur ; <see cref="Ouvert"/> à partir
/// du jour de la séance ; <see cref="AppelFait"/> une fois enregistré.
/// </summary>
public sealed record AppelDeLaSeance(
    Guid SeanceId,
    DateOnly Date,
    string Debut,
    string Fin,
    GroupeDeLAppel Groupe,
    string Professeur,
    bool Ouvert,
    bool AppelFait,
    List<EleveALAppel> Eleves);

/// <summary>SEA-09 : une séance passée encore « appel à faire ».</summary>
public sealed record AppelARecopier(Guid SeanceId, DateOnly Date, string Debut, string Fin, GroupeDeLAppel Groupe, string Professeur);
