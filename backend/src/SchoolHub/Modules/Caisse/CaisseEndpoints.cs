using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Caisse;

/// <summary>CAI-03, CAI-05 à CAI-07 : les adresses de l'encaissement et des reçus, pour toute personne connectée du centre.</summary>
public static class CaisseEndpoints
{
    public static IEndpointRouteBuilder MapCaisse(this IEndpointRouteBuilder api)
    {
        api.MapGet("/eleves/{id:guid}/encaissable", async Task<Results<Ok<Encaissable>, JsonHttpResult<Avis>>> (Guid id, RecusDuCentre recus) =>
            await recus.EncaissableAsync(id) is { } encaissable
                ? TypedResults.Ok(encaissable)
                : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, "Élève introuvable"));
        api.MapPost("/eleves/{id:guid}/encaissement", async Task<Results<Created<RecuDecrit>, JsonHttpResult<Avis>>> (
            Guid id, EncaissementSaisi saisie, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, RecusDuCentre recus) =>
        {
            var issue = await recus.EncaisserAsync(await AuteurAsync(utilisateur, comptes), id, saisie);
            return issue.Recu is null
                ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
                : TypedResults.Created($"/api/recus/{issue.Recu.Id}", issue.Recu);
        });

        // CAI-04 : l'encaissement groupé, depuis un groupe.
        api.MapGet("/groupes/{id:guid}/encaissement", async Task<Results<Ok<EncaissementDuGroupe>, JsonHttpResult<Avis>>> (Guid id, RecusDuCentre recus) =>
            await recus.EncaissementDuGroupeAsync(id) is { } encaissement
                ? TypedResults.Ok(encaissement)
                : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, RecusDuCentre.GroupeIntrouvable));
        api.MapPost("/groupes/{id:guid}/encaissement", async Task<Results<Created<RecusDuGroupe>, JsonHttpResult<Avis>>> (
            Guid id, EncaissementGroupeSaisi saisie, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, RecusDuCentre recus) =>
        {
            var issue = await recus.EncaisserLeGroupeAsync(await AuteurAsync(utilisateur, comptes), id, saisie);
            return issue.Recus is null
                ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
                : TypedResults.Created("/api/recus", issue.Recus);
        });

        // CAI-08 : le rappel de paiement, et les élèves en rouge.
        api.MapGet("/eleves/en-rouge", (RappelsDePaiement rappels) => rappels.EnRougeAsync());
        api.MapGet("/eleves/{id:guid}/recus", (Guid id, RecusDuCentre recus) => recus.DeLEleveAsync(id));
        api.MapGet("/eleves/{id:guid}/rappel", async Task<Results<Ok<RappelDePaiement>, JsonHttpResult<Avis>>> (Guid id, RappelsDePaiement rappels) =>
        {
            var issue = await rappels.LireAsync(id);
            return issue.Rappel is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Rappel);
        });
        api.MapPost("/eleves/{id:guid}/rappel/envoye", async Task<Results<NoContent, JsonHttpResult<Avis>>> (Guid id, RappelsDePaiement rappels) =>
        {
            var issue = await rappels.NoterAsync(id);
            return issue.Message is null ? TypedResults.NoContent() : ConnexionEndpoints.Refus(issue.Statut, issue.Message);
        });

        // CAI-09 : sa caisse et sa clôture du soir ; CAI-10 : le contrôle des clôtures, pour le propriétaire.
        var caisse = api.MapGroup("/caisse");
        caisse.MapGet("", async (ClaimsPrincipal utilisateur, UserManager<Compte> comptes, CaissesDuCentre caisses) =>
            await caisses.MaCaisseAsync(await AuteurAsync(utilisateur, comptes)));
        caisse.MapPost("/cloture", async Task<Results<Created<ClotureDecrite>, JsonHttpResult<Avis>>> (
            ClotureSaisie saisie, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, CaissesDuCentre caisses) =>
        {
            var issue = await caisses.CloturerAsync(await AuteurAsync(utilisateur, comptes), saisie);
            return issue.Cloture is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Created("/api/caisse", issue.Cloture);
        });
        caisse.MapGet("/clotures", (DateOnly? du, DateOnly? au, Guid? compteId, CaissesDuCentre caisses) => caisses.ControleAsync(du, au, compteId))
            .RequireAuthorization(Politiques.Proprietaire);
        caisse.MapPut("/clotures/{id:guid}/argent-recu", async Task<Results<Ok<ClotureDecrite>, JsonHttpResult<Avis>>> (
            Guid id, ArgentRecuSaisi saisie, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, CaissesDuCentre caisses) =>
        {
            var issue = await caisses.MarquerLArgentRecuAsync(await AuteurAsync(utilisateur, comptes), id, saisie);
            return issue.Cloture is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Cloture);
        }).RequireAuthorization(Politiques.Proprietaire);

        var groupe = api.MapGroup("/recus");
        groupe.MapGet("", (DateOnly? jour, int? numero, string? eleve, RecusDuCentre recus) => recus.ListerAsync(jour, numero, eleve));
        groupe.MapGet("/{id:guid}", async Task<Results<Ok<RecuDecrit>, JsonHttpResult<Avis>>> (
            Guid id, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, RecusDuCentre recus) =>
            await recus.LireAsync(id, await AuteurAsync(utilisateur, comptes)) is { } recu
                ? TypedResults.Ok(recu)
                : ConnexionEndpoints.Refus(StatusCodes.Status404NotFound, RecusDuCentre.RecuIntrouvable));
        groupe.MapPost("/{id:guid}/annulation", async (Guid id, AnnulationDuRecu saisie, ClaimsPrincipal utilisateur, UserManager<Compte> comptes,
            RecusDuCentre recus) => Repondre(await recus.AnnulerAsync(await AuteurAsync(utilisateur, comptes), id, saisie)));
        groupe.MapPost("/{id:guid}/email", async (Guid id, ClaimsPrincipal utilisateur, UserManager<Compte> comptes, RecusDuCentre recus) =>
            Repondre(await recus.RenvoyerLEmailAsync(id, await AuteurAsync(utilisateur, comptes))));
        return api;
    }

    private static Results<Ok<RecuDecrit>, JsonHttpResult<Avis>> Repondre(IssueDuRecu issue) =>
        issue.Recu is null ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!) : TypedResults.Ok(issue.Recu);

    /// <summary>La personne connectée : elle encaisse dans sa caisse, et elle est l'auteur des lignes du journal.</summary>
    private static async Task<Compte> AuteurAsync(ClaimsPrincipal utilisateur, UserManager<Compte> comptes) =>
        await comptes.GetUserAsync(utilisateur) ?? throw new InvalidOperationException("Aucun compte connecté.");
}

