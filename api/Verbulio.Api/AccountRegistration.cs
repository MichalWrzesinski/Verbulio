using System.Net.Mail;

namespace Verbulio.Api;

internal sealed class RegistrationRequest
{
    public Guid PlacementTestId { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }
}

internal sealed class AccountRegistration
{
    private AccountRegistration(Guid placementTestId, string email, string password)
    {
        PlacementTestId = placementTestId;
        Email = email;
        NormalizedEmail = NormalizeEmail(email);
        Password = password;
    }

    internal Guid PlacementTestId { get; }
    internal string Email { get; }
    internal string NormalizedEmail { get; }
    internal string Password { get; }

    internal static string NormalizeEmail(string email) => email.ToUpperInvariant();

    internal static bool IsValidPassword(string password) =>
        password.Length <= 256 && password.EnumerateRunes().Count() is >= 12 and <= 128;

    internal static AccountRegistration? FromRequest(RegistrationRequest request)
    {
        var email = request.Email?.Trim();
        if (request.PlacementTestId == Guid.Empty || email is null || email.Length > 254
            || email.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))
            || !MailAddress.TryCreate(email, out var address)
            || !string.Equals(address.Address, email, StringComparison.Ordinal)
            || request.Password is not { } password || !IsValidPassword(password))
        {
            return null;
        }

        return new AccountRegistration(request.PlacementTestId, email, password);
    }
}
