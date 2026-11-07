using System.Security.Claims;
using RTUB.Web.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The running build's release notes for signed-in members (task 083, issue #264; docs/release-and-rollback.md →
/// Release notes). Members only (401 to visitors), no-store, the acknowledgement needs the antiforgery token. Always
/// the caller's own account: no id in any request. Visitors never get the notes, so the dialog cannot show to them.
/// </summary>
public static class ReleaseEndpoints
{
    public static void MapReleaseEndpoints(this IEndpointRouteBuilder app)
    {
        var releases = app.MapGroup("/api/releases").AllowAnonymous()
            .AddEndpointFilter(EventEndpoints.NoStore).AddEndpointFilter(RequireMember);

        releases.MapGet("/notes", async (HttpContext http, ReleaseNotesService notes) =>
            await notes.GetAsync(UserId(http.User)) is { } state ? Results.Ok(state) : Results.Unauthorized());

        releases.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery)
            .MapPost("/notes/ack", async (ReleaseAckInput input, HttpContext http, ReleaseNotesService notes) =>
                await notes.AcknowledgeAsync(UserId(http.User), input.Version) switch
                {
                    true => Results.NoContent(),
                    false => Results.Unauthorized(),
                    null => Results.ValidationProblem(new Dictionary<string, string[]> { ["version"] = ["Esta não é a versão em execução."] },
                        title: "Há campos por corrigir."),
                })
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(1024));
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private static async ValueTask<object?> RequireMember(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        string.IsNullOrEmpty(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier))
            ? Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.", statusCode: StatusCodes.Status401Unauthorized)
            : await next(context);
}

/// <summary>POST /api/releases/notes/ack: the version the member just read; must be the running build's.</summary>
public sealed record ReleaseAckInput(string? Version);
