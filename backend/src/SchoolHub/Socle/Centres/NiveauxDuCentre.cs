using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Socle.Centres;

/// <summary>CEN-03 : la liste des niveaux du centre courant. Le filtre automatique ne montre que ceux du centre.</summary>
public sealed class NiveauxDuCentre(SchoolHubDbContext contexte)
{
    /// <summary>Les niveaux d'office dans l'ordre scolaire, puis les niveaux ajoutés, par nom.</summary>
    public Task<List<Niveau>> ListerAsync() =>
        contexte.Niveaux
            .OrderByDescending(n => n.DOffice)
            .ThenBy(n => n.Rang)
            .ThenBy(n => n.Nom)
            .ToListAsync();

    /// <summary>Ajoute les 29 niveaux d'office au centre courant, sans enregistrer : ils partent avec la création du centre.</summary>
    public void RecopierLesNiveauxDOffice() =>
        contexte.Niveaux.AddRange(NiveauxDOffice.Liste.Select((nom, i) => new Niveau(nom, dOffice: true, rang: i + 1)));
}
