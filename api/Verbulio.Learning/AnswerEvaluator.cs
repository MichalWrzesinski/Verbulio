using System.Text;

namespace Verbulio.Learning;

public enum AnswerGrade
{
    Correct,
    CorrectWithSpelling,
    RetryWithBaseForm,
    Incorrect,
}

public sealed record AnswerAssessment(AnswerGrade Grade, string? SuggestedSpelling = null)
{
    public bool IsCorrect => Grade is AnswerGrade.Correct or AnswerGrade.CorrectWithSpelling;
}

public sealed class AnswerEvaluator(IEnumerable<string> dictionaryForms)
{
    private readonly HashSet<string> _dictionaryForms = dictionaryForms
        .Select(Normalize)
        .ToHashSet(StringComparer.Ordinal);

    public AnswerAssessment Assess(
        string answer,
        IReadOnlyList<string> translations,
        IReadOnlySet<string> inflectedForms,
        bool isPolish,
        bool allowBaseFormRetry)
    {
        var input = Normalize(answer);
        if (input.Length == 0)
        {
            throw new ArgumentException("An answer is required.", nameof(answer));
        }

        var candidates = translations.Select(translation => (Display: translation, Normalized: Normalize(translation)))
            .ToArray();

        if (candidates.Any(candidate => candidate.Normalized == input))
        {
            return new(AnswerGrade.Correct);
        }

        if (isPolish)
        {
            foreach (var candidate in candidates)
            {
                if (MatchesWithoutPolishAccents(input, candidate.Normalized))
                {
                    return new(AnswerGrade.CorrectWithSpelling, candidate.Display);
                }
            }
        }

        if (inflectedForms.Any(form => Normalize(form) == input))
        {
            return new(allowBaseFormRetry ? AnswerGrade.RetryWithBaseForm : AnswerGrade.Incorrect);
        }

        if (!_dictionaryForms.Contains(input) && CountLetters(input) >= 4)
        {
            foreach (var candidate in candidates)
            {
                if (IsOneEditApart(input, candidate.Normalized))
                {
                    return new(AnswerGrade.CorrectWithSpelling, candidate.Display);
                }
            }
        }

        return new(AnswerGrade.Incorrect);
    }

    public static string Normalize(string form) =>
        string.Join(' ', form.Normalize(NormalizationForm.FormC).ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static int CountLetters(string form) => form.EnumerateRunes().Count(Rune.IsLetter);

    internal static bool MatchesWithoutPolishAccents(string input, string expected)
    {
        if (input.Length != expected.Length)
        {
            return false;
        }

        for (var index = 0; index < input.Length; index++)
        {
            var unaccented = expected[index] switch
            {
                'ą' => 'a',
                'ć' => 'c',
                'ę' => 'e',
                'ł' => 'l',
                'ń' => 'n',
                'ó' => 'o',
                'ś' => 's',
                'ź' or 'ż' => 'z',
                _ => expected[index],
            };

            if (input[index] != expected[index] && input[index] != unaccented)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOneEditApart(string input, string expected)
    {
        var left = input.EnumerateRunes().ToArray().AsSpan();
        var right = expected.EnumerateRunes().ToArray().AsSpan();
        if (left.Length > right.Length)
        {
            return IsOneEditApart(expected, input);
        }

        if (right.Length - left.Length > 1)
        {
            return false;
        }

        var firstDifference = left.CommonPrefixLength(right);
        if (left.Length < right.Length)
        {
            return left[firstDifference..].SequenceEqual(right[(firstDifference + 1)..]);
        }

        if (firstDifference == left.Length)
        {
            return false;
        }

        var next = firstDifference + 1;
        return left[next..].SequenceEqual(right[next..])
            || (next < left.Length
                && left[firstDifference] == right[next]
                && left[next] == right[firstDifference]
                && left[(next + 1)..].SequenceEqual(right[(next + 1)..]));
    }
}
