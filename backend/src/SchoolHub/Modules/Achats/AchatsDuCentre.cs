using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;

namespace SchoolHub.Modules.Achats;

/// <summary>
/// RHA-04 à RHA-06 : les achats du centre courant. Un achat « payé depuis ma caisse » est une sortie de caisse : il va au
/// journal et baisse les espèces attendues de la caisse de son auteur. L'auteur modifie ou supprime son achat le jour même,
/// tant que sa caisse n'est pas clôturée ; ensuite, seul le propriétaire le peut.
/// </summary>
public sealed class AchatsDuCentre(SchoolHubDbContext contexte, JournalDuCentre journal, TimeProvider temps)
{
    public const string AchatIntrouvable = "Achat introuvable";
    private const string ChoisirLaDate = "Choisissez la date";
    private const string LibelleObligatoire = "Écrivez le libellé";
    private const string LibelleTropLong = "Le libellé fait au plus 200 caractères";
    private const string ChoisirLaCategorie = "Choisissez la catégorie";
    private const string MontantPasValable = "Le montant est positif, au millime près";
    private const string FournisseurTropLong = "Le fournisseur fait au plus 200 caractères";
    private const string SortieDuJour = "Une sortie de caisse se note le jour même";
    private const string DAvantAujourdhui = "Cet achat date d'avant aujourd'hui : seul le propriétaire peut le changer";
    private const string DUneAutrePersonne = "Cet achat a été saisi par une autre personne : seul le propriétaire peut le changer";
    private const string CaisseCloturee = "Votre caisse est clôturée : seul le propriétaire peut changer cet achat";
    public const string FacturePasValable = "La facture est une photo (JPEG, PNG, WebP) ou un PDF de 5 Mo au plus";

    private static readonly HashSet<string> TypesDeFacture = ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    /// <summary>RHA-06 : les achats d'un mois (par défaut le mois en cours) ou d'une année, et d'une catégorie, avec les totaux.</summary>
    public async Task<AchatsEtTotaux> ListerAsync(Compte lecteur, string? mois, int? annee, string? categorie)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        DateOnly du, au;
        if (annee is { } a)
            (du, au) = (new DateOnly(a, 1, 1), new DateOnly(a, 12, 31));
        else
        {
            var premier = mois is not null && DateOnly.TryParseExact($"{mois}-01", "yyyy-MM-dd", out var lu) ? lu : new DateOnly(aujourdhui.Year, aujourdhui.Month, 1);
            (du, au) = (premier, premier.AddMonths(1).AddDays(-1));
        }

        var requete = contexte.Achats.Where(x => x.Jour >= du && x.Jour <= au);
        if (Enum.TryParse<CategorieDAchat>(categorie, out var laCategorie) && Enum.IsDefined(laCategorie))
            requete = requete.Where(x => x.Categorie == laCategorie);
        var achats = await requete.OrderByDescending(x => x.Jour).ThenByDescending(x => x.CreeLe).ToListAsync();
        var ids = achats.Select(x => x.Id).ToList();
        var avecFacture = (await contexte.FacturesDAchat.Where(f => ids.Contains(f.AchatId)).Select(f => f.AchatId).ToListAsync()).ToHashSet();
        var clotures = await CloturesDuJourAsync(achats.Select(x => x.AuteurId).Distinct().ToList(), aujourdhui);

