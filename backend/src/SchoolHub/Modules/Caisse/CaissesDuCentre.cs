using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;

namespace SchoolHub.Modules.Caisse;

/// <summary>
/// CAI-09 et CAI-10 : la caisse de chaque compte et sa clôture du soir, puis le contrôle des clôtures par le propriétaire.
/// Une caisse, ce sont les reçus d'un compte, d'un jour, pas encore clôturés : un encaissement après la clôture ouvre la
/// caisse suivante du jour. Les achats payés depuis la caisse (RHA-04) sont ses sorties : ils s'y soustraient.
/// </summary>
public sealed class CaissesDuCentre(SchoolHubDbContext contexte, JournalDuCentre journal, CentreCourant centreCourant, TimeProvider temps)
{
    public const string ClotureIntrouvable = "Clôture introuvable";
    private const string ChoisirLeJour = "Choisissez le jour";
    private const string JourAVenir = "Ce jour est à venir";
    private const string MontantCompte = "Saisissez le montant compté, au millime près";
    private const string RienACloturer = "Rien à clôturer ce jour-là";

    /// <summary>Le contrôle montre par défaut les 30 derniers jours.</summary>
    private const int JoursParDefaut = 30;

    /// <summary>Le nombre de clôtures d'un contrôle : assez pour plusieurs mois, jamais toute l'histoire du centre.</summary>
    private const int CloturesAuPlus = 500;

    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    /// <summary>
    /// CAI-09 : « Ma caisse » : ses caisses ouvertes, les oubliées d'abord (jours passés pas clôturés), puis celle du jour
    /// (même vide) ; et ses clôtures du jour.
    /// </summary>
    public async Task<MaCaisse> MaCaisseAsync(Compte compte)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var ouverts = await contexte.Recus.Where(r => r.AuteurId == compte.Id && r.ClotureId == null && r.Jour <= aujourdhui).ToListAsync();
        // RHA-04 : les achats payés depuis sa caisse, pas encore clôturés : ses sorties de caisse.
        var sorties = await SortiesOuvertes(compte.Id).Where(a => a.Jour <= aujourdhui).ToListAsync();
        var jours = ouverts.Select(r => r.Jour).Concat(sorties.Select(a => a.Jour)).Append(aujourdhui).Distinct().Order().ToList();
        var rangs = await contexte.CloturesDeCaisse.Where(c => c.CompteId == compte.Id && jours.Contains(c.Jour))
            .GroupBy(c => c.Jour).Select(g => new { Jour = g.Key, Nombre = g.Count() })
            .ToDictionaryAsync(g => g.Jour, g => g.Nombre);

