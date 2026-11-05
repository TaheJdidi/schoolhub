using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Inscriptions;

/// <summary>ELV-03 : les adresses des inscriptions, pour toute personne connectée du centre.</summary>
public static class InscriptionsEndpoints
{
    public static IEndpointRouteBuilder MapInscriptions(this IEndpointRouteBuilder api)
    {
        var groupe = api.MapGroup("/inscriptions");
        groupe.MapGet("", ListerAsync);
        groupe.MapPost("", InscrireAsync);
        groupe.MapGet("/groupes-possibles", GroupesPossiblesAsync);
        api.MapGet("/eleves/{id:guid}/assurance", AssuranceAsync);
        api.MapGet("/eleves/{id:guid}/a-payer", APayerAsync);
        // ELV-05 et ELV-06 : le prix spécial et la dispense sont réservés au propriétaire.
        groupe.MapPut("/{id:guid}/prix-special", AccorderUnPrixSpecialAsync).RequireAuthorization(Politiques.Proprietaire);
        groupe.MapDelete("/{id:guid}/prix-special", RetirerLePrixSpecialAsync).RequireAuthorization(Politiques.Proprietaire);
        api.MapPost("/eleves/{id:guid}/assurance/dispense", DispenserAsync).RequireAuthorization(Politiques.Proprietaire);
        // ELV-07 et ELV-10 : toute personne connectée du centre.
        groupe.MapPost("/{id:guid}/paye-avant-schoolhub", PayeAvantSchoolHubAsync);
        groupe.MapPost("/{id:guid}/arret", async (Guid id, DateSaisie saisie, ClaimsPrincipal utilisateur, UserManager<Compte> comptes,
            InscriptionsDuCentre inscriptions) => Repondre(await inscriptions.ArreterAsync(await AuteurAsync(utilisateur, comptes), id, saisie)));
        groupe.MapPost("/{id:guid}/changement", ChangerDeGroupeAsync);
        groupe.MapPost("/{id:guid}/reste", async (Guid id, InscriptionsDuCentre inscriptions) => Repondre(await inscriptions.RegrouperLeResteAsync(id)));
        api.MapPost("/eleves/{id:guid}/assurance/payee-avant-schoolhub", AssurancePayeeAvantSchoolHubAsync);
        return api;
    }