        var materiel = achats.Where(x => x.Categorie == CategorieDAchat.Materiel).Sum(x => x.Montant);
        var service = achats.Where(x => x.Categorie == CategorieDAchat.Service).Sum(x => x.Montant);
        return new AchatsEtTotaux(achats.Select(x => Decrire(x, lecteur, avecFacture.Contains(x.Id), clotures)).ToList(), materiel, service, materiel + service);
    }

    /// <summary>RHA-04 : enregistre un achat ; une sortie de caisse va au journal.</summary>
    public async Task<IssueDeLAchat> AjouterAsync(Compte auteur, AchatSaisi saisie)
    {
        var (valeurs, refus) = Lire(saisie);
        if (refus is not null)
            return refus;
        if (valeurs!.PayeDepuisLaCaisse && valeurs.Jour != DateDeTunis.Aujourdhui(temps))
            return Refus(StatusCodes.Status400BadRequest, SortieDuJour);

        var achat = new Achat(valeurs.Jour, valeurs.Libelle, valeurs.Categorie, valeurs.Montant, valeurs.Fournisseur, valeurs.PayeDepuisLaCaisse, auteur.Id,
            $"{auteur.Prenom} {auteur.Nom}", temps.GetUtcNow());
        contexte.Achats.Add(achat);
        if (achat.PayeDepuisLaCaisse)
            NoterLaSortie(auteur, achat, null, $"Sortie, {SaisieDuGroupe.EcrireUnMontant(achat.Montant)} TND");
        await contexte.SaveChangesAsync();
        return new IssueDeLAchat(await DecrireAsync(achat, auteur), StatusCodes.Status201Created, null);
    }

    /// <summary>RHA-04 : modifie un achat (l'auteur le jour même avant sa clôture, ou le propriétaire).</summary>
    public async Task<IssueDeLAchat> ModifierAsync(Compte auteur, Guid id, AchatSaisi saisie)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var achat = await AchatVerrouilleAsync(id);
        if (achat is null)
            return Refus(StatusCodes.Status404NotFound, AchatIntrouvable);
        if (await PourquoiPasModifiableAsync(achat, auteur) is { } raison)
            return Refus(StatusCodes.Status403Forbidden, raison);
        var (valeurs, refus) = Lire(saisie);
        if (refus is not null)
            return refus;
        var devientSortie = valeurs!.PayeDepuisLaCaisse && achat.ClotureId is null;
        if (devientSortie && valeurs.Jour != DateDeTunis.Aujourdhui(temps))
            return Refus(StatusCodes.Status400BadRequest, SortieDuJour);

        var avant = achat.PayeDepuisLaCaisse ? $"Sortie, {SaisieDuGroupe.EcrireUnMontant(achat.Montant)} TND" : "Hors caisse";
        var etaitSortie = achat.PayeDepuisLaCaisse;
        achat.Modifier(valeurs.Jour, valeurs.Libelle, valeurs.Categorie, valeurs.Montant, valeurs.Fournisseur, valeurs.PayeDepuisLaCaisse);
        if (etaitSortie || achat.PayeDepuisLaCaisse)
            NoterLaSortie(auteur, achat, avant, achat.PayeDepuisLaCaisse ? $"Sortie, {SaisieDuGroupe.EcrireUnMontant(achat.Montant)} TND" : "Hors caisse");
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueDeLAchat(await DecrireAsync(achat, auteur), StatusCodes.Status200OK, null);
    }

    /// <summary>RHA-04 : supprime un achat saisi par erreur (mêmes droits que pour le modifier) ; une sortie va au journal.</summary>
    public async Task<IssueDeLAchat> SupprimerAsync(Compte auteur, Guid id)
    {
        await using var transaction = await contexte.Database.BeginTransactionAsync();
        var achat = await AchatVerrouilleAsync(id);
        if (achat is null)
            return Refus(StatusCodes.Status404NotFound, AchatIntrouvable);
        if (await PourquoiPasModifiableAsync(achat, auteur) is { } raison)
            return Refus(StatusCodes.Status403Forbidden, raison);
        if (achat.PayeDepuisLaCaisse)
            NoterLaSortie(auteur, achat, $"Sortie, {SaisieDuGroupe.EcrireUnMontant(achat.Montant)} TND", "Supprimée");
        contexte.Achats.Remove(achat);
        await contexte.SaveChangesAsync();
        await transaction.CommitAsync();
        return new IssueDeLAchat(null, StatusCodes.Status204NoContent, null);
    }

    /// <summary>RHA-04 : joint (ou remplace) la facture d'un achat : une photo ou un PDF de 5 Mo au plus.</summary>
    public async Task<IssueDeLAchat> JoindreLaFactureAsync(Compte auteur, Guid id, IFormFile? fichier)
    {
        var achat = await contexte.Achats.SingleOrDefaultAsync(x => x.Id == id);
        if (achat is null)
            return Refus(StatusCodes.Status404NotFound, AchatIntrouvable);
        if (await PourquoiPasModifiableAsync(achat, auteur) is { } raison)
            return Refus(StatusCodes.Status403Forbidden, raison);
        if (fichier is null || fichier.Length == 0 || fichier.Length > FactureDAchat.TailleMaximale || !TypesDeFacture.Contains(fichier.ContentType))
            return Refus(StatusCodes.Status400BadRequest, FacturePasValable);

        using var flux = new MemoryStream();
        await fichier.CopyToAsync(flux);
        contexte.FacturesDAchat.RemoveRange(await contexte.FacturesDAchat.Where(f => f.AchatId == id).ToListAsync());
        var nom = Path.GetFileName(fichier.FileName);
        contexte.FacturesDAchat.Add(new FactureDAchat(id, nom.Length > 200 ? nom[..200] : nom, fichier.ContentType, flux.ToArray()));
        await contexte.SaveChangesAsync();
        return new IssueDeLAchat(await DecrireAsync(achat, auteur), StatusCodes.Status200OK, null);
    }

    /// <summary>RHA-04 : retire la facture d'un achat.</summary>
    public async Task<IssueDeLAchat> RetirerLaFactureAsync(Compte auteur, Guid id)
    {
        var achat = await contexte.Achats.SingleOrDefaultAsync(x => x.Id == id);
        if (achat is null)
            return Refus(StatusCodes.Status404NotFound, AchatIntrouvable);
        if (await PourquoiPasModifiableAsync(achat, auteur) is { } raison)
            return Refus(StatusCodes.Status403Forbidden, raison);
        contexte.FacturesDAchat.RemoveRange(await contexte.FacturesDAchat.Where(f => f.AchatId == id).ToListAsync());
        await contexte.SaveChangesAsync();
        return new IssueDeLAchat(await DecrireAsync(achat, auteur), StatusCodes.Status200OK, null);
    }

    /// <summary>
    /// L'achat, une fois verrouillée la caisse de son auteur, comme la clôture (CAI-09) : un changement et une clôture
    /// passent l'une après l'autre, et la clôture ne fige jamais une sortie changée ou supprimée pendant qu'elle se fait.
    /// </summary>
    private async Task<Achat?> AchatVerrouilleAsync(Guid id)
    {
        if (await contexte.Achats.Where(x => x.Id == id).Select(x => (Guid?)x.AuteurId).SingleOrDefaultAsync() is not { } auteurId)
            return null;
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM comptes WHERE id = {auteurId} FOR NO KEY UPDATE");
        return await contexte.Achats.SingleOrDefaultAsync(x => x.Id == id);
    }

    /// <summary>La facture d'un achat de ce centre ; <c>null</c> s'il n'y en a pas.</summary>
    public Task<FactureDAchat?> FactureAsync(Guid id) => contexte.FacturesDAchat.SingleOrDefaultAsync(f => f.AchatId == id);

    /// <summary>
    /// Pourquoi ce compte ne peut pas changer cet achat ; <c>null</c> s'il le peut. Le propriétaire peut toujours ; l'auteur,
    /// le jour même, tant qu'il n'a pas clôturé de caisse depuis l'achat.
    /// </summary>
    private async Task<string?> PourquoiPasModifiableAsync(Achat achat, Compte compte)
    {
        if (compte.Role == RoleDuCompte.Proprietaire)
            return null;
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var clotures = await CloturesDuJourAsync([achat.AuteurId], aujourdhui);
        return PourquoiPas(achat, compte, aujourdhui, clotures);
    }

    private static string? PourquoiPas(Achat achat, Compte compte, DateOnly aujourdhui, IReadOnlyDictionary<Guid, DateTimeOffset> clotures)
    {
        if (compte.Role == RoleDuCompte.Proprietaire)
            return null;
        if (achat.AuteurId != compte.Id)
            return DUneAutrePersonne;
        // Le jour même : celui où l'achat a été saisi.
        if (DateDeTunis.Jour(achat.CreeLe) != aujourdhui)
            return DAvantAujourdhui;
        if (achat.ClotureId is not null || (clotures.TryGetValue(achat.AuteurId, out var derniere) && derniere >= achat.CreeLe))
            return CaisseCloturee;
        return null;
    }

    /// <summary>La dernière clôture du jour de chaque compte : un achat saisi avant elle est « clôturé ».</summary>
    private async Task<Dictionary<Guid, DateTimeOffset>> CloturesDuJourAsync(List<Guid> comptes, DateOnly aujourdhui) =>
        await contexte.CloturesDeCaisse.Where(c => comptes.Contains(c.CompteId) && c.Jour == aujourdhui)
            .GroupBy(c => c.CompteId)
            .Select(g => new { CompteId = g.Key, Derniere = g.Max(c => c.ClotureeLe) })
            .ToDictionaryAsync(g => g.CompteId, g => g.Derniere);

    private void NoterLaSortie(Compte auteur, Achat achat, string? avant, string apres)
    {
        var detail = $"{achat.Libelle} · {(achat.Categorie == CategorieDAchat.Materiel ? "Matériel" : "Service")}";
        journal.Noter(auteur, ActionDuJournal.SortieDeCaisse, detail.Length > 500 ? detail[..500] : detail, avant, apres);
    }

    private async Task<AchatDecrit> DecrireAsync(Achat achat, Compte lecteur)
    {
        var aUneFacture = await contexte.FacturesDAchat.AnyAsync(f => f.AchatId == achat.Id);
        var clotures = await CloturesDuJourAsync([achat.AuteurId], DateDeTunis.Aujourdhui(temps));
        return Decrire(achat, lecteur, aUneFacture, clotures);
    }

    private AchatDecrit Decrire(Achat achat, Compte lecteur, bool aUneFacture, IReadOnlyDictionary<Guid, DateTimeOffset> clotures)
    {
        var pourquoi = PourquoiPas(achat, lecteur, DateDeTunis.Aujourdhui(temps), clotures);
        return new AchatDecrit(achat.Id, achat.Jour, achat.Libelle, achat.Categorie.ToString(), achat.Montant, achat.Fournisseur, achat.PayeDepuisLaCaisse,
            achat.Auteur, aUneFacture, pourquoi is null, pourquoi);
    }

    /// <summary>Les champs dans l'ordre du formulaire, sans les espaces autour : le premier refusé donne la réponse.</summary>
    private static (ValeursDeLAchat? Valeurs, IssueDeLAchat? Refus) Lire(AchatSaisi saisie)
    {
        if (saisie.Jour is not { } jour)
            return (null, Refus(StatusCodes.Status400BadRequest, ChoisirLaDate));
        var libelle = saisie.Libelle?.Trim() ?? "";
        if (libelle.Length == 0)
            return (null, Refus(StatusCodes.Status400BadRequest, LibelleObligatoire));
        if (libelle.Length > Achat.LongueurMaximaleDuLibelle)
            return (null, Refus(StatusCodes.Status400BadRequest, LibelleTropLong));
        if (!Enum.TryParse<CategorieDAchat>(saisie.Categorie?.Trim(), out var categorie) || !Enum.IsDefined(categorie)
            || int.TryParse(saisie.Categorie, out _))
            return (null, Refus(StatusCodes.Status400BadRequest, ChoisirLaCategorie));
        if (saisie.Montant is not { } montant || montant <= 0 || decimal.Round(montant, 3) != montant || montant > 999_999_999m)
            return (null, Refus(StatusCodes.Status400BadRequest, MontantPasValable));
        var fournisseur = string.IsNullOrWhiteSpace(saisie.Fournisseur) ? null : saisie.Fournisseur.Trim();
        if (fournisseur is { Length: > Achat.LongueurMaximaleDuLibelle })
            return (null, Refus(StatusCodes.Status400BadRequest, FournisseurTropLong));
        return (new ValeursDeLAchat(jour, libelle, categorie, montant, fournisseur, saisie.PayeDepuisLaCaisse), null);
    }

    private static IssueDeLAchat Refus(int statut, string message) => new(null, statut, message);

    private sealed record ValeursDeLAchat(DateOnly Jour, string Libelle, CategorieDAchat Categorie, decimal Montant, string? Fournisseur, bool PayeDepuisLaCaisse);
}

/// <summary>RHA-04 : ce que la page envoie pour un achat.</summary>
public sealed record AchatSaisi(DateOnly? Jour, string? Libelle, string? Categorie, decimal? Montant, string? Fournisseur, bool PayeDepuisLaCaisse);

/// <summary>Un achat, tel que la liste le montre ; s'il peut être changé par la personne connectée, ou pourquoi pas.</summary>
public sealed record AchatDecrit(
    Guid Id,
    DateOnly Jour,
    string Libelle,
    string Categorie,
    decimal Montant,
    string? Fournisseur,
    bool PayeDepuisLaCaisse,
    string Auteur,
    bool AUneFacture,
    bool Modifiable,
    string? PourquoiPasModifiable);

/// <summary>RHA-06 : les achats et leurs totaux.</summary>
public sealed record AchatsEtTotaux(List<AchatDecrit> Achats, decimal TotalMateriel, decimal TotalService, decimal Total);

public sealed record IssueDeLAchat(AchatDecrit? Achat, int Statut, string? Message);