        var ouvertes = jours
            .Select(jour =>
            {
                var recus = ouverts.Where(r => r.Jour == jour).ToList();
                var encaisse = Encaisse(recus);
                var sortiesDuJour = sorties.Where(a => a.Jour == jour).Sum(a => a.Montant);
                return new CaisseOuverte(jour, rangs.GetValueOrDefault(jour) + 1, encaisse, sortiesDuJour, encaisse - sortiesDuJour, recus.Count(r => r.AnnuleLe is null),
                    jour < aujourdhui, recus.Count(r => r.AnnuleLe is not null), Cheques(recus), NombreDeCheques(recus));
            })
            .ToList();
        var clotures = await contexte.CloturesDeCaisse.Where(c => c.CompteId == compte.Id && c.Jour == aujourdhui).OrderBy(c => c.Rang).ToListAsync();
        return new MaCaisse(ouvertes, clotures.Select(Decrire).ToList());
    }

    /// <summary>
    /// CAI-09 : clôture la caisse ouverte du compte pour ce jour (aujourd'hui, ou un jour oublié) avec le montant compté.
    /// Ses reçus y sont rangés ; l'attendu et l'écart sont figés. La ligne du compte reste verrouillée jusqu'à la fin :
    /// deux clôtures ne comptent jamais les mêmes reçus. La clôture va au journal.
    /// </summary>
    public async Task<IssueDeLaCloture> CloturerAsync(Compte auteur, ClotureSaisie saisie)
    {
        if (saisie.Jour is not { } jour)
            return Refus(StatusCodes.Status400BadRequest, ChoisirLeJour);
        if (jour > DateDeTunis.Aujourdhui(temps))
            return Refus(StatusCodes.Status400BadRequest, JourAVenir);
        if (saisie.Compte is not { } compte || compte < 0 || decimal.Round(compte, 3) != compte || compte > 999_999_999m)
            return Refus(StatusCodes.Status400BadRequest, MontantCompte);

        await using var transaction = await contexte.Database.BeginTransactionAsync();
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM comptes WHERE id = {auteur.Id} AND centre_id = {CentreId} FOR NO KEY UPDATE");
        var recus = await contexte.Recus.Where(r => r.AuteurId == auteur.Id && r.Jour == jour && r.ClotureId == null).ToListAsync();
        var sorties = await SortiesOuvertes(auteur.Id).Where(a => a.Jour == jour).ToListAsync();
        if (recus.Count == 0 && sorties.Count == 0)
            return Refus(StatusCodes.Status409Conflict, RienACloturer);

        var rang = await contexte.CloturesDeCaisse.CountAsync(c => c.CompteId == auteur.Id && c.Jour == jour) + 1;
        var cloture = new ClotureDeCaisse(auteur.Id, $"{auteur.Prenom} {auteur.Nom}", jour, rang, Encaisse(recus), sorties.Sum(a => a.Montant), compte,
            temps.GetUtcNow(), Cheques(recus), NombreDeCheques(recus));
        contexte.CloturesDeCaisse.Add(cloture);
        foreach (var recu in recus)
            recu.Ranger(cloture.Id);
        foreach (var sortie in sorties)
            sortie.Ranger(cloture.Id);
        journal.Noter(auteur, ActionDuJournal.CaisseCloturee,
            $"Caisse de {cloture.Secretaire} du {SaisieDuGroupe.EcrireUneDate(jour)}" + (rang > 1 ? $" ({rang}ᵉ caisse)" : ""),
            $"Attendu {SaisieDuGroupe.EcrireUnMontant(cloture.Attendu)} TND",
            $"Compté {SaisieDuGroupe.EcrireUnMontant(compte)} TND, écart {Signe(cloture.Ecart)} TND");
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueDeLaCloture(Decrire(cloture), StatusCodes.Status201Created, null);
    }

    /// <summary>
    /// CAI-10 : les clôtures de toutes les caisses du centre, les plus récentes en premier, sur une période (par défaut
    /// les 30 derniers jours) et pour un compte ; le total à récupérer ; les caisses des jours passés pas clôturées.
    /// </summary>
    public async Task<ControleDesClotures> ControleAsync(DateOnly? du, DateOnly? au, Guid? compteId)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var fin = au ?? aujourdhui;
        var debut = du ?? fin.AddDays(-JoursParDefaut);
        var requete = contexte.CloturesDeCaisse.Where(c => c.Jour >= debut && c.Jour <= fin);
        if (compteId is { } id)
            requete = requete.Where(c => c.CompteId == id);
        var clotures = await requete.OrderByDescending(c => c.Jour).ThenByDescending(c => c.ClotureeLe).Take(CloturesAuPlus).ToListAsync();

        var aRecuperer = await contexte.CloturesDeCaisse.Where(c => c.ArgentRecuLe == null).SumAsync(c => (decimal?)c.Compte) ?? 0m;
        var recusOuverts = await contexte.Recus.Where(r => r.ClotureId == null && r.Jour < aujourdhui).ToListAsync();
        var sortiesOuvertes = await contexte.Achats.Where(a => a.PayeDepuisLaCaisse && a.ClotureId == null && a.Jour < aujourdhui).ToListAsync();
        var nonCloturees = recusOuverts.Select(r => (r.AuteurId, r.Auteur, r.Jour)).Concat(sortiesOuvertes.Select(a => (a.AuteurId, a.Auteur, a.Jour)))
            .DistinctBy(c => (c.AuteurId, c.Jour))
            .OrderBy(c => c.Jour).ThenBy(c => c.Auteur)
            .Select(c =>
            {
                var recus = recusOuverts.Where(r => r.AuteurId == c.AuteurId && r.Jour == c.Jour).ToList();
                var sorties = sortiesOuvertes.Where(a => a.AuteurId == c.AuteurId && a.Jour == c.Jour).Sum(a => a.Montant);
                return new CaisseNonCloturee(c.AuteurId, c.Auteur, c.Jour, Encaisse(recus) - sorties, recus.Count(r => r.AnnuleLe is null));
            })
            .ToList();
        var comptes = await contexte.Users.Where(c => c.CentreId == CentreId).OrderBy(c => c.Prenom).ThenBy(c => c.Nom)
            .Select(c => new CompteDeLaCaisse(c.Id, c.Prenom + " " + c.Nom))
            .ToListAsync();
        return new ControleDesClotures(clotures.Select(Decrire).ToList(), aRecuperer, nonCloturees, comptes);
    }

    /// <summary>CAI-10 : le propriétaire coche « argent reçu » (ou le décoche, s'il s'est trompé).</summary>
    public async Task<IssueDeLaCloture> MarquerLArgentRecuAsync(Compte auteur, Guid id, ArgentRecuSaisi saisie)
    {
        var cloture = await contexte.CloturesDeCaisse.SingleOrDefaultAsync(c => c.Id == id);
        if (cloture is null)
            return Refus(StatusCodes.Status404NotFound, ClotureIntrouvable);
        cloture.MarquerLArgentRecu(saisie.Recu ?? true, auteur.Id, temps.GetUtcNow());
        await contexte.SaveChangesAsync();
        return new IssueDeLaCloture(Decrire(cloture), StatusCodes.Status200OK, null);
    }

    /// <summary>RHA-04 : les sorties de caisse d'un compte pas encore clôturées : ses achats payés depuis sa caisse.</summary>
    private IQueryable<SchoolHub.Modules.Achats.Achat> SortiesOuvertes(Guid compteId) =>
        contexte.Achats.Where(a => a.AuteurId == compteId && a.PayeDepuisLaCaisse && a.ClotureId == null);

    /// <summary>L'encaissé en espèces : les reçus en espèces qui ne sont pas annulés ; c'est l'argent du tiroir.</summary>
    private static decimal Encaisse(IEnumerable<Recu> recus) =>
        recus.Where(r => r.AnnuleLe is null && r.Mode == ModeDePaiement.Especes).Sum(r => r.Total);

    /// <summary>Les chèques reçus, pas annulés : comptés à part de la caisse.</summary>
    private static decimal Cheques(IEnumerable<Recu> recus) =>
        recus.Where(r => r.AnnuleLe is null && r.Mode == ModeDePaiement.Cheque).Sum(r => r.Total);

    private static int NombreDeCheques(IEnumerable<Recu> recus) => recus.Count(r => r.AnnuleLe is null && r.Mode == ModeDePaiement.Cheque);

    /// <summary>« −5,000 », « +2,500 », « 0,000 ».</summary>
    internal static string Signe(decimal montant) =>
        (montant > 0 ? "+" : montant < 0 ? "−" : "") + SaisieDuGroupe.EcrireUnMontant(Math.Abs(montant));

    private static ClotureDecrite Decrire(ClotureDeCaisse c) => new(c.Id, c.Jour, c.Rang, c.CompteId, c.Secretaire, c.Encaisse, c.Sorties, c.Attendu,
        c.Compte, c.Ecart, c.ClotureeLe, c.ArgentRecuLe is not null, c.ArgentRecuLe, c.Cheques, c.NombreDeCheques);

    private static IssueDeLaCloture Refus(int statut, string message) => new(null, statut, message);
}

