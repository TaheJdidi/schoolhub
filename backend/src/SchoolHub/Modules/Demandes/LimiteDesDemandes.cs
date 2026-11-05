using System.Threading.RateLimiting;

namespace SchoolHub.Modules.Demandes;

/// <summary>
/// DEM-01 : au plus 5 demandes enregistrées par heure et par adresse. Seules les demandes acceptées comptent : un parent
/// qui oublie un champ et renvoie le formulaire n'use pas sa limite. Une seule limite pour toute l'application.
/// </summary>
public sealed class LimiteDesDemandes : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limite = PartitionedRateLimiter.Create<string, string>(adresse =>
        RateLimitPartition.GetFixedWindowLimiter(adresse,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));

    /// <summary>Prend une place pour cette adresse ; <c>false</c> si ses 5 demandes de l'heure sont déjà enregistrées.</summary>
    public bool Prendre(string adresse)
    {
        using var bail = _limite.AttemptAcquire(adresse);
        return bail.IsAcquired;
    }

    public void Dispose() => _limite.Dispose();
}
