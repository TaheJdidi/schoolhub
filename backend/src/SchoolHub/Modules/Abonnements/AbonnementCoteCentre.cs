using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;

namespace SchoolHub.Modules.Abonnements;

/// <summary>
/// L'abonnement vu depuis le centre courant : ses bandeaux (EDI-05, EDI-06), l'accès support que le propriétaire
/// donne ou retire (EDI-07), et l'export de ses données après la suspension (EDI-08).
/// </summary>
public sealed class AbonnementCoteCentre(
    SchoolHubDbContext contexte,
    CentreCourant centreCourant,
    JournalDuCentre journal,
    ExportDuCentre export,
    TimeProvider temps,
    IOptions<OptionsDesAbonnements> options)
{
    /// <summary>La durée d'un accès support.</summary>
    public static readonly TimeSpan DureeDuSupport = TimeSpan.FromHours(24);

    public const string ExportImpossible =
        "L'export de vos données est possible pendant les 12 mois qui suivent la suspension de votre abonnement";

    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    /// <summary>La situation de l'abonnement d'un centre aujourd'hui (aussi lue par la lecture seule, à chaque écriture).</summary>
    public static async Task<SituationDeLAbonnement> SituationAsync(
        SchoolHubDbContext contexte,
        Guid centreId,
        TimeProvider temps,
        CancellationToken annulation = default)
    {
        var abonnement = await contexte.Set<Abonnement>().AsNoTracking().SingleOrDefaultAsync(a => a.CentreId == centreId, annulation);
        return SituationDeLAbonnement.Calculer(abonnement, DateDeTunis.Aujourdhui(temps));
    }

    public async Task<AbonnementDuCentre> LireAsync(CancellationToken annulation = default)
    {
        var abonnement = await contexte.Set<Abonnement>().AsNoTracking().SingleOrDefaultAsync(a => a.CentreId == CentreId, annulation);
        var situation = SituationDeLAbonnement.Calculer(abonnement, DateDeTunis.Aujourdhui(temps));
        return new AbonnementDuCentre(
            situation.Etat.ToString(),
            situation.PayeJusquAu,
            situation.FinDeLaPeriodeDeGrace,
            situation.FinProche,
            situation.LectureSeule,
            abonnement is not null && abonnement.SupportOuvert(temps.GetUtcNow()) ? abonnement.SupportAutoriseJusquAu : null,
            situation.ExportPossible,
            situation.ExportPossible ? situation.SuppressionPossibleLe!.Value.AddDays(-1) : null);
    }

    /// <summary>« Autoriser le support 24 h » : l'Éditeur voit le centre en lecture seule jusqu'à demain à la même heure.</summary>
    public async Task<AbonnementDuCentre> AutoriserLeSupportAsync(Compte proprietaire, CancellationToken annulation = default)
    {
        var abonnement = await AbonnementAsync(annulation);
        var jusquAu = temps.GetUtcNow() + DureeDuSupport;
        abonnement.AutoriserLeSupport(jusquAu);
        journal.Noter(
            proprietaire,
            ActionDuJournal.AccesSupportAutorise,
            $"Accès support autorisé jusqu'au {MessagesAuProprietaire.Date(DateDeTunis.Jour(jusquAu))} à {DateDeTunis.Heure(jusquAu):HH'h'mm}");
        await contexte.SaveChangesAsync(annulation);
        return await LireAsync(annulation);
    }

    /// <summary>Le propriétaire retire l'accès support avant la fin des 24 h : l'Éditeur ne voit plus rien du centre.</summary>
    public async Task<AbonnementDuCentre> RetirerLeSupportAsync(Compte proprietaire, CancellationToken annulation = default)
    {
        var abonnement = await AbonnementAsync(annulation);
        if (abonnement.SupportOuvert(temps.GetUtcNow()))
            journal.Noter(proprietaire, ActionDuJournal.AccesSupportRetire, "Accès support retiré avant la fin des 24 h");
        abonnement.RetirerLeSupport();
        await contexte.SaveChangesAsync(annulation);
        return await LireAsync(annulation);
    }

    /// <summary>Le fichier Excel des données du centre, pendant les 12 mois qui suivent sa suspension ; <c>null</c> sinon.</summary>
    public async Task<(byte[] Contenu, string NomDuFichier)?> ExporterAsync(Compte proprietaire, CancellationToken annulation = default)
    {
        var situation = await SituationAsync(contexte, CentreId, temps, annulation);
        if (!situation.ExportPossible)
            return null;

        var fichier = await export.ProduireAsync(CentreId, annulation);
        journal.Noter(proprietaire, ActionDuJournal.ExportDesDonnees, $"Export Excel des données du centre · {string.Join(", ", ExportDuCentre.Feuilles)}");
        await contexte.SaveChangesAsync(annulation);
        return fichier;
    }

    private async Task<Abonnement> AbonnementAsync(CancellationToken annulation)
    {
        var abonnement = await contexte.Set<Abonnement>().SingleOrDefaultAsync(a => a.CentreId == CentreId, annulation);
        if (abonnement is not null)
            return abonnement;

        // Un centre ouvert avant le back office (par la commande creer-centre) : son abonnement naît ici, En attente.
        abonnement = new Abonnement(CentreId, options.Value.PrixMensuel);
        contexte.Set<Abonnement>().Add(abonnement);
        return abonnement;
    }
}
