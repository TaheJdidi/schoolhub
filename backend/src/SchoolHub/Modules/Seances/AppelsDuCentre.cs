using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;
using SchoolHub.Socle.Messages;

namespace SchoolHub.Modules.Seances;

/// <summary>
/// SEA-07, SEA-09, SEA-10 et SEA-11 : l'appel des séances du centre courant. Tous les élèves attendus sont présents,
/// sauf ceux marqués absents. Le premier enregistrement fait passer la séance à « appel fait » ; les suivants sont des
/// corrections, chacune au journal. Après l'appel, un message WhatsApp est prêt pour les destinataires de chaque absent.
/// </summary>
public sealed class AppelsDuCentre(SchoolHubDbContext contexte, JournalDuCentre journal, CentreCourant centreCourant, TimeProvider temps)
{
    public const string SeanceIntrouvable = "Séance introuvable";
    private const string PasEncoreOuvert = "L'appel s'ouvre le jour de la séance";
    private const string SeanceAnnulee = "Cette séance est annulée : elle n'a pas d'appel";
    private const string PasAttendu = "Cet élève n'est pas attendu à cette séance";
    private const string AbsenceIntrouvable = "Absence introuvable";
    private const string Present = "Présent";
    private const string Absent = "Absent";

    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    /// <summary>
    /// L'appel d'une séance : ses élèves attendus (inscrits ce jour-là), par nom puis prénom, absents ou présents, avec
    /// le message prêt pour chaque absent. <c>null</c> si la séance n'est pas dans ce centre.
    /// </summary>
    public async Task<AppelDeLaSeance?> LireAsync(Guid seanceId)
    {
        var seance = await contexte.Seances.SingleOrDefaultAsync(s => s.Id == seanceId);
        return seance is null ? null : await DecrireAsync(seance);
    }

    /// <summary>
    /// SEA-07 et SEA-10 : enregistre l'appel, les absents donnés, tous les autres présents. Une séance qui a déjà son
    /// appel est corrigée : chaque élève qui change va au journal, avec l'ancienne et la nouvelle valeur. La ligne de la
    /// séance reste verrouillée jusqu'à la fin : deux appels de la même séance passent l'un après l'autre.
    /// </summary>
    public async Task<IssueDeLAppel> EnregistrerAsync(Compte auteur, Guid seanceId, AppelSaisi saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM seances WHERE id = {seanceId} AND centre_id = {CentreId} FOR UPDATE");
        var seance = await contexte.Seances.SingleOrDefaultAsync(s => s.Id == seanceId);
        if (seance is null)
            return Refus(StatusCodes.Status404NotFound, SeanceIntrouvable);
        await contexte.Entry(seance).ReloadAsync();
        if (seance.AnnuleeLe is not null)
            return Refus(StatusCodes.Status409Conflict, SeanceAnnulee);
        if (seance.Date > DateDeTunis.Aujourdhui(temps))
            return Refus(StatusCodes.Status409Conflict, PasEncoreOuvert);

        // SEA-12 : les élèves en rattrapage sont à l'appel aussi, avec l'inscription de leur propre groupe.
        var attendus = (await AttendusAsync(seance)).Concat((await EnRattrapageAsync(seance.Id)).Select(r => r.Attendu)).ToList();
        var absents = (saisie.Absents ?? []).Distinct().ToHashSet();
        if (absents.Any(id => attendus.All(a => a.Inscription.Id != id)))
            return Refus(StatusCodes.Status400BadRequest, PasAttendu);

        var avant = await contexte.Absences.Where(a => a.SeanceId == seance.Id).ToListAsync();
        var etaientAbsents = avant.Select(a => a.InscriptionId).ToHashSet();
        if (seance.AppelFaitLe is not null)
            NoterLesCorrections(auteur, seance, attendus, etaientAbsents, absents, await ContexteDuGroupeAsync(seance.GroupeId));

        // Seuls les attendus changent : l'absence d'un élève qui ne l'est plus (inscription arrêtée depuis) reste telle quelle.
        var attendusIds = attendus.Select(a => a.Inscription.Id).ToHashSet();
        contexte.Absences.RemoveRange(avant.Where(a => attendusIds.Contains(a.InscriptionId) && !absents.Contains(a.InscriptionId)));
        contexte.Absences.AddRange(absents.Where(id => !etaientAbsents.Contains(id)).Select(id => new Absence(seance.Id, id)));
        seance.MarquerLAppelFait(temps.GetUtcNow(), $"{auteur.Prenom} {auteur.Nom}");
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueDeLAppel(await DecrireAsync(seance), StatusCodes.Status200OK, null);
    }

