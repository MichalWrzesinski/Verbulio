namespace Verbulio.Learning.Placement;

public sealed record PlacementItem(
    string Id,
    VocabularyLevel Difficulty,
    TranslationPrompt EnglishToPolish,
    TranslationPrompt PolishToEnglish);

public sealed record PlacementBank(
    string Version,
    IReadOnlyList<PlacementItem> Items,
    IReadOnlyList<string> KnownForms);

public sealed record PlacementSubmission(int QuestionNumber, bool Skip = false, string? Text = null, int? Choice = null);

public sealed record PlacementView(LearningQuestion? Question, int AnsweredCount, int TotalCount, VocabularyLevel? Result);

public sealed class PlacementTest
{
    public const int QuestionCount = 20;

    // Maximum visits to each level over any 20-question path starting at A2.
    private static readonly int[] MaxVisitsByLevel = [19, 10, 10, 9, 9, 16];

    private readonly Dictionary<VocabularyLevel, Queue<PlacementItem>> _items;
    private readonly List<PlacementObservation> _observations = [];
    private readonly Dictionary<Guid, PlacementSubmission> _submissions = [];
    private readonly AnswerEvaluator _evaluator;
    private readonly Random _random;
    private VocabularyLevel _difficulty = VocabularyLevel.A2;
    private PlacementItem? _currentItem;

    public PlacementTest(PlacementBank bank, int seed)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentException.ThrowIfNullOrWhiteSpace(bank.Version, nameof(bank));
        var items = bank.Items.ToArray();
        if (items.Any(item => string.IsNullOrWhiteSpace(item.Id) || !Enum.IsDefined(item.Difficulty))
            || items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != items.Length)
        {
            throw new ArgumentException("Placement item IDs must be unique and levels must be supported.", nameof(bank));
        }

        ValidateDistinctPrompts(items.Select(item => item.EnglishToPolish), nameof(bank));
        ValidateDistinctPrompts(items.Select(item => item.PolishToEnglish), nameof(bank));
        foreach (var item in items)
        {
            ChoicePromptValidation.Validate(item.EnglishToPolish, isPolish: true);
            ChoicePromptValidation.Validate(item.PolishToEnglish, isPolish: false);
        }

        _random = new Random(seed);
        var bankForms = items.SelectMany(item => FormsIn(item.EnglishToPolish).Concat(FormsIn(item.PolishToEnglish)));
        _evaluator = new AnswerEvaluator(bank.KnownForms.Concat(bankForms));
        _items = new Dictionary<VocabularyLevel, Queue<PlacementItem>>();
        foreach (var level in Enum.GetValues<VocabularyLevel>())
        {
            var candidates = items.Where(item => item.Difficulty == level)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
            var required = MaxVisitsByLevel[(int)level];
            if (candidates.Length < required)
            {
                throw new ArgumentException($"Placement level {level} requires at least {required} items.", nameof(bank));
            }

            _random.Shuffle(candidates.AsSpan());
            _items.Add(level, new Queue<PlacementItem>(candidates));
        }

        View = new PlacementView(CreateQuestion(), 0, QuestionCount, null);
    }

    public PlacementView View { get; private set; }

    public PlacementView Submit(Guid submissionId, PlacementSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (submissionId == Guid.Empty)
        {
            throw new ArgumentException("A submission identifier is required.", nameof(submissionId));
        }

        if (_submissions.TryGetValue(submissionId, out var saved))
        {
            if (saved != submission)
            {
                throw new LearningConflictException(LearningConflict.ReusedSubmission);
            }

            return View;
        }

        var question = View.Question;
        if (question is null || submission.QuestionNumber != question.Number)
        {
            throw new LearningConflictException(LearningConflict.StaleQuestion);
        }

        var correct = IsCorrect(submission, question, PromptFor(_currentItem!, question.Direction));
        _observations.Add(new PlacementObservation(_difficulty, question.Mode, correct));
        _submissions.Add(submissionId, submission);

        if (_observations.Count == QuestionCount)
        {
            _currentItem = null;
            View = new PlacementView(null, QuestionCount, QuestionCount, PlacementEstimator.Estimate(_observations));
            return View;
        }

        _difficulty = (VocabularyLevel)Math.Clamp(
            (int)_difficulty + (correct ? 1 : -1), (int)VocabularyLevel.A1, (int)VocabularyLevel.C2);
        View = new PlacementView(CreateQuestion(), _observations.Count, QuestionCount, null);
        return View;
    }

    private LearningQuestion CreateQuestion()
    {
        var index = _observations.Count;
        var item = _items[_difficulty].Dequeue();
        _currentItem = item;
        var direction = index % 4 is 0 or 3 ? TranslationDirection.EnglishToPolish : TranslationDirection.PolishToEnglish;
        var mode = index % 2 == 0 ? AnswerMode.Choices : AnswerMode.Typed;
        var prompt = PromptFor(item, direction);
        string[] choices = mode == AnswerMode.Choices ? [prompt.Translations[0], .. prompt.Distractors] : [];
        _random.Shuffle(choices.AsSpan());
        return new LearningQuestion(index + 1, prompt.Text, prompt.Hint, direction, mode, Array.AsReadOnly(choices), false);
    }

    private bool IsCorrect(PlacementSubmission submission, LearningQuestion question, TranslationPrompt prompt)
    {
        if (submission.Skip)
        {
            if (submission.Text is not null || submission.Choice is not null)
            {
                throw new ArgumentException("A skipped question cannot include an answer.", nameof(submission));
            }

            return false;
        }

        if (question.Mode == AnswerMode.Choices)
        {
            if (submission.Text is not null || submission.Choice is not int index
                || index < 0 || index >= question.Choices.Count)
            {
                throw new ArgumentException("Select one of the displayed choices.", nameof(submission));
            }

            var choice = AnswerEvaluator.Normalize(question.Choices[index]);
            return prompt.Translations.Any(translation => AnswerEvaluator.Normalize(translation) == choice);
        }

        if (submission.Text is null || submission.Choice is not null)
        {
            throw new ArgumentException("Supply a typed answer.", nameof(submission));
        }

        return _evaluator.Assess(submission.Text, prompt.Translations, prompt.InflectedForms,
            question.Direction == TranslationDirection.EnglishToPolish, allowBaseFormRetry: false).IsCorrect;
    }

    private static IEnumerable<string> FormsIn(TranslationPrompt prompt) =>
        prompt.Translations.Concat(prompt.Distractors).Concat(prompt.InflectedForms).Append(prompt.Text);

    private static void ValidateDistinctPrompts(IEnumerable<TranslationPrompt> prompts, string parameter)
    {
        var visiblePrompts = prompts.Select(prompt => (
            Text: AnswerEvaluator.Normalize(prompt.Text),
            Hint: AnswerEvaluator.Normalize(prompt.Hint ?? string.Empty))).ToArray();
        if (visiblePrompts.Any(prompt => prompt.Text.Length == 0)
            || visiblePrompts.Distinct().Count() != visiblePrompts.Length)
        {
            throw new ArgumentException("Placement prompts must be nonempty and distinguishable in each direction.", parameter);
        }
    }

    private static TranslationPrompt PromptFor(PlacementItem item, TranslationDirection direction) =>
        direction == TranslationDirection.EnglishToPolish ? item.EnglishToPolish : item.PolishToEnglish;
}
