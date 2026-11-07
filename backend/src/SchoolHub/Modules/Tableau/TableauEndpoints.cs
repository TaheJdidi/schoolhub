using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Tableau;

/// <summary>TDB-01 à TDB-04 : les adresses du tableau de bord et du journal, réservées au propriétaire.</summary>
public static class TableauEndpoints
{
    public static IEndpointRouteBuilder MapTableauDeBord(this IEndpointRouteBuilder api)
    {
        var tableau = api.MapGroup("/tableau-de-bord").RequireAuthorization(Politiques.Proprietaire);
        tableau.MapGet("", (string? mois, bool? annee, TableauDeBord service) => service.LireAsync(mois, annee == true));
        tableau.MapGet("/prix-speciaux", (TableauDeBord service) => service.PrixSpeciauxEtDispensesAsync());
        api.MapGet("/journal", (Guid? compteId, string? action, DateOnly? du, DateOnly? au, TableauDeBord service) => service.JournalAsync(compteId, action, du, au))
            .RequireAuthorization(Politiques.Proprietaire);
        return api;
    }
}
