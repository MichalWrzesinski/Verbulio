using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Verbulio.Learning;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal sealed record WordState(string Id, TranslationDirection Direction, AnswerMode Mode, ReviewSchedule? Review)
{
    internal SessionWord ToSessionWord(LearningCatalog catalog) =>
        catalog.Words[Id] with { NextDirection = Direction, NextMode = Mode, Review = Review };
}

internal sealed record StoredAnswer(Guid Id, LearningAnswer Answer, DateTimeOffset AnsweredAt);
internal sealed record SessionView(Guid Id, LearningQuestion? Question, LearningSummary Summary);
internal sealed record AnswerView(LearningFeedback Feedback, SessionView Session);

internal sealed record SessionState(int Version, int Seed, WordState[] Words, List<StoredAnswer> Answers, bool Ended)
{
    internal LearningSession Reconstitute()
    {
        var catalog = LearningCatalog.Resolve(Version);
        var session = new LearningSession(Words.Select(word => word.ToSessionWord(catalog)), catalog.Evaluator, Seed);
        foreach (var answer in Answers)
        {
            session.Submit(answer.Id, answer.Answer, answer.AnsweredAt);
        }

        if (Ended)
        {
            session.End();
        }

        return session;
    }
}

internal sealed class SessionStore(NpgsqlDataSource dataSource, TimeProvider timeProvider)
{
    internal async Task<SessionView?> StartAsync(Guid owner, VocabularyLevel? startingLevel, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1, 0))", connection, transaction))
        {
            gate.Parameters.AddWithValue(owner.ToString());
            await gate.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var existing = new NpgsqlCommand(
            "SELECT id, state::text FROM learning_sessions WHERE owner_id = $1 AND active FOR UPDATE", connection, transaction))
        {
            existing.Parameters.AddWithValue(owner);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return View(reader.GetGuid(0), Deserialize(reader.GetString(1)).Reconstitute());
            }
        }

        var progress = new Dictionary<string, WordState>(StringComparer.Ordinal);
        await using (var select = new NpgsqlCommand(
            "SELECT word_id, direction, mode, review_box, due_at FROM word_progress WHERE owner_id = $1",
            connection, transaction))
        {
            select.Parameters.AddWithValue(owner);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                progress.Add(id, new WordState(id, (TranslationDirection)reader.GetInt32(1), (AnswerMode)reader.GetInt32(2),
                    new ReviewSchedule((ReviewBox)reader.GetInt32(3), reader.GetFieldValue<DateTimeOffset>(4))));
            }
        }

        var seed = Random.Shared.Next();
        var catalog = startingLevel is null ? LearningCatalog.Fixture : LearningCatalog.Pilot;
        var candidates = catalog.Words.Values
            .Where(word => startingLevel is null || catalog.Levels[word.Id] == startingLevel || progress.ContainsKey(word.Id))
            .Select(word => progress.TryGetValue(word.Id, out var saved) ? saved.ToSessionWord(catalog) : word);
        var selected = SessionSelection.Select(candidates, timeProvider.GetUtcNow(), seed);
        if (selected.Count == 0)
        {
            return null;
        }

        var sessionId = Guid.NewGuid();
        var state = new SessionState(catalog.Version, seed,
            selected.Select(word => new WordState(word.Id, word.NextDirection, word.NextMode, word.Review)).ToArray(), [], false);
        await using var insert = new NpgsqlCommand(
            "INSERT INTO learning_sessions (id, owner_id, active, state) VALUES ($1, $2, true, $3)", connection, transaction);
        insert.Parameters.AddWithValue(sessionId);
        insert.Parameters.AddWithValue(owner);
        insert.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(state));
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return View(sessionId, state.Reconstitute());
    }

    internal async Task<SessionView?> ReadAsync(Guid owner, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var state = await LoadAsync(connection, null, owner, id, cancellationToken);
        return state is null ? null : View(id, state.Reconstitute());
    }

    internal async Task<AnswerView?> SubmitAsync(Guid owner, Guid id, Guid key, LearningAnswer answer,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var state = await LoadAsync(connection, transaction, owner, id, cancellationToken);
        if (state is null)
        {
            return null;
        }

        var session = state.Reconstitute();
        var answeredAt = timeProvider.GetUtcNow();
        var duplicate = state.Answers.Exists(previous => previous.Id == key);
        var feedback = session.Submit(key, answer, answeredAt);

        if (!duplicate)
        {
            state.Answers.Add(new StoredAnswer(key, answer, answeredAt));
            if (feedback.Progress is { } progress)
            {
                await SaveProgressAsync(connection, transaction, owner, progress, cancellationToken);
            }

            await SaveAsync(connection, transaction, owner, id, state, session.CurrentQuestion is not null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return new AnswerView(feedback, View(id, session));
    }

    internal async Task<SessionView?> EndAsync(Guid owner, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var state = await LoadAsync(connection, transaction, owner, id, cancellationToken);
        if (state is null)
        {
            return null;
        }

        var session = state.Reconstitute();
        session.End();
        await SaveAsync(connection, transaction, owner, id, state with { Ended = true }, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return View(id, session);
    }

    private static async Task<SessionState?> LoadAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        Guid owner, Guid id, CancellationToken cancellationToken)
    {
        const string query = "SELECT state::text FROM learning_sessions WHERE id = $1 AND owner_id = $2";
        await using var command = new NpgsqlCommand(transaction is null ? query : query + " FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(owner);
        return await command.ExecuteScalarAsync(cancellationToken) is string json ? Deserialize(json) : null;
    }

    private static async Task SaveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid owner, Guid id,
        SessionState state, bool active, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "UPDATE learning_sessions SET state = $1, active = $2 WHERE id = $3 AND owner_id = $4", connection, transaction);
        command.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(state));
        command.Parameters.AddWithValue(active);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(owner);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SaveProgressAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid owner,
        WordProgressChange progress, CancellationToken cancellationToken)
    {
        const string update = """
            UPDATE word_progress SET direction = $1, mode = $2 WHERE owner_id = $3 AND word_id = $4
            """;
        const string upsert = """
            INSERT INTO word_progress (direction, mode, owner_id, word_id, review_box, due_at) VALUES ($1, $2, $3, $4, $5, $6)
            ON CONFLICT (owner_id, word_id) DO UPDATE SET direction = EXCLUDED.direction, mode = EXCLUDED.mode,
                review_box = EXCLUDED.review_box, due_at = EXCLUDED.due_at
            """;
        await using var command = new NpgsqlCommand(progress.ChangedReview is null ? update : upsert, connection, transaction);
        command.Parameters.AddWithValue((int)progress.NextDirection);
        command.Parameters.AddWithValue((int)progress.NextMode);
        command.Parameters.AddWithValue(owner);
        command.Parameters.AddWithValue(progress.WordId);
        if (progress.ChangedReview is { } review)
        {
            command.Parameters.AddWithValue((int)review.Box);
            command.Parameters.AddWithValue(review.DueAt);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SessionView View(Guid id, LearningSession session) => new(id, session.CurrentQuestion, session.Summary);

    private static SessionState Deserialize(string json) =>
        JsonSerializer.Deserialize<SessionState>(json) ?? throw new InvalidOperationException("Missing session state.");
}
