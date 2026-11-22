namespace Verbulio.Api;

internal static partial class AccountSecurityEvents
{
    [LoggerMessage(8100, LogLevel.Warning, "Account login rejected")]
    internal static partial void LoginRejected(ILogger logger);

    [LoggerMessage(8101, LogLevel.Warning, "Account login attempt rate limit reached")]
    internal static partial void LoginLimited(ILogger logger);

    [LoggerMessage(8102, LogLevel.Warning, "Account session authorization denied")]
    internal static partial void SessionDenied(ILogger logger);
}
