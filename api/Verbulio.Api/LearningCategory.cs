namespace Verbulio.Api;

internal sealed record LearningCategory(string Id, string Title, IReadOnlySet<string> WordIds)
{
    internal static IReadOnlyList<LearningCategory> All { get; } =
    [
        Create("podroze", "Podróże", "a2-01", "a2-03", "a2-05", "a2-08", "a2-12", "a2-20", "b1-02"),
        Create("praca", "Praca", "a2-09", "b1-15", "b2-01", "b2-14"),
        Create("jedzenie", "Jedzenie", "a1-03", "a1-06", "a1-10", "a1-14", "a1-15"),
        Create("dom", "Dom", "a1-01", "a1-05", "a1-09", "a1-13", "a2-07", "a2-18"),
        Create("zdrowie", "Zdrowie i samopoczucie", "a1-16", "a1-17", "a2-11"),
        Create("emocje", "Emocje", "b2-10"),
        Create("sport", "Sport", "a1-18", "a1-20"),
    ];

    private static LearningCategory Create(string id, string title, params string[] wordIds) =>
        new(id, title, wordIds.ToHashSet(StringComparer.Ordinal));
}
