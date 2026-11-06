using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Dates;

namespace SchoolHub.Modules.Inscriptions;

/// <summary>
/// CAI-01, CAI-02 et CAI-08 : la couleur des élèves et de leurs inscriptions, le badge des groupes, et ce qu'un élève
/// doit à ce jour pour son rappel de paiement. Tout se calcule, comme ce qu'il doit : rien ne s'enregistre.
/// </summary>
public sealed partial class InscriptionsDuCentre
{
    /// <summary>La couleur d'une inscription : la pire de ses sommes dues et pas payées. <c>null</c> pour un prix spécial à 0 TND.</summary>
    private static Couleur? CouleurDe(Echeance echeance, DateOnly aujourdhui)
    {
        // CAI-01 : un élève à prix spécial 0 n'a jamais de couleur.
        if (echeance.Inscription.PrixSpecial == 0m)
            return null;
        return echeance.Sommes
            .Where(s => !s.Reglee && s.Montant > 0)
            .Select(s => CouleurDesPaiements.DUneSomme(echeance.Calendrier.Seances, s.Seance - echeance.Calendrier.PremiereSeance, s.Date, aujourdhui))
            .Aggregate(Couleur.AJour, CouleurDesPaiements.Pire);
    }

    /// <summary>
    /// CAI-01 et CAI-08 : la situation de chaque élève demandé : la pire couleur de ses inscriptions et de son assurance,
    /// la couleur de chaque inscription, et ce qu'il doit à ce jour (pour le rappel de paiement). Un élève sans
    /// inscription est à jour.
    /// </summary>
    internal async Task<Dictionary<Guid, SituationDeLEleve>> SituationsAsync(IReadOnlyCollection<Guid> eleveIds)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var inscriptions = await contexte.Inscriptions.Where(i => eleveIds.Contains(i.EleveId)).ToListAsync();
        var echeances = await EcheancesAsync(inscriptions);
        var matiereIds = echeances.Select(e => e.Groupe.MatiereId).Distinct().ToList();
        var matieres = await contexte.Matieres.Where(m => matiereIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Nom);
        var assurances = await AssurancesAPayerAsync(eleveIds, echeances);

