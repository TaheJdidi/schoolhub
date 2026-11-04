using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Groupes;

/// <summary>GRP-04 : les adresses des groupes et des matières, pour toute personne connectée du centre.</summary>
public static class GroupesEndpoints
{
    public static IEndpointRouteBuilder MapGroupes(this IEndpointRouteBuilder api)
    {
        api.MapGet("/matieres", async (GroupesDuCentre groupes) =>
            (await groupes.MatieresAsync()).Select(m => new MatiereDuCentre(m.Id, m.Nom)).ToList());

        var groupe = api.MapGroup("/groupes");
        groupe.MapGet("", async (GroupesDuCentre groupes) => await groupes.ListerAsync());
        groupe.MapPost("", CreerAsync);
        groupe.MapGet("/{id:guid}", LireAsync);
        groupe.MapPut("/{id:guid}", ModifierAsync);
        return api;
    }

    private static async Task<Results<Ok<FicheDuGroupe>, JsonHttpResult<Avis>>> ModifierAsync(
        Guid id,
        ModificationDUnGroupe saisie,
        GroupesDuCentre groupes)
    {
        var issue = await groupes.ModifierAsync(id, saisie);
        return issue.Fiche is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Ok(issue.Fiche);
    }

    private static async Task<Results<Created<FicheDuGroupe>, JsonHttpResult<Avis>>> CreerAsync(
        CreationDUnGroupe saisie,
        GroupesDuCentre groupes)
    {
        var issue = await groupes.CreerAsync(saisie);
        return issue.Fiche is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Created($"/api/groupes/{issue.Fiche.Id}", issue.Fiche);
    }

    private static async Task<Results<Ok<FicheDuGroupe>, JsonHttpResult<Avis>>> LireAsync(Guid id, GroupesDuCentre groupes)
    {
        var fiche = await groupes.LireAsync(id);
        if (fiche is not null)
            return TypedResults.Ok(fiche);

        var refus = GroupesDuCentre.GroupeIntrouvable;
        return ConnexionEndpoints.Refus(refus.Statut, refus.Message!);
    }
}

/// <summary>Une matière du centre, pour les propositions du formulaire.</summary>
public sealed record MatiereDuCentre(Guid Id, string Nom);

/// <summary>Un créneau tel que la page l'envoie : « Samedi », « 10:00 », « 12:00 ».</summary>
public sealed record CreneauSaisi(string? Jour, string? Debut, string? Fin);

/// <summary>Ce que la page envoie pour créer un groupe d'Étude. Un nom vide prend le nom proposé.</summary>
public sealed record CreationDUnGroupe(
    Guid? ProfesseurId,
    string? Matiere,
    Guid? NiveauId,
    List<CreneauSaisi>? Creneaux,
    decimal? PrixDuMois,
    DateOnly? DateDeDebut,
    int? Capacite,
    string? Nom);

/// <summary>Ce que la page envoie pour modifier un groupe : tout ensemble. Un nom vide reprend le nom proposé.</summary>
public sealed record ModificationDUnGroupe(string? Nom, int? Capacite, decimal? PrixDuMois);

/// <summary>
/// Un groupe dans la liste des groupes du centre, pour les cadres de la page Professeurs. <see cref="Niveau"/> est
/// le nom du niveau ; <see cref="Eleves"/> vaut 0 jusqu'aux inscriptions (ELV).
/// </summary>
public sealed record ResumeDuGroupe(
    Guid Id,
    string Nom,
    string Type,
    Guid ProfesseurId,
    string Matiere,
    string? Niveau,
    List<CreneauDuGroupe> Creneaux,
    int? Capacite,
    int Eleves);

/// <summary>Un créneau tel que l'API le décrit, avec ses heures « HH:mm », quelle que soit la langue du serveur.</summary>
public sealed record CreneauDuGroupe(string Jour, string Debut, string Fin)
{
    public static CreneauDuGroupe De(Creneau creneau) =>
        new(creneau.Jour.ToString(), creneau.Debut.ToString("HH:mm", CultureInfo.InvariantCulture), creneau.Fin.ToString("HH:mm", CultureInfo.InvariantCulture));
}

public sealed record ProfesseurDuGroupe(Guid Id, string Prenom, string Nom);

public sealed record NiveauDuGroupe(Guid Id, string Nom);

public sealed record SeanceDuGroupe(Guid Id, DateOnly Date, string Debut, string Fin)
{
    public static SeanceDuGroupe De(Seance seance) =>
        new(seance.Id, seance.Date, seance.Debut.ToString("HH:mm", CultureInfo.InvariantCulture), seance.Fin.ToString("HH:mm", CultureInfo.InvariantCulture));
}

/// <summary>Un Mois du groupe : son numéro, son prix, s'il a commencé (sa séance 1 a eu lieu), et ses séances.</summary>
public sealed record MoisDuGroupe(int Numero, decimal Prix, bool Commence, List<SeanceDuGroupe> Seances);

/// <summary>
/// La fiche d'un groupe. <see cref="PrixDuMois"/> est le prix du premier Mois qui n'a pas commencé : celui que
/// le formulaire de modification propose. <see cref="Eleves"/> vaut 0 jusqu'aux inscriptions (ELV).
/// </summary>
public sealed record FicheDuGroupe(
    Guid Id,
    string Nom,
    string Type,
    ProfesseurDuGroupe Professeur,
    string Matiere,
    NiveauDuGroupe? Niveau,
    List<CreneauDuGroupe> Creneaux,
    int? Capacite,
    int Eleves,
    DateOnly DateDeDebut,
    decimal PrixDuMois,
    List<MoisDuGroupe> Mois);
