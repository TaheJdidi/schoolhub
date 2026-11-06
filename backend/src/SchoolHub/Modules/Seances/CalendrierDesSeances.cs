using System.Globalization;
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
/// SEA-03 à SEA-06 : changer le calendrier d'un groupe. Annuler une séance, la déplacer (une annulation et une séance de
/// remplacement en une seule action), remplacer une séance déjà annulée, ajouter une séance supplémentaire, changer
/// l'horaire à partir d'une date. Rien ne s'enregistre sur les Mois : ils se recalculent sur les séances qui comptent.
/// Chaque action prépare un message WhatsApp pour les destinataires des élèves concernés ; l'annulation et le
/// déplacement vont au journal.
/// </summary>
public sealed class CalendrierDesSeances(SchoolHubDbContext contexte, JournalDuCentre journal, CentreCourant centreCourant, TimeProvider temps)
{
    private const string SeanceIntrouvable = "Séance introuvable";
    private const string GroupeIntrouvable = "Groupe introuvable";
    private const string GroupeFerme = "Ce groupe est fermé : ses séances ne changent plus";
    private const string GroupeTermine = "Ce groupe est terminé : ses séances ne changent plus";
    private const string DejaAnnulee = "Cette séance est déjà annulée";
    private const string AppelDejaFait = "Cette séance a son appel : elle a eu lieu";
    private const string MotifObligatoire = "Écrivez le motif de l'annulation";
    private const string MotifTropLong = "Le motif fait au plus 200 caractères";
    private const string PasAnnulee = "Seule une séance annulée se remplace";
    private const string DejaRemplacee = "Cette séance est déjà remplacée";
    private const string SupplementaireNeSeRemplacePas = "Une séance supplémentaire ne se remplace pas";
    private const string ChoisirLaDate = "Choisissez la date";
    private const string ChoisirLesHeures = "Choisissez l'heure de début et l'heure de fin";
    private const string SeanceALEnvers = "La séance doit finir après son début";
    private const string Chevauchement = "Le groupe a déjà une séance à ce moment-là";
    private const string ChoisirLaDateDeDepart = "Choisissez la date de départ du nouvel horaire";
    private const string DepartTropTot = "Le nouvel horaire part au plus tôt demain";
    private const string DepartApresLAnnee = "Cette date est après la fin de l'année scolaire";
    private const int LongueurMaximaleDuDetail = 500;

    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    /// <summary>
    /// SEA-03 : annule une séance qui n'a pas encore eu son appel, avec un motif. Elle ne compte plus : le Mois s'allonge
    /// et les suivants se décalent. L'annulation va au journal.
    /// </summary>
    public async Task<IssueDuCalendrier> AnnulerAsync(Compte auteur, Guid seanceId, AnnulationSaisie saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var (seance, groupe, refus) = await SeanceModifiableAsync(seanceId);
        if (refus is not null)
            return refus;
        var (motif, refusDuMotif) = LireLeMotif(saisie.Motif);
        if (refusDuMotif is not null)
            return refusDuMotif;

        var contexteDuGroupe = await ContexteAsync(groupe!);
        seance!.Annuler(motif!, temps.GetUtcNow());
        Noter(auteur, ActionDuJournal.SeanceAnnulee, $"{Decrire(contexteDuGroupe)} · {motif}", Quand(seance), "Annulée");
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        var texte = $"Bonjour, la séance de {contexteDuGroupe.MatiereEnMinuscule} du {Jour(seance.Date)}, de {Heure(seance.Debut)} à {Heure(seance.Fin)}, "
            + $"est annulée. {contexteDuGroupe.Centre}.";
        return await ReponseAsync(groupe!, texte, AuJour(seance.Date));
    }

