using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;
using SchoolHub.Socle.Messages;

namespace SchoolHub.Modules.Inscriptions;

/// <summary>
/// ELV-03 : les inscriptions du centre courant. Le filtre automatique ne montre que celles du centre : un élève ou un
/// groupe d'un autre centre est « introuvable ». L'inscription dit ce que l'élève paiera à sa première séance, prévient
/// d'un groupe complet sans bloquer, et prépare le message WhatsApp de confirmation.
/// </summary>
public sealed class InscriptionsDuCentre(SchoolHubDbContext contexte, TimeProvider temps, CentreCourant centreCourant, JournalDuCentre journal)
{
    /// <summary>La taille de la colonne d'une raison : prix spécial, dispense.</summary>
    public const int LongueurMaximaleDUneRaison = 200;

    /// <summary>La taille de la colonne du détail d'une ligne du journal.</summary>
    private const int LongueurMaximaleDuDetail = 500;

    private const string InscriptionIntrouvable = "Inscription introuvable";
    private const string PrixSpecialPasValable = "Le prix spécial doit être positif ou nul, au millime près";
    private const string RaisonDuPrixSpecialObligatoire = "Écrivez la raison du prix spécial";
    private const string RaisonTropLongue = "La raison fait au plus 200 caractères";
    private const string SansPrixSpecial = "Cette inscription n'a pas de prix spécial";
    private const string AucuneAssurance = "Aucune assurance cette année";
    private const string AssuranceDejaPayee = "L'assurance est déjà payée";
    private const string AssuranceDejaDispensee = "L'assurance est déjà dispensée";
    private const string RaisonDeLaDispenseObligatoire = "Écrivez la raison de la dispense";
    private const string SommeIntrouvable = "Somme introuvable";
    private const string SommeDejaPayee = "Cette somme est déjà payée";
    private const string SommeDApres = "Cette somme ne date pas d'avant SchoolHub";
    private const string SeuleUneFormationEnTranches = "Seule une Formation payée en tranches regroupe son reste";
    private const string ResteDejaRegroupe = "Le reste est déjà regroupé";
    private const string AucuneTrancheAVenir = "Il ne reste aucune tranche à venir";
    private const string DejaArretee = "Cette inscription est déjà arrêtée";
    private const string ChoisirLaDateDeLArret = "Choisissez la date de l'arrêt";
    private const string ChoisirLeNouveauGroupe = "Choisissez le nouveau groupe";
    private const string DejaDansCeGroupe = "L'élève est déjà dans ce groupe";
    private const string PasLeMemeType = "Le nouveau groupe doit être du même type";
    private const string ChangementTropTot = "Le changement vient avant sa première séance dans l'ancien groupe";
    private const string PrixSpecialQuiNeSuitPas = "Le prix spécial de l'ancien groupe ne suit pas : prévenez le propriétaire.";

    public const string ChoisirLEleve = "Choisissez l'élève";
    public const string EleveIntrouvable = "Élève introuvable";
    private const string ChoisirLEleveOuLeGroupe = "Choisissez l'élève ou le groupe";
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
        var eleve = await EleveVerrouilleAsync(eleveId);
        if (eleve is null)
            return Refus(StatusCodes.Status404NotFound, EleveIntrouvable);

