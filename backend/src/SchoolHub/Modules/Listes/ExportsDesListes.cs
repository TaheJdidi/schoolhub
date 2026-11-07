using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Achats;
using SchoolHub.Modules.Bibliotheque;
using SchoolHub.Modules.Caisse;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Rh;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Modules.Listes;

/// <summary>
/// LST-02 : chaque liste en fichier Excel, avec le filtre en cours : les élèves, les professeurs, un groupe et ses élèves,
/// les reçus, les entrées de la bibliothèque, les achats, et les employés (propriétaire seulement).
/// </summary>
public sealed class ExportsDesListes(
    SchoolHubDbContext contexte,
    ElevesDuCentre eleves,
    RecusDuCentre recus,
    BibliothequeDuCentre bibliotheque,
    AchatsDuCentre achats,
    RessourcesHumaines rh,
    TimeProvider temps)
{
    /// <summary>Un fichier prêt : son nom (« eleves-2027-11-27.xlsx ») et son contenu.</summary>
    public sealed record Fichier(string Nom, byte[] Contenu);

    private string Nom(string liste) => $"{liste}-{DateDeTunis.Aujourdhui(temps):yyyy-MM-dd}.xlsx";

    /// <summary>
    /// Les élèves : ceux de la recherche (comme la liste), d'un niveau, ou seulement ceux sélectionnés (LST-05), par nom
    /// puis prénom.
    /// </summary>
    public async Task<Fichier> ElevesAsync(string? recherche, string? niveau, IReadOnlyCollection<Guid>? ids)
    {
        var trouves = (await eleves.ListerAsync(recherche)).Select(e => e.Id).ToHashSet();
        var niveaux = await contexte.Niveaux.ToDictionaryAsync(n => n.Id, n => n.Nom);
        var liste = (await contexte.Eleves.ToListAsync())
            .Where(e => trouves.Contains(e.Id) && (string.IsNullOrWhiteSpace(niveau) || niveaux[e.NiveauId] == niveau)
                && (ids is null || ids.Count == 0 || ids.Contains(e.Id)))
            .OrderBy(e => e.Nom).ThenBy(e => e.Prenom);
        return new(Nom("eleves"), Classeur.Creer("Élèves",
            [new("Prénom"), new("Nom"), new("Niveau"), new("Téléphone du parent"), new("Email du parent"), new("Élève majeur"),
                new("Téléphone de l'élève"), new("Email de l'élève"), new("Établissement")],
            liste.Select(e => new object?[] { e.Prenom, e.Nom, niveaux[e.NiveauId], Telephone(e.TelephoneParent), e.EmailParent, e.Majeur,
                Telephone(e.TelephoneEleve), e.EmailEleve, e.Etablissement })));
    }

    /// <summary>Les professeurs, au centre ou partis.</summary>
    public async Task<Fichier> ProfesseursAsync(bool partis)
    {
        var liste = await contexte.Professeurs.Where(p => p.Parti == partis).OrderBy(p => p.Nom).ThenBy(p => p.Prenom).ToListAsync();
        return new(Nom("professeurs"), Classeur.Creer("Professeurs", [new("Prénom"), new("Nom"), new("Téléphone"), new("Email")],
            liste.Select(p => new object?[] { p.Prenom, p.Nom, Telephone(p.Telephone), p.Email })));
    }

    /// <summary>Un groupe et ses élèves encore inscrits aujourd'hui ; <c>null</c> s'il n'est pas dans ce centre.</summary>
    public async Task<Fichier?> GroupeAsync(Guid groupeId)
    {
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == groupeId);
        if (groupe is null)
            return null;
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var inscrits = (await contexte.Inscriptions.Where(i => i.GroupeId == groupeId && (i.ArreteLe == null || i.ArreteLe >= aujourdhui))
                .Join(contexte.Eleves, i => i.EleveId, e => e.Id, (i, e) => new { Inscription = i, Eleve = e })
                .ToListAsync())
            .OrderBy(x => x.Eleve.Nom).ThenBy(x => x.Eleve.Prenom);
        return new(Nom("groupe"), Classeur.Creer(groupe.Nom,
            [new("Prénom"), new("Nom"), new("Téléphone du parent"), new("Téléphone de l'élève"), new("Depuis le", TypeDeColonne.Date), new("Prix spécial", TypeDeColonne.Montant)],
            inscrits.Select(x => new object?[] { x.Eleve.Prenom, x.Eleve.Nom, Telephone(x.Eleve.TelephoneParent), Telephone(x.Eleve.TelephoneEleve),
                x.Inscription.DebutLe, x.Inscription.PrixSpecial })));
    }

    /// <summary>Les reçus de la recherche (comme la page Caisse).</summary>
    public async Task<Fichier> RecusAsync(DateOnly? jour, int? numero, string? eleve)
    {
        var liste = await recus.ListerAsync(jour, numero, eleve);
        return new(Nom("recus"), Classeur.Creer("Reçus",
            [new("Numéro", TypeDeColonne.Nombre), new("Date", TypeDeColonne.Date), new("Élève"), new("Montant", TypeDeColonne.Montant), new("Secrétaire"), new("Annulé")],
            liste.Select(r => new object?[] { r.Numero, r.Jour, r.Eleve, r.Total, r.Secretaire, r.Annule })));
    }

    /// <summary>Les entrées de la bibliothèque d'un jour.</summary>
    public async Task<Fichier> BibliothequeAsync(DateOnly? jour)
    {
        var journee = await bibliotheque.JourneeAsync(jour);
        return new(Nom("bibliotheque"), Classeur.Creer("Bibliothèque",
            [new("Date", TypeDeColonne.Date), new("Prénom"), new("Nom"), new("Élève du centre"), new("Arrivée"), new("Départ"), new("Reçu", TypeDeColonne.Nombre), new("Reçu annulé")],
            journee.Liste.Select(e => new object?[] { journee.Jour, e.Prenom, e.Nom, !e.Exterieur, e.Arrivee, e.Depart, e.Numero, e.Annule })));
    }

    /// <summary>Les achats du filtre (mois ou année, catégorie).</summary>
    public async Task<Fichier> AchatsAsync(Compte lecteur, string? mois, int? annee, string? categorie)
    {
        var liste = await achats.ListerAsync(lecteur, mois, annee, categorie);
        return new(Nom("achats"), Classeur.Creer("Achats",
            [new("Date", TypeDeColonne.Date), new("Libellé"), new("Catégorie"), new("Montant", TypeDeColonne.Montant), new("Fournisseur"),
                new("Sortie de caisse"), new("Saisi par")],
            liste.Achats.Select(a => new object?[] { a.Jour, a.Libelle, a.Categorie == "Materiel" ? "Matériel" : "Service", a.Montant, a.Fournisseur,
                a.PayeDepuisLaCaisse, a.Auteur })));
    }

    /// <summary>Les employés (propriétaire seulement).</summary>
    public async Task<Fichier> EmployesAsync()
    {
        var liste = await rh.ListerAsync(null);
        return new(Nom("employes"), Classeur.Creer("Employés",
            [new("Prénom"), new("Nom"), new("Poste"), new("Horaires"), new("Salaire", TypeDeColonne.Montant), new("Téléphone"), new("Email"),
                new("Jours de congé cette année", TypeDeColonne.Nombre), new("Parti le", TypeDeColonne.Date)],
            liste.Select(e => new object?[] { e.Prenom, e.Nom, e.Poste, e.Horaires, e.Salaire, e.Telephone, e.Email, e.JoursDeConge, e.PartiLe })));
    }

    private static string? Telephone(string? chiffres) => chiffres is { Length: 8 } ? TelephoneTunisien.Grouper(chiffres) : chiffres;
}
