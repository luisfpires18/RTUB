using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /logistics and /logistics/{id} ("Logística", React track 023, docs/react-logistics.md; were the Blazor pages).
/// Thin: every rule lives in <see cref="ILogisticsKanbanService"/>, decided from the session. Signed-in members, Leitões
/// refused unless they manage. Every write needs the antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class LogisticsEndpoints
{
    public static void MapLogisticsEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/logistics").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = api.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);
        var small = new RequestSizeLimitAttribute(64 * 1024);

        // Boards
        api.MapGet("/", async (string? q, HttpContext http, ILogisticsKanbanService s) => ToResult(await s.GetBoardsAsync(q, http.User)));
        api.MapGet("/events", async (string? q, HttpContext http, ILogisticsKanbanService s) => ToResult(await s.GetEventsAsync(q, http.User)));
        api.MapGet("/members", async (string? q, HttpContext http, ILogisticsKanbanService s) => ToResult(await s.SearchMembersAsync(q, http.User)));
        writes.MapPost("/boards", async (LogisticsBoardInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.CreateBoardAsync(input, http.User), b => Results.Created($"/api/logistics/boards/{b.Id}", b))).WithMetadata(small);
        writes.MapPut("/boards/{id:int}", async (int id, LogisticsBoardInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.UpdateBoardAsync(id, input, http.User))).WithMetadata(small);
        writes.MapPost("/boards/{id:int}/state", async (int id, LogisticsBoardStateInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.SetBoardStateAsync(id, input, http.User))).WithMetadata(small);
        writes.MapDelete("/boards/{id:int}", async (int id, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.DeleteBoardAsync(id, http.User), _ => Results.NoContent()));

        // One board and its lists
        api.MapGet("/boards/{id:int}", async (int id, HttpContext http, ILogisticsKanbanService s) => ToResult(await s.GetBoardAsync(id, http.User)));
        writes.MapPost("/boards/{id:int}/lists", async (int id, LogisticsListInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.CreateListAsync(id, input, http.User))).WithMetadata(small);
        writes.MapPut("/lists/{id:int}", async (int id, LogisticsListInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.RenameListAsync(id, input, http.User))).WithMetadata(small);
        writes.MapPost("/lists/{id:int}/move", async (int id, LogisticsPositionInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.MoveListAsync(id, input, http.User), _ => Results.NoContent())).WithMetadata(small);
        writes.MapDelete("/lists/{id:int}", async (int id, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.DeleteListAsync(id, http.User), _ => Results.NoContent()));

        // Cards
        writes.MapPost("/lists/{id:int}/cards", async (int id, LogisticsCardCreateInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.CreateCardAsync(id, input, http.User))).WithMetadata(small);
        api.MapGet("/cards/{id:int}", async (int id, HttpContext http, ILogisticsKanbanService s) => ToResult(await s.GetCardAsync(id, http.User)));
        writes.MapPut("/cards/{id:int}", async (int id, LogisticsCardInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.UpdateCardAsync(id, input, http.User))).WithMetadata(small);
        writes.MapPost("/cards/{id:int}/status", async (int id, LogisticsStatusInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.SetStatusAsync(id, input, http.User))).WithMetadata(small);
        writes.MapPost("/cards/{id:int}/move", async (int id, LogisticsMoveInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.MoveCardAsync(id, input, http.User), _ => Results.NoContent())).WithMetadata(small);
        writes.MapPut("/cards/{id:int}/labels", async (int id, LogisticsLabelsInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.SetLabelsAsync(id, input, http.User))).WithMetadata(small);
        writes.MapPut("/cards/{id:int}/checklist", async (int id, LogisticsChecklistInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.SetChecklistAsync(id, input, http.User))).WithMetadata(small);
        writes.MapPut("/cards/{id:int}/links", async (int id, LogisticsLinksInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.SetLinksAsync(id, input, http.User))).WithMetadata(small);
        writes.MapPost("/cards/{id:int}/assignments", async (int id, LogisticsAssignmentInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.AddAssignmentAsync(id, input, http.User))).WithMetadata(small);
        writes.MapDelete("/cards/{id:int}/assignments/{userId}", async (int id, string userId, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.RemoveAssignmentAsync(id, userId, http.User)));
        writes.MapDelete("/cards/{id:int}", async (int id, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.DeleteCardAsync(id, http.User), _ => Results.NoContent()));

        // Reminders (any member who sees the board) and board files
        writes.MapPost("/boards/{id:int}/reminders", async (int id, LogisticsReminderInput input, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.CreateReminderAsync(id, input, http.User))).WithMetadata(small);
        api.MapGet("/boards/{id:int}/files", async (int id, string? name, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.GetFileAsync(id, name, http.User)));
        writes.MapPost("/boards/{id:int}/files", async (int id, IFormCollection form, HttpContext http, ILogisticsKanbanService s) =>
            {
                if (form.Files.GetFile("file") is not { } file)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = new[] { "Escolha um ficheiro." } },
                        title: "Há campos por corrigir.");
                }

                await using var content = file.OpenReadStream();
                return ToResult(await s.UploadFileAsync(id, new LogisticsFileUpload(content, file.FileName, file.ContentType ?? string.Empty, file.Length), http.User));
            })
            .WithMetadata(new RequestSizeLimitAttribute(LogisticsKanbanService.MaxFileBytes + 64 * 1024));
        writes.MapDelete("/boards/{id:int}/files", async (int id, string? name, HttpContext http, ILogisticsKanbanService s) =>
            ToResult(await s.DeleteFileAsync(id, name, http.User), _ => Results.NoContent()));
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