    private static async Task<Results<Ok<APayer>, JsonHttpResult<Avis>>> APayerAsync(Guid id, InscriptionsDuCentre inscriptions) =>
        await inscriptions.APayerAsync(id) is { } aPayer
            ? TypedResults.Ok(aPayer)
            : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, InscriptionsDuCentre.EleveIntrouvable);

    private static async Task<Results<Ok<InscriptionDeLEleve>, JsonHttpResult<Avis>>> AccorderUnPrixSpecialAsync(
        Guid id,
        PrixSpecialSaisi saisie,
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        InscriptionsDuCentre inscriptions) =>
        Repondre(await inscriptions.AccorderUnPrixSpecialAsync(await AuteurAsync(utilisateur, comptes), id, saisie));

    private static async Task<Results<Ok<InscriptionDeLEleve>, JsonHttpResult<Avis>>> RetirerLePrixSpecialAsync(
        Guid id,
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        InscriptionsDuCentre inscriptions) =>
        Repondre(await inscriptions.RetirerLePrixSpecialAsync(await AuteurAsync(utilisateur, comptes), id));

    private static async Task<Results<Ok<AssuranceDeLEleve>, JsonHttpResult<Avis>>> DispenserAsync(
        Guid id,
        RaisonSaisie saisie,
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        InscriptionsDuCentre inscriptions)
    {
        var issue = await inscriptions.DispenserAsync(await AuteurAsync(utilisateur, comptes), id, saisie);
        return issue.Assurance is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Assurance);
    }

    private static async Task<Results<Ok<InscriptionDeLEleve>, JsonHttpResult<Avis>>> PayeAvantSchoolHubAsync(
        Guid id,
        CleSaisie saisie,
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        InscriptionsDuCentre inscriptions) =>
        Repondre(await inscriptions.PayeAvantSchoolHubAsync(await AuteurAsync(utilisateur, comptes), id, saisie));

    private static async Task<Results<Ok<AssuranceDeLEleve>, JsonHttpResult<Avis>>> AssurancePayeeAvantSchoolHubAsync(
        Guid id,
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        InscriptionsDuCentre inscriptions)
    {
        var issue = await inscriptions.AssurancePayeeAvantSchoolHubAsync(await AuteurAsync(utilisateur, comptes), id);
        return issue.Assurance is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Assurance);
    }

    private static Results<Ok<InscriptionDeLEleve>, JsonHttpResult<Avis>> Repondre(IssueSurUneInscription issue) =>
        issue.Inscription is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Inscription);

    /// <summary>La personne connectée : elle est l'auteur de la ligne du journal.</summary>
    private static async Task<Compte> AuteurAsync(ClaimsPrincipal utilisateur, UserManager<Compte> comptes) =>
        await comptes.GetUserAsync(utilisateur) ?? throw new InvalidOperationException("Aucun compte connecté.");

    private static async Task<Results<Ok<List<InscriptionDeLEleve>>, JsonHttpResult<Avis>>> ListerAsync(
        Guid? eleve,
        Guid? groupe,
        InscriptionsDuCentre inscriptions)
    {
        var issue = await inscriptions.ListerAsync(eleve, groupe);
        return issue.Liste is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Liste);
    }

    private static async Task<Results<Ok<List<GroupePossible>>, JsonHttpResult<Avis>>> GroupesPossiblesAsync(
        Guid? eleve,
        Guid? depuis,
        InscriptionsDuCentre inscriptions)
    {
        if (eleve is not { } eleveId)
            return ConnexionEndpoints.Refus(StatusCodes.Status400BadRequest, InscriptionsDuCentre.ChoisirLEleve);
        var (groupes, introuvable) = await inscriptions.GroupesPossiblesAsync(eleveId, depuis);
        return groupes is null ? ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, introuvable!) : TypedResults.Ok(groupes);
    }

    private static async Task<Results<Created<EnregistrementDeLInscription>, JsonHttpResult<Avis>>> ChangerDeGroupeAsync(
        Guid id,
        DemandeDeChangement demande,
        ClaimsPrincipal utilisateur,
        UserManager<Compte> comptes,
        InscriptionsDuCentre inscriptions)
    {
        var issue = await inscriptions.ChangerDeGroupeAsync(await AuteurAsync(utilisateur, comptes), id, demande);
        return issue.Enregistrement is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Created($"/api/inscriptions/{issue.Enregistrement.Inscription.Id}", issue.Enregistrement);
    }

    private static async Task<Results<Ok<AssuranceDeLEleve>, NoContent, JsonHttpResult<Avis>>> AssuranceAsync(
        Guid id,
        InscriptionsDuCentre inscriptions)
    {
        var (trouve, assurance) = await inscriptions.AssuranceAsync(id);
        if (!trouve)
            return ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, InscriptionsDuCentre.EleveIntrouvable);
        return assurance is null ? TypedResults.NoContent() : TypedResults.Ok(assurance);
    }

    private static async Task<Results<Created<EnregistrementDeLInscription>, JsonHttpResult<Avis>>> InscrireAsync(
        InscriptionSaisie demande,
        InscriptionsDuCentre inscriptions)
    {
        var issue = await inscriptions.InscrireAsync(demande);
        return issue.Enregistrement is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Created($"/api/inscriptions/{issue.Enregistrement.Inscription.Id}", issue.Enregistrement);
    }
}

/// <summary>Ce que la page envoie pour inscrire un élève : le mode peut manquer quand le groupe n'offre pas de choix.</summary>
public sealed record InscriptionSaisie(Guid? EleveId, Guid? GroupeId, string? Mode, DateOnly? DebutLe);

public sealed record EleveInscrit(Guid Id, string Prenom, string Nom);

public sealed record GroupeDeLInscription(Guid Id, string Nom, string Type, string Matiere);

public sealed record SeanceDeDebut(DateOnly Date, TimeOnly Debut);

/// <summary>Une somme réglée : « Tranche 1 », 100,000 TND, payée avant SchoolHub (« AvantSchoolHub »).</summary>
public sealed record SommeReglee(string Libelle, decimal Montant, string Origine);

