using System.Net;
using System.Net.Http.Json;

namespace Verbulio.Api;

internal enum MailSubmission
{
    Accepted,
    Rejected,
    Unknown
}

internal sealed record TransactionalMail(string Recipient, string Subject, string Text, string Html);

internal sealed class BrevoMailSender(string apiKey, string senderAddress, HttpMessageHandler? handler = null)
    : IDisposable
{
    private static readonly Uri Endpoint = new("https://api.brevo.com/v3/smtp/email");
    private readonly HttpClient _client = new(handler ?? new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(3)
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    internal async Task<MailSubmission> SendAsync(TransactionalMail message, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("api-key", apiKey);
        request.Content = JsonContent.Create(new
        {
            sender = new { email = senderAddress, name = "Verbulio" },
            to = new[] { new { email = message.Recipient } },
            subject = message.Subject,
            textContent = message.Text,
            htmlContent = message.Html
        });

        try
        {
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.Created => MailSubmission.Accepted,
                HttpStatusCode.RequestTimeout => MailSubmission.Unknown,
                >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError => MailSubmission.Rejected,
                _ => MailSubmission.Unknown
            };
        }
        catch (HttpRequestException)
        {
            return MailSubmission.Unknown;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return MailSubmission.Unknown;
        }
    }

    public void Dispose() => _client.Dispose();
}
