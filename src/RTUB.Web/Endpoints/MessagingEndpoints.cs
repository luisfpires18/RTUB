using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// Messaging v2 for the React /messages (task 041, docs/react-messages.md) over the preserved v1 tables. Thin: every
/// rule lives in <see cref="IMessagingService"/>, decided from the session. Signed-in members only; a conversation the
/// caller does not take part in is a 404. No-store throughout; every write needs the X-CSRF-TOKEN header. Text only:
/// no endpoint takes a file.
/// </summary>
public static class MessagingEndpoints
{
    public static void MapMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        var conversations = app.MapGroup("/api/conversations").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var conversationWrites = conversations.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        conversations.MapGet("/", async (HttpContext http, IMessagingService service) =>
            ToResult(await service.GetConversationsAsync(http.User)));

        // The header badge (task 044): one number, polled from every member page.
        conversations.MapGet("/unread", async (HttpContext http, IMessagingService service) =>
            ToResult(await service.GetUnreadAsync(http.User)));

        conversations.MapGet("/recipients", async (HttpContext http, IMessagingService service) =>
            ToResult(await service.GetRecipientsAsync(http.User)));

        // ?before=<message id>: the page of messages older than that one.
        conversations.MapGet("/{id:int}", async (int id, int? before, HttpContext http, IMessagingService service) =>
            ToResult(await service.GetConversationAsync(id, before, http.User)));

        conversationWrites.MapPost("/", async (StartConversationRequest input, HttpContext http, IMessagingService service) =>
                ToResult(await service.StartConversationAsync(input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        conversationWrites.MapPost("/{id:int}/messages", async (int id, SendMessageRequest input, HttpContext http, IMessagingService service) =>
                ToResult(await service.SendAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        conversationWrites.MapPost("/{id:int}/read", async (int id, HttpContext http, IMessagingService service) =>
            ToResult(await service.MarkReadAsync(id, http.User), _ => Results.NoContent()));

        conversationWrites.MapPost("/{id:int}/mute", async (int id, HttpContext http, IMessagingService service) =>
            ToResult(await service.SetMutedAsync(id, true, http.User)));

        conversationWrites.MapPost("/{id:int}/unmute", async (int id, HttpContext http, IMessagingService service) =>
            ToResult(await service.SetMutedAsync(id, false, http.User)));

        var messageWrites = app.MapGroup("/api/messages").AllowAnonymous()
            .AddEndpointFilter(EventEndpoints.NoStore).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        messageWrites.MapPost("/{id:int}/reactions", async (int id, ReactionRequest input, HttpContext http, IMessagingService service) =>
                ToResult(await service.ReactAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        messageWrites.MapDelete("/{id:int}/reactions", async (int id, HttpContext http, IMessagingService service) =>
            ToResult(await service.RemoveReactionAsync(id, http.User)));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não podes escrever nesta conversa.", statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: "Conversa não encontrada.", statusCode: StatusCodes.Status404NotFound),
    };
}
