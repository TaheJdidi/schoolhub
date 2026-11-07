using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Modules.Parts;
using SchoolHub.Modules.Rh;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;

namespace SchoolHub.Modules.Tableau;

/// <summary>
/// TDB-01 à TDB-04 : la vue du propriétaire. Sur un mois du calendrier ou l'année scolaire : l'encaissé (les reçus, sans
/// les annulés, par origine), les impayés, les dépenses (parts des professeurs, salaires, achats) et le bénéfice, avec
/// le détail par groupe. Chaque somme compte dans le mois de sa date. Puis les prix spéciaux et les dispenses, et le journal.
/// </summary>
public sealed class TableauDeBord(
    SchoolHubDbContext contexte,
    InscriptionsDuCentre inscriptions,
    PartsDesProfesseurs parts,
    RessourcesHumaines rh,
    TimeProvider temps)
{
    private const string Etude = "Étude";
    private const string Revision = "Révision";
    private const string Formation = "Formation";
    private const string Bibliotheque = "Bibliothèque";
    private const string Assurance = "Assurance";

    /// <summary>Le nombre de lignes du journal d'une recherche.</summary>
    private const int LignesAuPlus = 500;

    /// <summary>TDB-01 et TDB-02 : un mois (« 2027-10 », par défaut le mois en cours) ou l'année scolaire (<paramref name="annee"/>).</summary>
    public async Task<TableauDUnePeriode> LireAsync(string? mois, bool annee)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var (du, au, libelle) = await PeriodeAsync(mois, annee, aujourdhui);

        // L'encaissé : les lignes des reçus pas annulés de la période, par origine.
        var recus = await contexte.Recus.Where(r => r.AnnuleLe == null && r.Jour >= du && r.Jour <= au).ToListAsync();
        var lignes = recus.SelectMany(r => r.Lignes).ToList();
        var inscriptionIds = lignes.Where(l => l.InscriptionId is not null).Select(l => l.InscriptionId!.Value).Distinct().ToList();
        var groupeDeLInscription = await contexte.Inscriptions.Where(i => inscriptionIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.GroupeId);
        var groupes = await contexte.Groupes.ToDictionaryAsync(g => g.Id);
        var encaisse = new Dictionary<string, decimal> { [Etude] = 0m, [Revision] = 0m, [Formation] = 0m, [Bibliotheque] = 0m, [Assurance] = 0m };
        var encaisseParGroupe = new Dictionary<Guid, decimal>();
        foreach (var ligne in lignes)
        {
            if (ligne.AssuranceId is not null)
                encaisse[Assurance] += ligne.Montant;
            else if (ligne.InscriptionId is { } inscriptionId && groupeDeLInscription.TryGetValue(inscriptionId, out var groupeId))
            {
                encaisse[Origine(groupes[groupeId].Type)] += ligne.Montant;
                encaisseParGroupe[groupeId] = encaisseParGroupe.GetValueOrDefault(groupeId) + ligne.Montant;
            }
            else
                encaisse[Bibliotheque] += ligne.Montant;
        }

        // Les impayés de la période, et les parts des périodes finies dans la période.
        var impayes = await inscriptions.ImpayesAsync(du, au);
        var lignesDeParts = (await parts.ListerAsync()).Professeurs.SelectMany(p => p.Lignes).Where(l => l.Fin >= du && l.Fin <= au).ToList();
        var partsDesProfesseurs = lignesDeParts.Sum(l => l.MontantDuGroupe - l.PartDuCentre);

        // Les salaires de chaque mois de la période (payés, figés, ou à payer), et les achats.
        var salaires = 0m;
        for (var premier = new DateOnly(du.Year, du.Month, 1); premier <= au && premier <= aujourdhui; premier = premier.AddMonths(1))
        {
            var duMois = await rh.SalairesAsync(premier.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            salaires += duMois.TotalPaye + duMois.TotalAPayer;
        }
        var achats = await contexte.Achats.Where(a => a.Jour >= du && a.Jour <= au).SumAsync(a => (decimal?)a.Montant) ?? 0m;

        // Le détail par groupe : tout groupe qui a quelque chose dans la période.
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);
        var professeurs = await contexte.Professeurs.ToDictionaryAsync(p => p.Id, p => $"{p.Prenom} {p.Nom}");
        var groupeIds = encaisseParGroupe.Keys.Concat(impayes.ParGroupe.Where(x => x.Value > 0).Select(x => x.Key)).Concat(lignesDeParts.Select(l => l.GroupeId)).Distinct();
        var parGroupe = groupeIds.Select(id => groupes[id])
            .OrderBy(g => professeurs[g.ProfesseurId]).ThenBy(g => g.Nom).ThenBy(g => g.Id)
            .Select(g =>
            {
                var sesParts = lignesDeParts.Where(l => l.GroupeId == g.Id).ToList();
                return new LigneDuTableau(g.Id, g.Nom, matieres[g.MatiereId], Origine(g.Type), g.ProfesseurId, professeurs[g.ProfesseurId],
                    sesParts.Count == 0 ? null : sesParts.Sum(l => l.MontantDuGroupe), encaisseParGroupe.GetValueOrDefault(g.Id),
                    impayes.ParGroupe.GetValueOrDefault(g.Id), sesParts.Count == 0 ? null : sesParts.Sum(l => l.MontantDuGroupe - l.PartDuCentre),
                    sesParts.Count == 0 ? null : sesParts.Sum(l => l.PartDuCentre));
            })
            .ToList();

        var totalEncaisse = encaisse.Values.Sum();
        var depenses = partsDesProfesseurs + salaires + achats;
        return new TableauDUnePeriode(libelle, du, au, totalEncaisse, encaisse.Select(x => new Montant(x.Key, x.Value)).ToList(),
            impayes.ParGroupe.Values.Sum() + impayes.Assurances, impayes.Assurances, depenses, partsDesProfesseurs, salaires, achats, totalEncaisse - depenses, parGroupe);
    }

    /// <summary>TDB-03 : les prix spéciaux en cours, et les dispenses d'assurance de l'année.</summary>
    public async Task<PrixSpeciauxEtDispenses> PrixSpeciauxEtDispensesAsync()
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var avecPrix = await contexte.Inscriptions.Where(i => i.PrixSpecial != null && (i.ArreteLe == null || i.ArreteLe >= aujourdhui))
            .Join(contexte.Eleves, i => i.EleveId, e => e.Id, (i, e) => new { Inscription = i, Eleve = e })
            .ToListAsync();
        var groupes = await contexte.Groupes.ToDictionaryAsync(g => g.Id);
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);
        var prix = avecPrix
            .OrderBy(x => x.Eleve.Nom).ThenBy(x => x.Eleve.Prenom)
            .Select(x =>
            {
                var groupe = groupes[x.Inscription.GroupeId];
                var normal = groupe.Type == TypeDeGroupe.Etude ? groupe.Prix.MaxBy(p => p.APartirDuMois)?.Montant : groupe.PrixTotal;
                return new PrixSpecialEnCours(x.Eleve.Id, $"{x.Eleve.Prenom} {x.Eleve.Nom}", $"{matieres[groupe.MatiereId]} · {groupe.Nom}",
                    groupe.Type == TypeDeGroupe.Etude ? "par Mois" : "en tout", normal, x.Inscription.PrixSpecial!.Value, x.Inscription.RaisonDuPrixSpecial,
                    x.Inscription.PrixSpecialAccordeLe, x.Inscription.PrixSpecialAccordePar);
            })
            .ToList();

        var annee = await contexte.AnneesScolaires.OrderByDescending(a => a.Debut).FirstOrDefaultAsync();
        var dispenses = annee is null
            ? []
            : (await contexte.Assurances.Where(a => a.AnneeScolaireId == annee.Id && a.DispenseeLe != null)
                    .Join(contexte.Eleves, a => a.EleveId, e => e.Id, (a, e) => new { Assurance = a, Eleve = e })
                    .ToListAsync())
                .OrderBy(x => x.Eleve.Nom).ThenBy(x => x.Eleve.Prenom)
                .Select(x => new DispenseDeLAnnee(x.Eleve.Id, $"{x.Eleve.Prenom} {x.Eleve.Nom}", x.Assurance.Montant, x.Assurance.RaisonDeLaDispense,
                    x.Assurance.DispenseeLe!.Value, x.Assurance.DispenseePar))
                .ToList();
        return new PrixSpeciauxEtDispenses(prix, dispenses);
    }

    /// <summary>TDB-04 : le journal, les plus récentes en premier, filtré par personne, par action et par période (500 lignes au plus).</summary>
    public async Task<JournalFiltre> JournalAsync(Guid? compteId, string? action, DateOnly? du, DateOnly? au)
    {
        var requete = contexte.LignesDuJournal.AsQueryable();
        if (compteId is { } id)
            requete = requete.Where(l => l.AuteurId == id);
        if (Enum.TryParse<ActionDuJournal>(action, out var lAction) && Enum.IsDefined(lAction) && !int.TryParse(action, out _))
            requete = requete.Where(l => l.Action == lAction);
        // Les jours de Tunis : de minuit à minuit, UTC+1.
        if (du is { } debut)
        {
            var depuis = new DateTimeOffset(debut.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(1)).ToUniversalTime();
            requete = requete.Where(l => l.Quand >= depuis);
        }
        if (au is { } fin)
        {
            var jusqua = new DateTimeOffset(fin.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(1)).ToUniversalTime();
            requete = requete.Where(l => l.Quand < jusqua);
        }
        var lignes = await requete.OrderByDescending(l => l.Quand).ThenByDescending(l => l.Id).Take(LignesAuPlus).ToListAsync();
        var auteurs = await contexte.LignesDuJournal.Where(l => l.AuteurId != null).Select(l => new { l.AuteurId, l.Auteur }).Distinct().ToListAsync();
        return new JournalFiltre(
            lignes.Select(l => new LigneDuJournalDecrite(l.Quand, l.Auteur, l.Action.ToString(), l.Detail, l.Avant, l.Apres)).ToList(),
            auteurs.DistinctBy(a => a.AuteurId).OrderBy(a => a.Auteur).Select(a => new AuteurDuJournal(a.AuteurId!.Value, a.Auteur)).ToList(),
            Enum.GetNames<ActionDuJournal>().ToList());
    }

    private async Task<(DateOnly Du, DateOnly Au, string Libelle)> PeriodeAsync(string? mois, bool annee, DateOnly aujourdhui)
    {
        if (annee && await contexte.AnneesScolaires.OrderByDescending(a => a.Debut).FirstOrDefaultAsync(a => a.Debut <= aujourdhui) is { } scolaire)
            return (scolaire.Debut, scolaire.Fin, $"Année {scolaire.Debut.Year}-{scolaire.Fin.Year}");
        var premier = mois is not null && DateOnly.TryParseExact($"{mois.Trim()}-01", "yyyy-MM-dd", out var lu) ? lu : new DateOnly(aujourdhui.Year, aujourdhui.Month, 1);
        var nom = premier.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"));
        return (premier, premier.AddMonths(1).AddDays(-1), char.ToUpperInvariant(nom[0]) + nom[1..]);
    }

    private static string Origine(TypeDeGroupe type) => type switch
    {
        TypeDeGroupe.Etude => Etude,
        TypeDeGroupe.Revision => Revision,
        _ => Formation,
    };
}

