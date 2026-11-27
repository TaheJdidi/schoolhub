using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Abonnements;

/// <summary>
/// Les adresses des abonnements : le back office sous <c>/api/editeur</c> (rôle Éditeur avec sa double
/// authentification, EDI-00), et l'abonnement vu par le centre sous <c>/api/abonnement</c>.
/// Elles lisent la requête et répondent ; les règles sont dans <see cref="BackOfficeDesCentres"/>
/// et <see cref="AbonnementCoteCentre"/>.
/// </summary>
public static class AbonnementsEndpoints
{
    public static IServiceCollection AjouterLesAbonnements(this IServiceCollection services)
    {
        services.AddOptions<OptionsDesAbonnements>().BindConfiguration(OptionsDesAbonnements.Section);
        services.AddScoped<BackOfficeDesCentres>();
        services.AddScoped<JournalDeLEditeur>();
        services.AddScoped<AbonnementCoteCentre>();
        return services;
    }

    public static IEndpointRouteBuilder MapAbonnements(this IEndpointRouteBuilder api)
    {
        MapBackOffice(api.MapGroup("/editeur").RequireAuthorization(Politiques.Editeur));

        // L'abonnement vu par le centre : tout compte du centre le lit (les bandeaux).
        api.MapGet("/abonnement", (AbonnementCoteCentre abonnement, CancellationToken annulation) => abonnement.LireAsync(annulation));
        return api;
    }

    private static void MapBackOffice(RouteGroupBuilder editeur)
    {
        editeur.MapGet("/offre", (BackOfficeDesCentres backOffice) => backOffice.Offre());
        editeur.MapGet("/centres", (BackOfficeDesCentres backOffice, CancellationToken annulation) => backOffice.ListerAsync(annulation));
        editeur.MapGet("/journal", (BackOfficeDesCentres backOffice, CancellationToken annulation) => backOffice.JournalAsync(annulation));
        editeur.MapPost("/centres", async (DemandeDeCentre demande, [AsParameters] Contexte c) =>
        {
            var issue = await c.BackOffice.CreerAsync(await c.EditeurAsync(), demande, c.Annulation);
            return issue.Valeur is null
                ? (Results<Created<FicheDuCentre>, JsonHttpResult<Avis>>)ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
                : TypedResults.Created($"/api/editeur/centres/{issue.Valeur.Id}", issue.Valeur);
        });

        var fiche = editeur.MapGroup("/centres/{id:guid}");
        fiche.MapGet("", async Task<Results<Ok<FicheDuCentre>, JsonHttpResult<Avis>>> (Guid id, BackOfficeDesCentres backOffice, CancellationToken annulation) =>
            await backOffice.FicheAsync(id, annulation) is { } trouvee
                ? TypedResults.Ok(trouvee)
                : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, BackOfficeDesCentres.Introuvable));
        fiche.MapPut("/prix", async (Guid id, PrixDuCentre prix, [AsParameters] Contexte c) =>
            Repondre(await c.BackOffice.ChangerLePrixAsync(await c.EditeurAsync(), id, prix, c.Annulation)));
        fiche.MapPost("/paiements", async (Guid id, SaisieDePaiement saisie, [AsParameters] Contexte c) =>
            Repondre(await c.BackOffice.SaisirUnPaiementAsync(await c.EditeurAsync(), id, saisie, c.Annulation)));
        fiche.MapPost("/paiements/{paiement:guid}/confirmation/email", async (Guid id, Guid paiement, [AsParameters] Contexte c) =>
            Repondre(await c.BackOffice.ConfirmerParEmailAsync(id, paiement, c.Annulation)));
        fiche.MapPost("/paiements/{paiement:guid}/confirmation/whatsapp", async (Guid id, Guid paiement, [AsParameters] Contexte c) =>
            Repondre(await c.BackOffice.ConfirmerParWhatsAppAsync(id, paiement, c.Annulation)));
        fiche.MapPost("/invitation/email", async (Guid id, [AsParameters] Contexte c) =>
            Repondre(await c.BackOffice.InviterParEmailAsync(id, c.Annulation)));
        fiche.MapPost("/invitation/whatsapp", async (Guid id, [AsParameters] Contexte c) =>
            Repondre(await c.BackOffice.InviterParWhatsAppAsync(id, c.Annulation)));
        fiche.MapPut("/proprietaire", async (Guid id, CorrectionDuProprietaire correction, [AsParameters] Contexte c) =>
            Repondre(await c.BackOffice.CorrigerLeProprietaireAsync(await c.EditeurAsync(), id, correction, c.Annulation)));
        fiche.MapPost("/suspension", async (Guid id, Motif motif, [AsParameters] Contexte c) =>
            Repondre(await c.BackOffice.SuspendreAsync(await c.EditeurAsync(), id, motif, c.Annulation)));
        fiche.MapPost("/reactivation", async (Guid id, Motif motif, [AsParameters] Contexte c) =>
            Repondre(await c.BackOffice.ReactiverAsync(await c.EditeurAsync(), id, motif, c.Annulation)));
    }

    private static Results<Ok<T>, JsonHttpResult<Avis>> Repondre<T>(Issue<T> issue) where T : class =>
        issue.Valeur is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Valeur);

    /// <summary>Ce dont chaque adresse du back office a besoin : le service, l'Éditeur connecté et l'annulation.</summary>
    public sealed record Contexte(BackOfficeDesCentres BackOffice, ClaimsPrincipal Utilisateur, UserManager<Compte> Comptes, CancellationToken Annulation)
    {
        public async Task<Compte> EditeurAsync() =>
            await Comptes.GetUserAsync(Utilisateur) ?? throw new InvalidOperationException("Aucun compte connecté.");
    }
}
