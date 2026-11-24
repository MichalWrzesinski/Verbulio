using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Verbulio.Api;

namespace Verbulio.Learning.Tests;

public sealed class ConfirmationMailTests
{
    [Fact]
    public async Task SendsRecipientAndFragmentLinkThroughTheTransport()
    {
        var mail = new ConfirmationMail(new Uri("https://example.invalid/potwierdz-adres"), (message, _) =>
        {
            Assert.Equal("learner@example.invalid", message.Recipient);
            Assert.Contains("https://example.invalid/potwierdz-adres#token=a%26b", message.Text);
            Assert.Contains("href=\"https://example.invalid/potwierdz-adres#token=a%26b\"", message.Html);
            return Task.FromResult(MailSubmission.Unknown);
        });

        Assert.Equal(MailSubmission.Unknown,
            await mail.SendAsync(new EmailConfirmationIssue("learner@example.invalid", "a&b"), default));
    }

    [Fact]
    public void RegistrationDoesNotEnableDeliveryByDefault()
    {
        var services = new ServiceCollection();
        services.AddEmailConfirmation(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<ConfirmationMail>());
    }
}
