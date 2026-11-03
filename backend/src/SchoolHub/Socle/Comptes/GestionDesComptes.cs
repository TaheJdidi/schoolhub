using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// CPT-04 et CPT-05 : les comptes du centre courant, et ce que le propriétaire en fait.
/// Les comptes ne passent pas par le filtre automatique (on les lit à la connexion, avant de connaître le centre) :
/// chaque lecture filtre donc elle-même sur le centre courant. Un compte d'un autre centre est « introuvable ».
/// </summary>
public sealed class GestionDesComptes(
    SchoolHubDbContext contexte,
    UserManager<Compte> comptes,
    Invitations invitations,
    JournalDuCentre journal,
    CentreCourant centreCourant,
    ILogger<GestionDesComptes> journaux)
{
    private const string ChampsObligatoires = "Le prénom, le nom et le rôle sont obligatoires";
    private const string NomTropLong = "Le prénom et le nom font au plus 100 caractères";
    private const string EmailPasValable = "Cet email n'est pas valable";
    private const string EmailDejaUtilise = "Cet email est déjà utilisé sur SchoolHub";
    private const string InvitationPasPartie = "L'invitation n'est pas partie : réessayez dans un moment";
    private const string Introuvable = "Compte introuvable";
    private const string PlusDInvitation = "Ce compte n'attend plus d'invitation";
    private const string PasSoiMeme = "Vous ne pouvez pas désactiver votre propre compte";
    private const string DejaDesactive = "Ce compte est déjà désactivé";
    private const string PasDesactive = "Ce compte n'est pas désactivé";
    private const string VientDeChanger = "Ce compte vient de changer : rechargez la page";
    private const string DernierProprietaire = "Il doit rester au moins un propriétaire actif";

    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    /// <summary>Les comptes du centre : actifs et invités d'abord, puis les désactivés, chacun par prénom puis par nom.</summary>
    public Task<List<Compte>> ListerAsync() =>
        contexte.Users
            .Where(c => c.CentreId == CentreId)
            .OrderBy(c => c.Etat == EtatDuCompte.Desactive)
            .ThenBy(c => c.Prenom)
            .ThenBy(c => c.Nom)
            .ToListAsync();

    /// <summary>
    /// Crée le compte invité et lui envoie son invitation. Tout ou rien : le compte, la ligne du journal
    /// et l'email forment une seule transaction, validée seulement une fois l'email parti.
    /// </summary>
    public async Task<IssueDeLAction> InviterAsync(Compte auteur, NouvelleInvitation invitation, CancellationToken annulation = default)
    {
        var prenom = invitation.Prenom?.Trim() ?? "";
        var nom = invitation.Nom?.Trim() ?? "";
        var email = invitation.Email?.Trim() ?? "";

        // Seuls les noms exacts : Enum.TryParse accepterait aussi « 0 », « 1 » ou « proprietaire ».
        if (prenom.Length == 0 || nom.Length == 0 || !Enum.GetNames<RoleDuCompte>().Contains(invitation.Role))
            return IssueDeLAction.Refusee(StatusCodes.Status400BadRequest, ChampsObligatoires);
        var role = Enum.Parse<RoleDuCompte>(invitation.Role!);

        if (prenom.Length > Compte.LongueurMaximaleDuNom || nom.Length > Compte.LongueurMaximaleDuNom)
            return IssueDeLAction.Refusee(StatusCodes.Status400BadRequest, NomTropLong);

        if (!AdresseEmail.EstValable(email))
            return IssueDeLAction.Refusee(StatusCodes.Status400BadRequest, EmailPasValable);

        // Identity compare les emails sans tenir compte de la casse, dans tous les centres.
        if (await comptes.FindByEmailAsync(email) is not null)
            return IssueDeLAction.Refusee(StatusCodes.Status409Conflict, EmailDejaUtilise);

        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId, annulation);
        await using var transaction = await contexte.Database.BeginTransactionAsync(annulation);

        var compte = new Compte(CentreId, prenom, nom, email, role);
        try
        {
            // Sans mot de passe, la création ne peut échouer que sur un email déjà pris :
            // Identity le voit (DuplicateEmail), ou l'index unique quand deux invitations arrivent ensemble.
            if (!(await comptes.CreateAsync(compte)).Succeeded)
                return IssueDeLAction.Refusee(StatusCodes.Status409Conflict, EmailDejaUtilise);
        }
        catch (DbUpdateException)
        {
            return IssueDeLAction.Refusee(StatusCodes.Status409Conflict, EmailDejaUtilise);
        }

        journal.Noter(auteur, ActionDuJournal.CompteCree, $"{prenom} {nom} · {LibellesDesComptes.De(role)} · {email}");
        await contexte.SaveChangesAsync(annulation);

        try
        {
            await invitations.EnvoyerAsync(compte, centre, annulation);
        }
        catch (Exception exception)
        {
            journaux.LogError(exception, "L'invitation de {Email} n'est pas partie : rien n'est créé", email);
            await transaction.RollbackAsync(CancellationToken.None);
            return IssueDeLAction.Refusee(StatusCodes.Status503ServiceUnavailable, InvitationPasPartie);
        }

        // L'email est parti : le compte vers lequel pointe son lien doit exister, même si l'onglet vient de se fermer.
        await transaction.CommitAsync(CancellationToken.None);
        return IssueDeLAction.Reussie(compte);
    }

    /// <summary>
    /// Envoie un nouveau lien à un compte encore invité. Le tampon de sécurité change d'abord, ce qui rend l'ancien lien
    /// caduc ; si l'email ne part pas, rien ne change et l'ancien lien marche encore. Ce n'est pas noté au journal.
    /// </summary>
    public async Task<IssueDeLAction> RenvoyerLInvitationAsync(Guid id, CancellationToken annulation = default)
    {
        var compte = await TrouverAsync(id);
        if (compte is null)
            return IssueDeLAction.Refusee(StatusCodes.Status404NotFound, Introuvable);
        if (compte.Etat != EtatDuCompte.Invite)
            return IssueDeLAction.Refusee(StatusCodes.Status409Conflict, PlusDInvitation);

        var centre = await contexte.Centres.SingleAsync(c => c.Id == CentreId, annulation);
        await using var transaction = await contexte.Database.BeginTransactionAsync(annulation);

        // Une autre requête a pu changer le compte depuis sa lecture (un autre renvoi, une désactivation) :
        // le nouveau tampon n'est alors pas enregistré, et un lien fabriqué avec lui serait mort.
        if (!(await comptes.UpdateSecurityStampAsync(compte)).Succeeded)
            return IssueDeLAction.Refusee(StatusCodes.Status409Conflict, VientDeChanger);

        try
        {
            await invitations.EnvoyerAsync(compte, centre, annulation);
        }
        catch (Exception exception)
        {
            journaux.LogError(exception, "La nouvelle invitation de {Email} n'est pas partie : l'ancien lien reste valable", compte.Email);
            await transaction.RollbackAsync(CancellationToken.None);
            return IssueDeLAction.Refusee(StatusCodes.Status503ServiceUnavailable, InvitationPasPartie);
        }

        // L'email est parti : son nouveau tampon doit être enregistré, même si l'onglet vient de se fermer.
        await transaction.CommitAsync(CancellationToken.None);
        return IssueDeLAction.Reussie(compte);
    }

    /// <summary>
    /// Désactive un compte actif ou invité, jamais le sien. Son tampon de sécurité change : ses liens d'email ne marchent plus,
    /// et sa session tombe à sa prochaine requête. Il reste toujours un propriétaire actif : les désactivations d'un centre
    /// passent l'une après l'autre, et celui qui clique est relu une fois son tour venu.
    /// </summary>
    public async Task<IssueDeLAction> DesactiverAsync(Compte auteur, Guid id, CancellationToken annulation = default)
    {
        if (id == auteur.Id)
            return IssueDeLAction.Refusee(StatusCodes.Status400BadRequest, PasSoiMeme);

        await using var transaction = await contexte.Database.BeginTransactionAsync(annulation);

        // Le verrou de la ligne du centre, jusqu'à la fin de la transaction : si Hedi et Mehdi se désactivent
        // l'un l'autre à la même seconde, le second attend que le premier ait fini.
        await contexte.Database.ExecuteSqlAsync($"SELECT 1 FROM centres WHERE id = {CentreId} FOR UPDATE", annulation);

        // Relu sans suivi : le compte connecté est déjà suivi depuis la validation de la session, avec son état d'alors.
        if (!await contexte.Users.AsNoTracking().AnyAsync(c => c.Id == auteur.Id && c.Etat == EtatDuCompte.Actif, annulation))
            return IssueDeLAction.Refusee(StatusCodes.Status403Forbidden, ConnexionEndpoints.CompteDesactive);

        var compte = await TrouverAsync(id);
        if (compte is null)
            return IssueDeLAction.Refusee(StatusCodes.Status404NotFound, Introuvable);
        if (compte.Etat == EtatDuCompte.Desactive)
            return IssueDeLAction.Refusee(StatusCodes.Status409Conflict, DejaDesactive);

        var unAutreProprietaireReste = await contexte.Users.AnyAsync(
            c => c.CentreId == CentreId && c.Id != compte.Id && c.Role == RoleDuCompte.Proprietaire && c.Etat == EtatDuCompte.Actif,
            annulation);
        if (compte.Role == RoleDuCompte.Proprietaire && !unAutreProprietaireReste)
            return IssueDeLAction.Refusee(StatusCodes.Status409Conflict, DernierProprietaire);

        journal.Noter(
            auteur,
            ActionDuJournal.CompteDesactive,
            Designation(compte),
            avant: LibellesDesComptes.De(compte.Etat),
            apres: LibellesDesComptes.De(EtatDuCompte.Desactive));
        compte.Desactiver();

        // Le changement de tampon enregistre le compte, et la ligne du journal avec lui.
        VerifierLEnregistrement(await comptes.UpdateSecurityStampAsync(compte));
        await transaction.CommitAsync(annulation);
        return IssueDeLAction.Reussie(compte);
    }

    /// <summary>
    /// Réactive un compte désactivé : actif avec son ancien mot de passe, ou invité s'il ne l'avait jamais choisi.
    /// Un invité réactivé n'a pas de lien valable : le propriétaire lui renvoie l'invitation.
    /// </summary>
    public async Task<IssueDeLAction> ReactiverAsync(Compte auteur, Guid id)
    {
        var compte = await TrouverAsync(id);
        if (compte is null)
            return IssueDeLAction.Refusee(StatusCodes.Status404NotFound, Introuvable);
        if (compte.Etat != EtatDuCompte.Desactive)
            return IssueDeLAction.Refusee(StatusCodes.Status409Conflict, PasDesactive);

        compte.Reactiver();
        journal.Noter(
            auteur,
            ActionDuJournal.CompteReactive,
            Designation(compte),
            avant: LibellesDesComptes.De(EtatDuCompte.Desactive),
            apres: LibellesDesComptes.De(compte.Etat));
        VerifierLEnregistrement(await comptes.UpdateAsync(compte));
        return IssueDeLAction.Reussie(compte);
    }

    /// <summary>Le compte tel que le journal le nomme : « Sonia Mabrouk · Secrétaire ».</summary>
    private static string Designation(Compte compte) => $"{compte.Prenom} {compte.Nom} · {LibellesDesComptes.De(compte.Role)}";

    /// <summary>
    /// Un compte qui ne s'enregistre pas a été modifié par une autre requête entre sa lecture et son enregistrement :
    /// l'erreur remonte, et la transaction est annulée.
    /// </summary>
    private static void VerifierLEnregistrement(IdentityResult resultat)
    {
        if (!resultat.Succeeded)
            throw new InvalidOperationException(
                "Le compte n'a pas pu être enregistré : " + string.Join(" ", resultat.Errors.Select(e => e.Description)));
    }

    /// <summary>Le compte de ce centre qui a cet identifiant, ou <c>null</c> : un compte d'un autre centre n'est pas trouvé.</summary>
    private Task<Compte?> TrouverAsync(Guid id) =>
        contexte.Users.SingleOrDefaultAsync(c => c.Id == id && c.CentreId == CentreId);
}

/// <summary>Ce que le propriétaire saisit pour inviter une personne. Un champ vide reçoit un refus.</summary>
public sealed record NouvelleInvitation(string? Prenom, string? Nom, string? Email, string? Role);

/// <summary>Ce qu'une action sur un compte a donné : le compte à jour, ou un refus (statut HTTP et message).</summary>
public sealed record IssueDeLAction(Compte? Compte, int Statut, string? Message)
{
    public static IssueDeLAction Reussie(Compte compte) => new(compte, StatusCodes.Status200OK, null);

    public static IssueDeLAction Refusee(int statut, string message) => new(null, statut, message);
}

/// <summary>Les mots que lit le propriétaire, dans le journal et sur la page « Comptes ».</summary>
public static class LibellesDesComptes
{
    public static string De(RoleDuCompte role) => role switch
    {
        RoleDuCompte.Proprietaire => "Propriétaire",
        RoleDuCompte.Secretaire => "Secrétaire",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static string De(EtatDuCompte etat) => etat switch
    {
        EtatDuCompte.Invite => "Invité",
        EtatDuCompte.Actif => "Actif",
        EtatDuCompte.Desactive => "Désactivé",
        _ => throw new ArgumentOutOfRangeException(nameof(etat)),
    };
}