/// <summary>Une somme cochée : son inscription et sa clé (« Mois 3 ») ; pour l'assurance, pas d'inscription et la clé « Assurance ».</summary>
public sealed record SommeChoisie(Guid? InscriptionId, string? Cle);

/// <summary>CAI-03 : les sommes cochées sur la fiche de l'élève.</summary>
public sealed record EncaissementSaisi(List<SommeChoisie>? Sommes);

/// <summary>CAI-04 : un élève coché, avec les sommes que la page a montrées pour lui.</summary>
public sealed record EleveCoche(Guid? EleveId, List<SommeChoisie>? Sommes);

/// <summary>CAI-04 : les élèves cochés de l'encaissement groupé.</summary>
public sealed record EncaissementGroupeSaisi(List<EleveCoche>? Eleves);

/// <summary>CAI-04 : une somme due à ce jour dans le groupe (ou l'assurance, sans inscription).</summary>
public sealed record SommeDuGroupe(Guid? InscriptionId, string Cle, string Libelle, decimal Montant);

/// <summary>CAI-04 : un élève qui doit quelque chose à ce jour dans le groupe : sa couleur (CAI-01), ses sommes et leur total.</summary>
public sealed record EleveAEncaisser(Guid EleveId, string Prenom, string Nom, string Couleur, List<SommeDuGroupe> Sommes, decimal Total);

public sealed record GroupeEncaisse(Guid Id, string Nom, string Matiere, string Type);

/// <summary>CAI-04 : le groupe, les élèves qui doivent quelque chose à ce jour, et ceux qui sont déjà à jour.</summary>
public sealed record EncaissementDuGroupe(GroupeEncaisse Groupe, List<EleveAEncaisser> APayer, List<EleveDuRecu> AJour);

/// <summary>CAI-04 : les reçus créés, un par élève, dans l'ordre de leurs numéros.</summary>
public sealed record RecusDuGroupe(List<RecuDecrit> Recus);

/// <summary>CAI-07 : le motif de l'annulation, « Mauvais élève sélectionné ».</summary>
public sealed record AnnulationDuRecu(string? Motif);

public sealed record EleveDuRecu(Guid Id, string Prenom, string Nom);

/// <summary>
/// Une somme que l'élève peut payer : son inscription et sa clé, son groupe, son libellé, son montant, le jour où elle
/// est due (vide pour une assurance pas encore due), et si elle est due aujourd'hui ou avant.
/// </summary>
public sealed record SommeAEncaisser(Guid? InscriptionId, string Cle, string Groupe, string Libelle, decimal Montant, DateOnly? DueLe, bool Due);

/// <summary>CAI-03 : ce que l'élève peut payer, et s'il a un email pour recevoir son reçu.</summary>
public sealed record Encaissable(EleveDuRecu Eleve, List<SommeAEncaisser> Sommes, bool AUnEmail);

public sealed record CentreDuRecu(string Nom, string? Telephone);

public sealed record LigneDecrite(string Groupe, string Libelle, decimal Montant);

/// <summary>
/// CAI-05 : un reçu, tel que la page le montre et l'imprime ; son texte pour WhatsApp et l'email, un message par
/// destinataire, les emails connus ; s'il peut être annulé par la personne connectée, ou pourquoi pas (CAI-07).
/// </summary>
public sealed record RecuDecrit(
    Guid Id,
    int Numero,
    DateOnly Jour,
    DateTimeOffset CreeLe,
    CentreDuRecu Centre,
    EleveDuRecu Eleve,
    List<LigneDecrite> Lignes,
    decimal Total,
    string Secretaire,
    bool Annule,
    string? MotifDAnnulation,
    DateTimeOffset? AnnuleLe,
    string Texte,
    List<MessageWhatsApp> Messages,
    List<string> Emails,
    bool Annulable,
    string? PourquoiPasAnnulable);

/// <summary>CAI-06 : un reçu dans la liste.</summary>
/// <summary>Un reçu sur la fiche de l'élève : son numéro, son jour, ce qu'il payait (« Mois 2 (4 séances) »).</summary>
public sealed record RecuDeLEleve(Guid Id, int Numero, DateOnly Jour, string Libelle, decimal Total, string Secretaire, bool Annule);

public sealed record RecuDeLaListe(Guid Id, int Numero, DateOnly Jour, DateTimeOffset CreeLe, string Eleve, decimal Total, string Secretaire, bool Annule);