/// <summary>CAI-09 : le jour de la caisse à clôturer, et le montant compté.</summary>
public sealed record ClotureSaisie(DateOnly? Jour, decimal? Compte);

/// <summary>CAI-10 : « argent reçu » coché ou décoché.</summary>
public sealed record ArgentRecuSaisi(bool? Recu);

/// <summary>
/// CAI-09 : une caisse pas encore clôturée : son jour, son rang dans le jour, l'encaissé, les sorties, les espèces
/// attendues, le nombre de reçus valables, si elle est oubliée (d'un jour passé), et le nombre de reçus annulés : une
/// caisse qui n'a que des reçus annulés se clôture aussi.
/// </summary>
public sealed record CaisseOuverte(DateOnly Jour, int Rang, decimal Encaisse, decimal Sorties, decimal Attendu, int Recus, bool Oubliee, int Annules,
    decimal Cheques = 0m, int NombreDeCheques = 0);

/// <summary>Une clôture, telle que « Ma caisse » et le contrôle la montrent.</summary>
public sealed record ClotureDecrite(
    Guid Id,
    DateOnly Jour,
    int Rang,
    Guid CompteId,
    string Secretaire,
    decimal Encaisse,
    decimal Sorties,
    decimal Attendu,
    decimal Compte,
    decimal Ecart,
    DateTimeOffset ClotureeLe,
    bool ArgentRecu,
    DateTimeOffset? ArgentRecuLe,
    decimal Cheques = 0m,
    int NombreDeCheques = 0);

/// <summary>CAI-09 : « Ma caisse » : les caisses ouvertes (oubliées d'abord), et les clôtures du jour.</summary>
public sealed record MaCaisse(List<CaisseOuverte> Ouvertes, List<ClotureDecrite> Clotures);

/// <summary>CAI-10 : une caisse d'un jour passé, pas clôturée : son compte, son jour, l'attendu, le nombre de reçus.</summary>
public sealed record CaisseNonCloturee(Guid CompteId, string Secretaire, DateOnly Jour, decimal Attendu, int Recus);

public sealed record CompteDeLaCaisse(Guid Id, string Nom);

/// <summary>CAI-10 : les clôtures, le total à récupérer, les caisses pas clôturées, et les comptes pour le filtre.</summary>
public sealed record ControleDesClotures(List<ClotureDecrite> Clotures, decimal ARecuperer, List<CaisseNonCloturee> NonCloturees, List<CompteDeLaCaisse> Comptes);

/// <summary>Ce qu'une action sur une clôture a donné : la clôture et le statut HTTP, ou un refus.</summary>
public sealed record IssueDeLaCloture(ClotureDecrite? Cloture, int Statut, string? Message);
