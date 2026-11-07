using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Emails;
using SchoolHub.Socle.Journal;
using SchoolHub.Socle.Messages;

namespace SchoolHub.Modules.Caisse;

/// <summary>
/// CAI-03, CAI-05, CAI-06 et CAI-07 : encaisser en espèces ce qu'un élève doit, avec un reçu ; le remettre (WhatsApp,
/// email, impression) ; retrouver les reçus ; les annuler. Espèces seulement, pas de paiement partiel : chaque somme se
/// paie en entier, au montant calculé par la plateforme.
/// </summary>
public sealed partial class RecusDuCentre(
    SchoolHubDbContext contexte,
    InscriptionsDuCentre inscriptions,
    JournalDuCentre journal,
    IEnvoiEmail emails,
    CentreCourant centreCourant,
    TimeProvider temps,
    ILogger<RecusDuCentre> traces)
{
    public const string RecuIntrouvable = "Reçu introuvable";
    private const string EleveIntrouvable = "Élève introuvable";
    private const string AucuneSomme = "Cochez au moins une somme";
    private const string SommePlusAPayer = "Une somme cochée n'est plus à payer : rechargez la page";
    private const string DejaAnnule = "Ce reçu est déjà annulé";
    private const string MotifObligatoire = "Écrivez le motif de l'annulation";
    private const string MotifTropLong = "Le motif fait au plus 200 caractères";
    private const string DAvantAujourdhui = "Ce reçu date d'avant aujourd'hui : seul le propriétaire peut l'annuler";
    private const string DUneAutreSecretaire = "Ce reçu a été fait par une autre secrétaire : seul le propriétaire peut l'annuler";
    private const string CaisseCloturee = "Votre caisse est clôturée : seul le propriétaire peut annuler ce reçu";
    public const string AucunEmail = "Aucun email n'est connu pour cet élève. Ajoutez-en un sur sa fiche, ou utilisez WhatsApp.";

    /// <summary>Le nombre de reçus d'une recherche : assez pour une page, jamais toute une année.</summary>
    private const int RecusAuPlus = 200;

    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    /// <summary>
    /// CAI-03 : ce que l'élève peut payer : les sommes dues à ce jour (cochées d'office sur la page), puis celles à venir
    /// (« Payer d'avance »). <c>null</c> si l'élève n'est pas dans ce centre.
    /// </summary>
    public async Task<Encaissable?> EncaissableAsync(Guid eleveId)
    {
        var eleve = await contexte.Eleves.SingleOrDefaultAsync(e => e.Id == eleveId);
        if (eleve is null)
            return null;
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var sommes = await inscriptions.SommesEncaissablesAsync(eleveId);
        return new Encaissable(
            new EleveDuRecu(eleve.Id, eleve.Prenom, eleve.Nom),
            sommes.Select(s => new SommeAEncaisser(s.InscriptionId, s.Cle, s.Groupe, s.Libelle, s.Montant, s.DueLe == DateOnly.MaxValue ? null : s.DueLe,
                s.DueLe <= aujourdhui)).ToList(),
            EmailsDe(eleve).Count > 0);
    }

    /// <summary>
    /// CAI-03 : encaisse en espèces les sommes cochées, en entier, et crée un seul reçu pour l'élève, au numéro qui suit
    /// le dernier du centre. Les sommes deviennent réglées : sa couleur suit. L'encaissement va au journal ; le reçu part
    /// ensuite par email à ses destinataires qui en ont un, avec copie au centre.
    /// </summary>
    public async Task<IssueDuRecu> EncaisserAsync(Compte auteur, Guid eleveId, EncaissementSaisi saisie)
    {
        Recu recu;
        await using (var transaction = await contexte.Database.BeginTransactionAsync())
        {
            // L'élève d'abord, puis le centre : deux encaissements du même élève passent l'un après l'autre, et deux
            // encaissements du centre ne prennent jamais le même numéro.
            await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM eleves WHERE id = {eleveId} AND centre_id = {CentreId} FOR NO KEY UPDATE");
            var eleve = await contexte.Eleves.SingleOrDefaultAsync(e => e.Id == eleveId);
            if (eleve is null)
                return Refus(StatusCodes.Status404NotFound, EleveIntrouvable);
            var choisies = (saisie.Sommes ?? []).Where(s => s is not null).Select(s => (s.InscriptionId, Cle: s.Cle?.Trim() ?? "")).Distinct().ToList();
            if (choisies.Count == 0)
                return Refus(StatusCodes.Status400BadRequest, AucuneSomme);

            var encaissables = await inscriptions.SommesEncaissablesAsync(eleveId);
            var aRegler = choisies.Select(c => encaissables.FirstOrDefault(s => s.InscriptionId == c.InscriptionId && s.Cle == c.Cle)).ToList();
            if (aRegler.Any(s => s is null))
                return Refus(StatusCodes.Status409Conflict, SommePlusAPayer);

            recu = CreerLeRecu(auteur, eleve, aRegler.OfType<SommeEncaissable>().ToList(), await NumeroSuivantAsync());
            await contexte.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await EnvoyerParEmailAsync(recu);
        return new IssueDuRecu(await DecrireAsync(recu, auteur), StatusCodes.Status201Created, null);
    }

    /// <summary>
    /// Le numéro du prochain reçu du centre. La ligne du centre reste verrouillée jusqu'à la fin de la transaction : deux
    /// encaissements ne prennent jamais le même numéro.
    /// </summary>
    private async Task<int> NumeroSuivantAsync()
    {
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM centres WHERE id = {CentreId} FOR NO KEY UPDATE");
        return (await contexte.Recus.MaxAsync(r => (int?)r.Numero) ?? 0) + 1;
    }

    /// <summary>
    /// Le reçu de l'élève, à ce numéro, avec une ligne et un règlement par somme : les sommes deviennent réglées. Va au
    /// journal. Rien n'est encore enregistré : l'appelant enregistre et valide la transaction.
    /// </summary>
    private Recu CreerLeRecu(Compte auteur, Eleve eleve, IReadOnlyList<SommeEncaissable> aRegler, int numero)
    {
        var maintenant = temps.GetUtcNow();
        var recu = new Recu(numero, eleve.Id, auteur.Id, $"{auteur.Prenom} {auteur.Nom}", maintenant, DateDeTunis.Aujourdhui(temps),
            aRegler.Select(s => new LigneDuRecu(s.Groupe, s.Libelle, s.Montant, s.InscriptionId, s.AssuranceId, s.Cle)));
        contexte.Recus.Add(recu);
        // Une entrée à la bibliothèque (BIB-01) n'était pas due : elle n'a pas de règlement.
        foreach (var somme in aRegler.Where(s => s.AssuranceId is not null || s.InscriptionId is not null))
        {
            contexte.Reglements.Add(somme.AssuranceId is { } assuranceId
                ? Reglement.DeLAssurance(assuranceId, somme.Montant, OrigineDuReglement.Encaissement, maintenant, auteur.Id, recu.Id)
                : Reglement.DUneSomme(somme.InscriptionId!.Value, somme.Cle, somme.Montant, OrigineDuReglement.Encaissement, maintenant, auteur.Id,
                    somme.ReportDeduit, recu.Id));
        }

        journal.Noter(auteur, ActionDuJournal.Encaissement,
            Couper($"Reçu n° {numero} · {eleve.Prenom} {eleve.Nom} · {string.Join(", ", aRegler.Select(s => s.Libelle))}"),
            "À payer", $"Encaissé, {SaisieDuGroupe.EcrireUnMontant(recu.Total)} TND");
        return recu;
    }

    /// <summary>
    /// BIB-01 : le reçu d'une entrée à la bibliothèque, payée en espèces, dans la transaction de l'appelant. Il ne part pas
    /// par email : il se remet seulement si la personne le demande.
    /// </summary>
    internal async Task<Recu> CreerUnRecuDeBibliothequeAsync(Compte auteur, Eleve eleve, decimal prix)
    {
        var recu = CreerLeRecu(auteur, eleve,
            [new SommeEncaissable(null, null, LigneDeBibliotheque, LibelleDeBibliotheque, CleDeBibliotheque, prix, DateDeTunis.Aujourdhui(temps), 0m)],
            await NumeroSuivantAsync());
        return recu;
    }

    internal const string LigneDeBibliotheque = "Bibliothèque";
    internal const string LibelleDeBibliotheque = "Entrée à la bibliothèque (journée)";
    private const string CleDeBibliotheque = "Bibliotheque";

    /// <summary>CAI-05 : le reçu décrit pour la page ; utilisé aussi par la bibliothèque.</summary>
    internal Task<RecuDecrit> DecrireLeRecuAsync(Recu recu, Compte lecteur) => DecrireAsync(recu, lecteur);

    /// <summary>CAI-05 et CAI-06 : un reçu, son texte et ses messages prêts ; <c>null</c> s'il n'est pas dans ce centre.</summary>
    public async Task<RecuDecrit?> LireAsync(Guid id, Compte lecteur)
    {
        var recu = await contexte.Recus.SingleOrDefaultAsync(r => r.Id == id);
        return recu is null ? null : await DecrireAsync(recu, lecteur);
    }

    /// <summary>
    /// CAI-06 : les reçus du centre, les plus récents en premier : ceux d'un jour, d'un numéro, ou d'un élève (chaque mot,
    /// sans accents ni majuscules, dans son prénom ou son nom). La secrétaire voit tous les reçus du centre.
    /// </summary>
    /// <summary>La fiche de l'élève : ses reçus, du plus récent au plus ancien, avec ce que chacun payait.</summary>
    public async Task<List<RecuDeLEleve>> DeLEleveAsync(Guid eleveId) =>
        (await contexte.Recus.Where(r => r.EleveId == eleveId).OrderByDescending(r => r.Numero).Take(RecusAuPlus).ToListAsync())
        .Select(r => new RecuDeLEleve(r.Id, r.Numero, r.Jour, string.Join(", ", r.Lignes.Select(l => l.Libelle)), r.Total, r.Auteur,
            r.AnnuleLe is not null))
        .ToList();

    public async Task<List<RecuDeLaListe>> ListerAsync(DateOnly? jour, int? numero, string? eleve)
    {
        var requete = contexte.Recus.AsQueryable();
        if (jour is { } le)
            requete = requete.Where(r => r.Jour == le);
        if (numero is { } n)
            requete = requete.Where(r => r.Numero == n);
        var eleves = contexte.Eleves.AsQueryable();
        if (!string.IsNullOrWhiteSpace(eleve))
        {
            foreach (var mot in Eleve.Normaliser(eleve).Split(' '))
                eleves = eleves.Where(e => e.NomDeRecherche.Contains(mot));
        }

        return await requete
            .Join(eleves, r => r.EleveId, e => e.Id, (r, e) => new { Recu = r, Eleve = e })
            .OrderByDescending(x => x.Recu.Numero)
            .Take(RecusAuPlus)
            .Select(x => new RecuDeLaListe(x.Recu.Id, x.Recu.Numero, x.Recu.Jour, x.Recu.CreeLe, x.Eleve.Prenom + " " + x.Eleve.Nom, x.Recu.Total,
                x.Recu.Auteur, x.Recu.AnnuleLe != null))
            .ToListAsync();
    }

    /// <summary>
    /// CAI-07 : annule un reçu, avec un motif. La secrétaire annule ses reçus du jour ; sinon, seul le propriétaire. Le
    /// reçu reste visible avec « annulé » ; ce qu'il payait redevient à payer. L'annulation va au journal.
    /// </summary>
    public async Task<IssueDuRecu> AnnulerAsync(Compte auteur, Guid id, AnnulationDuRecu saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        // La caisse de son auteur d'abord, comme la clôture (CAI-09) : une annulation et une clôture passent l'une après
        // l'autre, et la clôture ne compte jamais un reçu annulé pendant qu'elle se fait.
        if (await contexte.Recus.Where(r => r.Id == id).Select(r => (Guid?)r.AuteurId).SingleOrDefaultAsync() is { } auteurDuRecu)
            await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM comptes WHERE id = {auteurDuRecu} AND centre_id = {CentreId} FOR NO KEY UPDATE");
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM recus WHERE id = {id} AND centre_id = {CentreId} FOR UPDATE");
        var recu = await contexte.Recus.SingleOrDefaultAsync(r => r.Id == id);
        if (recu is null)
            return Refus(StatusCodes.Status404NotFound, RecuIntrouvable);
        await contexte.Entry(recu).ReloadAsync();
        if (recu.AnnuleLe is not null)
            return Refus(StatusCodes.Status409Conflict, DejaAnnule);
        if (PourquoiPasAnnulable(recu, auteur) is { } raison)
            return Refus(StatusCodes.Status403Forbidden, raison);
        var motif = saisie.Motif?.Trim() ?? "";
        if (motif.Length == 0)
            return Refus(StatusCodes.Status400BadRequest, MotifObligatoire);
        if (motif.Length > Recu.LongueurMaximaleDuMotif)
            return Refus(StatusCodes.Status400BadRequest, MotifTropLong);

        var eleve = await contexte.Eleves.SingleAsync(e => e.Id == recu.EleveId);
        journal.Noter(auteur, ActionDuJournal.RecuAnnule, Couper($"Reçu n° {recu.Numero} · {eleve.Prenom} {eleve.Nom} · {motif}"),
            $"Encaissé, {SaisieDuGroupe.EcrireUnMontant(recu.Total)} TND", "Annulé");
        recu.Annuler(auteur.Id, motif, temps.GetUtcNow());
        // Ce que le reçu payait redevient à payer : ses règlements partent, ses lignes restent sur le reçu.
        contexte.Reglements.RemoveRange(await contexte.Reglements.Where(r => r.RecuId == recu.Id).ToListAsync());
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueDuRecu(await DecrireAsync(recu, auteur), StatusCodes.Status200OK, null);
    }

    /// <summary>CAI-05 : « Renvoyer l'email » ; sans email connu pour l'élève, la page le dit.</summary>
    public async Task<IssueDuRecu> RenvoyerLEmailAsync(Guid id, Compte lecteur)
    {
        var recu = await contexte.Recus.SingleOrDefaultAsync(r => r.Id == id);
        if (recu is null)
            return Refus(StatusCodes.Status404NotFound, RecuIntrouvable);
        if (!await EnvoyerParEmailAsync(recu))
            return Refus(StatusCodes.Status400BadRequest, AucunEmail);
        return new IssueDuRecu(await DecrireAsync(recu, lecteur), StatusCodes.Status200OK, null);
    }

    /// <summary>
    /// Le reçu part à chaque destinataire de l'élève qui a un email, avec copie au centre s'il en a un. Un email qui
    /// échoue ne défait pas l'encaissement : la secrétaire peut le renvoyer. Rend faux si l'élève n'a aucun email.
    /// </summary>
    private async Task<bool> EnvoyerParEmailAsync(Recu recu)
    {
        var eleve = await contexte.Eleves.SingleAsync(e => e.Id == recu.EleveId);
        var adresses = EmailsDe(eleve);
        if (adresses.Count == 0)
            return false;
        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId);
        if (centre.Email is { } copie && !adresses.Contains(copie, StringComparer.OrdinalIgnoreCase))
            adresses.Add(copie);
        var sujet = $"Reçu n° {recu.Numero} · {centre.Nom}";
        var texte = Texte(recu, eleve, centre);
        foreach (var adresse in adresses)
        {
            try
            {
                await emails.EnvoyerAsync(adresse, sujet, texte);
            }
            catch (Exception erreur)
            {
                traces.LogWarning(erreur, "Le reçu n° {Numero} n'a pas pu partir par email.", recu.Numero);
            }
        }

        return true;
    }

    /// <summary>
    /// Les emails des destinataires de l'élève (exigences communes) : un élève majeur reçoit seul, ou son parent s'il n'a
    /// pas d'email ; un mineur, son parent et lui.
    /// </summary>
    private static List<string> EmailsDe(Eleve eleve)
    {
        IEnumerable<string?> adresses = eleve.Majeur ? [eleve.EmailEleve ?? eleve.EmailParent] : [eleve.EmailParent, eleve.EmailEleve];
        return adresses.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// CAI-07 : pourquoi ce compte ne peut pas annuler ce reçu ; <c>null</c> s'il le peut. Le propriétaire peut toujours.
    /// Après la clôture de sa caisse (CAI-09), la secrétaire ne l'annule plus.
    /// </summary>
    private string? PourquoiPasAnnulable(Recu recu, Compte compte)
    {
        if (compte.Role == RoleDuCompte.Proprietaire)
            return null;
        if (recu.AuteurId != compte.Id)
            return DUneAutreSecretaire;
        if (recu.Jour < DateDeTunis.Aujourdhui(temps))
            return DAvantAujourdhui;
        return recu.ClotureId is not null ? CaisseCloturee : null;
    }

    private async Task<RecuDecrit> DecrireAsync(Recu recu, Compte lecteur)
    {
        var eleve = await contexte.Eleves.SingleAsync(e => e.Id == recu.EleveId);
        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId);
        var texte = Texte(recu, eleve, centre);
        return new RecuDecrit(
            recu.Id,
            recu.Numero,
            recu.Jour,
            recu.CreeLe,
            new CentreDuRecu(centre.Nom, centre.Telephone is { } telephone ? TelephoneTunisien.Grouper(telephone) : null),
            new EleveDuRecu(eleve.Id, eleve.Prenom, eleve.Nom),
            recu.Lignes.Select(l => new LigneDecrite(l.Groupe, l.Libelle, l.Montant)).ToList(),
            recu.Total,
            recu.Auteur,
            recu.AnnuleLe is not null,
            recu.MotifDAnnulation,
            recu.AnnuleLe,
            texte,
            DestinatairesDeLEleve.De(eleve)
                .Select(d => new MessageWhatsApp(d.Nom, TelephoneTunisien.Grouper(d.Chiffres), WhatsApp.Lien(d.Chiffres, texte)))
                .ToList(),
            EmailsDe(eleve),
            recu.AnnuleLe is null && PourquoiPasAnnulable(recu, lecteur) is null,
            recu.AnnuleLe is null ? PourquoiPasAnnulable(recu, lecteur) : null);
    }

    /// <summary>
    /// CAI-05 : tout le contenu du reçu en texte, pour WhatsApp et l'email. « Reçu n° 12 · Centre Les Jasmins, 71 234 567 »,
    /// la date, l'élève, une ligne par somme, le total, la secrétaire ; « REÇU ANNULÉ » en tête s'il l'est.
    /// </summary>
    internal static string Texte(Recu recu, Eleve eleve, Centre centre)
    {
        var lignes = new List<string>();
        if (recu.AnnuleLe is not null)
            lignes.Add($"REÇU ANNULÉ : {recu.MotifDAnnulation}");
        lignes.Add($"Reçu n° {recu.Numero} · {centre.Nom}" + (centre.Telephone is { } telephone ? $", {TelephoneTunisien.Grouper(telephone)}" : ""));
        lignes.Add($"Le {SaisieDuGroupe.EcrireUneDate(recu.Jour)}");
        lignes.Add($"Élève : {eleve.Prenom} {eleve.Nom}");
        // L'assurance et la bibliothèque n'ont pas de groupe : leur libellé suffit.
        lignes.AddRange(recu.Lignes.Select(l => l.AssuranceId is not null || l.InscriptionId is null
            ? $"• {l.Libelle} : {SaisieDuGroupe.EcrireUnMontant(l.Montant)} TND"
            : $"• {l.Groupe}, {l.Libelle} : {SaisieDuGroupe.EcrireUnMontant(l.Montant)} TND"));
        lignes.Add($"Total payé en espèces : {SaisieDuGroupe.EcrireUnMontant(recu.Total)} TND");
        lignes.Add($"Reçu par {recu.Auteur}");
        return string.Join("\n", lignes);
    }

    private static string Couper(string texte) => texte.Length > 500 ? texte[..500] : texte;

    private static IssueDuRecu Refus(int statut, string message) => new(null, statut, message);
}

/// <summary>Ce qu'une action sur un reçu a donné : le reçu et le statut HTTP, ou un refus (statut et message).</summary>
public sealed record IssueDuRecu(RecuDecrit? Recu, int Statut, string? Message);
