using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Verbulio.Learning;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal sealed record GuestPlacementQuestion(
    int Number, string Text, TranslationDirection Direction, AnswerMode Mode, IReadOnlyList<string> Choices);
internal sealed record GuestPlacementProgress(
    GuestPlacementQuestion? Question, int AnsweredCount, int TotalCount, VocabularyLevel? Result);
internal sealed record GuestPlacementView(Guid Id, GuestPlacementProgress Test);
internal sealed record GuestPlacementSubmission(Guid Key, PlacementSubmission Answer);

internal sealed class GuestPlacementStore(NpgsqlDataSource dataSource)
{
    internal async Task<GuestPlacementView> StartAsync(Guid guest, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockGuestAsync(connection, transaction, guest, cancellationToken);
        var existing = await LoadAsync(connection, transaction, guest, id: null, cancellationToken);
        var view = existing is null
            ? await InsertAsync(connection, transaction, guest, cancellationToken)
            : ToView(existing);
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    internal async Task<GuestPlacementView?> ReadAsync(Guid guest, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var saved = await LoadAsync(connection, transaction: null, guest, id, cancellationToken);
        return saved is null ? null : ToView(saved);
    }

    internal async Task<GuestPlacementView?> RestartAsync(Guid guest, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockGuestAsync(connection, transaction, guest, cancellationToken);
        await using var retire = new NpgsqlCommand("""
            UPDATE guest_placement_tests SET superseded = true
            WHERE id = $1 AND guest_id = $2 AND NOT superseded
            """, connection, transaction);
        retire.Parameters.AddWithValue(id);
        retire.Parameters.AddWithValue(guest);
        if (await retire.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            return null;
        }

        var view = await InsertAsync(connection, transaction, guest, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    internal async Task<GuestPlacementView?> SubmitAsync(Guid guest, Guid id, Guid key, PlacementSubmission answer,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var saved = await LoadAsync(connection, transaction, guest, id, cancellationToken);
        if (saved is null)
        {
            return null;
        }

        var test = Replay(saved);
        var view = test.Submit(key, answer);
        if (saved.Submissions.All(previous => previous.Key != key))
        {
            saved.Submissions.Add(new GuestPlacementSubmission(key, answer));
            await using var update = new NpgsqlCommand("""
                UPDATE guest_placement_tests SET submissions = $1, result = $2
                WHERE id = $3 AND guest_id = $4
                """, connection, transaction);
            update.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(saved.Submissions));
            update.Parameters.AddWithValue(NpgsqlDbType.Integer, view.Result is { } level ? (int)level : DBNull.Value);
            update.Parameters.AddWithValue(id);
            update.Parameters.AddWithValue(guest);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ToView(id, view);
    }

    private static async Task LockGuestAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid guest,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended($1, 0))", connection, transaction);
        command.Parameters.AddWithValue($"placement:{guest}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<GuestPlacementView> InsertAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid guest, CancellationToken cancellationToken)
    {
        var bank = LocalPlacementBank.Instance;
        var id = Guid.NewGuid();
        var seed = Random.Shared.Next();
        await using var insert = new NpgsqlCommand("""
            INSERT INTO guest_placement_tests (id, guest_id, bank_version, seed) VALUES ($1, $2, $3, $4)
            """, connection, transaction);
        insert.Parameters.AddWithValue(id);
        insert.Parameters.AddWithValue(guest);
        insert.Parameters.AddWithValue(bank.Version);
        insert.Parameters.AddWithValue(seed);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return ToView(id, new PlacementTest(bank, seed).View);
    }

    private static async Task<SavedTest?> LoadAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        Guid guest, Guid? id, CancellationToken cancellationToken)
    {
        const string select = """
            SELECT id, bank_version, seed, submissions::text, result FROM guest_placement_tests
            WHERE guest_id = $1 AND NOT superseded AND ($2 IS NULL OR id = $2)
            """;
        await using var command = new NpgsqlCommand(transaction is null ? select : select + " FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue(guest);
        command.Parameters.AddWithValue(NpgsqlDbType.Uuid, id is { } value ? value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SavedTest(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2),
            JsonSerializer.Deserialize<List<GuestPlacementSubmission>>(reader.GetString(3))
                ?? throw new InvalidOperationException("Missing stored placement submissions."),
            reader.IsDBNull(4) ? null : (VocabularyLevel)reader.GetInt32(4));
    }

    private static GuestPlacementView ToView(SavedTest saved) => ToView(saved.Id, Replay(saved).View);

    private static GuestPlacementView ToView(Guid id, PlacementView view)
    {
        var question = view.Question is { } model
            ? new GuestPlacementQuestion(model.Number, model.Text, model.Direction, model.Mode, model.Choices)
            : null;
        return new GuestPlacementView(id, new GuestPlacementProgress(question, view.AnsweredCount, view.TotalCount, view.Result));
    }

    private static PlacementTest Replay(SavedTest saved)
    {
        var bank = LocalPlacementBank.Instance;
        if (saved.BankVersion != bank.Version)
        {
            throw new InvalidOperationException("Unsupported placement bank version.");
        }

        var test = new PlacementTest(bank, saved.Seed);
        foreach (var submission in saved.Submissions)
        {
            test.Submit(submission.Key, submission.Answer);
        }

        if (test.View.Result != saved.Result)
        {
            throw new InvalidOperationException("The stored placement result differs from the replayed result.");
        }

        return test;
    }

    private sealed record SavedTest(Guid Id, string BankVersion, int Seed,
        List<GuestPlacementSubmission> Submissions, VocabularyLevel? Result);
}
