using Microsoft.AspNetCore.Identity;
using Npgsql;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal sealed record RegisteredAccount(Guid Id, VocabularyLevel StartingLevel);

internal sealed class RegistrationStore(
    NpgsqlDataSource dataSource, IPasswordHasher<RegisteredAccount> passwordHasher, TimeProvider timeProvider)
{
    internal async Task<RegisteredAccount?> RegisterAsync(Guid guest, AccountRegistration request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        await using var claim = new NpgsqlCommand("""
            UPDATE guest_placement_tests SET claimed_at = $1
            WHERE id = $2 AND guest_id = $3 AND NOT superseded AND result IS NOT NULL AND claimed_at IS NULL
            RETURNING result
            """, connection, transaction);
        claim.Parameters.AddWithValue(now);
        claim.Parameters.AddWithValue(request.PlacementTestId);
        claim.Parameters.AddWithValue(guest);
        if (await claim.ExecuteScalarAsync(cancellationToken) is not int level)
        {
            return null;
        }

        var account = new RegisteredAccount(Guid.NewGuid(), (VocabularyLevel)level);
        var passwordHash = passwordHasher.HashPassword(account, request.Password);
        await using var create = new NpgsqlCommand("""
            INSERT INTO accounts (id, email, normalized_email, password_hash, starting_level, placement_test_id, created_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            ON CONFLICT (normalized_email) DO NOTHING
            RETURNING id
            """, connection, transaction);
        create.Parameters.AddWithValue(account.Id);
        create.Parameters.AddWithValue(request.Email);
        create.Parameters.AddWithValue(request.NormalizedEmail);
        create.Parameters.AddWithValue(passwordHash);
        create.Parameters.AddWithValue(level);
        create.Parameters.AddWithValue(request.PlacementTestId);
        create.Parameters.AddWithValue(now);
        if (await create.ExecuteScalarAsync(cancellationToken) is null)
        {
            // Disposing the transaction also rolls back the claim.
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return account;
    }
}
