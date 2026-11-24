using System.Net;
using System.Text.Json;
using Verbulio.Api;

namespace Verbulio.Learning.Tests;

public sealed class BrevoMailSenderTests
{
    private static readonly TransactionalMail Message = new("learner@example.invalid", "Potwierdź adres e-mail",
        "Potwierdź adres w Verbulio.", "<p>Potwierdź adres w Verbulio.</p>");

    [Fact]
    public async Task SubmitsOneMessageWithBothRepresentations()
    {
        using var sender = new BrevoMailSender("fixture-key", "sender@example.invalid",
            new Handler(async (request, cancellationToken) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://api.brevo.com/v3/smtp/email", request.RequestUri!.AbsoluteUri);
                Assert.Equal("fixture-key", Assert.Single(request.Headers.GetValues("api-key")));
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var json = body.RootElement;
                Assert.Equal("sender@example.invalid", json.GetProperty("sender").GetProperty("email").GetString());
                Assert.Equal(Message.Recipient, Assert.Single(json.GetProperty("to").EnumerateArray())
                    .GetProperty("email").GetString());
                Assert.Equal(Message.Subject, json.GetProperty("subject").GetString());
                Assert.Equal(Message.Text, json.GetProperty("textContent").GetString());
                Assert.Equal(Message.Html, json.GetProperty("htmlContent").GetString());
                return new HttpResponseMessage(HttpStatusCode.Created);
            }));

        Assert.Equal(MailSubmission.Accepted, await sender.SendAsync(Message, default));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, false)]
    [InlineData(HttpStatusCode.Redirect, false)]
    [InlineData(HttpStatusCode.RequestTimeout, false)]
    [InlineData(HttpStatusCode.BadRequest, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.BadGateway, false)]
    public async Task ClassifiesProviderResponsesWithoutRetry(HttpStatusCode status, bool rejected)
    {
        var attempts = 0;
        using var sender = new BrevoMailSender("fixture-key", "sender@example.invalid", new Handler((_, _) =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(status));
        }));

        Assert.Equal(rejected ? MailSubmission.Rejected : MailSubmission.Unknown,
            await sender.SendAsync(Message, default));
        Assert.Equal(1, attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TreatsTransportFailureOrTimeoutAsUnconfirmed(bool timeout)
    {
        using var sender = new BrevoMailSender("fixture-key", "sender@example.invalid", new Handler((_, _) =>
            Task.FromException<HttpResponseMessage>(timeout ? new TaskCanceledException() : new HttpRequestException())));

        Assert.Equal(MailSubmission.Unknown, await sender.SendAsync(Message, default));
    }

    [Fact]
    public async Task PropagatesCallerCancellation()
    {
        using var caller = new CancellationTokenSource();
        using var sender = new BrevoMailSender("fixture-key", "sender@example.invalid", new Handler((_, token) =>
        {
            caller.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(token);
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.SendAsync(Message, caller.Token));
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
