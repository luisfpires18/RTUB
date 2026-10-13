using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The member's own Web Push subscriptions, for the React /profile (task 038, docs/react-push-v2.md; replaces
/// PushController). Signed-in members only (401 otherwise), every answer no-store, every POST needs the antiforgery
/// token in X-CSRF-TOKEN. A member only ever reads, adds or removes their own subscriptions. Push is available to the
/// Owner always and to everyone else when WebPush:Enabled; sending needs the VAPID keys. Sending to members is not
/// here: the domain services and the Owner's /notifications (OwnerEndpoints) do it.
/// </summary>
public static class PushEndpoints
{
    public static void MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        var push = app.MapGroup("/api/push").AllowAnonymous()
            .AddEndpointFilter(EventEndpoints.NoStore).AddEndpointFilter(RequireMember);

        push.MapGet("/status", async (HttpContext http, IPushNotificationService service, IOptions<WebPushOptions> options) =>
            Results.Ok(await StatusAsync(http.User, service, options.Value)));

        var writes = push.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        writes.MapPost("/check", async (PushEndpointInput input, HttpContext http, IPushNotificationService service) =>
                string.IsNullOrWhiteSpace(input.Endpoint)
                    ? Invalid("endpoint", "Falta o endereço da subscrição.")
                    : Results.Ok(new { registered = await service.IsRegisteredAsync(UserId(http.User), input.Endpoint) }))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        writes.MapPost("/subscribe", async (PushSubscribeInput input, HttpContext http, IPushNotificationService service,
                    IOptions<WebPushOptions> options, ILoggerFactory logs) =>
                {
                    if (!HasAccess(http.User, options.Value))
                    {
                        return Results.Problem(title: "As notificações push ainda não estão disponíveis.", statusCode: StatusCodes.Status403Forbidden);
                    }

                    if (!service.IsConfigured())
                    {
                        return Results.Problem(title: "As notificações push não estão configuradas neste servidor.",
                            statusCode: StatusCodes.Status409Conflict, type: "push:not-configured");
                    }

                    var errors = PushSubscriptionRules.Validate(input.Endpoint, input.Keys?.P256dh, input.Keys?.Auth);
                    if (errors.Count > 0)
                    {
                        return Results.ValidationProblem(errors, title: "Há campos por corrigir.");
                    }

                    var userId = UserId(http.User);
                    var userName = http.User.Identity?.Name;
                    var isNew = await service.SubscribeAsync(userId, new PushSubscriptionDto
                    {
                        Endpoint = input.Endpoint!,
                        Keys = input.Keys!,
                        ExpirationTime = input.ExpirationTime is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime : null,
                    }, http.Request.Headers.UserAgent.ToString(), userName);

                    // The same device's previous endpoint (rotated by the browser, or replaced by a repair) goes with it.
                    if (!string.IsNullOrWhiteSpace(input.Replaces) && input.Replaces != input.Endpoint)
                    {
                        await service.RemoveForUserAsync(userId, input.Replaces);
                    }

                    await service.SetOptedOutAsync(userId, false);
                    var log = logs.CreateLogger(typeof(PushEndpoints));
                    if (isNew)
                    {
                        log.LogInformation("User {UserName} subscribed to push notifications", userName);
                    }
                    else
                    {
                        log.LogInformation("User {UserName} renewed a push subscription", userName);
                    }

                    return Results.Ok(await StatusAsync(http.User, service, options.Value));
                })
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        // Works even when push is switched off, so a member can always remove what is stored. Opted out once nothing
        // is left, so the service worker's endpoint-rotation handler does not bring a subscription back.
        writes.MapPost("/unsubscribe", async (PushUnsubscribeInput input, HttpContext http, IPushNotificationService service,
                    IOptions<WebPushOptions> options, ILoggerFactory logs) =>
                {
                    if (!input.All && string.IsNullOrWhiteSpace(input.Endpoint))
                    {
                        return Invalid("endpoint", "Falta o endereço da subscrição.");
                    }

                    var userId = UserId(http.User);
                    var left = await service.RemoveForUserAsync(userId, input.All ? null : input.Endpoint);
                    if (left == 0)
                    {
                        await service.SetOptedOutAsync(userId, true);
                    }

                    logs.CreateLogger(typeof(PushEndpoints)).LogInformation(
                        "User {UserName} turned off push notifications ({Scope}; {Left} left)", http.User.Identity?.Name,
                        input.All ? "every device" : "one device", left);

                    return Results.Ok(await StatusAsync(http.User, service, options.Value));
                })
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));
    }

    private static async Task<PushStatusDto> StatusAsync(ClaimsPrincipal user, IPushNotificationService service, WebPushOptions options)
    {
        var userId = UserId(user);
        var enabled = HasAccess(user, options);
        var configured = service.IsConfigured();

        return new PushStatusDto(
            enabled,
            configured,
            !enabled ? "disabled" : !configured ? "not-configured" : null,
            enabled && configured ? service.GetVapidPublicKey() : null,
            await service.IsOptedOutAsync(userId),
            await service.CountForUserAsync(userId));
    }

    private static bool HasAccess(ClaimsPrincipal user, WebPushOptions options) => user.IsInRole("Owner") || options.Enabled;

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, title: "Há campos por corrigir.");

    private static async ValueTask<object?> RequireMember(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        string.IsNullOrEmpty(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier))
            ? Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.", statusCode: StatusCodes.Status401Unauthorized)
            : await next(context);
}
