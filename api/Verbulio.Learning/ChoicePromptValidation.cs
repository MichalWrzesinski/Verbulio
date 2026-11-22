namespace Verbulio.Learning;

internal static class ChoicePromptValidation
{
    internal static void Validate(TranslationPrompt prompt, bool isPolish)
    {
        var translations = prompt.Translations.Select(AnswerEvaluator.Normalize).ToHashSet(StringComparer.Ordinal);
        var distractors = prompt.Distractors.Select(AnswerEvaluator.Normalize).ToArray();
        if (translations.Count == 0 || translations.Contains(string.Empty) || distractors.Length != 3
            || distractors.Any(distractor => distractor.Length == 0 || translations.Contains(distractor)
                || (isPolish && translations.Any(translation =>
                    AnswerEvaluator.MatchesWithoutPolishAccents(distractor, translation))))
            || distractors.Distinct(StringComparer.Ordinal).Count() != 3)
        {
            throw new ArgumentException("Each prompt needs an accepted translation and three distinct incorrect choices.");
        }
    }
}
