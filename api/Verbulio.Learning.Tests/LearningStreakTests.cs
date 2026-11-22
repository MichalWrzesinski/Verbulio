using Verbulio.Learning;

namespace Verbulio.Learning.Tests;

public sealed class LearningStreakTests
{
    [Fact]
    public void EmptyDaysStartAtZero()
    {
        Assert.Equal(new LearningStreak(0, 0), LearningStreak.Calculate([], new DateOnly(2026, 3, 5)));
    }

    [Theory]
    [InlineData(4, 2, 3)]
    [InlineData(5, 2, 3)]
    [InlineData(6, 0, 3)]
    public void KeepsRecordAndCountsTheLatestLocalRun(int day, int current, int best)
    {
        DateOnly[] days =
        [
            new(2026, 2, 25), new(2026, 2, 26), new(2026, 2, 27),
            new(2026, 3, 4), new(2026, 3, 3), new(2026, 3, 4),
        ];
        Assert.Equal(new LearningStreak(current, best), LearningStreak.Calculate(days, new DateOnly(2026, 3, day)));
    }

    [Fact]
    public void TravelPreservesCreditedDaysAndDoesNotCountAFutureDayAsToday()
    {
        DateOnly[] days = [new(2026, 3, 4), new(2026, 3, 5)];
        Assert.Equal(new LearningStreak(1, 2), LearningStreak.Calculate(days, new DateOnly(2026, 3, 4)));
    }

    [Fact]
    public void LocalDayUsesTheOffsetAfterDaylightSavingTransition()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        var before = new DateTimeOffset(2026, 3, 29, 21, 59, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 3, 29), LearningStreak.LocalDay(before, zone));
        Assert.Equal(new DateOnly(2026, 3, 30), LearningStreak.LocalDay(before.AddMinutes(1), zone));
    }
}