    // ----- Le rattrapage (SEA-12) -----

    /// <summary>Les séances qu'un élève peut rattraper : celles de son groupe qui comptent, 8 semaines avant ou après.</summary>
    private const int JoursAutourDeLaSeance = 56;

    private const string InscriptionIntrouvable = "Inscription introuvable";
    private const string DejaDansCeGroupe = "Cet élève est déjà dans ce groupe";
    private const string PasLeMemeGroupe = "Le rattrapage se fait dans un groupe du même professeur, de la même matière et du même niveau";
    private const string PasInscritCeJourLa = "Cet élève n'est pas inscrit dans son groupe à la date de cette séance";
    private const string ChoisirLaSeanceRattrapee = "Choisissez la séance de son groupe qu'il rattrape";
    private const string SeanceDejaRattrapee = "Cette séance est déjà rattrapée";
    private const string DejaEnRattrapage = "Cet élève est déjà en rattrapage à cette séance";
    private const string RattrapageIntrouvable = "Rattrapage introuvable";
    private const string GroupeFerme = "Ce groupe est fermé : pas de rattrapage";

    /// <summary>
    /// SEA-12 : les élèves qui peuvent venir en rattrapage à cette séance : inscrits ce jour-là dans un autre groupe ouvert
    /// du même professeur, de la même matière et du même niveau, avec les séances de leur groupe qu'ils peuvent rattraper
    /// (celles qui comptent, 8 semaines avant ou après, pas encore rattrapées). <c>null</c> si la séance n'est pas dans ce centre.
    /// </summary>
    public async Task<List<EleveRattrapable>?> RattrapablesAsync(Guid seanceId)
    {
        var seance = await contexte.Seances.SingleOrDefaultAsync(s => s.Id == seanceId);
        if (seance is null)
            return null;
        var groupe = await contexte.Groupes.SingleAsync(g => g.Id == seance.GroupeId);
        var groupes = await contexte.Groupes
            .Where(g => g.Id != groupe.Id && g.FermeLe == null && g.ProfesseurId == groupe.ProfesseurId && g.MatiereId == groupe.MatiereId
                && g.NiveauId == groupe.NiveauId)
            .ToDictionaryAsync(g => g.Id);
        var groupeIds = groupes.Keys.ToList();
        var dejaLa = await contexte.Rattrapages.Where(r => r.SeanceId == seance.Id).Select(r => r.InscriptionId).ToListAsync();
        var inscrits = await contexte.Inscriptions
            .Where(i => groupeIds.Contains(i.GroupeId) && i.DebutLe <= seance.Date && (i.ArreteLe == null || i.ArreteLe >= seance.Date) && !dejaLa.Contains(i.Id))
            .Join(contexte.Eleves, i => i.EleveId, e => e.Id, (i, e) => new { Inscription = i, Eleve = e })
            .ToListAsync();
        var (du, au) = (seance.Date.AddDays(-JoursAutourDeLaSeance), seance.Date.AddDays(JoursAutourDeLaSeance));
        var seances = await contexte.Seances.QuiComptent()
            .Where(s => groupeIds.Contains(s.GroupeId) && s.Date >= du && s.Date <= au)
            .OrderBy(s => s.Date).ThenBy(s => s.Debut)
            .ToListAsync();
        var inscriptionIds = inscrits.Select(i => i.Inscription.Id).ToList();
        var rattrapees = (await contexte.Rattrapages.Where(r => inscriptionIds.Contains(r.InscriptionId))
            .Select(r => new { r.InscriptionId, r.SeanceRattrapeeId }).ToListAsync())
            .ToLookup(r => r.InscriptionId, r => r.SeanceRattrapeeId);

        return inscrits
            .OrderBy(i => i.Eleve.Nom).ThenBy(i => i.Eleve.Prenom).ThenBy(i => i.Eleve.Id)
            .Select(i => new EleveRattrapable(
                i.Inscription.Id,
                i.Eleve.Id,
                i.Eleve.Prenom,
                i.Eleve.Nom,
                new GroupeDuRattrapage(i.Inscription.GroupeId, groupes[i.Inscription.GroupeId].Nom),
                seances.Where(s => s.GroupeId == i.Inscription.GroupeId && s.Date >= i.Inscription.DebutLe
                        && (i.Inscription.ArreteLe == null || s.Date <= i.Inscription.ArreteLe) && !rattrapees[i.Inscription.Id].Contains(s.Id))
                    .Select(s => new SeanceARattraper(s.Id, s.Date, Heure(s.Debut), Heure(s.Fin)))
                    .ToList()))
            .ToList();
    }

