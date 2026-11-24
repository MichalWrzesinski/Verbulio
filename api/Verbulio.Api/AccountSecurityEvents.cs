namespace Verbulio.Api;

internal static partial class AccountSecurityEvents
{
    [LoggerMessage(8100, LogLevel.Warning, "Account login rejected")]
    internal static partial void LoginRejected(ILogger logger);

    [LoggerMessage(8101, LogLevel.Warning, "Account login attempt rate limit reached")]
    internal static partial void LoginLimited(ILogger logger);

    [LoggerMessage(8102, LogLevel.Warning, "Account session authorization denied")]
    internal static partial void SessionDenied(ILogger logger);

    [LoggerMessage(8103, LogLevel.Warning, "Account deletion journal unavailable")]
    internal static partial void DeletionJournalUnavailable(ILogger logger);

    [LoggerMessage(8104, LogLevel.Information, "Account password reset completed")]
    internal static partial void PasswordResetCompleted(ILogger logger);

    [LoggerMessage(8105, LogLevel.Warning, "Account link rate limit reached")]
    internal static partial void AccountLinkLimited(ILogger logger);
}
