using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Rh;

/// <summary>
/// RHA-01 à RHA-03 : les employés du centre, leurs congés et leurs salaires, pour le propriétaire. Un employé parti
/// sort de la liste ; ses congés et ses salaires restent. Les salaires se paient hors caisse.
/// </summary>
public sealed class RessourcesHumaines(SchoolHubDbContext contexte, TimeProvider temps)
{
    public const string EmployeIntrouvable = "Employé introuvable";
    public const string CongeIntrouvable = "Congé introuvable";
    private const string PrenomObligatoire = "Le prénom est obligatoire";
    private const string NomObligatoire = "Le nom est obligatoire";
    private const string NomTropLong = "Le prénom et le nom font au plus 100 caractères chacun";
    private const string EmailPasValable = "L'email n'est pas valable";
    private const string TelephonePasValable = "Le téléphone doit avoir 8 chiffres";
    private const string PosteObligatoire = "Écrivez le poste";
    private const string PosteTropLong = "Le poste fait au plus 100 caractères";
    private const string HorairesTropLongs = "Les horaires font au plus 300 caractères";
    private const string SalairePasValable = "Le salaire est 0 ou plus, au millime près";
    private const string ChoisirLesDates = "Choisissez les dates du congé";
    private const string CongeALEnvers = "Le congé finit après son début";
    private const string NoteTropLongue = "La note fait au plus 200 caractères";
    private const string ChoisirLeMois = "Choisissez le mois";

    /// <summary>RHA-01 et RHA-02 : tous les employés, par nom puis prénom, avec leurs jours de congé dans l'année.</summary>
    public async Task<List<EmployeDecrit>> ListerAsync(int? annee)
    {
        var lAnnee = annee ?? DateDeTunis.Aujourdhui(temps).Year;
        var employes = await contexte.Employes.OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ThenBy(e => e.Id).ToListAsync();
        var conges = (await contexte.Conges.Where(c => c.Du.Year <= lAnnee && c.Au.Year >= lAnnee).ToListAsync()).ToLookup(c => c.EmployeId);
        return employes.Select(e => Decrire(e, conges[e.Id].Sum(c => c.JoursDans(lAnnee)))).ToList();
    }

    public async Task<IssueRh<EmployeDecrit>> AjouterAsync(EmployeSaisi saisie)
    {
        var (fiche, refus) = Lire(saisie);
        if (refus is not null)
            return new(default, StatusCodes.Status400BadRequest, refus);
        var employe = new Employe(fiche!);
        contexte.Employes.Add(employe);
        await contexte.SaveChangesAsync();
        return new(Decrire(employe, 0), StatusCodes.Status201Created, null);
    }

    public async Task<IssueRh<EmployeDecrit>> ModifierAsync(Guid id, EmployeSaisi saisie)
    {
        var employe = await contexte.Employes.SingleOrDefaultAsync(e => e.Id == id);
        if (employe is null)
            return new(default, StatusCodes.Status404NotFound, EmployeIntrouvable);
        var (fiche, refus) = Lire(saisie);
        if (refus is not null)
            return new(default, StatusCodes.Status400BadRequest, refus);
        employe.Modifier(fiche!);
        await contexte.SaveChangesAsync();
        return new(await DecrireAsync(employe), StatusCodes.Status200OK, null);
    }

    /// <summary>RHA-01 : l'employé quitte le centre aujourd'hui (<paramref name="parti"/>), ou y revient.</summary>
    public async Task<IssueRh<EmployeDecrit>> ChangerLaPresenceAsync(Guid id, bool parti)
    {
        var employe = await contexte.Employes.SingleOrDefaultAsync(e => e.Id == id);
        if (employe is null)
            return new(default, StatusCodes.Status404NotFound, EmployeIntrouvable);
        if (parti)
            employe.Partir(DateDeTunis.Aujourdhui(temps));
        else
            employe.Revenir();
        await contexte.SaveChangesAsync();
        return new(await DecrireAsync(employe), StatusCodes.Status200OK, null);
    }

    /// <summary>RHA-02 : les congés d'un employé, les plus récents en premier ; <c>null</c> s'il n'est pas dans ce centre.</summary>
    public async Task<List<CongeDecrit>?> CongesAsync(Guid employeId)
    {
        if (!await contexte.Employes.AnyAsync(e => e.Id == employeId))
            return null;
        return (await contexte.Conges.Where(c => c.EmployeId == employeId).OrderByDescending(c => c.Du).ToListAsync()).Select(Decrire).ToList();
    }

