using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /logistics and /logistics/{id} ("Logística", docs/react-logistics.md).
/// Thin: every rule lives in <see cref="ILogisticsKanbanService"/>, decided from the session. Signed-in members (Leitões
/// included) read; assignees tick checklists and change status; managers do the rest. Every write needs the
/// antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class LogisticsEndpoints
{
    public static void MapLogisticsEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/logistics").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = api.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);
        var bodyLimit64Kb = new RequestSizeLimitAttribute(64 * 1024);

        // Boards
        api.MapGet("/", async (string? q, HttpContext http, ILogisticsKanbanService logistics) => ToResult(await logistics.GetBoardsAsync(q, http.User)));
        api.MapGet("/events", async (string? q, HttpContext http, ILogisticsKanbanService logistics) => ToResult(await logistics.GetEventsAsync(q, http.User)));
        api.MapGet("/members", async (string? q, int? boardId, HttpContext http, ILogisticsKanbanService logistics) => ToResult(await logistics.SearchMembersAsync(q, http.User, boardId)));
        api.MapGet("/templates", async (HttpContext http, ILogisticsKanbanService logistics) => ToResult(await logistics.GetTemplatesAsync(http.User)));
        writes.MapPost("/boards/compose", async (LogisticsComposeInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.ComposeBoardAsync(input, http.User), b => Results.Created($"/api/logistics/boards/{b.Id}", b))).WithMetadata(new RequestSizeLimitAttribute(512 * 1024));
        writes.MapPost("/boards/{id:int}/archive", async (int id, LogisticsArchiveInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.ArchiveBoardAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPost("/boards/{id:int}/duplicate", async (int id, LogisticsDuplicateInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.DuplicateBoardAsync(id, input, http.User), b => Results.Created($"/api/logistics/boards/{b.Id}", b))).WithMetadata(bodyLimit64Kb);
        writes.MapPost("/boards", async (LogisticsBoardInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.CreateBoardAsync(input, http.User), b => Results.Created($"/api/logistics/boards/{b.Id}", b))).WithMetadata(bodyLimit64Kb);
        writes.MapPut("/boards/{id:int}", async (int id, LogisticsBoardInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.UpdateBoardAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPost("/boards/{id:int}/state", async (int id, LogisticsBoardStateInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.SetBoardStateAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapDelete("/boards/{id:int}", async (int id, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.DeleteBoardAsync(id, http.User), _ => Results.NoContent()));

        // One board and its lists
        // files=false: the React page loads the storage listing on its own request (file-list), so the board does not wait for it.
        api.MapGet("/boards/{id:int}", async (int id, bool? files, HttpContext http, ILogisticsKanbanService logistics) => ToResult(await logistics.GetBoardAsync(id, http.User, files ?? true)));
        api.MapGet("/boards/{id:int}/file-list", async (int id, HttpContext http, ILogisticsKanbanService logistics) => ToResult(await logistics.GetFilesAsync(id, http.User)));
        writes.MapPost("/boards/{id:int}/lists", async (int id, LogisticsListInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.CreateListAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPut("/lists/{id:int}", async (int id, LogisticsListInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.RenameListAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPost("/lists/{id:int}/move", async (int id, LogisticsPositionInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.MoveListAsync(id, input, http.User), _ => Results.NoContent())).WithMetadata(bodyLimit64Kb);
        writes.MapDelete("/lists/{id:int}", async (int id, int? moveTo, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.DeleteListAsync(id, moveTo, http.User), _ => Results.NoContent()));

        // Cards
        writes.MapPost("/lists/{id:int}/cards", async (int id, LogisticsCardCreateInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.CreateCardAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        api.MapGet("/cards/{id:int}", async (int id, HttpContext http, ILogisticsKanbanService logistics) => ToResult(await logistics.GetCardAsync(id, http.User)));
        writes.MapPut("/cards/{id:int}", async (int id, LogisticsCardInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.UpdateCardAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPost("/cards/{id:int}/status", async (int id, LogisticsStatusInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.SetStatusAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPost("/cards/{id:int}/move", async (int id, LogisticsMoveInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.MoveCardAsync(id, input, http.User), _ => Results.NoContent())).WithMetadata(bodyLimit64Kb);
        writes.MapPut("/cards/{id:int}/labels", async (int id, LogisticsLabelsInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.SetLabelsAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        // The checklist one item at a time (add, rename, remove, tick); the text identifies the item.
        writes.MapPost("/cards/{id:int}/checklist", async (int id, LogisticsChecklistItemInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.AddChecklistItemAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPut("/cards/{id:int}/checklist/{index:int}", async (int id, int index, LogisticsChecklistItemInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.RenameChecklistItemAsync(id, index, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapDelete("/cards/{id:int}/checklist/{index:int}", async (int id, int index, string? task, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.RemoveChecklistItemAsync(id, index, task, http.User)));
        writes.MapPost("/cards/{id:int}/checklist/{index:int}", async (int id, int index, LogisticsChecklistToggleInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.ToggleChecklistItemAsync(id, index, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPut("/cards/{id:int}/links", async (int id, LogisticsLinksInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.SetLinksAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPost("/cards/{id:int}/assignments", async (int id, LogisticsAssignmentInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.AddAssignmentAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapDelete("/cards/{id:int}/assignments/{userId}", async (int id, string userId, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.RemoveAssignmentAsync(id, userId, http.User)));
        writes.MapPost("/cards/{id:int}/archive", async (int id, LogisticsArchiveInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.ArchiveCardAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPost("/cards/{id:int}/duplicate", async (int id, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.DuplicateCardAsync(id, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapDelete("/cards/{id:int}", async (int id, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.DeleteCardAsync(id, http.User), _ => Results.NoContent()));

        // The card's discussion and its own files and links
        writes.MapPost("/cards/{id:int}/comments", async (int id, LogisticsCommentInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.AddCommentAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPut("/comments/{id:int}", async (int id, LogisticsCommentInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.EditCommentAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapDelete("/comments/{id:int}", async (int id, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.DeleteCommentAsync(id, http.User)));
        writes.MapPost("/cards/{id:int}/attachments", async (int id, IFormCollection form, HttpContext http, ILogisticsKanbanService logistics) =>
            {
                if (form.Files.GetFile("file") is not { } file)
                {
                    return EndpointResults.Invalid("file", "Escolha um ficheiro.");
                }

                await using var content = file.OpenReadStream();
                return ToResult(await logistics.UploadAttachmentAsync(id, new LogisticsFileUpload(content, file.FileName, file.ContentType ?? string.Empty, file.Length), http.User));
            })
            .WithMetadata(new RequestSizeLimitAttribute(LogisticsKanbanService.MaxFileBytes + 64 * 1024));
        writes.MapPost("/cards/{id:int}/links", async (int id, LogisticsLinkInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.AddLinkAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        api.MapGet("/cards/{id:int}/attachments/{attachmentId:int}", async (int id, int attachmentId, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.GetAttachmentAsync(id, attachmentId, http.User)));
        writes.MapDelete("/cards/{id:int}/attachments/{attachmentId:int}", async (int id, int attachmentId, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.DeleteAttachmentAsync(id, attachmentId, http.User)));

        // A card's reminders (managers for board viewers; owner and collaborators for themselves) and "As minhas tarefas"
        writes.MapPost("/cards/{id:int}/reminders", async (int id, LogisticsReminderInput input, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.CreateReminderAsync(id, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapPut("/cards/{id:int}/reminders/{reminderId:int}", async (int id, int reminderId, LogisticsReminderInput input, HttpContext http,
            ILogisticsKanbanService logistics) => ToResult(await logistics.UpdateReminderAsync(id, reminderId, input, http.User))).WithMetadata(bodyLimit64Kb);
        writes.MapDelete("/cards/{id:int}/reminders/{reminderId:int}", async (int id, int reminderId, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.DeleteReminderAsync(id, reminderId, http.User)));
        api.MapGet("/my-tasks", async (HttpContext http, ILogisticsKanbanService logistics) => ToResult(await logistics.GetMyTasksAsync(http.User)));

        // Board files
        // Files by id ("f:name" / "l:year/name"); ?name= still works for older pages.
        api.MapGet("/boards/{id:int}/files", async (int id, string? file, string? name, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.GetFileAsync(id, file ?? name, http.User)));
        writes.MapPost("/boards/{id:int}/files", async (int id, IFormCollection form, HttpContext http, ILogisticsKanbanService logistics) =>
            {
                if (form.Files.GetFile("file") is not { } file)
                {
                    return EndpointResults.Invalid("file", "Escolha um ficheiro.");
                }

                await using var content = file.OpenReadStream();
                return ToResult(await logistics.UploadFileAsync(id, new LogisticsFileUpload(content, file.FileName, file.ContentType ?? string.Empty, file.Length), http.User));
            })
            .WithMetadata(new RequestSizeLimitAttribute(LogisticsKanbanService.MaxFileBytes + 64 * 1024));
        writes.MapDelete("/boards/{id:int}/files", async (int id, string? file, string? name, HttpContext http, ILogisticsKanbanService logistics) =>
            ToResult(await logistics.DeleteFileAsync(id, file ?? name, http.User), _ => Results.NoContent()));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) =>
        EndpointResults.ToResult(result, ok, special: status => status switch
        {
            EventResultStatus.Closed => Results.Problem(title: "Arquivado: só leitura.", statusCode: StatusCodes.Status409Conflict, type: "logistics:archived"),
            EventResultStatus.Conflict => Results.Problem(title: "O cartão mudou entretanto.", statusCode: StatusCodes.Status409Conflict, type: "logistics:conflict"),
            _ => null,
        });
}
