using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Socle.Centres;

/// <summary>Ce qu'il faut pour ouvrir un centre : son nom, son code, et son premier propriétaire.</summary>
public sealed record NouveauCentre(
    string Nom,
    string Code,
    string PrenomDuProprietaire,
    string NomDuProprietaire,
    string EmailDuProprietaire);

/// <summary>Ce qui s'est passé : le message est destiné à la personne qui lance la commande.</summary>
public sealed record ResultatDeCreation(bool Reussi, string Message);

/// <summary>
/// CEN-01 : crée un centre et son premier propriétaire (invité), puis lui envoie son invitation.
/// Tout ou rien : le centre, le compte et l'envoi de l'email forment une seule transaction,
/// validée seulement une fois l'email parti.
/// </summary>
public sealed partial class CreationDeCentre(SchoolHubDbContext contexte, UserManager<Compte> comptes, Invitations invitations)
{
    public async Task<ResultatDeCreation> CreerAsync(NouveauCentre demande, CancellationToken annulation = default)
    {
        if (!CodeValable().IsMatch(demande.Code))
            return Refus("Le code ne peut contenir que des lettres minuscules, des chiffres et des tirets.");

        if (!EmailValable(demande.EmailDuProprietaire))
            return Refus($"L'email {demande.EmailDuProprietaire} n'est pas valable.");

        if (await contexte.Centres.AnyAsync(c => c.Code == demande.Code, annulation))
            return Refus($"Le code « {demande.Code} » est déjà pris par un autre centre.");

        // Identity compare les emails sans tenir compte de la casse : « HEDI@… » et « hedi@… » sont le même email.
        if (await comptes.FindByEmailAsync(demande.EmailDuProprietaire) is not null)
            return Refus($"L'email {demande.EmailDuProprietaire} est déjà utilisé sur SchoolHub.");

        await using var transaction = await contexte.Database.BeginTransactionAsync(annulation);

        var centre = new Centre(demande.Nom, demande.Code);
        contexte.Centres.Add(centre);
        await contexte.SaveChangesAsync(annulation);

        var proprietaire = new Compte(
            centre.Id,
            demande.PrenomDuProprietaire,
            demande.NomDuProprietaire,
            demande.EmailDuProprietaire,
            RoleDuCompte.Proprietaire);
        var creation = await comptes.CreateAsync(proprietaire);
        if (!creation.Succeeded)
        {
            await transaction.RollbackAsync(annulation);
            return Refus(creation.Errors.First().Description);
        }

        try
        {
            await invitations.EnvoyerAsync(proprietaire, centre, annulation);
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(annulation);
            return Refus($"Rien n'a été créé : l'invitation n'est pas partie ({exception.Message}).");
        }

        await transaction.CommitAsync(annulation);
        return new ResultatDeCreation(true, $"Centre « {centre.Nom} » créé. Invitation envoyée à {proprietaire.Email}.");
    }

    private static ResultatDeCreation Refus(string message) => new(false, message);

    /// <summary>Une adresse écrite telle quelle (pas de nom devant, pas de liste) et dont le domaine a un point.</summary>
    private static bool EmailValable(string email) =>
        MailAddress.TryCreate(email, out var adresse) && adresse.Address == email && adresse.Host.Contains('.');

    // Des minuscules et des chiffres, séparés par des tirets seuls : le code va dans l'adresse du formulaire public.
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex CodeValable();
}