    /// <summary>
    /// SEA-04 : déplace une séance à une autre date et une autre heure, en une seule action : la séance d'origine est
    /// annulée, et une séance de remplacement la remplace ; elle compte à sa place. Une séance supplémentaire déplacée
    /// reste supplémentaire. Le déplacement va au journal.
    /// </summary>
    public async Task<IssueDuCalendrier> DeplacerAsync(Compte auteur, Guid seanceId, NouvelleSeanceSaisie saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var (seance, groupe, refus) = await SeanceModifiableAsync(seanceId);
        if (refus is not null)
            return refus;
        var (nouvelle, refusDeLaNouvelle) = await LireLaNouvelleSeanceAsync(groupe!.Id, saisie, sauf: seance!.Id);
        if (refusDeLaNouvelle is not null)
            return refusDeLaNouvelle;

        var contexteDuGroupe = await ContexteAsync(groupe);
        var vers = $"{Jour(nouvelle!.Date)}, {Heure(nouvelle.Debut)} → {Heure(nouvelle.Fin)}";
        seance.Annuler($"Déplacée au {SaisieDuGroupe.EcrireUneDate(nouvelle.Date)}", temps.GetUtcNow());
        var nature = seance.Nature == NatureDeLaSeance.Supplementaire ? NatureDeLaSeance.Supplementaire : NatureDeLaSeance.Remplacement;
        contexte.Seances.Add(new Seance(groupe.Id, nouvelle.Date, nouvelle.Debut, nouvelle.Fin, nature,
            nature == NatureDeLaSeance.Remplacement ? seance.Id : null));
        Noter(auteur, ActionDuJournal.SeanceDeplacee, Decrire(contexteDuGroupe), Quand(seance), vers);
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        var texte = $"Bonjour, la séance de {contexteDuGroupe.MatiereEnMinuscule} du {Jour(seance.Date)} à {Heure(seance.Debut)} est déplacée "
            + $"au {Jour(nouvelle.Date)}, de {Heure(nouvelle.Debut)} à {Heure(nouvelle.Fin)}. {contexteDuGroupe.Centre}.";
        return await ReponseAsync(groupe, texte, AuJour(seance.Date < nouvelle.Date ? seance.Date : nouvelle.Date));
    }

    /// <summary>
    /// SEA-03 et SEA-04 : remplace une séance déjà annulée, pour garder le nombre de séances prévu d'une Révision ou d'une
    /// Formation (ou un Mois complet). Le remplacement va au journal comme un déplacement.
    /// </summary>
    public async Task<IssueDuCalendrier> RemplacerAsync(Compte auteur, Guid seanceId, NouvelleSeanceSaisie saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var (seance, groupe, refus) = await SeanceVerrouilleeAsync(seanceId);
        if (refus is not null)
            return refus;
        if (seance!.AnnuleeLe is null)
            return Refus(StatusCodes.Status409Conflict, PasAnnulee);
        if (seance.Nature == NatureDeLaSeance.Supplementaire)
            return Refus(StatusCodes.Status409Conflict, SupplementaireNeSeRemplacePas);
        if (await contexte.Seances.AnyAsync(s => s.RemplaceLaSeanceId == seance.Id && s.AnnuleeLe == null))
            return Refus(StatusCodes.Status409Conflict, DejaRemplacee);
        var (nouvelle, refusDeLaNouvelle) = await LireLaNouvelleSeanceAsync(groupe!.Id, saisie, sauf: null);
        if (refusDeLaNouvelle is not null)
            return refusDeLaNouvelle;

        var contexteDuGroupe = await ContexteAsync(groupe);
        contexte.Seances.Add(new Seance(groupe.Id, nouvelle!.Date, nouvelle.Debut, nouvelle.Fin, NatureDeLaSeance.Remplacement, seance.Id));
        Noter(auteur, ActionDuJournal.SeanceDeplacee, Decrire(contexteDuGroupe), Quand(seance) + " (annulée)",
            $"{Jour(nouvelle.Date)}, {Heure(nouvelle.Debut)} → {Heure(nouvelle.Fin)}");
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        var texte = $"Bonjour, la séance de {contexteDuGroupe.MatiereEnMinuscule} du {Jour(seance.Date)}, annulée, est remplacée "
            + $"le {Jour(nouvelle.Date)}, de {Heure(nouvelle.Debut)} à {Heure(nouvelle.Fin)}. {contexteDuGroupe.Centre}.";
        return await ReponseAsync(groupe, texte, AuJour(nouvelle.Date));
    }

