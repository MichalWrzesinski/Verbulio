using Npgsql;

namespace Verbulio.Api;

internal sealed record EmailConfirmationIssue(string Email, string Token);

internal sealed class EmailConfirmationStore(NpgsqlDataSource dataSource, TimeProvider timeProvider)
{
    internal static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    internal async Task<EmailConfirmationIssue?> IssueAsync(Guid accountId, CancellationToken cancellationToken,
        TimeSpan minimumInterval = default)
    {
        var token = AccountLinkToken.Create();
        var expiresAt = timeProvider.GetUtcNow() + Lifetime;
        await using var command = dataSource.CreateCommand("""
            UPDATE accounts
            SET email_confirmation_digest = $1, email_confirmation_expires_at = $2
            WHERE id = $3 AND email_confirmed_at IS NULL
                AND (email_confirmation_expires_at IS NULL OR email_confirmation_expires_at <= $4)
            RETURNING email
            """);
        command.Parameters.AddWithValue(AccountLinkToken.Digest(token));
        command.Parameters.AddWithValue(expiresAt);
        command.Parameters.AddWithValue(accountId);
        command.Parameters.AddWithValue(expiresAt - minimumInterval);
        return await command.ExecuteScalarAsync(cancellationToken) is string email
            ? new EmailConfirmationIssue(email, token)
            : null;
    }

    internal async Task<bool> ConfirmAsync(string token, CancellationToken cancellationToken)
    {
        if (token.Length != AccountLinkToken.EncodedLength)
        {
            return false;
        }

        await using var command = dataSource.CreateCommand("""
            UPDATE accounts
            SET email_confirmed_at = $1, email_confirmation_digest = NULL, email_confirmation_expires_at = NULL
            WHERE email_confirmation_digest = $2 AND email_confirmation_expires_at > $1
                AND email_confirmed_at IS NULL
            """);
        command.Parameters.AddWithValue(timeProvider.GetUtcNow());
        command.Parameters.AddWithValue(AccountLinkToken.Digest(token));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
