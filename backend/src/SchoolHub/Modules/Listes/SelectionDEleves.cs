using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Listes;

/// <summary>
/// LST-05 : la même action pour plusieurs élèves sélectionnés : les inscrire dans un groupe (les règles d'ELV-03 pour
/// chacun ; les élèves déjà inscrits listés à part), ou changer leur niveau (les règles d'ELV-02 pour chacun). Chaque élève
/// passe ou est refusé seul, avec sa raison. Il n'y a pas de suppression en masse.
/// </summary>
public sealed class SelectionDEleves(
    SchoolHubDbContext contexte,
    ElevesDuCentre eleves,
    InscriptionsDuCentre inscriptions,
    ImportsDesListes imports,
    TimeProvider temps)
{
    private const string AucunEleve = "Sélectionnez au moins un élève";
    private const string TropDEleves = "Sélectionnez au plus 200 élèves";
    private const string ChoisirLeGroupe = "Choisissez le groupe";
    private const string GroupeIntrouvable = "Groupe introuvable";
    private const string PlusDeSeance = "Ce groupe n'a plus de séance à venir";
    private const string ChoisirLeNiveau = "Choisissez le niveau";
    private const int ElevesAuPlus = 200;

    /// <summary>
    /// LST-05 : inscrit chaque élève sélectionné dans le groupe, à partir de la séance choisie ou, sans elle, du Mois en
    /// cours du groupe (GRP-07).
    /// </summary>
    public async Task<IssueDeLaSelection> InscrireAsync(InscriptionDeLaSelection saisie)
    {
        var ids = (saisie.EleveIds ?? []).Distinct().ToList();
        if (Refus(ids) is { } refus)
            return refus;
        if (saisie.GroupeId is not { } groupeId)
            return new(null, StatusCodes.Status400BadRequest, ChoisirLeGroupe);
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == groupeId);
        if (groupe is null)
            return new(null, StatusCodes.Status404NotFound, GroupeIntrouvable);
        var debutLe = saisie.DebutLe ?? await imports.PremiereSeanceDuMoisEnCoursAsync(groupe);
        if (debutLe is null)
            return new(null, StatusCodes.Status409Conflict, PlusDeSeance);

        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var dejaInscrits = (await contexte.Inscriptions.Where(i => i.GroupeId == groupeId && ids.Contains(i.EleveId) && (i.ArreteLe == null || i.ArreteLe >= aujourdhui))
            .Select(i => i.EleveId).ToListAsync()).ToHashSet();
        var lesEleves = (await contexte.Eleves.Where(e => ids.Contains(e.Id)).ToListAsync()).OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ToList();

        var reussis = new List<string>();
        var aPart = new List<string>();
        var refuses = new List<EleveRefuse>();
        foreach (var eleve in lesEleves)
        {
            var nom = $"{eleve.Prenom} {eleve.Nom}";
            if (dejaInscrits.Contains(eleve.Id))
            {
                aPart.Add(nom);
                continue;
            }
            var issue = await inscriptions.InscrireAsync(new InscriptionSaisie(eleve.Id, groupeId, saisie.Mode, debutLe));
            if (issue.Enregistrement is null)
                refuses.Add(new EleveRefuse(eleve.Id, nom, issue.Message!));
            else
                reussis.Add(nom);
        }

        return new(new ResultatDeLaSelection(reussis, aPart, refuses), StatusCodes.Status200OK, null);
    }

    /// <summary>LST-05 : change le niveau de chaque élève sélectionné, avec les règles de sa fiche (contacts à partir de la 7ᵉ).</summary>
    public async Task<IssueDeLaSelection> ChangerLeNiveauAsync(NiveauDeLaSelection saisie)
    {
        var ids = (saisie.EleveIds ?? []).Distinct().ToList();
        if (Refus(ids) is { } refus)
            return refus;
        if (saisie.NiveauId is not { } niveauId)
            return new(null, StatusCodes.Status400BadRequest, ChoisirLeNiveau);

        var lesEleves = (await contexte.Eleves.Where(e => ids.Contains(e.Id)).ToListAsync()).OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ToList();
        var reussis = new List<string>();
        var refuses = new List<EleveRefuse>();
        foreach (var eleve in lesEleves)
        {
            var nom = $"{eleve.Prenom} {eleve.Nom}";
            var fiche = new FicheSaisieDeLEleve(eleve.Prenom, eleve.Nom, niveauId, eleve.Majeur, eleve.TelephoneParent, eleve.EmailParent, eleve.TelephoneEleve,
                eleve.EmailEleve, eleve.Etablissement);
            var issue = await eleves.ModifierAsync(eleve.Id, fiche);
            if (issue.Fiche is null)
                refuses.Add(new EleveRefuse(eleve.Id, nom, issue.Message!));
            else
                reussis.Add(nom);
        }

        return new(new ResultatDeLaSelection(reussis, [], refuses), StatusCodes.Status200OK, null);
    }

    private static IssueDeLaSelection? Refus(List<Guid> ids) => ids.Count switch
    {
        0 => new(null, StatusCodes.Status400BadRequest, AucunEleve),
        > ElevesAuPlus => new(null, StatusCodes.Status400BadRequest, TropDEleves),
        _ => null,
    };
}

public sealed record InscriptionDeLaSelection(List<Guid>? EleveIds, Guid? GroupeId, string? Mode, DateOnly? DebutLe);

public sealed record NiveauDeLaSelection(List<Guid>? EleveIds, Guid? NiveauId);

public sealed record EleveRefuse(Guid EleveId, string Nom, string Raison);

/// <summary>LST-05 : les élèves pour qui l'action a réussi, ceux laissés à part (déjà inscrits), et ceux refusés avec leur raison.</summary>
public sealed record ResultatDeLaSelection(List<string> Reussis, List<string> DejaInscrits, List<EleveRefuse> Refuses);

public sealed record IssueDeLaSelection(ResultatDeLaSelection? Resultat, int Statut, string? Message);
