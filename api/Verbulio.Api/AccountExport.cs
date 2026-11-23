using System.Runtime.Versioning;
using System.Text.Json;
using Npgsql;

namespace Verbulio.Api;

internal static class AccountExport
{
    private static readonly JsonSerializerOptions ExportFormat = new() { WriteIndented = true };

    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 3 || !Guid.TryParseExact(args[1], "D", out var owner) || owner == Guid.Empty
            || !Path.IsPathFullyQualified(args[2]))
        {
            Console.Error.WriteLine("Usage: --export-account <account-uuid> <absolute-new-file>");
            return 2;
        }

        if (OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Run the export on Linux or macOS.");
            return 2;
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Learning");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Configure the Learning database connection in the environment.");
            return 2;
        }

        try
        {
            await using var database = NpgsqlDataSource.Create(connectionString);
            await using var command = database.CreateCommand(ExportQuery);
            command.Parameters.AddWithValue(owner);
            if (await command.ExecuteScalarAsync() is not string json)
            {
                Console.Error.WriteLine("Account not found.");
                return 3;
            }

            using var document = JsonDocument.Parse(json);
            await WritePrivateFileAsync(args[2], document.RootElement);
            Console.WriteLine("Account export written.");
            return 0;
        }
        catch (Exception error) when (error is NpgsqlException or IOException or UnauthorizedAccessException
            or ArgumentException or JsonException or TimeoutException)
        {
            Console.Error.WriteLine("Account export failed. Check the database and that the destination file is new and writable.");
            return 1;
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static async Task WritePrivateFileAsync(string destination, JsonElement document)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(destination))!;
        var staging = Path.Combine(directory, $".verbulio-export-{Guid.NewGuid():N}.tmp");
        var streamOptions = new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.CreateNew,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        };

        // Create the staging file before entering cleanup so that a failed open never deletes an existing file.
        var stream = new FileStream(staging, streamOptions);
        try
        {
            await using (stream)
            {
                await JsonSerializer.SerializeAsync(stream, document, ExportFormat);
            }
            File.Move(staging, destination, overwrite: false);
        }
        finally
        {
            File.Delete(staging);
        }
    }

    private const string ExportQuery = """
        SELECT jsonb_build_object(
            'version', 1,
            'account', jsonb_build_object(
                'email', a.email,
                'startingLevel', (ARRAY['A1','A2','B1','B2','C1','C2'])[a.starting_level + 1],
                'createdAt', a.created_at,
                'emailConfirmedAt', a.email_confirmed_at,
                'autoAdvance', a.auto_advance),
            'placement', jsonb_build_object(
                'dictionaryVersion', t.bank_version,
                'claimedAt', t.claimed_at,
                'answers', COALESCE((
                    SELECT jsonb_agg(jsonb_build_object(
                        'questionNumber', entry.value->'Answer'->'QuestionNumber',
                        'text', entry.value->'Answer'->'Text',
                        'choice', entry.value->'Answer'->'Choice',
                        'skipped', entry.value->'Answer'->'Skip') ORDER BY entry.position)
                    FROM jsonb_array_elements(t.submissions) WITH ORDINALITY AS entry(value, position)
                ), '[]'::jsonb)),
            'wordProgress', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'wordId', p.word_id,
                    'direction', CASE p.direction WHEN 0 THEN 'EnglishToPolish' ELSE 'PolishToEnglish' END,
                    'mode', CASE p.mode WHEN 0 THEN 'Choices' ELSE 'Typed' END,
                    'reviewBox', p.review_box,
                    'dueAt', p.due_at) ORDER BY p.word_id)
                FROM word_progress p WHERE p.owner_id = a.id
            ), '[]'::jsonb),
            'learningSessions', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'active', s.active,
                    'dictionaryVersion', s.state->'Version',
                    'category', s.state->'Category',
                    'timeZone', s.state->'TimeZoneId',
                    'endedByRequest', s.state->'Ended',
                    'wordIds', COALESCE((
                        SELECT jsonb_agg(word.value->'Id' ORDER BY word.position)
                        FROM jsonb_array_elements(s.state->'Words') WITH ORDINALITY AS word(value, position)
                    ), '[]'::jsonb),
                    'answers', COALESCE((
                        SELECT jsonb_agg(jsonb_build_object(
                            'questionNumber', entry.value->'Answer'->'QuestionNumber',
                            'text', entry.value->'Answer'->'Text',
                            'choice', entry.value->'Answer'->'Choice',
                            'answeredAt', entry.value->'AnsweredAt') ORDER BY entry.position)
                        FROM jsonb_array_elements(s.state->'Answers') WITH ORDINALITY AS entry(value, position)
                    ), '[]'::jsonb)) ORDER BY s.id)
                FROM learning_sessions s WHERE s.owner_id = a.id
            ), '[]'::jsonb),
            'learningDays', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'day', d.credited_day,
                    'completedAt', d.completed_at,
                    'timeZone', d.time_zone) ORDER BY d.credited_day)
                FROM learning_days d WHERE d.owner_id = a.id
            ), '[]'::jsonb)
        )::text
        FROM accounts a JOIN guest_placement_tests t ON t.id = a.placement_test_id
        WHERE a.id = $1
        """;
}