    /// <summary>SEA-05 : ajoute une séance offerte : elle a son appel, ne compte pas dans le Mois, et rien de plus n'est dû.</summary>
    public async Task<IssueDuCalendrier> AjouterAsync(Guid groupeId, NouvelleSeanceSaisie saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var (groupe, refus) = await GroupeModifiableAsync(groupeId);
        if (refus is not null)
            return refus;
        var (nouvelle, refusDeLaNouvelle) = await LireLaNouvelleSeanceAsync(groupe!.Id, saisie, sauf: null);
        if (refusDeLaNouvelle is not null)
            return refusDeLaNouvelle;

        contexte.Seances.Add(new Seance(groupe.Id, nouvelle!.Date, nouvelle.Debut, nouvelle.Fin, NatureDeLaSeance.Supplementaire));
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        var contexteDuGroupe = await ContexteAsync(groupe);
        var texte = $"Bonjour, une séance supplémentaire de {contexteDuGroupe.MatiereEnMinuscule} est ajoutée le {Jour(nouvelle.Date)}, "
            + $"de {Heure(nouvelle.Debut)} à {Heure(nouvelle.Fin)}. Elle est offerte. {contexteDuGroupe.Centre}.";
        return await ReponseAsync(groupe, texte, AuJour(nouvelle.Date));
    }

    /// <summary>
    /// SEA-06 : change l'horaire du groupe à partir d'une date (demain au plus tôt). Les séances prévues de cette date et
    /// après, sans appel, sont recréées sur les nouveaux créneaux ; les séances d'avant, de remplacement et supplémentaires
    /// restent. Une Étude va jusqu'à la fin de son année scolaire ; une Révision ou une Formation garde son nombre de séances.
    /// </summary>
    public async Task<IssueDuCalendrier> ChangerLHoraireAsync(Guid groupeId, ChangementDHoraire saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var (groupe, refus) = await GroupeModifiableAsync(groupeId);
        if (refus is not null)
            return refus;
        var (creneaux, refusDesCreneaux) = SaisieDuGroupe.LireLesCreneaux(saisie.Creneaux);
        if (refusDesCreneaux is not null)
            return Refus(StatusCodes.Status400BadRequest, refusDesCreneaux);
        if (saisie.APartirDu is not { } depart)
            return Refus(StatusCodes.Status400BadRequest, ChoisirLaDateDeDepart);
        if (depart <= DateDeTunis.Aujourdhui(temps))
            return Refus(StatusCodes.Status400BadRequest, DepartTropTot);

        var seances = await contexte.Seances.Where(s => s.GroupeId == groupe!.Id).ToListAsync();
        var aRecreer = seances.Where(s => s.Nature == NatureDeLaSeance.Prevue && s.Date >= depart && s.AppelFaitLe is null).ToList();
        IReadOnlyList<SeancePrevue> nouvelles;
        if (groupe!.Type == TypeDeGroupe.Etude)
        {
            var annee = await contexte.AnneesScolaires.SingleAsync(a => a.Id == groupe.AnneeScolaireId);
            if (depart > annee.Fin)
                return Refus(StatusCodes.Status400BadRequest, DepartApresLAnnee);
            nouvelles = CalendrierDuGroupe.Seances(creneaux!, depart, annee.Fin);
        }
        else
        {
            // Le nombre de séances prévu dans SchoolHub, moins celles qui restent et comptent.
            var gardees = seances.Except(aRecreer).Count(s => s.Compte);
            var aCreer = groupe.NombreDeSeances!.Value - groupe.PremiereSeance + 1 - gardees;
            nouvelles = aCreer > 0 ? CalendrierDuGroupe.SeancesEnNombre(creneaux!, depart, aCreer) : [];
        }

        contexte.Seances.RemoveRange(aRecreer);
        contexte.Seances.AddRange(nouvelles.Select(s => new Seance(groupe.Id, s.Date, s.Debut, s.Fin)));
        groupe.ChangerLesCreneaux(creneaux!);
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        var contexteDuGroupe = await ContexteAsync(groupe);
        var horaires = string.Join(" et ", creneaux!.Select(c => $"le {c.Jour.ToString().ToLowerInvariant()} de {Heure(c.Debut)} à {Heure(c.Fin)}"));
        var texte = $"Bonjour, à partir du {Jour(depart)}, "
            + (creneaux!.Count > 1 ? $"les séances de {contexteDuGroupe.MatiereEnMinuscule} ont lieu {horaires}." : $"la séance de {contexteDuGroupe.MatiereEnMinuscule} a lieu {horaires}.")
            + $" {contexteDuGroupe.Centre}.";
        return await ReponseAsync(groupe, texte, i => i.ArreteLe == null || i.ArreteLe >= depart);
    }

