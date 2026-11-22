using Verbulio.Learning;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal sealed record LearningCatalog(
    int Version,
    IReadOnlyDictionary<string, SessionWord> Words,
    IReadOnlyDictionary<string, VocabularyLevel> Levels,
    AnswerEvaluator Evaluator)
{
    internal static readonly LearningCatalog Fixture = new(FixtureDictionary.Version, FixtureDictionary.ById,
        new Dictionary<string, VocabularyLevel>(StringComparer.Ordinal), FixtureDictionary.Evaluator);

    internal static readonly LearningCatalog Pilot = CreatePilot();

    internal static LearningCatalog Resolve(int version) =>
        version == Fixture.Version ? Fixture
        : version == Pilot.Version ? Pilot
        : throw new InvalidOperationException("Unsupported learning session dictionary version.");

    private static LearningCatalog CreatePilot()
    {
        const int version = 2;
        var bank = LocalPlacementBank.Resolve("placement-pilot-v1");
        return new LearningCatalog(
            version,
            bank.Items.ToDictionary(item => item.Id,
                item => new SessionWord(item.Id, item.EnglishToPolish, item.PolishToEnglish), StringComparer.Ordinal),
            bank.Items.ToDictionary(item => item.Id, item => item.Difficulty, StringComparer.Ordinal),
            new AnswerEvaluator(bank.KnownForms.Concat(bank.Items.SelectMany(item =>
                Forms(item.EnglishToPolish).Concat(Forms(item.PolishToEnglish))))));
    }

    private static IEnumerable<string> Forms(TranslationPrompt prompt) =>
        prompt.Translations.Concat(prompt.Distractors).Concat(prompt.InflectedForms);
}
