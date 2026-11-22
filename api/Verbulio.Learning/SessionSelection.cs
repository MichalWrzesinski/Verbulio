namespace Verbulio.Learning;

public static class SessionSelection
{
    public static IReadOnlyList<SessionWord> Select(
        IEnumerable<SessionWord> candidates,
        DateTimeOffset now,
        int seed)
    {
        var eligible = candidates.ToArray();
        var due = eligible.Where(word => word.Review?.DueAt <= now)
            .OrderBy(word => word.Review!.Box)
            .ThenBy(word => word.Review!.DueAt)
            .ThenBy(word => word.Id, StringComparer.Ordinal);
        var unseen = eligible.Where(word => word.Review is null)
            .OrderBy(word => word.Id, StringComparer.Ordinal)
            .ToArray();

        new Random(seed).Shuffle(unseen.AsSpan());
        return due.Concat(unseen).DistinctBy(word => word.Id, StringComparer.Ordinal)
            .Take(10).ToArray();
    }
}
