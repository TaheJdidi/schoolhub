using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Professeurs;

/// <summary>
/// GRP-01 et GRP-03 : les professeurs du centre courant. Le filtre automatique ne montre que ceux du centre :
/// un professeur d'un autre centre est « introuvable ». Un téléphone déjà pris prévient, sans bloquer.
/// </summary>
public sealed class ProfesseursDuCentre(SchoolHubDbContext contexte, TimeProvider temps)
{
    private const string PrenomObligatoire = "Le prénom est obligatoire";
    private const string PrenomTropLong = "Le prénom fait au plus 100 caractères";
    private const string NomObligatoire = "Le nom est obligatoire";
    private const string NomTropLong = "Le nom fait au plus 100 caractères";
    private const string TelephoneObligatoire = "Le téléphone est obligatoire";
    private const string TelephonePasValable = "Le téléphone doit avoir 8 chiffres";
    private const string EmailPasValable = "Cet email n'est pas valable";
    private const string Introuvable = "Professeur introuvable";
    private const string FermerSesGroupes = "Fermez d'abord ses groupes";
    private const string ADesGroupes = "Ce professeur a eu des groupes : marquez-le parti";

    /// <summary>Tous les professeurs, partis compris, par prénom puis par nom.</summary>
    public Task<List<Professeur>> ListerAsync() =>
        contexte.Professeurs.OrderBy(p => p.Prenom).ThenBy(p => p.Nom).ThenBy(p => p.Id).ToListAsync();

    public async Task<IssueSurUnProfesseur> AjouterAsync(FicheDuProfesseur saisie)
    {
        var (fiche, refus) = VerifierLaFiche(saisie);
        if (refus is not null)
            return refus;

        var avertissement = await AvertissementAsync(fiche.Telephone, sauf: null);
        var professeur = new Professeur(fiche.Prenom, fiche.Nom, fiche.Telephone, fiche.Email);
        contexte.Professeurs.Add(professeur);
        await contexte.SaveChangesAsync();
        return IssueSurUnProfesseur.Reussie(professeur, StatusCodes.Status201Created, avertissement);
    }

    public async Task<IssueSurUnProfesseur> ModifierAsync(Guid id, FicheDuProfesseur saisie)
    {
        var (professeur, introuvable) = await TrouverAsync(id);
        if (introuvable is not null)
            return introuvable;

        var (fiche, refus) = VerifierLaFiche(saisie);
        if (refus is not null)
            return refus;

        // Le professeur lui-même ne compte pas : garder son téléphone ne le met pas en double.
        var avertissement = await AvertissementAsync(fiche.Telephone, sauf: id);
        professeur!.Modifier(fiche.Prenom, fiche.Nom, fiche.Telephone, fiche.Email);
        await contexte.SaveChangesAsync();
        return IssueSurUnProfesseur.Reussie(professeur, avertissement: avertissement);
    }

    /// <summary>Le nombre de groupes ouverts de chaque professeur qui en a.</summary>
    public Task<Dictionary<Guid, int>> GroupesOuvertsAsync() =>
        GroupesOuverts()
            .GroupBy(g => g.ProfesseurId)
            .Select(g => new { ProfesseurId = g.Key, Nombre = g.Count() })
            .ToDictionaryAsync(g => g.ProfesseurId, g => g.Nombre);

    public Task<int> GroupesOuvertsAsync(Guid professeurId) => GroupesOuverts().CountAsync(g => g.ProfesseurId == professeurId);

