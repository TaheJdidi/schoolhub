using Microsoft.AspNetCore.Http.HttpResults;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Professeurs;

/// <summary>GRP-01, GRP-02 et GRP-03 : les adresses des professeurs, pour toute personne connectée du centre.</summary>
public static class ProfesseursEndpoints
{
    public static IEndpointRouteBuilder MapProfesseurs(this IEndpointRouteBuilder api)
    {
        var groupe = api.MapGroup("/professeurs");
        groupe.MapGet("", async (ProfesseursDuCentre professeurs) =>
            (await professeurs.ListerAsync()).Select(ProfesseurDuCentre.De).ToList());
        groupe.MapPost("", AjouterAsync);
        groupe.MapPut("/{id:guid}", ModifierAsync);
        groupe.MapDelete("/{id:guid}", SupprimerAsync);
        groupe.MapPost("/{id:guid}/depart", (Guid id, ProfesseursDuCentre professeurs) => ChangerAsync(professeurs.PartirAsync(id)));
        groupe.MapPost("/{id:guid}/retour", (Guid id, ProfesseursDuCentre professeurs) => ChangerAsync(professeurs.RevenirAsync(id)));
        return api;
    }

    private static async Task<Results<Ok<EnregistrementDuProfesseur>, JsonHttpResult<Avis>>> ModifierAsync(
        Guid id,
        FicheDuProfesseur saisie,
        ProfesseursDuCentre professeurs)
    {
        var issue = await professeurs.ModifierAsync(id, saisie);
        return issue.Professeur is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Ok(EnregistrementDuProfesseur.De(issue));
    }

    private static async Task<Results<NoContent, JsonHttpResult<Avis>>> SupprimerAsync(Guid id, ProfesseursDuCentre professeurs)
    {
        var issue = await professeurs.SupprimerAsync(id);
        return issue.Professeur is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.NoContent();
    }

    /// <summary>Le départ ou le retour : le professeur à jour, ou le refus.</summary>
    private static async Task<Results<Ok<ProfesseurDuCentre>, JsonHttpResult<Avis>>> ChangerAsync(Task<IssueSurUnProfesseur> action)
    {
        var issue = await action;
        return issue.Professeur is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Ok(ProfesseurDuCentre.De(issue.Professeur));
    }

    private static async Task<Results<Created<EnregistrementDuProfesseur>, JsonHttpResult<Avis>>> AjouterAsync(
        FicheDuProfesseur saisie,
        ProfesseursDuCentre professeurs)
    {
        var issue = await professeurs.AjouterAsync(saisie);
        return issue.Professeur is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Created($"/api/professeurs/{issue.Professeur.Id}", EnregistrementDuProfesseur.De(issue));
    }
}

/// <summary>Ce que la page envoie pour ajouter ou modifier un professeur.</summary>
public sealed record FicheDuProfesseur(string? Prenom, string? Nom, string? Telephone, string? Email);

/// <summary>Un professeur tel que l'API le décrit, avec son téléphone groupé (« 98 401 220 »).</summary>
public sealed record ProfesseurDuCentre(
    Guid Id,
    string Prenom,
    string Nom,
    string Telephone,
    string? Email,
    bool Parti,
    int GroupesOuverts)
{
    // Aucun groupe n'existe encore : GRP-04 comptera les groupes ouverts de chaque professeur.
    public static ProfesseurDuCentre De(Professeur professeur) => new(
        professeur.Id,
        professeur.Prenom,
        professeur.Nom,
        TelephoneTunisien.Grouper(professeur.Telephone),
        professeur.Email,
        professeur.Parti,
        GroupesOuverts: 0);
}

/// <summary>La réponse d'un ajout ou d'une modification : le professeur, et l'avertissement du téléphone s'il y en a un.</summary>
public sealed record EnregistrementDuProfesseur(ProfesseurDuCentre Professeur, string? Avertissement)
{
    public static EnregistrementDuProfesseur De(IssueSurUnProfesseur issue) =>
        new(ProfesseurDuCentre.De(issue.Professeur!), issue.Avertissement);
}
