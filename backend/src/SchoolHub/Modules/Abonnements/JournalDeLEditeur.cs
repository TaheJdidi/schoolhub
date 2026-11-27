using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Abonnements;

/// <summary>
/// Une ligne du journal de l'Éditeur : ce qu'il a fait sur un centre (création, paiement, prix, suspension, suppression…).
/// Elle n'a pas de lien vers le centre, dont elle garde le nom : elle survit à sa suppression (EDI-08).
/// Personne ne peut la modifier ni l'effacer, même en base (<see cref="JournalDeLEditeur.SqlDeLaProtection"/>).
/// </summary>
public sealed class LigneDuJournalDeLEditeur
{
    internal LigneDuJournalDeLEditeur(
        DateTimeOffset quand,
        Guid? auteurId,
        string auteur,
        ActionDeLEditeur action,
        Guid centreId,
        string centre,
        string detail)
    {
        Quand = quand;
        AuteurId = auteurId;
        Auteur = auteur;
        Action = action;
        CentreId = centreId;
        Centre = centre;
        Detail = detail;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public DateTimeOffset Quand { get; private set; }

    public Guid? AuteurId { get; private set; }

    public string Auteur { get; private set; }

    public ActionDeLEditeur Action { get; private set; }

    /// <summary>L'identifiant du centre, sans clé étrangère.</summary>
    public Guid CentreId { get; private set; }

    /// <summary>Le nom du centre au moment de l'action.</summary>
    public string Centre { get; private set; }

    public string Detail { get; private set; }
}

public enum ActionDeLEditeur
{
    CentreCree,
    PrixChange,
    PaiementSaisi,
    ProprietaireCorrige,
    CentreSuspendu,
    CentreReactive,
    CentreSupprime,
}

/// <summary>Écrit au journal de l'Éditeur, avant l'enregistrement : la ligne part avec l'action.</summary>
public sealed class JournalDeLEditeur(SchoolHubDbContext contexte, TimeProvider temps)
{
    /// <summary>
    /// Le journal de l'Éditeur ne se modifie ni ne s'efface, même en cas d'erreur dans notre code.
    /// La migration BackOfficeDeLEditeur l'exécute, et la base des tests aussi.
    /// </summary>
    public const string SqlDeLaProtection =
        """
        CREATE FUNCTION journal_editeur_refuser_les_changements() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Le journal de l''Éditeur ne se modifie pas et ne s''efface pas';
        END
        $$;
        CREATE TRIGGER journal_editeur_ni_modification_ni_suppression BEFORE UPDATE OR DELETE ON journal_editeur
            FOR EACH ROW EXECUTE FUNCTION journal_editeur_refuser_les_changements();
        CREATE TRIGGER journal_editeur_pas_de_vidage BEFORE TRUNCATE ON journal_editeur
            FOR EACH STATEMENT EXECUTE FUNCTION journal_editeur_refuser_les_changements();
        """;

    public const string SqlDuRetour =
        """
        DROP TRIGGER journal_editeur_ni_modification_ni_suppression ON journal_editeur;
        DROP TRIGGER journal_editeur_pas_de_vidage ON journal_editeur;
        DROP FUNCTION journal_editeur_refuser_les_changements();
        """;

    public void Noter(Compte auteur, ActionDeLEditeur action, Guid centreId, string centre, string detail) =>
        contexte.Set<LigneDuJournalDeLEditeur>().Add(new LigneDuJournalDeLEditeur(
            temps.GetUtcNow(),
            auteur.Id,
            $"{auteur.Prenom} {auteur.Nom}",
            action,
            centreId,
            centre,
            detail));
}

internal sealed class LigneDuJournalDeLEditeurConfiguration : IEntityTypeConfiguration<LigneDuJournalDeLEditeur>
{
    public void Configure(EntityTypeBuilder<LigneDuJournalDeLEditeur> ligne)
    {
        ligne.ToTable("journal_editeur");
        ligne.Property(l => l.Auteur).HasMaxLength(201);
        ligne.Property(l => l.Action).HasConversion<string>().HasMaxLength(30);
        ligne.Property(l => l.Centre).HasMaxLength(200);
        ligne.Property(l => l.Detail).HasMaxLength(500);
        ligne.HasOne<Compte>().WithMany().HasForeignKey(l => l.AuteurId).OnDelete(DeleteBehavior.Restrict);
    }
}
