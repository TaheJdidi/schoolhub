using ClosedXML.Excel;

namespace SchoolHub.Modules.Listes;

/// <summary>Le type d'une colonne : les montants sont des nombres et les dates des dates (LST-02).</summary>
public enum TypeDeColonne
{
    Texte,
    Montant,
    Date,
    Nombre,
}

public sealed record Colonne(string Titre, TypeDeColonne Type = TypeDeColonne.Texte);

/// <summary>
/// LST-02 : un fichier Excel d'une seule feuille : une ligne de titres en gras, figée, puis une ligne par élément ; les
/// montants au format « 0,000 », les dates au format « jj/mm/aaaa ». Les colonnes s'ajustent à leur contenu.
/// </summary>
public static class Classeur
{
    public const string TypeMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Creer(string feuille, IReadOnlyList<Colonne> colonnes, IEnumerable<object?[]> lignes)
    {
        using var classeur = new XLWorkbook();
        var page = classeur.Worksheets.Add(feuille.Length > 31 ? feuille[..31] : feuille);
        for (var c = 0; c < colonnes.Count; c++)
            page.Cell(1, c + 1).Value = colonnes[c].Titre;
        page.Row(1).Style.Font.Bold = true;
        page.SheetView.FreezeRows(1);

        var numero = 2;
        foreach (var ligne in lignes)
        {
            for (var c = 0; c < colonnes.Count; c++)
            {
                var cellule = page.Cell(numero, c + 1);
                switch (ligne[c])
                {
                    case null:
                        break;
                    case DateOnly date:
                        cellule.Value = date.ToDateTime(TimeOnly.MinValue);
                        cellule.Style.DateFormat.Format = "dd/mm/yyyy";
                        break;
                    case decimal montant:
                        cellule.Value = montant;
                        cellule.Style.NumberFormat.Format = colonnes[c].Type == TypeDeColonne.Montant ? "0.000" : "0";
                        break;
                    case int entier:
                        cellule.Value = entier;
                        break;
                    case bool oui:
                        cellule.Value = oui ? "oui" : "non";
                        break;
                    default:
                        cellule.Value = ligne[c]!.ToString();
                        break;
                }
            }
            numero++;
        }

        page.Columns().AdjustToContents(1, Math.Min(numero, 200), 8, 60);
        using var flux = new MemoryStream();
        classeur.SaveAs(flux);
        return flux.ToArray();
    }
}
