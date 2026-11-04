using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;

namespace SchoolHub.Modules.Groupes;

/// <summary>
/// GRP-04 à GRP-07 : les groupes du centre courant. Le filtre automatique ne montre que ceux du centre : un groupe, un
/// professeur ou un niveau d'un autre centre est « introuvable ». Les séances sont créées avec le groupe ; leurs numéros,
/// les Mois, les prix des séances et l'état « terminé » se calculent. Les règles de saisie sont dans <see cref="SaisieDuGroupe"/>.
/// </summary>
public sealed class GroupesDuCentre(SchoolHubDbContext contexte, TimeProvider temps, JournalDuCentre journal)
{
    private const string TypeInconnu = "Le type du groupe est inconnu";
    private const string ChoisirLeProfesseur = "Choisissez le professeur";
    private const string ProfesseurIntrouvable = "Professeur introuvable";
    private const string ProfesseurParti = "Ce professeur a quitté le centre";
    private const string MatiereObligatoire = "La matière est obligatoire";
    private const string MatiereTropLongue = "La matière fait au plus 100 caractères";
    private const string ChoisirLeNiveau = "Choisissez le niveau";
    private const string NiveauIntrouvable = "Niveau introuvable";
    private const string PrixPasValable = "Le prix du Mois doit être positif, au millime près";
    private const string PrixTotalPasValable = "Le prix total doit être positif, au millime près";
    private const string NombreDeSeancesPasValable = "Le nombre de séances va de 1 à 200";
    private const string MoisEnCoursPasValable = "Le Mois en cours va de 1 à 20";
    private const string DejaTermine = "Ce groupe serait déjà terminé : vérifiez les dates";
    private const string TropLoin = "Une date est à plus de 5 ans d'ici : vérifiez les dates";
    private const string DateObligatoire = "La date de début est obligatoire";
    private const string SansAnneeScolaire = "Réglez d'abord l'année scolaire dans les paramètres du centre";
    private const string CapacitePasValable = "La capacité doit être d'au moins 1 place";
    private const string NomTropLong = "Le nom du groupe fait au plus 100 caractères";
    private const string AucuneSeance = "Aucune séance entre la date de début et la fin de l'année scolaire";
    private const string Introuvable = "Groupe introuvable";
    private const string DejaFerme = "Ce groupe est déjà fermé";
    private const string Ferme = "Ce groupe est fermé : il ne se modifie plus";
    private const string DateDeFermetureObligatoire = "La date de fermeture est obligatoire";
    private const string FermetureDansLeFutur = "La date de fermeture ne peut pas être après aujourd'hui";
    private const string MotifObligatoire = "Écrivez le motif de la fermeture";
    private const string MotifTropLong = "Le motif fait au plus 200 caractères";

    /// <summary>La taille de la colonne du détail d'une ligne du journal.</summary>
    private const int LongueurMaximaleDuDetail = 500;

    /// <summary>Les matières du centre, par nom.</summary>
    public Task<List<Matiere>> MatieresAsync() => contexte.Matieres.OrderBy(m => m.Nom).ToListAsync();

