using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Messages;

namespace SchoolHub.Modules.Inscriptions;

/// <summary>
/// ELV-03 : les inscriptions du centre courant. Le filtre automatique ne montre que celles du centre : un élève ou un
/// groupe d'un autre centre est « introuvable ». L'inscription dit ce que l'élève paiera à sa première séance, prévient
/// d'un groupe complet sans bloquer, et prépare le message WhatsApp de confirmation.
/// </summary>
public sealed class InscriptionsDuCentre(SchoolHubDbContext contexte, TimeProvider temps, CentreCourant centreCourant)
{
    private const string ChoisirLEleve = "Choisissez l'élève";
    private const string EleveIntrouvable = "Élève introuvable";
    private const string ChoisirLeGroupe = "Choisissez le groupe";
    private const string GroupeIntrouvable = "Groupe introuvable";
    private const string GroupeFerme = "Ce groupe est fermé : on n'y inscrit plus";
    private const string GroupeTermine = "Ce groupe est terminé : on n'y inscrit plus";
    private const string DejaInscrit = "Cet élève est déjà inscrit dans ce groupe";
    private const string ChoisirLeMode = "Choisissez le mode de tarification";
    private const string ModeQuiNeConvientPas = "Ce mode de tarification ne convient pas à ce groupe";
    private const string ChoisirLaPremiereSeance = "Choisissez la première séance";
    private const string PasDeSeanceCeJourLa = "Ce groupe n'a pas de séance ce jour-là";
    private const string CapaciteAtteinte = "Ce groupe a atteint sa capacité. L'inscription reste possible.";

    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    /// <summary>
    /// Inscrit l'élève, dans l'ordre des vérifications de la spec : le premier refus donne la réponse, et rien n'est
    /// enregistré. La ligne de l'élève reste verrouillée jusqu'à la fin : deux inscriptions du même élève, venues de deux
    /// onglets, passent l'une après l'autre, et la seconde voit la première (et son assurance).
    /// </summary>
    public async Task<IssueDInscription> InscrireAsync(DemandeDInscription demande)
    {
        if (demande.EleveId is not { } eleveId)
            return Refus(StatusCodes.Status400BadRequest, ChoisirLEleve);

        await using var transaction = await contexte.Database.BeginTransactionAsync();
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM eleves WHERE id = {eleveId} FOR NO KEY UPDATE");
        var eleve = await contexte.Eleves.SingleOrDefaultAsync(e => e.Id == eleveId);
        if (eleve is null)
            return Refus(StatusCodes.Status404NotFound, EleveIntrouvable);

        if (demande.GroupeId is not { } groupeId)
            return Refus(StatusCodes.Status400BadRequest, ChoisirLeGroupe);
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == groupeId);
        if (groupe is null)
            return Refus(StatusCodes.Status404NotFound, GroupeIntrouvable);
        if (groupe.FermeLe is not null)
            return Refus(StatusCodes.Status409Conflict, GroupeFerme);
        var seances = await SeancesAsync(groupeId);
        if (CalendrierDuGroupe.EstTermine(groupe.Type, seances.LastOrDefault()?.Date, DateDeTunis.Aujourdhui(temps)))
            return Refus(StatusCodes.Status409Conflict, GroupeTermine);
        if (await contexte.Inscriptions.AnyAsync(i => i.EleveId == eleveId && i.GroupeId == groupeId))
            return Refus(StatusCodes.Status409Conflict, DejaInscrit);

        var modes = Echeancier.ModesPossibles(groupe.Type, groupe.Tranches.Count > 0);
        ModeDeTarification mode;
        if (string.IsNullOrWhiteSpace(demande.Mode))
        {
            if (modes.Count > 1)
                return Refus(StatusCodes.Status400BadRequest, ChoisirLeMode);
            mode = modes[0];
        }
        else if (!Enum.GetNames<ModeDeTarification>().Contains(demande.Mode.Trim())
                 || !modes.Contains(mode = Enum.Parse<ModeDeTarification>(demande.Mode.Trim())))
        {
            return Refus(StatusCodes.Status400BadRequest, ModeQuiNeConvientPas);
        }

