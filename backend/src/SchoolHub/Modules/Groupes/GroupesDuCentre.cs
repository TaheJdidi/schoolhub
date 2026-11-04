using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Groupes;

/// <summary>
/// GRP-04 : les groupes du centre courant. Le filtre automatique ne montre que ceux du centre : un groupe, un professeur
/// ou un niveau d'un autre centre est « introuvable ». Les séances sont créées avec le groupe ; les Mois se calculent.
/// </summary>
public sealed class GroupesDuCentre(SchoolHubDbContext contexte, TimeProvider temps)
{
    private const string ChoisirLeProfesseur = "Choisissez le professeur";
    private const string ProfesseurIntrouvable = "Professeur introuvable";
    private const string ProfesseurParti = "Ce professeur a quitté le centre";
    private const string MatiereObligatoire = "La matière est obligatoire";
    private const string MatiereTropLongue = "La matière fait au plus 100 caractères";
    private const string ChoisirLeNiveau = "Choisissez le niveau";
    private const string NiveauIntrouvable = "Niveau introuvable";
    private const string AuMoinsUnCreneau = "Ajoutez au moins un créneau";
    private const string CreneauIncomplet = "Un créneau doit avoir un jour, une heure de début et une heure de fin";
    private const string CreneauALEnvers = "Un créneau doit finir après son début";
    private const string CreneauxQuiSeChevauchent = "Deux créneaux se chevauchent";
    private const string PrixPasValable = "Le prix du Mois doit être positif, au millime près";
    private const string DateObligatoire = "La date de début est obligatoire";
    private const string SansAnneeScolaire = "Réglez d'abord l'année scolaire dans les paramètres du centre";
    private const string CapacitePasValable = "La capacité doit être d'au moins 1 place";
    private const string NomTropLong = "Le nom du groupe fait au plus 100 caractères";
    private const string AucuneSeance = "Aucune séance entre la date de début et la fin de l'année scolaire";
    private const string Introuvable = "Groupe introuvable";

    /// <summary>Les matières du centre, par nom.</summary>
    public Task<List<Matiere>> MatieresAsync() => contexte.Matieres.OrderBy(m => m.Nom).ToListAsync();

    /// <summary>Crée un groupe d'Étude et ses séances, de la date de début à la fin de son année scolaire.</summary>
    public async Task<IssueSurUnGroupe> CreerAsync(CreationDUnGroupe saisie)
    {
        // Les champs dans l'ordre du formulaire : le premier refusé donne la réponse, et rien n'est enregistré.
        if (saisie.ProfesseurId is not { } professeurId)
            return Refus(StatusCodes.Status400BadRequest, ChoisirLeProfesseur);
        var professeur = await contexte.Professeurs.SingleOrDefaultAsync(p => p.Id == professeurId);
        if (professeur is null)
            return Refus(StatusCodes.Status404NotFound, ProfesseurIntrouvable);
        if (professeur.Parti)
            return Refus(StatusCodes.Status409Conflict, ProfesseurParti);

        var nomDeLaMatiere = saisie.Matiere?.Trim() ?? "";
        if (nomDeLaMatiere.Length == 0)
            return Refus(StatusCodes.Status400BadRequest, MatiereObligatoire);
        // La forme normalisée peut s'allonger : elle doit tenir dans sa colonne elle aussi.
        if (nomDeLaMatiere.Length > Matiere.LongueurMaximaleDuNom || Matiere.Normaliser(nomDeLaMatiere).Length > Matiere.LongueurMaximaleDuNom)
            return Refus(StatusCodes.Status400BadRequest, MatiereTropLongue);

        if (saisie.NiveauId is not { } niveauId)
            return Refus(StatusCodes.Status400BadRequest, ChoisirLeNiveau);
        if (!await contexte.Niveaux.AnyAsync(n => n.Id == niveauId))
            return Refus(StatusCodes.Status404NotFound, NiveauIntrouvable);

        var (creneaux, refusDesCreneaux) = LireLesCreneaux(saisie.Creneaux);
        if (refusDesCreneaux is not null)
            return Refus(StatusCodes.Status400BadRequest, refusDesCreneaux);

        if (!PrixValable(saisie.PrixDuMois))
            return Refus(StatusCodes.Status400BadRequest, PrixPasValable);

        if (saisie.DateDeDebut is not { } dateDeDebut)
            return Refus(StatusCodes.Status400BadRequest, DateObligatoire);
        var annees = await contexte.AnneesScolaires.OrderByDescending(a => a.Debut).ToListAsync();
        if (annees.Count == 0)
            return Refus(StatusCodes.Status409Conflict, SansAnneeScolaire);
        var annee = annees.FirstOrDefault(a => a.Debut <= dateDeDebut && dateDeDebut <= a.Fin);
        if (annee is null)
            return Refus(StatusCodes.Status400BadRequest,
                $"La date de début doit être dans l'année scolaire, du {Ecrire(annees[0].Debut)} au {Ecrire(annees[0].Fin)}");

        if (saisie.Capacite is < 1)
            return Refus(StatusCodes.Status400BadRequest, CapacitePasValable);

        var nom = NomDuGroupe(saisie.Nom, creneaux![0]);
        if (nom.Length > Groupe.LongueurMaximaleDuNom)
            return Refus(StatusCodes.Status400BadRequest, NomTropLong);

        var seances = CalendrierDuGroupe.Seances(creneaux, dateDeDebut, annee.Fin);
        if (seances.Count == 0)
            return Refus(StatusCodes.Status400BadRequest, AucuneSeance);

        var normalise = Matiere.Normaliser(nomDeLaMatiere);
        var matiere = await contexte.Matieres.SingleOrDefaultAsync(m => m.NomNormalise == normalise);
        if (matiere is null)
        {
            matiere = new Matiere(nomDeLaMatiere);
            contexte.Matieres.Add(matiere);
        }

        var groupe = Groupe.Etude(nom, professeur.Id, matiere.Id, niveauId, annee.Id, saisie.Capacite, dateDeDebut, creneaux,
            saisie.PrixDuMois!.Value);
        contexte.Groupes.Add(groupe);
        contexte.Seances.AddRange(seances.Select(s => new Seance(groupe.Id, s.Date, s.Debut, s.Fin)));
        await contexte.SaveChangesAsync();

        return new IssueSurUnGroupe((await LireAsync(groupe.Id))!, StatusCodes.Status201Created, null);
    }