    /// <summary>SEA-12 : l'élève vient à cette séance, à la place d'une séance précise de son groupe. Il paie toujours dans son groupe.</summary>
    public async Task<IssueDeLAppel> AjouterUnRattrapageAsync(Guid seanceId, RattrapageSaisi saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM seances WHERE id = {seanceId} AND centre_id = {CentreId} FOR UPDATE");
        var seance = await contexte.Seances.SingleOrDefaultAsync(s => s.Id == seanceId);
        if (seance is null)
            return Refus(StatusCodes.Status404NotFound, SeanceIntrouvable);
        if (seance.AnnuleeLe is not null)
            return Refus(StatusCodes.Status409Conflict, SeanceAnnulee);
        var inscription = saisie.InscriptionId is { } inscriptionId ? await contexte.Inscriptions.SingleOrDefaultAsync(i => i.Id == inscriptionId) : null;
        if (inscription is null)
            return Refus(StatusCodes.Status404NotFound, InscriptionIntrouvable);
        if (inscription.GroupeId == seance.GroupeId)
            return Refus(StatusCodes.Status400BadRequest, DejaDansCeGroupe);
        var accueil = await contexte.Groupes.SingleAsync(g => g.Id == seance.GroupeId);
        var origine = await contexte.Groupes.SingleAsync(g => g.Id == inscription.GroupeId);
        if (accueil.FermeLe is not null || origine.FermeLe is not null)
            return Refus(StatusCodes.Status409Conflict, GroupeFerme);
        if (origine.ProfesseurId != accueil.ProfesseurId || origine.MatiereId != accueil.MatiereId || origine.NiveauId != accueil.NiveauId)
            return Refus(StatusCodes.Status400BadRequest, PasLeMemeGroupe);
        if (inscription.DebutLe > seance.Date || inscription.ArreteLe < seance.Date)
            return Refus(StatusCodes.Status400BadRequest, PasInscritCeJourLa);
        // Les mêmes règles que la liste des possibles : une séance de son groupe qui compte, pendant son inscription, à
        // 8 semaines au plus de la séance d'accueil.
        var rattrapee = saisie.SeanceRattrapeeId is { } rattrapeeId
            ? await contexte.Seances.QuiComptent().SingleOrDefaultAsync(s => s.Id == rattrapeeId && s.GroupeId == inscription.GroupeId)
            : null;
        if (rattrapee is null || rattrapee.Date < inscription.DebutLe || rattrapee.Date > inscription.ArreteLe
            || Math.Abs(rattrapee.Date.DayNumber - seance.Date.DayNumber) > JoursAutourDeLaSeance)
            return Refus(StatusCodes.Status400BadRequest, ChoisirLaSeanceRattrapee);
        if (await contexte.Rattrapages.AnyAsync(r => r.SeanceId == seance.Id && r.InscriptionId == inscription.Id))
            return Refus(StatusCodes.Status409Conflict, DejaEnRattrapage);
        if (await contexte.Rattrapages.AnyAsync(r => r.InscriptionId == inscription.Id && r.SeanceRattrapeeId == rattrapee.Id))
            return Refus(StatusCodes.Status409Conflict, SeanceDejaRattrapee);

        contexte.Rattrapages.Add(new Rattrapage(seance.Id, inscription.Id, rattrapee.Id));
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueDeLAppel(await DecrireAsync(seance), StatusCodes.Status200OK, null);
    }

