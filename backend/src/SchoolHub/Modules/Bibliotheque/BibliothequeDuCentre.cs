using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Caisse;
using SchoolHub.Modules.Eleves;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Bibliotheque;

/// <summary>
/// BIB-01 à BIB-03 : la bibliothèque du centre courant. Une entrée à la journée se paie en arrivant, au prix des
/// paramètres du centre, avec un reçu qui entre dans la caisse du jour ; la liste d'un jour montre qui est venu, qui est
/// encore là, et l'encaissé.
/// </summary>
public sealed class BibliothequeDuCentre(
    SchoolHubDbContext contexte,
    ElevesDuCentre eleves,
    RecusDuCentre recus,
    CentreCourant centreCourant,
    TimeProvider temps)
{
    public const string EntreeIntrouvable = "Entrée introuvable";
    private const string PrixPasFixe = "Le prix de l'entrée n'est pas fixé : voyez les paramètres du centre";
    private const string HeureDeDepart = "L'heure de départ s'écrit HH:mm, après l'arrivée";

    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    /// <summary>BIB-02 et BIB-03 : la liste d'un jour (aujourd'hui par défaut), de la première arrivée à la dernière.</summary>
    public async Task<JourneeALaBibliotheque> JourneeAsync(DateOnly? jour)
    {
        var le = jour ?? DateDeTunis.Aujourdhui(temps);
        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId);
        var entrees = await contexte.EntreesALaBibliotheque.Where(e => e.Jour == le)
            .Join(contexte.Eleves, e => e.EleveId, el => el.Id, (e, el) => new { Entree = e, Eleve = el })
            .Join(contexte.Recus, x => x.Entree.RecuId, r => r.Id, (x, r) => new { x.Entree, x.Eleve, r.Numero, Annule = r.AnnuleLe != null })
            .OrderBy(x => x.Entree.ArriveeLe).ThenBy(x => x.Entree.Id)
            .ToListAsync();
        var eleveIds = entrees.Select(x => x.Eleve.Id).Distinct().ToList();
        var inscrits = (await contexte.Inscriptions.Where(i => eleveIds.Contains(i.EleveId)).Select(i => i.EleveId).Distinct().ToListAsync()).ToHashSet();

        var valables = entrees.Where(x => !x.Annule).ToList();
        return new JourneeALaBibliotheque(
            le,
            centre.PrixDeLEntreeALaBibliotheque,
            centre.CapaciteDeLaBibliotheque,
            valables.Count(x => x.Entree.Depart is null),
            valables.Count,
            valables.Sum(x => x.Entree.Montant),
            entrees.Select(x => new EntreeDecrite(x.Entree.Id, x.Eleve.Id, x.Eleve.Prenom, x.Eleve.Nom, !inscrits.Contains(x.Eleve.Id),
                Heure(DateDeTunis.Heure(x.Entree.ArriveeLe)), x.Entree.Depart is { } depart ? Heure(depart) : null, x.Entree.RecuId, x.Numero, x.Annule))
                .ToList());
    }

    /// <summary>
    /// BIB-01 : enregistre et encaisse une entrée, pour un élève du centre ou une nouvelle fiche (une personne de
    /// l'extérieur), tout ou rien. Prévient sans bloquer d'une bibliothèque pleine, ou d'une entrée déjà notée aujourd'hui.
    /// </summary>
    public async Task<IssueDeLEntree> EntrerAsync(Compte auteur, EntreeSaisie saisie)
    {
        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId);
        if (centre.PrixDeLEntreeALaBibliotheque is not { } prix)
            return Refus(StatusCodes.Status409Conflict, PrixPasFixe);

        await using var transaction = await contexte.Database.BeginTransactionAsync();
        Eleve? eleve;
        if (saisie.EleveId is { } eleveId)
        {
            eleve = await contexte.Eleves.SingleOrDefaultAsync(e => e.Id == eleveId);
            if (eleve is null)
                return Refus(StatusCodes.Status404NotFound, ElevesDuCentre.EleveIntrouvable.Message!);
        }
        else
        {
            var fiche = await eleves.AjouterAsync(saisie.Fiche ?? new FicheSaisieDeLEleve(null, null, null, false, null, null, null, null, null));
            if (fiche.Fiche is null)
                return Refus(fiche.Statut, fiche.Message!);
            eleve = await contexte.Eleves.SingleAsync(e => e.Id == fiche.Fiche.Id);
        }

        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var avertissements = new List<string>();
        var duJour = await contexte.EntreesALaBibliotheque.Where(e => e.Jour == aujourdhui)
            .Join(contexte.Recus.Where(r => r.AnnuleLe == null), e => e.RecuId, r => r.Id, (e, r) => e)
            .ToListAsync();
        if (duJour.Any(e => e.EleveId == eleve.Id))
            avertissements.Add($"{eleve.Prenom} {eleve.Nom} a déjà une entrée aujourd'hui.");
        var presents = duJour.Count(e => e.Depart is null);
        if (centre.CapaciteDeLaBibliotheque is { } capacite && presents >= capacite)
            avertissements.Add($"La bibliothèque est pleine ({presents} / {capacite}) : l'entrée est quand même notée.");

        var recu = await recus.CreerUnRecuDeBibliothequeAsync(auteur, eleve, prix);
        var entree = new EntreeALaBibliotheque(eleve.Id, aujourdhui, recu.CreeLe, recu.Id, prix);
        contexte.EntreesALaBibliotheque.Add(entree);
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();

        return new IssueDeLEntree(
            new EntreeEnregistree(await DecrireAsync(entree), await recus.DecrireLeRecuAsync(recu, auteur), avertissements),
            StatusCodes.Status201Created, null);
    }

    /// <summary>BIB-02 : note l'heure de départ : maintenant, ou l'heure corrigée par la secrétaire (pas avant l'arrivée).</summary>
    public async Task<IssueDuDepart> NoterLeDepartAsync(Guid id, DepartSaisi saisie)
    {
        var entree = await contexte.EntreesALaBibliotheque.SingleOrDefaultAsync(e => e.Id == id);
        if (entree is null)
            return new IssueDuDepart(null, StatusCodes.Status404NotFound, EntreeIntrouvable);
        TimeOnly heure;
        if (string.IsNullOrWhiteSpace(saisie.Heure))
            heure = DateDeTunis.Heure(temps.GetUtcNow());
        else if (!TimeOnly.TryParseExact(saisie.Heure.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out heure))
            return new IssueDuDepart(null, StatusCodes.Status400BadRequest, HeureDeDepart);
        if (heure < DateDeTunis.Heure(entree.ArriveeLe))
            return new IssueDuDepart(null, StatusCodes.Status400BadRequest, HeureDeDepart);

        entree.NoterLeDepart(heure);
        await contexte.SaveChangesAsync();
        return new IssueDuDepart(await DecrireAsync(entree), StatusCodes.Status200OK, null);
    }

    private async Task<EntreeDecrite> DecrireAsync(EntreeALaBibliotheque entree)
    {
        var eleve = await contexte.Eleves.SingleAsync(e => e.Id == entree.EleveId);
        var recu = await contexte.Recus.SingleAsync(r => r.Id == entree.RecuId);
        var inscrit = await contexte.Inscriptions.AnyAsync(i => i.EleveId == eleve.Id);
        return new EntreeDecrite(entree.Id, eleve.Id, eleve.Prenom, eleve.Nom, !inscrit, Heure(DateDeTunis.Heure(entree.ArriveeLe)),
            entree.Depart is { } depart ? Heure(depart) : null, recu.Id, recu.Numero, recu.AnnuleLe is not null);
    }

    private static string Heure(TimeOnly heure) => heure.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static IssueDeLEntree Refus(int statut, string message) => new(null, statut, message);
}

