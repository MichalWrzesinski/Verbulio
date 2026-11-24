using Microsoft.AspNetCore.Identity;
using Npgsql;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal sealed record PasswordResetRequest(string? Token, string? Password);

internal sealed class PasswordResetIssue(string email, string token)
{
    internal string Email { get; } = email;
    internal string Token { get; } = token;
}

internal sealed class PasswordResetStore(NpgsqlDataSource dataSource, IPasswordHasher<RegisteredAccount> passwordHasher,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    internal async Task<PasswordResetIssue?> IssueAsync(string email, CancellationToken cancellationToken)
    {
        if (email.Length is 0 or > 254 || email.Contains('\0')) return null;

        var token = AccountLinkToken.Create();
        var expiresAt = timeProvider.GetUtcNow() + Lifetime;
        await using var command = dataSource.CreateCommand("""
            UPDATE accounts SET password_reset_digest = $1, password_reset_expires_at = $2
            WHERE normalized_email = $3
                AND (password_reset_expires_at IS NULL OR password_reset_expires_at <= $4)
            RETURNING email
            """);
        command.Parameters.AddWithValue(AccountLinkToken.Digest(token));
        command.Parameters.AddWithValue(expiresAt);
        command.Parameters.AddWithValue(AccountRegistration.NormalizeEmail(email.Trim()));
        command.Parameters.AddWithValue(expiresAt - MinimumInterval);
        return await command.ExecuteScalarAsync(cancellationToken) is string recipient
            ? new(recipient, token)
            : null;
    }

    internal async Task<bool> ResetAsync(string token, string password, CancellationToken cancellationToken)
    {
        if (token.Length != AccountLinkToken.EncodedLength || !AccountRegistration.IsValidPassword(password))
        {
            return false;
        }

        var digest = AccountLinkToken.Digest(token);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        RegisteredAccount account;
        await using (var lookup = new NpgsqlCommand(
            "SELECT id, starting_level FROM accounts WHERE password_reset_digest = $1 AND password_reset_expires_at > $2",
            connection, transaction))
        {
            lookup.Parameters.AddWithValue(digest);
            lookup.Parameters.AddWithValue(timeProvider.GetUtcNow());
            await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return false;
            account = new RegisteredAccount(reader.GetGuid(0), (VocabularyLevel)reader.GetInt32(1));
        }

        await SessionStore.LockOwnerAsync(connection, transaction, account.Id, cancellationToken);
        var hash = passwordHasher.HashPassword(account, password);
        // Keep the last expiry as the issuance clock even after consuming the digest.
        await using var reset = new NpgsqlCommand("""
            UPDATE accounts SET password_hash = $1, password_reset_digest = NULL
            WHERE id = $2 AND password_reset_digest = $3 AND password_reset_expires_at > $4
            """, connection, transaction);
        reset.Parameters.AddWithValue(hash);
        reset.Parameters.AddWithValue(account.Id);
        reset.Parameters.AddWithValue(digest);
        reset.Parameters.AddWithValue(timeProvider.GetUtcNow());
        if (await reset.ExecuteNonQueryAsync(cancellationToken) != 1) return false;

        await using var revoke = new NpgsqlCommand("DELETE FROM account_sessions WHERE account_id = $1", connection, transaction);
        revoke.Parameters.AddWithValue(account.Id);
        await revoke.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