    public async Task<IssueRh<CongeDecrit>> AjouterUnCongeAsync(Guid employeId, CongeSaisi saisie)
    {
        if (!await contexte.Employes.AnyAsync(e => e.Id == employeId))
            return new(default, StatusCodes.Status404NotFound, EmployeIntrouvable);
        if (saisie.Du is not { } du || saisie.Au is not { } au)
            return new(default, StatusCodes.Status400BadRequest, ChoisirLesDates);
        if (au < du)
            return new(default, StatusCodes.Status400BadRequest, CongeALEnvers);
        var note = string.IsNullOrWhiteSpace(saisie.Note) ? null : saisie.Note.Trim();
        if (note is { Length: > Conge.LongueurMaximaleDeLaNote })
            return new(default, StatusCodes.Status400BadRequest, NoteTropLongue);
        var conge = new Conge(employeId, du, au, note);
        contexte.Conges.Add(conge);
        await contexte.SaveChangesAsync();
        return new(Decrire(conge), StatusCodes.Status201Created, null);
    }

    public async Task<IssueRh<CongeDecrit>> RetirerUnCongeAsync(Guid id)
    {
        var conge = await contexte.Conges.SingleOrDefaultAsync(c => c.Id == id);
        if (conge is null)
            return new(default, StatusCodes.Status404NotFound, CongeIntrouvable);
        contexte.Conges.Remove(conge);
        await contexte.SaveChangesAsync();
        return new(default, StatusCodes.Status204NoContent, null);
    }