        if (demande.GroupeId is not { } groupeId)
            return Refus(StatusCodes.Status400BadRequest, ChoisirLeGroupe);
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == groupeId);
        if (groupe is null)
            return Refus(StatusCodes.Status404NotFound, GroupeIntrouvable);
        var (mode, seances, refus) = await VerifierLeGroupeAsync(eleveId, groupe, demande.Mode, demande.DebutLe);
        if (refus is not null)
            return refus;

        var debutLe = demande.DebutLe!.Value;
        var avertissement = await AvertissementDeCapaciteAsync(groupe);
        var inscription = new Inscription(eleveId, groupe.Id, mode, debutLe, temps.GetUtcNow());
        contexte.Inscriptions.Add(inscription);
        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId);
        var assurance = await AssuranceALArriveeAsync(eleveId, groupe, centre, debutLe);
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        return new IssueDInscription(await EnregistrementAsync(eleve, inscription, groupe, seances, assurance, avertissement, centre),
            StatusCodes.Status201Created, null);
    }

    /// <summary>
    /// ELV-09 : passe l'élève dans un autre groupe du même type, à sa première séance dans le nouveau groupe. L'ancienne
    /// inscription s'arrête la veille ; la valeur de ses séances payées et pas suivies devient le report de la nouvelle.
    /// Le changement va au journal.
    /// </summary>
    public async Task<IssueDInscription> ChangerDeGroupeAsync(Compte auteur, Guid id, DemandeDeChangement demande)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var ancienne = await InscriptionVerrouilleeAsync(id);
        if (ancienne is null)
            return Refus(StatusCodes.Status404NotFound, InscriptionIntrouvable);
        if (ancienne.ArreteLe is not null)
            return Refus(StatusCodes.Status409Conflict, DejaArretee);
        if (demande.GroupeId is not { } groupeId)
            return Refus(StatusCodes.Status400BadRequest, ChoisirLeNouveauGroupe);
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == groupeId);
        if (groupe is null)
            return Refus(StatusCodes.Status404NotFound, GroupeIntrouvable);
        if (groupe.Id == ancienne.GroupeId)
            return Refus(StatusCodes.Status409Conflict, DejaDansCeGroupe);
        var ancienGroupe = await contexte.Groupes.SingleAsync(g => g.Id == ancienne.GroupeId);
        if (groupe.Type != ancienGroupe.Type)
            return Refus(StatusCodes.Status409Conflict, PasLeMemeType);
        var (mode, seances, refus) = await VerifierLeGroupeAsync(ancienne.EleveId, groupe, demande.Mode, demande.DebutLe);
        if (refus is not null)
            return refus;
        var debutLe = demande.DebutLe!.Value;
        if (debutLe < ancienne.DebutLe)
            return Refus(StatusCodes.Status400BadRequest, ChangementTropTot);

        var echeance = (await EcheancesAsync([ancienne]))[0];
        var report = Echeancier.ValeurNonSuivie(echeance.Calendrier, echeance.Conditions, debutLe);
        var eleve = (await contexte.Eleves.SingleAsync(e => e.Id == ancienne.EleveId))!;
        var matieres = await contexte.Matieres.Where(m => m.Id == groupe.MatiereId || m.Id == ancienGroupe.MatiereId).ToDictionaryAsync(m => m.Id, m => m.Nom);
        var detail = $"{eleve.Prenom} {eleve.Nom} · {ancienGroupe.Nom} ({matieres[ancienGroupe.MatiereId]}) → {groupe.Nom} ({matieres[groupe.MatiereId]})"
            + (report > 0 ? $" · report de {SaisieDuGroupe.EcrireUnMontant(report)} TND" : "");
        journal.Noter(auteur, ActionDuJournal.ChangementDeGroupe, Couper(detail), CouperA100(ancienGroupe.Nom),
            CouperA100($"{groupe.Nom}, depuis le {SaisieDuGroupe.EcrireUneDate(debutLe)}"));

        var avertissements = new[]
        {
            await AvertissementDeCapaciteAsync(groupe),
            ancienne.PrixSpecial is null ? null : PrixSpecialQuiNeSuitPas,
        }.OfType<string>().ToList();
        var avertissement = avertissements.Count == 0 ? null : string.Join(" ", avertissements);
        ancienne.Arreter(debutLe.AddDays(-1));
        var inscription = new Inscription(ancienne.EleveId, groupe.Id, mode, debutLe, temps.GetUtcNow(), ancienne.Id);
        contexte.Inscriptions.Add(inscription);
        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId);
        var assurance = await AssuranceALArriveeAsync(ancienne.EleveId, groupe, centre, debutLe);
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        return new IssueDInscription(await EnregistrementAsync(eleve, inscription, groupe, seances, assurance, avertissement, centre),
            StatusCodes.Status201Created, null);
    }

    /// <summary>
    /// Les vérifications d'une inscription dans ce groupe, dans l'ordre de la spec : fermé, terminé, déjà inscrit, le mode,
    /// la première séance. Rend le mode et les séances du groupe, ou le premier refus.
    /// </summary>
    private async Task<(ModeDeTarification Mode, List<Seance> Seances, IssueDInscription? Refus)> VerifierLeGroupeAsync(
        Guid eleveId, Groupe groupe, string? modeSaisi, DateOnly? debutLe)
    {
        if (groupe.FermeLe is not null)
            return (default, [], Refus(StatusCodes.Status409Conflict, GroupeFerme));
        var seances = await SeancesAsync(groupe.Id);
        if (CalendrierDuGroupe.EstTermine(groupe.Type, seances.LastOrDefault()?.Date, DateDeTunis.Aujourdhui(temps)))
            return (default, [], Refus(StatusCodes.Status409Conflict, GroupeTermine));
        // Une inscription pas arrêtée, ou arrêtée à la première séance demandée ou après : l'élève y est encore.
        if (await contexte.Inscriptions.AnyAsync(i => i.EleveId == eleveId && i.GroupeId == groupe.Id
                && (i.ArreteLe == null || (debutLe != null && i.ArreteLe >= debutLe))))
            return (default, [], Refus(StatusCodes.Status409Conflict, DejaInscrit));

        var modes = Echeancier.ModesPossibles(groupe.Type, groupe.Tranches.Count > 0);
        ModeDeTarification mode;
        if (string.IsNullOrWhiteSpace(modeSaisi))
        {
            if (modes.Count > 1)
                return (default, [], Refus(StatusCodes.Status400BadRequest, ChoisirLeMode));
            mode = modes[0];
        }
        else if (!Enum.GetNames<ModeDeTarification>().Contains(modeSaisi.Trim())
                 || !modes.Contains(mode = Enum.Parse<ModeDeTarification>(modeSaisi.Trim())))
        {
            return (default, [], Refus(StatusCodes.Status400BadRequest, ModeQuiNeConvientPas));
        }

        if (debutLe is not { } date)
            return (default, [], Refus(StatusCodes.Status400BadRequest, ChoisirLaPremiereSeance));
        if (seances.All(s => s.Date != date))
            return (default, [], Refus(StatusCodes.Status400BadRequest, PasDeSeanceCeJourLa));
        return (mode, seances, null);
    }

    /// <summary>« Ce groupe a atteint sa capacité… » si le groupe compte déjà autant d'élèves inscrits que sa capacité.</summary>
    private async Task<string?> AvertissementDeCapaciteAsync(Groupe groupe)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        return groupe.Capacite is { } capacite
            && await contexte.Inscriptions.CountAsync(i => i.GroupeId == groupe.Id && (i.ArreteLe == null || i.ArreteLe >= aujourdhui)) >= capacite
            ? CapaciteAtteinte
            : null;
    }

    /// <summary>
    /// La réponse d'une inscription ou d'un changement : l'inscription, ce qu'il paiera à sa première séance (report déduit,
    /// assurance comprise), l'avertissement, et le message WhatsApp pour ses destinataires.
    /// </summary>
    private async Task<EnregistrementDeLInscription> EnregistrementAsync(
        Eleve eleve, Inscription inscription, Groupe groupe, List<Seance> seances, SommeALArrivee? assurance, string? avertissement, Centre centre)
    {
        var decrite = (await DecrireAsync([inscription]))[0];
        var echeance = (await EcheancesAsync([inscription]))[0];
        var premiere = Echeancier.PremiereSeance(echeance.Calendrier, inscription.DebutLe);
        var aPayer = echeance.Sommes
            .Where(s => s.Seance == premiere)
            .Select(s => new SommeALArrivee(s.Libelle, s.Montant))
            .ToList();
        if (assurance is not null)
            aPayer.Add(assurance);

        var texte = TexteDeConfirmation(eleve, decrite, seances.First(s => s.Date == inscription.DebutLe), centre);
        var messages = DestinatairesDeLEleve.De(eleve)
            .Select(d => new MessageWhatsApp(d.Nom, TelephoneTunisien.Grouper(d.Chiffres), WhatsApp.Lien(d.Chiffres, texte)))
            .ToList();
        return new EnregistrementDeLInscription(decrite, aPayer, avertissement, texte, messages);
    }

    /// <summary>L'avant et l'après d'une ligne du journal tiennent en 100 caractères.</summary>
    private static string CouperA100(string texte) => texte.Length > 100 ? texte[..100] : texte;

    /// <summary>
    /// Les inscriptions d'un élève, par nom du groupe, ou celles d'un groupe, par nom puis prénom de l'élève ; un élève ou
    /// un groupe absent de ce centre est introuvable.
    /// </summary>
    public async Task<IssueDesInscriptions> ListerAsync(Guid? eleveId, Guid? groupeId)
    {
        if (eleveId is { } eleve)
        {
            if (!await contexte.Eleves.AnyAsync(e => e.Id == eleve))
                return new IssueDesInscriptions(null, StatusCodes.Status404NotFound, EleveIntrouvable);
            var deLEleve = await DecrireAsync(await contexte.Inscriptions.Where(i => i.EleveId == eleve).ToListAsync());
            return new IssueDesInscriptions(
                deLEleve.OrderBy(i => i.Groupe.Nom).ThenBy(i => i.Groupe.Matiere).ThenBy(i => i.Id).ToList(), StatusCodes.Status200OK, null);
        }

        if (groupeId is not { } groupe)
            return new IssueDesInscriptions(null, StatusCodes.Status400BadRequest, ChoisirLEleveOuLeGroupe);
        if (!await contexte.Groupes.AnyAsync(g => g.Id == groupe))
            return new IssueDesInscriptions(null, StatusCodes.Status404NotFound, GroupeIntrouvable);
        var duGroupe = await DecrireAsync(await contexte.Inscriptions.Where(i => i.GroupeId == groupe).ToListAsync());
        return new IssueDesInscriptions(
            duGroupe.OrderBy(i => i.Eleve.Nom).ThenBy(i => i.Eleve.Prenom).ThenBy(i => i.Id).ToList(), StatusCodes.Status200OK, null);
    }

    /// <summary>
    /// Les groupes où l'on peut inscrire l'élève : ouverts et pas terminés, ceux de son niveau d'abord, puis par matière
    /// et par nom. Un groupe où il est déjà inscrit est marqué. <c>null</c> si l'élève n'est pas dans ce centre.
    /// </summary>
    public async Task<(List<GroupePossible>? Groupes, string? Introuvable)> GroupesPossiblesAsync(Guid eleveId, Guid? depuis = null)
    {
        var eleve = await contexte.Eleves.SingleOrDefaultAsync(e => e.Id == eleveId);
        if (eleve is null)
            return (null, EleveIntrouvable);

        var groupes = await contexte.Groupes.Where(g => g.FermeLe == null).ToListAsync();
        // ELV-09 : pour un changement, seulement les groupes du même type que celui de départ, sans lui.
        if (depuis is { } inscriptionId)
        {
            var ancienne = await contexte.Inscriptions.SingleOrDefaultAsync(i => i.Id == inscriptionId && i.EleveId == eleveId);
            if (ancienne is null)
                return (null, InscriptionIntrouvable);
            var type = (await contexte.Groupes.SingleAsync(g => g.Id == ancienne.GroupeId)).Type;
            groupes = groupes.Where(g => g.Type == type && g.Id != ancienne.GroupeId).ToList();
        }
        var dernieresSeances = await contexte.Seances
            .GroupBy(s => s.GroupeId)
            .Select(g => new { GroupeId = g.Key, Derniere = g.Max(s => s.Date) })
            .ToDictionaryAsync(g => g.GroupeId, g => g.Derniere);
        var aujourdhuiPourLesInscrits = DateDeTunis.Aujourdhui(temps);
        var inscrits = await contexte.Inscriptions
            .Where(i => i.ArreteLe == null || i.ArreteLe >= aujourdhuiPourLesInscrits)
            .GroupBy(i => i.GroupeId)
            .Select(g => new { GroupeId = g.Key, Nombre = g.Count() })
            .ToDictionaryAsync(g => g.GroupeId, g => g.Nombre);
        var dejaInscrit = (await contexte.Inscriptions.Where(i => i.EleveId == eleveId && i.ArreteLe == null).Select(i => i.GroupeId).ToListAsync())
            .ToHashSet();
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);
        var niveaux = await contexte.Niveaux.ToDictionaryAsync(n => n.Id, n => n.Nom);
        var professeurs = await contexte.Professeurs.ToDictionaryAsync(p => p.Id, p => $"{p.Prenom} {p.Nom}");
        var aujourdhui = DateDeTunis.Aujourdhui(temps);

        return (groupes
            .Where(g => !CalendrierDuGroupe.EstTermine(g.Type, dernieresSeances.TryGetValue(g.Id, out var derniere) ? derniere : null, aujourdhui))
            .Select(g => new GroupePossible(
                g.Id,
                g.Nom,
                g.Type.ToString(),
                matieres[g.MatiereId],
                professeurs[g.ProfesseurId],
                g.NiveauId is { } niveauId ? niveaux[niveauId] : null,
                g.NiveauId == eleve.NiveauId,
                inscrits.GetValueOrDefault(g.Id),
                g.Capacite,
                dejaInscrit.Contains(g.Id)))
            .OrderByDescending(g => g.DuNiveauDeLEleve)
            .ThenBy(g => g.Matiere)
            .ThenBy(g => g.Nom)
            .ThenBy(g => g.Id)
            .ToList(), null);
    }

    /// <summary>
    /// L'assurance de l'élève pour l'année scolaire du centre (la plus récente). Elle est due à la plus ancienne première
    /// séance de ses inscriptions en Étude de cette année. <c>Trouve</c> est faux si l'élève n'est pas dans ce centre ;
    /// <c>Assurance</c> vaut <c>null</c> quand elle est sans objet.
    /// </summary>
    public async Task<(bool Trouve, AssuranceDeLEleve? Assurance)> AssuranceAsync(Guid eleveId)
    {
        if (!await contexte.Eleves.AnyAsync(e => e.Id == eleveId))
            return (false, null);
        return (true, (await AssuranceEnCoursAsync(eleveId))?.Decrite);
    }

    /// <summary>
    /// ELV-04 : ce que l'élève doit à ce jour, groupe par groupe, puis l'assurance : chaque somme dont la séance a eu lieu
    /// ou a lieu aujourd'hui, pas réglée, et de plus de 0 TND (une somme à 0 TND compte comme payée). <c>null</c> si
    /// l'élève n'est pas dans ce centre.
    /// </summary>
    public async Task<APayer?> APayerAsync(Guid eleveId)
    {
        if (!await contexte.Eleves.AnyAsync(e => e.Id == eleveId))
            return null;
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var echeances = await EcheancesAsync(await contexte.Inscriptions.Where(i => i.EleveId == eleveId).ToListAsync());
        var matiereIds = echeances.Select(e => e.Groupe.MatiereId).Distinct().ToList();
        var matieres = await contexte.Matieres.Where(m => matiereIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Nom);

        var lignes = echeances
            .OrderBy(e => e.Groupe.Nom).ThenBy(e => matieres[e.Groupe.MatiereId]).ThenBy(e => e.Inscription.Id)
            .SelectMany(e => e.Sommes
                .Where(somme => somme.Date <= aujourdhui && !somme.Reglee && somme.Montant > 0)
                .Select(somme => new LigneAPayer(
                    e.Inscription.Id, $"{matieres[e.Groupe.MatiereId]} · {e.Groupe.Nom}", somme.Libelle, somme.Cle, somme.Montant, somme.Date)))
            .ToList();
        if (await AssuranceEnCoursAsync(eleveId) is { Decrite: { Etat: EtatAPayer, DueLe: { } dueLe } assurance } && dueLe <= aujourdhui
            && assurance.Montant > 0)
            lignes.Add(new LigneAPayer(null, "Assurance", $"Assurance {assurance.Annee}", Reglement.CleDeLAssurance, assurance.Montant, dueLe));
        return new APayer(lignes, lignes.Sum(l => l.Montant));
    }

    /// <summary>
    /// ELV-05 : le propriétaire accorde ou change le prix spécial d'une inscription, avec sa raison. Il vaut pour ce qui
    /// n'est pas encore payé. L'action va au journal, avec le prix d'avant et celui d'après.
    /// </summary>
    public async Task<IssueSurUneInscription> AccorderUnPrixSpecialAsync(Compte auteur, Guid id, PrixSpecialSaisi saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var inscription = await InscriptionVerrouilleeAsync(id);
        if (inscription is null)
            return RefusSurUneInscription(StatusCodes.Status404NotFound, InscriptionIntrouvable);
        if (saisie.Montant is not { } montant || montant < 0 || montant >= 1_000_000 || decimal.Round(montant, 3) != montant)
            return RefusSurUneInscription(StatusCodes.Status400BadRequest, PrixSpecialPasValable);
        var raison = saisie.Raison?.Trim() ?? "";
        if (raison.Length == 0)
            return RefusSurUneInscription(StatusCodes.Status400BadRequest, RaisonDuPrixSpecialObligatoire);
        if (raison.Length > LongueurMaximaleDUneRaison)
            return RefusSurUneInscription(StatusCodes.Status400BadRequest, RaisonTropLongue);

        var avant = (await DecrireAsync([inscription]))[0];
        journal.Noter(auteur, ActionDuJournal.PrixSpecialAccorde, Couper($"{await QuiEtOuAsync(inscription)} · {raison}"),
            EcrireLePrix(avant.Groupe.Type, avant.PrixSpecial ?? avant.PrixNormal), EcrireLePrix(avant.Groupe.Type, montant));
        inscription.AccorderUnPrixSpecial(montant, raison);
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueSurUneInscription((await DecrireAsync([inscription]))[0], StatusCodes.Status200OK, null);
    }

    /// <summary>ELV-05 : le propriétaire retire le prix spécial ; le prix normal revient pour ce qui n'est pas payé.</summary>
    public async Task<IssueSurUneInscription> RetirerLePrixSpecialAsync(Compte auteur, Guid id)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var inscription = await InscriptionVerrouilleeAsync(id);
        if (inscription is null)
            return RefusSurUneInscription(StatusCodes.Status404NotFound, InscriptionIntrouvable);
        if (inscription.PrixSpecial is not { } special)
            return RefusSurUneInscription(StatusCodes.Status409Conflict, SansPrixSpecial);

        var avant = (await DecrireAsync([inscription]))[0];
        journal.Noter(auteur, ActionDuJournal.PrixSpecialRetire, Couper(await QuiEtOuAsync(inscription)),
            EcrireLePrix(avant.Groupe.Type, special), EcrireLePrix(avant.Groupe.Type, avant.PrixNormal));
        inscription.RetirerLePrixSpecial();
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueSurUneInscription((await DecrireAsync([inscription]))[0], StatusCodes.Status200OK, null);
    }

    /// <summary>
    /// ELV-06 : le propriétaire dispense l'élève de l'assurance de l'année, avec une raison ; elle n'est plus due.
    /// L'année suivante a sa propre assurance. La dispense va au journal.
    /// </summary>
    public async Task<IssueSurUneAssurance> DispenserAsync(Compte auteur, Guid eleveId, RaisonSaisie saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var eleve = await EleveVerrouilleAsync(eleveId);
        if (eleve is null)
            return new IssueSurUneAssurance(null, StatusCodes.Status404NotFound, EleveIntrouvable);
        if (await AssuranceEnCoursAsync(eleveId) is not { } enCours)
            return new IssueSurUneAssurance(null, StatusCodes.Status404NotFound, AucuneAssurance);
        if (enCours.Decrite.Etat == EtatDispensee)
            return new IssueSurUneAssurance(null, StatusCodes.Status409Conflict, AssuranceDejaDispensee);
        if (enCours.Decrite.Etat != EtatAPayer)
            return new IssueSurUneAssurance(null, StatusCodes.Status409Conflict, AssuranceDejaPayee);
        var raison = saisie.Raison?.Trim() ?? "";
        if (raison.Length == 0)
            return new IssueSurUneAssurance(null, StatusCodes.Status400BadRequest, RaisonDeLaDispenseObligatoire);
        if (raison.Length > LongueurMaximaleDUneRaison)
            return new IssueSurUneAssurance(null, StatusCodes.Status400BadRequest, RaisonTropLongue);

        journal.Noter(auteur, ActionDuJournal.AssuranceDispensee,
            Couper($"{eleve.Prenom} {eleve.Nom} · Assurance {enCours.Decrite.Annee} · {raison}"), "À payer", "Dispensée");
        enCours.Assurance.Dispenser(DateDeTunis.Aujourdhui(temps), raison);
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueSurUneAssurance((await AssuranceEnCoursAsync(eleveId))!.Value.Decrite, StatusCodes.Status200OK, null);
    }

    /// <summary>
    /// ELV-07 : une somme d'une Révision ou d'une Formation déjà en cours, due à la première séance du groupe dans
    /// SchoolHub, a été payée avant : elle devient réglée, sans reçu, hors de la caisse. L'action va au journal.
    /// </summary>
    public async Task<IssueSurUneInscription> PayeAvantSchoolHubAsync(Compte auteur, Guid id, CleSaisie saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var inscription = await InscriptionVerrouilleeAsync(id);
        if (inscription is null)
            return RefusSurUneInscription(StatusCodes.Status404NotFound, InscriptionIntrouvable);
        var echeance = (await EcheancesAsync([inscription]))[0];
        var somme = echeance.Sommes.FirstOrDefault(s => s.Cle == saisie.Cle?.Trim());
        if (somme is null)
            return RefusSurUneInscription(StatusCodes.Status404NotFound, SommeIntrouvable);
        if (somme.Reglee || somme.Montant <= 0)
            return RefusSurUneInscription(StatusCodes.Status409Conflict, SommeDejaPayee);
        if (!PayableAvantSchoolHub(echeance, somme))
            return RefusSurUneInscription(StatusCodes.Status409Conflict, SommeDApres);

        journal.Noter(auteur, ActionDuJournal.PayeAvantSchoolHub, Couper($"{await QuiEtOuAsync(inscription)} · {somme.Libelle}"),
            "À payer", $"Payé avant SchoolHub, {SaisieDuGroupe.EcrireUnMontant(somme.Montant)} TND");
        contexte.Reglements.Add(Reglement.DUneSomme(inscription.Id, somme.Cle, somme.Montant, OrigineDuReglement.AvantSchoolHub, temps.GetUtcNow(), auteur.Id,
            somme.ReportDeduit));
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueSurUneInscription((await DecrireAsync([inscription]))[0], StatusCodes.Status200OK, null);
    }

    /// <summary>ELV-07 : l'assurance de l'année a été payée avant SchoolHub, tant qu'elle n'est ni réglée ni dispensée.</summary>
    public async Task<IssueSurUneAssurance> AssurancePayeeAvantSchoolHubAsync(Compte auteur, Guid eleveId)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var eleve = await EleveVerrouilleAsync(eleveId);
        if (eleve is null)
            return new IssueSurUneAssurance(null, StatusCodes.Status404NotFound, EleveIntrouvable);
        if (await AssuranceEnCoursAsync(eleveId) is not { } enCours)
            return new IssueSurUneAssurance(null, StatusCodes.Status404NotFound, AucuneAssurance);
        if (enCours.Decrite.Etat == EtatDispensee)
            return new IssueSurUneAssurance(null, StatusCodes.Status409Conflict, AssuranceDejaDispensee);
        if (enCours.Decrite.Etat != EtatAPayer)
            return new IssueSurUneAssurance(null, StatusCodes.Status409Conflict, AssuranceDejaPayee);

        journal.Noter(auteur, ActionDuJournal.PayeAvantSchoolHub, Couper($"{eleve.Prenom} {eleve.Nom} · Assurance {enCours.Decrite.Annee}"),
            "À payer", $"Payé avant SchoolHub, {SaisieDuGroupe.EcrireUnMontant(enCours.Assurance.Montant)} TND");
        contexte.Reglements.Add(Reglement.DeLAssurance(enCours.Assurance.Id, enCours.Assurance.Montant, OrigineDuReglement.AvantSchoolHub,
            temps.GetUtcNow(), auteur.Id));
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueSurUneAssurance((await AssuranceEnCoursAsync(eleveId))!.Value.Decrite, StatusCodes.Status200OK, null);
    }

    /// <summary>
    /// ELV-10 : « Payer tout le reste » d'une Formation en tranches : ses tranches pas réglées à venir deviennent une seule
    /// somme, due aujourd'hui. Pas de réduction.
    /// </summary>
    public async Task<IssueSurUneInscription> RegrouperLeResteAsync(Guid id)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var inscription = await InscriptionVerrouilleeAsync(id);
        if (inscription is null)
            return RefusSurUneInscription(StatusCodes.Status404NotFound, InscriptionIntrouvable);
        if (inscription.Mode != ModeDeTarification.EnTranches)
            return RefusSurUneInscription(StatusCodes.Status409Conflict, SeuleUneFormationEnTranches);
        if (inscription.ResteRegroupeLe is not null)
            return RefusSurUneInscription(StatusCodes.Status409Conflict, ResteDejaRegroupe);
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        if (!ATrancheAVenir((await EcheancesAsync([inscription]))[0], aujourdhui))
            return RefusSurUneInscription(StatusCodes.Status409Conflict, AucuneTrancheAVenir);

        inscription.RegrouperLeReste(aujourdhui);
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueSurUneInscription((await DecrireAsync([inscription]))[0], StatusCodes.Status200OK, null);
    }

    /// <summary>
    /// ELV-07 : une somme pas réglée, de plus de 0 TND, d'une Révision ou d'une Formation déjà en cours (première séance dans
    /// SchoolHub après la 1), qui était due avant SchoolHub : la totalité ou le paiement en une fois d'un élève arrivé avant
    /// (sa première séance est celle du groupe dans SchoolHub), ou une tranche dont la séance vient avant. Une séance à la
    /// séance, ou une tranche due dans SchoolHub, se paie à la caisse.
    /// </summary>
    private static bool PayableAvantSchoolHub(Echeance echeance, SommeDue somme)
    {
        var groupe = echeance.Groupe;
        if (groupe.Type == TypeDeGroupe.Etude || groupe.PremiereSeance <= 1 || somme.Reglee || somme.Montant <= 0)
            return false;
        if (somme.Cle is "Totalité" or "Une fois")
            return somme.Seance == groupe.PremiereSeance;
        if (!somme.Cle.StartsWith("Tranche ", StringComparison.Ordinal) || !int.TryParse(somme.Cle["Tranche ".Length..], out var rang))
            return false;
        var tranches = groupe.Tranches.OrderBy(t => t.Seance).ToList();
        return rang >= 1 && rang <= tranches.Count && tranches[rang - 1].Seance < groupe.PremiereSeance;
    }

    /// <summary>Une Formation en tranches pas encore regroupée, qui a une tranche à regrouper aujourd'hui, de plus de 0 TND.</summary>
    private static bool ATrancheAVenir(Echeance echeance, DateOnly aujourdhui) =>
        Echeancier.PeutRegrouperLeReste(echeance.Calendrier, echeance.Conditions, aujourdhui);

    /// <summary>
    /// ELV-08 : arrête l'inscription à une date, son dernier jour compté : rien n'est dû après, ce qui était dû avant reste
    /// dû, et ce qui est payé le reste, sans remboursement. Une date avant sa première séance la rend sans effet. L'arrêt
    /// va au journal.
    /// </summary>
    public async Task<IssueSurUneInscription> ArreterAsync(Compte auteur, Guid id, DateSaisie saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var inscription = await InscriptionVerrouilleeAsync(id);
        if (inscription is null)
            return RefusSurUneInscription(StatusCodes.Status404NotFound, InscriptionIntrouvable);
        if (inscription.ArreteLe is not null)
            return RefusSurUneInscription(StatusCodes.Status409Conflict, DejaArretee);
        if (saisie.Date is not { } date)
            return RefusSurUneInscription(StatusCodes.Status400BadRequest, ChoisirLaDateDeLArret);

        journal.Noter(auteur, ActionDuJournal.InscriptionArretee, Couper(await QuiEtOuAsync(inscription)),
            $"Depuis le {SaisieDuGroupe.EcrireUneDate(inscription.DebutLe)}", $"Arrêtée le {SaisieDuGroupe.EcrireUneDate(date)}");
        inscription.Arreter(date);
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueSurUneInscription((await DecrireAsync([inscription]))[0], StatusCodes.Status200OK, null);
    }

    private const string EtatAPayer = "APayer";
    private const string EtatPayeeAvantSchoolHub = "PayeeAvantSchoolHub";
    private const string EtatDispensee = "Dispensee";

    /// <summary>
    /// L'assurance de l'élève pour l'année scolaire du centre (la plus récente), et ce que la fiche en montre : due à la
    /// plus ancienne première séance de ses inscriptions en Étude de cette année ; à payer, payée avant SchoolHub, ou
    /// dispensée. <c>null</c> quand elle est sans objet.
    /// </summary>
    private async Task<(AssuranceDeLAnnee Assurance, AssuranceDeLEleve Decrite)?> AssuranceEnCoursAsync(Guid eleveId)
    {
        var annee = await contexte.AnneesScolaires.OrderByDescending(a => a.Debut).FirstOrDefaultAsync();
        if (annee is null)
            return null;
        var assurance = await contexte.Assurances.SingleOrDefaultAsync(a => a.EleveId == eleveId && a.AnneeScolaireId == annee.Id);
        if (assurance is null)
            return null;

        var reglee = await contexte.Reglements.AnyAsync(r => r.AssuranceId == assurance.Id);
        var etat = assurance.DispenseeLe is not null ? EtatDispensee : reglee ? EtatPayeeAvantSchoolHub : EtatAPayer;
        var dueLe = await PremiereSeanceDEtudeAsync(eleveId, annee.Id);
        // Plus d'inscription en Étude commencée : une assurance ni payée ni dispensée est sans objet.
        if (dueLe is null && etat == EtatAPayer)
            return null;
        return (assurance, new AssuranceDeLEleve(AssuranceDeLAnnee.Annee(annee), assurance.Montant, dueLe, etat, assurance.RaisonDeLaDispense));
    }

    /// <summary>« 40,000 TND par Mois » pour une Étude, « 100,000 TND » sinon : le prix comme le journal l'écrit.</summary>
    private static string EcrireLePrix(string type, decimal montant) =>
        type == nameof(TypeDeGroupe.Etude) ? $"{SaisieDuGroupe.EcrireUnMontant(montant)} TND par Mois" : $"{SaisieDuGroupe.EcrireUnMontant(montant)} TND";

    /// <summary>« Adem Miled · Groupe Samedi 10h → 12h (Mathématiques) » : l'élève et le groupe d'une ligne du journal.</summary>
    private async Task<string> QuiEtOuAsync(Inscription inscription)
    {
        var eleve = await contexte.Eleves.SingleAsync(e => e.Id == inscription.EleveId);
        var groupe = await contexte.Groupes.SingleAsync(g => g.Id == inscription.GroupeId);
        var matiere = await contexte.Matieres.SingleAsync(m => m.Id == groupe.MatiereId);
        return $"{eleve.Prenom} {eleve.Nom} · {groupe.Nom} ({matiere.Nom})";
    }

    /// <summary>Un nom, un groupe et une raison à leur plus long dépasseraient la colonne du détail : il est coupé.</summary>
    private static string Couper(string detail) => detail.Length > LongueurMaximaleDuDetail ? detail[..LongueurMaximaleDuDetail] : detail;

    /// <summary>
    /// L'inscription de ce centre, la ligne de son élève verrouillée jusqu'à la fin de la transaction : deux actions sur le
    /// même élève, venues de deux onglets, passent l'une après l'autre.
    /// </summary>
    private async Task<Inscription?> InscriptionVerrouilleeAsync(Guid id)
    {
        var inscription = await contexte.Inscriptions.SingleOrDefaultAsync(i => i.Id == id);
        if (inscription is null)
            return null;
        await EleveVerrouilleAsync(inscription.EleveId);
        // Relue après le verrou : une action passée entre-temps est vue.
        await contexte.Entry(inscription).ReloadAsync();
        return inscription;
    }

    /// <summary>L'élève de ce centre, sa ligne verrouillée jusqu'à la fin de la transaction ; <c>null</c> s'il n'y est pas.</summary>
    private async Task<Eleve?> EleveVerrouilleAsync(Guid eleveId)
    {
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM eleves WHERE id = {eleveId} AND centre_id = {CentreId} FOR NO KEY UPDATE");
        return await contexte.Eleves.SingleOrDefaultAsync(e => e.Id == eleveId);
    }

    private static IssueSurUneInscription RefusSurUneInscription(int statut, string message) => new(null, statut, message);

    /// <summary>
    /// L'assurance à payer à la première séance de cette inscription en Étude. La première inscription en Étude d'une
    /// année crée l'assurance, au montant du centre ce jour-là (un centre sans montant n'en réclame pas). Une inscription
    /// suivante qui commence plus tôt que les autres l'avance à sa première séance : elle s'y annonce aussi. Doit être
    /// appelée avant l'enregistrement de l'inscription.
    /// </summary>
    private async Task<SommeALArrivee?> AssuranceALArriveeAsync(Guid eleveId, Groupe groupe, Centre centre, DateOnly debutLe)
    {
        if (groupe.Type != TypeDeGroupe.Etude || groupe.AnneeScolaireId is not { } anneeId)
            return null;
        var annee = await contexte.AnneesScolaires.SingleAsync(a => a.Id == anneeId);
        var existante = await contexte.Assurances.SingleOrDefaultAsync(a => a.EleveId == eleveId && a.AnneeScolaireId == anneeId);
        if (existante is null)
        {
            // Un centre sans montant, ou à 0 TND, ne réclame pas d'assurance.
            if (centre.MontantDeLAssurance is not { } montant || montant <= 0)
                return null;
            contexte.Assurances.Add(new AssuranceDeLAnnee(eleveId, anneeId, montant));
            return new SommeALArrivee(AssuranceDeLAnnee.Libelle(annee), montant);
        }

        var dueLe = await PremiereSeanceDEtudeAsync(eleveId, anneeId);
        return dueLe is null || debutLe < dueLe ? new SommeALArrivee(AssuranceDeLAnnee.Libelle(annee), existante.Montant) : null;
    }

    /// <summary>La plus ancienne première séance des inscriptions en Étude de l'élève, cette année : l'assurance y est due.</summary>
    private async Task<DateOnly?> PremiereSeanceDEtudeAsync(Guid eleveId, Guid anneeId)
    {
        var etudes = await contexte.Inscriptions
            .Where(i => i.EleveId == eleveId)
            .Join(contexte.Groupes.Where(g => g.Type == TypeDeGroupe.Etude && g.AnneeScolaireId == anneeId),
                i => i.GroupeId, g => g.Id, (i, g) => new { i.GroupeId, i.DebutLe, i.ArreteLe })
            .ToListAsync();
        var groupeIds = etudes.Select(e => e.GroupeId).ToList();
        var seances = await contexte.Seances.Where(s => groupeIds.Contains(s.GroupeId)).Select(s => new { s.GroupeId, s.Date }).ToListAsync();
        // Une inscription arrêtée avant sa première séance n'a jamais commencé : elle ne compte pas.
        return etudes
            .Select(e => seances.Where(s => s.GroupeId == e.GroupeId && s.Date >= e.DebutLe).Select(s => (DateOnly?)s.Date).Min() is { } premiere
                && (e.ArreteLe is not { } arreteLe || premiere <= arreteLe) ? premiere : (DateOnly?)null)
            .Min();
    }

    /// <summary>
    /// « Bonjour, l'inscription d'Adem Miled est enregistrée. » ; la matière, le professeur, le groupe, la date et l'heure de
    /// la première séance ; puis le nom du centre et son téléphone, s'il en a un. Le nom du centre ne va qu'en signature :
    /// « au Les Jasmins » ou « au Académie Ennour » ne se lirait pas.
    /// </summary>
    private static string TexteDeConfirmation(Eleve eleve, InscriptionDeLEleve inscription, Seance premiere, Centre centre)
    {
        var jour = Jours.De(premiere.Date.DayOfWeek).ToString().ToLowerInvariant();
        var signature = centre.Telephone is { } telephone ? $"{centre.Nom}, {TelephoneTunisien.Grouper(telephone)}" : centre.Nom;
        return $"Bonjour, l'inscription {Francais.De($"{eleve.Prenom} {eleve.Nom}")} est enregistrée.\n"
            + $"{inscription.Groupe.Matiere} avec {inscription.Professeur}, {inscription.Groupe.Nom} : première séance le {jour} "
            + $"{SaisieDuGroupe.EcrireUneDate(premiere.Date)} à {CalendrierDuGroupe.Heure(premiere.Debut)}.\n"
            + $"À bientôt. {signature}";
    }

    /// <summary>Une inscription, son groupe et ses sommes dues : prix spécial, règlements et reste regroupé compris.</summary>
    private sealed record Echeance(Inscription Inscription, Groupe Groupe, IReadOnlyList<Seance> Seances, IReadOnlyList<SommeDue> Sommes,
        IReadOnlyDictionary<string, OrigineDuReglement> Origines, CalendrierAPayer Calendrier, ConditionsDeLInscription Conditions);

    private async Task<List<Echeance>> EcheancesAsync(IReadOnlyCollection<Inscription> demandees)
    {
        // Les inscriptions de départ (ELV-09), de proche en proche : leur report vient de la précédente.
        var inscriptions = demandees.ToDictionary(i => i.Id);
        var aChercher = demandees.Where(i => i.DepuisInscriptionId is not null).Select(i => i.DepuisInscriptionId!.Value).ToList();
        while (aChercher.Count > 0)
        {
            var trouvees = await contexte.Inscriptions.Where(i => aChercher.Contains(i.Id)).ToListAsync();
            foreach (var trouvee in trouvees)
                inscriptions.TryAdd(trouvee.Id, trouvee);
            aChercher = trouvees.Where(i => i.DepuisInscriptionId is { } depuis && !inscriptions.ContainsKey(depuis))
                .Select(i => i.DepuisInscriptionId!.Value).Distinct().ToList();
        }

        var toutes = inscriptions.Values.ToList();
        var echeances = await EcheancesSansReportAsync(toutes);
        var calculees = new Dictionary<Guid, Echeance>();
        Echeance Calculer(Guid id)
        {
            if (calculees.TryGetValue(id, out var deja))
                return deja;
            var echeance = echeances[id];
            if (echeance.Inscription.DepuisInscriptionId is { } depuis)
            {
                var precedente = Calculer(depuis);
                var report = Echeancier.ValeurNonSuivie(precedente.Calendrier, precedente.Conditions, echeance.Inscription.DebutLe);
                var conditions = echeance.Conditions with { Report = report };
                echeance = echeance with { Conditions = conditions, Sommes = Echeancier.Calculer(echeance.Calendrier, conditions) };
            }

            return calculees[id] = echeance;
        }

        return demandees.Select(i => Calculer(i.Id)).ToList();
    }

    private async Task<Dictionary<Guid, Echeance>> EcheancesSansReportAsync(IReadOnlyCollection<Inscription> inscriptions)
    {
        var inscriptionIds = inscriptions.Select(i => (Guid?)i.Id).ToList();
        var groupeIds = inscriptions.Select(i => i.GroupeId).Distinct().ToList();
        var groupes = await contexte.Groupes.Where(g => groupeIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id);
        var seances = (await contexte.Seances.Where(s => groupeIds.Contains(s.GroupeId)).ToListAsync())
            .GroupBy(s => s.GroupeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Date).ThenBy(s => s.Debut).ThenBy(s => s.Id).ToList());
        var reglements = (await contexte.Reglements.Where(r => inscriptionIds.Contains(r.InscriptionId)).ToListAsync())
            .ToLookup(r => r.InscriptionId!.Value);

        return inscriptions.Select(i =>
        {
            var groupe = groupes[i.GroupeId];
            var duGroupe = seances.GetValueOrDefault(i.GroupeId) ?? [];
            var regles = reglements[i.Id].ToList();
            var conditions = new ConditionsDeLInscription(i.Mode, i.DebutLe, i.PrixSpecial, regles.ToDictionary(r => r.Cle, r => r.Montant), i.ResteRegroupeLe,
                i.ArreteLe, 0m, regles.ToDictionary(r => r.Cle, r => r.PartDuReport));
            var calendrier = Calendrier(groupe, duGroupe);
            return new Echeance(i, groupe, duGroupe, Echeancier.Calculer(calendrier, conditions),
                regles.ToDictionary(r => r.Cle, r => r.Origine), calendrier, conditions);
        }).ToDictionary(e => e.Inscription.Id);
    }

    /// <summary>
    /// Les inscriptions telles que les pages les montrent, avec leur élève, leur groupe, leur première séance, leur prix
    /// spécial et ce qui est réglé.
    /// </summary>
    private async Task<List<InscriptionDeLEleve>> DecrireAsync(IReadOnlyCollection<Inscription> inscriptions)
    {
        var eleveIds = inscriptions.Select(i => i.EleveId).Distinct().ToList();
        var echeances = await EcheancesAsync(inscriptions);
        var eleves = await contexte.Eleves.Where(e => eleveIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
        var professeurIds = echeances.Select(e => e.Groupe.ProfesseurId).Distinct().ToList();
        var matiereIds = echeances.Select(e => e.Groupe.MatiereId).Distinct().ToList();
        var professeurs = await contexte.Professeurs.Where(p => professeurIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => $"{p.Prenom} {p.Nom}");
        var matieres = await contexte.Matieres.Where(m => matiereIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Nom);
        var aujourdhui = DateDeTunis.Aujourdhui(temps);

        return echeances.Select(e =>
        {
            var (i, groupe) = (e.Inscription, e.Groupe);
            var eleve = eleves[i.EleveId];
            var premiere = e.Seances.FirstOrDefault(s => s.Date >= i.DebutLe);
            var prixNormal = groupe.Type == TypeDeGroupe.Etude
                ? CalendrierDuGroupe.PrixDuMois(groupe.Prix, GroupesDuCentre.PremierMoisAVenir(groupe, e.Seances, aujourdhui))
                : groupe.PrixTotal!.Value;
            return new InscriptionDeLEleve(
                i.Id,
                new EleveInscrit(eleve.Id, eleve.Prenom, eleve.Nom),
                new GroupeDeLInscription(groupe.Id, groupe.Nom, groupe.Type.ToString(), matieres[groupe.MatiereId]),
                professeurs[groupe.ProfesseurId],
                i.Mode.ToString(),
                prixNormal,
                groupe.Tranches.Count,
                premiere is null ? null : new SeanceDeDebut(premiere.Date, premiere.Debut),
                i.PrixSpecial,
                i.RaisonDuPrixSpecial,
                e.Sommes.Where(s => s.Reglee).Select(s => new SommeReglee(s.Libelle, s.Montant, e.Origines[s.Cle].ToString())).ToList(),
                e.Sommes.Where(s => PayableAvantSchoolHub(e, s)).Select(s => new SommePayable(s.Cle, s.Libelle, s.Montant)).ToList(),
                ATrancheAVenir(e, aujourdhui),
                i.ArreteLe,
                e.Conditions.Report);
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

/// <summary>Ce qu'une action sur une inscription a donné : l'inscription et le statut HTTP, ou un refus.</summary>
public sealed record IssueSurUneInscription(InscriptionDeLEleve? Inscription, int Statut, string? Message);

/// <summary>Ce qu'une action sur l'assurance a donné : l'assurance et le statut HTTP, ou un refus.</summary>
public sealed record IssueSurUneAssurance(AssuranceDeLEleve? Assurance, int Statut, string? Message);

/// <summary>Une liste d'inscriptions, ou un refus (statut et message).</summary>
public sealed record IssueDesInscriptions(List<InscriptionDeLEleve>? Liste, int Statut, string? Message);
