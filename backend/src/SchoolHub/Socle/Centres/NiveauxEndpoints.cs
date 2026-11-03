namespace SchoolHub.Socle.Centres;

/// <summary>
/// CEN-03 : les adresses des niveaux. Toute personne connectée lit la liste : la secrétaire en a besoin
/// pour les groupes, les élèves et le formulaire public.
/// </summary>
public static class NiveauxEndpoints
{
    public static IEndpointRouteBuilder MapNiveaux(this IEndpointRouteBuilder api)
    {
        var groupe = api.MapGroup("/niveaux");
        groupe.MapGet("", async (NiveauxDuCentre niveaux) => (await niveaux.ListerAsync()).Select(NiveauDuCentre.De).ToList());
        return api;
    }
}

/// <summary>Un niveau tel que l'API le décrit : <see cref="DOffice"/> dit s'il vient de la liste tunisienne.</summary>
public sealed record NiveauDuCentre(Guid Id, string Nom, bool DOffice)
{
    public static NiveauDuCentre De(Niveau niveau) => new(niveau.Id, niveau.Nom, niveau.DOffice);
}