    /// <summary>
    /// Crée un groupe et ses séances : une Étude jusqu'à la fin de son année scolaire, une Révision ou une Formation
    /// jusqu'à son nombre de séances. Les champs sont vérifiés dans l'ordre du formulaire : le premier refusé donne
    /// la réponse, et rien n'est enregistré.
    /// </summary>
    public async Task<IssueSurUnGroupe> CreerAsync(CreationDUnGroupe saisie)
    {
        if (!SaisieDuGroupe.LireLeType(saisie.Type, out var type))
            return Refus(StatusCodes.Status400BadRequest, TypeInconnu);

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

        // Le niveau est facultatif pour une Formation seulement.
        if (saisie.NiveauId is { } niveauId)
        {
            if (!await contexte.Niveaux.AnyAsync(n => n.Id == niveauId))
                return Refus(StatusCodes.Status404NotFound, NiveauIntrouvable);
        }
        else if (type != TypeDeGroupe.Formation)
        {
            return Refus(StatusCodes.Status400BadRequest, ChoisirLeNiveau);
        }

        var (prevision, refus) = type == TypeDeGroupe.Etude
            ? await PrevoirUneEtudeAsync(saisie)
            : await PrevoirUneRevisionOuUneFormationAsync(saisie, type);
        if (refus is not null)
            return refus;

        if (saisie.Capacite is < 1)
            return Refus(StatusCodes.Status400BadRequest, CapacitePasValable);
        var nom = string.IsNullOrWhiteSpace(saisie.Nom) ? prevision!.NomPropose : saisie.Nom.Trim();
        if (nom.Length > Groupe.LongueurMaximaleDuNom)
            return Refus(StatusCodes.Status400BadRequest, NomTropLong);

        var normalise = Matiere.Normaliser(nomDeLaMatiere);
        var matiere = await contexte.Matieres.SingleOrDefaultAsync(m => m.NomNormalise == normalise);
        if (matiere is null)
        {
            matiere = new Matiere(nomDeLaMatiere);
            contexte.Matieres.Add(matiere);
        }

        var groupe = type == TypeDeGroupe.Etude
            ? Groupe.Etude(nom, professeur.Id, matiere.Id, saisie.NiveauId!.Value, prevision!.AnneeScolaireId!.Value, saisie.Capacite,
                prevision.DateDeDebut, prevision.Creneaux, saisie.PrixDuMois!.Value, prevision.PremiereSeance)
            : Groupe.RevisionOuFormation(type, nom, professeur.Id, matiere.Id, saisie.NiveauId, prevision!.AnneeScolaireId, saisie.Capacite,
                prevision.DateDeDebut, prevision.Creneaux, saisie.PrixTotal!.Value, saisie.NombreDeSeances!.Value, prevision.PremiereSeance,
                prevision.Tranches);
        contexte.Groupes.Add(groupe);
        contexte.Seances.AddRange(prevision.Seances.Select(s => new Seance(groupe.Id, s.Date, s.Debut, s.Fin)));
        await contexte.SaveChangesAsync();

        return new IssueSurUnGroupe((await LireAsync(groupe.Id))!, StatusCodes.Status201Created, null);
    }

