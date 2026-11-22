using Verbulio.Api;
using Verbulio.Learning.Placement;

namespace Verbulio.Learning.Tests;

public sealed class PlacementBankDataTests
{
    [Fact]
    public void TheCurrentBankCoversAllLevelsWithRealPromptsAndBilingualChoices()
    {
        var bank = LocalPlacementBank.Current;

        Assert.Equal(PilotPlacementBank.Version, bank.Version);
        Assert.Equal(108, bank.Items.Count);
        Assert.Equal(6, bank.Items.Select(item => item.Difficulty).Distinct().Count());
        Assert.All(bank.Items, item =>
        {
            Assert.NotEmpty(item.EnglishToPolish.Text);
            Assert.NotEmpty(item.PolishToEnglish.Text);
            Assert.Equal(3, item.EnglishToPolish.Distractors.Count);
            Assert.Equal(3, item.PolishToEnglish.Distractors.Count);
        });
        var test = new PlacementTest(bank, seed: 1);
        Assert.Equal(4, test.View.Question?.Choices.Count);
    }

    [Fact]
    public void ThePilotDistinguishesSenseHintsFromAcceptedForms()
    {
        var bank = LocalPlacementBank.Current;
        var verb = Assert.Single(bank.Items, entry => entry.EnglishToPolish.Text == "to run (on foot)");
        Assert.Contains("biec", verb.EnglishToPolish.Translations);
        Assert.Contains("run", verb.PolishToEnglish.Translations);
        Assert.Contains("ran", verb.PolishToEnglish.InflectedForms);
        Assert.DoesNotContain(verb.EnglishToPolish.Text, verb.PolishToEnglish.Translations);
    }

    [Theory]
    [InlineData("to borrow", "pożyczać", true)]
    [InlineData("to borrow", "pożyczyć", true)]
    [InlineData("łagodzić (zmniejszać dotkliwość)", "alleviate", false)]
    public void TypedAnswersAcceptCommonVariantsForTheSelectedSense(string source, string answer, bool isPolish)
    {
        var prompts = LocalPlacementBank.Current.Items
            .Select(item => isPolish ? item.EnglishToPolish : item.PolishToEnglish);
        var prompt = Assert.Single(prompts, entry => entry.Text == source);
        var evaluator = new AnswerEvaluator([]);

        var result = evaluator.Assess(answer, prompt.Translations, prompt.InflectedForms, isPolish, false);

        Assert.Equal(AnswerGrade.Correct, result.Grade);
    }

    [Fact]
    public void ThePreviousBankStillReplaysByItsPinnedVersion()
    {
        var bank = LocalPlacementBank.Resolve("synthetic-placement-v1");

        var test = new PlacementTest(bank, seed: 55);
        Assert.StartsWith("en-A2-", test.View.Question?.Text, StringComparison.Ordinal);

        var first = Assert.IsType<LearningQuestion>(test.View.Question);
        var next = test.Submit(Guid.NewGuid(), new PlacementSubmission(first.Number, Skip: true));
        Assert.StartsWith("pl-A1-", next.Question?.Text, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => LocalPlacementBank.Resolve("unknown"));
    }
}
