using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Professeurs;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Listes;

/// <summary>
/// LST-03 et LST-04 : importer les élèves et les professeurs d'un nouveau centre depuis le fichier modèle. Chaque ligne
/// est vérifiée avant d'importer quoi que ce soit ; tant qu'il reste une erreur, rien n'est importé. Les doublons
/// possibles sont signalés sans bloquer. Tout ou rien.
/// </summary>
public sealed class ImportsDesListes(
    SchoolHubDbContext contexte,
    ElevesDuCentre eleves,
    ProfesseursDuCentre professeurs,
    InscriptionsDuCentre inscriptions,
    TimeProvider temps)
{
    public const string FichierPasValable = "Envoyez le fichier modèle rempli (.xlsx)";
    private const string FichierVide = "Le fichier n'a aucune ligne à importer";

    /// <summary>Le nombre de lignes d'un import : un centre, jamais une région entière.</summary>
    private const int LignesAuPlus = 2000;

    public static readonly string[] ColonnesDesEleves =
    [
        "Prénom", "Nom", "Niveau", "Téléphone WhatsApp du parent", "Élève majeur (oui ou non)", "Téléphone de l'élève", "Email de l'élève",
        "Email du parent", "Établissement", "Groupe (facultatif)",
    ];

    public static readonly string[] ColonnesDesProfesseurs = ["Prénom", "Nom", "Téléphone", "Email (facultatif)"];

    /// <summary>LST-03 : le modèle des élèves, avec une ligne d'exemple et la liste des niveaux du centre dans une 2ᵉ feuille.</summary>
    public async Task<byte[]> ModeleDesElevesAsync()
    {
        // Les niveaux d'office dans l'ordre scolaire, puis ceux ajoutés par le centre.
        var niveaux = await contexte.Niveaux.OrderBy(n => n.Rang == null).ThenBy(n => n.Rang).ThenBy(n => n.Nom).Select(n => n.Nom).ToListAsync();
        using var classeur = new XLWorkbook();
        var feuille = Entetes(classeur.Worksheets.Add("Élèves"), ColonnesDesEleves);
        string[] exemple = ["Adem", "Miled", niveaux.FirstOrDefault(n => n.StartsWith("9", StringComparison.Ordinal)) ?? niveaux.FirstOrDefault() ?? "",
            "98 401 220", "non", "22 345 678", "adem@exemple.tn", "", "Collège Ibn Khaldoun", "Mathématiques · Groupe Samedi 10h → 12h"];
        for (var c = 0; c < exemple.Length; c++)
            feuille.Cell(2, c + 1).Value = exemple[c];
        var liste = classeur.Worksheets.Add("Niveaux");
        liste.Cell(1, 1).Value = "Niveaux du centre";
        liste.Cell(1, 1).Style.Font.Bold = true;
        for (var i = 0; i < niveaux.Count; i++)
            liste.Cell(i + 2, 1).Value = niveaux[i];
        liste.Columns().AdjustToContents();
        return Enregistrer(classeur);
    }

    /// <summary>LST-04 : le modèle des professeurs.</summary>
    public static byte[] ModeleDesProfesseurs()
    {
        using var classeur = new XLWorkbook();
        var feuille = Entetes(classeur.Worksheets.Add("Professeurs"), ColonnesDesProfesseurs);
        string[] exemple = ["Karim", "Ben Salah", "98 401 220", ""];
        for (var c = 0; c < exemple.Length; c++)
            feuille.Cell(2, c + 1).Value = exemple[c];
        return Enregistrer(classeur);
    }

    /// <summary>
    /// LST-03 : vérifie toutes les lignes, puis importe les élèves et inscrit ceux qui ont un groupe à partir du Mois en
    /// cours du groupe (GRP-07). Une seule erreur, et rien n'est importé.
    /// </summary>
    public async Task<ResultatDImport> ImporterDesElevesAsync(Stream fichier)
    {
        if (LireLesLignes(fichier, ColonnesDesEleves.Length) is not { } lignes)
            return ResultatDImport.Refuse([FichierPasValable]);
        if (lignes.Count == 0)
            return ResultatDImport.Refuse([FichierVide]);
        if (lignes.Count > LignesAuPlus)
            return ResultatDImport.Refuse([$"Le fichier a plus de {LignesAuPlus} lignes : découpez-le"]);

        var niveaux = await contexte.Niveaux.ToListAsync();
        var groupes = await contexte.Groupes.Where(g => g.FermeLe == null).ToListAsync();
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);
        var existants = await contexte.Eleves.Select(e => new { e.Prenom, e.Nom, e.NomDeRecherche, e.TelephoneParent, e.TelephoneEleve }).ToListAsync();

        var erreurs = new List<string>();
        var doublons = new List<string>();
        var aImporter = new List<(int Ligne, FicheSaisieDeLEleve Fiche, Groupe? Groupe)>();
        var dejaVus = new Dictionary<string, int>();
        foreach (var (numero, cellules) in lignes)
        {
            var niveau = niveaux.FirstOrDefault(n => n.NomNormalise == Niveau.Normaliser(cellules[2].Trim()));
            if (niveau is null)
            {
                erreurs.Add(string.IsNullOrWhiteSpace(cellules[2]) ? $"ligne {numero} : niveau manquant" : $"ligne {numero} : niveau inconnu « {cellules[2].Trim()} »");
                continue;
            }

            var majeur = Oui(cellules[4]);
            if (majeur is null)
            {
                erreurs.Add($"ligne {numero} : « élève majeur » vaut oui ou non");
                continue;
            }

            var fiche = new FicheSaisieDeLEleve(cellules[0], cellules[1], niveau.Id, majeur.Value, Vide(cellules[3]), Vide(cellules[7]), Vide(cellules[5]),
                Vide(cellules[6]), Vide(cellules[8]));
            var (coordonnees, refus) = await eleves.VerifierLaFicheAsync(fiche);
            if (refus is not null)
            {
                erreurs.Add($"ligne {numero} : {Minuscule(refus.Message!)}");
                continue;
            }

            Groupe? groupe = null;
            if (!string.IsNullOrWhiteSpace(cellules[9]))
            {
                var (trouve, probleme) = TrouverLeGroupe(cellules[9], groupes, matieres);
                if (probleme is not null)
                {
                    erreurs.Add($"ligne {numero} : {probleme}");
                    continue;
                }
                groupe = trouve;
            }

            // Les doublons possibles : même prénom et même nom, ou un téléphone en commun, dans le centre ou dans le fichier.
            var nomDeRecherche = Eleve.Normaliser($"{coordonnees!.Prenom} {coordonnees.Nom}");
            var telephones = new[] { coordonnees.TelephoneParent, coordonnees.TelephoneEleve }.OfType<string>().ToList();
            if (existants.FirstOrDefault(e => e.NomDeRecherche == nomDeRecherche || telephones.Contains(e.TelephoneParent ?? "-") || telephones.Contains(e.TelephoneEleve ?? "-"))
                is { } existant)
                doublons.Add($"ligne {numero} : doublon possible avec {existant.Prenom} {existant.Nom}, déjà dans le centre");
            else if (dejaVus.TryGetValue(nomDeRecherche, out var autre))
                doublons.Add($"ligne {numero} : doublon possible avec la ligne {autre}");
            dejaVus.TryAdd(nomDeRecherche, numero);
            aImporter.Add((numero, fiche, groupe));
        }

        if (erreurs.Count > 0)
            return ResultatDImport.Refuse(erreurs, doublons);

        var inscrits = 0;
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        foreach (var (numero, fiche, groupe) in aImporter)
        {
            var ajout = await eleves.AjouterAsync(fiche);
            if (ajout.Fiche is null)
                return ResultatDImport.Refuse([$"ligne {numero} : {Minuscule(ajout.Message!)}"], doublons);
            if (groupe is null)
                continue;
            if (await PremiereSeanceDuMoisEnCoursAsync(groupe) is not { } debutLe)
                return ResultatDImport.Refuse([$"ligne {numero} : ce groupe n'a plus de séance à venir"], doublons);
            var inscription = await inscriptions.InscrireAsync(new InscriptionSaisie(ajout.Fiche.Id, groupe.Id, null, debutLe));
            if (inscription.Enregistrement is null)
                return ResultatDImport.Refuse([$"ligne {numero} : {Minuscule(inscription.Message!)}"], doublons);
            inscrits++;
        }

        await transaction.CommitAsync();
        return new ResultatDImport(true, aImporter.Count, inscrits, [], doublons);
    }

    /// <summary>LST-04 : vérifie toutes les lignes, puis importe les professeurs. Une seule erreur, et rien n'est importé.</summary>
    public async Task<ResultatDImport> ImporterDesProfesseursAsync(Stream fichier)
    {
        if (LireLesLignes(fichier, ColonnesDesProfesseurs.Length) is not { } lignes)
            return ResultatDImport.Refuse([FichierPasValable]);
        if (lignes.Count == 0)
            return ResultatDImport.Refuse([FichierVide]);
        if (lignes.Count > LignesAuPlus)
            return ResultatDImport.Refuse([$"Le fichier a plus de {LignesAuPlus} lignes : découpez-le"]);

        var existants = await contexte.Professeurs.Select(p => new { p.Prenom, p.Nom, p.Telephone }).ToListAsync();
        var erreurs = new List<string>();
        var doublons = new List<string>();
        var aImporter = new List<(int Ligne, FicheDuProfesseur Fiche)>();
        var dejaVus = new Dictionary<string, int>();
        foreach (var (numero, cellules) in lignes)
        {
            var fiche = new FicheDuProfesseur(cellules[0], cellules[1], cellules[2], Vide(cellules[3]));
            var (verifiee, refus) = ProfesseursDuCentre.VerifierLaFiche(fiche);
            if (refus is not null)
            {
                erreurs.Add($"ligne {numero} : {Minuscule(refus.Message!)}");
                continue;
            }
            if (existants.FirstOrDefault(p => p.Telephone == verifiee.Telephone) is { } existant)
                doublons.Add($"ligne {numero} : doublon possible avec {existant.Prenom} {existant.Nom}, même téléphone");
            else if (dejaVus.TryGetValue(verifiee.Telephone, out var autre))
                doublons.Add($"ligne {numero} : doublon possible avec la ligne {autre}, même téléphone");
            dejaVus.TryAdd(verifiee.Telephone, numero);
            aImporter.Add((numero, fiche));
        }

        if (erreurs.Count > 0)
            return ResultatDImport.Refuse(erreurs, doublons);

        await using var transaction = await contexte.Database.BeginTransactionAsync();
        foreach (var (numero, fiche) in aImporter)
        {
            var ajout = await professeurs.AjouterAsync(fiche);
            if (ajout.Professeur is null)
                return ResultatDImport.Refuse([$"ligne {numero} : {Minuscule(ajout.Message!)}"], doublons);
        }
        await transaction.CommitAsync();
        return new ResultatDImport(true, aImporter.Count, 0, [], doublons);
    }

    /// <summary>
    /// GRP-07 : la première séance du Mois en cours d'une Étude (le Mois de la dernière séance passée), ou la prochaine
    /// séance d'une Révision ou d'une Formation ; <c>null</c> si le groupe n'a plus de séance.
    /// </summary>
    internal async Task<DateOnly?> PremiereSeanceDuMoisEnCoursAsync(Groupe groupe)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var dates = await contexte.Seances.QuiComptent().Where(s => s.GroupeId == groupe.Id)
            .OrderBy(s => s.Date).ThenBy(s => s.Debut).Select(s => s.Date).ToListAsync();
        if (dates.Count == 0 || dates[^1] < aujourdhui)
            return null;
        if (groupe.Type != TypeDeGroupe.Etude)
            return dates.First(d => d >= aujourdhui);
        var passees = dates.Count(d => d <= aujourdhui);
        if (passees == 0)
            return dates[0];
        var numero = groupe.PremiereSeance + passees - 1;
        var premiereDuMois = Math.Max(CalendrierDuGroupe.PremiereSeanceDuMois(CalendrierDuGroupe.MoisDeLaSeance(numero)), groupe.PremiereSeance);
        return dates[premiereDuMois - groupe.PremiereSeance];
    }

    /// <summary>« Mathématiques · Groupe Samedi 10h → 12h », ou le nom seul s'il n'y a qu'un groupe ouvert de ce nom.</summary>
    private static (Groupe? Groupe, string? Probleme) TrouverLeGroupe(string saisie, List<Groupe> groupes, Dictionary<Guid, string> matieres)
    {
        var texte = saisie.Trim();
        var morceaux = texte.Split('·', 2, StringSplitOptions.TrimEntries);
        var candidats = morceaux.Length == 2
            ? groupes.Where(g => Pareil(matieres[g.MatiereId], morceaux[0]) && Pareil(g.Nom, morceaux[1])).ToList()
            : groupes.Where(g => Pareil(g.Nom, texte)).ToList();
        return candidats.Count switch
        {
            1 => (candidats[0], null),
            0 => (null, $"groupe introuvable « {texte} »"),
            _ => (null, $"plusieurs groupes s'appellent « {texte} » : écrivez « Matière · {texte} »"),
        };
    }

    /// <summary>Les lignes remplies (à partir de la 2ᵉ), avec leur numéro dans le fichier ; <c>null</c> si ce n'est pas un fichier Excel.</summary>
    private static List<(int Numero, string[] Cellules)>? LireLesLignes(Stream fichier, int colonnes)
    {
        try
        {
            using var classeur = new XLWorkbook(fichier);
            var feuille = classeur.Worksheet(1);
            var derniere = feuille.LastRowUsed()?.RowNumber() ?? 1;
            var lignes = new List<(int, string[])>();
            for (var numero = 2; numero <= derniere; numero++)
            {
                var cellules = Enumerable.Range(1, colonnes).Select(c => feuille.Cell(numero, c).GetFormattedString().Trim()).ToArray();
                if (cellules.All(string.IsNullOrWhiteSpace))
                    continue;
                lignes.Add((numero, cellules));
            }
            return lignes;
        }
        catch (Exception erreur) when (erreur is InvalidDataException or ArgumentException or NotSupportedException or IOException
                                           or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static IXLWorksheet Entetes(IXLWorksheet feuille, string[] colonnes)
    {
        for (var c = 0; c < colonnes.Length; c++)
            feuille.Cell(1, c + 1).Value = colonnes[c];
        feuille.Row(1).Style.Font.Bold = true;
        feuille.SheetView.FreezeRows(1);
        feuille.Columns().Width = 24;
        return feuille;
    }

    private static byte[] Enregistrer(XLWorkbook classeur)
    {
        using var flux = new MemoryStream();
        classeur.SaveAs(flux);
        return flux.ToArray();
    }

    private static bool? Oui(string texte) => Eleve.Normaliser(texte.Trim()) switch
    {
        "" or "non" or "n" or "0" or "false" => false,
        "oui" or "o" or "1" or "true" or "x" => true,
        _ => null,
    };

    private static string? Vide(string texte) => string.IsNullOrWhiteSpace(texte) ? null : texte.Trim();

    private static bool Pareil(string a, string b) => Eleve.Normaliser(a) == Eleve.Normaliser(b);

    /// <summary>« Le prénom est obligatoire » → « le prénom est obligatoire », après « ligne 14 : ».</summary>
    private static string Minuscule(string message) =>
        message.Length > 1 && char.IsUpper(message[0]) && !char.IsUpper(message[1]) ? char.ToLowerInvariant(message[0]) + message[1..] : message;
}

/// <summary>
/// LST-03 et LST-04 : le résultat d'un import : importé ou non, le nombre de fiches et d'inscriptions, les erreurs à
/// corriger (rien n'est importé s'il y en a), et les doublons possibles.
/// </summary>
public sealed record ResultatDImport(bool Importe, int Importes, int Inscrits, List<string> Erreurs, List<string> Doublons)
{
    public static ResultatDImport Refuse(List<string> erreurs, List<string>? doublons = null) => new(false, 0, 0, erreurs, doublons ?? []);
}
