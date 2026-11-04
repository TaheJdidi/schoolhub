using Microsoft.AspNetCore.Http.HttpResults;
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
        return api;
    }

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
        InscriptionsDuCentre inscriptions)
    {
        if (eleve is not { } eleveId)
            return ConnexionEndpoints.Refus(StatusCodes.Status400BadRequest, InscriptionsDuCentre.ChoisirLEleve);
        var groupes = await inscriptions.GroupesPossiblesAsync(eleveId);
        return groupes is null
            ? ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, InscriptionsDuCentre.EleveIntrouvable)
            : TypedResults.Ok(groupes);
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
        DemandeDInscription demande,
        InscriptionsDuCentre inscriptions)
    {
        var issue = await inscriptions.InscrireAsync(demande);
        return issue.Enregistrement is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Created($"/api/inscriptions/{issue.Enregistrement.Inscription.Id}", issue.Enregistrement);
    }
}

/// <summary>Ce que la page envoie pour inscrire un élève : le mode peut manquer quand le groupe n'offre pas de choix.</summary>
public sealed record DemandeDInscription(Guid? EleveId, Guid? GroupeId, string? Mode, DateOnly? DebutLe);

public sealed record EleveInscrit(Guid Id, string Prenom, string Nom);

public sealed record GroupeDeLInscription(Guid Id, string Nom, string Type, string Matiere);

public sealed record SeanceDeDebut(DateOnly Date, TimeOnly Debut);

/// <summary>
/// Une inscription, comme les pages la montrent. <see cref="PrixNormal"/> est le prix du Mois à venir pour une Étude, le
/// prix total sinon ; <see cref="PremiereSeance"/> vaut <c>null</c> si le groupe n'a plus de séance à sa date ou après.
/// </summary>
public sealed record InscriptionDeLEleve(
    Guid Id,
    EleveInscrit Eleve,
    GroupeDeLInscription Groupe,
    string Professeur,
    string Mode,
    decimal PrixNormal,
    int NombreDeTranches,
    SeanceDeDebut? PremiereSeance);

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

/// <summary>L'assurance de l'année : « 2027-2028 », son montant, et la date de la première séance d'Étude où elle est due.</summary>
public sealed record AssuranceDeLEleve(string Annee, decimal Montant, DateOnly? DueLe);

/// <summary>La réponse d'une inscription : l'inscription, ce qu'il paiera à sa première séance, l'avertissement et les messages.</summary>
public sealed record EnregistrementDeLInscription(
    InscriptionDeLEleve Inscription,
    List<SommeALArrivee> APayerALArrivee,
    string? Avertissement,
    string Texte,
    List<MessageWhatsApp> Messages);
