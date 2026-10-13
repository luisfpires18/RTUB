using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The member-area pages React took over in task 032 (docs/react-member-area.md): /profile (the member's own profile,
/// was the Blazor /member/profile), /members/map (was /member/map) and /hall-of-fame. Thin: every rule lives in
/// <see cref="IMyProfileService"/>, <see cref="IMemberMapService"/> and <see cref="IHallOfFameService"/>, decided from
/// the session. Signed-in members only, as the Blazor pages were. Every write needs the antiforgery token in the
/// X-CSRF-TOKEN header.
/// </summary>
public static class MemberAreaEndpoints
{
    public static void MapMemberAreaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/hall-of-fame", async (HttpContext http, IHallOfFameService service) =>
                ToResult(await service.GetAsync(http.User)))
            .AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);

        // A literal segment, so it wins over /api/members/{id}.
        app.MapGet("/api/members/map", async (HttpContext http, IMemberMapService service) =>
                ToResult(await service.GetAsync(http.User)))
            .AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);

        var me = app.MapGroup("/api/me").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);

        me.MapGet("/profile", async (HttpContext http, IMyProfileService profile) =>
            ToResult(await profile.GetAsync(http.User)));

        me.MapGet("/mentors", async (string? q, HttpContext http, IMyProfileService profile) =>
            ToResult(await profile.SearchMentorsAsync(q, http.User)));

        var writes = me.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        writes.MapPut("/profile/personal", async (MyPersonalInput input, HttpContext http, IMyProfileService profile) =>
                ToResult(await profile.UpdatePersonalAsync(input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapPut("/profile/tuna", async (MyTunaInput input, HttpContext http, IMyProfileService profile) =>
                ToResult(await profile.UpdateTunaAsync(input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        writes.MapPost("/instruments", async (MemberInstrumentInput input, HttpContext http, IMyProfileService profile) =>
                ToResult(await profile.AddInstrumentAsync(input.Instrument, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        writes.MapDelete("/instruments/{id:int}", async (int id, HttpContext http, IMyProfileService profile) =>
            ToResult(await profile.RemoveInstrumentAsync(id, http.User)));

        writes.MapPut("/instruments/{id:int}/primary", async (int id, HttpContext http, IMyProfileService profile) =>
            ToResult(await profile.SetPrimaryInstrumentAsync(id, http.User)));

        // Multipart: "photo", already cropped square in the browser.
        writes.MapPost("/photo", async (IFormCollection form, HttpContext http, IMyProfileService profile) =>
                form.Files.GetFile("photo") is { } photo
                    ? ToResult(await profile.SetPhotoAsync(
                        new MyPhotoUpload(photo.OpenReadStream(), photo.FileName, photo.ContentType ?? string.Empty, photo.Length), http.User))
                    : Results.ValidationProblem(new Dictionary<string, string[]> { ["photo"] = new[] { "Escolha uma foto." } },
                        title: "Há campos por corrigir."))
            .WithMetadata(new RequestSizeLimitAttribute(RTUB.Application.Services.MyProfileService.MaxPhotoBytes + 64 * 1024));

        writes.MapPut("/subscription", async (MySubscriptionInput input, HttpContext http, IMyProfileService profile) =>
                ToResult(await profile.SetSubscribedAsync(input.Subscribed, http.User), subscribed => Results.Ok(new { subscribed })))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        writes.MapPost("/password", async (MyPasswordInput input, HttpContext http, IMyProfileService profile,
                    UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) =>
                {
                    var result = await profile.ChangePasswordAsync(input, http.User);
                    if (result.Status == EventResultStatus.Ok && await users.GetUserAsync(http.User) is { } member)
                    {
                        // The change rotated the security stamp: a fresh cookie keeps this session signed in (the old
                        // page left it to expire at the next validation).
                        await signIn.RefreshSignInAsync(member);
                    }

                    return ToResult(result, _ => Results.NoContent());
                })
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.", statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: "Não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
