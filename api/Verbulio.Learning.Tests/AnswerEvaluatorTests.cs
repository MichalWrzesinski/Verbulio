using Verbulio.Learning;

namespace Verbulio.Learning.Tests;

public sealed class AnswerEvaluatorTests
{
    [Theory]
    [InlineData("  HOUSE ", "house", false, AnswerGrade.Correct)]
    [InlineData("lodz", "łódź", true, AnswerGrade.CorrectWithSpelling)]
    [InlineData("łodz", "łódź", true, AnswerGrade.CorrectWithSpelling)]
    [InlineData("ło\u0301dz\u0301", "łódź", true, AnswerGrade.Correct)]
    [InlineData("huose", "house", false, AnswerGrade.CorrectWithSpelling)]
    [InlineData("houuse", "house", false, AnswerGrade.CorrectWithSpelling)]
    [InlineData("huse", "house", false, AnswerGrade.CorrectWithSpelling)]
    [InlineData("huuse", "house", false, AnswerGrade.CorrectWithSpelling)]
    [InlineData("cot", "cat", false, AnswerGrade.Incorrect)]
    [InlineData("catx", "cat", false, AnswerGrade.CorrectWithSpelling)]
    [InlineData("hose", "house", false, AnswerGrade.Incorrect)]
    [InlineData("lodx", "łódź", true, AnswerGrade.Incorrect)]
    [InlineData("house!", "house", false, AnswerGrade.CorrectWithSpelling)]
    public void AssessesSpelling(string input, string expected, bool isPolish, AnswerGrade grade)
    {
        var evaluator = new AnswerEvaluator(["cat", "cot", "house", "hose", "łódź"]);
        var result = evaluator.Assess(input, [expected], new HashSet<string>(), isPolish, true);
        Assert.Equal(grade, result.Grade);
        if (grade == AnswerGrade.CorrectWithSpelling)
        {
            Assert.Equal(expected, result.SuggestedSpelling);
        }
    }

    [Fact]
    public void AcceptsSynonymsAndAllowsOneBaseFormRetry()
    {
        var evaluator = new AnswerEvaluator(["house", "home", "houses"]);
        var inflections = new HashSet<string> { "houses" };

        Assert.Equal(AnswerGrade.Correct, evaluator.Assess("home", ["house", "home"], inflections, false, true).Grade);
        Assert.Equal(AnswerGrade.RetryWithBaseForm, evaluator.Assess("houses", ["house"], inflections, false, true).Grade);
        Assert.Equal(AnswerGrade.Incorrect, evaluator.Assess("houses", ["house"], inflections, false, false).Grade);
        Assert.Throws<ArgumentException>(() => evaluator.Assess(" ", ["house"], inflections, false, true));
    }
}
