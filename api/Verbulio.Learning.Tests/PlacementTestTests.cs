using System.Text.Json;
using Verbulio.Learning.Placement;

namespace Verbulio.Learning.Tests;

public sealed class PlacementTestTests
{
    private static readonly PlacementItem[] Bank = Enum.GetValues<VocabularyLevel>()
        .SelectMany(level => Enumerable.Range(1, PlacementTest.QuestionCount).Select(index =>
        {
            var id = $"{level}-{index}";
            var en = $"en-{id}";
            var pl = $"pl-{id}";
            return new PlacementItem(id, level,
                new TranslationPrompt(en, null, [pl], new HashSet<string>(), ["most", "stół", "ogród"]),
                new TranslationPrompt(pl, null, [en], new HashSet<string>(), ["bridge", "table", "garden"]));
        })).ToArray();

    [Theory]
    [InlineData("00000000000000000000", VocabularyLevel.A1)]
    [InlineData("11111111111111111111", VocabularyLevel.C2)]
    [InlineData("11111111111111111110", VocabularyLevel.C2)]
    [InlineData("01111111111111111111", VocabularyLevel.C2)]
    [InlineData("00000000000000000001", VocabularyLevel.A1)]
    [InlineData("10000000000000000000", VocabularyLevel.A1)]
    [InlineData("10101010101010101010", VocabularyLevel.A2)]
    [InlineData("01010101010101010101", VocabularyLevel.A1)]
    public void EstimatesFromTheWholeResponseProfile(string profile, VocabularyLevel expected)
    {
        var test = MakeTest();
        foreach (var response in profile)
        {
            var question = Assert.IsType<LearningQuestion>(test.View.Question);
            test.Submit(Guid.NewGuid(), Submission(question, response == '1'));
        }

        Assert.Equal(expected, test.View.Result);
        Assert.Null(test.View.Question);
        Assert.Equal(20, test.View.AnsweredCount);
        Assert.Throws<LearningConflictException>(() => test.Submit(Guid.NewGuid(), new(21, Skip: true)));
    }