        return eleveIds.ToDictionary(id => id, id =>
        {
            var siennes = echeances.Where(e => e.Inscription.EleveId == id).ToList();
            var couleurs = siennes.ToDictionary(e => e.Inscription.Id, e => CouleurDe(e, aujourdhui));
            var dues = siennes
                .SelectMany(e => e.Sommes
                    .Where(s => !s.Reglee && s.Montant > 0 && s.Date <= aujourdhui)
                    .Select(s => new SommeDueAujourdhui(s.Libelle, matieres[e.Groupe.MatiereId], s.Montant, s.Date)))
                .ToList();
            var pire = couleurs.Values.OfType<Couleur>().Aggregate(Couleur.AJour, CouleurDesPaiements.Pire);
            if (assurances.TryGetValue(id, out var assurance) && assurance.DueLe <= aujourdhui)
            {
                pire = CouleurDesPaiements.Pire(pire, assurance.Couleur);
                dues.Add(new SommeDueAujourdhui(assurance.Libelle, null, assurance.Montant, assurance.DueLe));
            }

            return new SituationDeLEleve(pire, couleurs, dues.OrderBy(d => d.DueLe).ToList());
        });
    }

    /// <summary>
    /// CAI-02 : le badge de chaque groupe, d'après ses élèves encore inscrits aujourd'hui. « Groupe payé » quand tous ont
    /// payé ce qui est dû à ce jour (un élève à 0 TND compte comme payé) ; sinon le nombre de rappels à envoyer (élèves
    /// orange ou rouges), ou d'élèves à payer. Un élève en rattrapage n'est pas une inscription : il ne compte pas.
    /// </summary>
    internal async Task<Dictionary<Guid, BadgeDuGroupe>> BadgesAsync(IReadOnlyCollection<Guid>? groupeIds = null)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var requete = contexte.Inscriptions.Where(i => i.ArreteLe == null || i.ArreteLe >= aujourdhui);
        if (groupeIds is not null)
            requete = requete.Where(i => groupeIds.Contains(i.GroupeId));
        var echeances = await EcheancesAsync(await requete.ToListAsync());

        return echeances
            .GroupBy(e => e.Inscription.GroupeId)
            .ToDictionary(g => g.Key, g =>
            {
                var couleurs = g.Select(e => CouleurDe(e, aujourdhui)).ToList();
                var rappels = couleurs.Count(c => c is Couleur.Orange or Couleur.Rouge);
                var aPayer = couleurs.Count(c => c is Couleur.APayer);
                return rappels > 0 ? new BadgeDuGroupe("Rappels", rappels)
                    : aPayer > 0 ? new BadgeDuGroupe("APayer", aPayer)
                    : new BadgeDuGroupe("Paye", 0);
            });
    }

    /// <summary>
    /// L'assurance à payer de chaque élève, cette année : due à la première séance de sa première inscription en Étude,
    /// avec la couleur que lui donnent les séances suivantes de ce groupe. Ni payée ni dispensée.
    /// </summary>
    private async Task<Dictionary<Guid, AssuranceAPayer>> AssurancesAPayerAsync(IReadOnlyCollection<Guid> eleveIds, List<Echeance> echeances)
    {
        var annee = await contexte.AnneesScolaires.OrderByDescending(a => a.Debut).FirstOrDefaultAsync();
        if (annee is null)
            return [];
        var assurances = await contexte.Assurances
            .Where(a => eleveIds.Contains(a.EleveId) && a.AnneeScolaireId == annee.Id && a.DispenseeLe == null && a.Montant > 0
                && !contexte.Reglements.Any(r => r.AssuranceId == a.Id))
            .ToListAsync();
        var aujourdhui = DateDeTunis.Aujourdhui(temps);

        var resultat = new Dictionary<Guid, AssuranceAPayer>();
        foreach (var assurance in assurances)
        {
            // La première séance de l'élève dans chacune de ses Études de l'année ; une inscription arrêtée avant ne compte pas.
            var premieres = echeances
                .Where(e => e.Inscription.EleveId == assurance.EleveId && e.Groupe.Type == TypeDeGroupe.Etude && e.Groupe.AnneeScolaireId == annee.Id)
                .Select(e => (Echeance: e, Rang: e.Calendrier.Seances.ToList().FindIndex(d => d >= e.Inscription.DebutLe)))
                .Where(x => x.Rang >= 0 && (x.Echeance.Inscription.ArreteLe is not { } arrete || x.Echeance.Calendrier.Seances[x.Rang] <= arrete))
                .OrderBy(x => x.Echeance.Calendrier.Seances[x.Rang])
                .ToList();
            if (premieres.Count == 0)
                continue;
            var (echeance, rang) = premieres[0];
            var dueLe = echeance.Calendrier.Seances[rang];
            resultat[assurance.EleveId] = new AssuranceAPayer(AssuranceDeLAnnee.Libelle(annee), assurance.Montant, dueLe,
                CouleurDesPaiements.DUneSomme(echeance.Calendrier.Seances, rang, dueLe, aujourdhui));
        }

        return resultat;
    }

    private sealed record AssuranceAPayer(string Libelle, decimal Montant, DateOnly DueLe, Couleur Couleur);
}

/// <summary>CAI-08 : une somme due à ce jour et pas payée : « Mois 3 », sa matière (vide pour l'assurance), son montant.</summary>
public sealed record SommeDueAujourdhui(string Libelle, string? Matiere, decimal Montant, DateOnly DueLe);

/// <summary>CAI-01 : la pire couleur d'un élève, celle de chacune de ses inscriptions (vide à 0 TND), et ce qu'il doit à ce jour.</summary>
public sealed record SituationDeLEleve(Couleur Couleur, IReadOnlyDictionary<Guid, Couleur?> Inscriptions, List<SommeDueAujourdhui> Dues);

/// <summary>CAI-02 : le badge d'un groupe : « Paye », « Rappels » (avec leur nombre) ou « APayer » (avec le nombre d'élèves).</summary>
public sealed record BadgeDuGroupe(string Etat, int Nombre);
