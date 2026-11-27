using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;

namespace SchoolHub.Modules.Abonnements;

/// <summary>
/// Efface un centre et tout ce qui lui appartient, dans une seule transaction (EDI-08). Les données des modules sont
/// trouvées dans le modèle : toute donnée qui implémente <see cref="IAppartientAuCentre"/> est effacée, sans qu'un module
/// ait à s'inscrire. Elles partent dans l'ordre de leurs clés étrangères : une donnée part avant celles qu'elle désigne
/// (le paiement avant l'inscription, l'inscription avant l'élève). Le journal du centre ne s'efface qu'ici : sa protection
/// laisse passer les lignes du centre nommé dans <c>schoolhub.centre_supprime</c>, pour cette transaction seulement.
/// </summary>
public static class SuppressionDUnCentre
{
    /// <summary>
    /// La protection du journal du centre, élargie : une modification ou un vidage restent toujours refusés, et une ligne
    /// ne s'efface qu'avec son centre. La migration SuiteDuBackOffice l'exécute, et la base des tests aussi.
    /// </summary>
    public const string SqlDeLaProtection =
        """
        CREATE OR REPLACE FUNCTION journal_refuser_les_changements() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF TG_OP = 'DELETE' THEN
                IF OLD.centre_id::text = current_setting('schoolhub.centre_supprime', true) THEN
                    RETURN OLD;
                END IF;
            END IF;
            RAISE EXCEPTION 'Le journal ne se modifie pas et ne s''efface pas';
        END
        $$;
        """;

    /// <summary>Le retour arrière de la migration : le journal redevient intouchable.</summary>
    public const string SqlDuRetour =
        """
        CREATE OR REPLACE FUNCTION journal_refuser_les_changements() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Le journal ne se modifie pas et ne s''efface pas';
        END
        $$;
        """;

    public static async Task SupprimerAsync(
        SchoolHubDbContext contexte,
        Guid centreId,
        CancellationToken annulation,
        Func<Task>? avantDeValider = null)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync(annulation);
        await contexte.Database.ExecuteSqlAsync($"SELECT set_config('schoolhub.centre_supprime', {centreId.ToString()}, true)", annulation);

        foreach (var type in OrdreDeSuppression(contexte.Model))
        {
            var colonne = type.FindProperty(nameof(IAppartientAuCentre.CentreId))!.GetColumnName();
            // Le nom de la table et de la colonne viennent du modèle, jamais d'une saisie ; le centre est un paramètre.
#pragma warning disable EF1002
            await contexte.Database.ExecuteSqlRawAsync(
                $"DELETE FROM \"{type.GetTableName()}\" WHERE \"{colonne}\" = {{0}}",
                [centreId],
                annulation);
#pragma warning restore EF1002
        }

        await contexte.Set<EnvoiAuProprietaire>().Where(e => e.CentreId == centreId).ExecuteDeleteAsync(annulation);
        await contexte.Set<PaiementDAbonnement>().Where(p => p.CentreId == centreId).ExecuteDeleteAsync(annulation);
        await contexte.Set<Abonnement>().Where(a => a.CentreId == centreId).ExecuteDeleteAsync(annulation);

        // Les jetons des comptes partent avec eux (clé étrangère en cascade).
        await contexte.Set<Compte>().Where(c => c.CentreId == centreId).ExecuteDeleteAsync(annulation);
        await contexte.Centres.Where(c => c.Id == centreId).ExecuteDeleteAsync(annulation);

        if (avantDeValider is not null)
            await avantDeValider();
        await transaction.CommitAsync(annulation);
    }

    /// <summary>
    /// Les données d'un centre (chacune sa table), dans un ordre où aucune n'est effacée tant qu'une autre la désigne encore.
    /// Une donnée qui se désigne elle-même (une séance qui en remplace une autre) part en une fois, avec les siennes.
    /// </summary>
    public static List<IEntityType> OrdreDeSuppression(IModel modele)
    {
        var restantes = modele.GetEntityTypes()
            .Where(t => typeof(IAppartientAuCentre).IsAssignableFrom(t.ClrType) && !t.IsOwned() && t.GetTableName() is not null)
            .ToList();
        var ordre = new List<IEntityType>();
        while (restantes.Count > 0)
        {
            // Une table que plus aucune autre table restante ne désigne peut partir.
            var libre = restantes.FirstOrDefault(t => !restantes.Any(autre =>
                autre != t && autre.GetForeignKeys().Any(cle => cle.PrincipalEntityType.GetTableName() == t.GetTableName())));
            if (libre is null)
                throw new InvalidOperationException(
                    "Les données des centres se désignent en boucle : " + string.Join(", ", restantes.Select(t => t.GetTableName())));
            ordre.Add(libre);
            restantes.Remove(libre);
        }

        return ordre;
    }
}