    // ----- Les vérifications -----

    /// <summary>La séance, sa ligne verrouillée, et son groupe ouvert et pas terminé ; ou le premier refus.</summary>
    private async Task<(Seance? Seance, Groupe? Groupe, IssueDuCalendrier? Refus)> SeanceVerrouilleeAsync(Guid seanceId)
    {
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM seances WHERE id = {seanceId} AND centre_id = {CentreId} FOR UPDATE");
        var seance = await contexte.Seances.SingleOrDefaultAsync(s => s.Id == seanceId);
        if (seance is null)
            return (null, null, Refus(StatusCodes.Status404NotFound, SeanceIntrouvable));
        await contexte.Entry(seance).ReloadAsync();
        var (groupe, refus) = await GroupeModifiableAsync(seance.GroupeId);
        return (seance, groupe, refus);
    }

    /// <summary>Une séance qu'on peut encore annuler ou déplacer : ni annulée, ni avec son appel.</summary>
    private async Task<(Seance? Seance, Groupe? Groupe, IssueDuCalendrier? Refus)> SeanceModifiableAsync(Guid seanceId)
    {
        var (seance, groupe, refus) = await SeanceVerrouilleeAsync(seanceId);
        if (refus is not null)
            return (null, null, refus);
        if (seance!.AnnuleeLe is not null)
            return (null, null, Refus(StatusCodes.Status409Conflict, DejaAnnulee));
        if (seance.AppelFaitLe is not null)
            return (null, null, Refus(StatusCodes.Status409Conflict, AppelDejaFait));
        return (seance, groupe, null);
    }