    /// <summary>Tous les groupes du centre, par nom, pour les cadres de la page Professeurs.</summary>
    public async Task<List<ResumeDuGroupe>> ListerAsync()
    {
        var groupes = await contexte.Groupes.OrderBy(g => g.Nom).ThenBy(g => g.Id).ToListAsync();
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);
        var niveaux = await contexte.Niveaux.ToDictionaryAsync(n => n.Id, n => n.Nom);

        return groupes
            .Select(g => new ResumeDuGroupe(
                g.Id,
                g.Nom,
                g.Type.ToString(),
                g.ProfesseurId,
                matieres[g.MatiereId],
                g.NiveauId is { } niveauId ? niveaux[niveauId] : null,
                g.Creneaux.Select(CreneauDuGroupe.De).ToList(),
                g.Capacite,
                Eleves: 0))
            .ToList();
    }

    /// <summary>
    /// Change le nom, la capacité et le prix, avec les règles de la création. Le nouveau prix vaut à partir du premier
    /// Mois qui n'a pas commencé ; s'il est le même que celui de ce Mois, rien n'est ajouté.
    /// </summary>
    public async Task<IssueSurUnGroupe> ModifierAsync(Guid id, ModificationDUnGroupe saisie)
    {
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == id);
        if (groupe is null)
            return GroupeIntrouvable;

        var nom = NomDuGroupe(saisie.Nom, groupe.Creneaux[0]);
        if (nom.Length > Groupe.LongueurMaximaleDuNom)
            return Refus(StatusCodes.Status400BadRequest, NomTropLong);
        if (saisie.Capacite is < 1)
            return Refus(StatusCodes.Status400BadRequest, CapacitePasValable);
        if (!PrixValable(saisie.PrixDuMois))
            return Refus(StatusCodes.Status400BadRequest, PrixPasValable);

        groupe.Modifier(nom, saisie.Capacite);
        var premierMoisAVenir = CalendrierDuGroupe.PremierMoisNonCommence(
            CalendrierDuGroupe.EnMois(await SeancesAsync(id)).Select(mois => mois[0].Date).ToList(),
            DateDeTunis.Aujourdhui(temps));
        if (CalendrierDuGroupe.PrixDuMois(groupe.Prix, premierMoisAVenir) != saisie.PrixDuMois)
            groupe.ChangerLePrix(saisie.PrixDuMois!.Value, premierMoisAVenir);
        await contexte.SaveChangesAsync();

        return new IssueSurUnGroupe((await LireAsync(id))!, StatusCodes.Status200OK, null);
    }

    /// <summary>La fiche du groupe, avec ses Mois ; <c>null</c> s'il n'est pas dans ce centre.</summary>
    public async Task<FicheDuGroupe?> LireAsync(Guid id)
    {
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == id);
        if (groupe is null)
            return null;

        var professeur = await contexte.Professeurs.SingleAsync(p => p.Id == groupe.ProfesseurId);
        var matiere = await contexte.Matieres.SingleAsync(m => m.Id == groupe.MatiereId);
        var niveau = groupe.NiveauId is { } niveauId ? await contexte.Niveaux.SingleAsync(n => n.Id == niveauId) : null;
        var seances = await SeancesAsync(id);

        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var mois = CalendrierDuGroupe.EnMois(seances)
            .Select((seancesDuMois, i) => new MoisDuGroupe(
                i + 1,
                CalendrierDuGroupe.PrixDuMois(groupe.Prix, i + 1),
                seancesDuMois[0].Date <= aujourdhui,
                seancesDuMois.Select(SeanceDuGroupe.De).ToList()))
            .ToList();
        var premierMoisAVenir = CalendrierDuGroupe.PremierMoisNonCommence(mois.Select(m => m.Seances[0].Date).ToList(), aujourdhui);

        return new FicheDuGroupe(
            groupe.Id,
            groupe.Nom,
            groupe.Type.ToString(),
            new ProfesseurDuGroupe(professeur.Id, professeur.Prenom, professeur.Nom),
            matiere.Nom,
            niveau is null ? null : new NiveauDuGroupe(niveau.Id, niveau.Nom),
            groupe.Creneaux.Select(CreneauDuGroupe.De).ToList(),
            groupe.Capacite,
            Eleves: 0,
            groupe.DateDeDebut,
            CalendrierDuGroupe.PrixDuMois(groupe.Prix, premierMoisAVenir),
            mois);
    }

    /// <summary>Les séances du groupe, rangées comme les Mois les comptent : par date, puis par heure de début.</summary>
    private Task<List<Seance>> SeancesAsync(Guid groupeId) =>
        contexte.Seances
            .Where(s => s.GroupeId == groupeId)
            .OrderBy(s => s.Date).ThenBy(s => s.Debut).ThenBy(s => s.Id)
            .ToListAsync();

    /// <summary>Le nom saisi, sans les espaces autour ; vide, le nom proposé depuis le premier créneau.</summary>
    private static string NomDuGroupe(string? saisie, Creneau premier) =>
        string.IsNullOrWhiteSpace(saisie) ? CalendrierDuGroupe.NomPropose(premier) : saisie.Trim();

    /// <summary>Les créneaux dans l'ordre saisi, ou le refus du premier qui ne va pas.</summary>
    private static (List<Creneau>? Creneaux, string? Refus) LireLesCreneaux(List<CreneauSaisi>? saisis)
    {
        if (saisis is null || saisis.Count == 0)
            return (null, AuMoinsUnCreneau);

        var creneaux = new List<Creneau>();
        foreach (var saisi in saisis)
        {
            if (saisi is null || !Jours.EssayerDeLire(saisi.Jour, out var jour)
                || !LireUneHeure(saisi.Debut, out var debut) || !LireUneHeure(saisi.Fin, out var fin))
                return (null, CreneauIncomplet);
            if (fin <= debut)
                return (null, CreneauALEnvers);
            creneaux.Add(new Creneau(jour, debut, fin));
        }

        // Deux créneaux qui se suivent (10h → 12h, puis 12h → 14h) ne se chevauchent pas.
        var chevauchement = creneaux
            .SelectMany((a, i) => creneaux.Skip(i + 1).Select(b => (a, b)))
            .Any(paire => paire.a.Jour == paire.b.Jour && paire.a.Debut < paire.b.Fin && paire.b.Debut < paire.a.Fin);
        return chevauchement ? (null, CreneauxQuiSeChevauchent) : (creneaux, null);
    }

    private static bool LireUneHeure(string? saisie, out TimeOnly heure) =>
        TimeOnly.TryParseExact(saisie?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out heure);

    private static bool PrixValable(decimal? prix) =>
        prix is { } p && p > 0 && p < 1_000_000 && decimal.Round(p, 3) == p;

    private static string Ecrire(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static IssueSurUnGroupe Refus(int statut, string message) => new(null, statut, message);

    /// <summary>Le refus de lecture d'un groupe absent : inconnu, ou d'un autre centre.</summary>
    public static IssueSurUnGroupe GroupeIntrouvable => Refus(StatusCodes.Status404NotFound, Introuvable);
}

/// <summary>Ce qu'une action sur un groupe a donné : la fiche et le statut HTTP, ou un refus (statut et message).</summary>
public sealed record IssueSurUnGroupe(FicheDuGroupe? Fiche, int Statut, string? Message);
