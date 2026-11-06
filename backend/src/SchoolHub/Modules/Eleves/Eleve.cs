using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Eleves;

/// <summary>
/// Un élève (ELV-02) : une seule fiche, d'une année sur l'autre, avec les contacts utiles. Jamais de carte d'identité,
/// d'adresse, de date de naissance ni de photo. Ses inscriptions viendront le relier aux groupes (ELV-03).
/// </summary>
public sealed class Eleve : IAppartientAuCentre
{
    public const int LongueurMaximaleDuNom = 100;
    public const int LongueurMaximaleDeLEtablissement = 200;

    internal Eleve(Coordonnees coordonnees) => Modifier(coordonnees);

    // Pour EF Core.
    private Eleve()
    {
        Prenom = null!;
        Nom = null!;
        NomDeRecherche = null!;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public string Prenom { get; private set; }

    public string Nom { get; private set; }

    public Guid NiveauId { get; private set; }

    /// <summary>Un élève majeur n'a pas besoin du téléphone de son parent ; les messages ne vont qu'à lui.</summary>
    public bool Majeur { get; private set; }

    /// <summary>Les 8 chiffres du téléphone WhatsApp du parent.</summary>
    public string? TelephoneParent { get; private set; }

    public string? EmailParent { get; private set; }

    /// <summary>Les 8 chiffres du téléphone de l'élève.</summary>
    public string? TelephoneEleve { get; private set; }

    public string? EmailEleve { get; private set; }

    public string? Etablissement { get; private set; }

    /// <summary>« hedi ben salah » : le prénom et le nom sans accents, en minuscules, pour la recherche (ELV-01).</summary>
    public string NomDeRecherche { get; private set; }

    /// <summary>CAI-08 : le moment où le dernier rappel de paiement a été ouvert, pour ne pas l'envoyer deux fois.</summary>
    public DateTimeOffset? DernierRappelLe { get; private set; }

    internal void NoterUnRappel(DateTimeOffset maintenant) => DernierRappelLe = maintenant;

    /// <summary>La nouvelle fiche, déjà vérifiée.</summary>
    [MemberNotNull(nameof(Prenom), nameof(Nom), nameof(NomDeRecherche))]
    public void Modifier(Coordonnees coordonnees)
    {
        Prenom = coordonnees.Prenom;
        Nom = coordonnees.Nom;
        NiveauId = coordonnees.NiveauId;
        Majeur = coordonnees.Majeur;
        TelephoneParent = coordonnees.TelephoneParent;
        EmailParent = coordonnees.EmailParent;
        TelephoneEleve = coordonnees.TelephoneEleve;
        EmailEleve = coordonnees.EmailEleve;
        Etablissement = coordonnees.Etablissement;
        NomDeRecherche = Normaliser($"{coordonnees.Prenom} {coordonnees.Nom}");
    }

    /// <summary>
    /// Un texte comme on le cherche : sans accents, en minuscules, les espaces réduits à un seul. « Hédi  BEN Salah »
    /// donne « hedi ben salah ».
    /// </summary>
    public static string Normaliser(string texte)
    {
        var sansAccents = new string(texte.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return string.Join(' ', sansAccents.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

/// <summary>La fiche d'un élève, déjà vérifiée : les téléphones en 8 chiffres, les champs vides à <c>null</c>.</summary>
public sealed record Coordonnees(
    string Prenom,
    string Nom,
    Guid NiveauId,
    bool Majeur,
    string? TelephoneParent,
    string? EmailParent,
    string? TelephoneEleve,
    string? EmailEleve,
    string? Etablissement);

internal sealed class EleveConfiguration : IEntityTypeConfiguration<Eleve>
{
    public void Configure(EntityTypeBuilder<Eleve> eleve)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        eleve.ToTable("eleves");
        eleve.Property(e => e.Prenom).HasMaxLength(Eleve.LongueurMaximaleDuNom);
        eleve.Property(e => e.Nom).HasMaxLength(Eleve.LongueurMaximaleDuNom);
        eleve.Property(e => e.NomDeRecherche).HasMaxLength(2 * Eleve.LongueurMaximaleDuNom + 1);
        eleve.Property(e => e.TelephoneParent).HasMaxLength(8);
        eleve.Property(e => e.TelephoneEleve).HasMaxLength(8);
        eleve.Property(e => e.EmailParent).HasMaxLength(AdresseEmail.LongueurMaximale);
        eleve.Property(e => e.EmailEleve).HasMaxLength(AdresseEmail.LongueurMaximale);
        eleve.Property(e => e.Etablissement).HasMaxLength(Eleve.LongueurMaximaleDeLEtablissement);
        eleve.HasOne<Niveau>().WithMany().HasForeignKey(e => e.NiveauId).OnDelete(DeleteBehavior.Restrict);
    }
}