    /// <summary>SEA-12 : retire un rattrapage ajouté par erreur, avec son absence à cette séance s'il en avait une.</summary>
    public async Task<IssueDeLAppel> RetirerUnRattrapageAsync(Guid seanceId, Guid rattrapageId)
    {
        var rattrapage = await contexte.Rattrapages.SingleOrDefaultAsync(r => r.Id == rattrapageId && r.SeanceId == seanceId);
        if (rattrapage is null)
            return Refus(StatusCodes.Status404NotFound, RattrapageIntrouvable);
        contexte.Absences.RemoveRange(await contexte.Absences.Where(a => a.SeanceId == seanceId && a.InscriptionId == rattrapage.InscriptionId).ToListAsync());
        contexte.Rattrapages.Remove(rattrapage);
        await contexte.SaveChangesAsync();
        return new IssueDeLAppel(await DecrireAsync(await contexte.Seances.SingleAsync(s => s.Id == seanceId)), StatusCodes.Status200OK, null);
    }

    /// <summary>SEA-11 : le message à un absent est ouvert ; il est marqué, pour ne pas l'envoyer deux fois.</summary>
    public async Task<IssueDeLAppel> PrevenirAsync(Guid seanceId, Guid inscriptionId)
    {
        var absence = await contexte.Absences.SingleOrDefaultAsync(a => a.SeanceId == seanceId && a.InscriptionId == inscriptionId);
        if (absence is null)
            return Refus(StatusCodes.Status404NotFound, AbsenceIntrouvable);
        absence.MarquerPrevenu(temps.GetUtcNow());
        await contexte.SaveChangesAsync();
        return new IssueDeLAppel(null, StatusCodes.Status204NoContent, null);
    }

