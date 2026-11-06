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

        var attendus = await AttendusAsync(seance);
        var absents = (saisie.Absents ?? []).Distinct().ToHashSet();
        if (absents.Any(id => attendus.All(a => a.Inscription.Id != id)))
            return Refus(StatusCodes.Status400BadRequest, PasAttendu);

        var avant = await contexte.Absences.Where(a => a.SeanceId == seance.Id).ToListAsync();
        var etaientAbsents = avant.Select(a => a.InscriptionId).ToHashSet();
        if (seance.AppelFaitLe is not null)
            NoterLesCorrections(auteur, seance, attendus, etaientAbsents, absents, await ContexteDuGroupeAsync(seance.GroupeId));

        contexte.Absences.RemoveRange(avant.Where(a => !absents.Contains(a.InscriptionId)));
        contexte.Absences.AddRange(absents.Where(id => !etaientAbsents.Contains(id)).Select(id => new Absence(seance.Id, id)));
        seance.MarquerLAppelFait(temps.GetUtcNow());
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueDeLAppel(await DecrireAsync(seance), StatusCodes.Status200OK, null);
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
        return seances.Select(s =>
        {
            var groupe = groupes[s.GroupeId];
            return new AppelARecopier(s.Id, s.Date, Heure(s.Debut), Heure(s.Fin),
                new GroupeDeLAppel(groupe.Id, groupe.Nom, groupe.Type.ToString(), matieres[groupe.MatiereId]), professeurs[groupe.ProfesseurId]);
        }).ToList();
    }

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
        var absences = await contexte.Absences.Where(a => a.SeanceId == seance.Id).ToDictionaryAsync(a => a.InscriptionId);
        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId);

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
                var messages = absence is null
                    ? []
                    : DestinatairesDeLEleve.De(a.Eleve)
                        .Select(d => new MessageWhatsApp(d.Nom, TelephoneTunisien.Grouper(d.Chiffres),
                            WhatsApp.Lien(d.Chiffres, TexteDeLAbsence(a.Eleve, groupe.Matiere, seance, centre))))
                        .ToList();
                return new EleveALAppel(a.Inscription.Id, a.Eleve.Id, a.Eleve.Prenom, a.Eleve.Nom, absence is not null, absence?.PrevenuLe, messages);
            }).ToList(),
            seance.AnnuleeLe is not null,
            seance.MotifDAnnulation,
            seance.Nature.ToString());
    }

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

    private static string Heure(TimeOnly heure) => heure.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static IssueDeLAppel Refus(int statut, string message) => new(null, statut, message);

    private sealed record Attendu(Inscription Inscription, Eleve Eleve);

    private sealed record ContexteDuGroupe(string Nom, string Type, string Matiere, string Professeur);
}

/// <summary>Ce qu'une action sur un appel a donné : l'appel et le statut HTTP, ou un refus (statut et message).</summary>
public sealed record IssueDeLAppel(AppelDeLaSeance? Appel, int Statut, string? Message);