    /// <summary>Le groupe, sa ligne verrouillée, ouvert et pas terminé ; ou le premier refus.</summary>
    private async Task<(Groupe? Groupe, IssueDuCalendrier? Refus)> GroupeModifiableAsync(Guid groupeId)
    {
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM groupes WHERE id = {groupeId} AND centre_id = {CentreId} FOR NO KEY UPDATE");
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == groupeId);
        if (groupe is null)
            return (null, Refus(StatusCodes.Status404NotFound, GroupeIntrouvable));
        if (groupe.FermeLe is not null)
            return (null, Refus(StatusCodes.Status409Conflict, GroupeFerme));
        var derniere = await contexte.Seances.QuiComptent().Where(s => s.GroupeId == groupeId).MaxAsync(s => (DateOnly?)s.Date);
        if (CalendrierDuGroupe.EstTermine(groupe.Type, derniere, DateDeTunis.Aujourdhui(temps)))
            return (null, Refus(StatusCodes.Status409Conflict, GroupeTermine));
        return (groupe, null);
    }

    private static (string? Motif, IssueDuCalendrier? Refus) LireLeMotif(string? saisie)
    {
        var motif = saisie?.Trim() ?? "";
        if (motif.Length == 0)
            return (null, Refus(StatusCodes.Status400BadRequest, MotifObligatoire));
        return motif.Length > Seance.LongueurMaximaleDuMotif ? (null, Refus(StatusCodes.Status400BadRequest, MotifTropLong)) : (motif, null);
    }

    /// <summary>La date et les heures d'une nouvelle séance, qui ne chevauche aucune autre séance du groupe qui a lieu.</summary>
    private async Task<(SeancePrevue? Seance, IssueDuCalendrier? Refus)> LireLaNouvelleSeanceAsync(Guid groupeId, NouvelleSeanceSaisie saisie, Guid? sauf)
    {
        if (saisie.Date is not { } date)
            return (null, Refus(StatusCodes.Status400BadRequest, ChoisirLaDate));
        if (!LireUneHeure(saisie.Debut, out var debut) || !LireUneHeure(saisie.Fin, out var fin))
            return (null, Refus(StatusCodes.Status400BadRequest, ChoisirLesHeures));
        if (fin <= debut)
            return (null, Refus(StatusCodes.Status400BadRequest, SeanceALEnvers));
        if (await contexte.Seances.QuiOntLieu().AnyAsync(s => s.GroupeId == groupeId && s.Id != sauf && s.Date == date && s.Debut < fin && debut < s.Fin))
            return (null, Refus(StatusCodes.Status409Conflict, Chevauchement));
        return (new SeancePrevue(date, debut, fin), null);
    }

    // ----- Les messages -----

    /// <summary>Les élèves inscrits dans le groupe ce jour-là : arrivés ce jour ou avant, pas encore arrêtés.</summary>
    private static Func<Inscription, bool> AuJour(DateOnly date) =>
        i => i.DebutLe <= date && (i.ArreteLe == null || i.ArreteLe >= date);

    /// <summary>Le texte, et pour chaque élève concerné, par nom puis prénom, un message par destinataire.</summary>
    private async Task<IssueDuCalendrier> ReponseAsync(Groupe groupe, string texte, Func<Inscription, bool> concerne)
    {
        var inscriptions = (await contexte.Inscriptions.Where(i => i.GroupeId == groupe.Id).ToListAsync()).Where(concerne).ToList();
        var eleveIds = inscriptions.Select(i => i.EleveId).Distinct().ToList();
        var eleves = await contexte.Eleves.Where(e => eleveIds.Contains(e.Id)).ToListAsync();
        var messages = eleves
            .OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ThenBy(e => e.Id)
            .Select(e => new MessagesDUnEleve(e.Id, e.Prenom, e.Nom, DestinatairesDeLEleve.De(e)
                .Select(d => new MessageWhatsApp(d.Nom, TelephoneTunisien.Grouper(d.Chiffres), WhatsApp.Lien(d.Chiffres, texte)))
                .ToList()))
            .ToList();
        return new IssueDuCalendrier(new ChangementDuCalendrier(texte, messages), StatusCodes.Status200OK, null);
    }

    private async Task<ContexteDuGroupe> ContexteAsync(Groupe groupe)
    {
        var matiere = await contexte.Matieres.Where(m => m.Id == groupe.MatiereId).Select(m => m.Nom).SingleAsync();
        var professeur = await contexte.Professeurs.Where(p => p.Id == groupe.ProfesseurId).Select(p => p.Prenom + " " + p.Nom).SingleAsync();
        var centre = await contexte.Centres.Where(c => c.Id == CentreId).Select(c => c.Nom).SingleAsync();
        return new ContexteDuGroupe(groupe.Nom, matiere, professeur, centre);
    }

    private void Noter(Compte auteur, ActionDuJournal action, string detail, string avant, string apres) =>
        journal.Noter(auteur, action, detail.Length > LongueurMaximaleDuDetail ? detail[..LongueurMaximaleDuDetail] : detail,
            Couper(avant), Couper(apres));

    private static string Couper(string texte) => texte.Length > 100 ? texte[..100] : texte;

    /// <summary>« Mathématiques, Groupe Samedi 10h → 12h · Karim Ben Salah ».</summary>
    private static string Decrire(ContexteDuGroupe groupe) => $"{groupe.Matiere}, {groupe.Nom} · {groupe.Professeur}";

    /// <summary>« samedi 16/10/2027, 10h → 12h ».</summary>
    private static string Quand(Seance seance) => $"{Jour(seance.Date)}, {Heure(seance.Debut)} → {Heure(seance.Fin)}";

    /// <summary>« samedi 16/10/2027 ».</summary>
    private static string Jour(DateOnly date) => $"{Jours.De(date.DayOfWeek).ToString().ToLowerInvariant()} {SaisieDuGroupe.EcrireUneDate(date)}";

    private static string Heure(TimeOnly heure) => CalendrierDuGroupe.Heure(heure);

    private static bool LireUneHeure(string? saisie, out TimeOnly heure) =>
        TimeOnly.TryParseExact(saisie?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out heure);

    private static IssueDuCalendrier Refus(int statut, string message) => new(null, statut, message);

    /// <summary>« mathématiques », mais « SVT » : la matière dans une phrase.</summary>
    private sealed record ContexteDuGroupe(string Nom, string Matiere, string Professeur, string Centre)
    {
        public string MatiereEnMinuscule => Matiere.Length > 1 && char.IsLower(Matiere[1]) ? char.ToLowerInvariant(Matiere[0]) + Matiere[1..] : Matiere;
    }
}

/// <summary>Ce qu'une action sur le calendrier a donné : le message prêt et le statut HTTP, ou un refus.</summary>
public sealed record IssueDuCalendrier(ChangementDuCalendrier? Changement, int Statut, string? Message);
