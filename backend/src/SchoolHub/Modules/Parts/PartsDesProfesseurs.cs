using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Parts;

/// <summary>
/// PRT-01 à PRT-04 : ce que le centre doit à chaque professeur. Une ligne par période finie de chacun de ses groupes : le
/// montant du groupe (payé ou non), la part du centre, l'avance déduite et ce qui reste à verser. Les avances se déduisent
/// des parts pas encore payées, de la plus ancienne à la plus récente ; une part payée est figée.
/// </summary>
public sealed class PartsDesProfesseurs(SchoolHubDbContext contexte, InscriptionsDuCentre inscriptions, CentreCourant centreCourant, TimeProvider temps)
{
    public const string ProfesseurIntrouvable = "Professeur introuvable";
    private const string PartIntrouvable = "Part introuvable";
    private const string ChoisirLeProfesseur = "Choisissez le professeur";
    private const string MontantDeLAvance = "Le montant de l'avance est positif, au millime près";
    private const string ChoisirLaDate = "Choisissez la date";
    private const string Pourcentage = "La part du centre va de 0 à 100 %, au centième près";

    private Guid CentreId => centreCourant.Id ?? throw new InvalidOperationException("Aucun centre courant.");

    /// <summary>PRT-01 : les parts de tous les professeurs qui en ont, avec leur résumé et celui du centre.</summary>
    public async Task<PartsDuCentre> ListerAsync()
    {
        var periodes = await inscriptions.PeriodesFiniesAsync();
        var payees = await contexte.PartsPayees.ToListAsync();
        var avances = await contexte.Avances.ToListAsync();
        var regles = await contexte.PartsDesProfesseurs.OrderBy(p => p.ChangeeLe).ThenBy(p => p.Id).ToListAsync();
        var parDefaut = await contexte.Centres.Where(c => c.Id == CentreId).Select(c => c.PartDuCentreParDefaut).SingleAsync() ?? 0m;
        var professeurIds = periodes.Select(p => p.Groupe.ProfesseurId).Concat(avances.Select(a => a.ProfesseurId)).Distinct().ToList();
        var professeurs = await contexte.Professeurs.Where(p => professeurIds.Contains(p.Id)).OrderBy(p => p.Nom).ThenBy(p => p.Prenom).ToListAsync();
        var matieres = await contexte.Matieres.ToDictionaryAsync(m => m.Id, m => m.Nom);

        var resultat = new List<PartsDUnProfesseur>();
        foreach (var professeur in professeurs)
        {
            var siennes = regles.Where(r => r.ProfesseurId == professeur.Id).ToList();
            decimal PourcentageAu(DateOnly debut) =>
                siennes.LastOrDefault(r => r.ChangeeLe < debut) is { } regle ? regle.Pourcentage ?? parDefaut : parDefaut;

            var lignes = new List<LigneDePart>();
            var solde = avances.Where(a => a.ProfesseurId == professeur.Id).Sum(a => a.Montant)
                - payees.Where(p => p.ProfesseurId == professeur.Id).Sum(p => p.AvanceDeduite);
            foreach (var periode in periodes.Where(p => p.Groupe.ProfesseurId == professeur.Id)
                         .OrderBy(p => p.Fin).ThenBy(p => p.Groupe.Nom).ThenBy(p => p.Debut))
            {
                var groupe = $"{periode.Groupe.Nom}";
                if (payees.FirstOrDefault(p => p.GroupeId == periode.Groupe.Id && p.Cle == periode.Cle) is { } payee)
                {
                    lignes.Add(new LigneDePart(periode.Groupe.Id, groupe, matieres[periode.Groupe.MatiereId], periode.Cle, periode.Libelle, periode.Debut,
                        periode.Fin, payee.MontantDuGroupe, payee.Pourcentage, payee.PartDuCentre, payee.AvanceDeduite, payee.AVerser, true, payee.PayeeLe));
                    continue;
                }

                var pourcentage = PourcentageAu(periode.Debut);
                var partDuCentre = decimal.Round(periode.MontantDuGroupe * pourcentage / 100m, 3, MidpointRounding.AwayFromZero);
                var net = periode.MontantDuGroupe - partDuCentre;
                var deduite = Math.Max(0m, Math.Min(solde, net));
                solde -= deduite;
                lignes.Add(new LigneDePart(periode.Groupe.Id, groupe, matieres[periode.Groupe.MatiereId], periode.Cle, periode.Libelle, periode.Debut,
                    periode.Fin, periode.MontantDuGroupe, pourcentage, partDuCentre, deduite, net - deduite, false, null));
            }

            var actuelle = siennes.LastOrDefault() is { Pourcentage: { } p } ? p : parDefaut;
            resultat.Add(new PartsDUnProfesseur(professeur.Id, $"{professeur.Prenom} {professeur.Nom}", actuelle,
                lignes.Where(l => !l.Payee).Sum(l => l.AVerser), lignes.Where(l => l.Payee).Sum(l => l.AVerser), Math.Max(0m, solde), lignes));
        }

        return new PartsDuCentre(resultat.Sum(p => p.ResteAVerser), resultat.Sum(p => p.DejaVerse), resultat.Sum(p => p.AvancesRestantes), resultat);
    }

