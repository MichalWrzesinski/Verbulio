using Npgsql;

namespace Verbulio.Api;

internal sealed record AccountContact(string Email);
internal sealed record LearningPreferences(bool AutoAdvance);
internal sealed record LearningPreferencesUpdate(bool? AutoAdvance);

internal sealed class AccountSettingsStore(NpgsqlDataSource dataSource)
{
    internal async Task<AccountContact?> ReadContactAsync(Guid owner, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT email FROM accounts WHERE id = $1");
        command.Parameters.AddWithValue(owner);
        return await command.ExecuteScalarAsync(cancellationToken) is string email ? new(email) : null;
    }

    internal async Task<LearningPreferences?> ReadAsync(Guid owner, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT auto_advance FROM accounts WHERE id = $1");
        command.Parameters.AddWithValue(owner);
        return await command.ExecuteScalarAsync(cancellationToken) is bool enabled ? new(enabled) : null;
    }

    internal async Task<LearningPreferences?> SaveAsync(Guid owner, bool enabled, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE accounts SET auto_advance = $2 WHERE id = $1 RETURNING auto_advance
            """);
        command.Parameters.AddWithValue(owner);
        command.Parameters.AddWithValue(enabled);
        return await command.ExecuteScalarAsync(cancellationToken) is bool saved ? new(saved) : null;
    }
}
