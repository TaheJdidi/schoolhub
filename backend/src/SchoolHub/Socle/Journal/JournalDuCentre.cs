using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Socle.Journal;

/// <summary>
/// Écrit au journal du centre courant. Chaque action appelle <see cref="Noter"/> elle-même, avant d'enregistrer :
/// la ligne part avec l'action, dans la même transaction. Si l'action échoue, la ligne n'existe pas.
/// Il ne s'appelle pas « Journal », qui est le nom de son espace de noms : C# confondrait les deux.
/// </summary>
public sealed class JournalDuCentre(SchoolHubDbContext contexte, TimeProvider temps)
{
    /// <summary>L'auteur d'une action faite sans compte connecté : la commande de création d'un centre (CEN-01).</summary>
    public const string AuteurSansCompte = "SchoolHub";

    /// <summary>
    /// La protection du journal dans PostgreSQL : toute modification, suppression ou vidage de la table est refusé,
    /// même en cas d'erreur dans notre code. La migration AjouterLeJournal l'exécute, et la base des tests aussi,
    /// car EnsureCreated ignore les migrations.
    /// </summary>
    public const string SqlDeLaProtection =
        """
        CREATE FUNCTION journal_refuser_les_changements() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Le journal ne se modifie pas et ne s''efface pas';
        END
        $$;
        CREATE TRIGGER journal_ni_modification_ni_suppression BEFORE UPDATE OR DELETE ON journal
            FOR EACH ROW EXECUTE FUNCTION journal_refuser_les_changements();
        CREATE TRIGGER journal_pas_de_vidage BEFORE TRUNCATE ON journal
            FOR EACH STATEMENT EXECUTE FUNCTION journal_refuser_les_changements();
        """;

    /// <summary>
    /// Ajoute une ligne au journal, sans l'enregistrer : elle part avec le prochain enregistrement de l'action.
    /// L'auteur est le compte connecté, ou <c>null</c> pour SchoolHub.
    /// </summary>
    public void Noter(Compte? auteur, ActionDuJournal action, string detail, string? avant = null, string? apres = null) =>
        contexte.LignesDuJournal.Add(new LigneDuJournal(
            temps.GetUtcNow(),
            auteur?.Id,
            auteur is null ? AuteurSansCompte : $"{auteur.Prenom} {auteur.Nom}",
            action,
            detail,
            avant,
            apres));
}