    /// <summary>
    /// SEA-09 : les séances passées (avant aujourd'hui) encore « appel à faire », les plus anciennes en premier. Une
    /// séance compte à sa date même si son appel n'est jamais fait : la liste sert seulement à recopier les feuilles.
    /// </summary>
    public async Task<List<AppelARecopier>> ARecopierAsync()
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var seances = await contexte.Seances.QuiOntLieu().Where(s => s.Date < aujourdhui && s.AppelFaitLe == null)
            .OrderBy(s => s.Date).ThenBy(s => s.Debut).ThenBy(s => s.Id)
            .ToListAsync();
        var groupeIds = seances.Select(s => s.GroupeId).Distinct().ToList();
        var groupes = await contexte.Groupes.Where(g => groupeIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id);
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);
        var professeurs = await contexte.Professeurs.ToDictionaryAsync(p => p.Id, p => $"{p.Prenom} {p.Nom}");
        var niveaux = await contexte.Niveaux.ToDictionaryAsync(n => n.Id, n => n.Nom);
        return seances.Select(s =>
        {
            var groupe = groupes[s.GroupeId];
            return new AppelARecopier(s.Id, s.Date, Heure(s.Debut), Heure(s.Fin),
                new GroupeDeLAppel(groupe.Id, groupe.Nom, groupe.Type.ToString(), matieres[groupe.MatiereId], NiveauDe(groupe, niveaux)),
                professeurs[groupe.ProfesseurId]);
        }).ToList();
    }

    /// <summary>
    /// SEA-01 : les séances d'aujourd'hui, heure par heure : le groupe, le professeur, le nombre d'élèves attendus (les
    /// inscrits ce jour-là et ceux en rattrapage), et leur état : « AppelFait », « AppelAFaire » ou « Annulee ».
    /// </summary>
    public async Task<List<SeanceDuJour>> DuJourAsync()
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var seances = await contexte.Seances.Where(s => s.Date == aujourdhui).OrderBy(s => s.Debut).ThenBy(s => s.Id).ToListAsync();
        var groupeIds = seances.Select(s => s.GroupeId).Distinct().ToList();
        var groupes = await contexte.Groupes.Where(g => groupeIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id);
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);
        var professeurs = await contexte.Professeurs.ToDictionaryAsync(p => p.Id, p => $"{p.Prenom} {p.Nom}");
        var niveaux = await contexte.Niveaux.ToDictionaryAsync(n => n.Id, n => n.Nom);
        var inscrits = await contexte.Inscriptions
            .Where(i => groupeIds.Contains(i.GroupeId) && i.DebutLe <= aujourdhui && (i.ArreteLe == null || i.ArreteLe >= aujourdhui))
            .GroupBy(i => i.GroupeId).Select(g => new { GroupeId = g.Key, Nombre = g.Count() })
            .ToDictionaryAsync(g => g.GroupeId, g => g.Nombre);
        var seanceIds = seances.Select(s => s.Id).ToList();
        var rattrapages = await contexte.Rattrapages.Where(r => seanceIds.Contains(r.SeanceId))
            .GroupBy(r => r.SeanceId).Select(g => new { SeanceId = g.Key, Nombre = g.Count() })
            .ToDictionaryAsync(g => g.SeanceId, g => g.Nombre);

        return seances.Select(s =>
        {
            var groupe = groupes[s.GroupeId];
            var etat = s.AnnuleeLe is not null ? "Annulee" : s.AppelFaitLe is not null ? "AppelFait" : "AppelAFaire";
            return new SeanceDuJour(s.Id, Heure(s.Debut), Heure(s.Fin),
                new GroupeDeLAppel(groupe.Id, groupe.Nom, groupe.Type.ToString(), matieres[groupe.MatiereId], NiveauDe(groupe, niveaux)),
                professeurs[groupe.ProfesseurId], inscrits.GetValueOrDefault(groupe.Id) + rattrapages.GetValueOrDefault(s.Id), etat);
        }).ToList();
    }

    /// <summary>Le nom du niveau du groupe ; <c>null</c> pour une Formation sans niveau.</summary>
    private static string? NiveauDe(Groupe groupe, Dictionary<Guid, string> niveaux) =>
        groupe.NiveauId is { } id ? niveaux.GetValueOrDefault(id) : null;

    private void NoterLesCorrections(Compte auteur, Seance seance, List<Attendu> attendus, HashSet<Guid> avant, HashSet<Guid> apres, ContexteDuGroupe groupe)
    {
        foreach (var attendu in attendus.Where(a => avant.Contains(a.Inscription.Id) != apres.Contains(a.Inscription.Id)))
        {
            var detail = $"{attendu.Eleve.Prenom} {attendu.Eleve.Nom} · {groupe.Matiere}, {groupe.Nom} · séance du {SaisieDuGroupe.EcrireUneDate(seance.Date)}";
            journal.Noter(auteur, ActionDuJournal.PresenceCorrigee, detail.Length > 500 ? detail[..500] : detail,
                avant.Contains(attendu.Inscription.Id) ? Absent : Present,
                apres.Contains(attendu.Inscription.Id) ? Absent : Present);
        }
    }

    private async Task<AppelDeLaSeance> DecrireAsync(Seance seance)
    {
        var groupe = await ContexteDuGroupeAsync(seance.GroupeId);
        var attendus = await AttendusAsync(seance);
        var enRattrapage = await EnRattrapageAsync(seance.Id);
        var absences = await contexte.Absences.Where(a => a.SeanceId == seance.Id).ToDictionaryAsync(a => a.InscriptionId);
        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId);
        // SEA-12 : dans le groupe d'origine, la séance rattrapée ailleurs montre « rattrapée le 18/01 ».
        var rattrapeesLe = await contexte.Rattrapages.Where(r => r.SeanceRattrapeeId == seance.Id)
            .Join(contexte.Seances, r => r.SeanceId, s => s.Id, (r, s) => new { r.InscriptionId, s.Date })
            .ToDictionaryAsync(r => r.InscriptionId, r => r.Date);

        List<MessageWhatsApp> Messages(Eleve eleve, Absence? absence) => absence is null
            ? []
            : DestinatairesDeLEleve.De(eleve)
                .Select(d => new MessageWhatsApp(d.Nom, TelephoneTunisien.Grouper(d.Chiffres),
                    WhatsApp.Lien(d.Chiffres, TexteDeLAbsence(eleve, groupe.Matiere, seance, centre))))
                .ToList();

        return new AppelDeLaSeance(
            seance.Id,
            seance.Date,
            Heure(seance.Debut),
            Heure(seance.Fin),
            new GroupeDeLAppel(seance.GroupeId, groupe.Nom, groupe.Type, groupe.Matiere),
            groupe.Professeur,
            seance.Date <= DateDeTunis.Aujourdhui(temps) && seance.AnnuleeLe is null,
            seance.AppelFaitLe is not null,
            attendus.Select(a =>
            {
                var absence = absences.GetValueOrDefault(a.Inscription.Id);
                return new EleveALAppel(a.Inscription.Id, a.Eleve.Id, a.Eleve.Prenom, a.Eleve.Nom, absence is not null, absence?.PrevenuLe,
                    Messages(a.Eleve, absence), rattrapeesLe.TryGetValue(a.Inscription.Id, out var le) ? le : null);
            }).ToList(),
            seance.AnnuleeLe is not null,
            seance.MotifDAnnulation,
            seance.Nature.ToString(),
            enRattrapage.Select(r =>
            {
                var absence = absences.GetValueOrDefault(r.Attendu.Inscription.Id);
                return new EleveEnRattrapage(r.Rattrapage.Id, r.Attendu.Inscription.Id, r.Attendu.Eleve.Id, r.Attendu.Eleve.Prenom, r.Attendu.Eleve.Nom,
                    r.GroupeDOrigine, r.SeanceRattrapee, absence is not null, absence?.PrevenuLe, Messages(r.Attendu.Eleve, absence));
            }).ToList(),
            seance.AppelFaitLe,
            seance.AppelFaitPar);
    }

    /// <summary>SEA-12 : les élèves en rattrapage à cette séance, par nom puis prénom, avec leur groupe et la séance rattrapée.</summary>
    private async Task<List<EnRattrapage>> EnRattrapageAsync(Guid seanceId) =>
        (await contexte.Rattrapages.Where(r => r.SeanceId == seanceId)
            .Join(contexte.Inscriptions, r => r.InscriptionId, i => i.Id, (r, i) => new { Rattrapage = r, Inscription = i })
            .Join(contexte.Eleves, x => x.Inscription.EleveId, e => e.Id, (x, e) => new { x.Rattrapage, x.Inscription, Eleve = e })
            .Join(contexte.Groupes, x => x.Inscription.GroupeId, g => g.Id, (x, g) => new { x.Rattrapage, x.Inscription, x.Eleve, Groupe = g.Nom })
            .Join(contexte.Seances, x => x.Rattrapage.SeanceRattrapeeId, s => s.Id, (x, s) => new { x.Rattrapage, x.Inscription, x.Eleve, x.Groupe, s.Date })
            .ToListAsync())
        .Select(x => new EnRattrapage(x.Rattrapage, new Attendu(x.Inscription, x.Eleve), x.Groupe, x.Date))
        .OrderBy(x => x.Attendu.Eleve.Nom).ThenBy(x => x.Attendu.Eleve.Prenom).ThenBy(x => x.Rattrapage.Id)
        .ToList();

    /// <summary>
    /// « Bonjour, Adem Miled était absent à la séance de mathématiques du samedi 15/01/2028, de 10h à 12h. Centre Les
    /// Jasmins. » La matière garde sa majuscule si c'est un sigle (« SVT »).
    /// </summary>
    internal static string TexteDeLAbsence(Eleve eleve, string matiere, Seance seance, Centre centre)
    {
        var enMinuscule = matiere.Length > 1 && char.IsLower(matiere[1]) ? char.ToLowerInvariant(matiere[0]) + matiere[1..] : matiere;
        var jour = Jours.De(seance.Date.DayOfWeek).ToString().ToLowerInvariant();
        return $"Bonjour, {eleve.Prenom} {eleve.Nom} était absent à la séance de {enMinuscule} du {jour} {SaisieDuGroupe.EcrireUneDate(seance.Date)}, "
            + $"de {CalendrierDuGroupe.Heure(seance.Debut)} à {CalendrierDuGroupe.Heure(seance.Fin)}. {centre.Nom}.";
    }

    /// <summary>Les élèves attendus : inscrits dans le groupe ce jour-là (arrivés avant ou ce jour, pas encore arrêtés), par nom puis prénom.</summary>
    private async Task<List<Attendu>> AttendusAsync(Seance seance) =>
        (await contexte.Inscriptions
            .Where(i => i.GroupeId == seance.GroupeId && i.DebutLe <= seance.Date && (i.ArreteLe == null || i.ArreteLe >= seance.Date))
            .Join(contexte.Eleves, i => i.EleveId, e => e.Id, (i, e) => new { Inscription = i, Eleve = e })
            .ToListAsync())
        .Select(a => new Attendu(a.Inscription, a.Eleve))
        .OrderBy(a => a.Eleve.Nom).ThenBy(a => a.Eleve.Prenom).ThenBy(a => a.Eleve.Id)
        .ToList();

    private async Task<ContexteDuGroupe> ContexteDuGroupeAsync(Guid groupeId)
    {
        var groupe = await contexte.Groupes.SingleAsync(g => g.Id == groupeId);
        var matiere = await contexte.Matieres.Where(m => m.Id == groupe.MatiereId).Select(m => m.Nom).SingleAsync();
        var professeur = await contexte.Professeurs.Where(p => p.Id == groupe.ProfesseurId).Select(p => p.Prenom + " " + p.Nom).SingleAsync();
        return new ContexteDuGroupe(groupe.Nom, groupe.Type.ToString(), matiere, professeur);
    }

    /// <summary>
    /// La fiche de l'élève : ses présences, inscription par inscription. Chaque séance qui a eu lieu, de son début à
    /// aujourd'hui (ou à l'arrêt) : « Present », « Absent », ou « AppelAFaire » tant que l'appel n'est pas fait.
    /// </summary>
    public async Task<List<PresencesDUneInscription>> PresencesDeLEleveAsync(Guid eleveId)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var inscriptions = await contexte.Inscriptions.Where(i => i.EleveId == eleveId).OrderBy(i => i.DebutLe).ThenBy(i => i.Id).ToListAsync();
        var groupeIds = inscriptions.Select(i => i.GroupeId).Distinct().ToList();
        var groupes = await contexte.Groupes.Where(g => groupeIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id);
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);
        var seances = await contexte.Seances.QuiOntLieu()
            .Where(s => groupeIds.Contains(s.GroupeId) && s.Date <= aujourdhui)
            .OrderBy(s => s.Date).ThenBy(s => s.Debut).ThenBy(s => s.Id)
            .ToListAsync();
        var inscriptionIds = inscriptions.Select(i => i.Id).ToList();
        var absences = (await contexte.Absences.Where(a => inscriptionIds.Contains(a.InscriptionId))
            .Select(a => new { a.SeanceId, a.InscriptionId }).ToListAsync())
            .Select(a => (a.SeanceId, a.InscriptionId)).ToHashSet();

        return inscriptions.Select(i =>
        {
            var groupe = groupes[i.GroupeId];
            var siennes = seances
                .Where(s => s.GroupeId == i.GroupeId && s.Date >= i.DebutLe && (i.ArreteLe == null || s.Date <= i.ArreteLe))
                .Select(s => new PresenceALaSeance(s.Id, s.Date, Heure(s.Debut),
                    s.AppelFaitLe is null ? "AppelAFaire" : absences.Contains((s.Id, i.Id)) ? "Absent" : "Present"))
                .ToList();
            return new PresencesDUneInscription(i.Id, groupe.Id, groupe.Nom, matieres[groupe.MatiereId], i.ArreteLe is not null,
                siennes.Count(p => p.Etat == "Absent"), siennes);
        }).ToList();
    }

    private static string Heure(TimeOnly heure) => heure.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static IssueDeLAppel Refus(int statut, string message) => new(null, statut, message);

    private sealed record Attendu(Inscription Inscription, Eleve Eleve);

    private sealed record EnRattrapage(Rattrapage Rattrapage, Attendu Attendu, string GroupeDOrigine, DateOnly SeanceRattrapee);

    private sealed record ContexteDuGroupe(string Nom, string Type, string Matiere, string Professeur);
}

/// <summary>Ce qu'une action sur un appel a donné : l'appel et le statut HTTP, ou un refus (statut et message).</summary>
public sealed record IssueDeLAppel(AppelDeLaSeance? Appel, int Statut, string? Message);
