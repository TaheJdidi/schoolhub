using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// CPT-04 et CPT-05 : les adresses de la page « Comptes », réservées au propriétaire.
/// Elles lisent la requête et répondent ; les règles sont dans <see cref="GestionDesComptes"/>.
/// </summary>
public static class ComptesEndpoints
{
    public static IEndpointRouteBuilder MapComptes(this IEndpointRouteBuilder api)
    {
        var groupe = api.MapGroup("/comptes").RequireAuthorization(Politiques.Proprietaire);
        groupe.MapGet("", ListerAsync);
        groupe.MapPost("", InviterAsync);
        groupe.MapPost("/{id:guid}/invitation", (Guid id, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, GestionDesComptes gestion, CancellationToken annulation) =>
            RepondreAsync(utilisateur, comptes, () => gestion.RenvoyerLInvitationAsync(id, annulation)));
        return api;
    }

    /// <summary>La réponse d'une action sur un compte : le compte à jour, ou le refus avec son message.</summary>
    private static async Task<Results<Ok<CompteDuCentre>, JsonHttpResult<Avis>>> RepondreAsync(
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        Func<Task<IssueDeLAction>> action)
    {
        var moi = await ConnecteAsync(utilisateur, comptes);
        var issue = await action();
        return issue.Compte is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Ok(CompteDuCentre.De(issue.Compte, moi));
    }

    private static async Task<Ok<List<CompteDuCentre>>> ListerAsync(
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        GestionDesComptes gestion)
    {
        var moi = await ConnecteAsync(utilisateur, comptes);
        var liste = await gestion.ListerAsync();
        return TypedResults.Ok(liste.Select(compte => CompteDuCentre.De(compte, moi)).ToList());
    }

    private static async Task<Results<Created<CompteDuCentre>, JsonHttpResult<Avis>>> InviterAsync(
        NouvelleInvitation invitation,
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        GestionDesComptes gestion,
        CancellationToken annulation)
    {
        var moi = await ConnecteAsync(utilisateur, comptes);
        var issue = await gestion.InviterAsync(moi, invitation, annulation);
        if (issue.Compte is null)
            return ConnexionEndpoints.Refus(issue.Statut, issue.Message!);

        return TypedResults.Created($"/api/comptes/{issue.Compte.Id}", CompteDuCentre.De(issue.Compte, moi));
    }

    /// <summary>Le compte connecté : la politique d'accès et la validation de la session garantissent qu'il existe.</summary>
    private static async Task<Compte> ConnecteAsync(ClaimsPrincipal utilisateur, UserManager<Compte> comptes) =>
        await comptes.GetUserAsync(utilisateur) ?? throw new InvalidOperationException("Aucun compte connecté.");
}

/// <summary>
/// Un compte tel que la page « Comptes » le montre. Le rôle et l'état sont les noms de leur énumération
/// (« Secretaire », « Invite ») ; <see cref="Moi"/> dit si c'est le compte de la personne connectée.
/// </summary>
public sealed record CompteDuCentre(Guid Id, string Prenom, string Nom, string Email, string Role, string Etat, bool Moi)
{
    public static CompteDuCentre De(Compte compte, Compte moi) =>
        new(compte.Id, compte.Prenom, compte.Nom, compte.Email!, compte.Role.ToString(), compte.Etat.ToString(), compte.Id == moi.Id);
}
