using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SchoolHub.Socle.Centres;

/// <summary>
/// Classe scolaire que vise un groupe, par exemple « 4ᵉ année secondaire, section mathématiques » (CEN-03).
/// Chaque centre a sa liste : les 29 niveaux d'office, qui ne se modifient pas, et ceux qu'il ajoute.
/// </summary>
public sealed class Niveau : IAppartientAuCentre
{
    /// <summary>La taille de la colonne du nom.</summary>
    public const int LongueurMaximaleDuNom = 100;

    internal Niveau(string nom, bool dOffice, int? rang)
    {
        Nom = nom;
        NomNormalise = Normaliser(nom);
        DOffice = dOffice;
        Rang = rang;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid CentreId { get; private set; }

    public string Nom { get; private set; }

    /// <summary>Le nom en minuscules : deux niveaux d'un centre ne peuvent pas avoir le même.</summary>
    public string NomNormalise { get; private set; }

    /// <summary>Un niveau de la liste scolaire tunisienne : il ne se renomme ni ne se retire.</summary>
    public bool DOffice { get; private set; }

    /// <summary>L'ordre scolaire d'un niveau d'office, de 1 à 29 ; vide pour un niveau ajouté.</summary>
    public int? Rang { get; private set; }

    /// <summary>Le nouveau nom d'un niveau ajouté, déjà vérifié.</summary>
    public void Renommer(string nom)
    {
        Nom = nom;
        NomNormalise = Normaliser(nom);
    }

    /// <summary>Le nom tel qu'on le compare : « Université, 1ʳᵉ année » et « université, 1ʳᵉ ANNÉE » sont le même niveau.</summary>
    public static string Normaliser(string nom) => nom.ToLowerInvariant();
}

/// <summary>
/// Les 29 niveaux scolaires tunisiens, dans l'ordre, validés par Lassad et Taha. Ils sont recopiés dans chaque centre :
/// par la commande de création d'un centre, et par la migration AjouterLesNiveaux pour les centres qui existaient avant.
/// </summary>
public static class NiveauxDOffice
{
    private static readonly string[] SectionsDe2eAnnee =
        ["sciences", "lettres", "économie et services", "technologies de l'informatique", "sport"];

    private static readonly string[] SectionsDu3eEtDu4eAnnee =
    [
        "mathématiques", "sciences expérimentales", "sciences techniques", "sciences de l'informatique",
        "économie et gestion", "lettres", "sport",
    ];

    public static IReadOnlyList<string> Liste { get; } =
    [
        // Les 9 années de l'enseignement de base : l'école, puis le collège.
        .. Enumerable.Range(1, 9).Select(annee => $"{(annee == 1 ? "1ʳᵉ" : $"{annee}ᵉ")} année de base"),
        "1ʳᵉ année secondaire",
        .. SectionsDe2eAnnee.Select(section => $"2ᵉ année secondaire, section {section}"),
        .. SectionsDu3eEtDu4eAnnee.Select(section => $"3ᵉ année secondaire, section {section}"),
        .. SectionsDu3eEtDu4eAnnee.Select(section => $"4ᵉ année secondaire, section {section}"),
    ];

    /// <summary>Le SQL qui donne les 29 niveaux d'office, dans l'ordre, à chaque centre de la base.</summary>
    public static string SqlDeRecopie()
    {
        var valeurs = string.Join(
            ",\n",
            Liste.Select((nom, i) => $"('{Echapper(nom)}', '{Echapper(Niveau.Normaliser(nom))}', {i + 1})"));
        return $"""
            INSERT INTO niveaux (id, centre_id, nom, nom_normalise, d_office, rang)
            SELECT gen_random_uuid(), c.id, v.nom, v.nom_normalise, true, v.rang
            FROM centres c
            CROSS JOIN (VALUES
            {valeurs}
            ) AS v(nom, nom_normalise, rang);
            """;
    }

    private static string Echapper(string texte) => texte.Replace("'", "''");
}

internal sealed class NiveauConfiguration : IEntityTypeConfiguration<Niveau>
{
    public void Configure(EntityTypeBuilder<Niveau> niveau)
    {
        // La clé étrangère vers le centre et le filtre par centre viennent de SchoolHubDbContext.
        niveau.ToTable("niveaux");
        niveau.Property(n => n.Nom).HasMaxLength(Niveau.LongueurMaximaleDuNom);
        niveau.Property(n => n.NomNormalise).HasMaxLength(Niveau.LongueurMaximaleDuNom);

        // Deux centres peuvent avoir le même niveau ajouté, mais pas un centre deux fois.
        niveau.HasIndex(n => new { n.CentreId, n.NomNormalise }).IsUnique();
    }
}
