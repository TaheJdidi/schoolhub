using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Messages;

namespace SchoolHub.Modules.Demandes;

/// <summary>
/// DEM-03, DEM-04 et DEM-05 : les demandes d'inscription du centre courant, à traiter ou traitées. La secrétaire les
/// voit, les enregistre comme élèves inscrits, ou les refuse. Le filtre automatique ne montre que celles du centre.
/// </summary>
public sealed class DemandesDuCentre(
    SchoolHubDbContext contexte,
    ElevesDuCentre eleves,
    InscriptionsDuCentre inscriptions,
    CentreCourant centreCourant,
    TimeProvider temps)
{
    public const string DemandeIntrouvable = "Demande introuvable";
    private const string DejaTraitee = "Cette demande est déjà traitée";
    private const string AucunGroupe = "Choisissez au moins un groupe";
    private const string ChoisirLeGroupe = "Choisissez le groupe";
    private const string GroupeIntrouvable = "Groupe introuvable";
    private const string PlusDeSeance = "Ce groupe n'a plus de séance à venir";
    private const string ChoisirLeMotif = "Choisissez le motif du refus";

    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    /// <summary>
    /// DEM-03 : les demandes à traiter, les plus anciennes en premier, avec « Doublon possible » ; ou les demandes
    /// traitées, les plus récemment traitées en premier.
    /// </summary>
    public async Task<List<DemandeDeLaListe>> ListerAsync(bool traitees)
    {
        var demandes = traitees
            ? await contexte.DemandesDInscription.Where(d => d.Etat != EtatDeLaDemande.ATraiter)
                .OrderByDescending(d => d.TraiteeLe).ThenByDescending(d => d.Id).ToListAsync()
            : await contexte.DemandesDInscription.Where(d => d.Etat == EtatDeLaDemande.ATraiter)
                .OrderBy(d => d.RecueLe).ThenBy(d => d.Id).ToListAsync();

        var niveaux = await contexte.Niveaux.ToDictionaryAsync(n => n.Id, n => n.Nom);
        var groupeIds = demandes.SelectMany(d => d.GroupesPreferes).Select(g => g.GroupeId).Distinct().ToList();
        var groupes = await contexte.Groupes.Where(g => groupeIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id);
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);
        var professeurs = await contexte.Professeurs.ToDictionaryAsync(p => p.Id, p => $"{p.Prenom} {p.Nom}");
        HashSet<Guid> doublons = traitees ? [] : await DoublonsPossiblesAsync(demandes);

        return demandes.Select(d => new DemandeDeLaListe(
                d.Id,
                d.Prenom,
                d.Nom,
                new NiveauDeLEleve(d.NiveauId, niveaux[d.NiveauId]),
                d.GroupesPreferes.Select(g => groupes[g.GroupeId]).Select(g => new MatiereDemandee(
                    matieres[g.MatiereId], professeurs[g.ProfesseurId], g.Creneaux.Select(CreneauDuGroupe.De).ToList())).ToList(),
                d.Amis,
                d.RecueLe,
                doublons.Contains(d.Id),
                d.Etat.ToString(),
                d.Motif?.ToString(),
                d.TraiteeLe))
            .ToList();
    }

    /// <summary>DEM-03 : le nombre de demandes à traiter, pour le menu et l'accueil.</summary>
    public Task<int> CompterAsync() => contexte.DemandesDInscription.CountAsync(d => d.Etat == EtatDeLaDemande.ATraiter);

    /// <summary>
    /// DEM-04 : une demande, avec ce qu'il faut pour l'enregistrer : les fiches d'élèves qui lui ressemblent (même nom ou
    /// même téléphone), et pour chaque matière demandée les groupes ouverts de son niveau qui ont encore une séance à
    /// venir, le préféré marqué, avec les amis cités qui y sont déjà inscrits. <c>null</c> si elle n'est pas dans ce centre.
    /// </summary>
    public async Task<DetailDeLaDemande?> LireAsync(Guid id)
    {
        var demande = await contexte.DemandesDInscription.SingleOrDefaultAsync(d => d.Id == id);
        if (demande is null)
            return null;

        var niveau = await contexte.Niveaux.SingleAsync(n => n.Id == demande.NiveauId);
        var telephones = new[] { demande.TelephoneParent, demande.TelephoneEleve }.OfType<string>().ToList();
        var fichesSemblables = await contexte.Eleves
            .Where(e => e.NomDeRecherche == demande.NomDeRecherche
                || (e.TelephoneParent != null && telephones.Contains(e.TelephoneParent))
                || (e.TelephoneEleve != null && telephones.Contains(e.TelephoneEleve)))
            .OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ThenBy(e => e.Id)
            .Join(contexte.Niveaux, e => e.NiveauId, n => n.Id, (e, n) => new EleveTrouve(e.Id, e.Prenom, e.Nom, n.Nom))
            .ToListAsync();
        var autreDemande = await contexte.DemandesDInscription
            .AnyAsync(d => d.Id != demande.Id && d.Etat == EtatDeLaDemande.ATraiter
                && (d.NomDeRecherche == demande.NomDeRecherche
                    || (d.TelephoneParent != null && telephones.Contains(d.TelephoneParent))
                    || (d.TelephoneEleve != null && telephones.Contains(d.TelephoneEleve))));

        return new DetailDeLaDemande(
            demande.Id,
            demande.Prenom,
            demande.Nom,
            new NiveauDeLEleve(niveau.Id, niveau.Nom),
            demande.Majeur,
            demande.TelephoneParent is { } parent ? TelephoneTunisien.Grouper(parent) : null,
            demande.EmailParent,
            demande.TelephoneEleve is { } telephone ? TelephoneTunisien.Grouper(telephone) : null,
            demande.EmailEleve,
            demande.Etablissement,
            demande.Amis,
            demande.RecueLe,
            demande.Etat.ToString(),
            demande.Motif?.ToString(),
            demande.TraiteeLe,
            demande.EleveId,
            fichesSemblables,
            autreDemande,
            await MatieresAsync(demande));
    }

    /// <summary>
    /// DEM-04 : enregistre la demande comme élève inscrit, tout ou rien. L'élève est une fiche existante (<c>eleveId</c>)
    /// ou une nouvelle fiche, vérifiée comme sur la page Élèves (ELV-02). Chaque groupe choisi l'inscrit à sa prochaine
    /// séance, avec les règles d'ELV-03. Le premier refus annule tout. La ligne de la demande reste verrouillée jusqu'à
    /// la fin : deux secrétaires ne l'enregistrent pas deux fois.
    /// </summary>
    public async Task<IssueDeLaDemande> EnregistrerAsync(Guid id, DemandeAEnregistrer saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var demande = await DemandeVerrouilleeAsync(id);
        if (demande is null)
            return Refus(StatusCodes.Status404NotFound, DemandeIntrouvable);
        if (demande.Etat != EtatDeLaDemande.ATraiter)
            return Refus(StatusCodes.Status409Conflict, DejaTraitee);
        var choisis = saisie.Groupes ?? [];
        if (choisis.Count == 0)
            return Refus(StatusCodes.Status400BadRequest, AucunGroupe);

        var avertissements = new List<string>();
        Guid eleveId;
        if (saisie.EleveId is { } existant)
        {
            if (!await contexte.Eleves.AnyAsync(e => e.Id == existant))
                return Refus(StatusCodes.Status404NotFound, ElevesDuCentre.EleveIntrouvable.Message!);
            eleveId = existant;
        }
        else
        {
            var fiche = await eleves.AjouterAsync(saisie.Fiche ?? new FicheSaisieDeLEleve(null, null, null, false, null, null, null, null, null));
            if (fiche.Fiche is null)
                return Refus(fiche.Statut, fiche.Message!);
            if (fiche.Avertissement is { } doublon)
                avertissements.Add(doublon);
            eleveId = fiche.Fiche.Id;
        }

        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var enregistrements = new List<EnregistrementDeLInscription>();
        foreach (var choisi in choisis)
        {
            if (choisi.GroupeId is not { } groupeId)
                return Refus(StatusCodes.Status400BadRequest, ChoisirLeGroupe);
            if (!await contexte.Groupes.AnyAsync(g => g.Id == groupeId))
                return Refus(StatusCodes.Status404NotFound, GroupeIntrouvable);
            var prochaine = await contexte.Seances.QuiComptent().Where(s => s.GroupeId == groupeId && s.Date >= aujourdhui)
                .OrderBy(s => s.Date).Select(s => (DateOnly?)s.Date).FirstOrDefaultAsync();
            // Sans séance à venir, l'inscription refuse quand même : un groupe fermé ou terminé a son propre refus, plus
            // parlant ; sinon « pas de séance ce jour-là » devient « plus de séance à venir ».
            var issue = await inscriptions.InscrireAsync(new InscriptionSaisie(eleveId, groupeId, choisi.Mode, prochaine ?? aujourdhui));
            if (issue.Enregistrement is null)
                return Refus(issue.Statut, issue.Message == InscriptionsDuCentre.PasDeSeanceCeJourLa ? PlusDeSeance : issue.Message!);
            enregistrements.Add(issue.Enregistrement);
            if (issue.Enregistrement.Avertissement is { } capacite && !avertissements.Contains(capacite))
                avertissements.Add(capacite);
        }

        demande.Inscrire(eleveId, temps.GetUtcNow());
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        var eleve = await contexte.Eleves.SingleAsync(e => e.Id == eleveId);
        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId);
        var texte = enregistrements.Count == 1
            ? enregistrements[0].Texte
            : InscriptionsDuCentre.TexteDeConfirmation(eleve, enregistrements.Select(e => e.Inscription), centre);
        var messages = DestinatairesDeLEleve.De(eleve)
            .Select(d => new MessageWhatsApp(d.Nom, TelephoneTunisien.Grouper(d.Chiffres), WhatsApp.Lien(d.Chiffres, texte)))
            .ToList();
        return new IssueDeLaDemande(
            new DemandeEnregistree(new EleveInscrit(eleve.Id, eleve.Prenom, eleve.Nom), enregistrements,
                avertissements.Count == 0 ? null : string.Join(" ", avertissements), texte, messages),
            StatusCodes.Status201Created, null);
    }

    /// <summary>DEM-05 : refuse la demande, avec son motif. Aucun message ne part.</summary>
    public async Task<IssueDeLaDemande> RefuserAsync(Guid id, RefusSaisi saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var demande = await DemandeVerrouilleeAsync(id);
        if (demande is null)
            return Refus(StatusCodes.Status404NotFound, DemandeIntrouvable);
        if (demande.Etat != EtatDeLaDemande.ATraiter)
            return Refus(StatusCodes.Status409Conflict, DejaTraitee);
        // Le nom du motif seulement : « 1 » ne vaut pas « FausseDemande ».
        var nom = saisie.Motif?.Trim();
        if (nom is null || !Enum.GetNames<MotifDuRefus>().Contains(nom))
            return Refus(StatusCodes.Status400BadRequest, ChoisirLeMotif);
        var motif = Enum.Parse<MotifDuRefus>(nom);

        demande.Refuser(motif, temps.GetUtcNow());
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueDeLaDemande(null, StatusCodes.Status204NoContent, null);
    }

    /// <summary>La demande de ce centre, sa ligne verrouillée jusqu'à la fin de la transaction ; <c>null</c> si elle n'y est pas.</summary>
    private async Task<DemandeDInscription?> DemandeVerrouilleeAsync(Guid id)
    {
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM demandes_d_inscription WHERE id = {id} AND centre_id = {CentreId} FOR UPDATE");
        var demande = await contexte.DemandesDInscription.SingleOrDefaultAsync(d => d.Id == id);
        if (demande is not null)
            await contexte.Entry(demande).ReloadAsync();
        return demande;
    }

    /// <summary>
    /// Les demandes qui ressemblent à un élève du centre ou à une autre demande à traiter : même prénom et même nom (sans
    /// accents ni majuscules), ou un téléphone en commun, celui de l'élève ou du parent, dans un sens ou dans l'autre.
    /// </summary>
    private async Task<HashSet<Guid>> DoublonsPossiblesAsync(List<DemandeDInscription> demandes)
    {
        static IEnumerable<string> Telephones(string? parent, string? eleve) => new[] { parent, eleve }.OfType<string>();

        var noms = demandes.Select(d => d.NomDeRecherche).Distinct().ToList();
        var telephones = demandes.SelectMany(d => Telephones(d.TelephoneParent, d.TelephoneEleve)).Distinct().ToList();
        var semblables = await contexte.Eleves
            .Where(e => noms.Contains(e.NomDeRecherche)
                || (e.TelephoneParent != null && telephones.Contains(e.TelephoneParent))
                || (e.TelephoneEleve != null && telephones.Contains(e.TelephoneEleve)))
            .Select(e => new { e.NomDeRecherche, e.TelephoneParent, e.TelephoneEleve })
            .ToListAsync();
        var nomsDesEleves = semblables.Select(e => e.NomDeRecherche).ToHashSet();
        var telephonesDesEleves = semblables.SelectMany(e => Telephones(e.TelephoneParent, e.TelephoneEleve)).ToHashSet();

        return demandes
            .Where(d => nomsDesEleves.Contains(d.NomDeRecherche)
                || Telephones(d.TelephoneParent, d.TelephoneEleve).Any(telephonesDesEleves.Contains)
                || demandes.Any(autre => autre.Id != d.Id
                    && (autre.NomDeRecherche == d.NomDeRecherche
                        || Telephones(autre.TelephoneParent, autre.TelephoneEleve).Intersect(Telephones(d.TelephoneParent, d.TelephoneEleve)).Any())))
            .Select(d => d.Id)
            .ToHashSet();
    }

    /// <summary>Pour chaque matière demandée, dans l'ordre du formulaire : les groupes où l'inscrire, le préféré en premier.</summary>
    private async Task<List<MatiereDeLaDemande>> MatieresAsync(DemandeDInscription demande)
    {
        var preferes = demande.GroupesPreferes.Select(g => g.GroupeId).ToList();
        var groupesPreferes = await contexte.Groupes.Where(g => preferes.Contains(g.Id)).ToDictionaryAsync(g => g.Id);
        var matiereIds = groupesPreferes.Values.Select(g => g.MatiereId).Distinct().ToList();
        var aujourdhui = DateDeTunis.Aujourdhui(temps);

        var groupes = await contexte.Groupes
            .Where(g => g.FermeLe == null && matiereIds.Contains(g.MatiereId) && (g.NiveauId == null || g.NiveauId == demande.NiveauId))
            .ToListAsync();
        var groupeIds = groupes.Select(g => g.Id).ToList();
        var seances = await contexte.Seances.QuiComptent().Where(s => groupeIds.Contains(s.GroupeId))
            .OrderBy(s => s.Date).ThenBy(s => s.Debut)
            .Select(s => new { s.GroupeId, s.Date, s.Debut })
            .ToListAsync();
        var inscrits = await contexte.Inscriptions
            .Where(i => groupeIds.Contains(i.GroupeId) && (i.ArreteLe == null || i.ArreteLe >= aujourdhui))
            .Join(contexte.Eleves, i => i.EleveId, e => e.Id, (i, e) => new { i.GroupeId, e.NomDeRecherche })
            .ToListAsync();
        var matieres = await contexte.Matieres.Where(m => matiereIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Nom);
        var professeurs = await contexte.Professeurs.ToDictionaryAsync(p => p.Id, p => $"{p.Prenom} {p.Nom}");
        var amis = AmisCites(demande.Amis);

        GroupeProposeALaDemande? Proposer(Groupe groupe)
        {
            var duGroupe = seances.Where(s => s.GroupeId == groupe.Id).ToList();
            var prochaine = duGroupe.FirstOrDefault(s => s.Date >= aujourdhui);
            if (prochaine is null || CalendrierDuGroupe.EstTermine(groupe.Type, duGroupe.LastOrDefault()?.Date, aujourdhui))
                return null;
            var duGroupeInscrits = inscrits.Where(i => i.GroupeId == groupe.Id).ToList();
            var nomsInscrits = duGroupeInscrits.Select(i => i.NomDeRecherche).ToHashSet();
            var modes = Echeancier.ModesPossibles(groupe.Type, groupe.Tranches.Count > 0);
            return new GroupeProposeALaDemande(
                groupe.Id,
                groupe.Nom,
                groupe.Type.ToString(),
                professeurs[groupe.ProfesseurId],
                groupe.Creneaux.Select(CreneauDuGroupe.De).ToList(),
                duGroupeInscrits.Count,
                groupe.Capacite,
                modes.Count > 1 ? modes.Select(m => m.ToString()).ToList() : [],
                groupe.Tranches.Count,
                new SeanceDeDebut(prochaine.Date, prochaine.Debut),
                amis.Where(a => nomsInscrits.Contains(a.Normalise)).Select(a => a.Ecrit).ToList());
        }

        return demande.GroupesPreferes
            .Select(g => groupesPreferes[g.GroupeId])
            .Select(prefere => new MatiereDeLaDemande(
                matieres[prefere.MatiereId],
                prefere.Id,
                groupes.Where(g => g.MatiereId == prefere.MatiereId)
                    .OrderByDescending(g => g.Id == prefere.Id).ThenBy(g => g.Nom).ThenBy(g => g.Id)
                    .Select(Proposer).OfType<GroupeProposeALaDemande>().ToList()))
            .ToList();
    }

    /// <summary>« Sami Chaabane, Ines et Rania Trabelsi » : chaque ami cité, tel qu'écrit et sous sa forme de recherche.</summary>
    private static List<(string Ecrit, string Normalise)> AmisCites(string? amis) =>
        string.IsNullOrWhiteSpace(amis)
            ? []
            : amis.Replace(" et ", ",", StringComparison.OrdinalIgnoreCase)
                .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ami => (ami, Eleve.Normaliser(ami)))
                .Where(ami => ami.Item2.Length > 0)
                .ToList();

    private static IssueDeLaDemande Refus(int statut, string message) => new(null, statut, message);
}

/// <summary>Ce qu'une action sur une demande a donné : l'enregistrement et le statut HTTP, ou un refus (statut et message).</summary>
public sealed record IssueDeLaDemande(DemandeEnregistree? Enregistree, int Statut, string? Message);
