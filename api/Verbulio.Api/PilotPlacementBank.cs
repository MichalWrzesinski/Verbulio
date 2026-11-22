using Verbulio.Learning;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal static class PilotPlacementBank
{
    internal const string Version = "placement-pilot-v1";
    private const string Header = "id\tlevel\tgroup\tenglish\tpolish\tenglish_inflections\tpolish_inflections\tpolish_alternatives\tenglish_alternatives\tenglish_hint\tpolish_hint";

    internal static PlacementBank Load()
    {
        using var source = typeof(PilotPlacementBank).Assembly.GetManifestResourceStream(
            "Verbulio.Api.Dictionary.placement-pilot-v1.tsv")
            ?? throw new InvalidOperationException("The pilot placement bank is missing.");
        using var reader = new StreamReader(source);
        if (!string.Equals(reader.ReadLine(), Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The pilot placement bank has an invalid header.");
        }

        var rows = new List<Entry>();
        while (reader.ReadLine() is { } line)
        {
            rows.Add(Parse(line, rows.Count + 2));
        }

        var items = new List<PlacementItem>(rows.Count);
        foreach (var group in rows.GroupBy(row => row.Group, StringComparer.Ordinal))
        {
            var members = group.ToArray();
            if (members.Length != 4 || members.Any(member => member.Level != members[0].Level))
            {
                throw new InvalidDataException($"Pilot group {group.Key} must contain four entries of the same level.");
            }

            foreach (var member in members)
            {
                var others = members.Where(other => other.Id != member.Id).ToArray();
                var english = WithHint(member.English, member.EnglishHint);
                var polish = WithHint(member.Polish, member.PolishHint);
                var englishAnswers = member.English.StartsWith("to ", StringComparison.Ordinal)
                    ? new[] { member.English, member.English[3..] }
                    : [member.English];
                items.Add(new PlacementItem(member.Id, member.Level,
                    new TranslationPrompt(english, null, [member.Polish, .. member.PolishAlternatives],
                        member.PolishInflections, others.Select(other => other.Polish).ToArray()),
                    new TranslationPrompt(polish, null, [.. englishAnswers, .. member.EnglishAlternatives],
                        member.EnglishInflections, others.Select(other => other.English).ToArray())));
            }
        }

        return new PlacementBank(Version, items.AsReadOnly(), []);
    }

    private static Entry Parse(string line, int row)
    {
        var fields = line.Split('\t');
        if (fields.Length != 11 || fields.Take(5).Any(value => string.IsNullOrWhiteSpace(value) || value == "-")
            || !Enum.TryParse<VocabularyLevel>(fields[1], out var level) || !Enum.IsDefined(level)
            || !string.Equals(level.ToString(), fields[1], StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Invalid pilot placement entry at line {row}.");
        }

        return new Entry(fields[0], level, fields[2], fields[3], fields[4],
            Forms(fields[5]).ToHashSet(StringComparer.Ordinal),
            Forms(fields[6]).ToHashSet(StringComparer.Ordinal), Forms(fields[7]), Forms(fields[8]),
            fields[9], fields[10]);
    }

    private static string[] Forms(string field) => field == "-" ? [] : field.Split('|');

    private static string WithHint(string word, string hint) => hint == "-" ? word : $"{word} ({hint})";

    private sealed record Entry(string Id, VocabularyLevel Level, string Group, string English, string Polish,
        IReadOnlySet<string> EnglishInflections, IReadOnlySet<string> PolishInflections,
        string[] PolishAlternatives, string[] EnglishAlternatives, string EnglishHint, string PolishHint);
}
