using System.Security.Claims;
using RTUB.Web.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The running build's release notes for signed-in members (docs/release-and-rollback.md →
/// Release notes). Members only (401 to visitors), no-store, the acknowledgement needs the antiforgery token. Always
/// the caller's own account: no id in any request. Visitors never get the notes, so the dialog cannot show to them.
/// </summary>
public static class ReleaseEndpoints
{
    public static void MapReleaseEndpoints(this IEndpointRouteBuilder app)
    {
        var releases = app.MapGroup("/api/releases").AllowAnonymous()
            .AddEndpointFilter(EventEndpoints.NoStore).AddEndpointFilter(EventEndpoints.RequireMember);

        releases.MapGet("/notes", async (HttpContext http, ReleaseNotesService notes) =>
            await notes.GetAsync(UserId(http.User)) is { } state ? Results.Ok(state) : EndpointResults.SignInRequired());

        releases.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery)
            .MapPost("/notes/ack", async (ReleaseAckInput input, HttpContext http, ReleaseNotesService notes) =>
                await notes.AcknowledgeAsync(UserId(http.User), input.Version) switch
                {
                    true => Results.NoContent(),
                    false => EndpointResults.SignInRequired(),
                    null => Results.ValidationProblem(new Dictionary<string, string[]> { ["version"] = ["Esta não é a versão em execução."] },
                        title: "Há campos por corrigir."),
                })
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(1024));
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

/// <summary>POST /api/releases/notes/ack: the version the member just read; must be the running build's.</summary>
public sealed record ReleaseAckInput(string? Version);