    /// <summary>PRT-02 : « Payée » fige la ligne telle que calculée aujourd'hui ; « Non payée » la défige.</summary>
    public async Task<IssueDesParts> MarquerAsync(PaiementDUnePart saisie)
    {
        var parts = await ListerAsync();
        var ligne = parts.Professeurs.SelectMany(p => p.Lignes.Select(l => (Professeur: p, Ligne: l)))
            .FirstOrDefault(x => x.Ligne.GroupeId == saisie.GroupeId && x.Ligne.Cle == saisie.Cle);
        if (ligne.Ligne is null)
            return new IssueDesParts(null, StatusCodes.Status404NotFound, PartIntrouvable);

        var existante = await contexte.PartsPayees.SingleOrDefaultAsync(p => p.GroupeId == saisie.GroupeId && p.Cle == saisie.Cle);
        if (saisie.Payee == true && existante is null)
        {
            var l = ligne.Ligne;
            contexte.PartsPayees.Add(new PartPayee(l.GroupeId, l.Cle, ligne.Professeur.Id, l.MontantDuGroupe, l.Pourcentage, l.PartDuCentre, l.AvanceDeduite,
                l.AVerser, DateDeTunis.Aujourdhui(temps)));
        }
        else if (saisie.Payee != true && existante is not null)
        {
            contexte.PartsPayees.Remove(existante);
        }

        await contexte.SaveChangesAsync();
        return new IssueDesParts(await ListerAsync(), StatusCodes.Status200OK, null);
    }

    /// <summary>PRT-03 : une avance au professeur, déduite de ses parts suivantes.</summary>
    public async Task<IssueDesParts> AvancerAsync(AvanceSaisie saisie)
    {
        if (saisie.ProfesseurId is not { } professeurId)
            return new IssueDesParts(null, StatusCodes.Status400BadRequest, ChoisirLeProfesseur);
        if (saisie.Montant is not { } montant || montant <= 0 || decimal.Round(montant, 3) != montant || montant > 999_999_999m)
            return new IssueDesParts(null, StatusCodes.Status400BadRequest, MontantDeLAvance);
        if (saisie.Le is not { } le)
            return new IssueDesParts(null, StatusCodes.Status400BadRequest, ChoisirLaDate);
        if (!await contexte.Professeurs.AnyAsync(p => p.Id == professeurId))
            return new IssueDesParts(null, StatusCodes.Status404NotFound, ProfesseurIntrouvable);

        contexte.Avances.Add(new Avance(professeurId, montant, le));
        await contexte.SaveChangesAsync();
        return new IssueDesParts(await ListerAsync(), StatusCodes.Status201Created, null);
    }

