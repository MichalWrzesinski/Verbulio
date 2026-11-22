namespace Verbulio.Learning.Placement;

public enum VocabularyLevel
{
    A1,
    A2,
    B1,
    B2,
    C1,
    C2,
}

internal sealed record PlacementObservation(VocabularyLevel Difficulty, AnswerMode Mode, bool Correct);

internal static class PlacementEstimator
{
    internal static VocabularyLevel Estimate(IReadOnlyList<PlacementObservation> observations)
    {
        var best = VocabularyLevel.A1;
        var bestScore = double.NegativeInfinity;
        foreach (var level in Enum.GetValues<VocabularyLevel>())
        {
            var score = observations.Sum(observation =>
            {
                var floor = observation.Mode == AnswerMode.Choices ? .25 : .02;
                var exponent = 1.4 * (level - observation.Difficulty) + .5;
                var probability = floor + (.98 - floor) / (1 + Math.Exp(-exponent));
                return Math.Log(observation.Correct ? probability : 1 - probability);
            });

            if (score > bestScore)
            {
                best = level;
                bestScore = score;
            }
        }

        return best;
    }
}
