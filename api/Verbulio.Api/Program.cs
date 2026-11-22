using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Verbulio.Api;
using Verbulio.Learning;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("The fixture API is available only in the Development environment.");
}

var connectionString = builder.Configuration.GetConnectionString("Learning")
    ?? throw new InvalidOperationException("Configure the local Learning database connection.");
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<GuestPlacementStore>();
builder.Services.AddSingleton<RegistrationStore>();
builder.Services.AddSingleton<IPasswordHasher<RegisteredAccount>, PasswordHasher<RegisteredAccount>>();
builder.Services.AddProblemDetails();
builder.Services.AddDataProtection()
    .SetApplicationName("Verbulio.LocalLearning")
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["LearningKeyDirectory"] ?? Path.Combine(builder.Environment.ContentRootPath, ".local-keys")));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "verbulio.local-learning";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(1);
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 4096);

var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => TypedResults.Ok());
var learning = app.MapGroup("/api/v1/local-learning");
learning.MapPost("/identity", async (HttpContext http) =>
{
    if (http.User.Identity?.IsAuthenticated != true)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
    }

    return TypedResults.NoContent();
}).AddEndpointFilter(RequireLocalClient);

var sessions = learning.MapGroup("/sessions").RequireAuthorization();
sessions.MapPost("", async (ClaimsPrincipal user, SessionStore store, CancellationToken cancellationToken) =>
{
    var session = await store.StartAsync(Owner(user), cancellationToken);
    return session is null
        ? Problem(409, "no_words_available", "Brak słówek do powtórki.")
        : Results.Ok(session);
}).AddEndpointFilter(RequireLocalClient);

sessions.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, SessionStore store, CancellationToken cancellationToken) =>
    await store.ReadAsync(Owner(user), id, cancellationToken) is { } session
        ? Results.Ok(session)
        : Problem(404, "session_not_found", "Nie znaleziono sesji."));

sessions.MapPost("/{id:guid}/answers", async (Guid id, LearningAnswer answer, HttpContext http, SessionStore store,
    CancellationToken cancellationToken) =>
{
    if (!Guid.TryParse(http.Request.Headers["Idempotency-Key"].ToString(), out var key) || key == Guid.Empty)
    {
        return Problem(400, "invalid_submission_key", "Brakuje identyfikatora odpowiedzi.");
    }

    if (answer.Text?.Length > 256)
    {
        return Problem(400, "invalid_answer", "Odpowiedź jest za długa.");
    }

    try
    {
        return await store.SubmitAsync(Owner(http.User), id, key, answer, cancellationToken) is { } result
            ? Results.Ok(result)
            : Problem(404, "session_not_found", "Nie znaleziono sesji.");
    }
    catch (ArgumentException)
    {
        return Problem(400, "invalid_answer", "Podaj odpowiedź w wymaganym formacie.");
    }
    catch (LearningConflictException conflict) when (conflict.Reason == LearningConflict.ReusedSubmission)
    {
        return Problem(422, "reused_submission_key", "Ta odpowiedź ma już inny zapis.");
    }
    catch (LearningConflictException)
    {
        return Problem(409, "answer_conflict", "Odśwież sesję przed wysłaniem odpowiedzi.");
    }
}).AddEndpointFilter(RequireLocalClient);

sessions.MapPost("/{id:guid}/end", async (Guid id, ClaimsPrincipal user, SessionStore store, CancellationToken cancellationToken) =>
    await store.EndAsync(Owner(user), id, cancellationToken) is { } session
        ? Results.Ok(session)
        : Problem(404, "session_not_found", "Nie znaleziono sesji.")).AddEndpointFilter(RequireLocalClient);

app.MapLocalPlacement();

app.MapPost("/api/v1/local-registration/accounts", async (
    RegistrationRequest data, ClaimsPrincipal user, RegistrationStore store, CancellationToken cancellationToken) =>
{
    var registration = AccountRegistration.FromRequest(data);
    if (registration is null)
    {
        return Problem(400, "invalid_registration", "Podaj poprawny adres e-mail i hasło od 12 do 128 znaków.");
    }

    var account = await store.RegisterAsync(Owner(user), registration, cancellationToken);
    return account is null
        ? Problem(409, "registration_unavailable", "Nie udało się utworzyć konta z tymi danymi.")
        : Results.Json(account, statusCode: StatusCodes.Status201Created);
}).RequireAuthorization().AddEndpointFilter(RequireLocalClient);

app.MapGet("/api/v1/local-registration/outcomes/{placementTestId:guid}", async (
    Guid placementTestId, ClaimsPrincipal user, RegistrationStore store, CancellationToken cancellationToken) =>
    await store.ReadOutcomeAsync(Owner(user), placementTestId, cancellationToken) is { } account
        ? Results.Ok(account)
        : Problem(404, "registration_not_found", "Brak zapisanego konta dla tego testu."))
    .RequireAuthorization();

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    var dataSource = app.Services.GetRequiredService<NpgsqlDataSource>();
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    var assembly = typeof(SessionStore).Assembly;
    foreach (var resource in assembly.GetManifestResourceNames()
        .Where(name => name.StartsWith("Verbulio.Api.Migrations.", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal))
    {
        await using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Missing migration resource.");
        using var reader = new StreamReader(stream);
        await using var migration = new NpgsqlCommand(await reader.ReadToEndAsync(), connection, transaction);
        await migration.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
    return;
}

await app.RunAsync();

static Guid Owner(ClaimsPrincipal account) => Guid.Parse(account.FindFirstValue(ClaimTypes.NameIdentifier)!);

static IResult Problem(int status, string code, string title) =>
    Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });

static ValueTask<object?> RequireLocalClient(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
    context.HttpContext.Request.Headers["X-Verbulio-Client"] == "local-learning"
        ? next(context)
        : ValueTask.FromResult<object?>(Problem(403, "invalid_client", "Niedozwolone żądanie."));