    /// <summary>PRT-04 : la part du centre d'un professeur, celle du centre s'il n'en a pas. <c>null</c> s'il n'est pas dans ce centre.</summary>
    public async Task<PartDuCentreDUnProfesseur?> LirePartAsync(Guid professeurId)
    {
        if (!await contexte.Professeurs.AnyAsync(p => p.Id == professeurId))
            return null;
        var parDefaut = await contexte.Centres.Where(c => c.Id == CentreId).Select(c => c.PartDuCentreParDefaut).SingleAsync() ?? 0m;
        var derniere = await contexte.PartsDesProfesseurs.Where(p => p.ProfesseurId == professeurId)
            .OrderByDescending(p => p.ChangeeLe).ThenByDescending(p => p.Id).FirstOrDefaultAsync();
        return new PartDuCentreDUnProfesseur(derniere?.Pourcentage, parDefaut, derniere?.ChangeeLe);
    }

    /// <summary>PRT-04 : change la part du centre d'un professeur, pour les périodes qui commencent après aujourd'hui.</summary>
    public async Task<IssueDeLaPart> ChangerLaPartAsync(Guid professeurId, PartSaisie saisie)
    {
        if (!await contexte.Professeurs.AnyAsync(p => p.Id == professeurId))
            return new IssueDeLaPart(null, StatusCodes.Status404NotFound, ProfesseurIntrouvable);
        if (saisie.Pourcentage is { } p && (p < 0 || p > 100 || decimal.Round(p, 2) != p))
            return new IssueDeLaPart(null, StatusCodes.Status400BadRequest, Pourcentage);

        contexte.PartsDesProfesseurs.Add(new PartDUnProfesseur(professeurId, saisie.Pourcentage, DateDeTunis.Aujourdhui(temps)));
        await contexte.SaveChangesAsync();
        return new IssueDeLaPart(await LirePartAsync(professeurId), StatusCodes.Status200OK, null);
    }
}

/// <summary>PRT-01 : une ligne : le groupe et la période, le montant du groupe, la part du centre, l'avance déduite, à verser, et si elle est payée.</summary>
public sealed record LigneDePart(
    Guid GroupeId,
    string Groupe,
    string Matiere,
    string Cle,
    string Periode,
    DateOnly Debut,
    DateOnly Fin,
    decimal MontantDuGroupe,
    decimal Pourcentage,
    decimal PartDuCentre,
    decimal AvanceDeduite,
    decimal AVerser,
    bool Payee,
    DateOnly? PayeeLe);

/// <summary>PRT-01 : les parts d'un professeur, sa part du centre actuelle, et son résumé.</summary>
public sealed record PartsDUnProfesseur(Guid Id, string Nom, decimal Pourcentage, decimal ResteAVerser, decimal DejaVerse, decimal AvancesRestantes,
    List<LigneDePart> Lignes);

/// <summary>PRT-01 : le résumé du centre et les parts de chaque professeur.</summary>
public sealed record PartsDuCentre(decimal ResteAVerser, decimal DejaVerse, decimal AvancesRestantes, List<PartsDUnProfesseur> Professeurs);

public sealed record PaiementDUnePart(Guid GroupeId, string Cle, bool? Payee);

public sealed record AvanceSaisie(Guid? ProfesseurId, decimal? Montant, DateOnly? Le);

public sealed record PartSaisie(decimal? Pourcentage);

/// <summary>PRT-04 : sa part (vide : celle du centre), celle du centre, et la date du dernier changement.</summary>
public sealed record PartDuCentreDUnProfesseur(decimal? Pourcentage, decimal ParDefaut, DateOnly? ChangeeLe);

public sealed record IssueDesParts(PartsDuCentre? Parts, int Statut, string? Message);

public sealed record IssueDeLaPart(PartDuCentreDUnProfesseur? Part, int Statut, string? Message);
