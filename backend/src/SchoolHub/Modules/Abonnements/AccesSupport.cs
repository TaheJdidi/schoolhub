using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;

namespace SchoolHub.Modules.Abonnements;

/// <summary>
/// EDI-07 : l'Éditeur voit un centre en lecture seule, seulement pendant l'accès support que son propriétaire a donné.
/// Il voit alors l'accueil du centre (son identité et ses chiffres : élèves, groupes, professeurs) et ses comptes.
/// Chaque page consultée va au journal du centre. Sans autorisation, la réponse est 403.
/// La requête travaille alors pour ce centre (son centre courant) : les données se lisent filtrées, comme le centre
/// les lit lui-même. Rien n'est jamais écrit, sauf la ligne du journal.
/// </summary>
public sealed class AccesSupport(SchoolHubDbContext contexte, CentreCourant centreCourant, JournalDuCentre journal, TimeProvider temps)
{
    public const string SansAutorisation =
        "Le propriétaire n'a pas autorisé l'accès support : demandez-lui de cliquer « Autoriser le support 24 h » dans sa page Abonnement";

    /// <summary>Les pages du centre que l'Éditeur peut voir, et leur nom au journal.</summary>
    public static readonly IReadOnlyDictionary<string, string> Pages = new Dictionary<string, string>
    {
        ["accueil"] = "Accueil",
        ["comptes"] = "Comptes",
    };

    /// <summary>
    /// Ouvre la page pour l'Éditeur : <c>null</c> si le centre n'existe pas, <c>false</c> si l'accès support n'est pas ouvert,
    /// <c>true</c> sinon, une fois la consultation notée au journal du centre.
    /// </summary>
    public async Task<bool?> OuvrirAsync(Compte editeur, Guid centreId, string page, CancellationToken annulation = default)
    {
        if (!await contexte.Centres.AnyAsync(c => c.Id == centreId, annulation))
            return null;

        var abonnement = await contexte.Set<Abonnement>().AsNoTracking().SingleOrDefaultAsync(a => a.CentreId == centreId, annulation);
        if (abonnement is null || !abonnement.SupportOuvert(temps.GetUtcNow()))
            return false;

        centreCourant.Definir(centreId);
        journal.Noter(
            editeur,
            ActionDuJournal.AccesSupportConsultation,
            $"Accès support · {editeur.Prenom} {editeur.Nom} (Éditeur) · page {Pages[page]}");
        await contexte.SaveChangesAsync(annulation);
        return true;
    }

    /// <summary>L'accueil du centre : son identité, son état, et ses chiffres. Le centre courant est celui de <see cref="OuvrirAsync"/>.</summary>
    public async Task<CentreEnAccesSupport> AccueilAsync(Guid centreId, CancellationToken annulation = default)
    {
        var centre = await contexte.Centres.AsNoTracking().SingleAsync(c => c.Id == centreId, annulation);
        var abonnement = await contexte.Set<Abonnement>().AsNoTracking().SingleAsync(a => a.CentreId == centreId, annulation);
        var situation = SituationDeLAbonnement.Calculer(abonnement, DateDeTunis.Aujourdhui(temps));
        return new CentreEnAccesSupport(
            centre.Nom,
            centre.Code,
            centre.Telephone,
            centre.Email,
            situation.Etat.ToString(),
            abonnement.SupportAutoriseJusquAu!.Value,
            await contexte.Eleves.CountAsync(annulation),
            await contexte.Groupes.CountAsync(g => g.FermeLe == null, annulation),
            await contexte.Professeurs.CountAsync(p => !p.Parti, annulation));
    }

    public Task<List<CompteEnAccesSupport>> ComptesAsync(Guid centreId, CancellationToken annulation = default) =>
        contexte.Users.AsNoTracking()
            .Where(c => c.CentreId == centreId)
            .OrderBy(c => c.Etat == EtatDuCompte.Desactive)
            .ThenBy(c => c.Prenom)
            .ThenBy(c => c.Nom)
            .Select(c => new CompteEnAccesSupport(c.Prenom, c.Nom, c.Email!, c.Role.ToString(), c.Etat.ToString()))
            .ToListAsync(annulation);
}
