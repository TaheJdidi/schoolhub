namespace SchoolHub.Socle.Dates;

/// <summary>
/// La date du jour en Tunisie : un Mois « a commencé » si sa première séance a lieu aujourd'hui ou avant (GRP-04).
/// Elle vient de <see cref="TimeProvider"/>, que les tests règlent.
/// </summary>
public static class DateDeTunis
{
    private static readonly TimeZoneInfo Tunis = TimeZoneInfo.FindSystemTimeZoneById("Africa/Tunis");

    public static DateOnly Aujourdhui(TimeProvider temps) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(temps.GetUtcNow(), Tunis).DateTime);
}
