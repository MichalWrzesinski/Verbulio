using Npgsql;

namespace Verbulio.Api;

internal sealed class AccountDeletionJournal : IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;

    internal AccountDeletionJournal(string accountConnection, string journalConnection)
    {
        var account = new NpgsqlConnectionStringBuilder(accountConnection);
        var journal = new NpgsqlConnectionStringBuilder(journalConnection);
        if (string.IsNullOrWhiteSpace(account.Database) || string.IsNullOrWhiteSpace(journal.Database) ||
            string.Equals(account.Database, journal.Database, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The deletion journal requires a separate database.");
        }

        dataSource = NpgsqlDataSource.Create(journalConnection);
    }

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            CREATE TABLE IF NOT EXISTS verified_account_deletions (
                account_id uuid PRIMARY KEY,
                requested_at timestamptz NOT NULL DEFAULT now()
            )
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal async Task RecordAsync(Guid owner, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO verified_account_deletions (account_id)
            VALUES ($1) ON CONFLICT (account_id) DO NOTHING
            """);
        command.Parameters.AddWithValue(owner);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal async Task<IReadOnlyList<Guid>> ReadAsync(CancellationToken cancellationToken)
    {
        var owners = new List<Guid>();
        await using var command = dataSource.CreateCommand("SELECT account_id FROM verified_account_deletions");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            owners.Add(reader.GetGuid(0));
        }

        return owners;
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
