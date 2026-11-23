using Microsoft.AspNetCore.Identity;
using Npgsql;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal sealed record AccountDeletionRequest(string? Password);

internal enum AccountDeletionResult
{
    Deleted,
    Missing,
    InvalidPassword,
}

internal sealed class AccountDeletion(
    NpgsqlDataSource dataSource, AccountDeletionJournal journal, IPasswordHasher<RegisteredAccount> passwordHasher)
{
    internal async Task<AccountDeletionResult> DeleteAsync(Guid owner, string password, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SessionStore.LockOwnerAsync(connection, transaction, owner, cancellationToken);

        Guid placementId;
        await using (var read = new NpgsqlCommand("""
            SELECT placement_test_id, starting_level, password_hash FROM accounts WHERE id = $1 FOR UPDATE
            """, connection, transaction))
        {
            read.Parameters.AddWithValue(owner);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return AccountDeletionResult.Missing;

            var account = new RegisteredAccount(owner, (VocabularyLevel)reader.GetInt32(1));
            if (passwordHasher.VerifyHashedPassword(account, reader.GetString(2), password) == PasswordVerificationResult.Failed)
                return AccountDeletionResult.InvalidPassword;

            placementId = reader.GetGuid(0);
        }

        // Record the verified deletion intent before committing changes to the restorable database.
        await journal.RecordAsync(owner, cancellationToken);
        await DeleteRowsAsync(connection, transaction, owner, placementId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountDeletionResult.Deleted;
    }

    internal async Task ReplayAsync(CancellationToken cancellationToken)
    {
        var owners = await journal.ReadAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        foreach (var owner in owners)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await SessionStore.LockOwnerAsync(connection, transaction, owner, cancellationToken);
            await using var select = new NpgsqlCommand("""
                SELECT placement_test_id FROM accounts WHERE id = $1 FOR UPDATE
                """, connection, transaction);
            select.Parameters.AddWithValue(owner);
            if (await select.ExecuteScalarAsync(cancellationToken) is Guid placementId)
            {
                await DeleteRowsAsync(connection, transaction, owner, placementId, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task DeleteRowsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid owner,
        Guid placementId, CancellationToken cancellationToken)
    {
        // Deleting learning sessions first waits for any in-flight answer holding a session row lock.
        await using var batch = new NpgsqlBatch(connection, transaction);
        (string Sql, Guid Id)[] commands =
        [
            ("DELETE FROM learning_sessions WHERE owner_id = $1", owner),
            ("DELETE FROM word_progress WHERE owner_id = $1", owner),
            ("DELETE FROM learning_days WHERE owner_id = $1", owner),
            ("DELETE FROM accounts WHERE id = $1", owner),
            ("DELETE FROM guest_placement_tests WHERE id = $1", placementId),
        ];
        foreach (var (sql, id) in commands)
        {
            var command = new NpgsqlBatchCommand(sql);
            command.Parameters.AddWithValue(id);
            batch.BatchCommands.Add(command);
        }

        await batch.ExecuteNonQueryAsync(cancellationToken);
    }
}
