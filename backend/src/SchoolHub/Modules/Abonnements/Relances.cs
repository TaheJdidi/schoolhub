using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Emails;

namespace SchoolHub.Modules.Abonnements;

/// <summary>
/// EDI-06 : les e-mails automatiques au propriétaire, 7 jours avant la fin de son abonnement, puis le jour de la fin.
/// Chaque relance part une seule fois par échéance : la ligne de l'envoi est réservée avant l'e-mail (un index unique
/// la protège), et retirée si l'e-mail ne part pas, pour qu'il reparte au passage suivant. On peut donc lancer
/// <see cref="EnvoyerAsync"/> autant de fois qu'on veut dans la journée. Une relance manquée (serveur arrêté ce jour-là)
/// part au passage suivant : celle de J-7 jusqu'à la veille de la fin, celle du jour de la fin pendant la période de grâce.
/// </summary>
public sealed class Relances(SchoolHubDbContext contexte, IEnvoiEmail emails, TimeProvider temps, ILogger<Relances> journaux)
{
    /// <summary>Envoie les relances dues aujourd'hui, et rend leur nombre.</summary>
    public async Task<int> EnvoyerAsync(CancellationToken annulation = default)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        var abonnements = await contexte.Set<Abonnement>().AsNoTracking()
            .Where(a => a.PayeJusquAu != null && a.Forcage == null)
            .ToListAsync(annulation);

        var envoyees = 0;
        foreach (var abonnement in abonnements)
        {
            var fin = abonnement.PayeJusquAu!.Value;
            var avantLaFin = fin.DayNumber - aujourdhui.DayNumber;
            ObjetDeLEnvoi? objet = avantLaFin switch
            {
                > 0 and <= SituationDeLAbonnement.JoursAvantLaRelance => ObjetDeLEnvoi.RelanceAvantLaFin,
                <= 0 and >= -SituationDeLAbonnement.JoursDeGrace => ObjetDeLEnvoi.RelanceDeLaFin,
                _ => null,
            };
            if (objet is not null && await EnvoyerAsync(abonnement, objet.Value, fin, annulation))
                envoyees++;
        }

        return envoyees;
    }

    private async Task<bool> EnvoyerAsync(Abonnement abonnement, ObjetDeLEnvoi objet, DateOnly fin, CancellationToken annulation)
    {
        if (await contexte.Set<EnvoiAuProprietaire>().AnyAsync(
                e => e.CentreId == abonnement.CentreId && e.Objet == objet && e.Echeance == fin, annulation))
            return false;

        var centre = await contexte.Centres.AsNoTracking().SingleAsync(c => c.Id == abonnement.CentreId, annulation);
        // Le premier propriétaire, celui créé avec le centre (les identifiants suivent l'ordre de création).
        var proprietaire = await contexte.Users.AsNoTracking()
            .Where(c => c.CentreId == abonnement.CentreId && c.Role == RoleDuCompte.Proprietaire)
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(annulation);
        if (proprietaire?.Email is null)
            return false;

        // La réservation d'abord : un second passage simultané bute sur l'index unique et n'envoie rien.
        var envoi = new EnvoiAuProprietaire(abonnement.CentreId, objet, CanalDEnvoi.Email, temps.GetUtcNow(), proprietaire.Email, echeance: fin);
        contexte.Set<EnvoiAuProprietaire>().Add(envoi);
        try
        {
            await contexte.SaveChangesAsync(annulation);
        }
        catch (DbUpdateException)
        {
            contexte.ChangeTracker.Clear();
            return false;
        }

        var (sujet, texte) = MessagesAuProprietaire.EmailDeRelance(
            proprietaire.Prenom, centre.Nom, fin, abonnement.PrixMensuel, leJourMeme: objet == ObjetDeLEnvoi.RelanceDeLaFin);
        try
        {
            await emails.EnvoyerAsync(proprietaire.Email, sujet, texte, annulation);
            return true;
        }
        catch (Exception exception)
        {
            journaux.LogError(exception, "La relance du centre {Centre} n'est pas partie : elle repartira au prochain passage", abonnement.CentreId);
            contexte.Set<EnvoiAuProprietaire>().Remove(envoi);
            await contexte.SaveChangesAsync(CancellationToken.None);
            return false;
        }
    }
}

/// <summary>
/// La tâche de fond qui lance les relances : au démarrage, puis toutes les heures. Elles ne partent qu'une fois par
/// échéance : c'est, en pratique, un passage par jour, qui rattrape un redémarrage du serveur. Elle lit l'horloge de
/// l'application (<see cref="TimeProvider"/>).
/// </summary>
public sealed class TacheDesRelances(
    IServiceScopeFactory scopes,
    TimeProvider temps,
    IOptions<OptionsDesAbonnements> options,
    ILogger<TacheDesRelances> journaux) : BackgroundService
{
    public static readonly TimeSpan Intervalle = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken arret)
    {
        // Coupée par la configuration (les tests lancent les relances eux-mêmes).
        if (!options.Value.RelancesAutomatiques)
            return;

        using var minuteur = new PeriodicTimer(Intervalle, temps);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var nombre = await scope.ServiceProvider.GetRequiredService<Relances>().EnvoyerAsync(arret);
                if (nombre > 0)
                    journaux.LogInformation("{Nombre} relance(s) d'abonnement envoyée(s)", nombre);
            }
            catch (Exception exception) when (!arret.IsCancellationRequested)
            {
                journaux.LogError(exception, "Le passage des relances d'abonnement a échoué : il reprendra dans une heure");
            }
        }
        while (await minuteur.WaitForNextTickAsync(arret));
    }
}