    /// <summary>
    /// RHA-03 : les salaires d'un mois (par défaut le mois en cours) : chaque employé présent ce mois-là, et ceux payés
    /// pour ce mois même s'ils sont partis depuis ; payé (figé) ou à payer ; et les totaux.
    /// </summary>
    public async Task<SalairesDuMois> SalairesAsync(string? mois)
    {
        var premier = LireLeMois(mois) ?? PremierDuMois(DateDeTunis.Aujourdhui(temps));
        var payes = await contexte.SalairesPayes.Where(s => s.Mois == premier).ToDictionaryAsync(s => s.EmployeId);
        var employes = await contexte.Employes.Where(e => e.PartiLe == null || e.PartiLe >= premier || payes.Keys.Contains(e.Id))
            .OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ThenBy(e => e.Id).ToListAsync();
        var lignes = employes.Select(e => payes.TryGetValue(e.Id, out var paye)
                ? new SalaireDuMois(e.Id, $"{e.Prenom} {e.Nom}", e.Poste, paye.Montant, true, paye.PayeLe)
                : new SalaireDuMois(e.Id, $"{e.Prenom} {e.Nom}", e.Poste, e.Salaire, false, null))
            .ToList();
        return new SalairesDuMois(premier.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), lignes,
            lignes.Where(l => l.Paye).Sum(l => l.Montant), lignes.Where(l => !l.Paye).Sum(l => l.Montant));
    }

    /// <summary>RHA-03 : « Payé » fige le salaire du mois et la date ; « À payer » le défige.</summary>
    public async Task<IssueRh<SalairesDuMois>> MarquerUnSalaireAsync(SalaireSaisi saisie)
    {
        if (LireLeMois(saisie.Mois) is not { } premier)
            return new(default, StatusCodes.Status400BadRequest, ChoisirLeMois);
        var employe = saisie.EmployeId is { } id ? await contexte.Employes.SingleOrDefaultAsync(e => e.Id == id) : null;
        if (employe is null)
            return new(default, StatusCodes.Status404NotFound, EmployeIntrouvable);

        var existant = await contexte.SalairesPayes.SingleOrDefaultAsync(s => s.EmployeId == employe.Id && s.Mois == premier);
        if (saisie.Paye == true && existant is null)
            contexte.SalairesPayes.Add(new SalairePaye(employe.Id, premier, employe.Salaire, DateDeTunis.Aujourdhui(temps)));
        else if (saisie.Paye != true && existant is not null)
            contexte.SalairesPayes.Remove(existant);
        await contexte.SaveChangesAsync();
        return new(await SalairesAsync(saisie.Mois), StatusCodes.Status200OK, null);
    }

    private async Task<EmployeDecrit> DecrireAsync(Employe employe)
    {
        var annee = DateDeTunis.Aujourdhui(temps).Year;
        var conges = await contexte.Conges.Where(c => c.EmployeId == employe.Id && c.Du.Year <= annee && c.Au.Year >= annee).ToListAsync();
        return Decrire(employe, conges.Sum(c => c.JoursDans(annee)));
    }

    private static EmployeDecrit Decrire(Employe e, int joursDeConge) => new(e.Id, e.Prenom, e.Nom, e.Email,
        e.Telephone is { } t ? TelephoneTunisien.Grouper(t) : null, e.Poste, e.Horaires, e.Salaire, e.PartiLe, joursDeConge);

    private static CongeDecrit Decrire(Conge c) => new(c.Id, c.Du, c.Au, c.Note, c.Au.DayNumber - c.Du.DayNumber + 1);

    private static DateOnly? LireLeMois(string? mois) =>
        mois is not null && DateOnly.TryParseExact($"{mois.Trim()}-01", "yyyy-MM-dd", out var premier) ? premier : null;

    private static DateOnly PremierDuMois(DateOnly jour) => new(jour.Year, jour.Month, 1);

    /// <summary>Les champs dans l'ordre du formulaire, sans les espaces autour : le premier refusé donne la réponse.</summary>
    private static (FicheDEmploye? Fiche, string? Refus) Lire(EmployeSaisi saisie)
    {
        var prenom = saisie.Prenom?.Trim() ?? "";
        var nom = saisie.Nom?.Trim() ?? "";
        if (prenom.Length == 0)
            return (null, PrenomObligatoire);
        if (nom.Length == 0)
            return (null, NomObligatoire);
        if (prenom.Length > Employe.LongueurMaximaleDuNom || nom.Length > Employe.LongueurMaximaleDuNom)
            return (null, NomTropLong);
        var email = string.IsNullOrWhiteSpace(saisie.Email) ? null : saisie.Email.Trim();
        if (email is not null && !AdresseEmail.EstValable(email))
            return (null, EmailPasValable);
        string? telephone = null;
        if (!string.IsNullOrWhiteSpace(saisie.Telephone) && (telephone = TelephoneTunisien.Normaliser(saisie.Telephone)) is null)
            return (null, TelephonePasValable);
        var poste = saisie.Poste?.Trim() ?? "";
        if (poste.Length == 0)
            return (null, PosteObligatoire);
        if (poste.Length > Employe.LongueurMaximaleDuNom)
            return (null, PosteTropLong);
        var horaires = string.IsNullOrWhiteSpace(saisie.Horaires) ? null : saisie.Horaires.Trim();
        if (horaires is { Length: > Employe.LongueurMaximaleDesHoraires })
            return (null, HorairesTropLongs);
        if (saisie.Salaire is not { } salaire || salaire < 0 || decimal.Round(salaire, 3) != salaire || salaire > 999_999_999m)
            return (null, SalairePasValable);
        return (new FicheDEmploye(prenom, nom, email, telephone, poste, horaires, salaire), null);
    }
}

public sealed record EmployeSaisi(string? Prenom, string? Nom, string? Email, string? Telephone, string? Poste, string? Horaires, decimal? Salaire);

/// <summary>Un employé : sa fiche, son téléphone groupé, la date de son départ, et ses jours de congé dans l'année.</summary>
public sealed record EmployeDecrit(Guid Id, string Prenom, string Nom, string? Email, string? Telephone, string Poste, string? Horaires, decimal Salaire,
    DateOnly? PartiLe, int JoursDeConge);

public sealed record CongeSaisi(DateOnly? Du, DateOnly? Au, string? Note);

public sealed record CongeDecrit(Guid Id, DateOnly Du, DateOnly Au, string? Note, int Jours);

public sealed record SalaireSaisi(Guid? EmployeId, string? Mois, bool? Paye);

/// <summary>RHA-03 : un salaire du mois : l'employé, son poste, le montant (figé s'il est payé), payé ou non.</summary>
public sealed record SalaireDuMois(Guid EmployeId, string Nom, string Poste, decimal Montant, bool Paye, DateOnly? PayeLe);

/// <summary>RHA-03 : les salaires d'un mois (« 2027-10 »), le total payé et le total à payer.</summary>
public sealed record SalairesDuMois(string Mois, List<SalaireDuMois> Salaires, decimal TotalPaye, decimal TotalAPayer);

/// <summary>Ce qu'une action RH a donné : le résultat et le statut HTTP, ou un refus.</summary>
public sealed record IssueRh<T>(T? Resultat, int Statut, string? Message);
