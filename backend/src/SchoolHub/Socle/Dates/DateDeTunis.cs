namespace SchoolHub.Socle.Dates;

/// <summary>
/// La date du jour en Tunisie : un Mois « a commencé » si sa première séance a lieu aujourd'hui ou avant (GRP-04).
/// Elle vient de <see cref="TimeProvider"/>, que les tests règlent.
/// </summary>
public static class DateDeTunis
{
    private static readonly TimeZoneInfo Tunis = TrouverLeFuseau(nom =>
        TimeZoneInfo.TryFindSystemTimeZoneById(nom, out var fuseau) ? fuseau : null);

    public static DateOnly Aujourdhui(TimeProvider temps) => Aujourdhui(temps, Tunis);

    public static DateOnly Aujourdhui(TimeProvider temps, TimeZoneInfo fuseau) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(temps.GetUtcNow(), fuseau).DateTime);

    /// <summary>
    /// Le fuseau de Tunis : son nom IANA, puis son nom Windows. Un serveur qui ne connaît ni l'un ni l'autre
    /// (image Linux sans base des fuseaux, ancien Windows) prend UTC+1 : la Tunisie n'a plus d'heure d'été depuis 2009.
    /// </summary>
    public static TimeZoneInfo TrouverLeFuseau(Func<string, TimeZoneInfo?> chercher) =>
        chercher("Africa/Tunis")
        ?? chercher("W. Central Africa Standard Time")
        ?? TimeZoneInfo.CreateCustomTimeZone("Tunis", TimeSpan.FromHours(1), "Tunis", "Tunis");
}