/// <summary>Une somme qui peut être notée « payée avant SchoolHub » (ELV-07), avec sa clé.</summary>
public sealed record SommePayable(string Cle, string Libelle, decimal Montant);

/// <summary>
/// Une inscription, comme les pages la montrent. <see cref="PrixNormal"/> est le prix du Mois à venir pour une Étude, le
/// prix total sinon ; <see cref="PremiereSeance"/> vaut <c>null</c> si le groupe n'a plus de séance à sa date ou après.
/// Le prix spécial (ELV-05), ce qui est réglé, ce qui peut être noté payé avant SchoolHub (ELV-07) et « Payer tout le
/// reste » (ELV-10) suivent.
/// </summary>
public sealed record InscriptionDeLEleve(
    Guid Id,
    EleveInscrit Eleve,
    GroupeDeLInscription Groupe,
    string Professeur,
    string Mode,
    decimal PrixNormal,
    int NombreDeTranches,
    SeanceDeDebut? PremiereSeance,
    decimal? PrixSpecial,
    string? RaisonDuPrixSpecial,
    List<SommeReglee> Reglees,
    List<SommePayable> PayablesAvantSchoolHub,
    bool PeutRegrouperLeReste,
    DateOnly? ArreteLe,
    decimal Report);

/// <summary>Une somme due à la première séance de l'élève : « Mois 3 (3 séances) », 30,000 TND.</summary>
public sealed record SommeALArrivee(string Libelle, decimal Montant);

/// <summary>Un message de confirmation prêt à ouvrir : « Parent » ou le prénom de l'élève, son téléphone groupé, le lien.</summary>
public sealed record MessageWhatsApp(string Destinataire, string Telephone, string Lien);

/// <summary>Un groupe où l'on peut inscrire l'élève, avec ce qu'il faut pour choisir : niveau, nombre d'élèves, capacité.</summary>
public sealed record GroupePossible(
    Guid Id,
    string Nom,
    string Type,
    string Matiere,
    string Professeur,
    string? Niveau,
    bool DuNiveauDeLEleve,
    int Eleves,
    int? Capacite,
    bool DejaInscrit);

/// <summary>
/// L'assurance de l'année : « 2027-2028 », son montant, la date de la première séance d'Étude où elle est due, et son état :
/// « APayer », « PayeeAvantSchoolHub » ou « Dispensee » (avec la raison).
/// </summary>
public sealed record AssuranceDeLEleve(string Annee, decimal Montant, DateOnly? DueLe, string Etat, string? RaisonDeLaDispense);

/// <summary>Une ligne de « À payer » : l'inscription (rien pour l'assurance), le groupe, la somme et le jour où elle est due.</summary>
public sealed record LigneAPayer(Guid? InscriptionId, string Groupe, string Libelle, string Cle, decimal Montant, DateOnly DueLe);

/// <summary>ELV-04 : ce que l'élève doit à ce jour, et le total.</summary>
public sealed record APayer(List<LigneAPayer> Lignes, decimal Total);

/// <summary>Ce que le propriétaire envoie pour accorder un prix spécial : le montant (0 : gratuit) et la raison.</summary>
public sealed record PrixSpecialSaisi(decimal? Montant, string? Raison);

/// <summary>Une raison, pour une dispense d'assurance.</summary>
public sealed record RaisonSaisie(string? Raison);

/// <summary>ELV-09 : le nouveau groupe, son mode (s'il y a un choix) et la première séance de l'élève dans ce groupe.</summary>
public sealed record DemandeDeChangement(Guid? GroupeId, string? Mode, DateOnly? DebutLe);

/// <summary>Une date, pour l'arrêt d'une inscription (ELV-08).</summary>
public sealed record DateSaisie(DateOnly? Date);

/// <summary>La clé d'une somme d'une inscription : « Totalité », « Tranche 1 »…</summary>
public sealed record CleSaisie(string? Cle);

/// <summary>La réponse d'une inscription : l'inscription, ce qu'il paiera à sa première séance, l'avertissement et les messages.</summary>
public sealed record EnregistrementDeLInscription(
    InscriptionDeLEleve Inscription,
    List<SommeALArrivee> APayerALArrivee,
    string? Avertissement,
    string Texte,
    List<MessageWhatsApp> Messages);
