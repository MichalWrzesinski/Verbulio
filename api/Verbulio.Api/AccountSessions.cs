using Npgsql;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal sealed record AccountSession(Guid Id, DateTimeOffset ExpiresAt);

internal sealed class AccountSessions(NpgsqlDataSource dataSource, TimeProvider timeProvider)
{
    internal const string IdClaim = "account_session";
    internal const string LevelClaim = "starting_level";
    internal static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);
    internal static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    internal async Task<AccountSession?> ReplaceAsync(Guid account, Guid? previous, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        Guid? previousOwner = null;
        if (previous is { } priorSession)
        {
            await using var lookup = new NpgsqlCommand(
                "SELECT account_id FROM account_sessions WHERE id = $1", connection, transaction);
            lookup.Parameters.AddWithValue(priorSession);
            if (await lookup.ExecuteScalarAsync(cancellationToken) is not Guid owner) return null;
            previousOwner = owner;
        }

        // Order both locks if this browser is switching accounts.
        if (previousOwner is { } first && first.CompareTo(account) < 0)
        {
            await SessionStore.LockOwnerAsync(connection, transaction, first, cancellationToken);
        }
        await SessionStore.LockOwnerAsync(connection, transaction, account, cancellationToken);
        if (previousOwner is { } last && last.CompareTo(account) > 0)
        {
            await SessionStore.LockOwnerAsync(connection, transaction, last, cancellationToken);
        }

        await using (var existing = new NpgsqlCommand("SELECT 1 FROM accounts WHERE id = $1", connection, transaction))
        {
            existing.Parameters.AddWithValue(account);
            if (await existing.ExecuteScalarAsync(cancellationToken) is null) return null;
        }

        if (previous is { } id)
        {
            await using var revoke = new NpgsqlCommand("DELETE FROM account_sessions WHERE id = $1", connection, transaction);
            revoke.Parameters.AddWithValue(id);
            if (await revoke.ExecuteNonQueryAsync(cancellationToken) == 0) return null;
        }

        var now = timeProvider.GetUtcNow();
        var session = new AccountSession(Guid.NewGuid(), now.Add(Lifetime));
        await using var insert = new NpgsqlCommand("""
            INSERT INTO account_sessions (id, account_id, expires_at, last_seen_at) VALUES ($1, $2, $3, $4)
            """, connection, transaction);
        insert.Parameters.AddWithValue(session.Id);
        insert.Parameters.AddWithValue(account);
        insert.Parameters.AddWithValue(session.ExpiresAt);
        insert.Parameters.AddWithValue(now);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    internal async Task<RegisteredAccount?> ReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var command = dataSource.CreateCommand("""
            UPDATE account_sessions SET last_seen_at = GREATEST(account_sessions.last_seen_at, $2)
            FROM accounts
            WHERE accounts.id = account_sessions.account_id AND account_sessions.id = $1
                AND account_sessions.expires_at > $2 AND account_sessions.last_seen_at > $3
            RETURNING accounts.id, accounts.starting_level
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(now);
        command.Parameters.AddWithValue(now.Subtract(IdleTimeout));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new RegisteredAccount(reader.GetGuid(0), (VocabularyLevel)reader.GetInt32(1))
            : null;
    }

    internal async Task RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM account_sessions WHERE id = $1");
        command.Parameters.AddWithValue(id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
