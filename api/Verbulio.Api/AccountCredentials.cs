using Microsoft.AspNetCore.Identity;
using Npgsql;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal sealed class LoginRequest
{
    public string? Email { get; init; }
    public string? Password { get; init; }
}

internal sealed class VerifiedCredentials(RegisteredAccount account, string passwordHash)
{
    internal RegisteredAccount Account { get; } = account;
    internal string PasswordHash { get; } = passwordHash;
}

internal sealed class AccountCredentials(
    NpgsqlDataSource dataSource, IPasswordHasher<RegisteredAccount> passwordHasher)
{
    private static readonly RegisteredAccount UnknownAccount = new(Guid.Empty, VocabularyLevel.A1);
    private readonly string _unknownHash = passwordHasher.HashPassword(UnknownAccount, Guid.NewGuid().ToString());

    internal async Task<VerifiedCredentials?> VerifyAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        if (email is null or { Length: 0 or > 254 } || email.Contains('\0')
            || request.Password is not { Length: > 0 and <= 256 } password)
        {
            return null;
        }

        RegisteredAccount? account = null;
        var hash = _unknownHash;
        await using (var command = dataSource.CreateCommand("""
            SELECT id, starting_level, password_hash FROM accounts WHERE normalized_email = $1
            """))
        {
            command.Parameters.AddWithValue(AccountRegistration.NormalizeEmail(email));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                account = new RegisteredAccount(reader.GetGuid(0), (VocabularyLevel)reader.GetInt32(1));
                hash = reader.GetString(2);
            }
        }

        var verification = passwordHasher.VerifyHashedPassword(account ?? UnknownAccount, hash, password);
        return verification == PasswordVerificationResult.Failed || account is null ? null : new(account, hash);
    }
}
