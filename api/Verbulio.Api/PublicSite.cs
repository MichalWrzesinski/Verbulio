using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Verbulio.Api;

internal static class PublicSite
{
    private const string Host = "verbulio.gitpush.app";

    internal static async Task RunAsync(WebApplicationBuilder builder)
    {
        Configure(builder);
        await using var app = builder.Build();
        UseIngress(app);
        MapHealth(app);
        app.UseStaticFiles();
        app.Map("/api/{**path}", () => Results.NotFound());
        app.MapFallbackToFile("index.html");
        await app.RunAsync();
    }

    internal static void Configure(WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 5444));
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            options.KnownProxies.Add(IPAddress.Loopback);
            options.ForwardLimit = 1;
            options.RequireHeaderSymmetry = true;
        });
    }

    internal static void UseIngress(WebApplication app)
    {
        app.UseForwardedHeaders();
        app.Use(async (context, next) =>
        {
            if (!context.Request.IsHttps || !string.Equals(context.Request.Host.Value, Host, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var headers = context.Response.Headers;
            headers.StrictTransportSecurity = "max-age=31536000";
            headers.ContentSecurityPolicy = "frame-ancestors 'none'; base-uri 'self'; object-src 'none'; form-action 'self'";
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            await next(context);
        });
    }

    internal static void MapHealth(WebApplication app, Func<HttpContext, Task<bool>>? ready = null)
    {
        var revisionPath = Path.Combine(app.Environment.ContentRootPath, "REVISION");
        var revision = File.Exists(revisionPath) ? File.ReadAllText(revisionPath).Trim() : null;
        if (revision is not null && (revision.Length != 40 || !revision.All(Uri.IsHexDigit)))
        {
            throw new InvalidOperationException("Invalid public release revision.");
        }

        app.MapGet("/health", async (HttpContext context) =>
        {
            if (revision is not null)
            {
                context.Response.Headers["X-Verbulio-Revision"] = revision;
            }

            return ready is null || await ready(context)
                ? Results.Ok()
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        });
    }
}