    /// <summary>Tous les groupes du centre, par nom, pour les cadres de la page Professeurs.</summary>
    public async Task<List<ResumeDuGroupe>> ListerAsync()
    {
        var groupes = await contexte.Groupes.OrderBy(g => g.Nom).ThenBy(g => g.Id).ToListAsync();
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);
        var niveaux = await contexte.Niveaux.ToDictionaryAsync(n => n.Id, n => n.Nom);
        var dernieresSeances = await contexte.Seances
            .GroupBy(s => s.GroupeId)
            .Select(g => new { GroupeId = g.Key, Derniere = g.Max(s => s.Date) })
            .ToDictionaryAsync(g => g.GroupeId, g => g.Derniere);
        var aujourdhui = DateDeTunis.Aujourdhui(temps);

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
                Eleves: 0,
                CalendrierDuGroupe.EstTermine(g.Type, dernieresSeances.TryGetValue(g.Id, out var derniere) ? derniere : null, aujourdhui),
                Ferme: g.FermeLe is not null))
            .ToList();
    }

    /// <summary>
    /// Change le nom, la capacité et, pour une Étude, le prix du Mois, avec les règles de la création. Le nouveau prix vaut
    /// à partir du premier Mois qui n'a pas commencé ; s'il est le même que celui de ce Mois, rien n'est ajouté. Le prix
    /// d'une Révision ou d'une Formation est fixé à la création : un prix envoyé pour elles est ignoré.
    /// </summary>
    public async Task<IssueSurUnGroupe> ModifierAsync(Guid id, ModificationDUnGroupe saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        await VerrouillerAsync(id);
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == id);
        if (groupe is null)
            return GroupeIntrouvable;
        if (groupe.FermeLe is not null)
            return Refus(StatusCodes.Status409Conflict, Ferme);

        var nom = string.IsNullOrWhiteSpace(saisie.Nom) ? CalendrierDuGroupe.NomPropose(groupe.Creneaux[0]) : saisie.Nom.Trim();
        if (nom.Length > Groupe.LongueurMaximaleDuNom)
            return Refus(StatusCodes.Status400BadRequest, NomTropLong);
        if (saisie.Capacite is < 1)
            return Refus(StatusCodes.Status400BadRequest, CapacitePasValable);
        var etude = groupe.Type == TypeDeGroupe.Etude;
        if (etude && !SaisieDuGroupe.MontantValable(saisie.PrixDuMois))
            return Refus(StatusCodes.Status400BadRequest, PrixPasValable);

        groupe.Modifier(nom, saisie.Capacite);
        if (etude)
        {
            var premierMoisAVenir = PremierMoisAVenir(groupe, await SeancesAsync(id), DateDeTunis.Aujourdhui(temps));
            if (CalendrierDuGroupe.PrixDuMois(groupe.Prix, premierMoisAVenir) != saisie.PrixDuMois)
                groupe.ChangerLePrix(saisie.PrixDuMois!.Value, premierMoisAVenir);
        }

        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        return new IssueSurUnGroupe((await LireAsync(id))!, StatusCodes.Status200OK, null);
    }

    /// <summary>
    /// GRP-08 : ferme le groupe à une date (aujourd'hui ou avant), avec un motif. Ses séances après cette date
    /// disparaissent ; celle du jour même reste. La fermeture va au journal, dans le même enregistrement.
    /// </summary>
    public async Task<IssueSurUnGroupe> FermerAsync(Compte auteur, Guid id, FermetureDUnGroupe saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        await VerrouillerAsync(id);
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == id);
        if (groupe is null)
            return GroupeIntrouvable;
        if (groupe.FermeLe is not null)
            return Refus(StatusCodes.Status409Conflict, DejaFerme);

        if (saisie.Date is not { } date)
            return Refus(StatusCodes.Status400BadRequest, DateDeFermetureObligatoire);
        if (date > DateDeTunis.Aujourdhui(temps))
            return Refus(StatusCodes.Status400BadRequest, FermetureDansLeFutur);
        var motif = saisie.Motif?.Trim() ?? "";
        if (motif.Length == 0)
            return Refus(StatusCodes.Status400BadRequest, MotifObligatoire);
        if (motif.Length > Groupe.LongueurMaximaleDuMotif)
            return Refus(StatusCodes.Status400BadRequest, MotifTropLong);

        var professeur = await contexte.Professeurs.SingleAsync(p => p.Id == groupe.ProfesseurId);
        // Un nom, un professeur et un motif à leur plus long dépasseraient la colonne du détail : il est coupé.
        var detail = $"{groupe.Nom} · {professeur.Prenom} {professeur.Nom} · {motif}";
        journal.Noter(
            auteur,
            ActionDuJournal.GroupeFerme,
            detail.Length > LongueurMaximaleDuDetail ? detail[..LongueurMaximaleDuDetail] : detail,
            avant: "Ouvert",
            apres: $"Fermé le {SaisieDuGroupe.EcrireUneDate(date)}");
        groupe.Fermer(date, motif);
        contexte.Seances.RemoveRange(await contexte.Seances.Where(s => s.GroupeId == id && s.Date > date).ToListAsync());
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        return new IssueSurUnGroupe((await LireAsync(id))!, StatusCodes.Status200OK, null);
    }

    /// <summary>La fiche du groupe, selon son type ; <c>null</c> s'il n'est pas dans ce centre.</summary>
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

        List<MoisDuGroupe> mois = [];
        List<SeanceNumerotee> seancesNumerotees = [];
        decimal? prixDuMois = null;
        if (groupe.Type == TypeDeGroupe.Etude)
        {
            var premierMois = CalendrierDuGroupe.MoisDeLaSeance(groupe.PremiereSeance);
            mois = CalendrierDuGroupe.EnMois(seances)
                .Select((seancesDuMois, i) => new MoisDuGroupe(
                    premierMois + i,
                    CalendrierDuGroupe.PrixDuMois(groupe.Prix, premierMois + i),
                    seancesDuMois[0].Date <= aujourdhui,
                    seancesDuMois.Select(SeanceDuGroupe.De).ToList()))
                .ToList();
            prixDuMois = CalendrierDuGroupe.PrixDuMois(groupe.Prix, PremierMoisAVenir(groupe, seances, aujourdhui));
        }
        else
        {
            // Le prix d'une séance de Révision se calcule sur toutes ses séances, celles d'avant SchoolHub comprises.
            var prixDesSeances = groupe.Type == TypeDeGroupe.Revision
                ? CalendrierDuGroupe.PrixDesSeances(groupe.PrixTotal!.Value, groupe.NombreDeSeances!.Value)
                : null;
            seancesNumerotees = seances
                .Select((seance, i) => SeanceNumerotee.De(seance, groupe.PremiereSeance + i, prixDesSeances))
                .ToList();
        }

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
            prixDuMois,
            mois,
            CalendrierDuGroupe.EstTermine(groupe.Type, seances.LastOrDefault()?.Date, aujourdhui),
            groupe.PremiereSeance,
            groupe.PrixTotal,
            groupe.NombreDeSeances,
            seancesNumerotees,
            groupe.Tranches.OrderBy(t => t.Seance).Select(t => new TrancheDuGroupe(t.Montant, t.Seance)).ToList(),
            groupe.FermeLe,
            groupe.MotifDeFermeture);
    }

    /// <summary>Ce que la création prépare, selon le type : les créneaux, les séances, l'année et le nom proposé.</summary>
    private sealed record Prevision(
        List<Creneau> Creneaux,
        IReadOnlyList<SeancePrevue> Seances,
        DateOnly DateDeDebut,
        Guid? AnneeScolaireId,
        int PremiereSeance,
        string NomPropose,
        List<Tranche> Tranches);

    /// <summary>Une Étude : ses créneaux, son prix du Mois, et ses séances jusqu'à la fin de l'année scolaire.</summary>
    private async Task<(Prevision?, IssueSurUnGroupe?)> PrevoirUneEtudeAsync(CreationDUnGroupe saisie)
    {
        var (creneaux, refusDesCreneaux) = SaisieDuGroupe.LireLesCreneaux(saisie.Creneaux);
        if (refusDesCreneaux is not null)
            return (null, Refus(StatusCodes.Status400BadRequest, refusDesCreneaux));
        if (!SaisieDuGroupe.MontantValable(saisie.PrixDuMois))
            return (null, Refus(StatusCodes.Status400BadRequest, PrixPasValable));
        // Un groupe déjà en cours (GRP-07) : « Mois 5 » fait de sa première séance dans SchoolHub la séance 1 du Mois 5.
        if (saisie.MoisEnCours is < 1 or > SaisieDuGroupe.MoisEnCoursMaximal)
            return (null, Refus(StatusCodes.Status400BadRequest, MoisEnCoursPasValable));
        var premiereSeance = CalendrierDuGroupe.PremiereSeanceDuMois(saisie.MoisEnCours ?? 1);

        if (saisie.DateDeDebut is not { } dateDeDebut)
            return (null, Refus(StatusCodes.Status400BadRequest, DateObligatoire));
        var annees = await contexte.AnneesScolaires.OrderByDescending(a => a.Debut).ToListAsync();
        if (annees.Count == 0)
            return (null, Refus(StatusCodes.Status409Conflict, SansAnneeScolaire));
        var annee = annees.FirstOrDefault(a => a.Debut <= dateDeDebut && dateDeDebut <= a.Fin);
        if (annee is null)
            return (null, Refus(StatusCodes.Status400BadRequest,
                $"La date de début doit être dans l'année scolaire, du {SaisieDuGroupe.EcrireUneDate(annees[0].Debut)} au {SaisieDuGroupe.EcrireUneDate(annees[0].Fin)}"));

        var seances = CalendrierDuGroupe.Seances(creneaux!, dateDeDebut, annee.Fin);
        if (seances.Count == 0)
            return (null, Refus(StatusCodes.Status400BadRequest, AucuneSeance));

        return (new Prevision(creneaux!, seances, dateDeDebut, annee.Id, premiereSeance, CalendrierDuGroupe.NomPropose(creneaux![0]), []), null);
    }

    /// <summary>
    /// Une Révision ou une Formation : son prix total, son nombre de séances, ses dates (depuis des créneaux ou une à une)
    /// et, pour une Formation, ses tranches.
    /// </summary>
    private async Task<(Prevision?, IssueSurUnGroupe?)> PrevoirUneRevisionOuUneFormationAsync(CreationDUnGroupe saisie, TypeDeGroupe type)
    {
        if (!SaisieDuGroupe.MontantValable(saisie.PrixTotal))
            return (null, Refus(StatusCodes.Status400BadRequest, PrixTotalPasValable));
        if (saisie.NombreDeSeances is not { } nombre || nombre < 1 || nombre > SaisieDuGroupe.NombreMaximalDeSeances)
            return (null, Refus(StatusCodes.Status400BadRequest, NombreDeSeancesPasValable));

        // Un groupe déjà en cours (GRP-07) : « séance 4 sur 10 » crée les séances 4 à 10.
        if (saisie.SeanceEnCours is { } enCours && (enCours < 1 || enCours > nombre))
            return (null, Refus(StatusCodes.Status400BadRequest, $"La séance en cours va de 1 à {nombre}"));
        var premiereSeance = saisie.SeanceEnCours ?? 1;
        var seancesAVenir = nombre - premiereSeance + 1;
        // Une Révision ou une Formation n'a pas d'année scolaire pour borner ses dates : une faute de frappe (2025 pour
        // 2028, 9999) ne doit ni créer un groupe déjà terminé, qu'on ne pourrait plus corriger, ni déborder le calendrier.
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var auPlusTard = aujourdhui.AddYears(5);
        List<Creneau> creneaux;
        IReadOnlyList<SeancePrevue> seances;
        string nomPropose;
        if (saisie.Dates is { Count: > 0 } dates)
        {
            var (lues, refusDesDates) = SaisieDuGroupe.LireLesDates(dates, seancesAVenir);
            if (refusDesDates is not null)
                return (null, Refus(StatusCodes.Status400BadRequest, refusDesDates));
            if (lues!.Any(s => s.Date > auPlusTard))
                return (null, Refus(StatusCodes.Status400BadRequest, TropLoin));
            nomPropose = CalendrierDuGroupe.NomPropose(lues![0]);
            seances = lues.OrderBy(s => s.Date).ThenBy(s => s.Debut).ToList();
            // Les créneaux se déduisent des dates, dans l'ordre saisi : le filtre par jour et le nom proposé s'en servent.
            creneaux = lues
                .Select(s => (Jour: Jours.De(s.Date.DayOfWeek), s.Debut, s.Fin))
                .Distinct()
                .Select(c => new Creneau(c.Jour, c.Debut, c.Fin))
                .ToList();
        }
        else
        {
            var (lus, refusDesCreneaux) = SaisieDuGroupe.LireLesCreneaux(saisie.Creneaux);
            if (refusDesCreneaux is not null)
                return (null, Refus(StatusCodes.Status400BadRequest, refusDesCreneaux));
            if (saisie.DateDeDebut is not { } dateDeDebut)
                return (null, Refus(StatusCodes.Status400BadRequest, DateObligatoire));
            if (dateDeDebut > auPlusTard)
                return (null, Refus(StatusCodes.Status400BadRequest, TropLoin));
            creneaux = lus!;
            seances = CalendrierDuGroupe.SeancesEnNombre(creneaux, dateDeDebut, seancesAVenir);
            nomPropose = CalendrierDuGroupe.NomPropose(creneaux[0]);
        }

        if (seances[^1].Date < aujourdhui)
            return (null, Refus(StatusCodes.Status400BadRequest, DejaTermine));

        List<Tranche> tranches = [];
        if (type == TypeDeGroupe.Formation)
        {
            var (lues, refusDesTranches) = SaisieDuGroupe.LireLesTranches(saisie.Tranches, saisie.PrixTotal!.Value, nombre);
            if (refusDesTranches is not null)
                return (null, Refus(StatusCodes.Status400BadRequest, refusDesTranches));
            tranches = lues!;
        }

        // Pas d'année scolaire exigée : une Formation peut se faire l'été. Le groupe garde celle de sa première séance.
        var premiereDate = seances[0].Date;
        var annee = await contexte.AnneesScolaires
            .Where(a => a.Debut <= premiereDate && premiereDate <= a.Fin)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync();

        return (new Prevision(creneaux, seances, premiereDate, annee, premiereSeance, nomPropose, tranches), null);
    }

    /// <summary>Le numéro du premier Mois d'Étude qui n'a pas commencé, en comptant depuis le Mois en cours à la création.</summary>
    private static int PremierMoisAVenir(Groupe groupe, IReadOnlyList<Seance> seances, DateOnly aujourdhui) =>
        CalendrierDuGroupe.MoisDeLaSeance(groupe.PremiereSeance) - 1
        + CalendrierDuGroupe.PremierMoisNonCommence(CalendrierDuGroupe.EnMois(seances).Select(m => m[0].Date).ToList(), aujourdhui);

    /// <summary>
    /// Verrouille la ligne du groupe jusqu'à la fin de la transaction : deux fermetures (ou une fermeture et une
    /// modification) du même groupe, venues de deux onglets, passent l'une après l'autre. La seconde voit le groupe déjà
    /// fermé et le refuse, au lieu d'écrire une seconde ligne au journal ou de répondre 500. « NO KEY » suffit : les
    /// séances qui pointent vers le groupe restent libres.
    /// </summary>
    private Task VerrouillerAsync(Guid id) =>
        contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM groupes WHERE id = {id} FOR NO KEY UPDATE");

    /// <summary>Les séances du groupe, rangées comme on les compte : par date, puis par heure de début.</summary>
    private Task<List<Seance>> SeancesAsync(Guid groupeId) =>
        contexte.Seances
            .Where(s => s.GroupeId == groupeId)
            .OrderBy(s => s.Date).ThenBy(s => s.Debut).ThenBy(s => s.Id)
            .ToListAsync();

    private static IssueSurUnGroupe Refus(int statut, string message) => new(null, statut, message);

    /// <summary>Le refus de lecture d'un groupe absent : inconnu, ou d'un autre centre.</summary>
    public static IssueSurUnGroupe GroupeIntrouvable => Refus(StatusCodes.Status404NotFound, Introuvable);
}

/// <summary>Ce qu'une action sur un groupe a donné : la fiche et le statut HTTP, ou un refus (statut et message).</summary>
public sealed record IssueSurUnGroupe(FicheDuGroupe? Fiche, int Statut, string? Message);
