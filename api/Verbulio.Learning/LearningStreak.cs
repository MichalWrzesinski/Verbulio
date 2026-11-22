namespace Verbulio.Learning;

public sealed record LearningStreak(int CurrentDays, int BestDays)
{
    public static DateOnly LocalDay(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    public static LearningStreak Calculate(IEnumerable<DateOnly> creditedDays, DateOnly today)
    {
        DateOnly? previous = null;
        var consecutive = 0;
        var current = 0;
        var best = 0;

        foreach (var day in creditedDays.Distinct().Order())
        {
            consecutive = previous is { } prior && day.DayNumber == prior.DayNumber + 1
                ? consecutive + 1
                : 1;
            best = Math.Max(best, consecutive);
            if (day.DayNumber == today.DayNumber || day.DayNumber == today.DayNumber - 1)
            {
                current = consecutive;
            }
            previous = day;
        }

        return new(current, best);
    }
}