    [Fact]
    public void DeliversDistinctQuestionsWithBothModesAndDirectionsAndNoInterimResult()
    {
        var test = MakeTest();
        var questions = new List<LearningQuestion>();

        for (var index = 0; index < PlacementTest.QuestionCount; index++)
        {
            Assert.Null(test.View.Result);
            Assert.Equal(index, test.View.AnsweredCount);
            Assert.Equal(20, test.View.TotalCount);
            var question = Assert.IsType<LearningQuestion>(test.View.Question);
            Assert.Equal(index + 1, question.Number);
            Assert.Equal(question.Mode == AnswerMode.Choices ? 4 : 0, question.Choices.Count);
            var json = JsonSerializer.Serialize(test.View);
            Assert.DoesNotContain("CorrectTranslations", json, StringComparison.Ordinal);
            Assert.DoesNotContain("Assessment", json, StringComparison.Ordinal);
            questions.Add(question);
            test.Submit(Guid.NewGuid(), new(question.Number, Skip: true));
        }

        Assert.Equal(20, questions.Select(ItemId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(4, questions.GroupBy(question => (question.Mode, question.Direction)).Count());
        Assert.All(questions.GroupBy(question => (question.Mode, question.Direction)), group => Assert.Equal(5, group.Count()));
        Assert.Equal(VocabularyLevel.A1, test.View.Result);
    }

    [Fact]
    public void SelectsHarderQuestionAfterSuccessAndEasierAfterFailure()
    {
        var test = MakeTest();
        var first = Assert.IsType<LearningQuestion>(test.View.Question);
        Assert.StartsWith("A2-", ItemId(first), StringComparison.Ordinal);
        test.Submit(Guid.NewGuid(), Submission(first, correct: true));
        var second = Assert.IsType<LearningQuestion>(test.View.Question);
        Assert.StartsWith("B1-", ItemId(second), StringComparison.Ordinal);
        test.Submit(Guid.NewGuid(), Submission(second, correct: false));
        var third = Assert.IsType<LearningQuestion>(test.View.Question);
        Assert.StartsWith("A2-", ItemId(third), StringComparison.Ordinal);
    }

    [Fact]
    public void AReplayRestoresTheSameQuestionAndDuplicateKeysDoNotAdvanceIt()
    {
        var first = MakeTest();
        var replay = MakeTest(Bank.Reverse());
        var question = Assert.IsType<LearningQuestion>(first.View.Question);
        Assert.Equal(question.Choices, replay.View.Question?.Choices);
        var key = Guid.NewGuid();
        var answer = Submission(question, correct: true);

        var advanced = first.Submit(key, answer);
        var restored = replay.Submit(key, answer);
        Assert.Equal(advanced.Question?.Text, restored.Question?.Text);
        Assert.Equal(advanced.Question?.Choices, restored.Question?.Choices);
        Assert.Equal(advanced, first.Submit(key, answer));
        Assert.Equal(LearningConflict.ReusedSubmission,
            Assert.Throws<LearningConflictException>(() => first.Submit(key, new(1, Skip: true))).Reason);
        Assert.Equal(LearningConflict.StaleQuestion,
            Assert.Throws<LearningConflictException>(() => first.Submit(Guid.NewGuid(), answer)).Reason);
        Assert.Throws<ArgumentException>(() => first.Submit(Guid.NewGuid(), new(2, Text: " ")));
        Assert.Same(advanced, first.View);
    }

    [Fact]
    public void ReplaysTypedDecisionsAndFinalEstimateWithThePinnedDictionary()
    {
        const int seed = 86;
        var probe = MakeTest();
        var firstAnswer = Submission(Assert.IsType<LearningQuestion>(probe.View.Question), correct: true);
        probe.Submit(Guid.NewGuid(), firstAnswer);
        var typedQuestion = Assert.IsType<LearningQuestion>(probe.View.Question);
        var collision = Submission(typedQuestion, correct: true).Text + "x";
        var collidingAnswer = new PlacementSubmission(typedQuestion.Number, Text: collision);

        var bank = new PlacementBank("synthetic-v2", Bank, [collision]);
        var original = new PlacementTest(bank, seed);
        var replay = new PlacementTest(bank with { Items = Bank.Reverse().ToArray() }, seed);
        var newer = new PlacementTest(bank with { Version = "synthetic-v3", KnownForms = [] }, seed);

        var firstKey = Guid.NewGuid();
        var typedKey = Guid.NewGuid();
        foreach (var test in new[] { original, replay, newer })
        {
            test.Submit(firstKey, firstAnswer);
            test.Submit(typedKey, collidingAnswer);
        }

        Assert.StartsWith("A2-", ItemId(Assert.IsType<LearningQuestion>(original.View.Question)), StringComparison.Ordinal);
        Assert.StartsWith("B2-", ItemId(Assert.IsType<LearningQuestion>(newer.View.Question)), StringComparison.Ordinal);

        while (original.View.Question is { } question)
        {
            var replayQuestion = Assert.IsType<LearningQuestion>(replay.View.Question);
            Assert.Equal(question.Number, replayQuestion.Number);
            Assert.Equal(question.Text, replayQuestion.Text);
            Assert.Equal(question.Choices, replayQuestion.Choices);
            var key = Guid.NewGuid();
            var value = Submission(question, correct: true);
            original.Submit(key, value);
            replay.Submit(key, value);
        }

        Assert.NotNull(original.View.Result);
        Assert.Equal(original.View, replay.View);
    }

    [Fact]
    public void SupportsTheLargestAttainableRunAtEitherLevelBoundary()
    {
        int[] minimum = [19, 10, 10, 9, 9, 16];
        var bank = Bank.GroupBy(item => item.Difficulty)
            .SelectMany(group => group.Take(minimum[(int)group.Key])).ToArray();

        foreach (var correct in new[] { false, true })
        {
            var test = MakeTest(bank);
            while (test.View.Question is { } question)
            {
                test.Submit(Guid.NewGuid(), Submission(question, correct));
            }

            Assert.Equal(20, test.View.AnsweredCount);
        }
    }

    [Fact]
    public void RejectsIncompleteOrAmbiguousBanks()
    {
        Assert.Throws<ArgumentException>(() => MakeTest(Bank.Skip(2)));
        var inconsistent = Bank.ToArray();
        inconsistent[0] = inconsistent[0] with { EnglishToPolish = inconsistent[1].EnglishToPolish };
        Assert.Throws<ArgumentException>(() => MakeTest(inconsistent));
    }

    private static PlacementTest MakeTest(IEnumerable<PlacementItem>? bank = null) =>
        new(new PlacementBank("synthetic-v1", bank?.ToArray() ?? Bank, []), seed: 86);

    private static string ItemId(LearningQuestion question) => question.Text[3..];

    private static PlacementSubmission Submission(LearningQuestion question, bool correct)
    {
        var prefix = question.Direction == TranslationDirection.EnglishToPolish ? "pl-" : "en-";
        var expected = prefix + ItemId(question);
        if (question.Mode == AnswerMode.Typed)
        {
            return new PlacementSubmission(question.Number, Text: correct ? expected : "different");
        }

        var choices = question.Choices.ToList();
        return new PlacementSubmission(question.Number, Choice: choices.FindIndex(choice => (choice == expected) == correct));
    }
}
