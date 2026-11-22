namespace Verbulio.Learning;

public enum TranslationDirection
{
    EnglishToPolish,
    PolishToEnglish,
}

public enum AnswerMode
{
    Choices,
    Typed,
}

public sealed record TranslationPrompt(
    string Text,
    string? Hint,
    IReadOnlyList<string> Translations,
    IReadOnlySet<string> InflectedForms,
    IReadOnlyList<string> Distractors);

public sealed record SessionWord(
    string Id,
    TranslationPrompt EnglishToPolish,
    TranslationPrompt PolishToEnglish,
    ReviewSchedule? Review = null,
    TranslationDirection NextDirection = TranslationDirection.EnglishToPolish,
    AnswerMode NextMode = AnswerMode.Choices);

public sealed record LearningQuestion(
    int Number,
    string Text,
    string? Hint,
    TranslationDirection Direction,
    AnswerMode Mode,
    IReadOnlyList<string> Choices,
    bool BaseFormRetry);

public sealed record LearningAnswer(int QuestionNumber, string? Text = null, int? Choice = null);

public enum LearningConflict
{
    ReusedSubmission,
    StaleQuestion,
}

public sealed class LearningConflictException(LearningConflict reason) : InvalidOperationException(reason.ToString())
{
    public LearningConflict Reason { get; } = reason;
}

public sealed record WordProgressChange(
    string WordId,
    TranslationDirection NextDirection,
    AnswerMode NextMode,
    ReviewSchedule? ChangedReview);

public sealed record LearningFeedback(
    AnswerAssessment Assessment,
    IReadOnlyList<string> CorrectTranslations,
    WordProgressChange? Progress,
    bool Completed);

public sealed record LearningSummary(int FirstTryCorrect, int WordCount, int WordsResolved, bool Completed);

public sealed class LearningSession
{
    private readonly AnswerEvaluator _evaluator;
    private readonly List<SessionWord> _queue;
    private readonly int _wordCount;
    private readonly HashSet<string> _attemptedWords = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, (LearningAnswer Answer, LearningFeedback Feedback)> _responses = [];
    private readonly Random _random;
    private int _firstTryCorrect;
    private int _questionNumber;
    private bool _ended;

    public LearningSession(IEnumerable<SessionWord> selectedWords, AnswerEvaluator evaluator, int seed)
    {
        _queue = selectedWords.ToList();
        _wordCount = _queue.Count;
        if (_wordCount is < 1 or > 10 || _queue.Select(word => word.Id).Distinct(StringComparer.Ordinal).Count() != _wordCount)
        {
            throw new ArgumentException("Select between one and ten distinct words.", nameof(selectedWords));
        }

        foreach (var word in _queue)
        {
            ChoicePromptValidation.Validate(word.EnglishToPolish, isPolish: true);
            ChoicePromptValidation.Validate(word.PolishToEnglish, isPolish: false);
        }

        _evaluator = evaluator;
        _random = new Random(seed);
        CurrentQuestion = CreateQuestion();
    }

    public LearningQuestion? CurrentQuestion { get; private set; }

    public LearningSummary Summary => new(
        _firstTryCorrect, _wordCount, _wordCount - _queue.Count, _queue.Count == 0);

    public LearningFeedback Submit(Guid submissionId, LearningAnswer submission, DateTimeOffset answeredAt)
    {
        if (submissionId == Guid.Empty)
        {
            throw new ArgumentException("A submission identifier is required.", nameof(submissionId));
        }

        if (_responses.TryGetValue(submissionId, out var saved))
        {
            if (saved.Answer != submission)
            {
                throw new LearningConflictException(LearningConflict.ReusedSubmission);
            }

            return saved.Feedback;
        }

        var question = CurrentQuestion;
        if (_ended || question is null || submission.QuestionNumber != question.Number)
        {
            throw new LearningConflictException(LearningConflict.StaleQuestion);
        }

        var word = _queue[0];
        var prompt = PromptFor(word);
        var assessment = Evaluate(submission, question, prompt);
        LearningFeedback feedback;
        if (assessment.Grade == AnswerGrade.RetryWithBaseForm)
        {
            CurrentQuestion = question with { Number = ++_questionNumber, BaseFormRetry = true };
            feedback = new LearningFeedback(assessment, [], null, false);
        }
        else
        {
            var firstAnswer = !_attemptedWords.Contains(word.Id);
            var review = firstAnswer
                ? ReviewSchedule.AfterAnswer(word.Review?.Box, assessment.IsCorrect, answeredAt)
                : null;
            _attemptedWords.Add(word.Id);
            if (firstAnswer && assessment.IsCorrect)
            {
                _firstTryCorrect++;
            }

            var nextDirection = word.NextDirection == TranslationDirection.EnglishToPolish
                ? TranslationDirection.PolishToEnglish
                : TranslationDirection.EnglishToPolish;
            var nextMode = assessment.IsCorrect ? AnswerMode.Typed : AnswerMode.Choices;
            _queue.RemoveAt(0);
            if (!assessment.IsCorrect)
            {
                _queue.Insert(Math.Min(2, _queue.Count), word with
                {
                    NextMode = nextMode,
                    NextDirection = nextDirection,
                });
            }

            CurrentQuestion = _queue.Count == 0 ? null : CreateQuestion();
            var progress = new WordProgressChange(word.Id, nextDirection, nextMode, review);
            feedback = new LearningFeedback(
                assessment, Array.AsReadOnly(prompt.Translations.ToArray()), progress, CurrentQuestion is null);
        }

        _responses.Add(submissionId, (submission, feedback));
        return feedback;
    }

    public LearningSummary End()
    {
        _ended = true;
        CurrentQuestion = null;
        return Summary;
    }

    private AnswerAssessment Evaluate(LearningAnswer answer, LearningQuestion question, TranslationPrompt prompt)
    {
        if (question.Mode == AnswerMode.Choices)
        {
            if (answer.Text is not null || answer.Choice is not int choice
                || choice < 0 || choice >= question.Choices.Count)
            {
                throw new ArgumentException("Select one of the displayed choices.", nameof(answer));
            }

            var selected = AnswerEvaluator.Normalize(question.Choices[choice]);
            return new(prompt.Translations.Any(translation => AnswerEvaluator.Normalize(translation) == selected)
                ? AnswerGrade.Correct
                : AnswerGrade.Incorrect);
        }

        if (answer.Choice is not null || answer.Text is null)
        {
            throw new ArgumentException("Supply a typed answer.", nameof(answer));
        }

        return _evaluator.Assess(answer.Text, prompt.Translations, prompt.InflectedForms,
            question.Direction == TranslationDirection.EnglishToPolish, !question.BaseFormRetry);
    }

    private static TranslationPrompt PromptFor(SessionWord word) => word.NextDirection
        == TranslationDirection.EnglishToPolish ? word.EnglishToPolish : word.PolishToEnglish;

    private LearningQuestion CreateQuestion()
    {
        var word = _queue[0];
        var prompt = PromptFor(word);
        string[] choices = word.NextMode == AnswerMode.Choices
            ? [prompt.Translations[0], .. prompt.Distractors]
            : [];
        _random.Shuffle(choices.AsSpan());
        return new LearningQuestion(++_questionNumber, prompt.Text, prompt.Hint, word.NextDirection, word.NextMode,
            Array.AsReadOnly(choices), false);
    }

}