public sealed record Montant(string Libelle, decimal Valeur);

/// <summary>
/// TDB-02 : une ligne par groupe : le montant du groupe et les parts (des périodes finies dans la période ; vides sinon),
/// l'encaissé et les impayés de la période.
/// </summary>
public sealed record LigneDuTableau(
    Guid GroupeId,
    string Groupe,
    string Matiere,
    string Type,
    Guid ProfesseurId,
    string Professeur,
    decimal? MontantDuGroupe,
    decimal Encaisse,
    decimal Impayes,
    decimal? PartDuProfesseur,
    decimal? PartDuCentre);

/// <summary>TDB-01 : le tableau d'une période.</summary>
public sealed record TableauDUnePeriode(
    string Periode,
    DateOnly Du,
    DateOnly Au,
    decimal Encaisse,
    List<Montant> EncaisseParOrigine,
    decimal Impayes,
    decimal ImpayesDAssurance,
    decimal Depenses,
    decimal PartsDesProfesseurs,
    decimal Salaires,
    decimal Achats,
    decimal Benefice,
    List<LigneDuTableau> ParGroupe);

/// <summary>TDB-03 : un prix spécial en cours : l'élève, le groupe, le prix normal (par Mois, ou en tout), le prix spécial, la raison, quand et qui.</summary>
public sealed record PrixSpecialEnCours(Guid EleveId, string Eleve, string Groupe, string Unite, decimal? PrixNormal, decimal PrixSpecial, string? Raison,
    DateOnly? Le, string? Par);

/// <summary>TDB-03 : une dispense d'assurance de l'année.</summary>
public sealed record DispenseDeLAnnee(Guid EleveId, string Eleve, decimal Montant, string? Raison, DateOnly Le, string? Par);

public sealed record PrixSpeciauxEtDispenses(List<PrixSpecialEnCours> PrixSpeciaux, List<DispenseDeLAnnee> Dispenses);

public sealed record LigneDuJournalDecrite(DateTimeOffset Quand, string Auteur, string Action, string Detail, string? Avant, string? Apres);

public sealed record AuteurDuJournal(Guid Id, string Nom);

/// <summary>TDB-04 : les lignes, et de quoi filtrer (les auteurs, les actions).</summary>
public sealed record JournalFiltre(List<LigneDuJournalDecrite> Lignes, List<AuteurDuJournal> Auteurs, List<string> Actions);
