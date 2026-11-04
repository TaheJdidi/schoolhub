using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Socle.Centres;

/// <summary>
/// CEN-03 : la liste des niveaux du centre courant. Le filtre automatique ne montre que ceux du centre :
/// un niveau d'un autre centre est « introuvable ». Les niveaux d'office ne se renomment ni ne se retirent.
/// </summary>
public sealed class NiveauxDuCentre(SchoolHubDbContext contexte)
{
    private const string NomObligatoire = "Le nom du niveau est obligatoire";
    private const string NomTropLong = "Le nom du niveau fait au plus 100 caractères";
    private const string ExisteDeja = "Ce niveau existe déjà";
    private const string NiveauDOffice = "Les niveaux d'office ne se modifient pas";
    private const string Introuvable = "Niveau introuvable";
    private const string NiveauUtilise = "Ce niveau est utilisé par un groupe ou un élève";

    /// <summary>Les niveaux d'office dans l'ordre scolaire, puis les niveaux ajoutés, par nom.</summary>
    public Task<List<Niveau>> ListerAsync() =>
        contexte.Niveaux
            .OrderByDescending(n => n.DOffice)
            .ThenBy(n => n.Rang)
            .ThenBy(n => n.Nom)
            .ToListAsync();

    /// <summary>Ajoute les 29 niveaux d'office au centre courant, sans enregistrer : ils partent avec la création du centre.</summary>
    public void RecopierLesNiveauxDOffice() =>
        contexte.Niveaux.AddRange(NiveauxDOffice.Liste.Select((nom, i) => new Niveau(nom, dOffice: true, rang: i + 1)));

    public async Task<IssueSurUnNiveau> AjouterAsync(string? saisie)
    {
        var (nom, refus) = VerifierLeNom(saisie);
        if (refus is not null)
            return refus;
        if (await ExisteAsync(nom, sauf: null))
            return IssueSurUnNiveau.Refusee(StatusCodes.Status409Conflict, ExisteDeja);

        var niveau = new Niveau(nom, dOffice: false, rang: null);
        contexte.Niveaux.Add(niveau);
        return await EnregistrerAsync(niveau, StatusCodes.Status201Created);
    }

    public async Task<IssueSurUnNiveau> RenommerAsync(Guid id, string? saisie)
    {
        var (niveau, interdit) = await TrouverUnNiveauAjouteAsync(id);
        if (interdit is not null)
            return interdit;

        var (nom, refus) = VerifierLeNom(saisie);
        if (refus is not null)
            return refus;
        // Le niveau lui-même ne compte pas : changer ses majuscules n'en fait pas un doublon.
        if (await ExisteAsync(nom, sauf: id))
            return IssueSurUnNiveau.Refusee(StatusCodes.Status409Conflict, ExisteDeja);

        niveau!.Renommer(nom);
        return await EnregistrerAsync(niveau, StatusCodes.Status200OK);
    }

    /// <summary>
    /// Retire un niveau ajouté, si aucun groupe ne l'utilise. Les élèves n'existent pas encore : ELV-02 ajoutera
    /// leur niveau à ce refus.
    /// </summary>
    public async Task<IssueSurUnNiveau> RetirerAsync(Guid id)
    {
        var (niveau, interdit) = await TrouverUnNiveauAjouteAsync(id);
        if (interdit is not null)
            return interdit;
        if (await contexte.Groupes.AnyAsync(g => g.NiveauId == id))
            return IssueSurUnNiveau.Refusee(StatusCodes.Status409Conflict, NiveauUtilise);

        contexte.Niveaux.Remove(niveau!);
        await contexte.SaveChangesAsync();
        return IssueSurUnNiveau.Reussie(niveau!, StatusCodes.Status204NoContent);
    }

    /// <summary>Le niveau de ce centre, s'il peut changer ; sinon le refus : introuvable, ou d'office.</summary>
    private async Task<(Niveau? Niveau, IssueSurUnNiveau? Interdit)> TrouverUnNiveauAjouteAsync(Guid id)
    {
        var niveau = await contexte.Niveaux.SingleOrDefaultAsync(n => n.Id == id);
        if (niveau is null)
            return (null, IssueSurUnNiveau.Refusee(StatusCodes.Status404NotFound, Introuvable));
        if (niveau.DOffice)
            return (null, IssueSurUnNiveau.Refusee(StatusCodes.Status409Conflict, NiveauDOffice));
        return (niveau, null);
    }

    private static (string Nom, IssueSurUnNiveau? Refus) VerifierLeNom(string? saisie)
    {
        var nom = saisie?.Trim() ?? "";
        if (nom.Length == 0)
            return (nom, IssueSurUnNiveau.Refusee(StatusCodes.Status400BadRequest, NomObligatoire));
        // La forme normalisée peut s'allonger (« ﷺ » donne 18 caractères) : elle doit tenir dans sa colonne elle aussi.
        if (nom.Length > Niveau.LongueurMaximaleDuNom || Niveau.Normaliser(nom).Length > Niveau.LongueurMaximaleDuNom)
            return (nom, IssueSurUnNiveau.Refusee(StatusCodes.Status400BadRequest, NomTropLong));
        return (nom, null);
    }

    /// <summary>Un autre niveau du centre, d'office compris, a-t-il déjà ce nom, majuscules mises à part ?</summary>
    private Task<bool> ExisteAsync(string nom, Guid? sauf)
    {
        var normalise = Niveau.Normaliser(nom);
        return contexte.Niveaux.AnyAsync(n => n.NomNormalise == normalise && n.Id != sauf);
    }

    /// <summary>Enregistre ; deux ajouts du même nom à la même seconde butent sur l'index unique, et le second est refusé.</summary>
    private async Task<IssueSurUnNiveau> EnregistrerAsync(Niveau niveau, int statut)
    {
        try
        {
            await contexte.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return IssueSurUnNiveau.Refusee(StatusCodes.Status409Conflict, ExisteDeja);
        }

        return IssueSurUnNiveau.Reussie(niveau, statut);
    }
}

/// <summary>Ce qu'une action sur un niveau a donné : le niveau et le statut HTTP, ou un refus (statut et message).</summary>
public sealed record IssueSurUnNiveau(Niveau? Niveau, int Statut, string? Message)
{
    public static IssueSurUnNiveau Reussie(Niveau niveau, int statut = StatusCodes.Status200OK) => new(niveau, statut, null);

    public static IssueSurUnNiveau Refusee(int statut, string message) => new(null, statut, message);
}
