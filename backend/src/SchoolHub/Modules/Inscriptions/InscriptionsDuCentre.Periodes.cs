using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Dates;

namespace SchoolHub.Modules.Inscriptions;

/// <summary>
/// PRT-01 : les périodes de chaque groupe et leur montant du groupe : ce que ses inscriptions doivent pour la période,
/// payé ou non, prix spéciaux et prorata compris, sans l'assurance. Tout se calcule depuis l'échéancier.
/// </summary>
public sealed partial class InscriptionsDuCentre
{
    /// <summary>
    /// PRT-01 : les périodes finies de tous les groupes qui ont au moins une inscription : chaque Mois d'une Étude, le
    /// groupe entier d'une Révision ou d'une Formation. Une période est finie quand sa dernière séance a eu lieu avant
    /// aujourd'hui (un Mois incomplet : quand le groupe est fermé, ou qu'aucune séance ne vient plus).
    /// </summary>
    internal async Task<List<PeriodeDUnGroupe>> PeriodesFiniesAsync()
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var echeances = await EcheancesAsync(await contexte.Inscriptions.ToListAsync());
        var periodes = new List<PeriodeDUnGroupe>();
        foreach (var duGroupe in echeances.GroupBy(e => e.Groupe.Id))
        {
            var groupe = duGroupe.First().Groupe;
            var dates = duGroupe.First().Calendrier.Seances;
            if (dates.Count == 0)
                continue;
            var sommes = duGroupe.SelectMany(e => e.Sommes).ToList();
            var plusRien = groupe.FermeLe is not null || dates[^1] < aujourdhui;

            if (groupe.Type == TypeDeGroupe.Etude)
            {
                var numeros = Enumerable.Range(groupe.PremiereSeance, dates.Count).ToList();
                foreach (var mois in numeros.GroupBy(CalendrierDuGroupe.MoisDeLaSeance))
                {
                    var debut = dates[mois.First() - groupe.PremiereSeance];
                    var fin = dates[mois.Last() - groupe.PremiereSeance];
                    var complet = mois.Last() == CalendrierDuGroupe.PremiereSeanceDuMois(mois.Key) + CalendrierDuGroupe.SeancesParMois - 1;
                    if (fin >= aujourdhui || (!complet && !plusRien))
                        continue;
                    var cle = $"Mois {mois.Key}";
                    periodes.Add(new PeriodeDUnGroupe(groupe, cle, cle, debut, fin,
                        sommes.Where(s => s.Cle == cle).Sum(s => s.Montant + s.ReportDeduit)));
                }
            }
            else if (plusRien && dates[^1] < aujourdhui)
            {
                var libelle = groupe.Type == TypeDeGroupe.Revision ? "Toute la Révision" : "Toute la Formation";
                periodes.Add(new PeriodeDUnGroupe(groupe, CleDuGroupeEntier, libelle, dates[0], dates[^1], sommes.Sum(s => s.Montant + s.ReportDeduit)));
            }
        }

        return periodes;
    }

    /// <summary>
    /// TDB-01 et TDB-02 : ce qui était dû dans la période (jusqu'à aujourd'hui au plus) et n'est toujours pas payé : par
    /// groupe, les sommes des inscriptions ; à part, les assurances.
    /// </summary>
    internal async Task<ImpayesDeLaPeriode> ImpayesAsync(DateOnly du, DateOnly au)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var fin = au < aujourdhui ? au : aujourdhui;
        var echeances = await EcheancesAsync(await contexte.Inscriptions.ToListAsync());
        var parGroupe = echeances.GroupBy(e => e.Groupe.Id)
            .ToDictionary(g => g.Key, g => g.SelectMany(e => e.Sommes).Where(s => !s.Reglee && s.Montant > 0 && s.Date >= du && s.Date <= fin).Sum(s => s.Montant));
        var eleveIds = echeances.Select(e => e.Inscription.EleveId).Distinct().ToList();
        var assurances = (await AssurancesAPayerAsync(eleveIds, echeances)).Values.Where(a => a.DueLe >= du && a.DueLe <= fin).Sum(a => a.Montant);
        return new ImpayesDeLaPeriode(parGroupe, assurances);
    }

    /// <summary>La clé de la période d'une Révision ou d'une Formation : le groupe entier.</summary>
    internal const string CleDuGroupeEntier = "Groupe";
}

/// <summary>TDB-01 : les impayés d'une période : par groupe, et ceux des assurances.</summary>
internal sealed record ImpayesDeLaPeriode(IReadOnlyDictionary<Guid, decimal> ParGroupe, decimal Assurances);

/// <summary>
/// PRT-01 : une période finie d'un groupe : sa clé (« Mois 3 », ou « Groupe » pour une Révision ou une Formation), son
/// libellé, ses dates, et le montant du groupe.
/// </summary>
internal sealed record PeriodeDUnGroupe(Groupe Groupe, string Cle, string Libelle, DateOnly Debut, DateOnly Fin, decimal MontantDuGroupe);
