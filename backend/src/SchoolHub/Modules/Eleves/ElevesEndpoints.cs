using Microsoft.AspNetCore.Http.HttpResults;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;

namespace SchoolHub.Modules.Eleves;

/// <summary>ELV-01, ELV-02 et ELV-11 : les adresses des élèves, pour toute personne connectée du centre.</summary>
public static class ElevesEndpoints
{
    public static IEndpointRouteBuilder MapEleves(this IEndpointRouteBuilder api)
    {
        var groupe = api.MapGroup("/eleves");
        // CAI-01 : chaque élève avec sa couleur, la pire de ses inscriptions.
        groupe.MapGet("", async (string? recherche, ElevesDuCentre eleves, SchoolHub.Modules.Inscriptions.InscriptionsDuCentre inscriptions) =>
        {
            var trouves = await eleves.ListerAsync(recherche);
            var situations = await inscriptions.SituationsAsync(trouves.Select(e => e.Id).ToList());
            return trouves.Select(e => e with { Couleur = situations[e.Id].Couleur.ToString() }).ToList();
        });
        groupe.MapPost("", AjouterAsync);
        groupe.MapGet("/{id:guid}", LireAsync);
        groupe.MapPut("/{id:guid}", ModifierAsync);
        groupe.MapDelete("/{id:guid}", SupprimerAsync);
        return api;
    }

    private static async Task<Results<Created<EnregistrementDeLEleve>, JsonHttpResult<Avis>>> AjouterAsync(
        FicheSaisieDeLEleve saisie,
        ElevesDuCentre eleves)
    {
        var issue = await eleves.AjouterAsync(saisie);
        return issue.Fiche is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Created($"/api/eleves/{issue.Fiche.Id}", new EnregistrementDeLEleve(issue.Fiche, issue.Avertissement));
    }

    private static async Task<Results<Ok<FicheDeLEleve>, JsonHttpResult<Avis>>> LireAsync(Guid id, ElevesDuCentre eleves)
    {
        var fiche = await eleves.LireAsync(id);
        if (fiche is not null)
            return TypedResults.Ok(fiche);
        var refus = ElevesDuCentre.EleveIntrouvable;
        return ConnexionEndpoints.Refus(refus.Statut, refus.Message!);
    }

    private static async Task<Results<Ok<EnregistrementDeLEleve>, JsonHttpResult<Avis>>> ModifierAsync(
        Guid id,
        FicheSaisieDeLEleve saisie,
        ElevesDuCentre eleves)
    {
        var issue = await eleves.ModifierAsync(id, saisie);
        return issue.Fiche is null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message!)
            : TypedResults.Ok(new EnregistrementDeLEleve(issue.Fiche, issue.Avertissement));
    }

    private static async Task<Results<NoContent, JsonHttpResult<Avis>>> SupprimerAsync(Guid id, ElevesDuCentre eleves)
    {
        var issue = await eleves.SupprimerAsync(id);
        return issue.Message is not null
            ? ConnexionEndpoints.Refus(issue.Statut, issue.Message)
            : TypedResults.NoContent();
    }
}

/// <summary>
/// Ce que la page envoie pour créer ou modifier la fiche d'un élève. <c>SansParent</c> : une personne de l'extérieur, à
/// la bibliothèque, sans parent à joindre ; son propre téléphone est alors obligatoire.
/// </summary>
public sealed record FicheSaisieDeLEleve(
    string? Prenom,
    string? Nom,
    Guid? NiveauId,
    bool Majeur,
    string? TelephoneParent,
    string? EmailParent,
    string? TelephoneEleve,
    string? EmailEleve,
    string? Etablissement,
    bool SansParent = false);

public sealed record NiveauDeLEleve(Guid Id, string Nom);

/// <summary>La fiche d'un élève, ses téléphones groupés (« 98 401 220 »), ses champs vides à <c>null</c>.</summary>
public sealed record FicheDeLEleve(
    Guid Id,
    string Prenom,
    string Nom,
    NiveauDeLEleve Niveau,
    bool Majeur,
    string? TelephoneParent,
    string? EmailParent,
    string? TelephoneEleve,
    string? EmailEleve,
    string? Etablissement)
{
    public static FicheDeLEleve De(Eleve eleve, Niveau niveau) => new(
        eleve.Id,
        eleve.Prenom,
        eleve.Nom,
        new NiveauDeLEleve(niveau.Id, niveau.Nom),
        eleve.Majeur,
        eleve.TelephoneParent is { } parent ? TelephoneTunisien.Grouper(parent) : null,
        eleve.EmailParent,
        eleve.TelephoneEleve is { } telephone ? TelephoneTunisien.Grouper(telephone) : null,
        eleve.EmailEleve,
        eleve.Etablissement);
}

/// <summary>La réponse d'un ajout ou d'une modification : la fiche, et « Doublon possible : … » s'il y a lieu.</summary>
public sealed record EnregistrementDeLEleve(FicheDeLEleve Eleve, string? Avertissement);

/// <summary>Un élève dans la liste ou la recherche : son nom et le nom de son niveau.</summary>
public sealed record EleveTrouve(Guid Id, string Prenom, string Nom, string Niveau, string? Couleur = null);
