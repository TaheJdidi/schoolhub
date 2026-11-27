using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Achats;
using SchoolHub.Modules.Listes;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Abonnements;

/// <summary>
/// EDI-08 : le fichier Excel des données du centre courant, que son propriétaire télécharge pendant les 12 mois qui
/// suivent la suspension. Une feuille par liste, reprise des exports des listes (LST-02) : les élèves, les professeurs
/// (au centre et partis), tous les reçus, les employés, puis les achats et les comptes.
/// </summary>
public sealed class ExportDuCentre(SchoolHubDbContext contexte, ExportsDesListes listes, TimeProvider temps)
{
    /// <summary>Les feuilles du fichier, dans l'ordre : le journal du centre les nomme.</summary>
    public static readonly string[] Feuilles = ["Élèves", "Professeurs", "Professeurs partis", "Reçus", "Employés", "Achats", "Comptes"];

    public async Task<(byte[] Contenu, string NomDuFichier)> ProduireAsync(Guid centreId, CancellationToken annulation = default)
    {
        var centre = await contexte.Centres.AsNoTracking().SingleAsync(c => c.Id == centreId, annulation);

        using var classeur = new XLWorkbook();
        Recopier(classeur, (await listes.ElevesAsync(null, null, null)).Contenu, "Élèves");
        Recopier(classeur, (await listes.ProfesseursAsync(partis: false)).Contenu, "Professeurs");
        Recopier(classeur, (await listes.ProfesseursAsync(partis: true)).Contenu, "Professeurs partis");
        Recopier(classeur, (await listes.RecusAsync(null, null, null)).Contenu, "Reçus");
        Recopier(classeur, (await listes.EmployesAsync()).Contenu, "Employés");

        // Tous les achats, alors que leur liste ne montre qu'un mois ou une année.
        var achats = await contexte.Achats.AsNoTracking().OrderBy(a => a.Jour).ToListAsync(annulation);
        Recopier(classeur, Classeur.Creer("Achats",
            [new("Date", TypeDeColonne.Date), new("Libellé"), new("Catégorie"), new("Montant", TypeDeColonne.Montant), new("Fournisseur")],
            achats.Select(a => new object?[] { a.Jour, a.Libelle, a.Categorie == CategorieDAchat.Materiel ? "Matériel" : "Service", a.Montant, a.Fournisseur })), "Achats");

        var comptes = await contexte.Users.AsNoTracking()
            .Where(c => c.CentreId == centreId)
            .OrderBy(c => c.Prenom).ThenBy(c => c.Nom)
            .ToListAsync(annulation);
        Recopier(classeur, Classeur.Creer("Comptes",
            [new("Prénom"), new("Nom"), new("Email"), new("Rôle"), new("État")],
            comptes.Select(c => new object?[] { c.Prenom, c.Nom, c.Email, LibellesDesComptes.De(c.Role), LibellesDesComptes.De(c.Etat) })), "Comptes");

        using var flux = new MemoryStream();
        classeur.SaveAs(flux);
        var nom = new string(centre.Nom.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
        return (flux.ToArray(), $"schoolup-{nom}-{DateDeTunis.Aujourdhui(temps):yyyy-MM-dd}.xlsx");
    }

    /// <summary>Recopie la feuille d'un fichier de liste dans le classeur, sous ce nom.</summary>
    private static void Recopier(XLWorkbook classeur, byte[] fichier, string nom)
    {
        using var flux = new MemoryStream(fichier);
        using var source = new XLWorkbook(flux);
        source.Worksheet(1).CopyTo(classeur, nom);
    }
}