/// <summary>BIB-01 : un élève du centre (<c>eleveId</c>), ou la fiche d'une personne de l'extérieur.</summary>
public sealed record EntreeSaisie(Guid? EleveId, FicheSaisieDeLEleve? Fiche);

/// <summary>BIB-02 : l'heure de départ « HH:mm » ; vide : maintenant.</summary>
public sealed record DepartSaisi(string? Heure);

/// <summary>
/// Une entrée dans la liste : l'élève, « extérieur » s'il n'a aucune inscription, l'arrivée et le départ (« 14:32 »),
/// le reçu, et s'il est annulé.
/// </summary>
public sealed record EntreeDecrite(
    Guid Id,
    Guid EleveId,
    string Prenom,
    string Nom,
    bool Exterieur,
    string Arrivee,
    string? Depart,
    Guid RecuId,
    int Numero,
    bool Annule);

/// <summary>
/// BIB-02 et BIB-03 : la liste d'un jour : le prix et la capacité, les présents en ce moment, le nombre d'entrées et
/// l'encaissé (sans les reçus annulés), et chaque entrée.
/// </summary>
public sealed record JourneeALaBibliotheque(
    DateOnly Jour,
    decimal? Prix,
    int? Capacite,
    int Presents,
    int Entrees,
    decimal Encaisse,
    List<EntreeDecrite> Liste);

/// <summary>BIB-01 : l'entrée notée, son reçu, et les avertissements (déjà venu aujourd'hui, bibliothèque pleine).</summary>
public sealed record EntreeEnregistree(EntreeDecrite Entree, RecuDecrit Recu, List<string> Avertissements);

public sealed record IssueDeLEntree(EntreeEnregistree? Enregistree, int Statut, string? Message);

public sealed record IssueDuDepart(EntreeDecrite? Entree, int Statut, string? Message);
