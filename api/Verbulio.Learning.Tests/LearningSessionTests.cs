using Verbulio.Learning;

namespace Verbulio.Learning.Tests;

public sealed class LearningSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly SessionWord House = new(
        "house",
        new("house", null, ["dom"], new HashSet<string> { "domy" }, ["ogród", "most", "stół"]),
        new("dom", null, ["house"], new HashSet<string> { "houses" }, ["garden", "bridge", "table"]));

    private static readonly SessionWord Window = new(
        "window",
        new("window", null, ["okno"], new HashSet<string>(), ["drzwi", "ściana", "dach"]),
        new("okno", null, ["window"], new HashSet<string>(), ["door", "wall", "roof"]));

    private static readonly SessionWord Boat = new(
        "boat",
        new("boat", null, ["łódź"], new HashSet<string>(), ["statek", "samolot", "rower"]),
        new("łódź", null, ["boat"], new HashSet<string>(), ["ship", "plane", "bicycle"]));

    [Fact]
    public void IncorrectWordReturnsAfterTwoOtherQuestionsAndUpdatesScheduleOnce()
    {
        var session = CreateSession(House, Window, Boat);
        var question = Assert.IsType<LearningQuestion>(session.CurrentQuestion);
        var answer = Choice(question, "ogród");
        var id = Guid.NewGuid();

        var initial = session.Submit(id, answer, Now);
        Assert.Equal(AnswerGrade.Incorrect, initial.Assessment.Grade);
        Assert.Equal(ReviewBox.OneDay, initial.Progress?.ChangedReview?.Box);
        Assert.Equal(initial, session.Submit(id, answer, Now.AddMinutes(1)));
        Assert.Throws<LearningConflictException>(() => session.Submit(id, Choice(question, "dom"), Now));
        Assert.Throws<LearningConflictException>(() => session.Submit(Guid.NewGuid(), answer, Now));

        Assert.Equal("window", session.CurrentQuestion?.Text);
        SubmitChoice(session, "okno");
        Assert.Equal("boat", session.CurrentQuestion?.Text);
        SubmitChoice(session, "łódź");
        Assert.Equal("dom", session.CurrentQuestion?.Text);
        Assert.Equal(TranslationDirection.PolishToEnglish, session.CurrentQuestion?.Direction);

        var final = SubmitChoice(session, "house");
        Assert.True(final.Completed);
        Assert.Null(final.Progress?.ChangedReview);
        Assert.Equal(AnswerMode.Typed, final.Progress?.NextMode);
        Assert.Equal(new LearningSummary(2, 3, 3, true), session.Summary);
    }

    [Fact]
    public void ShortQueueReturnsImmediatelyAndCanBeInterrupted()
    {
        var session = CreateSession(House);
        SubmitChoice(session, "most");
        Assert.Equal("dom", session.CurrentQuestion?.Text);

        Assert.Equal(new LearningSummary(0, 1, 0, false), session.End());
        Assert.Null(session.CurrentQuestion);
    }

    [Fact]
    public void ShortQueueUsesItsOneRemainingWordBeforeRetry()
    {
        var session = CreateSession(House, Window);
        SubmitChoice(session, "most");
        Assert.Equal("window", session.CurrentQuestion?.Text);
        SubmitChoice(session, "okno");
        Assert.Equal("dom", session.CurrentQuestion?.Text);
        Assert.True(SubmitChoice(session, "house").Completed);
        Assert.Equal(new LearningSummary(1, 2, 2, true), session.Summary);
    }

    [Fact]
    public void QuestionSequenceCanBeReproducedWithTheSameSeedAndSubmissions()
    {
        var original = CreateSession(House, Window, Boat);
        var replay = CreateSession(House, Window, Boat);
        var initial = Assert.IsType<LearningQuestion>(original.CurrentQuestion);
        var sameInitial = Assert.IsType<LearningQuestion>(replay.CurrentQuestion);
        Assert.Equal(initial.Choices, sameInitial.Choices);

        var answer = Choice(initial, "most");
        var submission = Guid.NewGuid();
        original.Submit(submission, answer, Now);
        replay.Submit(submission, answer, Now);

        Assert.Equal(original.CurrentQuestion?.Number, replay.CurrentQuestion?.Number);
        Assert.Equal(original.CurrentQuestion?.Text, replay.CurrentQuestion?.Text);
        Assert.Equal(original.CurrentQuestion?.Choices, replay.CurrentQuestion?.Choices);
        Assert.Equal(original.Summary, replay.Summary);
    }

    [Fact]
    public void InflectionRetryHasNoReviewUntilTheSecondAnswer()
    {
        var session = CreateSession(House with { NextMode = AnswerMode.Typed });
        var original = Assert.IsType<LearningQuestion>(session.CurrentQuestion);

        var retry = session.Submit(Guid.NewGuid(), new(original.Number, "domy"), Now);
        Assert.Equal(AnswerGrade.RetryWithBaseForm, retry.Assessment.Grade);
        Assert.Null(retry.Progress);
        var second = Assert.IsType<LearningQuestion>(session.CurrentQuestion);
        Assert.True(second.BaseFormRetry);

        var final = session.Submit(Guid.NewGuid(), new(second.Number, "dom"), Now);
        Assert.Equal(AnswerGrade.Correct, final.Assessment.Grade);
        Assert.Equal(ReviewBox.OneDay, final.Progress?.ChangedReview?.Box);
        Assert.Equal(new LearningSummary(1, 1, 1, true), session.Summary);
    }

    [Fact]
    public void DueReviewsComeFirstWithMasteredLastAndFutureReviewsExcluded()
    {
        var candidates = new[]
        {
            House with { Id = "future", Review = new(ReviewBox.OneDay, Now.AddHours(1)) },
            Window with { Id = "new" },
            Boat with { Id = "mastered", Review = new(ReviewBox.Mastered, Now.AddDays(-2)) },
            House with { Id = "later", Review = new(ReviewBox.ThreeDays, Now) },
            House with { Id = "earlier", Review = new(ReviewBox.ThreeDays, Now.AddDays(-1)) },
            House with { Id = "first", Review = new(ReviewBox.OneDay, Now) },
        };

        Assert.Equal(
            ["first", "earlier", "later", "mastered", "new"],
            SessionSelection.Select(candidates, Now, seed: 1).Select(word => word.Id));
    }

    [Fact]
    public void RejectsAChoiceThatCouldAlsoBeTheCorrectTranslation()
    {
        var invalid = Boat with { EnglishToPolish = Boat.EnglishToPolish with { Distractors = ["lodz", "dom", "stół"] } };
        Assert.Throws<ArgumentException>(() => CreateSession(invalid));
    }

    private static LearningSession CreateSession(params SessionWord[] words) =>
        new(words, new AnswerEvaluator(["dom", "house", "okno", "window", "łódź", "boat"]), seed: 42);

    private static LearningFeedback SubmitChoice(LearningSession session, string value) =>
        session.Submit(Guid.NewGuid(), Choice(Assert.IsType<LearningQuestion>(session.CurrentQuestion), value), Now);

    private static LearningAnswer Choice(LearningQuestion question, string value) =>
        new(question.Number, Choice: Array.IndexOf(question.Choices.ToArray(), value));
}
