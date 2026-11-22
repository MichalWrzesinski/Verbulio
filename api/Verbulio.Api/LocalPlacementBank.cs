using Verbulio.Learning;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal static class LocalPlacementBank
{
    internal static readonly PlacementBank Current = PilotPlacementBank.Load();

    private static readonly PlacementBank Synthetic = new(
        "synthetic-placement-v1",
        Enum.GetValues<VocabularyLevel>().SelectMany(level =>
            Enumerable.Range(1, PlacementTest.QuestionCount).Select(index =>
            {
                var id = FormattableString.Invariant($"{level}-{index}");
                var english = $"en-{id}";
                var polish = $"pl-{id}";
                return new PlacementItem(id, level,
                    new TranslationPrompt(english, null, [polish], new HashSet<string>(), ["most", "stół", "ogród"]),
                    new TranslationPrompt(polish, null, [english], new HashSet<string>(), ["bridge", "table", "garden"]));
            })).ToArray(),
        []);

    internal static PlacementBank Resolve(string version) => version switch
    {
        PilotPlacementBank.Version => Current,
        "synthetic-placement-v1" => Synthetic,
        _ => throw new InvalidOperationException("Unsupported placement bank version."),
    };
}
