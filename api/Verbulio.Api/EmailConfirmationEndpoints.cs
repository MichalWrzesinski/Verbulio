using System.Net.Mail;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;

namespace Verbulio.Api;

internal static class EmailConfirmationEndpoints
{
    internal static void AddEmailConfirmation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<EmailConfirmationStore>();
        if (!configuration.GetValue<bool>("EmailConfirmation:SendEnabled"))
        {
            return;
        }

        var key = configuration["EmailConfirmation:BrevoApiKey"];
        var sender = configuration["EmailConfirmation:Sender"];
        if (string.IsNullOrWhiteSpace(key) || key.Any(char.IsWhiteSpace)
            || !MailAddress.TryCreate(sender, out var address) || address.Address != sender
            || !Uri.TryCreate(configuration["EmailConfirmation:PageUrl"], UriKind.Absolute, out var page)
            || page.Scheme != Uri.UriSchemeHttps || page.Fragment.Length != 0
            || page.Query.Length != 0 || page.UserInfo.Length != 0)
        {
            throw new InvalidOperationException("Configure the confirmation mail sender, Brevo key and HTTPS page URL.");
        }

        services.AddSingleton(_ => new BrevoMailSender(key, address.Address));
        services.AddSingleton(provider =>
        {
            var transport = provider.GetRequiredService<BrevoMailSender>();
            return new ConfirmationMail(page, transport.SendAsync);
        });
    }

    internal static void MapEmailConfirmation(this RouteGroupBuilder account, AuthorizeAttribute accountOnly,
        string rateLimit, Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> clientFilter)
    {
        account.MapPost("/email-confirmation/confirm", async (ConfirmationRequest request, EmailConfirmationStore store,
            CancellationToken cancellationToken) =>
            request.Token is { } token && await store.ConfirmAsync(token, cancellationToken)
                ? Results.NoContent()
                : Problem(400, "invalid_confirmation_token", "Link potwierdzający jest nieprawidłowy lub wygasł."))
            .RequireRateLimiting(rateLimit).AddEndpointFilter(clientFilter);

        account.MapPost("/email-confirmation/send", async (ClaimsPrincipal user, HttpContext http,
            EmailConfirmationStore store, CancellationToken cancellationToken) =>
        {
            var mail = http.RequestServices.GetService<ConfirmationMail>();
            if (mail is null)
            {
                return Problem(503, "confirmation_delivery_unavailable", "Wysyłka potwierdzeń jest niedostępna.");
            }

            var owner = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var issue = await store.IssueAsync(owner, cancellationToken, TimeSpan.FromMinutes(1));
            if (issue is null)
            {
                return Results.Accepted();
            }

            var result = await mail.SendAsync(issue, cancellationToken);
            return result == MailSubmission.Rejected
                ? Problem(503, "confirmation_delivery_rejected", "Nie udało się wysłać wiadomości potwierdzającej.")
                : Results.Accepted();
        }).RequireAuthorization(accountOnly).RequireRateLimiting(rateLimit).AddEndpointFilter(clientFilter);
    }

    private static IResult Problem(int status, string code, string title) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
}

internal sealed record ConfirmationRequest(string? Token);

internal sealed class ConfirmationMail(Uri page, Func<TransactionalMail, CancellationToken, Task<MailSubmission>> send)
{
    internal Task<MailSubmission> SendAsync(EmailConfirmationIssue issue, CancellationToken cancellationToken)
    {
        var link = new UriBuilder(page) { Fragment = "token=" + Uri.EscapeDataString(issue.Token) }.Uri.AbsoluteUri;
        var safeLink = HtmlEncoder.Default.Encode(link);
        return send(new TransactionalMail(issue.Email, "Potwierdź adres e-mail w Verbulio",
            $"Potwierdź adres e-mail w Verbulio: {link}\n\nLink jest ważny przez 24 godziny.",
            $"<p><a href=\"{safeLink}\">Potwierdź adres e-mail w Verbulio</a></p><p>Link jest ważny przez 24 godziny.</p>"),
            cancellationToken);
    }
}