    /// <summary>
    /// Les groupes qui ne sont pas terminés, la règle de <see cref="CalendrierDuGroupe.EstTermine"/> écrite pour la base :
    /// une Étude, ou une Révision ou une Formation qui a encore une séance aujourd'hui ou après. Les groupes fermés
    /// (GRP-08) en sortiront aussi.
    /// </summary>
    private IQueryable<Groupe> GroupesOuverts()
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        return contexte.Groupes.Where(g =>
            g.Type == TypeDeGroupe.Etude
            || !contexte.Seances.Any(s => s.GroupeId == g.Id)
            || contexte.Seances.Any(s => s.GroupeId == g.Id && s.Date >= aujourdhui));
    }

    /// <summary>
    /// Supprime une erreur de saisie. Un professeur qui a eu un groupe, même fermé, peut seulement partir : sinon,
    /// la clé étrangère de ses groupes répondrait 500.
    /// </summary>
    public async Task<IssueSurUnProfesseur> SupprimerAsync(Guid id)
    {
        var (professeur, introuvable) = await TrouverAsync(id);
        if (introuvable is not null)
            return introuvable;
        if (await contexte.Groupes.AnyAsync(g => g.ProfesseurId == id))
            return IssueSurUnProfesseur.Refusee(StatusCodes.Status409Conflict, ADesGroupes);

        contexte.Professeurs.Remove(professeur!);
        await contexte.SaveChangesAsync();
        return IssueSurUnProfesseur.Reussie(professeur!, StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Marque le professeur parti ; s'il l'est déjà, rien ne change. Un professeur qui a un groupe ouvert ne part pas :
    /// la secrétaire ferme d'abord ses groupes (GRP-08). Ses groupes terminés ne le retiennent pas.
    /// </summary>
    public async Task<IssueSurUnProfesseur> PartirAsync(Guid id)
    {
        // Un professeur d'un autre centre n'a aucun groupe ici : ChangerAsync le dira introuvable.
        if (await GroupesOuverts().AnyAsync(g => g.ProfesseurId == id))
            return IssueSurUnProfesseur.Refusee(StatusCodes.Status409Conflict, FermerSesGroupes);
        return await ChangerAsync(id, p => p.Partir());
    }

    /// <summary>« Revenir au centre » : le professeur reprend sa place dans la liste ; s'il y est déjà, rien ne change.</summary>
    public Task<IssueSurUnProfesseur> RevenirAsync(Guid id) => ChangerAsync(id, p => p.Revenir());

    private async Task<IssueSurUnProfesseur> ChangerAsync(Guid id, Action<Professeur> changement)
    {
        var (professeur, introuvable) = await TrouverAsync(id);
        if (introuvable is not null)
            return introuvable;

        changement(professeur!);
        await contexte.SaveChangesAsync();
        return IssueSurUnProfesseur.Reussie(professeur!);
    }

    /// <summary>Le professeur de ce centre, ou le refus 404 : inconnu, ou d'un autre centre.</summary>
    private async Task<(Professeur? Professeur, IssueSurUnProfesseur? Introuvable)> TrouverAsync(Guid id)
    {
        var professeur = await contexte.Professeurs.SingleOrDefaultAsync(p => p.Id == id);
        return professeur is null
            ? (null, IssueSurUnProfesseur.Refusee(StatusCodes.Status404NotFound, Introuvable))
            : (professeur, null);
    }

    /// <summary>
    /// Les champs dans l'ordre du formulaire, sans les espaces autour : le premier refusé donne la réponse.
    /// Le téléphone revient en 8 chiffres, et un email vide devient <c>null</c>.
    /// </summary>
    private static ((string Prenom, string Nom, string Telephone, string? Email) Fiche, IssueSurUnProfesseur? Refus) VerifierLaFiche(
        FicheDuProfesseur saisie)
    {
        var prenom = saisie.Prenom?.Trim() ?? "";
        var nom = saisie.Nom?.Trim() ?? "";
        var email = string.IsNullOrWhiteSpace(saisie.Email) ? null : saisie.Email.Trim();

        string? refus = null;
        string? telephone = null;
        if (prenom.Length == 0)
            refus = PrenomObligatoire;
        else if (prenom.Length > Professeur.LongueurMaximaleDuNom)
            refus = PrenomTropLong;
        else if (nom.Length == 0)
            refus = NomObligatoire;
        else if (nom.Length > Professeur.LongueurMaximaleDuNom)
            refus = NomTropLong;
        else if (string.IsNullOrWhiteSpace(saisie.Telephone))
            refus = TelephoneObligatoire;
        else if ((telephone = TelephoneTunisien.Normaliser(saisie.Telephone)) is null)
            refus = TelephonePasValable;
        else if (email is not null && !AdresseEmail.EstValable(email))
            refus = EmailPasValable;

        return refus is null
            ? ((prenom, nom, telephone!, email), null)
            : (default, IssueSurUnProfesseur.Refusee(StatusCodes.Status400BadRequest, refus));
    }

    /// <summary>
    /// « Ce téléphone est aussi celui de Karim Ben Salah et Leila Khelifi » : les autres professeurs du centre,
    /// partis compris, qui ont ce téléphone ; ou <c>null</c> s'il n'y en a pas.
    /// </summary>
    private async Task<string?> AvertissementAsync(string telephone, Guid? sauf)
    {
        var noms = await contexte.Professeurs
            .Where(p => p.Telephone == telephone && p.Id != sauf)
            .OrderBy(p => p.Prenom).ThenBy(p => p.Nom).ThenBy(p => p.Id)
            .Select(p => p.Prenom + " " + p.Nom)
            .ToListAsync();

        return noms.Count switch
        {
            0 => null,
            1 => $"Ce téléphone est aussi celui de {noms[0]}",
            _ => $"Ce téléphone est aussi celui de {string.Join(", ", noms[..^1])} et {noms[^1]}",
        };
    }
}

/// <summary>
/// Ce qu'une action sur un professeur a donné : le professeur et le statut HTTP, avec l'avertissement du téléphone ;
/// ou un refus (statut et message).
/// </summary>
public sealed record IssueSurUnProfesseur(Professeur? Professeur, int Statut, string? Message, string? Avertissement = null)
{
    public static IssueSurUnProfesseur Reussie(Professeur professeur, int statut = StatusCodes.Status200OK, string? avertissement = null) =>
        new(professeur, statut, null, avertissement);

    public static IssueSurUnProfesseur Refusee(int statut, string message) => new(null, statut, message);
}
