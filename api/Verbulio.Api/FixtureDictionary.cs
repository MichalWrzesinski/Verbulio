using Verbulio.Learning;

namespace Verbulio.Api;

internal static class FixtureDictionary
{
    internal const int Version = 1;

    private static readonly SessionWord[] Words =
    [
        new("house",
            new("house", null, ["dom"], new HashSet<string> { "domy" }, ["ogród", "most", "stół"]),
            new("dom", null, ["house"], new HashSet<string> { "houses" }, ["garden", "bridge", "table"])),
        new("window",
            new("window", null, ["okno"], new HashSet<string> { "okna" }, ["drzwi", "ściana", "dach"]),
            new("okno", null, ["window"], new HashSet<string> { "windows" }, ["door", "wall", "roof"])),
        new("boat",
            new("boat", null, ["łódź", "łódka"], new HashSet<string> { "łodzie" }, ["statek", "samolot", "rower"]),
            new("łódź", null, ["boat"], new HashSet<string> { "boats" }, ["ship", "plane", "bicycle"])),
    ];

    internal static readonly IReadOnlyDictionary<string, SessionWord> ById =
        Words.ToDictionary(word => word.Id, StringComparer.Ordinal);

    internal static readonly AnswerEvaluator Evaluator = new(Words.SelectMany(word =>
        word.EnglishToPolish.Translations.Concat(word.PolishToEnglish.Translations)
            .Concat(word.EnglishToPolish.Distractors).Concat(word.PolishToEnglish.Distractors)
            .Concat(word.EnglishToPolish.InflectedForms).Concat(word.PolishToEnglish.InflectedForms)));
}