        if (demande.DebutLe is not { } debutLe)
            return Refus(StatusCodes.Status400BadRequest, ChoisirLaPremiereSeance);
        if (seances.All(s => s.Date != debutLe))
            return Refus(StatusCodes.Status400BadRequest, PasDeSeanceCeJourLa);

        var avertissement = groupe.Capacite is { } capacite && await contexte.Inscriptions.CountAsync(i => i.GroupeId == groupeId) >= capacite
            ? CapaciteAtteinte
            : null;
        var inscription = new Inscription(eleveId, groupeId, mode, debutLe, temps.GetUtcNow());
        contexte.Inscriptions.Add(inscription);
        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId);
        var assurance = await NouvelleAssuranceAsync(eleveId, groupe, centre);
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        var decrite = (await DecrireAsync([inscription]))[0];
        var aPayer = Echeancier.Calculer(Calendrier(groupe, seances), mode, debutLe)
            .Where(s => s.Seance == Echeancier.PremiereSeance(Calendrier(groupe, seances), debutLe))
            .Select(s => new SommeALArrivee(s.Libelle, s.Montant))
            .ToList();
        if (assurance is not null)
            aPayer.Add(new SommeALArrivee(AssuranceDeLAnnee.Libelle(assurance.Value.Annee), assurance.Value.Assurance.Montant));

        var texte = TexteDeConfirmation(eleve, decrite, seances.First(s => s.Date == debutLe), centre);
        var messages = DestinatairesDeLEleve.De(eleve)
            .Select(d => new MessageWhatsApp(d.Nom, TelephoneTunisien.Grouper(d.Chiffres), WhatsApp.Lien(d.Chiffres, texte)))
            .ToList();
        return new IssueDInscription(new EnregistrementDeLInscription(decrite, aPayer, avertissement, texte, messages), StatusCodes.Status201Created, null);
    }

    /// <summary>
    /// La première inscription en Étude d'une année scolaire crée l'assurance de l'année, au montant du centre ce jour-là.
    /// Un centre sans montant n'en réclame pas.
    /// </summary>
    private async Task<(AssuranceDeLAnnee Assurance, AnneeScolaire Annee)?> NouvelleAssuranceAsync(Guid eleveId, Groupe groupe, Centre centre)
    {
        if (groupe.Type != TypeDeGroupe.Etude || groupe.AnneeScolaireId is not { } anneeId || centre.MontantDeLAssurance is not { } montant)
            return null;
        if (await contexte.Assurances.AnyAsync(a => a.EleveId == eleveId && a.AnneeScolaireId == anneeId))
            return null;

        var assurance = new AssuranceDeLAnnee(eleveId, anneeId, montant);
        contexte.Assurances.Add(assurance);
        return (assurance, await contexte.AnneesScolaires.SingleAsync(a => a.Id == anneeId));
    }

    /// <summary>
    /// « Bonjour, l'inscription d'Adem Miled au Centre Les Jasmins est enregistrée. » ; la matière, le professeur, le groupe,
    /// la date et l'heure de la première séance ; puis le nom du centre et son téléphone, s'il en a un.
    /// </summary>
    private static string TexteDeConfirmation(Eleve eleve, InscriptionDeLEleve inscription, Seance premiere, Centre centre)
    {
        var jour = Jours.De(premiere.Date.DayOfWeek).ToString().ToLowerInvariant();
        var signature = centre.Telephone is { } telephone ? $"{centre.Nom}, {TelephoneTunisien.Grouper(telephone)}" : centre.Nom;
        return $"Bonjour, l'inscription {Francais.De($"{eleve.Prenom} {eleve.Nom}")} au {centre.Nom} est enregistrée.\n"
            + $"{inscription.Groupe.Matiere} avec {inscription.Professeur}, {inscription.Groupe.Nom} : première séance le {jour} "
            + $"{SaisieDuGroupe.EcrireUneDate(premiere.Date)} à {CalendrierDuGroupe.Heure(premiere.Debut)}.\n"
            + $"À bientôt. {signature}";
    }

    /// <summary>Les inscriptions telles que les pages les montrent, avec leur élève, leur groupe et leur première séance.</summary>
    private async Task<List<InscriptionDeLEleve>> DecrireAsync(IReadOnlyCollection<Inscription> inscriptions)
    {
        var eleveIds = inscriptions.Select(i => i.EleveId).Distinct().ToList();
        var groupeIds = inscriptions.Select(i => i.GroupeId).Distinct().ToList();
        var eleves = await contexte.Eleves.Where(e => eleveIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
        var groupes = await contexte.Groupes.Where(g => groupeIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id);
        var professeurIds = groupes.Values.Select(g => g.ProfesseurId).Distinct().ToList();
        var matiereIds = groupes.Values.Select(g => g.MatiereId).Distinct().ToList();
        var professeurs = await contexte.Professeurs.Where(p => professeurIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => $"{p.Prenom} {p.Nom}");
        var matieres = await contexte.Matieres.Where(m => matiereIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Nom);
        var seances = (await contexte.Seances.Where(s => groupeIds.Contains(s.GroupeId)).ToListAsync())
            .GroupBy(s => s.GroupeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Date).ThenBy(s => s.Debut).ThenBy(s => s.Id).ToList());
        var aujourdhui = DateDeTunis.Aujourdhui(temps);

        return inscriptions.Select(i =>
        {
            var eleve = eleves[i.EleveId];
            var groupe = groupes[i.GroupeId];
            var duGroupe = seances.GetValueOrDefault(i.GroupeId) ?? [];
            var premiere = duGroupe.FirstOrDefault(s => s.Date >= i.DebutLe);
            var prixNormal = groupe.Type == TypeDeGroupe.Etude
                ? CalendrierDuGroupe.PrixDuMois(groupe.Prix, GroupesDuCentre.PremierMoisAVenir(groupe, duGroupe, aujourdhui))
                : groupe.PrixTotal!.Value;
            return new InscriptionDeLEleve(
                i.Id,
                new EleveInscrit(eleve.Id, eleve.Prenom, eleve.Nom),
                new GroupeDeLInscription(groupe.Id, groupe.Nom, groupe.Type.ToString(), matieres[groupe.MatiereId]),
                professeurs[groupe.ProfesseurId],
                i.Mode.ToString(),
                prixNormal,
                groupe.Tranches.Count,
                premiere is null ? null : new SeanceDeDebut(premiere.Date, premiere.Debut));
        }).ToList();
    }

    private static CalendrierAPayer Calendrier(Groupe groupe, IReadOnlyList<Seance> seances) => new(
        groupe.Type,
        groupe.PremiereSeance,
        seances.Select(s => s.Date).ToList(),
        groupe.Prix,
        groupe.PrixTotal,
        groupe.NombreDeSeances,
        groupe.Tranches);

    /// <summary>Les séances du groupe, rangées comme on les compte : par date, puis par heure de début.</summary>
    private Task<List<Seance>> SeancesAsync(Guid groupeId) =>
        contexte.Seances
            .Where(s => s.GroupeId == groupeId)
            .OrderBy(s => s.Date).ThenBy(s => s.Debut).ThenBy(s => s.Id)
            .ToListAsync();

    private static IssueDInscription Refus(int statut, string message) => new(null, statut, message);
}

/// <summary>Ce qu'une inscription a donné : l'enregistrement et le statut HTTP, ou un refus (statut et message).</summary>
public sealed record IssueDInscription(EnregistrementDeLInscription? Enregistrement, int Statut, string? Message);
