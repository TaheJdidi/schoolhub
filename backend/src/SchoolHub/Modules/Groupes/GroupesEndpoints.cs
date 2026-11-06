using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
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
        groupe.MapPost("/{id:guid}/fermeture", FermerAsync);
        return api;
    }

    /// <summary>GRP-08 : la personne connectée ferme le groupe ; elle est l'auteur de la ligne du journal.</summary>
    private static async Task<Results<Ok<FicheDuGroupe>, JsonHttpResult<Avis>>> FermerAsync(
        Guid id,
        FermetureDUnGroupe saisie,
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        GroupesDuCentre groupes)
    {
        var auteur = await comptes.GetUserAsync(utilisateur) ?? throw new InvalidOperationException("Aucun compte connecté.");
        var issue = await groupes.FermerAsync(auteur, id, saisie);
        return issue.Fiche is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Ok(issue.Fiche);
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

/// <summary>
/// Ce que la page envoie pour créer un groupe. Un nom vide prend le nom proposé ; un type vide fait une Étude.
/// Une Révision ou une Formation prend ses dates des créneaux, ou de <see cref="Dates"/> si la liste n'est pas vide.
/// </summary>
public sealed record CreationDUnGroupe(
    Guid? ProfesseurId,
    string? Matiere,
    Guid? NiveauId,
    List<CreneauSaisi>? Creneaux,
    decimal? PrixDuMois,
    DateOnly? DateDeDebut,
    int? Capacite,
    string? Nom,
    string? Type = null,
    decimal? PrixTotal = null,
    int? NombreDeSeances = null,
    List<SeanceSaisie>? Dates = null,
    List<TrancheSaisie>? Tranches = null,
    int? MoisEnCours = null,
    int? SeanceEnCours = null);

/// <summary>Une séance saisie une à une : sa date et ses heures « HH:mm ».</summary>
public sealed record SeanceSaisie(DateOnly? Date, string? Debut, string? Fin);

/// <summary>Une tranche d'une Formation telle que la page l'envoie : son montant et la séance où elle est due.</summary>
public sealed record TrancheSaisie(decimal? Montant, int? Seance);

/// <summary>Ce que la page envoie pour fermer un groupe (GRP-08) : la date de fermeture et le motif.</summary>
public sealed record FermetureDUnGroupe(DateOnly? Date, string? Motif);

/// <summary>Ce que la page envoie pour modifier un groupe : tout ensemble. Un nom vide reprend le nom proposé.</summary>
public sealed record ModificationDUnGroupe(string? Nom, int? Capacite, decimal? PrixDuMois);

/// <summary>
/// Un groupe dans la liste des groupes du centre, pour les cadres de la page Professeurs. <see cref="Niveau"/> est
/// le nom du niveau ; <see cref="Eleves"/> compte ses inscriptions (ELV-03).
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
    int Eleves,
    bool Termine,
    bool Ferme);

/// <summary>Un créneau tel que l'API le décrit, avec ses heures « HH:mm », quelle que soit la langue du serveur.</summary>
public sealed record CreneauDuGroupe(string Jour, string Debut, string Fin)
{
    public static CreneauDuGroupe De(Creneau creneau) =>
        new(creneau.Jour.ToString(), creneau.Debut.ToString("HH:mm", CultureInfo.InvariantCulture), creneau.Fin.ToString("HH:mm", CultureInfo.InvariantCulture));
}

public sealed record ProfesseurDuGroupe(Guid Id, string Prenom, string Nom);

public sealed record NiveauDuGroupe(Guid Id, string Nom);

/// <summary>Une séance d'Étude ; <see cref="AppelFait"/> dit si son appel est enregistré (SEA-02, SEA-07).</summary>
public sealed record SeanceDuGroupe(Guid Id, DateOnly Date, string Debut, string Fin, bool AppelFait = false, string Nature = "Prevue")
{
    public static SeanceDuGroupe De(Seance seance) =>
        new(seance.Id, seance.Date, seance.Debut.ToString("HH:mm", CultureInfo.InvariantCulture), seance.Fin.ToString("HH:mm", CultureInfo.InvariantCulture),
            seance.AppelFaitLe is not null, seance.Nature.ToString());
}

/// <summary>
/// SEA-02 : une séance qui ne compte pas : annulée (avec son motif, et la date de son remplacement s'il y en a un) ou
/// supplémentaire. <see cref="Nature"/> vaut « Prevue », « Remplacement » ou « Supplementaire ».
/// </summary>
public sealed record SeanceHorsCompte(
    Guid Id,
    DateOnly Date,
    string Debut,
    string Fin,
    string Nature,
    bool Annulee,
    string? Motif,
    DateOnly? RemplaceeLe,
    bool AppelFait);

/// <summary>
/// Une séance de Révision ou de Formation, avec son numéro (à partir de la première séance dans SchoolHub) et, pour une
/// Révision, son prix.
/// </summary>
public sealed record SeanceNumerotee(Guid Id, int Numero, DateOnly Date, string Debut, string Fin, decimal? Prix, bool AppelFait = false, string Nature = "Prevue")
{
    public static SeanceNumerotee De(Seance seance, int numero, IReadOnlyList<decimal>? prixDesSeances) => new(
        seance.Id,
        numero,
        seance.Date,
        seance.Debut.ToString("HH:mm", CultureInfo.InvariantCulture),
        seance.Fin.ToString("HH:mm", CultureInfo.InvariantCulture),
        prixDesSeances is { } prix && numero <= prix.Count ? prix[numero - 1] : null,
        seance.AppelFaitLe is not null,
        seance.Nature.ToString());
}

/// <summary>Une tranche d'une Formation : son montant, et la séance où elle est due.</summary>
public sealed record TrancheDuGroupe(decimal Montant, int Seance);

/// <summary>Un Mois du groupe : son numéro, son prix, s'il a commencé (sa séance 1 a eu lieu), et ses séances.</summary>
public sealed record MoisDuGroupe(int Numero, decimal Prix, bool Commence, List<SeanceDuGroupe> Seances);

/// <summary>
/// La fiche d'un groupe. Étude : <see cref="Mois"/>, et <see cref="PrixDuMois"/>, le prix du premier Mois qui n'a pas
/// commencé (celui que le formulaire de modification propose). Révision et Formation : <see cref="Seances"/> numérotées,
/// <see cref="PrixTotal"/>, <see cref="NombreDeSeances"/> et, pour une Formation, <see cref="Tranches"/>.
/// <see cref="Eleves"/> compte ses inscriptions (ELV-03).
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
    decimal? PrixDuMois,
    List<MoisDuGroupe> Mois,
    bool Termine,
    int PremiereSeance,
    decimal? PrixTotal,
    int? NombreDeSeances,
    List<SeanceNumerotee> Seances,
    List<TrancheDuGroupe> Tranches,
    DateOnly? FermeLe,
    string? MotifDeFermeture,
    List<SeanceHorsCompte>? HorsCompte = null);
