using Npgsql;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal sealed record AccountSession(Guid Id, DateTimeOffset ExpiresAt);

internal sealed class AccountSessions(NpgsqlDataSource dataSource, TimeProvider timeProvider)
{
    internal const string IdClaim = "account_session";
    internal const string LevelClaim = "starting_level";
    internal static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    internal async Task<AccountSession> ReplaceAsync(Guid account, Guid? previous, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (previous is { } id)
        {
            await using var revoke = new NpgsqlCommand("DELETE FROM account_sessions WHERE id = $1", connection, transaction);
            revoke.Parameters.AddWithValue(id);
            await revoke.ExecuteNonQueryAsync(cancellationToken);
        }

        var session = new AccountSession(Guid.NewGuid(), timeProvider.GetUtcNow().Add(Lifetime));
        await using var insert = new NpgsqlCommand("""
            INSERT INTO account_sessions (id, account_id, expires_at) VALUES ($1, $2, $3)
            """, connection, transaction);
        insert.Parameters.AddWithValue(session.Id);
        insert.Parameters.AddWithValue(account);
        insert.Parameters.AddWithValue(session.ExpiresAt);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    internal async Task<RegisteredAccount?> ReadAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT accounts.id, accounts.starting_level
            FROM account_sessions JOIN accounts ON accounts.id = account_sessions.account_id
            WHERE account_sessions.id = $1 AND account_sessions.expires_at > $2
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(timeProvider.GetUtcNow());
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
