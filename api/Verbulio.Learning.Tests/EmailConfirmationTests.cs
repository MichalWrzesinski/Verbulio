using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Verbulio.Api;

namespace Verbulio.Learning.Tests;

public sealed class EmailConfirmationTests
{
    [Fact]
    public async Task ConfirmationIsExpiringSingleUseAndScopedToItsAccount()
    {
        if (Environment.GetEnvironmentVariable("VERBULIO_CONFIRMATION_TEST_DB") is not { } connection)
        {
            return;
        }

        var schema = $"confirmation_{Guid.NewGuid():N}";
        await using var admin = NpgsqlDataSource.Create(connection);
        await using var create = admin.CreateCommand($"CREATE SCHEMA {schema}");
        await create.ExecuteNonQueryAsync();

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(
                new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString);
            await ApplyMigrationAsync(dataSource, "002-placement.sql");
            await ApplyMigrationAsync(dataSource, "003-registration.sql");
            var owner = await CreateAccountAsync(dataSource, "owner@example.invalid");
            var peer = await CreateAccountAsync(dataSource, "peer@example.invalid");
            await ApplyMigrationAsync(dataSource, "007-email-confirmation.sql");
            await ApplyMigrationAsync(dataSource, "007-email-confirmation.sql");

            var clock = new ConfirmationClock();
            var store = new EmailConfirmationStore(dataSource, clock);
            Assert.Null(await store.IssueAsync(Guid.NewGuid(), default));
            Assert.False(await store.ConfirmAsync("invalid", default));

            var original = Assert.IsType<EmailConfirmationIssue>(await store.IssueAsync(owner, default)).Token;
            var replacement = Assert.IsType<EmailConfirmationIssue>(await store.IssueAsync(owner, default)).Token;
            var peerToken = Assert.IsType<EmailConfirmationIssue>(await store.IssueAsync(peer, default)).Token;
            Assert.Null(await store.IssueAsync(owner, default, TimeSpan.FromMinutes(1)));
            Assert.NotEqual(original, replacement);
            Assert.False(await store.ConfirmAsync(original, default));

            clock.Now += TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1);
            Assert.True(await store.ConfirmAsync(peerToken, default));
            clock.Now += TimeSpan.FromSeconds(1);
            Assert.False(await store.ConfirmAsync(replacement, default));

            var current = Assert.IsType<EmailConfirmationIssue>(await store.IssueAsync(owner, default)).Token;
            var confirmations = await Task.WhenAll(store.ConfirmAsync(current, default),
                store.ConfirmAsync(current, default));
            Assert.Single(confirmations, result => result);
            Assert.False(await store.ConfirmAsync(current, default));
            Assert.Null(await store.IssueAsync(owner, default));

            await using var read = dataSource.CreateCommand("""
                SELECT email_confirmed_at, email_confirmation_digest, email_confirmation_expires_at
                FROM accounts WHERE id = $1
                """);
            read.Parameters.AddWithValue(owner);
            await using var row = await read.ExecuteReaderAsync();
            Assert.True(await row.ReadAsync());
            Assert.Equal(clock.Now.UtcDateTime, row.GetDateTime(0));
            Assert.True(row.IsDBNull(1));
            Assert.True(row.IsDBNull(2));
            Assert.Null(await store.IssueAsync(peer, default));

            await ExerciseSendEndpointAsync(dataSource, store, clock);
        }
        finally
        {
            await using var drop = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task ExerciseSendEndpointAsync(NpgsqlDataSource dataSource, EmailConfirmationStore store,
        ConfirmationClock clock)
    {
        var sender = await CreateAccountAsync(dataSource, "sender@example.invalid");
        var other = await CreateAccountAsync(dataSource, "other@example.invalid");
        var captured = new ConcurrentQueue<TransactionalMail>();
        var outcome = MailSubmission.Unknown;
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(new ConfirmationMail(new Uri("https://example.invalid/potwierdz-adres"),
            (mail, _) =>
            {
                captured.Enqueue(mail);
                return Task.FromResult(outcome);
            }));
        await using var app = builder.Build();
        app.MapGroup("/api/v1/local-account").MapEmailConfirmation(new AuthorizeAttribute(), "fixture",
            (context, next) => next(context));
        var send = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Single(endpoint =>
                endpoint.RoutePattern.RawText == "/api/v1/local-account/email-confirmation/send").RequestDelegate!;

        async Task<(int Status, string? Code)> RequestAsync(Guid owner)
        {
            await using var scope = app.Services.CreateAsyncScope();
            using var body = new MemoryStream();
            var http = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, owner.ToString())], "fixture"))
            };
            http.Request.Method = HttpMethods.Post;
            http.Request.Scheme = "https";
            http.Response.Body = body;
            await send(http);
            body.Position = 0;
            if (body.Length == 0)
            {
                return (http.Response.StatusCode, null);
            }

            using var problem = await JsonDocument.ParseAsync(body);
            return (http.Response.StatusCode, problem.RootElement.GetProperty("code").GetString());
        }

        var concurrent = await Task.WhenAll(RequestAsync(sender), RequestAsync(sender));
        Assert.All(concurrent, response => Assert.Equal((202, (string?)null), response));
        Assert.Equal("sender@example.invalid", Assert.Single(captured).Recipient);

        outcome = MailSubmission.Rejected;
        Assert.Equal((503, "confirmation_delivery_rejected"), await RequestAsync(other));
        Assert.Collection(captured,
            message => Assert.Equal("sender@example.invalid", message.Recipient),
            message => Assert.Equal("other@example.invalid", message.Recipient));

        outcome = MailSubmission.Accepted;
        clock.Now += TimeSpan.FromMinutes(1);
        Assert.Equal((202, (string?)null), await RequestAsync(sender));
        Assert.Equal(3, captured.Count);
    }

    private static async Task ApplyMigrationAsync(NpgsqlDataSource dataSource, string name)
    {
        await using var stream = typeof(EmailConfirmationStore).Assembly
            .GetManifestResourceStream($"Verbulio.Api.Migrations.{name}")!;
        using var reader = new StreamReader(stream);
        await using var migration = dataSource.CreateCommand(await reader.ReadToEndAsync());
        await migration.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> CreateAccountAsync(NpgsqlDataSource dataSource, string email)
    {
        var owner = Guid.NewGuid();
        await using var command = dataSource.CreateCommand("""
            WITH fixture AS (
                INSERT INTO guest_placement_tests (id, guest_id, bank_version, seed)
                VALUES (gen_random_uuid(), gen_random_uuid(), 'fixture', 1)
                RETURNING id
            )
            INSERT INTO accounts (id, email, normalized_email, password_hash, starting_level, placement_test_id, created_at)
            SELECT $1, $2, upper($2), 'fixture', 0, id, now() FROM fixture
            """);
        command.Parameters.AddWithValue(owner);
        command.Parameters.AddWithValue(email);
        await command.ExecuteNonQueryAsync();
        return owner;
    }

    private sealed class ConfirmationClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
