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
    JournalUnavailable,
}

internal sealed class AccountDeletion(
    NpgsqlDataSource dataSource, AccountDeletionJournal journal, IPasswordHasher<RegisteredAccount> passwordHasher,
    ILogger<AccountDeletion> logger)
{
    internal async Task<AccountDeletionResult> DeleteAsync(Guid owner, string password, CancellationToken cancellationToken)
    {
        await using (var read = dataSource.CreateCommand("SELECT starting_level, password_hash FROM accounts WHERE id = $1"))
        {
            read.Parameters.AddWithValue(owner);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return AccountDeletionResult.Missing;

            var account = new RegisteredAccount(owner, (VocabularyLevel)reader.GetInt32(0));
            if (passwordHasher.VerifyHashedPassword(account, reader.GetString(1), password) == PasswordVerificationResult.Failed)
                return AccountDeletionResult.InvalidPassword;
        }

        // Record the verified deletion intent before changing the restorable database.
        try
        {
            await journal.RecordAsync(owner, cancellationToken);
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException)
        {
            AccountSecurityEvents.DeletionJournalUnavailable(logger);
            return AccountDeletionResult.JournalUnavailable;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RemoveOwnerAsync(connection, owner, cancellationToken);
        return AccountDeletionResult.Deleted;
    }

    internal async Task ReplayAsync(CancellationToken cancellationToken)
    {
        var owners = await journal.ReadAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        foreach (var owner in owners)
        {
            await RemoveOwnerAsync(connection, owner, cancellationToken);
        }
    }

    private static async Task RemoveOwnerAsync(NpgsqlConnection connection, Guid owner, CancellationToken cancellationToken)
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
