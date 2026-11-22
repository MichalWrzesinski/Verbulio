namespace Verbulio.Learning;

public enum ReviewBox
{
    OneDay = 1,
    ThreeDays = 2,
    SevenDays = 3,
    FourteenDays = 4,
    ThirtyDays = 5,
    Mastered = 6,
}

public sealed record ReviewSchedule(ReviewBox Box, DateTimeOffset DueAt)
{
    public static ReviewSchedule AfterAnswer(ReviewBox? previous, bool correct, DateTimeOffset answeredAt)
    {
        var next = previous switch
        {
            null => ReviewBox.OneDay,
            ReviewBox.Mastered => correct ? ReviewBox.Mastered : ReviewBox.ThirtyDays,
            >= ReviewBox.OneDay and <= ReviewBox.ThirtyDays => correct
                ? (ReviewBox)((int)previous.Value + 1)
                : (ReviewBox)Math.Max((int)ReviewBox.OneDay, (int)previous.Value - 1),
            _ => throw new ArgumentOutOfRangeException(nameof(previous)),
        };

        var interval = next switch
        {
            ReviewBox.OneDay => 1,
            ReviewBox.ThreeDays => 3,
            ReviewBox.SevenDays => 7,
            ReviewBox.FourteenDays => 14,
            ReviewBox.ThirtyDays => 30,
            ReviewBox.Mastered => 90,
            _ => throw new InvalidOperationException("Unsupported review box."),
        };

        return new(next, answeredAt.ToUniversalTime().AddDays(interval));
    }
}
