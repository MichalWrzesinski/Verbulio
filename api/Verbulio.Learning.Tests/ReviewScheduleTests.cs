using Verbulio.Learning;

namespace Verbulio.Learning.Tests;

public sealed class ReviewScheduleTests
{
    [Theory]
    [InlineData(null, true, ReviewBox.OneDay, 1)]
    [InlineData(null, false, ReviewBox.OneDay, 1)]
    [InlineData(ReviewBox.OneDay, true, ReviewBox.ThreeDays, 3)]
    [InlineData(ReviewBox.OneDay, false, ReviewBox.OneDay, 1)]
    [InlineData(ReviewBox.ThreeDays, true, ReviewBox.SevenDays, 7)]
    [InlineData(ReviewBox.ThreeDays, false, ReviewBox.OneDay, 1)]
    [InlineData(ReviewBox.SevenDays, true, ReviewBox.FourteenDays, 14)]
    [InlineData(ReviewBox.SevenDays, false, ReviewBox.ThreeDays, 3)]
    [InlineData(ReviewBox.FourteenDays, true, ReviewBox.ThirtyDays, 30)]
    [InlineData(ReviewBox.FourteenDays, false, ReviewBox.SevenDays, 7)]
    [InlineData(ReviewBox.ThirtyDays, true, ReviewBox.Mastered, 90)]
    [InlineData(ReviewBox.ThirtyDays, false, ReviewBox.FourteenDays, 14)]
    [InlineData(ReviewBox.Mastered, true, ReviewBox.Mastered, 90)]
    [InlineData(ReviewBox.Mastered, false, ReviewBox.ThirtyDays, 30)]
    public void CalculatesDueDateFromTheAnswerInstant(
        ReviewBox? previous, bool correct, ReviewBox expected, int days)
    {
        var answeredAt = new DateTimeOffset(2026, 11, 22, 23, 30, 0, TimeSpan.FromHours(1));

        var schedule = ReviewSchedule.AfterAnswer(previous, correct, answeredAt);

        Assert.Equal(expected, schedule.Box);
        Assert.Equal(answeredAt.ToUniversalTime().AddDays(days), schedule.DueAt);
        Assert.Equal(TimeSpan.Zero, schedule.DueAt.Offset);
    }
}
