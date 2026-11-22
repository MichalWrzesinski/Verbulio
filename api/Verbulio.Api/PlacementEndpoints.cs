using System.Security.Claims;
using Verbulio.Learning;
using Verbulio.Learning.Placement;

namespace Verbulio.Api;

internal static class PlacementEndpoints
{
    internal static void MapLocalPlacement(this WebApplication app)
    {
        var tests = app.MapGroup("/api/v1/local-placement/tests").RequireAuthorization();
        tests.MapPost("", async (ClaimsPrincipal user, GuestPlacementStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.StartAsync(Guest(user), cancellationToken)))
            .AddEndpointFilter(RequireLocalClient);

        tests.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, GuestPlacementStore store, CancellationToken cancellationToken) =>
            await store.ReadAsync(Guest(user), id, cancellationToken) is { } test ? Results.Ok(test) : Missing());

        tests.MapPost("/{id:guid}/restart", async (Guid id, ClaimsPrincipal user, GuestPlacementStore store, CancellationToken cancellationToken) =>
            await store.RestartAsync(Guest(user), id, cancellationToken) is { } test ? Results.Ok(test) : Missing())
            .AddEndpointFilter(RequireLocalClient);

        tests.MapPost("/{id:guid}/answers", async (Guid id, PlacementSubmission answer, HttpContext http,
            GuestPlacementStore store, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(http.Request.Headers["Idempotency-Key"].ToString(), out var key) || key == Guid.Empty)
            {
                return Problem(400, "invalid_submission_key", "Brakuje identyfikatora odpowiedzi.");
            }

            if (answer.Text is { } text && (text.Length > 256 || text.Contains('\0')))
            {
                return Problem(400, "invalid_answer", "Podaj odpowiedź w wymaganym formacie.");
            }

            try
            {
                return await store.SubmitAsync(Guest(http.User), id, key, answer, cancellationToken) is { } test
                    ? Results.Ok(test) : Missing();
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
                return Problem(409, "answer_conflict", "Odśwież test przed wysłaniem odpowiedzi.");
            }
        }).AddEndpointFilter(RequireLocalClient);
    }

    private static Guid Guest(ClaimsPrincipal account) => Guid.Parse(account.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static IResult Missing() => Problem(404, "test_not_found", "Nie znaleziono testu.");

    private static IResult Problem(int status, string code, string title) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });

    private static ValueTask<object?> RequireLocalClient(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.Request.Headers["X-Verbulio-Client"] == "local-learning"
            ? next(context)
            : ValueTask.FromResult<object?>(Problem(403, "invalid_client", "Niedozwolone żądanie."));
}
