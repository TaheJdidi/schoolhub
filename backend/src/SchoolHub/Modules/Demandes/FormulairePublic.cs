using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Demandes;

/// <summary>
/// DEM-01 : le formulaire public d'un centre, ouvert sans compte depuis son lien (« …/inscription/les-jasmins »). Il
/// travaille dans son propre espace de services, avec le centre du lien pour centre courant : une personne connectée à
/// un autre centre envoie au centre du lien, et ne voit rien du sien. Il ne montre jamais d'élève, ni de nombre d'inscrits.
/// </summary>
public sealed class FormulairePublic(IServiceScopeFactory espaces, LimiteDesDemandes limite, TimeProvider temps)
{
    internal const string TropDEnvois = "Trop de demandes envoyées depuis cette connexion. Réessayez dans une heure.";
    private const string LienInconnu = "Ce lien d'inscription n'existe pas";
    private const string AucuneMatiere = "Choisissez au moins une matière";
    private const string GroupePlusPropose = "Ce groupe n'est plus proposé";
    private const string UnGroupeParMatiere = "Choisissez un seul groupe par matière";
    private const string AmisTropLongs = "Les amis font au plus 500 caractères";

    /// <summary>Le formulaire du centre de ce code : son nom, son année, ses niveaux et ses groupes proposés ; <c>null</c> si le code est inconnu.</summary>
    public async Task<FormulaireDuCentre?> LireAsync(string code)
    {
        await using var espace = espaces.CreateAsyncScope();
        var (contexte, centre) = await OuvrirAsync(espace, code);
        if (centre is null)
            return null;

        var annee = await contexte.AnneesScolaires.OrderByDescending(a => a.Debut).FirstOrDefaultAsync();
        var niveaux = await contexte.Niveaux.OrderBy(n => n.Rang).ThenBy(n => n.Nom).ToListAsync();
        return new FormulaireDuCentre(
            centre.Nom,
            annee is null ? null : $"{annee.Debut.Year}-{annee.Fin.Year}",
            niveaux.Select(n => new NiveauDuFormulaire(n.Id, n.Nom, n.DOffice)).ToList(),
            await GroupesProposesAsync(contexte));
    }

    /// <summary>
    /// Enregistre la demande, avec les règles de la fiche élève (ELV-02) puis celles des groupes. Un champ piège rempli
    /// (un robot) répond comme une demande reçue, sans rien enregistrer. Une demande acceptée prend une des 5 places de
    /// l'heure de cette adresse ; une demande refusée n'en prend pas.
    /// </summary>
    public async Task<IssueDeLEnvoi> EnvoyerAsync(string code, DemandeEnvoyee envoi, string adresse)
    {
        await using var espace = espaces.CreateAsyncScope();
        var (contexte, centre) = await OuvrirAsync(espace, code);
        if (centre is null)
            return Refus(StatusCodes.Status404NotFound, LienInconnu);
        if (!string.IsNullOrWhiteSpace(envoi.SiteWeb))
            return new IssueDeLEnvoi(new DemandeRecue(envoi.Prenom?.Trim() ?? ""), StatusCodes.Status201Created, null);

        var eleves = espace.ServiceProvider.GetRequiredService<ElevesDuCentre>();
        var (fiche, refusDeLaFiche) = await eleves.VerifierLaFicheAsync(envoi.Fiche);
        if (refusDeLaFiche is not null)
            return Refus(refusDeLaFiche.Statut, refusDeLaFiche.Message!);

        var choisis = (envoi.Groupes ?? []).Distinct().ToList();
        if (choisis.Count == 0)
            return Refus(StatusCodes.Status400BadRequest, AucuneMatiere);
        var proposes = (await GroupesProposesAsync(contexte)).ToDictionary(g => g.Id);
        // Un groupe du niveau de l'élève, ou une Formation sans niveau : les seuls que le formulaire lui propose.
        if (choisis.Any(id => !proposes.TryGetValue(id, out var groupe) || (groupe.NiveauId is { } niveau && niveau != fiche!.NiveauId)))
            return Refus(StatusCodes.Status400BadRequest, GroupePlusPropose);
        if (choisis.Select(id => proposes[id].Matiere).Distinct().Count() < choisis.Count)
            return Refus(StatusCodes.Status400BadRequest, UnGroupeParMatiere);
        var amis = string.IsNullOrWhiteSpace(envoi.Amis) ? null : envoi.Amis.Trim();
        if (amis?.Length > DemandeDInscription.LongueurMaximaleDesAmis)
            return Refus(StatusCodes.Status400BadRequest, AmisTropLongs);
        if (!limite.Prendre(adresse))
            return Refus(StatusCodes.Status429TooManyRequests, TropDEnvois);

        contexte.DemandesDInscription.Add(new DemandeDInscription(fiche!, choisis, amis, temps.GetUtcNow()));
        await contexte.SaveChangesAsync();
        return new IssueDeLEnvoi(new DemandeRecue(fiche!.Prenom), StatusCodes.Status201Created, null);
    }

    /// <summary>Le centre de ce code, devenu le centre courant de l'espace ; <c>null</c> si le code est inconnu.</summary>
    private static async Task<(SchoolHubDbContext Contexte, Centre? Centre)> OuvrirAsync(AsyncServiceScope espace, string code)
    {
        var contexte = espace.ServiceProvider.GetRequiredService<SchoolHubDbContext>();
        var normalise = code.Trim().ToLowerInvariant();
        var centre = await contexte.Centres.SingleOrDefaultAsync(c => c.Code == normalise);
        if (centre is not null)
            espace.ServiceProvider.GetRequiredService<CentreCourant>().Definir(centre.Id);
        return (contexte, centre);
    }

    /// <summary>Les groupes ouverts et pas terminés, par matière puis par nom, avec leur professeur et leur horaire.</summary>
    private async Task<List<GroupeDuFormulaire>> GroupesProposesAsync(SchoolHubDbContext contexte)
    {
        var groupes = await contexte.Groupes.Where(g => g.FermeLe == null).ToListAsync();
        var dernieresSeances = await contexte.Seances
            .GroupBy(s => s.GroupeId)
            .Select(g => new { GroupeId = g.Key, Derniere = g.Max(s => s.Date) })
            .ToDictionaryAsync(g => g.GroupeId, g => g.Derniere);
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);
        var professeurs = await contexte.Professeurs.ToDictionaryAsync(p => p.Id, p => $"{p.Prenom} {p.Nom}");
        var aujourdhui = DateDeTunis.Aujourdhui(temps);

        return groupes
            .Where(g => !CalendrierDuGroupe.EstTermine(g.Type, dernieresSeances.TryGetValue(g.Id, out var derniere) ? derniere : null, aujourdhui))
            .Select(g => new GroupeDuFormulaire(g.Id, matieres[g.MatiereId], g.NiveauId, g.Type.ToString(), professeurs[g.ProfesseurId],
                g.Creneaux.Select(CreneauDuGroupe.De).ToList()))
            .OrderBy(g => g.Matiere).ThenBy(g => g.Professeur).ThenBy(g => g.Id)
            .ToList();
    }

    private static IssueDeLEnvoi Refus(int statut, string message) => new(null, statut, message);
}

/// <summary>Ce que l'envoi a donné : la demande reçue et le statut HTTP, ou un refus (statut et message).</summary>
public sealed record IssueDeLEnvoi(DemandeRecue? Recue, int Statut, string? Message);
